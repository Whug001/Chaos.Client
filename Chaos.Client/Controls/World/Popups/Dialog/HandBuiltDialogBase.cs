#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Dialog;

/// <summary>
///     A framed dialog whose controls are built by hand from constants rather than read from a control prefab. Holds the
///     small button and caption builders such windows share.
/// </summary>
public abstract class HandBuiltDialogBase : FramedDialogPanelBase
{
    protected HandBuiltDialogBase(string prefabName, bool center = true)
        : base(prefabName, center) { }

    protected CustomButton AddButton(
        string caption,
        int width,
        int x,
        int y,
        Action onClick)
    {
        var button = new CustomButton(caption, width)
        {
            X = x,
            Y = y
        };

        button.Clicked += () => onClick();
        AddChild(button);

        return button;
    }

    protected UILabel Caption(
        string text,
        int x,
        int y,
        int width,
        HorizontalAlignment alignment = HorizontalAlignment.Left,
        Color? color = null)
    {
        var label = new UILabel
        {
            X = x,
            Y = y,
            Width = width,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = alignment,
            ForegroundColor = color ?? LegendColors.White,
            IsHitTestVisible = false,
            Text = text
        };

        AddChild(label);

        return label;
    }
}
