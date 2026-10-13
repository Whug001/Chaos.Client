using Chaos.Client.ViewModel.College;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class HangmanHelpersTests
{
    private static List<CollegeHangmanPlayerInfo> Players(int count, int turn, params int[] removed)
        => Enumerable.Range(0, count)
                     .Select(i => new CollegeHangmanPlayerInfo { Name = $"P{i}", IsTurn = i == turn, Removed = removed.Contains(i) })
                     .ToList();

    [Test]
    public void Strip_shows_one_before_the_current_and_counts_the_rest()
    {
        var strip = new HangmanTurnStrip(Players(9, 3), string.Empty, "P4");

        strip.Entries.Select(e => e.Name).Should().Equal("P2", "P3", "P4", "P5", "P6");
        strip.Entries.Single(e => e.Current).Name.Should().Be("P3");
        strip.Entries.Single(e => e.Viewer).Name.Should().Be("P4");
        strip.More.Should().Be(2);
    }

    [Test]
    public void Strip_stays_in_range_at_both_ends()
    {
        var first = new HangmanTurnStrip(Players(8, 0), string.Empty, "x");
        first.Entries.Select(e => e.Name).Should().Equal("P0", "P1", "P2", "P3", "P4");
        first.More.Should().Be(3);

        var last = new HangmanTurnStrip(Players(8, 7), string.Empty, "x");
        last.Entries.Select(e => e.Name).Should().Equal("P3", "P4", "P5", "P6", "P7");
        last.More.Should().Be(0);
    }

    [Test]
    public void Strip_leaves_out_removed_players_and_follows_a_named_current()
    {
        var strip = new HangmanTurnStrip(Players(4, 0, 1), "P3", "x");

        strip.Entries.Select(e => e.Name).Should().Equal("P0", "P2", "P3");
        strip.Entries.Single(e => e.Current).Name.Should().Be("P3");
        strip.More.Should().Be(0);
    }

    [Test]
    public void Strip_with_no_current_starts_at_the_front()
    {
        var strip = new HangmanTurnStrip(Players(6, -1), string.Empty, "x");

        strip.Entries.Should().HaveCount(5);
        strip.Entries.Should().OnlyContain(e => !e.Current);
        strip.More.Should().Be(1);
    }

    [Test]
    public void Gallows_adds_a_part_per_miss_and_clamps()
    {
        HangmanGallows.Segments(0).Should().HaveCount(3);

        for (var stage = 1; stage <= 6; stage++)
            HangmanGallows.Segments(stage).Count.Should().BeGreaterThan(HangmanGallows.Segments(stage - 1).Count);

        HangmanGallows.Segments(1).Count.Should().BeGreaterThan(3);
        HangmanGallows.Segments(6).Count.Should().BeGreaterThan(HangmanGallows.Segments(5).Count);
        HangmanGallows.Segments(9).Should().BeEquivalentTo(HangmanGallows.Segments(6));
        HangmanGallows.Segments(-1).Should().HaveCount(3);
    }

    [Test]
    public void Order_starts_ticked_and_gives_the_ticked_names_in_order()
    {
        var order = new TurnOrderList(["A", "B", "C"]);
        order.TickedNames.Should().Equal("A", "B", "C");

        order.Toggle(1);
        order.TickedNames.Should().Equal("A", "C");
        order.TickedCount.Should().Be(2);

        order.TickAll();
        order.TickedCount.Should().Be(3);
    }

    [Test]
    public void Order_moves_the_selected_row_and_keeps_it_selected()
    {
        var order = new TurnOrderList(["A", "B", "C"]);
        order.Select(0);

        order.MoveUp().Should().BeFalse();
        order.MoveDown().Should().BeTrue();
        order.Rows.Select(r => r.Name).Should().Equal("B", "A", "C");
        order.Selected.Should().Be(1);

        order.Move(1, 2).Should().BeTrue();
        order.Rows.Select(r => r.Name).Should().Equal("B", "C", "A");
        order.MoveDown().Should().BeFalse();
        order.Move(0, 0).Should().BeFalse();
        order.Move(0, 5).Should().BeFalse();
    }

    [Test]
    public void Order_shuffle_keeps_every_name_and_the_selection()
    {
        var order = new TurnOrderList(Enumerable.Range(0, 12).Select(i => $"N{i}"));
        order.Select(4);
        order.Toggle(2);

        order.Shuffle(new Random(7));

        order.Rows.Select(r => r.Name).Should().BeEquivalentTo(Enumerable.Range(0, 12).Select(i => $"N{i}"));
        order.Rows[order.Selected].Name.Should().Be("N4");
        order.Rows.Single(r => r.Name == "N2").Ticked.Should().BeFalse();
    }

    [Test]
    public void Order_select_out_of_range_clears()
    {
        var order = new TurnOrderList(["A"]);
        order.Select(3);

        order.Selected.Should().Be(-1);
        order.MoveDown().Should().BeFalse();
    }

    [Test]
    public void Order_toggle_out_of_range_changes_nothing()
    {
        var order = new TurnOrderList(["A", "B"]);

        order.Toggle(-1);
        order.Toggle(2);

        order.TickedCount.Should().Be(2);
    }

    [Test]
    public void Order_shuffle_with_nothing_selected_leaves_nothing_selected()
    {
        var order = new TurnOrderList(["A", "B", "C", "D"]);

        order.Shuffle(new Random(3));

        order.Selected.Should().Be(-1);
        order.Rows.Should().HaveCount(4);
    }

    [Test]
    public void Reconcile_keeps_order_and_ticks_drops_gone_names_and_appends_new_ones()
    {
        var order = new TurnOrderList(["A", "B", "C"]);
        order.Move(0, 2);
        order.Toggle(0);
        order.Select(1);

        order.Reconcile(["A", "C", "D", "B"]);

        order.Rows.Select(r => r.Name).Should().Equal("B", "C", "A", "D");
        order.Rows.Select(r => r.Ticked).Should().Equal(false, true, true, true);
        order.Selected.Should().Be(1);

        order.Reconcile(["D", "A"]);

        order.Rows.Select(r => r.Name).Should().Equal("A", "D");
        order.Selected.Should().Be(-1);
    }
}
