#region
using Chaos.Client.Controls.World.Popups.Tcg.Match;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
using static Chaos.Client.Tests.TcgMatchFixtures;
#endregion

namespace Chaos.Client.Tests;

public sealed class TcgMatchSpectatorTests
{
    private static TcgMatchInput Input() => new(Lookup);

    [Test]
    public void A_spectator_hovers_creatures_on_both_sides()
    {
        var board = Board(mine: [Creature(3, "bear")], theirs: [Creature(9, "owl")]);
        var bottom = TcgMatchLayout.CreatureSlot(0, 1, true);
        var top = TcgMatchLayout.CreatureSlot(0, 1, false);

        TcgMatchWindow.HoverAt(board, Input(), true, bottom.X, bottom.Y - 10)!.Value.CreatureId.Should().Be(3);
        TcgMatchWindow.HoverAt(board, Input(), true, top.X, top.Y - 10)!.Value.CreatureId.Should().Be(9);
    }

    [Test]
    public void A_spectator_never_hovers_a_hand_card()
    {
        var board = Board();
        board.Players[0].Hand.Add(new TcgCardView(5, "bear"));
        var slot = TcgMatchLayout.HandCard(0, 1);

        TcgMatchWindow.HoverAt(board, Input(), true, slot.X + 5, slot.Y + 5).Should().BeNull();
        TcgMatchWindow.HoverAt(board, Input(), false, slot.X + 5, slot.Y + 5)!.Value.HandId.Should().Be(5);
    }

    [Test]
    public void Spectators_see_the_waiting_card()
    {
        var board = Board(decision: TcgDecision.Respond, decider: TcgSeat.B);
        board.Stack.Add(new TcgStackView(7, "bear", TcgSeat.A, 0, false));

        TcgMatchWindow.ShowsStackBigCard(board, true).Should().BeTrue();
    }

    [Test]
    public void The_caster_sees_the_card_while_waiting()
    {
        var board = Board(decision: TcgDecision.Respond, decider: TcgSeat.B);
        board.Stack.Add(new TcgStackView(7, "bear", TcgSeat.A, 0, false));

        TcgMatchWindow.ShowsStackBigCard(board, false).Should().BeTrue();
    }

    [Test]
    public void The_responder_sees_the_card()
    {
        var board = Board(decision: TcgDecision.Respond, decider: TcgSeat.A);
        board.Stack.Add(new TcgStackView(7, "bolt", TcgSeat.B, 0, false));

        TcgMatchWindow.ShowsStackBigCard(board, false).Should().BeTrue();
    }

    [Test]
    public void A_hover_wins_over_the_waiting_card()
    {
        var board = Board(decision: TcgDecision.Respond, decider: TcgSeat.B);
        board.Stack.Add(new TcgStackView(7, "bolt", TcgSeat.A, 0, false));

        TcgMatchWindow.BigCardPick("bear", TcgPrinting.Normal, board, false).Should().Be(("bear", TcgPrinting.Normal));
    }

    [Test]
    public void A_spectator_hover_wins_over_the_waiting_card()
    {
        var board = Board(decision: TcgDecision.Respond, decider: TcgSeat.B);
        board.Stack.Add(new TcgStackView(7, "bolt", TcgSeat.A, 0, false));

        TcgMatchWindow.BigCardPick("owl", TcgPrinting.Full, board, true).Should().Be(("owl", TcgPrinting.Full));
    }

    [Test]
    public void Without_a_hover_the_waiting_card_shows()
    {
        var board = Board(decision: TcgDecision.Respond, decider: TcgSeat.B);
        board.Stack.Add(new TcgStackView(7, "bolt", TcgSeat.A, 0, false, TcgPrinting.NormalFoil));

        TcgMatchWindow.BigCardPick(null, TcgPrinting.Normal, board, false).Should().Be(("bolt", TcgPrinting.NormalFoil));
    }

    [Test]
    public void Without_a_hover_or_a_waiting_card_there_is_nothing()
    {
        TcgMatchWindow.BigCardPick(null, TcgPrinting.Normal, Board(decision: TcgDecision.Respond), false).Should().BeNull();
        TcgMatchWindow.BigCardPick(null, TcgPrinting.Normal, null, true).Should().BeNull();
    }

    [Test]
    public void No_card_once_the_stack_is_empty_or_the_window_closed()
    {
        TcgMatchWindow.ShowsStackBigCard(Board(decision: TcgDecision.Respond), true).Should().BeFalse();

        var main = Board();
        main.Stack.Add(new TcgStackView(7, "bear", TcgSeat.A, 0, false));
        TcgMatchWindow.ShowsStackBigCard(main, true).Should().BeFalse();
        TcgMatchWindow.ShowsStackBigCard(null, true).Should().BeFalse();
    }
}
