#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.World.Hud.Panel;
using Chaos.Client.Controls.World.Hud.Panel.Slots;
using Chaos.Client.Data;
using Chaos.Client.Models;
using Chaos.Client.Systems;
using Chaos.Client.Systems.KeyBinds;
using Chaos.Client.ViewModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Hud;

/// <summary>
///     One of the four hotbars: a row (or column) of up to fifteen slots holding items, skills and spells, each used by
///     clicking it or by its key from F12.
/// </summary>
/// <remarks>
///     The bar draws its own slots rather than hosting <see cref="PanelSlot" />s. A panel slot is a view of one book's
///     slot number and swaps within its own panel; a hotbar slot is a view of a name (see <see cref="HotbarEntry" />)
///     that can come from any of three books, so it shares the panels' icons and cooldown tints but none of their
///     wiring.
///     <para />
///     The padlock in the top-right corner locks the bar; it is the same setting as F4's Lock hotbar. Unlocked, slots
///     are filled by dragging from the inventory, skill or spell panel, a hotbar slot dragged onto another swaps with
///     it, one dragged anywhere else is emptied, and the grip moves the bar. Locked, the bar stays where it is, nothing
///     goes on or comes off, and dragging a slot out uses it instead (see <see cref="HotbarDragPayload" />).
///     <para />
///     The grip at the start of the bar moves it, the same way the group panels' grab bar does, anywhere on the screen
///     as long as the whole bar stays on it; where it was left is kept per character. Locking never moves the bar.
///     Picking an edge from F4's Position dropdown docks the bar there, placed by the screen, until it is dragged away.
/// </remarks>
public sealed class HotbarControl : UIPanel
{
    /// <summary>
    ///     The icon area inside a slot's frame. The panels' 32-pixel icons are drawn scaled down into it whole -- the bars
    ///     are about four-fifths the panels' size -- and nothing is drawn over them but the labels.
    /// </summary>
    private const int ICON_SIZE = 26;

    /// <summary>A slot: the icon area and a one-pixel frame around the outside of it.</summary>
    private const int SLOT_SIZE = ICON_SIZE + 2;

    private const int SLOT_GAP = 1;
    private const int PADDING = 2;

    /// <summary>
    ///     The handle the bar is dragged by: this deep along a horizontal bar, a slot wide across it. Its space stays while
    ///     the bar is locked, only empty, so locking never shifts the slots.
    /// </summary>
    private const int GRIP_DEPTH = 6;

    /// <summary>
    ///     The padlock button, square. A horizontal bar has it at the far end; a vertical one has a header row this tall,
    ///     the grip on its left and the padlock on its right. Either way it is the bar's top-right corner.
    /// </summary>
    private const int LOCK_SIZE = 10;

    /// <summary>The bar's number, drawn pixel by pixel this size: under the padlock on a horizontal bar, beside it on a vertical one.</summary>
    private const int DIGIT_WIDTH = 3;

    private const int DIGIT_HEIGHT = 5;

    /// <summary>How see-through the backing is while <see cref="ClientSettings.SolidHotbars" /> is off.</summary>
    private const float TRANSPARENT_PLATE_ALPHA = 0.45f;

    /// <summary>How faint an icon is drawn while the player has none of what the slot holds.</summary>
    private const float MISSING_ICON_ALPHA = 0.35f;

    /// <summary>The longest key label drawn in a slot's corner. Longer ones would run over the icon; F12 still shows them.</summary>
    private const int MAX_KEY_LABEL_LENGTH = 4;

    private const int MAX_DISPLAY_SECONDS = 999;

    //the same plate colours as the group panels, so the two read as the same kind of window
    private static readonly Color PlateFill = new(18, 11, 5);
    private static readonly Color PlateBorder = new(54, 34, 16);
    private static readonly Color SlotFill = Color.Black * 0.5f;
    private static readonly Color SlotBorder = new(54, 34, 16);
    private static readonly Color DropBorder = new(214, 190, 146);
    private static readonly Color GripLine = new(120, 96, 64);
    private static readonly Color LabelText = new(214, 190, 146);
    private static readonly Color Shadow = Color.Black;
    private static readonly Color LockedColor = new(214, 190, 146);
    private static readonly Color UnlockedColor = new(120, 96, 64);

    /// <summary>The bar numbers 1-4 as 3 x 5 pixel glyphs, a row per string. The text font is too tall for a vertical bar's header.</summary>
    private static readonly string[][] BarDigits =
    [
        [".#.", "##.", ".#.", ".#.", "###"],
        ["##.", "..#", ".#.", "#..", "###"],
        ["##.", "..#", ".#.", "..#", "##."],
        ["#.#", "#.#", "###", "..#", "..#"]
    ];

    /// <summary>The whole window: a free bar can go anywhere, hud included, so long as all of it stays on screen.</summary>
    private readonly Rectangle ScreenArea;

    /// <summary>The world viewport, where a bar that has never been moved first appears.</summary>
    private Rectangle ViewportArea;

    private bool Placed;

    /// <summary>Docked last frame. A bar freed from its edge saves where it was left, so it is still there next login.</summary>
    private bool WasDocked;
    private bool Dragging;
    private int DragOffsetX;
    private int DragOffsetY;

    /// <summary>The left button went down on the padlock. Only a click that also started there toggles it.</summary>
    private bool LockPressed;

    /// <summary>The slot the left button went down on, or -1. A drag that starts carries this slot.</summary>
    private int PressedSlot = -1;

    /// <summary>The slot a drag is hovering over, outlined to show where it would land, or -1.</summary>
    private int DropSlot = -1;
    private readonly Dictionary<GameAction, string> KeyLabelCache = new();
    private int KeyLabelVersion = -1;

    public HotbarControl(int index, Rectangle screenArea, Rectangle viewportArea)
    {
        Index = index;
        ScreenArea = screenArea;
        ViewportArea = viewportArea;
        Name = $"Hotbar{index + 1}";

        //stays Visible so the parent keeps calling Update; whether anything is drawn is decided per frame, the same
        //way GroupPanelStack does it
        Visible = true;
        IsHitTestVisible = false;
    }

    /// <summary>Which bar this is, 0-3.</summary>
    public int Index { get; }

    /// <summary>Holds the bar down while a full-screen overlay is up, as the group panels are.</summary>
    public bool Suppressed { get; set; }

    /// <summary>
    ///     While a spell is waiting for a target the bar lets clicks through to the world under it, so a target behind the
    ///     bar can still be picked. Set by the screen each frame.
    /// </summary>
    public bool ClickThrough { get; set; }

    /// <summary>Fired when a slot is clicked. Arguments are the bar (0-3) and the slot (0-14).</summary>
    public event Action<int, int>? SlotUsed;

    private int SlotCount => Math.Clamp(ClientSettings.HotbarSlotCount[Index], 1, HotbarSet.MAX_SLOTS);

    /// <summary>The slots run down a column rather than along a row.</summary>
    public bool Vertical => ClientSettings.HotbarVertical[Index];

    /// <summary>
    ///     Locked by the padlock or F4's Lock hotbar, one setting: the bar stays where it is and cannot be dragged, nothing
    ///     goes on or comes off it, and a slot dragged out is used.
    /// </summary>
    public bool Locked => ClientSettings.HotbarLocked[Index];

    /// <summary>The edge picked from F4's Position dropdown, or <see cref="HotbarDock.None" /> for a free bar.</summary>
    public HotbarDock Edge => ClientSettings.HotbarDockEdge[Index];

    /// <summary>Docked: placed against <see cref="Edge" /> by the screen each frame (see <see cref="Dock" />).</summary>
    public bool IsDocked => Edge != HotbarDock.None;

    /// <summary>Shown when turned on in F4, once a character is in the world (the per-character files are ready).</summary>
    public bool IsShown => ClientSettings.HotbarVisible[Index] && !Suppressed && DataContext.LocalPlayerSettings.IsInitialized;

    /// <summary>
    ///     Where the slots start along the bar. Horizontal: past the grip. Vertical: past the header row the grip and
    ///     padlock share. Either way the grip's space stays while the bar is locked, so locking never moves a slot.
    /// </summary>
    private int SlotsStart => PADDING + (Vertical ? LOCK_SIZE : GRIP_DEPTH) + SLOT_GAP;

    private int SlotsSpan => (SlotCount * SLOT_SIZE) + ((SlotCount - 1) * SLOT_GAP);

    /// <summary>How long the bar is along its slots. A horizontal bar adds the padlock at its far end.</summary>
    private int Length => SlotsStart + SlotsSpan + (Vertical ? 0 : SLOT_GAP + LOCK_SIZE) + PADDING;

    private static int Thickness => SLOT_SIZE + (2 * PADDING);

    /// <summary>A slot's top-left corner, in the bar's own coordinates.</summary>
    private Point SlotOrigin(int slot)
    {
        var along = SlotsStart + (slot * (SLOT_SIZE + SLOT_GAP));

        return Vertical ? new Point(PADDING, along) : new Point(along, PADDING);
    }

    /// <summary>A vertical bar's grip leaves room at its right end for the bar number, next to the padlock.</summary>
    private Rectangle GripBounds => Vertical
        ? new Rectangle(PADDING, PADDING, SLOT_SIZE - LOCK_SIZE - SLOT_GAP - DIGIT_WIDTH - 2, LOCK_SIZE)
        : new Rectangle(PADDING, PADDING, GRIP_DEPTH, SLOT_SIZE);

    /// <summary>
    ///     Where the bar number's top-left pixel goes. Horizontal: under the padlock, in the column it heads. Vertical: in
    ///     the header row, just left of the padlock, its foot level with the padlock's body.
    /// </summary>
    private Point NumberOrigin => Vertical
        ? new Point(PADDING + SLOT_SIZE - LOCK_SIZE - SLOT_GAP - DIGIT_WIDTH - 1, PADDING + LOCK_SIZE - DIGIT_HEIGHT)
        : new Point(LockBounds.X + 3, PADDING + LOCK_SIZE + 3);

    /// <summary>The padlock, in the bar's top-right corner.</summary>
    private Rectangle LockBounds => Vertical
        ? new Rectangle(PADDING + SLOT_SIZE - LOCK_SIZE, PADDING, LOCK_SIZE, LOCK_SIZE)
        : new Rectangle(Length - PADDING - LOCK_SIZE, PADDING, LOCK_SIZE, LOCK_SIZE);

    /// <summary>The slot under a point in the bar's own coordinates, or -1 for the grip, the padlock, a gap or the padding.</summary>
    private int SlotAt(int localX, int localY)
    {
        for (var slot = 0; slot < SlotCount; slot++)
        {
            var origin = SlotOrigin(slot);

            if ((localX >= origin.X) && (localX < origin.X + SLOT_SIZE) && (localY >= origin.Y) && (localY < origin.Y + SLOT_SIZE))
                return slot;
        }

        return -1;
    }

    private int SlotAtScreen(int screenX, int screenY) => SlotAt(screenX - ScreenX, screenY - ScreenY);

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (!IsShown)
        {
            IsHitTestVisible = false;
            DropSlot = -1;

            //the bar stays Visible for Update's sake, so the rect it last drew in would still read as "over a window"
            //to the screen's drop checks
            ClipRect = Rectangle.Empty;

            return;
        }

        RefreshSize();

        //a drag that ended elsewhere leaves no outline behind
        if (InputDispatcher.Instance?.ActiveDragPayload is null)
            DropSlot = -1;

        //a docked bar is put in place by the screen (see Dock); only a free one finds its own spot
        if (IsDocked)
            WasDocked = true;
        else
        {
            if (!Placed)
                PlaceAtDefault();

            ClampIntoScreen();

            //just freed (dragged off its edge, or "Free" picked in F4): it stays where the edge had it, and is kept there
            if (WasDocked)
            {
                WasDocked = false;
                DataContext.LocalPlayerSettings.SaveHotbarPosition(Index, X, Y);
            }
        }

        IsHitTestVisible = !ClickThrough;
    }

    /// <summary>
    ///     Sizes the bar from its F4 settings. Slot count, orientation and the position lock (which takes the grip away)
    ///     can change at any moment; recomputing is two assignments.
    /// </summary>
    public void RefreshSize()
    {
        Width = Vertical ? Thickness : Length;
        Height = Vertical ? Length : Thickness;
    }

    /// <summary>The viewport changes with the hud layout; a bar never moved appears in the current one.</summary>
    public void SetViewportBounds(Rectangle bounds) => ViewportArea = bounds;

    /// <summary>Puts a docked bar at its place against its edge, worked out by the screen with the other docked bars.</summary>
    public void Dock(int x, int y)
    {
        X = x;
        Y = y;
        Placed = true;

        //a long vertical bar docked to a side can reach past the bottom; it stays on screen like a free one
        ClampIntoScreen();
    }

    /// <summary>
    ///     Puts the bar where this character left it, or, never moved, centred along the bottom of the viewport with each
    ///     bar stacked above the last. Not marked done until the per-character store is ready, as with the group panels.
    /// </summary>
    private void PlaceAtDefault()
    {
        var settings = DataContext.LocalPlayerSettings;

        if (settings.IsInitialized && settings.TryLoadHotbarPosition(Index, out var savedX, out var savedY))
        {
            Placed = true;
            X = savedX;
            Y = savedY;

            return;
        }

        X = ViewportArea.X + ((ViewportArea.Width - Width) / 2);
        Y = ViewportArea.Bottom - ((Index + 1) * (Thickness + SLOT_GAP)) - PADDING;
        Placed = settings.IsInitialized;
    }

    /// <summary>Keeps the whole bar on the screen: no part of it can be dragged, or left by a resize, past an edge.</summary>
    private void ClampIntoScreen()
    {
        X = Math.Clamp(X, ScreenArea.X, Math.Max(ScreenArea.X, ScreenArea.Right - Width));
        Y = Math.Clamp(Y, ScreenArea.Y, Math.Max(ScreenArea.Y, ScreenArea.Bottom - Height));
    }

    #region Mouse
    public override void OnMouseDown(MouseDownEvent e)
    {
        if (!IsShown)
            return;

        //the bar is a window: a press anywhere on it, any button, stays off the world underneath -- a right-click that
        //got through would walk the player to the tile under the bar
        e.Handled = true;

        if (e.Button != MouseButton.Left)
            return;

        var localX = e.ScreenX - ScreenX;
        var localY = e.ScreenY - ScreenY;

        if (LockBounds.Contains(localX, localY))
        {
            LockPressed = true;

            return;
        }

        if (!Locked && GripBounds.Contains(localX, localY))
        {
            Dragging = true;
            DragOffsetX = localX;
            DragOffsetY = localY;

            return;
        }

        PressedSlot = SlotAt(localX, localY);
    }

    public override void OnMouseMove(MouseMoveEvent e)
    {
        if (!Dragging)
            return;

        //dragging a docked bar takes it off its edge; from here it goes where it is put
        if (IsDocked)
        {
            ClientSettings.HotbarDockEdge[Index] = HotbarDock.None;
            ClientSettings.Save();
        }

        X = e.ScreenX - DragOffsetX;
        Y = e.ScreenY - DragOffsetY;
        ClampIntoScreen();
    }

    public override void OnMouseUp(MouseUpEvent e)
    {
        if (e.Button != MouseButton.Left)
            return;

        var wasDragging = Dragging;
        Dragging = false;

        //a release off the padlock ends its press here; on it, the click that follows reads the flag and clears it
        if (!LockBounds.Contains(e.ScreenX - ScreenX, e.ScreenY - ScreenY))
            LockPressed = false;

        //saved on release rather than on every move: dragging fires per frame, and this writes a file
        if (wasDragging)
            DataContext.LocalPlayerSettings.SaveHotbarPosition(Index, X, Y);
    }

    public override void OnClick(ClickEvent e)
    {
        if (e.Button != MouseButton.Left)
            return;

        e.Handled = true;

        var wasLockPressed = LockPressed;
        LockPressed = false;

        if (LockBounds.Contains(e.ScreenX - ScreenX, e.ScreenY - ScreenY))
        {
            if (wasLockPressed)
                ToggleLock();

            return;
        }

        var slot = SlotAtScreen(e.ScreenX, e.ScreenY);

        if (slot >= 0)
            SlotUsed?.Invoke(Index, slot);
    }

    /// <summary>The padlock is F4's Lock hotbar. Locking keeps the bar exactly where it is.</summary>
    private void ToggleLock()
    {
        ClientSettings.HotbarLocked[Index] = !Locked;
        ClientSettings.Save();
    }

    public override void OnMouseLeave()
    {
        base.OnMouseLeave();
        DropSlot = -1;
    }

    public override void ResetInteractionState()
    {
        Dragging = false;
        LockPressed = false;
        PressedSlot = -1;
        DropSlot = -1;
    }
    #endregion

    #region Drag and drop
    public override void OnDragStart(DragStartEvent e)
    {
        //the grip moves the bar and the padlock is a button; neither is a drag of anything
        if (Dragging || LockPressed || (PressedSlot < 0) || WorldState.Hotbars.Get(Index, PressedSlot) is not { } entry)
            return;

        e.Payload = new HotbarDragPayload
        {
            Bar = Index,
            Slot = PressedSlot,
            Entry = entry,
            KeepsSource = Locked,
            GhostTexture = IconFor(entry)
        };
    }

    public override void OnDragMove(DragMoveEvent e)
    {
        DropSlot = !Locked && Accepts(e.Payload) ? SlotAtScreen(e.ScreenX, e.ScreenY) : -1;
    }

    /// <summary>
    ///     A drop on the bar always ends here: on a slot of an unlocked bar it lands, anywhere else on the bar -- a gap, the
    ///     grip, a locked bar -- it is a "never mind". It is never passed on to the world under the bar, which would drop
    ///     an item on the ground there or empty the slot it came from.
    /// </summary>
    public override void OnDragDrop(DragDropEvent e)
    {
        DropSlot = -1;

        var slot = Locked ? -1 : SlotAtScreen(e.ScreenX, e.ScreenY);

        switch (e.Payload)
        {
            case HotbarDragPayload moved:
                e.Handled = true;

                if (slot < 0)
                    break;

                //from a locked bar the slot stays where it was, so this is a copy; from an unlocked one, a move
                if (moved.KeepsSource)
                {
                    if ((moved.Bar != Index) || (moved.Slot != slot))
                        WorldState.Hotbars.Set(Index, slot, moved.Entry);
                } else
                    WorldState.Hotbars.Swap(moved.Bar, moved.Slot, Index, slot);

                break;

            case SlotDragPayload panelDrag:
                if ((slot >= 0) && TryMakeEntry(panelDrag, out var entry))
                    WorldState.Hotbars.Set(Index, slot, entry);

                //the panel only learns the drag is over from its own slots or the root; a drop here is neither
                (panelDrag.Source.Parent as PanelBase)?.EndDrag();
                e.Handled = true;

                break;
        }
    }

    private static bool Accepts(object? payload)
        => payload is HotbarDragPayload || (payload is SlotDragPayload panelDrag && TryMakeEntry(panelDrag, out _));

    /// <summary>
    ///     What a slot dragged from one of the panels would put on the bar. False for anything that is not an item, skill
    ///     or spell -- the gold slot, an empty slot, any other panel's drag.
    /// </summary>
    private static bool TryMakeEntry(SlotDragPayload drag, out HotbarEntry entry)
    {
        entry = default;

        switch (drag.Source)
        {
            case SkillSlot:
                {
                    var data = WorldState.SkillBook.GetSlot(drag.SlotIndex);

                    if (!data.IsOccupied || string.IsNullOrEmpty(data.AbilityName))
                        return false;

                    entry = new HotbarEntry(HotbarEntryKind.Skill, data.AbilityName, data.Sprite);

                    return true;
                }

            case SpellSlot:
                {
                    var data = WorldState.SpellBook.GetSlot(drag.SlotIndex);

                    if (!data.IsOccupied || string.IsNullOrEmpty(data.AbilityName))
                        return false;

                    entry = new HotbarEntry(HotbarEntryKind.Spell, data.AbilityName, data.Sprite);

                    return true;
                }
        }

        if (drag.SourcePanel != HudTab.Inventory)
            return false;

        var item = WorldState.Inventory.GetSlot(drag.SlotIndex);

        if (Inventory.BaseName(in item) is not { } name)
            return false;

        entry = new HotbarEntry(HotbarEntryKind.Item, name, item.Sprite, item.Color);

        return true;
    }
    #endregion

    #region Drawing
    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!IsShown)
        {
            ClipRect = Rectangle.Empty;

            return;
        }

        UpdateClipRect();

        if ((ClipRect.Width <= 0) || (ClipRect.Height <= 0))
            return;

        var bounds = new Rectangle(ScreenX, ScreenY, Width, Height);
        var fill = ClientSettings.SolidHotbars ? PlateFill : PlateFill * TRANSPARENT_PLATE_ALPHA;

        DrawRect(spriteBatch, bounds, fill);
        DrawBorder(spriteBatch, bounds, PlateBorder);

        if (!Locked)
            DrawGrip(spriteBatch);

        DrawPadlock(spriteBatch);
        DrawBarNumber(spriteBatch);

        for (var slot = 0; slot < SlotCount; slot++)
            DrawSlot(spriteBatch, slot);
    }

    /// <summary>Three short lines across the handle, so it reads as something to take hold of.</summary>
    private void DrawGrip(SpriteBatch spriteBatch)
    {
        var grip = GripBounds;
        grip.Offset(ScreenX, ScreenY);

        for (var line = 0; line < 3; line++)
        {
            var offset = 1 + (line * 2);

            var rect = Vertical
                ? new Rectangle(grip.X + 3, grip.Y + offset + 2, grip.Width - 6, 1)
                : new Rectangle(grip.X + offset, grip.Y + 5, 1, grip.Height - 10);

            DrawRect(spriteBatch, rect, GripLine);
        }
    }

    /// <summary>
    ///     A small padlock: closed and bright while the bar is locked, open (the shackle's right side lifted off the
    ///     body) and dim while they are not.
    /// </summary>
    private void DrawPadlock(SpriteBatch spriteBatch)
    {
        var button = LockBounds;
        button.Offset(ScreenX, ScreenY);

        var locked = Locked;
        var color = locked ? LockedColor : UnlockedColor;

        //body: 8 x 5 along the bottom of the button
        DrawRect(spriteBatch, new Rectangle(button.X + 1, button.Y + 5, 8, 5), color);

        //shackle: an arch over the body, raised a pixel when open
        var top = locked ? button.Y + 1 : button.Y;

        DrawRect(spriteBatch, new Rectangle(button.X + 2, top, 6, 1), color);
        DrawRect(spriteBatch, new Rectangle(button.X + 2, top, 1, button.Y + 5 - top), color);

        //closed, the right side reaches the body; open, it stops short and leaves a gap
        DrawRect(spriteBatch, new Rectangle(button.X + 7, top, 1, locked ? button.Y + 5 - top : 2), color);
    }

    /// <summary>Which bar this is, 1-4, small and shadowed so it reads on the see-through backing.</summary>
    private void DrawBarNumber(SpriteBatch spriteBatch)
    {
        if (Index >= BarDigits.Length)
            return;

        var glyph = BarDigits[Index];
        var origin = NumberOrigin;
        var x = ScreenX + origin.X;
        var y = ScreenY + origin.Y;

        for (var row = 0; row < DIGIT_HEIGHT; row++)
            for (var column = 0; column < DIGIT_WIDTH; column++)
                if (glyph[row][column] == '#')
                    DrawRect(spriteBatch, new Rectangle(x + column + 1, y + row + 1, 1, 1), Shadow);

        for (var row = 0; row < DIGIT_HEIGHT; row++)
            for (var column = 0; column < DIGIT_WIDTH; column++)
                if (glyph[row][column] == '#')
                    DrawRect(spriteBatch, new Rectangle(x + column, y + row, 1, 1), LabelText);
    }

    private void DrawSlot(SpriteBatch spriteBatch, int slot)
    {
        var origin = SlotOrigin(slot);
        var frame = new Rectangle(ScreenX + origin.X, ScreenY + origin.Y, SLOT_SIZE, SLOT_SIZE);

        //the frame goes around the icon area, not over it, so an icon's edges and the key label are never covered
        var rect = new Rectangle(frame.X + 1, frame.Y + 1, ICON_SIZE, ICON_SIZE);

        DrawBorder(spriteBatch, frame, slot == DropSlot ? DropBorder : SlotBorder);
        DrawRect(spriteBatch, rect, SlotFill);

        if (WorldState.Hotbars.Get(Index, slot) is { } entry)
            DrawEntry(spriteBatch, entry, rect);

        DrawKeyLabel(spriteBatch, slot, rect);
    }

    private void DrawEntry(SpriteBatch spriteBatch, HotbarEntry entry, Rectangle rect)
    {
        var icon = IconFor(entry);

        if (icon is null)
            return;

        //scaled down into the icon area whole
        var position = rect;

        switch (entry.Kind)
        {
            case HotbarEntryKind.Item:
                {
                    var count = WorldState.Inventory.CountOf(entry.Name);

                    DrawScaled(spriteBatch, icon, position, null, count > 0 ? Color.White : Color.White * MISSING_ICON_ALPHA);

                    if (count > 1)
                        DrawCount(spriteBatch, count, rect);

                    return;
                }

            case HotbarEntryKind.Skill:
                {
                    var bookSlot = WorldState.SkillBook.FindSlotByAbilityName(entry.Name);

                    if (bookSlot == 0)
                    {
                        DrawScaled(spriteBatch, icon, position, null, Color.White * MISSING_ICON_ALPHA);

                        return;
                    }

                    DrawWithCooldown(
                        spriteBatch,
                        icon,
                        $"skill:{WorldState.SkillBook.GetSlot(bookSlot).Sprite}",
                        position,
                        WorldState.SkillBook.GetCooldownPercent(bookSlot),
                        WorldState.SkillBook.GetCooldownSecondsRemaining(bookSlot));

                    return;
                }

            case HotbarEntryKind.Spell:
                {
                    var bookSlot = WorldState.SpellBook.FindSlotByAbilityName(entry.Name);

                    if (bookSlot == 0)
                    {
                        DrawScaled(spriteBatch, icon, position, null, Color.White * MISSING_ICON_ALPHA);

                        return;
                    }

                    DrawWithCooldown(
                        spriteBatch,
                        icon,
                        $"spell:{WorldState.SpellBook.GetSlot(bookSlot).Sprite}",
                        position,
                        WorldState.SpellBook.GetCooldownPercent(bookSlot),
                        WorldState.SpellBook.GetCooldownSecondsRemaining(bookSlot));

                    return;
                }
        }
    }

    /// <summary>
    ///     The panels' cooldown look: a grey icon with the blue copy revealed top to bottom as the cooldown runs out, and the
    ///     seconds left over it. The tinted copies come from the same cache keys the panels use, so they are shared.
    /// </summary>
    private void DrawWithCooldown(
        SpriteBatch spriteBatch,
        Texture2D icon,
        string cacheKey,
        Rectangle destination,
        float cooldownPercent,
        int secondsRemaining)
    {
        var renderer = UiRenderer.Instance;

        if ((cooldownPercent <= 0) || renderer is null)
        {
            DrawScaled(spriteBatch, icon, destination, null, Color.White);

            return;
        }

        var grey = renderer.GetCooldownTintedTexture(cacheKey, icon, LegendColors.DimGray);
        var blue = renderer.GetCooldownTintedTexture(cacheKey, icon, LegendColors.CornflowerBlue);

        DrawScaled(spriteBatch, grey, destination, null, Color.White);

        var revealHeight = (int)(blue.Height * (1f - cooldownPercent));

        if (revealHeight > 0)
        {
            var revealed = destination with { Height = destination.Height * revealHeight / blue.Height };

            DrawScaled(spriteBatch, blue, revealed, new Rectangle(0, 0, blue.Width, revealHeight), Color.White);
        }

        if (!ClientSettings.CooldownNumbersEnabled || (secondsRemaining <= 0))
            return;

        var text = Math.Min(secondsRemaining, MAX_DISPLAY_SECONDS)
                       .ToString();
        var width = CooldownNumberFont.MeasureWidth(text);

        CooldownNumberFont.Draw(
            spriteBatch,
            text,
            destination.X + ((destination.Width - width) / 2),
            destination.Y + ((destination.Height - CooldownNumberFont.GlyphHeight) / 2));
    }

    /// <summary>
    ///     Draws <paramref name="source" /> of a texture (all of it when null) stretched into <paramref name="destination" />.
    ///     The base class only draws at a texture's own size; the bars draw the panels' icons smaller.
    /// </summary>
    private static void DrawScaled(SpriteBatch spriteBatch, Texture2D texture, Rectangle destination, Rectangle? source, Color color)
    {
        if ((destination.Width <= 0) || (destination.Height <= 0))
            return;

        var part = source ?? new Rectangle(0, 0, texture.Width, texture.Height);

        if (texture is CachedTexture2D { AtlasRegion: { } region })
        {
            spriteBatch.Draw(region.Atlas, destination, part with { X = region.SourceRect.X + part.X, Y = region.SourceRect.Y + part.Y }, color);

            return;
        }

        spriteBatch.Draw(texture, destination, part, color);
    }

    /// <summary>How many of a stacked item the player carries, in the slot's bottom-right corner.</summary>
    private static void DrawCount(SpriteBatch spriteBatch, uint count, Rectangle rect)
    {
        var text = count > 999 ? "999+" : count.ToString();
        var width = TextRenderer.MeasureWidth(text);

        TextRenderer.DrawShadowedText(
            spriteBatch,
            new Vector2(rect.Right - width - 2, rect.Bottom - TextRenderer.CHAR_HEIGHT - 1),
            text,
            LabelText,
            Shadow,
            false);
    }

    /// <summary>
    ///     The slot's key from F12, in its top-left corner, so the player can see what to press. Modifiers are shortened
    ///     to a letter (S, C, A); a label that still will not fit is left off rather than drawn over the icon.
    /// </summary>
    private void DrawKeyLabel(SpriteBatch spriteBatch, int slot, Rectangle rect)
    {
        var label = CachedKeyLabel(GameActions.HotbarAction(Index, slot));

        if (string.IsNullOrEmpty(label))
            return;

        TextRenderer.DrawShadowedText(
            spriteBatch,
            new Vector2(rect.X + 1, rect.Y),
            label,
            LabelText,
            Shadow,
            false);
    }

    /// <summary>The slot's key label, cached while the bindings are unchanged so the per-frame draw never re-runs the chord iterator.</summary>
    private string CachedKeyLabel(GameAction action)
    {
        var version = KeyBindings.Current.Version;

        if (version != KeyLabelVersion)
        {
            KeyLabelCache.Clear();
            KeyLabelVersion = version;
        }

        if (!KeyLabelCache.TryGetValue(action, out var label))
        {
            label = KeyLabel(action);
            KeyLabelCache[action] = label;
        }

        return label;
    }

    /// <summary>The compact label for an action's first bound key, or empty when it has none or the label is too long.</summary>
    public static string KeyLabel(GameAction action)
    {
        var chord = KeyBindings.Current.ChordsFor(action)
                               .FirstOrDefault();

        if (chord.IsNone)
            return string.Empty;

        var label = (chord.Ctrl ? "C" : string.Empty) + (chord.Alt ? "A" : string.Empty) + (chord.Shift ? "S" : string.Empty) + KeyNames.Get(chord.Key);

        return label.Length <= MAX_KEY_LABEL_LENGTH ? label : string.Empty;
    }

    /// <summary>
    ///     The icon a slot shows: the current one while the player holds it (a skill's icon can change as it ranks up),
    ///     otherwise the one remembered when it was put on the bar.
    /// </summary>
    private static Texture2D? IconFor(HotbarEntry entry)
    {
        var renderer = UiRenderer.Instance;

        if (renderer is null)
            return null;

        switch (entry.Kind)
        {
            case HotbarEntryKind.Skill:
                {
                    var bookSlot = WorldState.SkillBook.FindSlotByAbilityName(entry.Name);
                    var sprite = bookSlot > 0 ? WorldState.SkillBook.GetSlot(bookSlot).Sprite : entry.Sprite;

                    return renderer.GetSkillIcon(sprite);
                }

            case HotbarEntryKind.Spell:
                {
                    var bookSlot = WorldState.SpellBook.FindSlotByAbilityName(entry.Name);
                    var sprite = bookSlot > 0 ? WorldState.SpellBook.GetSlot(bookSlot).Sprite : entry.Sprite;

                    return renderer.GetSpellIcon(sprite);
                }

            default:
                return renderer.GetItemIcon(entry.Sprite, entry.Color);
        }
    }
    #endregion
}
