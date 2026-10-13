using Chaos.Client.Controls.World.Popups.College;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class BoardSavedSelectionTests
{
    private static List<CollegeSavedBoardRowInfo> Rows(params string[] ids) => ids.Select(id => new CollegeSavedBoardRowInfo { Id = id }).ToList();

    [Test]
    public void The_selection_follows_its_board_when_the_list_shifts()
    {
        var before = Rows("a", "b", "c");
        var after = Rows("a", "c");

        var selected = BoardSavedSelection.IdAt(before, 3);

        selected.Should().Be("c");
        BoardSavedSelection.PositionOf(after, selected).Should().Be(2);
    }

    [Test]
    public void A_deleted_board_leaves_no_selection()
    {
        var after = Rows("a", "c");

        BoardSavedSelection.PositionOf(after, "b").Should().Be(0);
    }

    [Test]
    [Arguments(0)]
    [Arguments(4)]
    [Arguments(-1)]
    public void A_row_id_outside_the_list_has_no_board(int rowId)
    {
        var rows = Rows("a", "b", "c");

        BoardSavedSelection.IdAt(rows, rowId).Should().BeNull();
        BoardSavedSelection.PositionOf(rows, null).Should().Be(0);
    }
}
