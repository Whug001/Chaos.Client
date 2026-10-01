#region
using Chaos.Geometry.Abstractions.Definitions;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Systems;

/// <summary>
///     Where each character stood (tile-space, walk offset included) and which way it faced over the last
///     <see cref="KEEP_MS" />. The mirror renderer records every character once a frame on maps with mirrors.
/// </summary>
public sealed class EntityTrail
{
    public const long KEEP_MS = 2000;

    private readonly Dictionary<uint, List<Sample>> SamplesById = [];

    public void Clear() => SamplesById.Clear();

    /// <summary>How many samples are kept for <paramref name="id" />. For tests.</summary>
    public int Count(uint id) => SamplesById.TryGetValue(id, out var samples) ? samples.Count : 0;

    /// <summary>Forgets characters with no sample in the last <see cref="KEEP_MS" />.</summary>
    public void Prune(long nowMs)
    {
        foreach (var id in SamplesById.Where(kvp => kvp.Value[^1].Ms < nowMs - KEEP_MS)
                                      .Select(kvp => kvp.Key)
                                      .ToList())
            SamplesById.Remove(id);
    }

    public void Record(uint id, long ms, Vector2 tile, Direction facing)
    {
        if (!SamplesById.TryGetValue(id, out var samples))
            SamplesById[id] = samples = [];

        if ((samples.Count > 0) && (samples[^1].Ms == ms))
            samples[^1] = new Sample(ms, tile, facing);
        else
            samples.Add(new Sample(ms, tile, facing));

        //keep the newest sample older than the window, so a lookup exactly KEEP_MS back still finds one
        var cutoff = ms - KEEP_MS;
        var drop = 0;

        while ((drop + 1 < samples.Count) && (samples[drop + 1].Ms <= cutoff))
            drop++;

        if (drop > 0)
            samples.RemoveRange(0, drop);
    }

    /// <summary>The latest sample at or before <paramref name="ms" />.</summary>
    public bool TryGet(uint id, long ms, out Vector2 tile, out Direction facing)
    {
        tile = default;
        facing = default;

        if (!SamplesById.TryGetValue(id, out var samples))
            return false;

        for (var i = samples.Count - 1; i >= 0; i--)
            if (samples[i].Ms <= ms)
            {
                tile = samples[i].Tile;
                facing = samples[i].Facing;

                return true;
            }

        return false;
    }

    private readonly record struct Sample(long Ms, Vector2 Tile, Direction Facing);
}
