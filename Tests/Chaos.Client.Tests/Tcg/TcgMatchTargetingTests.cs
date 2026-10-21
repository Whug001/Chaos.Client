#region
using Chaos.Client.Controls.World.Popups.Tcg.Match;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
using static Chaos.Client.Tests.TcgMatchFixtures;
#endregion

namespace Chaos.Client.Tests;

public sealed class TcgMatchTargetingTests
{
    [Test]
    public void Spell_target_on_top_of_stack_gets_the_big_card_rect()
    {
        var board = Board();
        board.Stack.Add(new TcgStackView(7, "bolt", TcgSeat.B, 0, false));

        TcgMatchWindow.TargetRect(board, new TcgMatchInput(Lookup), 7).Should().Be(TcgMatchLayout.BigCard);
    }

    [Test]
    public void Older_stack_items_get_their_row_slots()
    {
        var board = Board();
        board.Stack.Add(new TcgStackView(7, "bolt", TcgSeat.B, 0, false));
        board.Stack.Add(new TcgStackView(8, "heal", TcgSeat.A, 0, false));
        var input = new TcgMatchInput(Lookup);

        TcgMatchWindow.TargetRect(board, input, 7).Should().Be(TcgMatchLayout.StackSlot(1, 2));
        TcgMatchWindow.TargetRect(board, input, 8).Should().Be(TcgMatchLayout.BigCard);
    }

    [Test]
    public void Creature_target_still_gets_its_slot_rect()
    {
        var board = Board(mine: [Creature(3, "bear")]);
        var feet = TcgMatchLayout.CreatureSlot(0, 1, true);

        TcgMatchWindow.TargetRect(board, new TcgMatchInput(Lookup), 3)
                      .Should()
                      .Be(new Microsoft.Xna.Framework.Rectangle(feet.X - 24, feet.Y - 48, 48, 48));
    }

    [Test]
    public void Confirmed_attackers_and_blockers_target_their_shifted_spots()
    {
        var board = Board(TcgDecision.Respond, mine: [Creature(1, "bear")], theirs: [Creature(8, "bear")]);
        board.Attackers.Add(8);
        board.Blocks.Add(new TcgBlock(1, 8));
        var input = new TcgMatchInput(Lookup);

        var attackerFeet = TcgMatchWindow.DrawFeet(board, input, 8, false, 0, 1);
        attackerFeet.Should().NotBe(TcgMatchLayout.CreatureSlot(0, 1, false));

        TcgMatchWindow.TargetRect(board, input, 8)
                      .Should()
                      .Be(new Microsoft.Xna.Framework.Rectangle(attackerFeet.X - 24, attackerFeet.Y - 48, 48, 48));

        var blockerFeet = TcgMatchWindow.DrawFeet(board, input, 1, true, 0, 1);
        blockerFeet.Should().NotBe(TcgMatchLayout.CreatureSlot(0, 1, true));

        TcgMatchWindow.TargetRect(board, input, 1)
                      .Should()
                      .Be(new Microsoft.Xna.Framework.Rectangle(blockerFeet.X - 24, blockerFeet.Y - 48, 48, 48));
    }

    [Test]
    public void Unknown_target_gets_no_rect()
        => TcgMatchWindow.TargetRect(Board(), new TcgMatchInput(Lookup), 99).Should().BeNull();

    [Test]
    public void Phase_line_prompts_for_a_target_while_targeting()
    {
        var board = Board(mana: [Mana(1, TcgElement.Fire)], theirs: [Creature(9, "bear")]);
        board.Players[0].Hand.Add(new TcgCardView(6, "bolt"));
        var input = new TcgMatchInput(Lookup);

        input.ClickHandCard(board, 6);
        input.ChoosePlay(board);

        input.Mode.Should().Be(TcgInputMode.Targeting);
        TcgMatchWindow.PhaseOverride(board, input, "Bo", false, Lookup).Should().Be("Click a target. Esc cancels.");
    }
}
