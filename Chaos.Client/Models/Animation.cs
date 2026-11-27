#region
using Chaos.Client.Data.Definitions;
#endregion

namespace Chaos.Client.Models;

/// <summary>
///     Tracks a single in-flight spell/effect animation. Can be attached to an entity or positioned at a tile.
/// </summary>
public sealed class Animation
{
    public EffectBlendMode BlendMode { get; init; }
    public int CurrentFrame { get; set; }
    public int EffectId { get; init; }
    public float ElapsedMs { get; set; }
    public int FrameCount { get; init; }
    public float FrameIntervalMs { get; init; }
    public bool IsBackground { get; init; }
    public uint? TargetEntityId { get; init; }
    public int? TileX { get; init; }
    public int? TileY { get; init; }

    public bool IsComplete => CurrentFrame >= FrameCount;

    /// <summary>
    ///     Set when the same effect was sent again just as this copy was finishing: instead of ending, it plays once more from
    ///     its first frame, so art the server keeps up loops without a blink.
    /// </summary>
    public bool LoopOnce { get; set; }

    /// <summary>Milliseconds left before this copy plays its last frame out.</summary>
    public float RemainingMs => ((FrameCount - CurrentFrame) * FrameIntervalMs) - ElapsedMs;

    /// <summary>Milliseconds one full play of this copy takes.</summary>
    public float LengthMs => FrameCount * FrameIntervalMs;
}