#region
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

public enum GlassFallPhase
{
    Sink,
    Dive,
    Land,
    Done
}

/// <param name="Row">-1 for no pane (never sent today, kept so a hole is never drawn off the glass).</param>
public readonly record struct GlassCrack(uint EntityId, int Lane, int Row, int Column, double StartedAt);

public readonly record struct GlassFallFrame(GlassFallPhase Phase, double Progress);

/// <summary>
///     The client's copy of a Glass Way match: lanes in use, the round and phase clock, lit panes, trail marks, cracks
///     and the label numbers. Pure; every time-dependent call takes the time in <see cref="TumbleClock" /> seconds.
/// </summary>
public sealed class GlassWayState
{
    public const double LAND_SECONDS = 1.2;
    public const double GO_SECONDS = 0.8;
    public const double SHATTER_SECONDS = 0.6;
    private const double GLOW_EDGE = 0.2;

    private readonly Dictionary<uint, GlassCrack> Cracks = [];
    private readonly Dictionary<uint, int> LaneByEntity = [];
    private readonly HashSet<int> LaneSet = [];
    private readonly List<(int Row, int Column, double LitAt)> Lit = [];
    private readonly HashSet<(int Lane, int Row, int Column)> Marks = [];
    private double GoAt = double.NegativeInfinity;
    private double HolesSince = double.NegativeInfinity;

    public bool IsActive { get; private set; }
    public IReadOnlyCollection<int> LanesInUse => LaneSet;
    public GlassWayPhase Phase { get; private set; }
    public double PhaseLengthSeconds { get; private set; }
    public double PhaseStartedAt { get; private set; }
    public int PlayersLeft { get; private set; }
    public int Round { get; private set; }
    public int RoundCap { get; private set; }
    public int Rows { get; private set; }
    public double StepSeconds { get; private set; } = 0.6;

    public void Apply(GlassWayStateArgs args, double now)
    {
        switch (args.Kind)
        {
            case GlassWayMessageKind.Layout:
                IsActive = true;
                Round = args.Round;
                RoundCap = args.RoundCap;
                Rows = args.Rows;
                Phase = args.Phase;
                PhaseStartedAt = now - args.PhaseMs / 1000.0;
                PhaseLengthSeconds = args.PhaseLengthMs / 1000.0;

                if (args.StepMs > 0)
                    StepSeconds = args.StepMs / 1000.0;

                LaneByEntity.Clear();
                LaneSet.Clear();

                foreach (var lane in args.Lanes)
                {
                    LaneByEntity[lane.EntityId] = lane.Lane;
                    LaneSet.Add(lane.Lane);
                }

                Lit.Clear();

                foreach (var pane in args.Lit)
                    Lit.Add((pane.Row, pane.Column, now - pane.MsSince / 1000.0));

                Marks.Clear();

                foreach (var mark in args.Marks)
                    if (LaneOf(mark.EntityId) is { } markLane)
                        Marks.Add((markLane, mark.Row, mark.Column));

                //a new round starts with whole glass
                if (args.Phase == GlassWayPhase.Show)
                    HolesSince = now;

                break;

            case GlassWayMessageKind.StepLit:
                Lit.Add((args.Row, args.Column, now - args.MsSince / 1000.0));

                if (args.GlowMs > 0)
                    StepSeconds = args.GlowMs / 1000.0;

                break;

            case GlassWayMessageKind.Go:
                Phase = GlassWayPhase.Cross;
                PhaseStartedAt = now - args.MsSince / 1000.0;
                PhaseLengthSeconds = args.CrossMs / 1000.0;
                GoAt = PhaseStartedAt;
                Lit.Clear();

                break;

            case GlassWayMessageKind.Stepped:
                if (LaneOf(args.EntityId) is { } steppedLane)
                    Marks.Add((steppedLane, args.Row, args.Column));

                break;

            case GlassWayMessageKind.Cracked:
                if (LaneOf(args.EntityId) is { } crackedLane)
                    Cracks[args.EntityId] = new GlassCrack(args.EntityId, crackedLane, args.Row, args.Column, now);

                break;

            case GlassWayMessageKind.RoundEnd:
                Phase = GlassWayPhase.Pause;
                PhaseStartedAt = now;
                PhaseLengthSeconds = 0;
                Lit.Clear();
                Marks.Clear();

                break;

            case GlassWayMessageKind.Status:
                PlayersLeft = args.PlayersLeft;

                break;

            case GlassWayMessageKind.Clear:
                Clear();

                break;
        }
    }

    public void Clear()
    {
        IsActive = false;
        Cracks.Clear();
        LaneByEntity.Clear();
        LaneSet.Clear();
        Lit.Clear();
        Marks.Clear();
        GoAt = double.NegativeInfinity;
        HolesSince = double.NegativeInfinity;
        Phase = GlassWayPhase.Countdown;
        PhaseLengthSeconds = 0;
        PhaseStartedAt = 0;
        PlayersLeft = 0;
        Round = 0;
        RoundCap = 0;
        Rows = 0;
    }

    public int? LaneOf(uint entityId) => LaneByEntity.TryGetValue(entityId, out var lane) ? lane : null;

    public bool HasFallen(uint entityId) => Cracks.ContainsKey(entityId);

    public bool IsMarked(int lane, int row, int column) => Marks.Contains((lane, row, column));

    /// <summary>Rows past this round's path, which are safe to walk; none outside the show and the crossing.</summary>
    public bool IsSafeRow(int row) => IsActive && (Rows > 0) && (row >= Rows) && Phase is GlassWayPhase.Show or GlassWayPhase.Cross;

    /// <summary>How brightly the rune shows on a pane, 0 to 1: it fades in, holds, and fades out over one step.</summary>
    public double GlowAt(int row, int column, double now)
    {
        var best = 0.0;
        var edge = StepSeconds * GLOW_EDGE;

        foreach ((var r, var c, var litAt) in Lit)
        {
            if ((r != row) || (c != column))
                continue;

            var since = now - litAt;

            if ((since < 0) || (since > StepSeconds))
                continue;

            best = Math.Max(best, Math.Clamp(Math.Min(since, StepSeconds - since) / edge, 0, 1));
        }

        return best;
    }

    public double? CrossSecondsLeft(double now)
        => Phase == GlassWayPhase.Cross ? Math.Max(0, PhaseLengthSeconds - (now - PhaseStartedAt)) : null;

    public bool ShowsGo(double now) => (now >= GoAt) && (now - GoAt < GO_SECONDS);

    public GlassFallFrame? FallFrame(uint entityId, double now)
    {
        if (!Cracks.TryGetValue(entityId, out var crack))
            return null;

        var t = now - crack.StartedAt;

        if (t < GlassWayTiming.SINK_SECONDS)
            return new GlassFallFrame(GlassFallPhase.Sink, Math.Max(0, t) / GlassWayTiming.SINK_SECONDS);

        t -= GlassWayTiming.SINK_SECONDS;

        if (t < GlassWayTiming.DIVE_SECONDS)
            return new GlassFallFrame(GlassFallPhase.Dive, t / GlassWayTiming.DIVE_SECONDS);

        t -= GlassWayTiming.DIVE_SECONDS;

        return t < LAND_SECONDS ? new GlassFallFrame(GlassFallPhase.Land, t / LAND_SECONDS) : new GlassFallFrame(GlassFallPhase.Done, 1);
    }

    /// <summary>Replaces the contents of <paramref name="into" /> with the panes still shattering. Allocates nothing.</summary>
    public void CollectShattering(double now, List<GlassCrack> into)
    {
        into.Clear();

        foreach (var crack in Cracks.Values)
            if ((crack.Row >= 0) && (now - crack.StartedAt) is >= 0 and < SHATTER_SECONDS)
                into.Add(crack);
    }

    /// <summary>Replaces the contents of <paramref name="into" /> with the panes broken this round. Allocates nothing.</summary>
    public void CollectHoles(List<GlassCrack> into)
    {
        into.Clear();

        foreach (var crack in Cracks.Values)
            if ((crack.Row >= 0) && (crack.StartedAt >= HolesSince))
                into.Add(crack);
    }
}
