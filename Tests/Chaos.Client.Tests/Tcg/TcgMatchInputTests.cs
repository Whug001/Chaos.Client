#region
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
using static Chaos.Client.Tests.TcgMatchFixtures;
#endregion

namespace Chaos.Client.Tests;

public sealed class TcgMatchInputTests
{
    private static TcgMatchInput Input() => new(Lookup);

    [Test]
    public void Playing_an_untargeted_card_casts_it()
    {
        var board = Board(mana: [Mana(1, TcgElement.Fire), Mana(2, TcgElement.Fire)]);
        board.Players[0].Hand.Add(new TcgCardView(5, "bear"));
        var input = Input();

        input.ClickHandCard(board, 5);
        var action = input.ChoosePlay(board);

        input.Mode.Should().Be(TcgInputMode.Idle);
        action!.Type.Should().Be(TcgActionType.Cast);
        action.CardId.Should().Be(5);
        action.TargetId.Should().Be(0);
    }

    [Test]
    public void Playing_a_trap_sets_it()
    {
        var board = Board(mana: [Mana(1, TcgElement.Fire)]);
        board.Players[0].Hand.Add(new TcgCardView(5, "snare"));
        var input = Input();

        input.ClickHandCard(board, 5);

        input.ChoosePlay(board)!.Type.Should().Be(TcgActionType.SetTrap);
    }

    [Test]
    public void A_targeted_card_waits_for_a_legal_target()
    {
        var board = Board(mana: [Mana(1, TcgElement.Fire)], mine: [Creature(3, "bear")], theirs: [Creature(9, "bear")]);
        board.Players[0].Hand.Add(new TcgCardView(6, "bolt"));
        var input = Input();

        input.ClickHandCard(board, 6);
        input.ChoosePlay(board).Should().BeNull();

        input.Mode.Should().Be(TcgInputMode.Targeting);
        input.Targets.Should().Equal(9);
        input.ChooseTarget(board, 3).Should().BeNull();

        var cast = input.ChooseTarget(board, 9);
        cast!.TargetId.Should().Be(9);
        input.Mode.Should().Be(TcgInputMode.Idle);
    }

    [Test]
    public void Mana_places_the_selected_card()
    {
        var board = Board();
        board.Players[0].Hand.Add(new TcgCardView(5, "bear"));
        var input = Input();

        input.ClickHandCard(board, 5);

        input.ChooseMana()!.Should().BeEquivalentTo(new { Type = TcgActionType.PlaceMana, CardId = 5 });
    }

    [Test]
    public void Picking_mana_while_targeting_keeps_the_target()
    {
        var board = Board(mana: [Mana(1, TcgElement.Fire), Mana(2, TcgElement.Fire)], theirs: [Creature(9, "bear")]);
        board.Players[0].Hand.Add(new TcgCardView(6, "bolt"));
        var input = Input();

        input.ClickHandCard(board, 6);
        input.ChoosePlay(board).Should().BeNull();
        input.Mode.Should().Be(TcgInputMode.Targeting);

        input.ToggleMana(board, 1);
        input.ToggleMana(board, 2);

        input.Mode.Should().Be(TcgInputMode.Targeting);
        input.Targets.Should().Equal(9);
        input.PickedMana.Should().Equal(1, 2);

        input.ToggleMana(board, 1);

        input.Mode.Should().Be(TcgInputMode.Targeting);
        input.PickedMana.Should().Equal(2);
    }

    [Test]
    public void Cancel_clears_picked_gems()
    {
        var board = Board(mana: [Mana(1, TcgElement.Fire)], theirs: [Creature(9, "bear")]);
        board.Players[0].Hand.Add(new TcgCardView(6, "bolt"));
        var input = Input();

        input.ClickHandCard(board, 6);
        input.ChoosePlay(board);
        input.ToggleMana(board, 1);
        input.PickedMana.Should().Equal(1);

        input.Cancel();

        input.PickedMana.Should().BeEmpty();
        input.Mode.Should().Be(TcgInputMode.Idle);
        input.Targets.Should().BeEmpty();
    }

    [Test]
    public void Cancel_returns_to_idle()
    {
        var board = Board(mana: [Mana(1, TcgElement.Fire)], theirs: [Creature(9, "bear")]);
        board.Players[0].Hand.Add(new TcgCardView(6, "bolt"));
        var input = Input();
        input.ClickHandCard(board, 6);
        input.ChoosePlay(board);

        input.Cancel();

        input.Mode.Should().Be(TcgInputMode.Idle);
    }

    [Test]
    public void Attacking_sends_only_ready_creatures()
    {
        var board = Board(mine: [Creature(1, "bear"), Creature(2, "bear", sick: true)]);
        var input = Input();

        input.StartAttack(board);
        input.ToggleAttacker(board, 1);
        input.ToggleAttacker(board, 2);

        input.Attackers.Should().Equal(1);
        input.Confirm(board)!.CardIds.Should().Equal(1);
    }

    [Test]
    public void Clicking_an_attacker_again_takes_it_back()
    {
        var board = Board(mine: [Creature(1, "bear")]);
        var input = Input();
        input.StartAttack(board);

        input.ToggleAttacker(board, 1);
        input.ToggleAttacker(board, 1);

        input.Confirm(board).Should().BeNull();
    }

    [Test]
    public void Blocks_follow_drags_and_confirm_sends_them()
    {
        var board = Board(TcgDecision.Block, decider: TcgSeat.A, mine: [Creature(1, "bear"), Creature(2, "bear")], theirs: [Creature(8, "bear"), Creature(9, "drake")]);
        board.Attackers.AddRange([8, 9]);
        var input = Input();

        input.Sync(board);
        input.Mode.Should().Be(TcgInputMode.Blocking);

        input.AssignBlock(board, 1, 9).Should().BeFalse();
        input.AssignBlock(board, 1, 8).Should().BeTrue();
        input.AssignBlock(board, 2, 8).Should().BeTrue();
        input.Unassign(2);

        input.Confirm(board)!.Blocks.Should().Equal(new TcgBlock(1, 8));
    }

    [Test]
    public void Damage_order_follows_clicks()
    {
        var board = Board(TcgDecision.DamageOrder, mine: [Creature(1, "bear")], theirs: [Creature(8, "bear"), Creature(9, "bear")]);
        board.Attackers.Add(1);
        board.Blocks.AddRange([new TcgBlock(8, 1), new TcgBlock(9, 1)]);
        var input = Input();

        input.Sync(board);
        input.Mode.Should().Be(TcgInputMode.Ordering);
        input.OrderingAttacker.Should().Be(1);

        input.ClickBlocker(9);
        input.Confirm(board).Should().BeNull();
        input.ClickBlocker(8);

        var action = input.Confirm(board)!;
        action.Type.Should().Be(TcgActionType.OrderDamage);
        action.CardId.Should().Be(1);
        action.CardIds.Should().Equal(9, 8);
    }

    [Test]
    public void Redraw_sends_the_marked_cards()
    {
        var board = Board(TcgDecision.Redraw, phase: TcgPhase.Redraw);
        board.Players[0].Hand.AddRange([new TcgCardView(1, "bear"), new TcgCardView(2, "bear")]);
        var input = Input();

        input.Sync(board);
        input.ToggleRedraw(2);

        input.Confirm(board)!.Should().BeEquivalentTo(new { Type = TcgActionType.Redraw, CardIds = new List<int> { 2 } });
    }

    [Test]
    public void Leaves_a_decision_mode_when_the_board_no_longer_asks()
    {
        var input = Input();
        var block = Board(TcgDecision.Block);
        input.Sync(block);

        input.Sync(Board());

        input.Mode.Should().Be(TcgInputMode.Idle);
    }

    [Test]
    public void Cancelling_a_concede_confirm_restores_the_block_mode()
    {
        var board = Board(TcgDecision.Block, decider: TcgSeat.A, mine: [Creature(1, "bear")], theirs: [Creature(8, "bear")]);
        board.Attackers.Add(8);
        var input = Input();

        input.Sync(board);
        input.AssignBlock(board, 1, 8);
        input.AskConcede();
        input.Mode.Should().Be(TcgInputMode.ConfirmConcede);

        input.Cancel();

        input.Mode.Should().Be(TcgInputMode.Blocking);
        input.Blocks.Should().ContainKey(1);
    }

    [Test]
    public void Cancelling_a_concede_confirm_restores_redraw_marks()
    {
        var board = Board(TcgDecision.Redraw, phase: TcgPhase.Redraw);
        board.Players[0].Hand.AddRange([new TcgCardView(1, "bear"), new TcgCardView(2, "bear")]);
        var input = Input();

        input.Sync(board);
        input.ToggleRedraw(2);
        input.AskConcede();
        input.Cancel();

        input.Mode.Should().Be(TcgInputMode.Redraw);
        input.RedrawMarks.Should().Equal(2);
    }

    [Test]
    public void Drops_from_the_hand_are_ignored_while_deciding_something_else()
    {
        var block = Board(TcgDecision.Block);
        var order = Board(TcgDecision.DamageOrder, mine: [Creature(1, "bear")], theirs: [Creature(8, "bear"), Creature(9, "bear")]);
        order.Attackers.Add(1);
        order.Blocks.AddRange([new TcgBlock(8, 1), new TcgBlock(9, 1)]);
        var redraw = Board(TcgDecision.Redraw, phase: TcgPhase.Redraw);

        foreach (var board in new[] { block, order, redraw })
        {
            var input = Input();
            input.Sync(board);
            var mode = input.Mode;

            input.DropHandCard(board, 5, false).Should().BeNull();
            input.Mode.Should().Be(mode);
        }
    }

    [Test]
    public void Losing_the_decision_clears_a_half_picked_target()
    {
        var board = Board(mana: [Mana(1, TcgElement.Fire)], theirs: [Creature(9, "bear")]);
        board.Players[0].Hand.Add(new TcgCardView(6, "bolt"));
        var input = Input();

        input.ClickHandCard(board, 6);
        input.ChoosePlay(board);
        input.Mode.Should().Be(TcgInputMode.Targeting);

        input.Sync(Board(decider: TcgSeat.B));

        input.Mode.Should().Be(TcgInputMode.Idle);
        input.Targets.Should().BeEmpty();
    }

    [Test]
    public void Staying_on_the_decision_re_filters_dead_targets()
    {
        var board = Board(mana: [Mana(1, TcgElement.Fire)], theirs: [Creature(9, "bear")]);
        board.Players[0].Hand.Add(new TcgCardView(6, "bolt"));
        var input = Input();

        input.ClickHandCard(board, 6);
        input.ChoosePlay(board);

        var next = Board(mana: [Mana(1, TcgElement.Fire)]);
        next.Players[0].Hand.Add(new TcgCardView(6, "bolt"));
        input.Sync(next);

        input.Mode.Should().Be(TcgInputMode.Targeting);
        input.Targets.Should().BeEmpty();
        input.ChooseTarget(next, 9).Should().BeNull();
    }

    [Test]
    public void A_refused_damage_order_can_be_sent_again()
    {
        var board = Board(TcgDecision.DamageOrder, mine: [Creature(1, "bear")], theirs: [Creature(8, "bear"), Creature(9, "bear")]);
        board.Attackers.Add(1);
        board.Blocks.AddRange([new TcgBlock(8, 1), new TcgBlock(9, 1)]);
        var input = Input();

        input.Sync(board);
        input.ClickBlocker(8);
        input.ClickBlocker(9);
        input.Confirm(board)!.Type.Should().Be(TcgActionType.OrderDamage);

        board.Rejected = TcgRejectReason.InvalidOrder;
        input.Sync(board);

        input.Mode.Should().Be(TcgInputMode.Ordering);
        input.OrderingAttacker.Should().Be(1);
    }

    [Test]
    public void Clicking_a_creature_that_can_block_selects_it()
    {
        var board = Board(TcgDecision.Block, decider: TcgSeat.A, mine: [Creature(1, "bear")], theirs: [Creature(8, "bear")]);
        board.Attackers.Add(8);
        var input = Input();

        input.Sync(board);
        input.SelectBlocker(board, 1);

        input.SelectedBlocker.Should().Be(1);
        input.Blocks.Should().BeEmpty();
    }

    [Test]
    public void Clicking_a_creature_that_cannot_block_selects_nothing()
    {
        var board = Board(TcgDecision.Block, decider: TcgSeat.A, mine: [Creature(1, "bear")], theirs: [Creature(8, "drake")]);
        board.Attackers.Add(8);
        var input = Input();

        input.Sync(board);
        input.SelectBlocker(board, 1);

        input.SelectedBlocker.Should().Be(0);
    }

    [Test]
    public void Clicking_an_attacker_with_a_selected_blocker_pairs_them()
    {
        var board = Board(TcgDecision.Block, decider: TcgSeat.A, mine: [Creature(1, "bear")], theirs: [Creature(8, "bear")]);
        board.Attackers.Add(8);
        var input = Input();

        input.Sync(board);
        input.SelectBlocker(board, 1);
        input.PairSelectedBlocker(board, 8).Should().BeTrue();

        input.Blocks.Should().ContainKey(1);
        input.Blocks[1].Should().Be(8);
        input.SelectedBlocker.Should().Be(0);
    }

    [Test]
    public void Clicking_a_blocking_creature_removes_its_block()
    {
        var board = Board(TcgDecision.Block, decider: TcgSeat.A, mine: [Creature(1, "bear")], theirs: [Creature(8, "bear")]);
        board.Attackers.Add(8);
        var input = Input();

        input.Sync(board);
        input.AssignBlock(board, 1, 8).Should().BeTrue();
        input.SelectBlocker(board, 1);

        input.Blocks.Should().BeEmpty();
        input.SelectedBlocker.Should().Be(0);
    }

    [Test]
    public void Clicking_the_selected_creature_again_clears_the_selection()
    {
        var board = Board(TcgDecision.Block, decider: TcgSeat.A, mine: [Creature(1, "bear")], theirs: [Creature(8, "bear")]);
        board.Attackers.Add(8);
        var input = Input();

        input.Sync(board);
        input.SelectBlocker(board, 1);
        input.SelectBlocker(board, 1);

        input.SelectedBlocker.Should().Be(0);
        input.Blocks.Should().BeEmpty();
    }

    [Test]
    public void Clearing_the_selection_empties_it()
    {
        var board = Board(TcgDecision.Block, decider: TcgSeat.A, mine: [Creature(1, "bear")], theirs: [Creature(8, "bear")]);
        board.Attackers.Add(8);
        var input = Input();

        input.Sync(board);
        input.SelectBlocker(board, 1);
        input.ClearBlockSelection();

        input.SelectedBlocker.Should().Be(0);
        input.Blocks.Should().BeEmpty();
    }

    [Test]
    public void Confirm_and_cancel_clear_the_selection()
    {
        var board = Board(TcgDecision.Block, decider: TcgSeat.A, mine: [Creature(1, "bear")], theirs: [Creature(8, "bear")]);
        board.Attackers.Add(8);
        var input = Input();

        input.Sync(board);
        input.SelectBlocker(board, 1);
        input.Cancel();

        input.SelectedBlocker.Should().Be(0);

        input.SelectBlocker(board, 1);
        input.Confirm(board);

        input.SelectedBlocker.Should().Be(0);
    }

    [Test]
    public void Leaving_blocking_clears_the_selection()
    {
        var board = Board(TcgDecision.Block, decider: TcgSeat.A, mine: [Creature(1, "bear")], theirs: [Creature(8, "bear")]);
        board.Attackers.Add(8);
        var input = Input();

        input.Sync(board);
        input.SelectBlocker(board, 1);
        input.Sync(Board());

        input.Mode.Should().Be(TcgInputMode.Idle);
        input.SelectedBlocker.Should().Be(0);
    }

    [Test]
    public void Incoming_damage_drops_as_blocks_are_added()
    {
        var board = Board(TcgDecision.Block, decider: TcgSeat.A, mine: [Creature(1, "bear"), Creature(2, "bear")], theirs: [Creature(8, "bear"), Creature(9, "bear")]);
        board.Attackers.AddRange([8, 9]);
        var input = Input();

        input.Sync(board);

        input.IncomingDamage(board).Should().Be(4);
        input.IncomingDamageText(board).Should().Be("Incoming: 4 damage");

        input.AssignBlock(board, 1, 8).Should().BeTrue();
        input.IncomingDamage(board).Should().Be(2);
        input.IncomingDamageText(board).Should().Be("Incoming: 2 damage");

        input.AssignBlock(board, 2, 9).Should().BeTrue();
        input.IncomingDamage(board).Should().Be(0);
        input.IncomingDamageText(board).Should().Be("All blocked");
    }

    [Test]
    public void Trample_damage_still_gets_through_a_small_blocker()
    {
        var board = Board(TcgDecision.Block, decider: TcgSeat.A, mine: [Creature(1, "bear")], theirs: [new TcgCreatureView(8, "boar", 5, 5, 0, false, false)]);
        board.Attackers.Add(8);
        var input = Input();

        input.Sync(board);

        input.IncomingDamage(board).Should().Be(5);

        input.AssignBlock(board, 1, 8).Should().BeTrue();
        input.IncomingDamage(board).Should().Be(3);
        input.IncomingDamageText(board).Should().Be("Incoming: 3 damage");
    }

    [Test]
    public void Shielded_players_take_no_incoming_damage()
    {
        var board = Board(TcgDecision.Block, decider: TcgSeat.A, mine: [Creature(1, "bear")], theirs: [Creature(8, "bear")]);
        board.Attackers.Add(8);
        board.Players[0].Shielded = true;
        var input = Input();

        input.Sync(board);

        input.IncomingDamage(board).Should().Be(0);
        input.IncomingDamageText(board).Should().Be("Phased");
    }

    [Test]
    public void A_first_strike_blocker_that_kills_a_trampler_stops_the_trample()
    {
        var board = Board(
            TcgDecision.Block,
            decider: TcgSeat.A,
            mine: [new TcgCreatureView(1, "pikeman", 5, 2, 0, false, false)],
            theirs: [new TcgCreatureView(8, "boar", 5, 5, 0, false, false)]);
        board.Attackers.Add(8);
        var input = Input();

        input.Sync(board);
        input.AssignBlock(board, 1, 8).Should().BeTrue();

        input.IncomingDamage(board).Should().Be(0);
    }

    [Test]
    public void A_first_strike_blocker_that_cannot_kill_still_takes_trample()
    {
        var board = Board(
            TcgDecision.Block,
            decider: TcgSeat.A,
            mine: [new TcgCreatureView(1, "pikeman", 2, 2, 0, false, false)],
            theirs: [new TcgCreatureView(8, "boar", 5, 5, 0, false, false)]);
        board.Attackers.Add(8);
        var input = Input();

        input.Sync(board);
        input.AssignBlock(board, 1, 8).Should().BeTrue();

        input.IncomingDamage(board).Should().Be(3);
    }

    [Test]
    public void Venom_first_strike_kills_a_trampler()
    {
        var board = Board(
            TcgDecision.Block,
            decider: TcgSeat.A,
            mine: [new TcgCreatureView(1, "viper", 1, 2, 0, false, false)],
            theirs: [new TcgCreatureView(8, "boar", 5, 5, 0, false, false)]);
        board.Attackers.Add(8);
        var input = Input();

        input.Sync(board);
        input.AssignBlock(board, 1, 8).Should().BeTrue();

        input.IncomingDamage(board).Should().Be(0);
    }

    [Test]
    public void A_first_strike_trampler_strikes_before_its_killer()
    {
        var board = Board(
            TcgDecision.Block,
            decider: TcgSeat.A,
            mine: [new TcgCreatureView(1, "pikeman", 5, 2, 0, false, false)],
            theirs: [new TcgCreatureView(8, "charger", 5, 5, 0, false, false)]);
        board.Attackers.Add(8);
        var input = Input();

        input.Sync(board);
        input.AssignBlock(board, 1, 8).Should().BeTrue();

        input.IncomingDamage(board).Should().Be(3);
    }

    [Test]
    public void Clicking_a_gem_picks_it_and_clicking_again_unpicks_it()
    {
        var board = Board(mana: [Mana(1, TcgElement.Fire), Mana(2, TcgElement.Fire)]);
        var input = Input();

        input.ToggleMana(board, 1);
        input.PickedMana.Should().BeEquivalentTo([1]);

        input.ToggleMana(board, 1);
        input.PickedMana.Should().BeEmpty();
    }

    [Test]
    public void Tapped_and_unknown_gems_cannot_be_picked()
    {
        var board = Board(mana: [Mana(1, TcgElement.Fire, tapped: true)]);
        var input = Input();

        input.ToggleMana(board, 1);
        input.ToggleMana(board, 99);

        input.PickedMana.Should().BeEmpty();
    }

    [Test]
    public void A_cast_sends_the_picks_and_clears_them()
    {
        var board = Board(mana: [Mana(1, TcgElement.Fire), Mana(2, TcgElement.Fire)]);
        board.Players[0].Hand.Add(new TcgCardView(5, "bear"));
        var input = Input();

        input.ToggleMana(board, 2);
        input.ToggleMana(board, 1);
        input.ClickHandCard(board, 5);
        var action = input.ChoosePlay(board);

        action!.ManaIds.Should().Equal(2, 1);
        input.PickedMana.Should().BeEmpty();
    }

    [Test]
    public void Picks_clear_on_a_turn_change()
    {
        var board = Board(mana: [Mana(1, TcgElement.Fire)]);
        var input = Input();
        input.ToggleMana(board, 1);

        var next = Board(mana: [Mana(1, TcgElement.Fire)]);
        next.Events.Add(new TcgEventView(TcgEventType.TurnStarted, TcgSeat.A, 0, 0, 0, ""));
        input.Sync(next);

        input.PickedMana.Should().BeEmpty();
    }

    [Test]
    public void Picks_clear_on_a_refusal()
    {
        var board = Board(mana: [Mana(1, TcgElement.Fire)]);
        var input = Input();
        input.ToggleMana(board, 1);

        var refused = Board(mana: [Mana(1, TcgElement.Fire)]);
        refused.Rejected = TcgRejectReason.WrongMana;
        input.Sync(refused);

        input.PickedMana.Should().BeEmpty();
    }

    [Test]
    public void A_pick_that_became_tapped_is_dropped()
    {
        var input = Input();
        input.ToggleMana(Board(mana: [Mana(1, TcgElement.Fire)]), 1);

        input.Sync(Board(mana: [Mana(1, TcgElement.Fire, tapped: true)]));

        input.PickedMana.Should().BeEmpty();
    }

    [Test]
    public void A_send_with_picks_remembers_the_wrong_mana_note()
    {
        var board = Board(mana: [Mana(1, TcgElement.Fire)]);
        board.Players[0].Hand.Add(new TcgCardView(5, "bear"));
        var input = Input();

        input.ToggleMana(board, 1);
        input.ClickHandCard(board, 5);
        input.ChoosePlay(board);

        input.WrongManaNote.Should().Be("Those gems don't pay for bear (needs 1 Fire + 1 any).");
    }
}
