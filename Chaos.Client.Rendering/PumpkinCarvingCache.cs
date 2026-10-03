using System.Diagnostics.CodeAnalysis;

namespace Chaos.Client.Rendering;

/// <summary>
///     Painted frames for pumpkins being carved (spec 2026-10-03-pumpkin-carving-design.md, 12.6). A carving changes about
///     twice a second, so each (entity, frame) keeps only its latest image; the old one is released when the grid changes.
/// </summary>
public sealed class PumpkinCarvingCache<T> where T: class
{
    private readonly Dictionary<(uint EntityId, int FrameIndex), (byte[] Grid, T Value)> Entries = [];
    private readonly Action<T> Release;

    public PumpkinCarvingCache(Action<T> release) => Release = release;

    public int Count => Entries.Count;

    public void Clear()
    {
        foreach (var entry in Entries.Values)
            Release(entry.Value);

        Entries.Clear();
    }

    public bool TryGet(uint entityId, int frameIndex, byte[] grid, [MaybeNullWhen(false)] out T value)
    {
        if (Entries.TryGetValue((entityId, frameIndex), out var entry) && entry.Grid.AsSpan().SequenceEqual(grid))
        {
            value = entry.Value;

            return true;
        }

        value = null;

        return false;
    }

    /// <summary>Releases and removes every frame cached for this pumpkin.</summary>
    public void Forget(uint entityId)
    {
        foreach (var key in Entries.Keys.Where(key => key.EntityId == entityId).ToList())
        {
            Release(Entries[key].Value);
            Entries.Remove(key);
        }
    }

    /// <summary>The cached image when the grid is unchanged; otherwise paints a new one, replacing and releasing the old.</summary>
    public T? GetOrPaint(uint entityId, int frameIndex, byte[] grid, Func<T?> paint)
    {
        var key = (entityId, frameIndex);

        if (Entries.TryGetValue(key, out var entry) && entry.Grid.AsSpan().SequenceEqual(grid))
            return entry.Value;

        if (paint() is not { } painted)
            return null;

        if (Entries.TryGetValue(key, out var old))
            Release(old.Value);

        Entries[key] = (grid.ToArray(), painted);

        return painted;
    }
}
