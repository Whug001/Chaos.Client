#region
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Geometry.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
#endregion

namespace Chaos.Client.Tests;

public class CausewayStateTests
{
    private static readonly CausewayLane Raft = new()
    {
        Kind = CausewayLaneKind.Raft,
        Row = 8,
        Direction = 1,
        IntervalMs = 500,
        RingLength = 18,
        Pieces = [new CausewayPiece(0, 3), new CausewayPiece(6, 4), new CausewayPiece(13, 2)]
    };

    private static readonly CausewayLane Road = new()
    {
        Kind = CausewayLaneKind.Road,
        Row = 5,
        Direction = -1,
        IntervalMs = 300,
        RingLength = 14,
        Pieces = [new CausewayPiece(2, 1)]
    };

    private static DrownedCausewayStateArgs Schedule(DrownedCausewayPhase phase = DrownedCausewayPhase.Playing, int elapsedMs = 1000)
        => new()
        {
            Kind = DrownedCausewayMessageKind.Schedule,
            Phase = phase,
            ElapsedMs = elapsedMs,
            MapId = 782,
            ViewerSide = CausewaySide.North,
            ShellPeriodMs = 8000,
            ShellWarnMs = 1000,
            ShellSunkMs = 2000,
            Lanes = [Road, Raft]
        };

    [Test]
    public void A_schedule_starts_the_clock_with_half_the_latency()
    {
        var state = new CausewayState();
        state.Apply(Schedule(), 50.0, 0.1);

        state.IsActive.Should().BeTrue();
        state.PlayMs(50.0).Should().Be(1050);
        state.PlayMs(50.5).Should().Be(1550);
        state.RiverLanes.Should().ContainSingle().Which.Row.Should().Be(8);
        state.RiverLaneAt(8).Should().Be(Raft);
        state.RiverLaneAt(5).Should().BeNull();
    }

    [Test]
    public void An_ended_match_freezes_the_clock()
    {
        var state = new CausewayState();
        state.Apply(Schedule(), 10, 0);
        state.Apply(new DrownedCausewayStateArgs { Kind = DrownedCausewayMessageKind.State, Phase = DrownedCausewayPhase.Ended, ElapsedMs = 5000 }, 20, 0);

        state.PlayMs(99).Should().Be(5000);
    }

    [Test]
    public void Floating_follows_the_rafts()
    {
        var state = new CausewayState();
        state.Apply(Schedule(), 0, 0);

        state.IsFloating(3, 8, 1250).Should().BeTrue();
        state.IsFloating(2, 8, 1250).Should().BeFalse();
        state.IsFloating(3, 7, 1250).Should().BeFalse();
    }

    [Test]
    public void The_viewer_cannot_step_into_the_enemy_start_or_onto_their_own_bank()
    {
        var state = new CausewayState();
        state.Apply(Schedule(), 0, 0);

        state.BlocksStep(5, 17).Should().BeTrue();
        state.BlocksStep(3, 3).Should().BeTrue();
        state.BlocksStep(4, 3).Should().BeFalse();
        state.BlocksStep(12, 15).Should().BeFalse();
        state.BlocksStep(5, 1).Should().BeFalse();
    }

    [Test]
    public void Movement_is_locked_in_the_countdown_and_during_a_hold()
    {
        var state = new CausewayState();
        state.Apply(Schedule(DrownedCausewayPhase.Countdown, -3000), 0, 0);
        state.IsMovementLocked(1).Should().BeTrue();

        state.Apply(new DrownedCausewayStateArgs { Kind = DrownedCausewayMessageKind.State, Phase = DrownedCausewayPhase.Playing, ElapsedMs = 0 }, 3, 0);
        state.IsMovementLocked(3).Should().BeFalse();

        state.Apply(new DrownedCausewayStateArgs { Kind = DrownedCausewayMessageKind.State, Phase = DrownedCausewayPhase.Playing, ElapsedMs = 7000, HoldMs = 3000 }, 10, 0);
        state.IsMovementLocked(12.9).Should().BeTrue();
        state.IsMovementLocked(13.1).Should().BeFalse();
    }

    [Test]
    public void A_schedule_sets_its_own_hold_and_drops_an_old_one()
    {
        var state = new CausewayState();
        state.Apply(Schedule() with { HoldMs = 3000 }, 0, 0);
        state.IsMovementLocked(2.9).Should().BeTrue();

        state.Apply(new DrownedCausewayStateArgs { Kind = DrownedCausewayMessageKind.State, Phase = DrownedCausewayPhase.Playing, ElapsedMs = 7000, HoldMs = 3000 }, 10, 0);
        state.Apply(Schedule(), 11, 0);
        state.IsMovementLocked(12).Should().BeFalse();
    }

    [Test]
    public void A_south_viewer_is_blocked_from_the_north_start_and_the_south_bank()
    {
        var state = new CausewayState();
        state.Apply(Schedule() with { ViewerSide = CausewaySide.South }, 0, 0);

        state.BlocksStep(5, 1).Should().BeTrue();
        state.BlocksStep(12, 15).Should().BeTrue();
        state.BlocksStep(3, 3).Should().BeFalse();
        state.BlocksStep(5, 17).Should().BeFalse();
    }

    [Test]
    public void A_creature_walk_along_a_raft_is_a_ride_step()
    {
        var state = new CausewayState();
        state.Apply(Schedule(), 0, 0);

        state.RideLaneFor(3, 8, Direction.Right, 1500).Should().Be(Raft);
        state.RideLaneFor(3, 8, Direction.Left, 1500).Should().BeNull();
        state.RideLaneFor(3, 7, Direction.Right, 1500).Should().BeNull();
    }

    [Test]
    public void Clear_turns_it_off()
    {
        var state = new CausewayState();
        state.Apply(Schedule(), 0, 0);
        state.Apply(new DrownedCausewayStateArgs { Kind = DrownedCausewayMessageKind.Clear }, 1, 0);

        state.IsActive.Should().BeFalse();
        state.BlocksStep(5, 17).Should().BeFalse();
    }
}
