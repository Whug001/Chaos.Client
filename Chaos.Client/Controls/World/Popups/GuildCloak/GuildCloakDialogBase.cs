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

    /// <summary>
    ///     The preview's step row across <paramref name="width" />: Prev, Pause/Play and Next. Prev and Next move one walk step
    ///     and pause the preview, so each frame can be looked at.
    /// </summary>
    protected void AddStepButtons(GuildCloakPreview preview, int x, int y, int width)
    {
        const int GAP = 4;
        var side = (width - (2 * GAP)) * 3 / 10;
        var play = AddButton("Pause", width - (2 * side) - (2 * GAP), x + side + GAP, y, () => preview.TogglePause());

        void ShowState() => play.Caption = preview.Paused ? "Play" : "Pause";

        play.Clicked += ShowState;

        AddButton(
            "Prev",
            side,
            x,
            y,
            () =>
            {
                preview.StepBy(-1);
                ShowState();
            });

        AddButton(
            "Next",
            side,
            x + width - side,
            y,
            () =>
            {
                preview.StepBy(1);
                ShowState();
            });
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
