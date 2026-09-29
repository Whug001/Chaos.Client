#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.ViewModel;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.TownBallot;

/// <summary>One candidate row in the ballot window: the name, and the level and class in gray under it.</summary>
public sealed class TownBallotRow : UIPanel
{
    public const int HEIGHT = 34;
    private const int TEXT_X = 6;

    //the import window's selected-row colors, so the lists read alike
    private static readonly Color SelectedColor = new(156, 96, 12, 180);

    private readonly UILabel ClassLabel;
    private readonly UILabel NameLabel;

    public TownBallotRow(int width)
    {
        Width = width;
        Height = HEIGHT;

        NameLabel = new UILabel
        {
            X = TEXT_X,
            Y = 3,
            Width = width - (2 * TEXT_X),
            Height = TextRenderer.CHAR_HEIGHT,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false
        };

        ClassLabel = new UILabel
        {
            X = TEXT_X,
            Y = 3 + TextRenderer.CHAR_HEIGHT + 2,
            Width = width - (2 * TEXT_X),
            Height = TextRenderer.CHAR_HEIGHT,
            ForegroundColor = LegendColors.Gray,
            IsHitTestVisible = false
        };

        AddChild(NameLabel);
        AddChild(ClassLabel);
    }

    public bool Selected
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;
            BackgroundColor = value ? SelectedColor : null;
        }
    }

    public event Action? Clicked;

    public override void OnClick(ClickEvent e)
    {
        if (e.Button != MouseButton.Left)
            return;

        Clicked?.Invoke();
        e.Handled = true;
    }

    /// <summary>Shows a candidate; null hides the row.</summary>
    public void SetCandidate(TownBallotCandidateInfo? candidate)
    {
        Visible = candidate is not null;

        if (candidate is null)
            return;

        NameLabel.Text = candidate.Name;
        ClassLabel.Text = TownBallotState.ClassLine(candidate);
    }
}
