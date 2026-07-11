#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.World.Hud.Panel.Slots;
using Chaos.Client.Data.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Hud.Panel;

/// <summary>
///     Spell book panel (D key, Shift+D for secondary). Thin view that subscribes to
///     <see cref="ViewModel.SpellBook" /> change events and renders spell icons with Progressive-style cooldowns.
/// </summary>
public sealed class SpellBookPanel : PanelBase
{
    private const int MAX_SLOTS = 90;

    public SpellBookPanel(
        ControlPrefabSet hudPrefabSet,
        SkillBookPage page = SkillBookPage.Page1,
        Texture2D? background = null,
        int normalVisibleSlots = DEFAULT_VISIBLE_SLOTS,
        int columns = DEFAULT_COLUMNS,
        int? cellCount = null,
        int gridOffsetX = 8,
        bool drawSlotNumberOverlay = true,
        bool loadFallbackBackground = true,
        int? compactGridPadding = null)
        : base(
            hudPrefabSet,
            MAX_SLOTS,
            (int)page,
            columns,
            cellCount,
            gridOffsetX,
            background: background,
            normalVisibleSlots: normalVisibleSlots,
            drawSlotNumberOverlay: drawSlotNumberOverlay,
            loadFallbackBackground: loadFallbackBackground,
            compactGridPadding: compactGridPadding)
    {
        Name = page switch
        {
            SkillBookPage.Page1 => "SpellBook",
            SkillBookPage.Page2 => "SpellBookAlt",
            SkillBookPage.Page3 => "SpellBookWorld",
            _                   => "SpellBook"
        };

        WorldState.SpellBook.SlotChanged += OnSlotChanged;
        WorldState.SpellBook.Cleared += OnCleared;
    }

    protected override PanelSlot CreateSlot(byte slotNumber, string name)
        => new SpellSlot
        {
            Name = name,
            Slot = slotNumber
        };

    //base.Dispose() walks the children, and PanelSlot.Dispose drops its own cooldown textures
    public override void Dispose()
    {
        WorldState.SpellBook.SlotChanged -= OnSlotChanged;
        WorldState.SpellBook.Cleared -= OnCleared;

        base.Dispose();
    }

    /// <summary>
    ///     Returns the SpellSlot for a 1-based slot number, or null.
    /// </summary>
    public SpellSlot? GetSpellSlot(byte slot) => FindSlot(slot) as SpellSlot;

    private void OnCleared()
    {
        foreach (var slot in Slots)
        {
            slot.NormalTexture?.Dispose();
            slot.NormalTexture = null;
            slot.ClearCooldownTextures();
            slot.CooldownPercent = 0;
            slot.CooldownSecondsRemaining = 0;
            slot.SlotName = null;
        }
    }

    private void OnSlotChanged(byte slot)
    {
        var control = FindSlot(slot);

        if (control is null)
            return;

        var data = WorldState.SpellBook.GetSlot(slot);

        //the sprite may have changed, so the tinted overlays have to be rebuilt
        control.ClearCooldownTextures();

        if (data.IsOccupied)
        {
            control.NormalTexture?.Dispose();
            control.NormalTexture = RenderIcon(data.Sprite);

            if (control is SpellSlot spellSlot)
            {
                spellSlot.SpellType = data.SpellType;
                spellSlot.Prompt = data.Prompt ?? string.Empty;
                spellSlot.CastLines = data.CastLines;

                if (data.Chants is not null)
                    Array.Copy(data.Chants, spellSlot.Chants, Math.Min(data.Chants.Length, 10));
            }

            SetSlotName(slot, data.Name);
        } else
        {
            control.NormalTexture?.Dispose();
            control.NormalTexture = null;
            control.SlotName = null;
            control.CooldownPercent = 0;
            control.CooldownSecondsRemaining = 0;
            control.CurrentDurability = 0;
            control.MaxDurability = 0;
        }
    }

    protected override Texture2D RenderIcon(ushort spriteId) => UiRenderer.Instance!.GetSpellIcon(spriteId);

    private Texture2D RenderGreyIcon(ushort spriteId)
    {
        var cache = UiRenderer.Instance!;

        return cache.GetCooldownTintedTexture($"spell:{spriteId}", cache.GetSpellIcon(spriteId), LegendColors.DimGray);
    }

    private Texture2D RenderTintedIcon(ushort spriteId)
    {
        var cache = UiRenderer.Instance!;

        return cache.GetCooldownTintedTexture($"spell:{spriteId}", cache.GetSpellIcon(spriteId), LegendColors.CornflowerBlue);
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        //read cooldown state each frame — progressive style: grey base with blue overlay
        for (var i = 0; (i < VisibleSlotCount) && (i < Slots.Count); i++)
        {
            var slot = (byte)(i + SlotOffset + 1);
            var control = Slots[i];
            var cooldownPercent = WorldState.SpellBook.GetCooldownPercent(slot);

            if (cooldownPercent > 0)
            {
                //build the overlays once per cooldown, not once per frame
                if ((control.GreyTexture is null) || (control.CooldownTexture is null))
                {
                    var data = WorldState.SpellBook.GetSlot(slot);

                    if (data.IsOccupied && control.NormalTexture is not null)
                    {
                        control.GreyTexture ??= RenderGreyIcon(data.Sprite);
                        control.CooldownTexture ??= RenderTintedIcon(data.Sprite);
                    }
                }

                control.CooldownSecondsRemaining = WorldState.SpellBook.GetCooldownSecondsRemaining(slot);
            } else
                control.CooldownSecondsRemaining = 0;

            control.CooldownPercent = cooldownPercent;
        }
    }
}