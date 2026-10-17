#region
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>The binder window's tabs, in the order they are drawn.</summary>
public enum TcgBinderTab : byte
{
    All,
    Fire,
    Water,
    Wind,
    Earth,
    Neutral,
    Decks
}

/// <summary>
///     The card binder's last snapshot from the server, and what the binder window shows: the tab, "Owned only", the page
///     and the selected card. The server checks every request again; the rules here only decide which buttons are enabled
///     and what the questions say.
/// </summary>
public sealed class TcgCollectionState
{
    public const int MAX_COPIES = 3;
    public const int MAX_DECKS = 12;
    public const int PAGE_SIZE = 12;

    private readonly Dictionary<string, TcgCatalogCardView> ByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (int Normal, int Foil)> Counts = new(StringComparer.OrdinalIgnoreCase);
    private List<TcgCatalogCardView> Sorted = [];

    /// <summary>Every card in the catalog, sorted by element (fire, water, wind, earth, neutral), then cost, then name.</summary>
    public IReadOnlyList<TcgCatalogCardView> Cards => Sorted;

    public IReadOnlyList<TcgDeckView> Decks { get; private set; } = [];
    public int Dust { get; private set; }

    /// <summary>The cards the current tab and "Owned only" show.</summary>
    public IReadOnlyList<TcgCatalogCardView> Filtered => Filter(Tab, OwnedOnly);

    public bool HasSnapshot { get; private set; }

    /// <summary>Why the server refused the last request, or empty.</summary>
    public string Message { get; private set; } = string.Empty;

    public bool OwnedOnly { get; private set; }
    public int Page { get; private set; }
    public IReadOnlyList<TcgCatalogCardView> PageCards => PageOf(Filtered, Page);
    public int PageCount => PageCountOf(Filtered.Count);
    public string PageText => PageTextOf(Filtered, Page);
    public TcgCatalogCardView? Selected => Get(SelectedKey);
    public string? SelectedKey { get; private set; }
    public TcgBinderTab Tab { get; private set; }

    /// <summary>Takes a snapshot. Other display kinds leave the state alone.</summary>
    public void Apply(TcgCollectionDisplayArgs args)
    {
        if (args.Kind != TcgCollectionDisplayKind.Snapshot)
            return;

        ByKey.Clear();
        Counts.Clear();

        foreach (var card in args.Cards)
            ByKey[card.Key] = card;

        foreach (var owned in args.Owned)
            Counts[owned.Key] = (owned.Normal, owned.Foil);

        Sorted = ByKey.Values.ToList();
        Sorted.Sort(Compare);
        Decks = args.Decks.ToList();
        Dust = args.Dust;
        Message = args.Message;
        HasSnapshot = true;

        if ((SelectedKey is not null) && !ByKey.ContainsKey(SelectedKey))
            SelectedKey = null;

        Page = Math.Clamp(Page, 0, PageCount - 1);
    }

    public static TcgCollectionActionArgs BreakAction(string key, bool foil)
        => new()
        {
            Type = TcgCollectionActionType.Break,
            CardKey = key,
            Foil = foil
        };

    public string BreakQuestion(TcgCatalogCardView card, bool foil) => $"Break {CardName(card, foil)} for {BreakValue(card.Rarity, foil)} dust?";

    /// <summary>Mirrors the server's <c>TcgCollectionRules.BreakValue</c>: a foil is worth four normal copies.</summary>
    public static int BreakValue(TcgRarity rarity, bool foil)
    {
        var normal = rarity switch
        {
            TcgRarity.Common   => 5,
            TcgRarity.Uncommon => 20,
            TcgRarity.Rare     => 100,
            _                  => 400
        };

        return foil ? normal * 4 : normal;
    }

    /// <summary>Only copies beyond the 3 a deck can use break, counting normal and foil together.</summary>
    public bool CanBreak(string key, bool foil) => (Count(key, foil) > 0) && (Copies(key) > MAX_COPIES);

    public bool CanCraft(string key) => Get(key) is { } card && (Copies(key) < MAX_COPIES) && (Dust >= CraftCost(card.Rarity));

    public bool CanPullOut(string key, bool foil) => Count(key, foil) > 0;

    public static string CardName(TcgCatalogCardView card, bool foil) => foil ? $"{card.Name} (Foil)" : card.Name;

    public void Clear()
    {
        ByKey.Clear();
        Counts.Clear();
        Sorted = [];
        Decks = [];
        Dust = 0;
        Message = string.Empty;
        HasSnapshot = false;
        OwnedOnly = false;
        Page = 0;
        SelectedKey = null;
        Tab = TcgBinderTab.All;
    }

    public void ClearMessage() => Message = string.Empty;

    public static TcgCollectionActionArgs CloseAction() => new() { Type = TcgCollectionActionType.Close };

    public static int Compare(TcgCatalogCardView left, TcgCatalogCardView right)
    {
        var byElement = ElementRank(left.Element).CompareTo(ElementRank(right.Element));

        if (byElement != 0)
            return byElement;

        var byCost = left.TotalCost.CompareTo(right.TotalCost);

        return byCost != 0 ? byCost : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
    }

    public int Copies(string key) => Counts.TryGetValue(key, out var counts) ? counts.Normal + counts.Foil : 0;

    public int Count(string key, bool foil) => Counts.TryGetValue(key, out var counts) ? foil ? counts.Foil : counts.Normal : 0;

    public static TcgCollectionActionArgs CraftAction(string key)
        => new()
        {
            Type = TcgCollectionActionType.Craft,
            CardKey = key
        };

    /// <summary>Mirrors the server's <c>TcgCollectionRules.CraftCost</c>.</summary>
    public static int CraftCost(TcgRarity rarity)
        => rarity switch
        {
            TcgRarity.Common   => 40,
            TcgRarity.Uncommon => 100,
            TcgRarity.Rare     => 400,
            _                  => 1600
        };

    public string CraftQuestion(TcgCatalogCardView card) => $"Craft {card.Name} for {CraftCost(card.Rarity)} dust?";

    public static TcgCollectionActionArgs DeleteAction(string name)
        => new()
        {
            Type = TcgCollectionActionType.DeleteDeck,
            DeckName = name
        };

    public static TcgElement? ElementOf(TcgBinderTab tab)
        => tab switch
        {
            TcgBinderTab.Fire    => TcgElement.Fire,
            TcgBinderTab.Water   => TcgElement.Water,
            TcgBinderTab.Wind    => TcgElement.Wind,
            TcgBinderTab.Earth   => TcgElement.Earth,
            TcgBinderTab.Neutral => TcgElement.Neutral,
            _                    => null
        };

    public static int ElementRank(TcgElement element)
        => element switch
        {
            TcgElement.Fire  => 0,
            TcgElement.Water => 1,
            TcgElement.Wind  => 2,
            TcgElement.Earth => 3,
            _                => 4
        };

    /// <summary>The cards a tab shows, sorted. The Decks tab shows no cards.</summary>
    public IReadOnlyList<TcgCatalogCardView> Filter(TcgBinderTab tab, bool ownedOnly)
    {
        if (tab == TcgBinderTab.Decks)
            return [];

        var element = ElementOf(tab);

        return Sorted.Where(card => ((element is null) || (card.Element == element)) && (!ownedOnly || (Copies(card.Key) > 0)))
                     .ToList();
    }

    public TcgCatalogCardView? Get(string? key) => (key is not null) && ByKey.TryGetValue(key, out var card) ? card : null;

    public void NextPage() => Page = Math.Min(Page + 1, PageCount - 1);

    public static TcgCollectionActionArgs OpenAction() => new() { Type = TcgCollectionActionType.Open };

    public static int PageCountOf(int count) => Math.Max(1, (count + PAGE_SIZE - 1) / PAGE_SIZE);

    public static IReadOnlyList<T> PageOf<T>(IReadOnlyList<T> items, int page)
        => items.Skip(page * PAGE_SIZE)
                .Take(PAGE_SIZE)
                .ToList();

    public string PageTextOf(IReadOnlyList<TcgCatalogCardView> cards, int page)
        => $"Page {page + 1} of {PageCountOf(cards.Count)} - Owned {cards.Count(card => Copies(card.Key) > 0)} of {cards.Count}";

    public void PreviousPage() => Page = Math.Max(Page - 1, 0);

    public static TcgCollectionActionArgs PullOutAction(string key, bool foil)
        => new()
        {
            Type = TcgCollectionActionType.PullOut,
            CardKey = key,
            Foil = foil
        };

    /// <summary>The warning to show before pulling out one copy, naming each saved deck that would then be a copy short; null when none would.</summary>
    public string? PullOutWarning(string key)
    {
        var left = Copies(key) - 1;

        var shortDecks = Decks.Where(deck => deck.Cards.Count(card => card.Equals(key, StringComparison.OrdinalIgnoreCase)) > left)
                              .Select(deck => deck.Name)
                              .ToList();

        return shortDecks.Count switch
        {
            0 => null,
            1 => $"{shortDecks[0]} will be missing a card.",
            _ => $"{string.Join(", ", shortDecks.Take(shortDecks.Count - 1))} and {shortDecks[^1]} will be missing a card."
        };
    }

    public static TcgCollectionActionArgs RenameAction(string name, string newName)
        => new()
        {
            Type = TcgCollectionActionType.RenameDeck,
            DeckName = name,
            NewName = newName
        };

    public void Select(string? key) => SelectedKey = Get(key)?.Key;

    public void SetOwnedOnly(bool ownedOnly)
    {
        OwnedOnly = ownedOnly;
        Page = 0;
    }

    public void SetTab(TcgBinderTab tab)
    {
        Tab = tab;
        Page = 0;
    }

    public static string StatusText(TcgDeckView deck)
        => deck.Status switch
        {
            TcgDeckStatus.Ready   => "Ready",
            TcgDeckStatus.Missing => deck.StatusCount == 1 ? "Missing 1 card" : $"Missing {deck.StatusCount} cards",
            _                     => $"{deck.StatusCount}/{TcgDeckDraft.DECK_SIZE}"
        };
}
