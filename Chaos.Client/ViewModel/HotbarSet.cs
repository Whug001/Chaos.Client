#region
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>What a hotbar slot holds.</summary>
public enum HotbarEntryKind : byte
{
    Item,
    Skill,
    Spell
}

/// <summary>
///     The viewport edge a locked hotbar sits against. Bars locked to the same edge stack outward from it in bar order.
///     The order is the F4 dropdown's, and is saved as the index.
/// </summary>
public enum HotbarDock : byte
{
    Bottom,
    Top,
    Left,
    Right
}

/// <summary>
///     One hotbar slot's contents: an item, skill or spell, remembered by name rather than by slot.
/// </summary>
/// <remarks>
///     By name because the slot moves. An item is dragged around the inventory and a stack runs out and is bought again;
///     a skill levels, which changes its full name, or is moved to the other page. The name -- an item's without its stack
///     count, an ability's without its level -- is what stays the same through all of that. The sprite and colour are
///     kept so the slot can still show what it holds while the player has none of it.
/// </remarks>
public readonly record struct HotbarEntry(HotbarEntryKind Kind, string Name, ushort Sprite, DisplayColor Color = DisplayColor.Default)
{
    private const char SEPARATOR = '|';

    /// <summary>"Kind|Sprite|Color|Name". The name goes last so a name with the separator in it still reads back whole.</summary>
    public string Serialize() => $"{Kind}{SEPARATOR}{Sprite}{SEPARATOR}{(int)Color}{SEPARATOR}{Name}";

    public static bool TryParse(string text, out HotbarEntry entry)
    {
        entry = default;

        var parts = text.Split(SEPARATOR, 4);

        if ((parts.Length != 4)
            || !Enum.TryParse<HotbarEntryKind>(parts[0], out var kind)
            || !Enum.IsDefined(kind)
            || !ushort.TryParse(parts[1], out var sprite)
            || !int.TryParse(parts[2], out var color)
            || string.IsNullOrWhiteSpace(parts[3]))
            return false;

        entry = new HotbarEntry(kind, parts[3], sprite, (DisplayColor)color);

        return true;
    }
}

/// <summary>
///     The contents of the four hotbars: up to twelve <see cref="HotbarEntry" /> each. How many slots a bar shows is an F4
///     setting; a bar cut down to fewer slots keeps what was in the rest, so turning it back up brings them back.
/// </summary>
public sealed class HotbarSet
{
    public const int BAR_COUNT = 4;
    public const int MAX_SLOTS = 12;

    private const string KEY_PREFIX = "Bar";

    private readonly HotbarEntry?[,] Entries = new HotbarEntry?[BAR_COUNT, MAX_SLOTS];

    /// <summary>Fired whenever a slot's contents change, so the owner can save.</summary>
    public event Action? Changed;

    public static bool IsValid(int bar, int slot) => bar is >= 0 and < BAR_COUNT && slot is >= 0 and < MAX_SLOTS;

    public HotbarEntry? Get(int bar, int slot) => IsValid(bar, slot) ? Entries[bar, slot] : null;

    public void Set(int bar, int slot, HotbarEntry? entry)
    {
        if (!IsValid(bar, slot) || (Entries[bar, slot] == entry))
            return;

        Entries[bar, slot] = entry;
        Changed?.Invoke();
    }

    /// <summary>Exchanges two slots' contents, on the same bar or different ones. Moving onto an empty slot is a swap with nothing.</summary>
    public void Swap(int fromBar, int fromSlot, int toBar, int toSlot)
    {
        if (!IsValid(fromBar, fromSlot) || !IsValid(toBar, toSlot) || ((fromBar == toBar) && (fromSlot == toSlot)))
            return;

        (Entries[fromBar, fromSlot], Entries[toBar, toSlot]) = (Entries[toBar, toSlot], Entries[fromBar, fromSlot]);
        Changed?.Invoke();
    }

    public void Clear()
    {
        Array.Clear(Entries);
        Changed?.Invoke();
    }

    /// <summary>One "Bar1Slot3 : Spell|12|0|beag ioc" line per filled slot, bars and slots counted from 1.</summary>
    public IEnumerable<string> ToLines()
    {
        for (var bar = 0; bar < BAR_COUNT; bar++)
            for (var slot = 0; slot < MAX_SLOTS; slot++)
                if (Entries[bar, slot] is { } entry)
                    yield return $"{KEY_PREFIX}{bar + 1}Slot{slot + 1} : {entry.Serialize()}";
    }

    /// <summary>
    ///     Replaces the contents with what <paramref name="lines" /> hold. A line that cannot be read is skipped; it never
    ///     costs the rest of the file.
    /// </summary>
    public void LoadLines(IEnumerable<string> lines)
    {
        Array.Clear(Entries);

        foreach (var line in lines)
        {
            var colonIndex = line.IndexOf(':');

            if (colonIndex < 0)
                continue;

            var key = line[..colonIndex]
                .Trim();

            var value = line[(colonIndex + 1)..]
                .Trim();

            if (TryParseKey(key, out var bar, out var slot) && HotbarEntry.TryParse(value, out var entry))
                Entries[bar, slot] = entry;
        }

        Changed?.Invoke();
    }

    private static bool TryParseKey(string key, out int bar, out int slot)
    {
        bar = -1;
        slot = -1;

        if (!key.StartsWith(KEY_PREFIX, StringComparison.Ordinal))
            return false;

        var slotIndex = key.IndexOf("Slot", StringComparison.Ordinal);

        if ((slotIndex < 0)
            || !int.TryParse(key.AsSpan(KEY_PREFIX.Length, slotIndex - KEY_PREFIX.Length), out var barNumber)
            || !int.TryParse(key.AsSpan(slotIndex + 4), out var slotNumber))
            return false;

        bar = barNumber - 1;
        slot = slotNumber - 1;

        return IsValid(bar, slot);
    }
}
