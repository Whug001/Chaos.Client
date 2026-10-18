#region
using Chaos.Client.Controls.World.Popups.Tcg.Match;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Tests;

public sealed class TcgMatchAnimationTests
{
    [Test]
    public void Only_the_creature_an_animation_draws_is_hidden()
    {
        TcgMatchWindow.AnimatedIdsFor(null).Should().BeEmpty();
        TcgMatchWindow.AnimatedIdsFor(new TcgEventView(TcgEventType.Attacked, TcgSeat.A, 7, -1, 0, "")).Should().Equal(7);
        TcgMatchWindow.AnimatedIdsFor(new TcgEventView(TcgEventType.Blocked, TcgSeat.A, 4, 7, 0, "")).Should().Equal(4);
        TcgMatchWindow.AnimatedIdsFor(new TcgEventView(TcgEventType.CreatureEntered, TcgSeat.A, 6, 0, 0, "")).Should().Equal(6);
        TcgMatchWindow.AnimatedIdsFor(new TcgEventView(TcgEventType.CreatureDestroyed, TcgSeat.A, 0, 5, 0, "")).Should().Equal(5);
        TcgMatchWindow.AnimatedIdsFor(new TcgEventView(TcgEventType.Damaged, TcgSeat.A, 7, 5, 3, "")).Should().BeEmpty();
        TcgMatchWindow.AnimatedIdsFor(new TcgEventView(TcgEventType.PlayerDamaged, TcgSeat.A, 0, 0, 3, "")).Should().BeEmpty();
        TcgMatchWindow.AnimatedIdsFor(new TcgEventView(TcgEventType.LifeGained, TcgSeat.A, 0, 0, 2, "")).Should().BeEmpty();
        TcgMatchWindow.AnimatedIdsFor(new TcgEventView(TcgEventType.CardCast, TcgSeat.A, 0, 0, 0, "bolt")).Should().BeEmpty();
        TcgMatchWindow.AnimatedIdsFor(new TcgEventView(TcgEventType.TrapRevealed, TcgSeat.A, 0, 0, 0, "snare")).Should().BeEmpty();
    }

    [Test]
    public void Settled_ids_keep_what_earlier_events_changed()
    {
        var past = new List<TcgEventView>
        {
            new(TcgEventType.Attacked, TcgSeat.A, 7, -1, 0, ""),
            new(TcgEventType.Damaged, TcgSeat.A, 7, 5, 3, ""),
            new(TcgEventType.CreatureDestroyed, TcgSeat.A, 0, 5, 0, ""),
            new(TcgEventType.CreatureEntered, TcgSeat.A, 6, 0, 0, "")
        };
        var destroyed = new HashSet<int>();
        var fresh = new HashSet<int>();

        TcgMatchWindow.SettledIds(past, destroyed, fresh);

        destroyed.Should().Equal(5);
        fresh.Should().BeEquivalentTo([7, 6]);

        TcgMatchWindow.SettledIds([new TcgEventView(TcgEventType.CreatureDestroyed, TcgSeat.A, 6, 0, 0, "")], destroyed, fresh);

        destroyed.Should().BeEquivalentTo([5, 6]);
        fresh.Should().Equal(7);
    }

    [Test]
    public void Attack_run_walks_then_strikes()
    {
        var start = new Point(235, 316);
        var river = new Point(235, 212);
        var target = new Point(235, 176);

        TcgMatchWindow.AttackedFeet(start, river, target, 0).Should().Be(start);
        TcgMatchWindow.AttackedFeet(start, river, target, 0.2f).Should().Be(new Point(235, 264));
        TcgMatchWindow.AttackedFeet(start, river, target, 0.4f).Should().Be(river);
        TcgMatchWindow.AttackedFeet(start, river, target, 0.7f).Should().Be(target);
        TcgMatchWindow.AttackedFeet(start, river, target, 1).Should().Be(target);

        TcgMatchWindow.AttackPose(0.69f).Should().Be(TcgBoardCreature.Pose.Walk);
        TcgMatchWindow.AttackPose(0.7f).Should().Be(TcgBoardCreature.Pose.Attack);
    }

    [Test]
    public void Blocker_steps_toward_the_attacker()
    {
        TcgMatchWindow.BlockedFeet(new Point(235, 220), new Point(235, 176)).Should().Be(new Point(235, 208));
        TcgMatchWindow.BlockedFeet(new Point(235, 220), new Point(235, 220)).Should().Be(new Point(235, 220));
    }

    [Test]
    public void Only_foil_ante_cards_shimmer()
    {
        var board = new TcgDisplayArgs
        {
            Type = TcgDisplayType.Update,
            Ante = [new TcgAnteView(TcgSeat.A, "bear", true), new TcgAnteView(TcgSeat.B, "bolt", false)]
        };

        TcgMatchWindow.IsFoilAnte(board, "bear").Should().BeTrue();
        TcgMatchWindow.IsFoilAnte(board, "bolt").Should().BeFalse();
        TcgMatchWindow.IsFoilAnte(board, "missing").Should().BeFalse();
        TcgMatchWindow.IsFoilAnte(board, null).Should().BeFalse();
    }

    [Test]
    public void Banner_frame_matches_the_outcome()
    {
        TcgMatchWindow.BannerFrame(TcgEndOutcome.Victory).Should().Be(0);
        TcgMatchWindow.BannerFrame(TcgEndOutcome.Defeat).Should().Be(1);
        TcgMatchWindow.BannerFrame(TcgEndOutcome.Draw).Should().Be(2);
    }

    [Test]
    public void Turn_banner_names_whose_turn_it_is()
    {
        var board = new TcgDisplayArgs
        {
            Type = TcgDisplayType.Update,
            YourSeat = TcgSeat.A,
            Players = [new TcgPlayerView { Name = "Ana" }, new TcgPlayerView { Name = "Bo" }]
        };

        TcgMatchWindow.TurnBannerText(board, new TcgEventView(TcgEventType.TurnStarted, TcgSeat.A, 0, 0, 0, "")).Should().Be("Ana's turn!");
        TcgMatchWindow.TurnBannerText(board, new TcgEventView(TcgEventType.TurnStarted, TcgSeat.B, 0, 0, 0, "")).Should().Be("Bo's turn!");
    }
}
