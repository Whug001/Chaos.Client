#region
using System.Globalization;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>The Teacher's saved boards: Load puts one up on the live board, Delete (after a "Sure?" click) removes it.</summary>
public sealed class BoardSavedControl : CollegeListWindow<CollegeSavedBoardRowInfo>
{
    private const string DELETE_CAPTION = "Delete";
    private const string CONFIRM_DELETE_CAPTION = "Sure?";

    private readonly CustomButton DeleteButton;
    private readonly UILabel Status;

    private List<CollegeSavedBoardRowInfo> All = [];
    private bool DeleteArmed;

    public BoardSavedControl()
        : base(
            "BoardSaved",
            32,
            12,
            [("Name", 0), ("Saved", 340)],
            "No saved boards.")
    {
        Caption("Saved boards", LEFT, TITLE_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gold);
        DeleteButton = AddSelectionButton(DELETE_CAPTION, 80, OnDeleteClicked);
        Status = Caption(string.Empty, LEFT, RowsTop + (12 * CollegeListRow.HEIGHT) + 6, INNER_WIDTH, color: LegendColors.Gray);
    }

    protected override string OpenCaption => "Load";

    protected override IReadOnlyList<CollegeSavedBoardRowInfo> Items => All;

    /// <summary>Sends the request for the saved list; <see cref="Show" /> fills the window when the server answers.</summary>
    public void Open() => Raise(new CollegeActionArgs { Type = CollegeActionType.BoardSavedList });

    /// <summary>Shows the saved boards the server sent, with its message in the status line.</summary>
    public void Show(CollegeDisplayArgs args)
    {
        if (!Visible)
            Page = 0;

        All = args.SavedBoards;
        Status.Text = args.Message;
        DisarmDelete();
        ShowRows();

        if (!Visible)
            Show();
    }

    //UTC, like the other College lists
    private static string Saved(uint unix)
        => DateTimeOffset.FromUnixTimeSeconds(unix)
                         .ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    protected override (string Text, Color Color)[] ColumnsOf(CollegeSavedBoardRowInfo item)
        =>
        [
            (item.Name, LegendColors.White),
            (Saved(item.UpdatedUnix), LegendColors.Gray)
        ];

    //rows have no number of their own, so the id is the position in the list, from 1
    protected override int IdOf(CollegeSavedBoardRowInfo item) => All.IndexOf(item) + 1;

    protected override CollegeActionArgs OpenAction(int id)
        => new()
        {
            Type = CollegeActionType.BoardLoad,
            BoardId = All[id - 1].Id
        };

    protected override void SelectionChanged(int id) => DisarmDelete();

    public override void Hide()
    {
        DisarmDelete();
        base.Hide();
    }

    private void OnDeleteClicked(int id)
    {
        if (!DeleteArmed)
        {
            DeleteArmed = true;
            DeleteButton.Caption = CONFIRM_DELETE_CAPTION;

            return;
        }

        DisarmDelete();

        Raise(
            new CollegeActionArgs
            {
                Type = CollegeActionType.BoardDelete,
                BoardId = All[id - 1].Id
            });
    }

    private void DisarmDelete()
    {
        DeleteArmed = false;

        //the base calls this while it is still being built
        if (DeleteButton is not null)
            DeleteButton.Caption = DELETE_CAPTION;
    }
}
