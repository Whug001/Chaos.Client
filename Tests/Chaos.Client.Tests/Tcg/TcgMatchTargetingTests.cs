#region
using Chaos.Client.Controls.World.Popups.Tcg.Match;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
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

        TcgMatchWindow.TargetRect(board, 7).Should().Be(TcgMatchLayout.BigCard);
    }

    [Test]
    public void Buried_stack_item_gets_no_rect()
    {
        var board = Board();
        board.Stack.Add(new TcgStackView(7, "bolt", TcgSeat.B, 0, false));
        board.Stack.Add(new TcgStackView(8, "heal", TcgSeat.A, 0, false));

        TcgMatchWindow.TargetRect(board, 7).Should().BeNull();
        TcgMatchWindow.TargetRect(board, 8).Should().Be(TcgMatchLayout.BigCard);
    }

    [Test]
    public void Creature_target_still_gets_its_slot_rect()
    {
        var board = Board(mine: [Creature(3, "bear")]);
        var feet = TcgMatchLayout.CreatureSlot(0, 1, true);

        TcgMatchWindow.TargetRect(board, 3)
                      .Should()
                      .Be(new Microsoft.Xna.Framework.Rectangle(feet.X - 24, feet.Y - 48, 48, 48));
    }

    [Test]
    public void Unknown_target_gets_no_rect()
        => TcgMatchWindow.TargetRect(Board(), 99).Should().BeNull();

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
