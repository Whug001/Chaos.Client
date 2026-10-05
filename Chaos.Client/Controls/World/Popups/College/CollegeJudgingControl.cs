#region
using Chaos.Client.Controls.Custom;
using Chaos.Client.Rendering.Definitions;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>The judging list: open entries without authors; moderators also see entries awaiting a verdict.</summary>
public sealed class CollegeJudgingControl : CollegeListWindow<CollegeJudgingRowInfo>
{
    private const string VOTING_EMPTY = "Nothing to judge right now.";

    private readonly CustomButton VotingTab;
    private readonly CustomButton WaitingTab;

    private List<CollegeJudgingRowInfo> All = [];
    private List<CollegeJudgingRowInfo> Shown = [];
    private bool ShowingWaiting;

    public CollegeJudgingControl()
        : base(
            "CollegeJudging",
            TABBED_HEADER_TOP,
            12,
            [("Subject", 0), ("Title", 80), ("Days", 340), ("Your vote", 400)],
            VOTING_EMPTY)
    {
        Caption("Judge entries", LEFT, TITLE_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gold);
        VotingTab = AddButton("Voting", 130, LEFT, TABS_TOP, () => SwitchTab(false));
        WaitingTab = AddButton("Awaiting verdict", 160, LEFT + 136, TABS_TOP, () => SwitchTab(true));
    }

    /// <summary>The visible tab's entry ids in order, all pages, for the reader's Prev and Next.</summary>
    public IReadOnlyList<int> VisibleOrder
        => Visible
            ? Items.Select(r => r.Id)
                   .ToList()
            : [];

    protected override IReadOnlyList<CollegeJudgingRowInfo> Items => Shown;

    /// <summary>Shows the list the server sent. A list that was closed opens on the Voting tab's first page.</summary>
    public void Open(CollegeDisplayArgs args)
    {
        if (!Visible)
        {
            ShowingWaiting = false;
            Page = 0;
        }

        All = args.JudgingRows;
        WaitingTab.Visible = args.CanModerate;

        if (!args.CanModerate)
            ShowingWaiting = false;

        Refresh();

        if (!Visible)
            Show();
    }

    protected override (string Text, Color Color)[] ColumnsOf(CollegeJudgingRowInfo item)
        =>
        [
            (item.Subject.ToString(), LegendColors.Gray),
            (item.Title, LegendColors.White),
            (item.Waiting ? "-" : item.DaysLeft.ToString(), LegendColors.White),
            (CollegeTiers.Name(item.MyTier), LegendColors.Gold)
        ];

    protected override int IdOf(CollegeJudgingRowInfo item) => item.Id;

    protected override CollegeActionArgs OpenAction(int id)
        => new()
        {
            Type = CollegeActionType.OpenPiece,
            Source = CollegePieceSource.Entry,
            Id = id
        };

    private void Refresh()
    {
        Shown = All.Where(r => r.Waiting == ShowingWaiting)
                   .ToList();
        VotingTab.Caption = $"Voting ({All.Count(r => !r.Waiting)})";
        WaitingTab.Caption = $"Awaiting verdict ({All.Count(r => r.Waiting)})";
        VotingTab.Selected = !ShowingWaiting;
        WaitingTab.Selected = ShowingWaiting;
        Empty.Text = ShowingWaiting ? "No entries are awaiting a verdict." : VOTING_EMPTY;
        ShowRows();
    }

    private void SwitchTab(bool waiting)
    {
        ShowingWaiting = waiting;
        Page = 0;
        Refresh();
    }
}
