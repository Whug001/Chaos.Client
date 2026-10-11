#region
using Chaos.DarkAges.Definitions;
using FluentAssertions;
#endregion

namespace Chaos.Client.Tests;

/// <summary>
///     The same numbers as the server's CausewayScheduleTests. The client compiles the schedule from the Chaos-Server
///     submodule, so this fails if the client is built against a server without the same maths.
/// </summary>
public class CausewayScheduleCrossCheckTests
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

    [Test]
    public void The_client_computes_the_servers_positions()
    {
        CausewaySchedule.Steps(Right, 1499).Should().Be(2);
        CausewaySchedule.Fraction(Right, 1250).Should().BeApproximately(0.5, 1e-9);
        CausewaySchedule.PieceAt(Right, 3, 2).Should().Be(0);
        CausewaySchedule.PieceAt(Right, 15, 0).Should().Be(-1);
        CausewaySchedule.PieceAt(Right, 1, 5).Should().Be(2);
        CausewaySchedule.Ride(Right, 3, 2, 4).Should().Be(5);
        CausewaySchedule.Ride(Right, 14, 0, 1).Should().BeNull();
        CausewaySchedule.ShellState(new CausewayShellTiming(8000, 1000, 2000), new CausewayPiece(1, 2), 6000).Should().Be(CausewayShellState.Sunk);
        CausewaySchedule.VisualStart(Right, Right.Pieces[0], 1250).Should().BeApproximately(2.5, 1e-9);
    }
}
