using Chaos.Networking.Entities.Server;

namespace Chaos.Client.ViewModel;

/// <summary>The seen snapshot of one quest: the stage text as last seen and whether it was ready.</summary>
public sealed record QuestSeen(string Text, bool Ready);

/// <summary>The per-character persisted quest watch data: the seen snapshot, the pinned quest key and the manually dismissed keys.</summary>
public sealed class QuestWatchData
{
    public Dictionary<string, QuestSeen> Seen { get; set; } = [];
    public string? PinnedKey { get; set; }
    public HashSet<string> Dismissed { get; set; } = [];
}

/// <summary>
///     Watches the quest log list the server sends for unseen changes (the quest button badge) and owns the single
///     pinned quest. The seen snapshot persists per character: a quest counts as unseen when its key is missing from
///     the snapshot (New), its stage text differs (Advanced), or it just became Ready. Opening the quest log window
///     replaces the snapshot with the current list and clears the badge. A finished pin unpins itself, and exactly
///     one new quest auto-pins at level 41 or below while the tracker setting is on.
/// </summary>
public sealed class QuestWatchState
{
    private readonly List<QuestLogEntryInfo> EntryList = [];
    private readonly Dictionary<string, QuestSeen> Seen = [];
    private readonly HashSet<string> Dismissed = [];
    private string? PinnedKeyField;

    /// <summary>True when the last list holds a New, Advanced or Ready quest.</summary>
    public bool HasUnseen { get; private set; }

    public string? PinnedKey => PinnedKeyField;

    /// <summary>The pinned entry from the last list, or null when nothing is pinned.</summary>
    public QuestLogEntryInfo? Pinned => PinnedKeyField is null ? null : EntryList.FirstOrDefault(entry => entry.Key == PinnedKeyField);

    /// <summary>True when the persisted data changed since the last load, reset or clear.</summary>
    public bool Dirty { get; private set; }

    /// <summary>The badge, pin or tracker changed.</summary>
    public event Action? Changed;

    /// <summary>True when the entry carries progress lines and every line is complete.</summary>
    public static bool IsReady(QuestLogEntryInfo entry)
        => (entry.Progress.Count > 0) && entry.Progress.All(line => line.Have == line.Need);

    public void Load(QuestWatchData data)
    {
        Seen.Clear();

        foreach (var pair in data.Seen)
            Seen[pair.Key] = pair.Value;

        PinnedKeyField = data.PinnedKey;
        Dismissed.Clear();
        Dismissed.UnionWith(data.Dismissed);
        Dirty = false;
        Changed?.Invoke();
    }

    public QuestWatchData Export()
        => new()
        {
            Seen = new Dictionary<string, QuestSeen>(Seen),
            PinnedKey = PinnedKeyField,
            Dismissed = [.. Dismissed]
        };

    public void Reset()
    {
        EntryList.Clear();
        Seen.Clear();
        Dismissed.Clear();
        PinnedKeyField = null;
        HasUnseen = false;
        Dirty = false;
        Changed?.Invoke();
    }

    public void OnEntries(IReadOnlyList<QuestLogEntryInfo> entries, int level, bool trackerEnabled)
    {
        EntryList.Clear();
        EntryList.AddRange(entries);

        var keys = new HashSet<string>(EntryList.Select(entry => entry.Key));
        var anyNew = false;
        var anyAdvanced = false;
        var anyReady = false;

        foreach (var entry in EntryList)
        {
            if (!Seen.TryGetValue(entry.Key, out var seen))
            {
                anyNew = true;

                if (IsReady(entry))
                    anyReady = true;
            } else
            {
                if (seen.Text != entry.Text)
                    anyAdvanced = true;

                if (!seen.Ready && IsReady(entry))
                    anyReady = true;
            }
        }

        HasUnseen = anyNew || anyAdvanced || anyReady;

        if (Dismissed.RemoveWhere(key => !keys.Contains(key)) > 0)
            Dirty = true;

        if ((PinnedKeyField is not null) && !keys.Contains(PinnedKeyField))
        {
            PinnedKeyField = null;
            Dirty = true;
        }

        if ((PinnedKeyField is null) && trackerEnabled && (level <= 41))
        {
            var next = EntryList.FirstOrDefault(entry => !Seen.ContainsKey(entry.Key) && !Dismissed.Contains(entry.Key));

            if (next is not null)
            {
                PinnedKeyField = next.Key;
                Dirty = true;
            }
        }

        Changed?.Invoke();
    }

    /// <summary>Copies the current list into the seen snapshot (Text, IsReady), drops missing keys and clears the badge.</summary>
    public void MarkSeen()
    {
        var fresh = new Dictionary<string, QuestSeen>(EntryList.Count);

        foreach (var entry in EntryList)
            fresh[entry.Key] = new QuestSeen(entry.Text, IsReady(entry));

        var changed = (fresh.Count != Seen.Count)
            || fresh.Any(pair => !Seen.TryGetValue(pair.Key, out var old) || old != pair.Value);
        var hadUnseen = HasUnseen;

        Seen.Clear();

        foreach (var pair in fresh)
            Seen[pair.Key] = pair.Value;

        HasUnseen = false;

        if (changed)
            Dirty = true;

        if (changed || hadUnseen)
            Changed?.Invoke();
    }

    public void Pin(string key)
    {
        if (Dismissed.Remove(key))
            Dirty = true;

        if (PinnedKeyField != key)
        {
            PinnedKeyField = key;
            Dirty = true;
        }

        Changed?.Invoke();
    }

    public void Unpin()
    {
        if (PinnedKeyField is null)
            return;

        Dismissed.Add(PinnedKeyField);
        PinnedKeyField = null;
        Dirty = true;
        Changed?.Invoke();
    }

    public void ClearDirty() => Dirty = false;
}
