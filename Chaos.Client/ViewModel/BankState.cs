using Chaos.Extensions.Common;
using Chaos.Networking.Entities.Server;

namespace Chaos.Client.ViewModel;

/// <summary>
///     The bank window's authoritative state, fed by BankDisplay packets. The category rail and the open category's
///     items live here; the control is a view over it.
/// </summary>
public sealed class BankState
{
    /// <summary>
    ///     The category rail — only categories holding matching items.
    /// </summary>
    public IReadOnlyList<string> Categories { get; private set; } = [];

    /// <summary>
    ///     The bank's gold.
    /// </summary>
    public uint Gold { get; private set; }

    /// <summary>
    ///     The open category's items, with full stats.
    /// </summary>
    public IReadOnlyList<BankItemEntry> Items { get; private set; } = [];

    /// <summary>
    ///     The category whose items are currently loaded.
    /// </summary>
    public string SelectedCategory { get; private set; } = string.Empty;

    /// <summary>
    ///     Raised when either the rail or the item list changes.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    ///     Clears everything. Called when the window closes.
    /// </summary>
    public void Clear()
    {
        Gold = 0;
        Categories = [];
        SelectedCategory = string.Empty;
        Items = [];
        Changed?.Invoke();
    }

    /// <summary>
    ///     Applies a Categories display. Clears the item list if the open category no longer exists.
    /// </summary>
    public void SetCategories(BankDisplayArgs args)
    {
        Gold = args.Gold;
        Categories = args.Categories ?? [];

        if (Categories.All(category => !category.EqualsI(SelectedCategory)))
        {
            SelectedCategory = string.Empty;
            Items = [];
        }

        Changed?.Invoke();
    }

    /// <summary>
    ///     Points the window at a category. Called when the request goes out, not when the items come back.
    /// </summary>
    /// <remarks>
    ///     The items are dropped on a real change so no row is ever shown under the wrong heading — for the round trip it
    ///     takes the new ones to arrive, the list is empty rather than lying. Re-selecting the category already open (what
    ///     every refresh does) leaves them alone, so a deposit does not blink the list.
    /// </remarks>
    public void SelectCategory(string categoryName)
    {
        if (SelectedCategory.EqualsI(categoryName))
            return;

        SelectedCategory = categoryName;
        Items = [];

        Changed?.Invoke();
    }

    /// <summary>
    ///     Applies an Items display.
    /// </summary>
    /// <remarks>
    ///     Replies are drained in request order, so these items belong to the last category
    ///     <see cref="SelectCategory" /> pointed the window at.
    /// </remarks>
    public void SetItems(BankDisplayArgs args)
    {
        Items = args.Items ?? [];

        Changed?.Invoke();
    }
}
