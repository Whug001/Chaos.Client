#region
using Chaos.Client.Controls.World.Popups.Tcg.Match;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
using static Chaos.Client.Tests.TcgMatchFixtures;
#endregion

namespace Chaos.Client.Tests;

public sealed class TcgMatchHandTests
{
    [Test]
    public void Shows_attack_and_end_phase_on_your_first_main()
        => TcgMatchWindow.TurnButtonLabels(Board()).Should().Equal("Attack", "End phase");

    [Test]
    public void Shows_end_turn_on_your_second_main()
        => TcgMatchWindow.TurnButtonLabels(Board(phase: TcgPhase.Main2)).Should().Equal("End turn");

    [Test]
    public void Shows_pass_on_your_response()
        => TcgMatchWindow.TurnButtonLabels(Board(TcgDecision.Respond)).Should().Equal("Pass");

    [Test]
    public void Shows_nothing_when_it_is_not_your_decision()
        => TcgMatchWindow.TurnButtonLabels(Board(decider: TcgSeat.B)).Should().BeEmpty();

    [Test]
    public void Attack_needs_a_ready_creature_in_main_one()
    {
        TcgMatchWindow.AttackEnabled(Board()).Should().BeFalse();
        TcgMatchWindow.AttackEnabled(Board(mine: [Creature(1, "bear")])).Should().BeTrue();
        TcgMatchWindow.AttackEnabled(Board(phase: TcgPhase.Main2, mine: [Creature(1, "bear")])).Should().BeFalse();
    }

    [Test]
    public void Clicking_a_hand_card_finds_the_topmost_id()
    {
        var board = Board();
        board.Players[0].Hand.AddRange([new TcgCardView(1, "bear"), new TcgCardView(2, "bear")]);

        TcgMatchWindow.HandCardIdAt(board, 50, 400).Should().Be(2);
        TcgMatchWindow.HandCardIdAt(board, 10, 400).Should().Be(1);
        TcgMatchWindow.HandCardIdAt(board, 10, 300).Should().Be(0);
    }

    [Test]
    public void Drops_land_on_the_battlefield_or_the_gems()
    {
        TcgMatchWindow.DropZoneAt(235, 200).Should().Be(TcgDropZone.Battlefield);
        TcgMatchWindow.DropZoneAt(60, 360).Should().Be(TcgDropZone.Mana);
        TcgMatchWindow.DropZoneAt(600, 400).Should().Be(TcgDropZone.None);
    }

    [Test]
    public void Shows_keep_for_both_seats_during_redraw()
    {
        TcgMatchWindow.TurnButtonLabels(Board(TcgDecision.Redraw, TcgSeat.A, TcgPhase.Redraw)).Should().Equal("Keep");
        TcgMatchWindow.TurnButtonLabels(Board(TcgDecision.Redraw, TcgSeat.B, TcgPhase.Redraw)).Should().Equal("Keep");
    }

    [Test]
    public void Hourglass_drains_over_the_decision_time()
    {
        var board = Board();
        board.DecisionMs = 8_000;
        var received = new DateTime(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);

        TcgMatchWindow.HourglassFrame(board, received, received).Should().Be(0);
        TcgMatchWindow.HourglassFrame(board, received.AddSeconds(4), received).Should().Be(4);
        TcgMatchWindow.HourglassFrame(board, received.AddSeconds(8), received).Should().Be(7);
    }

    [Test]
    public void Redraw_slots_cover_a_hand_over_seven()
        => TcgMatchWindow.RedrawSlots(9).Should().HaveCount(9);
}
