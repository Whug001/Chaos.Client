using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Chaos.Client.Rendering;

/// <summary>
///     Painted lit pumpkin frames, keyed by carving content (spec 2026-10-03-pumpkin-carving-design.md, 11.3). Every round
///     brings new carvings, so once the cache passes its limit the least recently drawn entries are released down to the
///     trim size. The limit sits well above what one screen draws, so drawing never causes repainting.
/// </summary>
public sealed class PumpkinLitCache<TKey, T> where TKey: notnull
{
    private readonly Dictionary<TKey, (T Value, long LastUse)> Entries = [];
    private readonly int Limit;
    private readonly Action<T> Release;
    private readonly int Trim;
    private long UseCounter;

    public PumpkinLitCache(int limit, int trim, Action<T> release)
    {
        Limit = limit;
        Trim = trim;
        Release = release;
    }

    public int Count => Entries.Count;

    public void Add(TKey key, T value)
    {
        if (Entries.Remove(key, out var old))
            Release(old.Value);

        Entries[key] = (value, ++UseCounter);

        if (Entries.Count <= Limit)
            return;

        foreach ((var oldKey, var entry) in Entries.OrderBy(entry => entry.Value.LastUse)
                                                   .Take(Entries.Count - Trim)
                                                   .ToList())
        {
            Entries.Remove(oldKey);
            Release(entry.Value);
        }
    }

    public void Clear()
    {
        foreach (var entry in Entries.Values)
            Release(entry.Value);

        Entries.Clear();
    }

    public bool TryGet(TKey key, [MaybeNullWhen(false)] out T value)
    {
        ref var entry = ref CollectionsMarshal.GetValueRefOrNullRef(Entries, key);

        if (Unsafe.IsNullRef(ref entry))
        {
            value = default!;

            return false;
        }

        entry.LastUse = ++UseCounter;
        value = entry.Value;

        return true;
    }
}
