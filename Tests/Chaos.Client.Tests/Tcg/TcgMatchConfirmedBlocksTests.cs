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

public sealed class TcgMatchConfirmedBlocksTests
{
    private static TcgMatchInput IdleInput() => new(Lookup);

    private static TcgDisplayArgs ConfirmedBoard(
        int attackerId,
        int blockerId,
        TcgSeat yourSeat = TcgSeat.A,
        bool attackerYours = false,
        bool spectating = false)
    {
        List<TcgCreatureView> mine = attackerYours ? [Creature(attackerId, "bear")] : [Creature(blockerId, "bear")];
        List<TcgCreatureView> theirs = attackerYours ? [Creature(blockerId, "bear")] : [Creature(attackerId, "bear")];
        var board = Board(mine: mine, theirs: theirs);

        board.YourSeat = yourSeat;
        board.Spectating = spectating;
        board.Attackers.Add(attackerId);
        board.Blocks.Add(new TcgBlock(blockerId, attackerId));

        return board;
    }

    [Test]
    public void The_attacker_sees_the_confirmed_block()
    {
        var board = ConfirmedBoard(attackerId: 1, blockerId: 8, attackerYours: true);
        var input = IdleInput();

        TcgMatchWindow.ShowsConfirmedBlocks(board, input).Should().BeTrue();

        var arrows = TcgMatchWindow.ConfirmedBlockArrows(board, input, Lookup);

        arrows.Should().ContainSingle().Which.Blocked.Should().BeTrue();

        var attackerFeet = TcgMatchWindow.DrawFeet(board, input, 1, true, 0, 1);
        attackerFeet.Should().Be(new Point(235, 316 - 24));

        var blockerFeet = TcgMatchWindow.DrawFeet(board, input, 8, false, 0, 1);
        blockerFeet.Should().Be(new Point(235, 316 - 24 - 44));

        arrows[0].From.Should().Be(new Point(attackerFeet.X, attackerFeet.Y - 24));
        arrows[0].To.Should().Be(new Point(blockerFeet.X, blockerFeet.Y - 24));

        TcgMatchWindow.OwnCreatureIdAt(board, input, attackerFeet.X, attackerFeet.Y - 10).Should().Be(1);
        TcgMatchWindow.HoverAt(board, input, false, attackerFeet.X, attackerFeet.Y - 10)!.Value.CreatureId.Should().Be(1);
        TcgMatchWindow.HoverAt(board, input, false, blockerFeet.X, blockerFeet.Y - 10)!.Value.CreatureId.Should().Be(8);
    }

    [Test]
    public void A_spectator_sees_confirmed_blocks_from_either_seat()
    {
        var fromA = ConfirmedBoard(attackerId: 8, blockerId: 1, spectating: true);
        var arrowsA = TcgMatchWindow.ConfirmedBlockArrows(fromA, IdleInput(), Lookup);

        arrowsA.Should().ContainSingle().Which.Blocked.Should().BeTrue();
        arrowsA[0].To.Should().Be(new Point(235, 176 + 24 + 44 - 24));

        var hoverA = TcgMatchWindow.HoverAt(fromA, IdleInput(), true, 235, 176 + 24 + 44 - 10);
        hoverA!.Value.CreatureId.Should().Be(1);

        var fromB = Board(mine: [Creature(9, "bear")], theirs: [Creature(3, "bear")]);
        fromB.YourSeat = TcgSeat.B;
        fromB.Spectating = true;
        fromB.Attackers.Add(9);
        fromB.Blocks.Add(new TcgBlock(3, 9));

        var arrowsB = TcgMatchWindow.ConfirmedBlockArrows(fromB, IdleInput(), Lookup);

        arrowsB.Should().ContainSingle().Which.Blocked.Should().BeTrue();
        arrowsB[0].To.Should().Be(new Point(235, 176 + 24 + 44 - 24));

        var hoverB = TcgMatchWindow.HoverAt(fromB, IdleInput(), true, 235, 176 + 24 + 44 - 10);
        hoverB!.Value.CreatureId.Should().Be(3);
    }

    [Test]
    public void Nothing_is_shown_while_the_block_decision_is_pending()
    {
        var board = Board(TcgDecision.Block, mine: [Creature(1, "bear")], theirs: [Creature(8, "bear")]);
        board.Attackers.Add(8);
        var input = IdleInput();

        TcgMatchWindow.ShowsConfirmedBlocks(board, input).Should().BeFalse();
        TcgMatchWindow.ConfirmedBlockArrows(board, input, Lookup).Should().BeEmpty();

        TcgMatchWindow.DrawFeet(board, input, 8, false, 0, 1).Should().Be(TcgMatchLayout.CreatureSlot(0, 1, false));
        TcgMatchWindow.DrawFeet(board, input, 1, true, 0, 1).Should().Be(TcgMatchLayout.CreatureSlot(0, 1, true));
    }

    [Test]
    public void Attackers_with_no_blocks_show_unblocked_arrows()
    {
        var board = Board(TcgDecision.Respond, mine: [Creature(1, "bear")], theirs: [Creature(8, "bear")]);
        board.Attackers.Add(8);
        var input = IdleInput();

        //the attack window and a zero-block confirm look the same on the wire, so both show the arrows
        TcgMatchWindow.ShowsConfirmedBlocks(board, input).Should().BeTrue();

        var arrows = TcgMatchWindow.ConfirmedBlockArrows(board, input, Lookup);

        arrows.Should().ContainSingle();
        arrows[0].Blocked.Should().BeFalse();
        arrows[0].To.Should().Be(TcgMatchLayout.YourSeal.Center);
        arrows[0].From.Should().Be(new Point(235, 176 + 24 - 24));

        TcgMatchWindow.DrawFeet(board, input, 8, false, 0, 1).Should().Be(new Point(235, 176 + 24));
        TcgMatchWindow.DrawFeet(board, input, 1, true, 0, 1).Should().Be(TcgMatchLayout.CreatureSlot(0, 1, true));
    }

    [Test]
    public void Animation_starts_where_the_confirmed_creatures_stand()
    {
        var board = ConfirmedBoard(attackerId: 1, blockerId: 8, attackerYours: true);
        var input = IdleInput();

        var blocker = TcgMatchWindow.AnimationStart(board, input, 8);

        blocker.Should().NotBeNull();
        blocker.Value.Yours.Should().BeFalse();
        blocker.Value.Feet.Should().Be(TcgMatchWindow.DrawFeet(board, input, 8, false, 0, 1));
        blocker.Value.Feet.Should().Be(new Point(235, 316 - 24 - 44));

        var attacker = TcgMatchWindow.AnimationStart(board, input, 1);

        attacker.Should().NotBeNull();
        attacker.Value.Yours.Should().BeTrue();
        attacker.Value.Feet.Should().Be(new Point(235, 316 - 24));

        TcgMatchWindow.AnimationStart(board, input, 99).Should().BeNull();
    }

    [Test]
    public void An_unblocked_attacker_points_at_the_defenders_seal()
    {
        var board = Board(mine: [Creature(1, "bear")], theirs: [Creature(8, "bear"), Creature(9, "bear")]);
        board.Attackers.Add(8);
        board.Attackers.Add(9);
        board.Blocks.Add(new TcgBlock(1, 8));
        var input = IdleInput();

        var arrows = TcgMatchWindow.ConfirmedBlockArrows(board, input, Lookup);

        arrows.Should().HaveCount(2);

        var blocked = arrows.Single(arrow => arrow.Blocked);
        arrows.Where(arrow => arrow.Blocked).Should().ContainSingle();
        blocked.To.Should().Be(new Point(235 - 32, 176 + 24 + 44 - 24));

        var unblocked = arrows.Single(arrow => !arrow.Blocked);
        arrows.Where(arrow => !arrow.Blocked).Should().ContainSingle();
        unblocked.To.Should().Be(TcgMatchLayout.YourSeal.Center);
    }

    [Test]
    public void The_defender_sees_the_same_view_after_confirming()
    {
        var choosing = Board(TcgDecision.Block, mine: [Creature(1, "bear")], theirs: [Creature(8, "bear")]);
        choosing.Attackers.Add(8);
        var live = new TcgMatchInput(Lookup);
        live.Sync(choosing);
        live.AssignBlock(choosing, 1, 8).Should().BeTrue();

        var liveBlocker = TcgMatchWindow.DrawFeet(choosing, live, 1, true, 0, 1);
        var liveAttacker = TcgMatchWindow.DrawFeet(choosing, live, 8, false, 0, 1);

        var confirmed = ConfirmedBoard(attackerId: 8, blockerId: 1);
        var idle = IdleInput();

        TcgMatchWindow.DrawFeet(confirmed, idle, 1, true, 0, 1).Should().Be(liveBlocker);
        TcgMatchWindow.DrawFeet(confirmed, idle, 8, false, 0, 1).Should().Be(liveAttacker);
        TcgMatchWindow.ConfirmedBlockArrows(confirmed, idle, Lookup).Should().ContainSingle();
    }

    [Test]
    public void A_blocker_of_a_dead_attacker_holds_its_normal_spot()
    {
        var board = ConfirmedBoard(attackerId: 8, blockerId: 1);
        board.Players[1].Battlefield.Clear();
        var input = IdleInput();

        var home = TcgMatchLayout.CreatureSlot(0, 1, true);

        TcgMatchWindow.DrawFeet(board, input, 1, true, 0, 1).Should().Be(home);

        var start = TcgMatchWindow.AnimationStart(board, input, 1);
        start.Should().NotBeNull();
        start.Value.Feet.Should().Be(home);

        TcgMatchWindow.TargetRect(board, input, 1)
                      .Should()
                      .Be(new Rectangle(home.X - 24, home.Y - 48, 48, 48));
    }

    [Test]
    public void An_attacker_blocked_only_by_dead_blockers_points_nowhere_without_trample()
    {
        var board = Board(mine: [Creature(1, "bear")], theirs: [Creature(8, "bear")]);
        board.Attackers.Add(8);
        board.Blocks.Add(new TcgBlock(1, 8));
        board.Players[0].Battlefield.Clear();
        var input = IdleInput();

        TcgMatchWindow.ConfirmedBlockArrows(board, input, Lookup).Should().BeEmpty();
    }

    [Test]
    public void An_attacker_blocked_only_by_dead_blockers_still_points_with_trample()
    {
        var board = Board(mine: [Creature(1, "bear")], theirs: [Creature(8, "boar")]);
        board.Attackers.Add(8);
        board.Blocks.Add(new TcgBlock(1, 8));
        board.Players[0].Battlefield.Clear();
        var input = IdleInput();

        var arrows = TcgMatchWindow.ConfirmedBlockArrows(board, input, Lookup);

        arrows.Should().ContainSingle();
        arrows[0].Blocked.Should().BeFalse();
        arrows[0].To.Should().Be(TcgMatchLayout.YourSeal.Center);
    }

    [Test]
    public void Damage_order_keeps_its_own_layout_and_clicks()
    {
        var board = Board(TcgDecision.DamageOrder, mine: [Creature(1, "bear")], theirs: [Creature(8, "bear"), Creature(9, "bear")]);
        board.Attackers.Add(1);
        board.Blocks.AddRange([new TcgBlock(8, 1), new TcgBlock(9, 1)]);
        var input = new TcgMatchInput(Lookup);

        input.Sync(board);
        input.Mode.Should().Be(TcgInputMode.Ordering);

        TcgMatchWindow.DrawFeet(board, input, 8, false, 0, 2).Should().Be(TcgMatchLayout.CreatureSlot(0, 2, false));
        TcgMatchWindow.DrawFeet(board, input, 9, false, 1, 2).Should().Be(TcgMatchLayout.CreatureSlot(1, 2, false));

        var far = TcgMatchLayout.CreatureSlot(1, 2, false);
        TcgMatchWindow.OrderedBlockerIdAt(board, input, far.X, far.Y - 10).Should().Be(9);
    }
}
