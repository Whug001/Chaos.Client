#region
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.Dialog;
#endregion

namespace Chaos.Client.Controls.World.Popups.GuildCloak;

/// <summary>
///     The guild cloak editor and review windows' base: the hand-built dialog helpers plus the preview's step row.
/// </summary>
public abstract class GuildCloakDialogBase : HandBuiltDialogBase
{
    protected GuildCloakDialogBase(string prefabName, bool center = true)
        : base(prefabName, center) { }

    /// <summary>
    ///     The preview's step row across <paramref name="width" />: Prev, Pause/Play and Next. Prev and Next move one walk step
    ///     and pause the preview, so each frame can be looked at. Returns the three buttons.
    /// </summary>
    protected IReadOnlyList<CustomButton> AddStepButtons(GuildCloakPreview preview, int x, int y, int width)
    {
        const int GAP = 4;
        var side = (width - (2 * GAP)) * 3 / 10;
        var play = AddButton("Pause", width - (2 * side) - (2 * GAP), x + side + GAP, y, () => preview.TogglePause());

        void ShowState() => play.Caption = preview.Paused ? "Play" : "Pause";

        play.Clicked += ShowState;

        var prev = AddButton(
            "Prev",
            side,
            x,
            y,
            () =>
            {
                preview.StepBy(-1);
                ShowState();
            });

        var next = AddButton(
            "Next",
            side,
            x + width - side,
            y,
            () =>
            {
                preview.StepBy(1);
                ShowState();
            });

        return [prev, play, next];
    }
}
