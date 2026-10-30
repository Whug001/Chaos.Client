#region
using Chaos.DarkAges.Definitions;
using Chaos.Geometry.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>
///     The client's copy of a Drowned Causeway match: the lane schedule, the play clock, the viewer's side and hold, and
///     each entity's raft drift. Pure; every time-dependent call takes the time in <see cref="TumbleClock" /> seconds or
///     a play time in milliseconds.
/// </summary>
public sealed class CausewayState
{
    private readonly List<CausewayLane> AllLanes = [];
    private long FrozenPlayMs;
    private double HoldUntil = double.MinValue;
    private double PlayStartedAt;

    public bool IsActive => AllLanes.Count > 0;
    public DrownedCausewayPhase Phase { get; private set; }
    public CausewayRideTracker Rides { get; } = new();
    public IEnumerable<CausewayLane> RiverLanes => AllLanes.Where(l => l.Kind != CausewayLaneKind.Road);
    public CausewayShellTiming ShellTiming { get; private set; } = new(8000, 1000, 2000);
    public CausewaySide? ViewerSide { get; private set; }

    public void Apply(DrownedCausewayStateArgs args, double now, double latencySeconds)
    {
        switch (args.Kind)
        {
            case DrownedCausewayMessageKind.Schedule:
                AllLanes.Clear();
                AllLanes.AddRange(args.Lanes);
                ShellTiming = new CausewayShellTiming(args.ShellPeriodMs, args.ShellWarnMs, args.ShellSunkMs);
                ViewerSide = args.ViewerSide;
                HoldUntil = args.HoldMs > 0 ? now + args.HoldMs / 1000.0 : double.MinValue;
                Rides.Clear();
                Sync(args, now, latencySeconds);

                break;

            case DrownedCausewayMessageKind.State:
                Sync(args, now, latencySeconds);

                if (args.HoldMs > 0)
                    HoldUntil = now + args.HoldMs / 1000.0;

                break;

            case DrownedCausewayMessageKind.Clear:
                Clear();

                break;
        }
    }

    /// <summary>Whether the viewer may not walk onto (x, y): the other team's start, or their own flag bank tiles.</summary>
    public bool BlocksStep(int x, int y)
        => IsActive
           && ViewerSide is { } side
           && (CausewayLayout.IsStart(CausewayLayout.Opponent(side), y) || (CausewayLayout.BankIndex(side, x, y) >= 0));

    public void Clear()
    {
        AllLanes.Clear();
        Phase = DrownedCausewayPhase.Countdown;
        ViewerSide = null;
        HoldUntil = double.MinValue;
        FrozenPlayMs = 0;
        Rides.Clear();
    }

    public bool IsFloating(int x, int y, long playMs) => RiverLaneAt(y) is { } lane && CausewaySchedule.Floats(lane, ShellTiming, x, playMs);

    public bool IsMovementLocked(double now) => IsActive && ((Phase == DrownedCausewayPhase.Countdown) || (now < HoldUntil));

    public long PlayMs(double now) => Phase == DrownedCausewayPhase.Ended ? FrozenPlayMs : (long)Math.Round((now - PlayStartedAt) * 1000);

    /// <summary>
    ///     The raft lane a creature walk from (oldX, oldY) belongs to when it is a ride step: along the lane's direction,
    ///     from a tile a piece covered just before or just after now. Null for an ordinary walk.
    /// </summary>
    public CausewayLane? RideLaneFor(int oldX, int oldY, Direction direction, long playMs)
    {
        if (RiverLaneAt(oldY) is not { } lane)
            return null;

        var dx = direction switch
        {
            Direction.Right => 1,
            Direction.Left  => -1,
            _               => 0
        };

        if (dx != lane.Direction)
            return null;

        var steps = CausewaySchedule.Steps(lane, playMs);

        for (var s = Math.Max(0, steps - 1); s <= steps + 1; s++)
            if (CausewaySchedule.PieceAt(lane, oldX, s) >= 0)
                return lane;

        return null;
    }

    public CausewayLane? RiverLaneAt(int row)
    {
        //a loop, not FirstOrDefault: the predicate lambda allocates a closure on every lane lookup
        foreach (var lane in AllLanes)
            if ((lane.Row == row) && (lane.Kind != CausewayLaneKind.Road))
                return lane;

        return null;
    }

    private void Sync(DrownedCausewayStateArgs args, double now, double latencySeconds)
    {
        Phase = args.Phase;
        PlayStartedAt = now - args.ElapsedMs / 1000.0 - latencySeconds / 2;
        FrozenPlayMs = args.ElapsedMs;
    }
}
