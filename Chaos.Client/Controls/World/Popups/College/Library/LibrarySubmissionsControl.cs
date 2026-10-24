#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.ViewModel.College.Library;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.College.Library;

/// <summary>The viewer's own library submissions: open, edit, send, take back or take down one.</summary>
public sealed class LibrarySubmissionsControl : CollegeListWindow<LibrarySubmissionRowInfo>
{
    private const int ROWS = 10;

    private readonly CustomButton EditButton;
    private readonly UILabel ReasonLabel;
    private readonly CustomButton SendButton;
    private readonly CustomButton TakeBackButton;
    private readonly CustomButton TakeDownButton;

    private List<LibrarySubmissionRowInfo> All = [];
    private bool ConfirmTakeDown;

    public LibrarySubmissionsControl()
        : base(
            "LibrarySubmissions",
            32,
            ROWS,
            [("Title", 0), ("Shelf", 200), ("Status", 300)],
            "No submissions yet.")
    {
        Caption("My submissions", LEFT, TITLE_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gold);
        ReasonLabel = Caption(
            string.Empty,
            LEFT,
            RowsTop + (ROWS * CollegeListRow.HEIGHT) + 4,
            INNER_WIDTH,
            color: LegendColors.Gray);

        EditButton = AddSelectionButton("Edit", 56, EditSelected);
        SendButton = AddSelectionButton("Send", 56, SendSelected);

        var secondRow = HEIGHT - BORDER_BOTTOM_HEIGHT - (2 * CustomButton.HEIGHT) - 10;
        TakeBackButton = AddButton("Take back", 76, LEFT + 180, secondRow, TakeBackSelected);
        TakeDownButton = AddButton("Take down", 76, LEFT + 180 + 76 + 6, secondRow, TakeDownSelected);
    }

    protected override IReadOnlyList<LibrarySubmissionRowInfo> Items => All;

    protected override string OpenCaption => "Open";

    /// <summary>Shows the submissions the server sent. A list that was closed starts on the first page.</summary>
    public void Open(CollegeDisplayArgs args)
    {
        if (!Visible)
            Page = 0;

        All = args.SubmissionRows;
        ShowRows();

        if (!Visible)
            Show();
    }

    protected override (string Text, Color Color)[] ColumnsOf(LibrarySubmissionRowInfo item)
        =>
        [
            (item.Title, LegendColors.White),
            (LibraryLabels.ShelfName(item.Shelf), LegendColors.White),
            (LibraryLabels.StatusName(item.Status), LegendColors.Gray)
        ];

    protected override int IdOf(LibrarySubmissionRowInfo item) => item.Id;

    protected override CollegeActionArgs OpenAction(int id)
        => new()
        {
            Type = CollegeActionType.LibraryOpenBook,
            Id = id,
            OpenMode = LibraryOpenMode.Own
        };

    protected override void SelectionChanged(int id)
    {
        ConfirmTakeDown = false;
        TakeDownButton.Caption = "Take down";

        var row = All.FirstOrDefault(r => r.Id == id);
        var actions = row is null
            ? new SubmissionActions(false, false, false, false)
            : SubmissionActions.For(row.Status);

        EditButton.Enabled = (row is not null) && actions.Edit;
        SendButton.Enabled = (row is not null) && actions.Send;
        TakeBackButton.Enabled = (row is not null) && actions.TakeBack;
        TakeDownButton.Enabled = (row is not null) && actions.TakeDown;
        ReasonLabel.Text = ReasonFor(row);
    }

    private static string ReasonFor(LibrarySubmissionRowInfo? row)
    {
        if ((row is null) || string.IsNullOrEmpty(row.Reason))
            return string.Empty;

        return row.Status switch
        {
            LibraryStatusCode.Rejected => row.Reason,
            LibraryStatusCode.TakenDown => string.IsNullOrEmpty(row.By) ? row.Reason : $"{row.Reason} by {row.By}",
            _ => string.Empty
        };
    }

    private void EditSelected(int id)
        => Raise(new CollegeActionArgs { Type = CollegeActionType.LibraryEdit, Id = id });

    private void SendSelected(int id)
        => Raise(new CollegeActionArgs { Type = CollegeActionType.LibrarySend, Id = id });

    private void TakeBackSelected()
    {
        if (SelectedId != 0)
            Raise(new CollegeActionArgs { Type = CollegeActionType.LibraryWithdraw, Id = SelectedId });
    }

    private void TakeDownSelected()
    {
        if (SelectedId == 0)
            return;

        if (!ConfirmTakeDown)
        {
            ConfirmTakeDown = true;
            TakeDownButton.Caption = "Confirm";

            return;
        }

        ConfirmTakeDown = false;
        TakeDownButton.Caption = "Take down";
        Raise(new CollegeActionArgs { Type = CollegeActionType.LibraryTakeDown, Id = SelectedId, Text = string.Empty });
    }
}
