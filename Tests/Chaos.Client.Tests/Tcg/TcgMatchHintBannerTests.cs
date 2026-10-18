#region
using Chaos.Client.Controls.World.Popups.Tcg.Match;
using Chaos.DarkAges.Definitions;
using FluentAssertions;
using static Chaos.Client.Tests.TcgMatchFixtures;
#endregion

namespace Chaos.Client.Tests;

public sealed class TcgMatchHintBannerTests
{
    [Test]
    public void Sits_below_the_opponent_strip_outside_redraw()
    {
        var banner = TcgMatchWindow.HintBannerRect(Board(), 22);

        banner.Y.Should().Be(TcgMatchLayout.OpponentStrip.Bottom + 4);
    }

    [Test]
    public void Moves_below_the_redraw_overlay_during_redraw()
    {
        var board = Board(phase: TcgPhase.Redraw);

        var banner = TcgMatchWindow.HintBannerRect(board, 22);

        banner.Y.Should().Be(356);
    }

    [Test]
    public void Never_covers_a_redraw_card()
    {
        var board = Board(phase: TcgPhase.Redraw);

        for (var count = 1; count <= 9; count++)
        {
            var banner = TcgMatchWindow.HintBannerRect(board, 22);

            TcgMatchWindow.RedrawSlots(count).Should().OnlyContain(slot => !slot.Intersects(banner), $"hand of {count}");
        }
    }

    [Test]
    public void Stays_left_of_the_buttons_during_redraw()
    {
        var board = Board(phase: TcgPhase.Redraw);

        var banner = TcgMatchWindow.HintBannerRect(board, 22);

        banner.Intersects(TcgMatchLayout.Buttons).Should().BeFalse();
        banner.Right.Should().BeLessThanOrEqualTo(TcgMatchLayout.Buttons.Left - 4);
    }

    [Test]
    public void Stays_put_when_the_board_is_missing()
        => TcgMatchWindow.HintBannerRect(null, 22).Y.Should().Be(TcgMatchLayout.OpponentStrip.Bottom + 4);
}
