#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.World.Hud;
using Chaos.Client.Controls.World.Hud.Panel.Slots;
using Chaos.Client.Controls.World.Popups.GroupPanels;
using Chaos.Client.Data;
using Chaos.Client.Models;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Screens;

/// <summary>
///     The four hotbars: building them, placing and layering them each frame, using a slot by click, key or drag, and
///     keeping what they hold in the character's <c>Hotbars.cfg</c>.
/// </summary>
public sealed partial class WorldScreen
{
    /// <summary>
    ///     The bars' usual layer: the hud's. Added after the hud, they draw over it -- a bar can be dragged onto the orange
    ///     bar or the panels -- and under everything at 0 and up: dialogs, F12, the group window, the group panels.
    /// </summary>
    private const int HOTBAR_Z_INDEX = -1;

    /// <summary>
    ///     The layer a bar drops to while one of the windows that sit under the hud (F4, the options menu, the world list,
    ///     the boards, at -2 and -3) is open over it, so the window is drawn, and clicked, over the bar.
    /// </summary>
    private const int HOTBAR_UNDER_Z_INDEX = -4;

    /// <summary>Pixels between two locked bars stacked on the same edge, and between a bar and its edge.</summary>
    private const int HOTBAR_STACK_GAP = 2;

    private readonly HotbarControl[] HotbarBars = new HotbarControl[HotbarSet.BAR_COUNT];

    /// <summary>
    ///     How high the bars locked to the bottom reach, as a y, or null when none are. The status strips (class
    ///     resource, song, oxygen) stack above this instead of the hud's baseline, so the bars sit between them and the
    ///     orange bar. Worked out in <see cref="LayoutHotbars" /> each frame.
    /// </summary>
    private int? HotbarBottomStackTop;

    private void WireHotbars() => WorldState.Hotbars.Changed += SaveHotbars;

    private void UnwireHotbars()
    {
        WorldState.Hotbars.Changed -= SaveHotbars;

        foreach (var bar in HotbarBars)
            if (bar is not null)
                bar.SlotUsed -= UseHotbarSlot;
    }

    /// <summary>
    ///     Builds the bars and puts them on the root. Must run after the hud is added: a bar shares the hud's layer and
    ///     draws over it only by coming later.
    /// </summary>
    private void BuildHotbars()
    {
        var screen = new Rectangle(0, 0, ChaosGame.VIRTUAL_WIDTH, ChaosGame.VIRTUAL_HEIGHT);

        for (var i = 0; i < HotbarBars.Length; i++)
        {
            var bar = new HotbarControl(i, screen, WorldHud.ViewportBounds)
            {
                ZIndex = HOTBAR_Z_INDEX
            };

            bar.SlotUsed += UseHotbarSlot;
            HotbarBars[i] = bar;
            Root!.AddChild(bar);
        }
    }

    /// <summary>
    ///     Per frame, after the root has updated: lets clicks through the bars and the group panel while a spell waits for a
    ///     target, places the position-locked bars against their edges, and puts any bar a low window covers under it.
    /// </summary>
    /// <remarks>
    ///     Bars locked to the same edge stack outward from it in bar order. Bottom and top bars are centred along the
    ///     edge. Left and right bars sit where the group panel first appears -- in from the edge past the vines in the map
    ///     art, and down past the system messages. The bottom stack starts from the same baseline as the status strips --
    ///     the top of the orange bar, or of an expanded inventory or ability panel -- and the strips then stack above it.
    /// </remarks>
    private void LayoutHotbars()
    {
        var viewport = WorldHud.ViewportBounds;
        var clickThrough = CastingSystem.IsTargeting;

        GroupVitals.ClickThrough = clickThrough;

        var bottom = StatusStripBaseline() - HOTBAR_STACK_GAP;
        var top = viewport.Y + HOTBAR_STACK_GAP;
        var left = viewport.X + GroupPanelStack.DEFAULT_LEFT_MARGIN;
        var right = viewport.Right - GroupPanelStack.DEFAULT_LEFT_MARGIN;
        var sideY = viewport.Y + GroupPanelStack.DEFAULT_TOP_GAP;
        var anyBottom = false;

        foreach (var bar in HotbarBars)
        {
            bar.SetViewportBounds(viewport);
            bar.ClickThrough = clickThrough;

            if (bar.IsShown && bar.PositionLocked)
            {
                bar.RefreshSize();

                var centredX = viewport.X + ((viewport.Width - bar.Width) / 2);

                switch (bar.Edge)
                {
                    case HotbarDock.Bottom:
                        bottom -= bar.Height;
                        bar.Dock(centredX, bottom);
                        bottom -= HOTBAR_STACK_GAP;
                        anyBottom = true;

                        break;

                    case HotbarDock.Top:
                        bar.Dock(centredX, top);
                        top += bar.Height + HOTBAR_STACK_GAP;

                        break;

                    case HotbarDock.Left:
                        bar.Dock(left, sideY);
                        left += bar.Width + HOTBAR_STACK_GAP;

                        break;

                    case HotbarDock.Right:
                        right -= bar.Width;
                        bar.Dock(right, sideY);
                        right -= HOTBAR_STACK_GAP;

                        break;
                }
            }

            SetHotbarLayer(bar, bar.IsShown && IsUnderLowWindow(bar));
        }

        HotbarBottomStackTop = anyBottom ? bottom + HOTBAR_STACK_GAP : null;
    }

    /// <summary>True when a window drawn under the hud (ZIndex below it) is open over any part of the bar.</summary>
    private bool IsUnderLowWindow(HotbarControl bar)
    {
        var bounds = bar.ScreenBounds;

        foreach (var child in Root!.Children)
            if ((child.ZIndex < HOTBAR_Z_INDEX) && child.Visible && child is not HotbarControl && child.ScreenBounds.Intersects(bounds))
                return true;

        return false;
    }

    /// <summary>
    ///     Moves a bar under the low windows, or back up to the hud's layer. Going back up, it is taken off the root and
    ///     added again: the root's sort keeps equal layers in list order, and only coming after the hud in the list puts
    ///     the bar over it.
    /// </summary>
    private void SetHotbarLayer(HotbarControl bar, bool under)
    {
        if (under)
        {
            bar.ZIndex = HOTBAR_UNDER_Z_INDEX;

            return;
        }

        if (bar.ZIndex == HOTBAR_Z_INDEX)
            return;

        Root!.Children.Remove(bar);
        bar.ZIndex = HOTBAR_Z_INDEX;
        Root.AddChild(bar);
    }

    /// <summary>The world map is a full-screen overlay; the bars stand down under it, as the group panels do.</summary>
    private void SuppressHotbars(bool suppressed)
    {
        foreach (var bar in HotbarBars)
            if (bar is not null)
                bar.Suppressed = suppressed;
    }

    /// <summary>Reads this character's hotbars. Called once per character, when their settings load.</summary>
    private void LoadHotbars() => WorldState.Hotbars.LoadLines(DataContext.LocalPlayerSettings.LoadHotbarLines());

    private void SaveHotbars() => DataContext.LocalPlayerSettings.SaveHotbarLines(WorldState.Hotbars.ToLines());

    /// <summary>
    ///     Uses what hotbar slot <paramref name="slot" /> of bar <paramref name="bar" /> holds, the same way clicking it in
    ///     its own panel would: an item is used, a skill is used (with its chant), a spell starts casting. Nothing happens
    ///     when the slot is empty or the player no longer has what it holds.
    /// </summary>
    private void UseHotbarSlot(int bar, int slot)
    {
        if (WorldState.Hotbars.Get(bar, slot) is not { } entry)
            return;

        switch (entry.Kind)
        {
            case HotbarEntryKind.Item:
                {
                    var inventorySlot = WorldState.Inventory.FindSlot(entry.Name);

                    if (inventorySlot > 0)
                        HandleInventorySlotClicked(inventorySlot);

                    break;
                }

            case HotbarEntryKind.Skill:
                {
                    var skillSlot = WorldState.SkillBook.FindSlotByAbilityName(entry.Name);

                    if (skillSlot > 0)
                        HandleSkillSlotClicked(skillSlot);

                    break;
                }

            case HotbarEntryKind.Spell:
                {
                    if (FindHotbarSpellSlot(entry) is { } control)
                        TryBeginCast(control);

                    break;
                }
        }
    }

    /// <summary>
    ///     A hotbar slot dropped somewhere no bar took it. From an unlocked bar the slot is emptied. From a locked bar it
    ///     stays, and the drop uses what it holds just as dragging it out of its own panel would: an item goes to whoever
    ///     or whatever it was dropped on (a player or creature, a trade window, the ground), a spell is cast on what it
    ///     landed on. A skill has nothing to be dropped on, so it does nothing.
    /// </summary>
    private void DropHotbarSlot(HotbarDragPayload payload, int screenX, int screenY)
    {
        if (!payload.KeepsSource)
        {
            WorldState.Hotbars.Set(payload.Bar, payload.Slot, null);

            return;
        }

        switch (payload.Entry.Kind)
        {
            case HotbarEntryKind.Item:
                {
                    var inventorySlot = WorldState.Inventory.FindSlot(payload.Entry.Name);

                    if (inventorySlot > 0)
                        HandleInventoryDropInViewport(inventorySlot, screenX, screenY);

                    break;
                }

            case HotbarEntryKind.Spell:
                {
                    if (FindHotbarSpellSlot(payload.Entry) is { } control)
                        CastSpellAtDrop(control, screenX, screenY);

                    break;
                }
        }
    }

    /// <summary>
    ///     True while a ground-targeted spell is being dragged off a locked bar, which aims it at a tile the same as dragging
    ///     it out of the spell panel does.
    /// </summary>
    private bool IsDraggingHotbarGroundSpell
        => Game.Dispatcher.ActiveDragPayload is HotbarDragPayload { KeepsSource: true, Entry.Kind: HotbarEntryKind.Spell } payload
           && FindHotbarSpellSlot(payload.Entry) is { SpellType: SpellType.GroundTargeted };

    /// <summary>True while an item is being dragged off a locked bar, which gives or drops it like an inventory drag.</summary>
    private bool IsDraggingHotbarItem
        => Game.Dispatcher.ActiveDragPayload is HotbarDragPayload { KeepsSource: true, Entry.Kind: HotbarEntryKind.Item };

    /// <summary>The spell panel slot holding a hotbar spell, whichever page it is on, or null when the player no longer has it.</summary>
    private SpellSlot? FindHotbarSpellSlot(HotbarEntry entry)
    {
        var spellSlot = WorldState.SpellBook.FindSlotByAbilityName(entry.Name);

        return spellSlot > 0 ? FindSpellSlotControl(spellSlot) : null;
    }

    /// <summary>
    ///     The spell panel slot for a spell book slot, whichever page it is on. Unlike <c>ResolveSpellSlot</c>, which
    ///     prefers the open tab, a hotbar knows the exact slot and works whatever panel is showing.
    /// </summary>
    private SpellSlot? FindSpellSlotControl(byte slot)
        => WorldHud.SpellBook.GetSpellSlot(slot) ?? WorldHud.SpellBookAlt.GetSpellSlot(slot) ?? WorldHud.Tools.WorldSpells.GetSpellSlot(slot);
}
