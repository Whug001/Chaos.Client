#region
using Chaos.Client.Controls.World.Popups.Tcg.Match;
using FluentAssertions;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Tests;

public sealed class TcgMatchLayoutTests
{
    [Test]
    public void Centres_a_single_creature_on_the_battlefield()
        => TcgMatchLayout.CreatureSlot(0, 1, true).Should().Be(new Point(235, 316));

    [Test]
    public void Spaces_creatures_sixty_four_apart()
    {
        TcgMatchLayout.CreatureSlot(0, 2, false).Should().Be(new Point(203, 176));
        TcgMatchLayout.CreatureSlot(1, 2, false).Should().Be(new Point(267, 176));
    }

    [Test]
    public void Shrinks_the_step_for_eight_or_more()
    {
        var first = TcgMatchLayout.CreatureSlot(0, 8, true);
        var second = TcgMatchLayout.CreatureSlot(1, 8, true);

        (second.X - first.X).Should().Be(50);
    }

    [Test]
    public void Keeps_the_board_and_column_side_by_side()
    {
        TcgMatchLayout.Board.Right.Should().Be(TcgMatchLayout.Column.Left);
        TcgMatchLayout.Column.Right.Should().Be(640);
    }

    private static readonly Rectangle[] Widgets =
    [
        TcgMatchLayout.OpponentSeal,
        TcgMatchLayout.OpponentGems,
        TcgMatchLayout.OpponentTraps,
        TcgMatchLayout.OpponentCounts,
        TcgMatchLayout.YourSeal,
        TcgMatchLayout.YourGems,
        TcgMatchLayout.YourTraps,
        TcgMatchLayout.Hand,
        TcgMatchLayout.Buttons,
        TcgMatchLayout.Concede,
        TcgMatchLayout.StopWatching,
        TcgMatchLayout.BigCard,
        TcgMatchLayout.Hourglass,
        TcgMatchLayout.Timers,
        TcgMatchLayout.PhaseLine,
        TcgMatchLayout.WatchingLine,
        TcgMatchLayout.Log,
        TcgMatchLayout.Chat,
        TcgMatchLayout.ChatBox,
        TcgMatchLayout.Banner
    ];

    [Test]
    public void Keeps_every_widget_inside_the_window()
    {
        foreach (var rect in Widgets)
            (rect is { X: >= 0, Y: >= 0 } && (rect.Right <= 640) && (rect.Bottom <= 480))
                .Should()
                .BeTrue(rect.ToString());
    }

    [Test]
    public void Keeps_the_spectator_rects_clear_of_other_widgets()
    {
        foreach (var rect in Widgets)
        {
            if ((rect == TcgMatchLayout.StopWatching) || (rect == TcgMatchLayout.WatchingLine))
                continue;

            TcgMatchLayout.StopWatching.Intersects(rect).Should().BeFalse($"StopWatching overlaps {rect}");
            TcgMatchLayout.WatchingLine.Intersects(rect).Should().BeFalse($"WatchingLine overlaps {rect}");
        }
    }

    [Test]
    public void Stack_starts_at_the_left_edge_of_its_row()
        => TcgMatchLayout.StackSlot(0, 1).Should().Be(new Rectangle(300, 74, 60, 84));

    [Test]
    public void Stack_slots_for_one_three_and_five_stay_inside_the_row()
    {
        foreach (var count in new[] { 1, 3, 5 })
        {
            for (var i = 0; i < count; i++)
            {
                var slot = TcgMatchLayout.StackSlot(i, count);
                slot.X.Should().BeGreaterThanOrEqualTo(TcgMatchLayout.StackRow.X);
                slot.Right.Should().BeLessThanOrEqualTo(TcgMatchLayout.StackRow.Right);
                slot.Y.Should().Be(TcgMatchLayout.StackRow.Y);
                slot.Height.Should().Be(TcgMatchLayout.StackRow.Height);
            }

            TcgMatchLayout.StackSlot(0, count).X.Should().Be(300);
        }
    }

    [Test]
    public void Stack_slots_spread_for_three_and_compress_for_five()
    {
        TcgMatchLayout.StackSlot(1, 3).X.Should().Be(353);
        TcgMatchLayout.StackSlot(2, 3).X.Should().Be(406);
        TcgMatchLayout.StackSlot(1, 5).X.Should().Be(326);
        TcgMatchLayout.StackSlot(4, 5).X.Should().Be(404);
    }

    [Test]
    public void Stack_hit_prefers_the_newest_on_overlap()
    {
        TcgMatchLayout.StackHitAt(355, 80, 3).Should().Be(0);
        TcgMatchLayout.StackHitAt(300, 74, 1).Should().Be(0);
    }

    [Test]
    public void Stack_hit_misses_outside_the_row()
    {
        TcgMatchLayout.StackHitAt(0, 0, 3).Should().BeNull();
        TcgMatchLayout.StackHitAt(299, 74, 3).Should().BeNull();
        TcgMatchLayout.StackHitAt(300, 73, 3).Should().BeNull();
        TcgMatchLayout.StackHitAt(300, 80, 0).Should().BeNull();
    }
}
