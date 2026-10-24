namespace Chaos.Client.Systems.Cinema;

public static class CinemaSync
{
    public const int CHECK_INTERVAL_MS = 5000;
    public const int RETRY_INTERVAL_MS = 15000;
    public const int SEEK_THRESHOLD_MS = 1500;
    //the server stops a film within 1 s of its length, so a real end is cleared before this runs out.
    public const int ENDED_GRACE_MS = 2000;

    /// <summary>Where to start so the film lines up with the server, given the round-trip ping (half of it is the trip here).</summary>
    public static long StartPosition(long serverMs, long? pingMs) => serverMs + Math.Max(0, pingMs ?? 0) / 2;

    public static bool NeedsSeek(long actualMs, long expectedMs) => Math.Abs(actualMs - expectedMs) > SEEK_THRESHOLD_MS;
}
