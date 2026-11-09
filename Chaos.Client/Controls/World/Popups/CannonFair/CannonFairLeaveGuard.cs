namespace Chaos.Client.Controls.World.Popups.CannonFair;

public sealed class CannonFairLeaveGuard
{
    private const long WINDOW_MS = 3000;
    private long? PressedAt;

    public bool Press(long nowMs)
    {
        if (PressedAt is { } first && (nowMs - first <= WINDOW_MS))
            return true;

        PressedAt = nowMs;

        return false;
    }

    public bool Warning(long nowMs) => PressedAt is { } first && (nowMs - first <= WINDOW_MS);
}
