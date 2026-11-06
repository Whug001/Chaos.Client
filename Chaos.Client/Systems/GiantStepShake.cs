#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Systems;

/// <summary>
///     A short camera nudge when a giant entity steps nearby. Pure: every time-dependent call takes the time in
///     <see cref="ViewModel.TumbleClock" /> seconds and the randomness to spend.
/// </summary>
public sealed class GiantStepShake
{
    public const double DURATION_SECONDS = 0.15;
    public const float PIXELS = 2f;
    private double? StartedAt;

    public void Trigger(double nowSeconds) => StartedAt = nowSeconds;

    public Vector2 OffsetAt(double nowSeconds, Random rng)
    {
        if (StartedAt is not { } start || (nowSeconds - start) > DURATION_SECONDS || nowSeconds < start)
            return Vector2.Zero;

        return new Vector2(
            (float)(rng.NextDouble() * 2 - 1) * PIXELS,
            (float)(rng.NextDouble() * 2 - 1) * PIXELS);
    }
}
