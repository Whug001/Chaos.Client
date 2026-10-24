#region
using Chaos.Client.Controls.Custom;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.ViewModel.College.Library;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.College.Library;

/// <summary>The review list: books waiting for a verdict, and reported shelf books. Staff only.</summary>
public sealed class LibraryReviewControl : CollegeListWindow<LibraryReviewRowInfo>
{
    private const string WAITING_EMPTY = "Nothing is waiting for review.";
    private const string REPORTS_EMPTY = "No reported books.";

    private readonly CustomButton WaitingTab;
    private readonly CustomButton ReportsTab;

    private List<LibraryReviewRowInfo> All = [];
    private List<LibraryReviewRowInfo> Shown = [];
    private bool ShowingReports;
    private readonly Dictionary<int, LibraryReviewKind> Kinds = new();

    public LibraryReviewControl()
        : base(
            "LibraryReview",
            TABBED_HEADER_TOP,
            12,
            [("Title", 0), ("Author", 170), ("Shelf", 280), ("Detail", 370)],
            WAITING_EMPTY)
    {
        Caption("Review books", LEFT, TITLE_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gold);
        WaitingTab = AddButton("Waiting", 130, LEFT, TABS_TOP, () => SwitchTab(false));
        ReportsTab = AddButton("Reports", 130, LEFT + 136, TABS_TOP, () => SwitchTab(true));
    }

    protected override IReadOnlyList<LibraryReviewRowInfo> Items => Shown;

    protected override string OpenCaption => "Open";

    /// <summary>Shows the review list the server sent. A list that was closed opens on the Waiting tab.</summary>
    public void Open(CollegeDisplayArgs args)
    {
        if (!Visible)
        {
            ShowingReports = false;
            Page = 0;
        }

        All = args.ReviewRows;
        Refresh();

        if (!Visible)
            Show();
    }

    protected override (string Text, Color Color)[] ColumnsOf(LibraryReviewRowInfo item)
        =>
        [
            (item.Title, LegendColors.White),
            (item.Author, LegendColors.White),
            (LibraryLabels.ShelfName(item.Shelf), LegendColors.Gray),
            item.IsEdit
                ? ("Edit", LegendColors.Gold)
                : (item.ReportCount == 1 ? "1 report" : $"{item.ReportCount} reports", LegendColors.White)
        ];

    protected override int IdOf(LibraryReviewRowInfo item) => item.Id;

    protected override CollegeActionArgs OpenAction(int id)
        => new()
        {
            Type = CollegeActionType.LibraryOpenBook,
            Id = id,
            OpenMode = Kinds.TryGetValue(id, out var kind) && (kind == LibraryReviewKind.Reported)
                ? LibraryOpenMode.ReviewReported
                : LibraryOpenMode.ReviewWaiting
        };

    private void Refresh()
    {
        Shown = All.Where(r => (r.Kind == LibraryReviewKind.Reported) == ShowingReports)
                   .ToList();
        Kinds.Clear();

        foreach (var row in All)
            Kinds[row.Id] = row.Kind;

        WaitingTab.Caption = $"Waiting ({All.Count(r => r.Kind == LibraryReviewKind.Waiting)})";
        ReportsTab.Caption = $"Reports ({All.Count(r => r.Kind == LibraryReviewKind.Reported)})";
        WaitingTab.Selected = !ShowingReports;
        ReportsTab.Selected = ShowingReports;
        Empty.Text = ShowingReports ? REPORTS_EMPTY : WAITING_EMPTY;
        ShowRows();
    }

    private void SwitchTab(bool reports)
    {
        ShowingReports = reports;
        Page = 0;
        Refresh();
    }
}
