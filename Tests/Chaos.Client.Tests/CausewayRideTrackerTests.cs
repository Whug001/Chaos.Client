#region
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using FluentAssertions;
#endregion

namespace Chaos.Client.Tests;

public class CausewayRideTrackerTests
{
    private static readonly CausewayLane Right = new()
    {
        Kind = CausewayLaneKind.Raft,
        Row = 8,
        Direction = 1,
        IntervalMs = 500,
        RingLength = 18,
        Pieces = [new CausewayPiece(0, 3), new CausewayPiece(6, 4), new CausewayPiece(13, 2)]
    };

    private static readonly CausewayLane Left = new()
    {
        Kind = CausewayLaneKind.Raft,
        Row = 10,
        Direction = -1,
        IntervalMs = 500,
        RingLength = 18,
        Pieces = [new CausewayPiece(11, 3), new CausewayPiece(4, 4), new CausewayPiece(17, 2)]
    };

    private static readonly CausewayLane StillShell = new()
    {
        Kind = CausewayLaneKind.Shell,
        Row = 9,
        Direction = 1,
        IntervalMs = 1_000_000,
        RingLength = 18,
        Pieces = [new CausewayPiece(1, 2)]
    };

    private static readonly CausewayShellTiming Timing = new(8000, 1000, 2000);

    [Test]
    public void Standing_on_a_raft_drifts_with_it()
        => new CausewayRideTracker().Offset(1, 3, 8, 1, Right, Timing, 250).Should().BeApproximately(0.5, 1e-9);

    [Test]
    public void A_local_step_is_continuous()
    {
        var tracker = new CausewayRideTracker();

        tracker.Offset(1, 3, 8, 1, Right, Timing, 499).Should().BeApproximately(0.998, 1e-9);
        tracker.Shifted(1, 4, 8, Right, 500);
        tracker.Offset(1, 4, 8, 1, Right, Timing, 500).Should().BeApproximately(0, 1e-9);
        tracker.Offset(1, 4, 8, 1, Right, Timing, 750).Should().BeApproximately(0.5, 1e-9);
    }

    [Test]
    public void A_late_server_step_is_continuous()
    {
        var tracker = new CausewayRideTracker();

        tracker.Offset(1, 3, 8, 1, Right, Timing, 0).Should().Be(0);
        tracker.Offset(1, 3, 8, 1, Right, Timing, 600).Should().BeApproximately(1.2, 1e-9);
        tracker.Shifted(1, 4, 8, Right, 600);
        tracker.Offset(1, 4, 8, 1, Right, Timing, 600).Should().BeApproximately(0.2, 1e-9);
    }

    [Test]
    public void An_early_server_step_is_continuous()
    {
        var tracker = new CausewayRideTracker();

        tracker.Offset(1, 3, 8, 1, Right, Timing, 480).Should().BeApproximately(0.96, 1e-9);
        tracker.Shifted(1, 4, 8, Right, 480);
        tracker.Offset(1, 4, 8, 1, Right, Timing, 480).Should().BeApproximately(-0.04, 1e-9);
    }

    [Test]
    public void A_raft_step_during_a_walk_is_continuous()
    {
        var tracker = new CausewayRideTracker();

        tracker.Offset(1, 2, 7, 1, null, Timing, 0).Should().Be(0);
        var before = 2 + tracker.Offset(1, 2, 8, 0.5, Right, Timing, 499);
        tracker.Shifted(1, 3, 8, Right, 500);
        var after = 3 + tracker.Offset(1, 3, 8, 0.5, Right, Timing, 500);

        after.Should().BeApproximately(before, 0.01);
    }

    [Test]
    public void Walking_off_a_raft_blends_the_drift_out()
    {
        var tracker = new CausewayRideTracker();
        tracker.Offset(1, 3, 8, 1, Right, Timing, 250);

        tracker.Offset(1, 3, 7, 0, null, Timing, 250).Should().BeApproximately(0.5, 1e-9);
        tracker.Offset(1, 3, 7, 0.5, null, Timing, 300).Should().BeApproximately(0.25, 1e-9);
        tracker.Offset(1, 3, 7, 1, null, Timing, 350).Should().Be(0);
    }

    [Test]
    public void Walking_onto_a_raft_blends_the_drift_in()
    {
        var tracker = new CausewayRideTracker();
        tracker.Offset(1, 3, 7, 1, null, Timing, 0);

        tracker.Offset(1, 3, 8, 0, Right, Timing, 250).Should().Be(0);
        tracker.Offset(1, 3, 8, 1, Right, Timing, 250).Should().BeApproximately(0.5, 1e-9);
    }

    [Test]
    public void A_sunk_shell_does_not_drift()
    {
        var tracker = new CausewayRideTracker();

        tracker.Offset(1, 2, 9, 1, StillShell, Timing, 6100).Should().Be(0);
        tracker.Offset(2, 2, 9, 1, StillShell, Timing, 5500).Should().BeApproximately(0.0055, 1e-9);
    }

    [Test]
    public void Leftward_lanes_drift_left()
        => new CausewayRideTracker().Offset(1, 6, 10, 1, Left, Timing, 250).Should().BeApproximately(-0.5, 1e-9);
}
