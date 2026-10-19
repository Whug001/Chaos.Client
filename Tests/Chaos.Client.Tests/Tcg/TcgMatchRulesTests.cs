#region
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
using static Chaos.Client.Tests.TcgMatchFixtures;
#endregion

namespace Chaos.Client.Tests;

public sealed class TcgMatchRulesTests
{

    [Test]
    public void Pays_element_symbols_with_their_element_and_generic_with_anything()
    {
        var mana = new List<TcgManaView> { Mana(1, TcgElement.Fire), Mana(2, TcgElement.Water) };

        TcgMatchRules.CanPay(mana, Catalog["bear"], 0).Should().BeTrue();
        TcgMatchRules.CanPay([Mana(1, TcgElement.Water), Mana(2, TcgElement.Water)], Catalog["bear"], 0).Should().BeFalse();
    }

    [Test]
    public void Tapped_mana_does_not_pay()
        => TcgMatchRules.CanPay([Mana(1, TcgElement.Fire, tapped: true), Mana(2, TcgElement.Fire)], Catalog["bear"], 0).Should().BeFalse();

    [Test]
    public void Greys_out_a_card_you_cannot_pay_for()
    {
        var board = Board(mana: [Mana(1, TcgElement.Fire)]);

        TcgMatchRules.Play(board, new TcgCardView(5, "bear"), Lookup, false).CanPlay.Should().BeFalse();
    }

    [Test]
    public void Allows_an_affordable_creature_and_mana_on_your_main_decision()
    {
        var board = Board(mana: [Mana(1, TcgElement.Fire), Mana(2, TcgElement.Earth)]);

        var play = TcgMatchRules.Play(board, new TcgCardView(5, "bear"), Lookup, false);

        play.CanPlay.Should().BeTrue();
        play.CanMana.Should().BeTrue();
    }

    [Test]
    public void Mana_greys_out_after_placing_one_this_turn()
        => TcgMatchRules.Play(Board(), new TcgCardView(5, "bear"), Lookup, true).CanMana.Should().BeFalse();

    [Test]
    public void Only_instants_play_in_a_response_window()
    {
        var board = Board(TcgDecision.Respond, mana: [Mana(1, TcgElement.Fire), Mana(2, TcgElement.Fire)], theirs: [Creature(9, "bear")]);

        TcgMatchRules.Play(board, new TcgCardView(5, "bear"), Lookup, false).CanPlay.Should().BeFalse();
        TcgMatchRules.Play(board, new TcgCardView(6, "bolt"), Lookup, false).CanPlay.Should().BeTrue();
        TcgMatchRules.Play(board, new TcgCardView(6, "bolt"), Lookup, false).CanMana.Should().BeFalse();
    }

    [Test]
    public void Nothing_plays_when_it_is_not_your_decision()
        => TcgMatchRules.Play(Board(decider: TcgSeat.B, mana: [Mana(1, TcgElement.Fire), Mana(2, TcgElement.Fire)]), new TcgCardView(5, "bear"), Lookup, false)
                        .Should().Be(new TcgPlayChoice(false, false));

    [Test]
    public void A_targeted_card_needs_a_legal_target()
    {
        var board = Board(mana: [Mana(1, TcgElement.Fire)]);

        TcgMatchRules.Play(board, new TcgCardView(6, "bolt"), Lookup, false).CanPlay.Should().BeFalse();
    }

    [Test]
    public void A_trap_needs_a_free_trap_slot()
    {
        var board = Board(mana: [Mana(1, TcgElement.Fire)], traps: [new(1, "snare"), new(2, "snare"), new(3, "snare")]);

        TcgMatchRules.Play(board, new TcgCardView(5, "snare"), Lookup, false).CanPlay.Should().BeFalse();
    }

    [Test]
    public void Enemy_creature_targets_are_only_the_opponents_creatures()
    {
        var board = Board(mana: [Mana(1, TcgElement.Fire)], mine: [Creature(3, "bear")], theirs: [Creature(9, "bear")]);

        TcgMatchRules.LegalTargets(board, Catalog["bolt"], Lookup).Should().Equal(9);
    }

    [Test]
    public void Creature_or_player_targets_include_both_players()
    {
        var bolt = Card("zap", TcgCardType.Spell, target: TcgTargetKind.CreatureOrPlayer);

        TcgMatchRules.LegalTargets(Board(theirs: [Creature(9, "bear")]), bolt, Lookup).Should().BeEquivalentTo([9, -1, -2]);
    }

    [Test]
    public void A_ward_creature_is_a_target_only_if_you_can_pay_the_tax()
    {
        var board = Board(mana: [Mana(1, TcgElement.Fire), Mana(2, TcgElement.Fire)], theirs: [Creature(9, "golem")]);

        TcgMatchRules.LegalTargets(board, Catalog["bolt"], Lookup).Should().BeEmpty();

        board.Players[0].Mana.Add(Mana(3, TcgElement.Water));

        TcgMatchRules.LegalTargets(board, Catalog["bolt"], Lookup).Should().Equal(9);
    }

    [Test]
    public void Self_and_opponent_cards_need_no_target()
        => TcgMatchRules.NeedsTarget(Catalog["heal"]).Should().BeFalse();

    [Test]
    public void Spell_targets_are_the_stack()
    {
        var counter = Card("nope", TcgCardType.Instant, target: TcgTargetKind.Spell);
        var board = Board(TcgDecision.Respond);
        board.Stack.Add(new TcgStackView(14, "bolt", TcgSeat.B, 3, false));

        TcgMatchRules.LegalTargets(board, counter, Lookup).Should().Equal(14);
    }

    [Test]
    public void Only_ready_creatures_attack_in_main_phase_one()
    {
        var board = Board(mine: [Creature(1, "bear"), Creature(2, "bear", tapped: true), Creature(3, "bear", sick: true)]);

        board.Players[0].Battlefield.Where(c => TcgMatchRules.CanAttack(board, c)).Select(c => c.Id).Should().Equal(1);
        TcgMatchRules.CanAttack(Board(phase: TcgPhase.Main2, mine: [Creature(1, "bear")]), Creature(1, "bear")).Should().BeFalse();
    }

    [Test]
    public void Flying_attackers_need_flying_or_reach_blockers()
    {
        TcgMatchRules.CanBlock(Creature(1, "bear"), Creature(9, "drake"), Lookup).Should().BeFalse();
        TcgMatchRules.CanBlock(Creature(2, "owl"), Creature(9, "drake"), Lookup).Should().BeTrue();
        TcgMatchRules.CanBlock(Creature(3, "bear", tapped: true), Creature(9, "bear"), Lookup).Should().BeFalse();
    }

    [Test]
    public void An_idle_guard_that_could_block_is_a_problem()
    {
        var board = Board(TcgDecision.Block, mine: [Creature(1, "wall"), Creature(2, "bear")], theirs: [Creature(9, "bear")]);
        board.Attackers.Add(9);

        TcgMatchRules.GuardProblem(board, [], Lookup).Should().BeTrue();
        TcgMatchRules.GuardProblem(board, [new TcgBlock(1, 9)], Lookup).Should().BeFalse();
    }

    [Test]
    public void Attackers_with_two_blockers_need_an_order_until_sent()
    {
        var board = Board(TcgDecision.DamageOrder, mine: [Creature(1, "bear"), Creature(2, "bear")], theirs: [Creature(8, "bear"), Creature(9, "bear")]);
        board.Attackers.AddRange([1, 2]);
        board.Blocks.AddRange([new TcgBlock(8, 1), new TcgBlock(9, 1)]);

        TcgMatchRules.AttackersNeedingOrder(board, new HashSet<int>()).Should().Equal(1);
        TcgMatchRules.AttackersNeedingOrder(board, new HashSet<int> { 1 }).Should().BeEmpty();
    }

    [Test]
    public void Describes_a_cost_in_words()
    {
        TcgMatchRules.CostText(Catalog["bear"]).Should().Be("1 Fire + 1 any");
        TcgMatchRules.CostText(Card("pyro", TcgCardType.Instant, fire: 1)).Should().Be("1 Fire");
        TcgMatchRules.CostText(Catalog["bear"], 2).Should().Be("1 Fire + 3 any");
        TcgMatchRules.CostText(Card("free", TcgCardType.Creature)).Should().Be("nothing");
    }
}
