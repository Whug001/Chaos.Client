namespace Chaos.Client.Models;

/// <summary>
///     A damage-absorption barrier plus the peak value it has held since it was last empty. Barrier indicators divide
///     by the peak rather than by maximum health, so a freshly granted barrier always renders completely full and
///     drains as it absorbs damage and decays. Any grant that pushes the barrier past its previous high-water mark
///     re-baselines the peak, refilling the indicator.
/// </summary>
public sealed class BarrierState
{
    /// <summary>
    ///     Damage the barrier can still absorb, as last reported by the server. 0 = no barrier.
    /// </summary>
    public uint Remaining { get; private set; }

    /// <summary>
    ///     The highest <see cref="Remaining" /> seen since the barrier was last empty. 0 = no barrier.
    /// </summary>
    public uint Peak { get; private set; }

    public bool IsActive => Remaining > 0;

    /// <summary>
    ///     How much of <see cref="Peak" /> is still standing, 0-1. 0 when there is no barrier.
    /// </summary>
    public float Fraction => Peak > 0 ? Math.Clamp((float)Remaining / Peak, 0f, 1f) : 0f;

    /// <summary>
    ///     Applies a server-reported barrier value, re-baselining <see cref="Peak" /> when the barrier grows and
    ///     clearing it when the barrier expires.
    /// </summary>
    public void Set(uint remaining)
    {
        Remaining = remaining;

        //a grant that pushes the barrier past its previous high-water mark restarts the indicator at full; reaching
        //zero clears the mark so the next grant does the same instead of measuring against a long-gone barrier
        Peak = remaining == 0 ? 0 : Math.Max(Peak, remaining);
    }
}
