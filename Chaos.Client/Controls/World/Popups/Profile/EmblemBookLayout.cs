#region
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Controls.World.Popups.Profile;

/// <summary>UI-free rules for the Emblem tab: paging, slot positions, and the text for time left and status.</summary>
public static class EmblemBookLayout
{
    public const int COLUMNS = 5;
    public const int PAGE_SIZE = COLUMNS * 5;
    public const int SLOT_SIZE = 36;
    public const int SLOT_STRIDE_X = 43;
    public const int SLOT_STRIDE_Y = 45;

    public static int IndexOf(IReadOnlyList<EmblemBookEntry> entries, string? key)
    {
        if (string.IsNullOrEmpty(key))
            return -1;

        for (var i = 0; i < entries.Count; i++)
            if (string.Equals(entries[i].Key, key, StringComparison.OrdinalIgnoreCase))
                return i;

        return -1;
    }

    public static int NextPage(int page, int entryCount) => (page + 1) % PageCount(entryCount);

    public static int PageCount(int entryCount) => Math.Max(1, (entryCount + PAGE_SIZE - 1) / PAGE_SIZE);

    public static string PageText(int page, int entryCount) => $"{page + 1}/{PageCount(entryCount)}";

    public static int PrevPage(int page, int entryCount)
    {
        var count = PageCount(entryCount);

        return (page - 1 + count) % count;
    }

    /// <summary>A grid slot's offset from the grid's top-left corner.</summary>
    public static (int X, int Y) SlotOffset(int slot) => (slot % COLUMNS * SLOT_STRIDE_X, slot / COLUMNS * SLOT_STRIDE_Y);

    /// <summary>The line under the description: who holds a record, when the player earned it, or Locked.</summary>
    public static string StatusText(EmblemBookEntry entry)
    {
        if (entry.Record && (entry.Holder.Length > 0))
            return $"Held by {entry.Holder}";

        if (entry.Owned && (entry.GrantedUnix > 0))
            return $"Earned {DateTimeOffset.FromUnixTimeSeconds(entry.GrantedUnix).UtcDateTime:yyyy-MM-dd}";

        return entry.Owned ? string.Empty : "Locked";
    }

    public static string TimeLeftText(EmblemBookEntry entry, long secondsLeftNow)
    {
        if (entry.Record)
            return "While first place";

        if (!entry.Owned)
            return "Locked";

        if (entry.SecondsLeft == 0)
            return "Never";

        if (secondsLeftNow <= 0)
            return "Expired";

        var left = TimeSpan.FromSeconds(secondsLeftNow);

        if (left.TotalDays >= 1)
            return $"{(int)left.TotalDays}d {left.Hours}h";

        if (left.TotalHours >= 1)
            return $"{left.Hours}h {left.Minutes}m";

        return $"{Math.Max(1, left.Minutes)}m";
    }
}
