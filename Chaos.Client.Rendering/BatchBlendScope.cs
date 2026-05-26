#region
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Wraps a <see cref="SpriteBatch" /> in Deferred mode and breaks + reopens the batch only when a different
///     <see cref="BlendState" /> is requested. Lets a single draw pass stay batched by default while still drawing the
///     occasional additive/screen sprite, replacing the old per-draw <c>device.BlendState</c> mutation that required
///     <see cref="SpriteSortMode.Immediate" />. Reused per frame: call <see cref="Begin" /> once, <see cref="Require" />
///     before any non-AlphaBlend draw (and again to restore), then <see cref="End" />.
/// </summary>
public sealed class BatchBlendScope
{
    private SamplerState Sampler = null!;
    private RasterizerState Rasterizer = null!;
    private Matrix Transform;
    private BlendState CurrentBlend = null!;

    /// <summary>The wrapped batch — use for the actual <c>.Draw</c> calls. The instance is stable across breaks.</summary>
    public SpriteBatch Batch { get; private set; } = null!;

    /// <summary>Opens the pass. Everything is fixed except the blend, which <see cref="Require" /> can change.</summary>
    public void Begin(
        SpriteBatch batch,
        BlendState initial,
        SamplerState sampler,
        RasterizerState rasterizer,
        Matrix transform)
    {
        Batch = batch;
        Sampler = sampler;
        Rasterizer = rasterizer;
        Transform = transform;
        CurrentBlend = initial;
        Batch.Begin(SpriteSortMode.Deferred, initial, sampler, null, rasterizer, null, transform);
    }

    /// <summary>No-op if the blend already matches; otherwise flush the current sub-batch and reopen with the new blend.</summary>
    public void Require(BlendState blend)
    {
        if (blend == CurrentBlend)
            return;

        Batch.End();
        CurrentBlend = blend;
        Batch.Begin(SpriteSortMode.Deferred, blend, Sampler, null, Rasterizer, null, Transform);
    }

    /// <summary>Ends the active sub-batch.</summary>
    public void End() => Batch.End();
}
