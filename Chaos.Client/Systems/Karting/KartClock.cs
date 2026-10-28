using Chaos.DarkAges.Karting;

namespace Chaos.Client.Systems.Karting;

/// <summary>Estimates the server's race step from snapshot ticks and the local clock.</summary>
public sealed class KartClock
{
    private const double SMOOTHING = 0.1;
    private const double SNAP_STEPS = 15;

    private double? Offset;

    public bool Synced => Offset.HasValue;

    public void OnSnapshot(uint serverTick, double nowSeconds)
    {
        var target = serverTick - nowSeconds * KartPhysics.STEPS_PER_SECOND;

        if (Offset is not { } offset || Math.Abs(target - offset) > SNAP_STEPS)
            Offset = target;
        else
            Offset = offset + (target - offset) * SMOOTHING;
    }

    public uint Estimate(double nowSeconds)
        => Offset is { } offset ? (uint)Math.Max(0, Math.Round(nowSeconds * KartPhysics.STEPS_PER_SECOND + offset)) : 0;

    public void Reset() => Offset = null;
}
