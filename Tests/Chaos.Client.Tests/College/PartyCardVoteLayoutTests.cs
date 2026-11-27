using System.Reflection;
using Chaos.Client.Controls.World.Popups.College;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class PartyCardVoteLayoutTests
{
    [Test]
    public void Ten_choices_fit_on_one_page()
    {
        PartyCardControl.VotePages(0).Should().Be(1);
        PartyCardControl.VotePages(10).Should().Be(1);
        PartyCardControl.VotePages(11).Should().Be(2);
        PartyCardControl.VotePages(20).Should().Be(2);
        PartyCardControl.VotePages(21).Should().Be(3);
    }

    [Test]
    public void Vote_order_runs_down_the_left_column_then_the_right()
    {
        var cells = Enumerable.Range(0, 10).Select(PartyCardControl.VoteCell).ToList();

        cells.Should()
            .Equal(
                (0, 0),
                (1, 0),
                (2, 0),
                (3, 0),
                (4, 0),
                (0, 1),
                (1, 1),
                (2, 1),
                (3, 1),
                (4, 1));
    }

    [Test]
    public void Vote_grid_fits_the_inner_width_with_no_extra_height()
    {
        var type = typeof(PartyCardControl);
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;

        var inner = (int)type.GetField("INNER_WIDTH", flags)!.GetRawConstantValue()!;
        var rows = (int)type.GetField("VOTE_ROWS", flags)!.GetRawConstantValue()!;
        var columns = (int)type.GetField("VOTE_COLUMNS", flags)!.GetRawConstantValue()!;
        var gap = (int)type.GetField("VOTE_COLUMN_GAP", flags)!.GetRawConstantValue()!;
        var width = (int)type.GetField("VOTE_BUTTON_WIDTH", flags)!.GetRawConstantValue()!;
        var pageSize = (int)type.GetField("VOTE_PAGE_SIZE", flags)!.GetRawConstantValue()!;

        pageSize.Should().Be(10);
        rows.Should().Be(5);
        ((columns * width) + gap).Should().BeLessThanOrEqualTo(inner);
    }
}
