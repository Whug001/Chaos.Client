#region
using Chaos.Client.Controls.World.Popups.Tcg.Match;
using FluentAssertions;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Tests;

public sealed class TcgMatchLayoutTests
{
    private static Rectangle Box(Point feet) => new(feet.X - 24, feet.Y - 48, 48, 48);

    [Test]
    public void Slots_stay_inside_the_battlefield()
    {
        for (var count = 1; count <= 10; count++)
        {
            foreach (var yours in new[] { true, false })
            {
                for (var i = 0; i < count; i++)
                {
                    var box = Box(TcgMatchLayout.CreatureSlot(i, count, yours));

                    TcgMatchLayout.Battlefield.Contains(box).Should().BeTrue($"count {count} yours {yours} i {i} box {box}");
                }
            }
        }
    }

    [Test]
    public void Ten_a_side_stays_inside_and_clear_of_the_stack()
    {
        foreach (var yours in new[] { true, false })
        {
            for (var i = 0; i < 10; i++)
            {
                var box = Box(TcgMatchLayout.CreatureSlot(i, 10, yours));

                TcgMatchLayout.Battlefield.Contains(box).Should().BeTrue($"yours {yours} i {i} box {box}");
                box.Intersects(TcgMatchLayout.StackRow).Should().BeFalse($"yours {yours} i {i} box {box} vs stack");
            }
        }
    }

    [Test]
    public void Five_use_a_two_tile_step_six_use_one()
    {
        var twoFirst = TcgMatchLayout.CreatureSlot(0, 5, true);
        var twoSecond = TcgMatchLayout.CreatureSlot(1, 5, true);

        (twoSecond.X - twoFirst.X).Should().Be(2 * TcgMatchLayout.RowAxis.X);
        (twoSecond.Y - twoFirst.Y).Should().Be(2 * TcgMatchLayout.RowAxis.Y);

        var oneFirst = TcgMatchLayout.CreatureSlot(0, 6, true);
        var oneSecond = TcgMatchLayout.CreatureSlot(1, 6, true);

        (oneSecond.X - oneFirst.X).Should().Be(TcgMatchLayout.RowAxis.X);
        (oneSecond.Y - oneFirst.Y).Should().Be(TcgMatchLayout.RowAxis.Y);
    }

    [Test]
    public void Rows_are_three_tiles_apart()
    {
        var yours = TcgMatchLayout.CreatureSlot(0, 1, true);
        var theirs = TcgMatchLayout.CreatureSlot(0, 1, false);
        var facing = TcgMatchLayout.FacingAxis(false);

        //resolve yours - theirs = a*RowAxis + b*facing; b is the separation in tiles
        var dx = yours.X - theirs.X;
        var dy = yours.Y - theirs.Y;
        var row = TcgMatchLayout.RowAxis;
        var det = (row.X * facing.Y) - (row.Y * facing.X);
        var a = (double)(dx * facing.Y - dy * facing.X) / det;
        var b = (double)(row.X * dy - row.Y * dx) / det;

        b.Should().BeApproximately(3.0, 0.001);
        a.Should().BeApproximately(-1.5, 0.001);
    }

    [Test]
    public void Your_creatures_face_left_theirs_right()
    {
        TcgMatchLayout.Facing(true).Should().Be(Chaos.Geometry.Abstractions.Definitions.Direction.Left);
        TcgMatchLayout.Facing(false).Should().Be(Chaos.Geometry.Abstractions.Definitions.Direction.Right);
    }

    [Test]
    public void Board_draws_in_feet_order()
    {
        var slots = new List<Point>();

        for (var i = 0; i < 3; i++)
        {
            slots.Add(TcgMatchLayout.CreatureSlot(i, 3, false));
            slots.Add(TcgMatchLayout.CreatureSlot(i, 3, true));
        }

        slots.Select(f => TcgMatchLayout.DrawOrder(f)).Should().Equal(slots.Select(f => f.Y));
        slots.OrderBy(f => TcgMatchLayout.DrawOrder(f)).Should().BeEquivalentTo(slots.OrderBy(f => f.Y));
    }

    [Test]
    public void Attack_reaches_the_midline_then_the_target()
    {
        var start = TcgMatchLayout.CreatureSlot(0, 2, true);
        var target = TcgMatchLayout.CreatureSlot(1, 2, false);
        var mid = TcgMatchLayout.MidlineFeet(start, true);

        TcgMatchWindow.AttackedFeet(start, target, true, 0).Should().Be(start);
        TcgMatchWindow.AttackedFeet(start, target, true, 0.4f).Should().Be(mid);
        TcgMatchWindow.AttackedFeet(start, target, true, 0.7f).Should().Be(target);
        TcgMatchWindow.AttackedFeet(start, target, true, 1).Should().Be(target);
    }

    [Test]
    public void Centres_a_single_creature_on_the_battlefield()
        => TcgMatchLayout.CreatureSlot(0, 1, true).Should().Be(new Point(192, 275));

    [Test]
    public void Spaces_creatures_two_tiles_apart()
    {
        var first = TcgMatchLayout.CreatureSlot(0, 2, false);
        var second = TcgMatchLayout.CreatureSlot(1, 2, false);

        (second.X - first.X).Should().Be(2 * TcgMatchLayout.RowAxis.X);
        (second.Y - first.Y).Should().Be(2 * TcgMatchLayout.RowAxis.Y);
    }

    [Test]
    public void Shrinks_the_step_for_six_or_more()
    {
        var first = TcgMatchLayout.CreatureSlot(0, 8, true);
        var second = TcgMatchLayout.CreatureSlot(1, 8, true);

        (second.X - first.X).Should().Be(TcgMatchLayout.RowAxis.X);
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
