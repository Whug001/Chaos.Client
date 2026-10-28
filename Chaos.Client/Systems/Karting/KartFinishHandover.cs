using Chaos.DarkAges.Karting;

namespace Chaos.Client.Systems.Karting;

public sealed class KartFinishHandover
{
    public const double MAX_WAIT_SECONDS = 0.5;
    public const double CATCH_UP_DISTANCE = 0.1;

    private KartBody? FrozenBody;
    private double StartTime;
    private bool Done;

    public void Capture(KartBody body, double now)
    {
        if (Done || FrozenBody is not null)
            return;

        FrozenBody = body;
        StartTime = now;
    }

    public KartBody? BodyFor(KartBody? interpolated, double now)
    {
        if (Done)
            return interpolated;

        if (FrozenBody is not { } frozen)
            return interpolated;

        if (ShouldKeepFrozen(frozen, interpolated, now - StartTime))
            return frozen;

        FrozenBody = null;
        Done = true;

        return interpolated;
    }

    public void Reset()
    {
        FrozenBody = null;
        StartTime = 0;
        Done = false;
    }

    public static bool ShouldKeepFrozen(KartBody frozen, KartBody? interpolated, double elapsedSeconds)
    {
        if (elapsedSeconds >= MAX_WAIT_SECONDS)
            return false;

        if (interpolated is not { } sample)
            return true;

        var dx = sample.X - frozen.X;
        var dy = sample.Y - frozen.Y;
        var distanceSquared = dx * dx + dy * dy;

        if (distanceSquared <= CATCH_UP_DISTANCE * CATCH_UP_DISTANCE)
            return false;

        var alongHeading = dx * Math.Cos(frozen.Heading) + dy * Math.Sin(frozen.Heading);

        return alongHeading < 0;
    }
}