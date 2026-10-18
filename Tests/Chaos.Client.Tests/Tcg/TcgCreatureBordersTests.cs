#region
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using FluentAssertions;
using static Chaos.Client.Tests.TcgMatchFixtures;
#endregion

namespace Chaos.Client.Tests;

public sealed class TcgCreatureBordersTests
{
    [Test]
    public void Outside_selection_modes_nothing_gets_a_border()
    {
        var board = Board(mine: [Creature(1, "bear")], theirs: [Creature(8, "bear")]);
        var input = new TcgMatchInput(Lookup);

        TcgCreatureBorders.ForCreature(board, input, board.Players[0].Battlefield[0], Lookup)
                          .Should()
                          .Be(new TcgCreatureBorderChoice(TcgCreatureBorder.None, TcgSelectionMode.Attack));

        TcgCreatureBorders.ForCreature(board, input, board.Players[1].Battlefield[0], Lookup)
                          .Border.Should()
                          .Be(TcgCreatureBorder.None);
    }

    [Test]
    public void Legal_attackers_are_selectable_then_selected()
    {
        var board = Board(mine: [Creature(1, "bear")]);
        var input = new TcgMatchInput(Lookup);

        input.StartAttack(board);

        TcgCreatureBorders.ForCreature(board, input, board.Players[0].Battlefield[0], Lookup)
                          .Should()
                          .Be(new TcgCreatureBorderChoice(TcgCreatureBorder.Selectable, TcgSelectionMode.Attack));

        input.ToggleAttacker(board, 1);

        TcgCreatureBorders.ForCreature(board, input, board.Players[0].Battlefield[0], Lookup)
                          .Should()
                          .Be(new TcgCreatureBorderChoice(TcgCreatureBorder.Selected, TcgSelectionMode.Attack));

        input.ToggleAttacker(board, 1);

        TcgCreatureBorders.ForCreature(board, input, board.Players[0].Battlefield[0], Lookup)
                          .Border.Should()
                          .Be(TcgCreatureBorder.Selectable);
    }

    [Test]
    public void Tapped_creatures_and_the_enemy_get_no_attack_border()
    {
        var board = Board(mine: [Creature(1, "bear", tapped: true)], theirs: [Creature(8, "bear")]);
        var input = new TcgMatchInput(Lookup);

        input.StartAttack(board);

        TcgCreatureBorders.ForCreature(board, input, board.Players[0].Battlefield[0], Lookup)
                          .Border.Should()
                          .Be(TcgCreatureBorder.None);

        TcgCreatureBorders.ForCreature(board, input, board.Players[1].Battlefield[0], Lookup)
                          .Border.Should()
                          .Be(TcgCreatureBorder.None);
    }

    [Test]
    public void Legal_blockers_are_selectable_then_selected_when_assigned()
    {
        var board = Board(TcgDecision.Block, mine: [Creature(1, "bear")], theirs: [Creature(8, "bear")]);
        board.Attackers.Add(8);
        var input = new TcgMatchInput(Lookup);

        input.Sync(board);
        input.Mode.Should().Be(TcgInputMode.Blocking);

        TcgCreatureBorders.ForCreature(board, input, board.Players[0].Battlefield[0], Lookup)
                          .Should()
                          .Be(new TcgCreatureBorderChoice(TcgCreatureBorder.Selectable, TcgSelectionMode.Block));

        input.AssignBlock(board, 1, 8).Should().BeTrue();

        TcgCreatureBorders.ForCreature(board, input, board.Players[0].Battlefield[0], Lookup)
                          .Should()
                          .Be(new TcgCreatureBorderChoice(TcgCreatureBorder.Selected, TcgSelectionMode.Block));
    }

    [Test]
    public void A_highlighted_but_unpaired_blocker_counts_as_selected()
    {
        var board = Board(TcgDecision.Block, mine: [Creature(1, "bear")], theirs: [Creature(8, "bear")]);
        board.Attackers.Add(8);
        var input = new TcgMatchInput(Lookup);

        input.Sync(board);
        input.SelectBlocker(board, 1);

        TcgCreatureBorders.ForCreature(board, input, board.Players[0].Battlefield[0], Lookup)
                          .Should()
                          .Be(new TcgCreatureBorderChoice(TcgCreatureBorder.Selected, TcgSelectionMode.Block));
    }

    [Test]
    public void Blockers_that_cannot_block_and_enemy_attackers_get_no_border()
    {
        var board = Board(TcgDecision.Block, mine: [Creature(1, "bear", tapped: true), Creature(2, "bear")], theirs: [Creature(8, "drake")]);
        board.Attackers.Add(8);
        var input = new TcgMatchInput(Lookup);

        input.Sync(board);

        TcgCreatureBorders.ForCreature(board, input, board.Players[0].Battlefield[0], Lookup)
                          .Border.Should()
                          .Be(TcgCreatureBorder.None);

        TcgCreatureBorders.ForCreature(board, input, board.Players[0].Battlefield[1], Lookup)
                          .Border.Should()
                          .Be(TcgCreatureBorder.None);

        TcgCreatureBorders.ForCreature(board, input, board.Players[1].Battlefield[0], Lookup)
                          .Border.Should()
                          .Be(TcgCreatureBorder.None);
    }

    [Test]
    public void Damage_order_is_not_a_selection_mode()
    {
        var board = Board(TcgDecision.DamageOrder, mine: [Creature(1, "bear")], theirs: [Creature(8, "bear"), Creature(9, "bear")]);
        board.Attackers.Add(1);
        board.Blocks.AddRange([new TcgBlock(8, 1), new TcgBlock(9, 1)]);
        var input = new TcgMatchInput(Lookup);

        input.Sync(board);
        input.Mode.Should().Be(TcgInputMode.Ordering);

        TcgCreatureBorders.ForCreature(board, input, board.Players[0].Battlefield[0], Lookup)
                          .Border.Should()
                          .Be(TcgCreatureBorder.None);
    }
}
