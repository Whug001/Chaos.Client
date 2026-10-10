#region
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.ViewModel;

public enum TumbleDivePhase
{
    Sink,
    Dive,
    Land,
    Done
}

/// <summary>One fall as the client keeps it. Tiles are map coordinates; StartedAt is in <see cref="TumbleClock" /> seconds.</summary>
public sealed record TumbleFall(
    uint EntityId,
    int FromX,
    int FromY,
    int ToX,
    int ToY,
    int FloorsDropped,
    TumbleTowerFallOutcome Outcome,
    double StartedAt);

/// <param name="Segment">Which floor-to-floor dive is playing (0 for the first).</param>
/// <param name="LowerFloor">The floor coming into view, or null when diving into darkness.</param>
public readonly record struct TumbleDiveFrame(
    TumbleDivePhase Phase,
    double Progress,
    int Segment,
    int UpperFloor,
    int? LowerFloor,
    bool IntoDarkness);

/// <summary>
///     Where a fall animation is at a moment in time. Sink and dive lengths come from the shared
///     <see cref="TumbleTowerTiming" /> so the server's stun ends 1.0 s into the 1.2 s landing phase.
/// </summary>
public static class TumbleDiveTimeline
{
    public const double LAND_SECONDS = 1.2;
    public const double LAND_DROP_SECONDS = 0.25;

    public static double TotalSeconds(int floorsDropped) => TumbleTowerTiming.FallSeconds(floorsDropped) + LAND_SECONDS;

    public static TumbleDiveFrame At(TumbleFall fall, double elapsed, int fromFloor)
    {
        var dropped = Math.Max(1, fall.FloorsDropped);
        var bottomOut = fall.Outcome != TumbleTowerFallOutcome.Landed;

        if (elapsed < TumbleTowerTiming.SINK_SECONDS)
            return new TumbleDiveFrame(TumbleDivePhase.Sink, elapsed / TumbleTowerTiming.SINK_SECONDS, 0, fromFloor, fromFloor + 1, false);

        var t = elapsed - TumbleTowerTiming.SINK_SECONDS;

        for (var segment = 0; segment < dropped; segment++)
        {
            var length = segment == 0 ? TumbleTowerTiming.DIVE_SECONDS : TumbleTowerTiming.CHAIN_DIVE_SECONDS;

            if (t < length)
            {
                var last = segment == dropped - 1;
                var dark = last && bottomOut;

                return new TumbleDiveFrame(TumbleDivePhase.Dive, t / length, segment, fromFloor + segment, dark ? null : fromFloor + segment + 1, dark);
            }

            t -= length;
        }

        return t < LAND_SECONDS
            ? new TumbleDiveFrame(TumbleDivePhase.Land, t / LAND_SECONDS, dropped - 1, fromFloor + dropped - 1, null, bottomOut)
            : new TumbleDiveFrame(TumbleDivePhase.Done, 1, dropped - 1, fromFloor + dropped - 1, null, bottomOut);
    }
}
