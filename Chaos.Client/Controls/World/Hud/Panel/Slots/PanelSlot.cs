#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Models;
using Chaos.Client.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Hud.Panel.Slots;

/// <summary>
///     A single slot in an icon grid panel (inventory, skill book, spell book). Extends UIButton with cooldown overlay
///     rendering, double-click detection, and drag-and-drop support. The parent panel creates one PanelSlotControl per
///     visible grid cell and manages layout, slot assignment, and drag state.
/// </summary>
public class PanelSlot : UIButton
{
    //three digits is what CooldownNumberFont fits inside a 32px slot icon; a longer cooldown parks here until it ticks
    //down into range
    private const int MAX_DISPLAY_SECONDS = 999;

    //the readout is redrawn every frame but only changes once a second — pre-render the strings so Draw allocates none
    private static readonly string[] SecondsText
        = [..Enumerable.Range(0, MAX_DISPLAY_SECONDS + 1).Select(static seconds => seconds.ToString())];

    private bool DoubleClickFired;

    /// <summary>
    ///     Cooldown progress from 0 (fully cooled down) to 1 (just started, fully on cooldown).
    /// </summary>
    public float CooldownPercent { get; set; }

    /// <summary>
    ///     Whole seconds left on this slot's cooldown, rounded up. Zero when ready. Drawn over the icon when
    ///     <see cref="ClientSettings.CooldownNumbersEnabled" /> is set. Panels that have no cooldown concept (inventory)
    ///     leave this at zero and get no readout.
    /// </summary>
    public int CooldownSecondsRemaining { get; set; }

    /// <summary>
    ///     Blue-tinted copy of the normal icon used as the cooldown overlay. Progressively revealed top-to-bottom over
    ///     the grey base as the cooldown elapses. Only the skill/spell panels set this; a slot without one (inventory,
    ///     tools) draws no cooldown treatment at all.
    /// </summary>
    public Texture2D? CooldownTexture { get; set; }

    public int CurrentDurability { get; set; }

    /// <summary>
    ///     Grey-tinted copy of the normal icon shown underneath the cooldown overlay.
    /// </summary>
    public Texture2D? GreyTexture { get; set; }

    public bool IsDropTarget { get; set; }

    public int MaxDurability { get; set; }

    /// <summary>
    ///     The 1-based slot number this control represents.
    /// </summary>
    public byte Slot { get; init; }

    /// <summary>
    ///     Display name of the item/skill/spell in this slot. Used for hover tooltips by the parent.
    /// </summary>
    public string? SlotName { get; set; }

    /// <summary>
    ///     Drops both cooldown overlays. Called when the slot's sprite changes, the book is cleared, or the slot is
    ///     disposed — they are rebuilt lazily on the next frame the slot is on cooldown.
    /// </summary>
    public void ClearCooldownTextures()
    {
        CooldownTexture?.Dispose();
        CooldownTexture = null;
        GreyTexture?.Dispose();
        GreyTexture = null;
    }

    public override void Dispose()
    {
        ClearCooldownTextures();

        base.Dispose();
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        base.Draw(spriteBatch);

        if (IsDropTarget)
        {
            DrawBorder(
                spriteBatch,
                new Rectangle(
                    ScreenX,
                    ScreenY,
                    Width,
                    Height),
                Color.Black);

            DrawBorder(
                spriteBatch,
                new Rectangle(
                    ScreenX + 1,
                    ScreenY + 1,
                    Width - 2,
                    Height - 2),
                Color.Black);

            DrawBorder(
                spriteBatch,
                new Rectangle(
                    ScreenX + 2,
                    ScreenY + 2,
                    Width - 4,
                    Height - 4),
                Color.Black);
        }

        //icon rendering with cooldown overlay
        var icon = NormalTexture;

        if (icon is null)
            return;

        var pos = new Vector2(ScreenX, ScreenY);

        if ((CooldownPercent > 0) && CooldownTexture is not null)
        {
            //grey-tinted base, with the blue-tinted overlay revealed top-to-bottom as the cooldown elapses
            DrawTexture(
                spriteBatch,
                GreyTexture ?? icon,
                pos,
                Color.White);

            var elapsed = 1f - CooldownPercent;
            var revealHeight = (int)(CooldownTexture.Height * elapsed);

            if (revealHeight > 0)
            {
                var srcRect = new Rectangle(
                    0,
                    0,
                    CooldownTexture.Width,
                    revealHeight);

                DrawTexture(
                    spriteBatch,
                    CooldownTexture,
                    pos,
                    srcRect,
                    Color.White);
            }
        } else
            DrawTexture(
                spriteBatch,
                icon,
                pos,
                Color.White);

        DrawCooldownNumber(spriteBatch, icon);
    }

    /// <summary>
    ///     Draws the whole-seconds readout centred over the icon, on top of the cooldown tint. Uses the purpose-authored
    ///     <see cref="CooldownNumberFont" /> rather than the game font, which is a fixed 8x12 bitmap with no larger face
    ///     shipped in the archives. The digits are sized so even a three-digit cooldown fits inside the icon; a longer
    ///     cooldown is clamped to <see cref="MAX_DISPLAY_SECONDS" /> rather than allowed to spill onto the neighbouring
    ///     slot.
    /// </summary>
    private void DrawCooldownNumber(SpriteBatch spriteBatch, Texture2D icon)
    {
        if (!ClientSettings.CooldownNumbersEnabled || (CooldownSecondsRemaining <= 0))
            return;

        var text = SecondsText[Math.Min(CooldownSecondsRemaining, MAX_DISPLAY_SECONDS)];
        var width = CooldownNumberFont.MeasureWidth(text);
        var height = CooldownNumberFont.GlyphHeight;

        CooldownNumberFont.Draw(
            spriteBatch,
            text,
            ScreenX + ((icon.Width - width) / 2),
            ScreenY + ((icon.Height - height) / 2));
    }

    /// <summary>
    ///     Fired on double-click. Parameter is the 1-based slot number.
    /// </summary>
    public event PanelSlotDoubleClickedHandler? DoubleClicked;

    /// <summary>
    ///     Fired when a drag begins on this slot. Parameter is the 1-based slot number.
    /// </summary>
    public event PanelSlotDragStartedHandler? DragStarted;

    public override void OnMouseDown(MouseDownEvent e)
    {
        if (e.Button == MouseButton.Left)
            DoubleClickFired = false;

        base.OnMouseDown(e);
    }

    public override void OnClick(ClickEvent e)
    {
        if (e.Button == MouseButton.Left)
            e.Handled = true;
    }

    public override void OnDoubleClick(DoubleClickEvent e)
    {
        //cache gate is intentional here (also OnDragStart): a mouse gesture only lands on a visible — therefore
        //freshly-updated — panel, and PanelSlot is book-agnostic (backs inventory too) so it cannot consult the model.
        //The macro/keyboard path in WorldScreen.InputHandlers must gate on WorldState.<Book> instead; do not "unify" these.
        if ((e.Button == MouseButton.Left) && NormalTexture is not null && (CooldownPercent <= 0))
        {
            DoubleClicked?.Invoke(Slot);
            DoubleClickFired = true;
            e.Handled = true;
        }
    }

    public override void OnDragStart(DragStartEvent e)
    {
        if (NormalTexture is null || (CooldownPercent > 0) || DoubleClickFired)
            return;

        e.Payload = new SlotDragPayload
        {
            Source = this,
            SlotIndex = Slot,
            SourcePanel = (Parent as PanelBase)?.Tab ?? default
        };

        DragStarted?.Invoke(this);
    }

    public override void OnDragMove(DragMoveEvent e)
    {
        if (e.Payload is SlotDragPayload payload && (payload.Source.Parent == Parent))
            IsDropTarget = true;
    }

    public override void OnMouseLeave()
    {
        base.OnMouseLeave();
        IsDropTarget = false;
        (Parent as PanelBase)?.ForceHoverExit();
    }

    public override void OnDragDrop(DragDropEvent e)
    {
        IsDropTarget = false;

        if (e.Payload is not SlotDragPayload payload)
            return;

        //only accept drops from slots within the same parent panel
        if (Parent is not PanelBase panel || (payload.Source.Parent != Parent))
            return;

        //dropping on the same slot is a no-op — just end drag
        if (payload.SlotIndex == Slot)
        {
            panel.EndDrag();
            e.Handled = true;

            return;
        }

        panel.CompleteDragSwap(Slot);
        e.Handled = true;
    }

}