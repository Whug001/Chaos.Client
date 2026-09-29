#region
using System.Globalization;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>The admin window's pool being edited: add, remove, reorder and price items, then save or revert to the last saved pool.</summary>
public sealed class TownImportPoolEditor
{
    private List<TownImportItemInfo> Saved = [];

    /// <summary>The pool as currently edited.</summary>
    public List<TownImportItemInfo> Items { get; } = [];

    /// <summary>The list differs from the last pool the server sent.</summary>
    public bool Unsaved { get; private set; }

    /// <summary>Replaces the pool with the one the server sent and clears the unsaved mark.</summary>
    public void Load(IReadOnlyList<TownImportItemInfo> pool)
    {
        Saved = pool.Select(item => item with { }).ToList();
        Items.Clear();
        Items.AddRange(Saved.Select(item => item with { }));
        Unsaved = false;
    }

    /// <summary>Adds a search result at the default price. False when it is already listed or the pool is full.</summary>
    public bool Add(TownImportItemInfo result)
    {
        if ((Items.Count >= TownImportProtocol.MAX_POOL)
            || Items.Any(item => item.TemplateKey.Equals(result.TemplateKey, StringComparison.OrdinalIgnoreCase)))
            return false;

        Items.Add(result with { Price = TownImportProtocol.DEFAULT_PRICE });
        Unsaved = true;

        return true;
    }

    /// <summary>Removes the item at <paramref name="index" />, if there is one.</summary>
    public void Remove(int index)
    {
        if ((index < 0) || (index >= Items.Count))
            return;

        Items.RemoveAt(index);
        Unsaved = true;
    }

    /// <summary>Moves the item at <paramref name="index" /> by <paramref name="delta" /> places. Returns where it ends up.</summary>
    public int Move(int index, int delta)
    {
        if ((index < 0) || (index >= Items.Count))
            return index;

        var target = Math.Clamp(index + delta, 0, Items.Count - 1);

        if (target == index)
            return index;

        var item = Items[index];
        Items.RemoveAt(index);
        Items.Insert(target, item);
        Unsaved = true;

        return target;
    }

    /// <summary>Sets a price from typed text ("150,000" is fine). False, changing nothing, for text that is not a whole number above 0.</summary>
    public bool SetPrice(int index, string text)
    {
        if ((index < 0) || (index >= Items.Count))
            return false;

        var digits = (text ?? string.Empty).Replace(",", string.Empty).Trim();

        if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var price) || (price <= 0))
            return false;

        Items[index] = Items[index] with { Price = price };
        Unsaved = true;

        return true;
    }

    /// <summary>Discards edits and goes back to the last loaded pool.</summary>
    public void Revert() => Load(Saved);

    /// <summary>The pool as entries to send to the server, in order.</summary>
    public IReadOnlyList<TownImportPoolEntry> ToEntries()
        => Items.Select(item => new TownImportPoolEntry { TemplateKey = item.TemplateKey, Price = item.Price }).ToList();
}
