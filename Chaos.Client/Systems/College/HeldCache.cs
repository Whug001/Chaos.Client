namespace Chaos.Client.Systems.College;

/// <summary>
///     Values by key, at most <c>capacity</c> of them besides those held; the least recently used unheld value goes first.
///     A value is held while a window shows it (<see cref="Hold" /> and <see cref="Release" /> in pairs), so it is never
///     released out from under the screen.
/// </summary>
public sealed class HeldCache<TValue>(int capacity, Action<TValue> release) where TValue : class
{
    private readonly Dictionary<string, Entry> Entries = new(StringComparer.Ordinal);
    private long Clock;

    public int Count => Entries.Count;

    public bool Contains(string key) => Entries.ContainsKey(key);

    public bool TryGet(string key, out TValue value)
    {
        if (!Entries.TryGetValue(key, out var entry))
        {
            value = null!;

            return false;
        }

        entry.LastUsed = ++Clock;
        value = entry.Value;

        return true;
    }

    public void Add(string key, TValue value)
    {
        if (Entries.TryGetValue(key, out var old))
        {
            if (!ReferenceEquals(old.Value, value))
                release(old.Value);

            old.Value = value;
            old.LastUsed = ++Clock;
        } else
            Entries[key] = new Entry(value, ++Clock);

        //the value just added is about to be shown, so it stays even when everything else is held
        Trim(key);
    }

    public void Hold(string key)
    {
        if (Entries.TryGetValue(key, out var entry))
            entry.Holds++;
    }

    public void Release(string key)
    {
        if (!Entries.TryGetValue(key, out var entry) || (entry.Holds == 0))
            return;

        entry.Holds--;
        Trim(null);
    }

    public void Clear()
    {
        foreach (var entry in Entries.Values)
            release(entry.Value);

        Entries.Clear();
    }

    private void Trim(string? keep)
    {
        while (Entries.Count > capacity)
        {
            string? oldest = null;
            var oldestUse = long.MaxValue;

            foreach (var (key, entry) in Entries)
                if ((entry.Holds == 0) && (entry.LastUsed < oldestUse) && (key != keep))
                {
                    oldest = key;
                    oldestUse = entry.LastUsed;
                }

            if (oldest is null)
                return;

            release(Entries[oldest].Value);
            Entries.Remove(oldest);
        }
    }

    private sealed class Entry(TValue value, long lastUsed)
    {
        public int Holds { get; set; }
        public long LastUsed { get; set; } = lastUsed;
        public TValue Value { get; set; } = value;
    }
}
