#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.Dialog;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.GuildCloak;

/// <summary>
///     Shared layout helpers for the guild cloak editor and review windows: both build their controls by hand from
///     constants (there is no shared control prefab for either), and both need the same small button and caption
///     builders to do it. Pulled out here so the two windows do not carry byte-for-byte duplicate copies.
/// </summary>
public abstract class GuildCloakDialogBase : FramedDialogPanelBase
{
    protected GuildCloakDialogBase(string prefabName, bool center = true)
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
