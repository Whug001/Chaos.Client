#region
using Chaos.Client.Controls.World.Popups.Tcg.Match;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
using Microsoft.Xna.Framework;
using static Chaos.Client.Tests.TcgMatchFixtures;
#endregion

namespace Chaos.Client.Tests;

public sealed class TcgMatchCombatTests
{
    [Test]
    public void Combat_modes_offer_confirm_and_cancel()
    {
        TcgMatchWindow.CombatButtonLabels(TcgInputMode.Attacking).Should().Equal("Confirm", "Cancel");
        TcgMatchWindow.CombatButtonLabels(TcgInputMode.Blocking).Should().Equal("Confirm", "Cancel");
        TcgMatchWindow.CombatButtonLabels(TcgInputMode.Ordering).Should().Equal("Confirm", "Cancel");
        TcgMatchWindow.CombatButtonLabels(TcgInputMode.Idle).Should().BeEmpty();
    }

    [Test]
    public void Attack_confirm_needs_a_chosen_attacker()
    {
        var board = Board(mine: [Creature(1, "bear")]);
        var input = new TcgMatchInput(Lookup);

        input.StartAttack(board);

        TcgMatchWindow.ConfirmEnabled(board, input, Lookup).Should().BeFalse();

        input.ToggleAttacker(board, 1);

        TcgMatchWindow.ConfirmEnabled(board, input, Lookup).Should().BeTrue();
    }

    [Test]
    public void An_idle_guard_blocks_confirm_until_it_blocks()
    {
        var board = Board(TcgDecision.Block, mine: [Creature(1, "wall")], theirs: [Creature(8, "bear")]);
        board.Attackers.Add(8);
        var input = new TcgMatchInput(Lookup);

        input.Sync(board);
        input.Mode.Should().Be(TcgInputMode.Blocking);
        TcgMatchWindow.ConfirmEnabled(board, input, Lookup).Should().BeFalse();

        input.AssignBlock(board, 1, 8).Should().BeTrue();

        TcgMatchWindow.ConfirmEnabled(board, input, Lookup).Should().BeTrue();
    }

    [Test]
    public void Damage_order_completes_when_every_blocker_is_numbered()
    {
        var board = Board(TcgDecision.DamageOrder, mine: [Creature(1, "bear")], theirs: [Creature(8, "bear"), Creature(9, "bear")]);
        board.Attackers.Add(1);
        board.Blocks.AddRange([new TcgBlock(8, 1), new TcgBlock(9, 1)]);
        var input = new TcgMatchInput(Lookup);

        input.Sync(board);
        input.Mode.Should().Be(TcgInputMode.Ordering);
        TcgMatchWindow.OrderComplete(board, 1, input.DamageOrder).Should().BeFalse();

        var far = TcgMatchLayout.CreatureSlot(1, 2, false);
        TcgMatchWindow.OrderedBlockerIdAt(board, input, far.X, far.Y - 10).Should().Be(9);

        input.ClickBlocker(9);
        input.ClickBlocker(8);

        input.DamageOrder.Should().Equal(9, 8);
        TcgMatchWindow.OrderComplete(board, 1, input.DamageOrder).Should().BeTrue();
        TcgMatchWindow.ConfirmEnabled(board, input, Lookup).Should().BeTrue();
    }

    [Test]
    public void Chosen_attackers_step_toward_the_river()
    {
        var board = Board(mine: [Creature(1, "bear")]);
        var input = new TcgMatchInput(Lookup);

        input.StartAttack(board);
        input.ToggleAttacker(board, 1);

        var plain = TcgMatchLayout.CreatureSlot(0, 1, true);
        var shifted = TcgMatchWindow.DrawFeet(board, input, 1, true, 0, 1);

        shifted.X.Should().Be(plain.X);
        shifted.Y.Should().Be(plain.Y - 24);
    }

    [Test]
    public void Attackers_step_down_and_blockers_stand_in_front()
    {
        var board = Board(TcgDecision.Block, mine: [Creature(1, "bear")], theirs: [Creature(8, "bear")]);
        board.Attackers.Add(8);
        var input = new TcgMatchInput(Lookup);

        input.Sync(board);
        input.AssignBlock(board, 1, 8).Should().BeTrue();

        var theirSlot = TcgMatchLayout.CreatureSlot(0, 1, false);
        TcgMatchWindow.DrawFeet(board, input, 8, false, 0, 1).Should().Be(new Point(theirSlot.X, theirSlot.Y + 24));

        var blocker = TcgMatchWindow.DrawFeet(board, input, 1, true, 0, 1);
        blocker.X.Should().Be(theirSlot.X);
        blocker.Y.Should().Be(theirSlot.Y + 24 + 44);

        TcgMatchWindow.AttackerIdAt(board, input, theirSlot.X, theirSlot.Y + 14).Should().Be(8);
        TcgMatchWindow.OwnCreatureIdAt(board, input, blocker.X, blocker.Y - 10).Should().Be(1);
    }

    [Test]
    public void Redraw_slots_fit_seven_cards_in_two_rows()
    {
        var slots = TcgMatchWindow.RedrawSlots(7);

        slots.Should().HaveCount(7);
        slots.Select(slot => slot.Y).Distinct().Should().HaveCount(2);

        foreach (var slot in slots)
        {
            slot.X.Should().BeGreaterThanOrEqualTo(0);
            slot.Right.Should().BeLessThanOrEqualTo(470);
            slot.Y.Should().BeGreaterThanOrEqualTo(0);
            slot.Bottom.Should().BeLessThanOrEqualTo(352);
        }
    }

    [Test]
    public void Redraw_clicks_find_each_hand_card()
    {
        var board = Board();

        for (var id = 1; id <= 7; id++)
            board.Players[0].Hand.Add(new TcgCardView(id, "bear"));

        var slots = TcgMatchWindow.RedrawSlots(7);

        for (var i = 0; i < slots.Count; i++)
            TcgMatchWindow.RedrawCardIdAt(board, slots[i].Center.X, slots[i].Center.Y).Should().Be(i + 1);

        TcgMatchWindow.RedrawCardIdAt(board, 0, 351).Should().Be(0);
    }

    [Test]
    public void Phase_banner_guides_blocking_and_waits_on_the_opponent()
    {
        var block = Board(TcgDecision.Block, mine: [Creature(1, "bear")], theirs: [Creature(8, "bear")]);
        block.Attackers.Add(8);
        var input = new TcgMatchInput(Lookup);

        input.Sync(block);

        TcgMatchWindow.PhaseOverride(block, input, "Bo", false, Lookup).Should().Be("Click or drag your creatures onto attackers to block, then Confirm.");

        var guard = Board(TcgDecision.Block, mine: [Creature(1, "wall")], theirs: [Creature(8, "bear")]);
        guard.Attackers.Add(8);
        var guardInput = new TcgMatchInput(Lookup);

        guardInput.Sync(guard);

        TcgMatchWindow.PhaseOverride(guard, guardInput, "Bo", false, Lookup).Should().Be("A Guard creature must block.");

        var respond = Board(TcgDecision.Respond, decider: TcgSeat.B);

        TcgMatchWindow.PhaseOverride(respond, new TcgMatchInput(Lookup), "Bo", false, Lookup).Should().Be("Waiting for Bo…");
    }

    [Test]
    public void A_ready_creature_glows_to_attack()
    {
        var bear = Creature(1, "bear");
        var board = Board(mine: [bear]);

        TcgMatchWindow.ReadyGlow(board, new TcgMatchInput(Lookup), bear, 0, Lookup).Should().Be(TcgReadyGlow.Attack);
    }

    [Test]
    public void Tapped_sick_and_late_creatures_do_not_glow()
    {
        var tapped = Creature(1, "bear", tapped: true);
        var sick = Creature(2, "bear", sick: true);
        var late = Creature(3, "bear");
        var input = new TcgMatchInput(Lookup);

        TcgMatchWindow.ReadyGlow(Board(mine: [tapped]), input, tapped, 0, Lookup).Should().Be(TcgReadyGlow.None);
        TcgMatchWindow.ReadyGlow(Board(mine: [sick]), input, sick, 0, Lookup).Should().Be(TcgReadyGlow.None);
        TcgMatchWindow.ReadyGlow(Board(phase: TcgPhase.Main2, mine: [late]), input, late, 0, Lookup).Should().Be(TcgReadyGlow.None);
    }

    [Test]
    public void A_chosen_attacker_stops_glowing()
    {
        var bear = Creature(1, "bear");
        var other = Creature(2, "bear");
        var board = Board(mine: [bear, other]);
        var input = new TcgMatchInput(Lookup);

        input.StartAttack(board);
        input.ToggleAttacker(board, 1);

        TcgMatchWindow.ReadyGlow(board, input, bear, 0, Lookup).Should().Be(TcgReadyGlow.None);
        TcgMatchWindow.ReadyGlow(board, input, other, 0, Lookup).Should().Be(TcgReadyGlow.Attack);
    }

    [Test]
    public void The_opponents_creatures_never_glow()
    {
        var theirs = Creature(8, "bear");
        var board = Board(mine: [Creature(1, "bear")], theirs: [theirs]);

        TcgMatchWindow.ReadyGlow(board, new TcgMatchInput(Lookup), theirs, 0, Lookup).Should().Be(TcgReadyGlow.None);
    }

    [Test]
    public void Only_a_creature_that_can_block_an_attacker_glows_to_block()
    {
        var bear = Creature(1, "bear");
        var owl = Creature(2, "owl");
        var board = Board(TcgDecision.Block, mine: [bear, owl], theirs: [Creature(8, "drake")]);
        board.Attackers.Add(8);
        var input = new TcgMatchInput(Lookup);

        input.Sync(board);

        TcgMatchWindow.ReadyGlow(board, input, bear, 0, Lookup).Should().Be(TcgReadyGlow.None);
        TcgMatchWindow.ReadyGlow(board, input, owl, 0, Lookup).Should().Be(TcgReadyGlow.Block);
    }

    [Test]
    public void An_assigned_blocker_stops_glowing()
    {
        var bear = Creature(1, "bear");
        var board = Board(TcgDecision.Block, mine: [bear], theirs: [Creature(8, "bear")]);
        board.Attackers.Add(8);
        var input = new TcgMatchInput(Lookup);

        input.Sync(board);
        TcgMatchWindow.ReadyGlow(board, input, bear, 0, Lookup).Should().Be(TcgReadyGlow.Block);

        input.AssignBlock(board, 1, 8).Should().BeTrue();

        TcgMatchWindow.ReadyGlow(board, input, bear, 0, Lookup).Should().Be(TcgReadyGlow.None);
    }

    [Test]
    public void Missing_in_blocking_clears_the_selection_and_lets_the_click_through()
    {
        var board = Board(TcgDecision.Block, mine: [Creature(1, "bear")], theirs: [Creature(8, "bear")]);
        board.Attackers.Add(8);
        var input = new TcgMatchInput(Lookup);

        input.Sync(board);
        input.SelectBlocker(board, 1);
        input.SelectedBlocker.Should().Be(1);

        TcgMatchWindow.HandleCombatClick(board, board, input, -100, -100).Should().BeFalse();
        input.SelectedBlocker.Should().Be(0);
    }

    [Test]
    public void Hovering_an_attacker_narrows_the_block_glow_to_it()
    {
        var bear = Creature(1, "bear");
        var board = Board(TcgDecision.Block, mine: [bear], theirs: [Creature(8, "bear"), Creature(9, "drake")]);
        board.Attackers.Add(8);
        board.Attackers.Add(9);
        var input = new TcgMatchInput(Lookup);

        input.Sync(board);

        TcgMatchWindow.ReadyGlow(board, input, bear, 0, Lookup).Should().Be(TcgReadyGlow.Block);
        TcgMatchWindow.ReadyGlow(board, input, bear, 8, Lookup).Should().Be(TcgReadyGlow.Block);
        TcgMatchWindow.ReadyGlow(board, input, bear, 9, Lookup).Should().Be(TcgReadyGlow.None);
        TcgMatchWindow.ReadyGlow(board, input, bear, 1, Lookup).Should().Be(TcgReadyGlow.Block);
    }
}
