#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Generic;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Dialog;

/// <summary>
///     Base class for floating dialog sub-panels that share the ornate 9-slice frame (DlgBack2.spf tiled background +
///     nd_f01–f08 border pieces). Subclasses add their own content controls inside the frame.
/// </summary>
public abstract class FramedDialogPanelBase : PrefabPanel
{
    /// <summary>
    ///     Thickness of the frame's ornate bottom border, in pixels — subclasses need this to keep their own
    ///     content (e.g. a footer button row) clear of it. Named distinctly from the same-valued
    ///     <c>BORDER_BOTTOM</c> already declared privately by <see cref="DialogOptionPanel" /> and
    ///     <see cref="MenuListPanel" /> for their own (differing) border metrics, so promoting this doesn't shadow
    ///     either of them.
    /// </summary>
    protected const int BORDER_BOTTOM_HEIGHT = OrnateFrame.BORDER_BOTTOM_HEIGHT;

    /// <summary>
    ///     The OK button used to split the bottom edge into rivets (left) and plain (right). Subclasses should set this if
    ///     they create a Btn1 control.
    /// </summary>
    protected UIButton? OkButton { get; set; }

    protected FramedDialogPanelBase(string prefabName, bool center = true)
        : base(prefabName, center)
        => Background = null;

    /// <summary>
    ///     The prefab's "OK" button re-skinned as Close (<c>_nbtn.spf</c> frame 0 normal, 1 pressed), wired to
    ///     <paramref name="onClick" /> and anchored to the bottom-right corner by the given margins. Null when the
    ///     prefab has no OK button, same as <see cref="PrefabPanel.CreateButton(string)" />.
    /// </summary>
    /// <remarks>
    ///     Every popup in this family borrows its prefab's OK button because none of them has a control file of
    ///     its own (see <c>SlotMachineControl</c>'s remarks). The hover, selected and disabled states are cleared
    ///     because they still point at the prefab's "OK" art -- only the Close normal and pressed frames ever show.
    /// </remarks>
    protected UIButton? CreateCloseButton(ClickedHandler onClick, int rightMargin, int bottomMargin)
    {
        ArgumentNullException.ThrowIfNull(onClick);

        var button = CreateButton("OK");

        if (button is null)
            return null;

        button.NormalTexture = UiRenderer.Instance!.GetSpfTexture("_nbtn.spf");
        button.PressedTexture = UiRenderer.Instance!.GetSpfTexture("_nbtn.spf", 1);
        button.HoverTexture = null;
        button.SelectedTexture = null;
        button.DisabledTexture = null;

        button.Clicked += onClick;
        button.X = Width - button.Width - rightMargin;
        button.Y = Height - button.Height - bottomMargin;

        return button;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        UpdateClipRect();

        if ((ClipRect.Width <= 0) || (ClipRect.Height <= 0))
            return;

        OrnateFrame.Draw(
            spriteBatch,
            ScreenX,
            ScreenY,
            Width,
            Height,
            OkButton?.X);

        //children (content controls)
        foreach (var child in Children)
            if (child.Visible)
            {
                child.Draw(spriteBatch);
                DebugOverlay.DrawElement(spriteBatch, child);
            }
    }

    /// <inheritdoc cref="OrnateFrame.TileTexture" />
    protected static void TileTexture(
        SpriteBatch spriteBatch,
        Texture2D texture,
        int x,
        int y,
        int width,
        int height)
        => OrnateFrame.TileTexture(
            spriteBatch,
            texture,
            x,
            y,
            width,
            height);
}
