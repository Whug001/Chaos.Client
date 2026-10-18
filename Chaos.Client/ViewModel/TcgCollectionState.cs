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
    Decks,
    Ranked
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
    private readonly Dictionary<string, TcgOwnedView> Counts = new(StringComparer.OrdinalIgnoreCase);
    private List<TcgCatalogCardView> Sorted = [];

    /// <summary>Every card in the catalog, sorted by element (fire, water, wind, earth, neutral), then cost, then name.</summary>
    public IReadOnlyList<TcgCatalogCardView> Cards => Sorted;

    public IReadOnlyList<TcgDeckView> Decks { get; private set; } = [];
    public TcgCardStyle DetailStyle { get; private set; }
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
            Counts[owned.Key] = owned;

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

    /// <summary>The printings a deck's copies get at match start; the server runs the same <see cref="TcgPrinting.AssignBest" />.</summary>
    public IReadOnlyList<TcgPrinting> AssignPrintings(IReadOnlyList<string> keys) => TcgPrinting.AssignBest(keys, Count);

    /// <summary>The best printing owned, or null when none is.</summary>
    public TcgPrinting? BestPrinting(string key)
    {
        foreach (var printing in TcgPrinting.BestFirst)
            if (Count(key, printing) > 0)
                return printing;

        return null;
    }

    public static TcgCollectionActionArgs BreakAction(string key, TcgPrinting printing)
        => new()
        {
            Type = TcgCollectionActionType.Break,
            CardKey = key,
            Foil = printing.Foil,
            Style = printing.Style
        };

    public string BreakQuestion(TcgCatalogCardView card, TcgPrinting printing)
        => $"Break {CardName(card, printing)} for {BreakValue(card.Rarity, printing)} dust?";

    /// <summary>Mirrors the server's <c>TcgCollectionRules.BreakValue</c>.</summary>
    public static int BreakValue(TcgRarity rarity, TcgPrinting printing)
    {
        if (rarity == TcgRarity.Mythic)
            return printing.Foil ? 6400 : 1600;

        var normal = rarity switch
        {
            TcgRarity.Common   => 5,
            TcgRarity.Uncommon => 20,
            TcgRarity.Rare     => 100,
            _                  => 400
        };

        var styled = printing.Style switch
        {
            TcgCardStyle.Extended => normal * 2,
            TcgCardStyle.Full     => normal * 4,
            _                     => normal
        };

        return printing.Foil ? styled * 4 : styled;
    }

    /// <summary>Only copies beyond the 3 a deck can use break, counting every printing together.</summary>
    public bool CanBreak(string key, TcgPrinting printing) => (Count(key, printing) > 0) && (Copies(key) > MAX_COPIES);

    public bool CanCraft(string key)
        => Get(key) is { } card && Craftable(card.Rarity) && (Copies(key) < MAX_COPIES) && (Dust >= CraftCost(card.Rarity));

    public bool CanPullOut(string key, TcgPrinting printing) => Count(key, printing) > 0;

    public static string CardName(TcgCatalogCardView card, TcgPrinting printing) => printing.Name(card.Name);

    public void Clear()
    {
        ByKey.Clear();
        Counts.Clear();
        Sorted = [];
        Decks = [];
        Dust = 0;
        Message = string.Empty;
        HasSnapshot = false;
        DetailStyle = TcgCardStyle.Normal;
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

    public int Copies(string key) => Counts.TryGetValue(key, out var owned) ? owned.Copies : 0;

    public int Count(string key, TcgPrinting printing) => Counts.TryGetValue(key, out var owned) ? owned.Count(printing) : 0;

    /// <summary>Mirrors the server's <c>TcgCollectionRules.Craftable</c>.</summary>
    public static bool Craftable(TcgRarity rarity) => rarity != TcgRarity.Mythic;

    /// <summary>The printing a card shows in the deck builder grid: what its first deck copy gets, or the best owned when it is not in the deck.</summary>
    public TcgPrinting DeckFacePrinting(IReadOnlyList<string> deckKeys, string key)
    {
        for (var i = 0; i < deckKeys.Count; i++)
            if (deckKeys[i].Equals(key, StringComparison.OrdinalIgnoreCase))
                return AssignPrintings(deckKeys)[i];

        return BestPrinting(key) ?? TcgPrinting.Normal;
    }

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
        if (tab is TcgBinderTab.Decks or TcgBinderTab.Ranked)
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

    public static TcgCollectionActionArgs PullOutAction(string key, TcgPrinting printing)
        => new()
        {
            Type = TcgCollectionActionType.PullOut,
            CardKey = key,
            Foil = printing.Foil,
            Style = printing.Style
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

    /// <summary>Selects a card and shows the style of its best owned printing.</summary>
    public void Select(string? key)
    {
        SelectedKey = Get(key)?.Key;
        DetailStyle = SelectedKey is not null && BestPrinting(SelectedKey) is { } best ? best.Style : TcgCardStyle.Normal;
    }

    public void SetDetailStyle(TcgCardStyle style) => DetailStyle = style;

    public static string StyleName(TcgCardStyle style)
        => style switch
        {
            TcgCardStyle.Extended => "Extended",
            TcgCardStyle.Full     => "Full art",
            _                     => "Normal"
        };

    /// <summary>The styles the binder's switch offers: full art only for rare or better.</summary>
    public static IReadOnlyList<TcgCardStyle> Styles(TcgCatalogCardView card)
        => card.Rarity >= TcgRarity.Rare
            ? [TcgCardStyle.Normal, TcgCardStyle.Extended, TcgCardStyle.Full]
            : [TcgCardStyle.Normal, TcgCardStyle.Extended];

    /// <summary>One style button's caption: the owned count, then the foil count.</summary>
    public string StyleText(string key, TcgCardStyle style)
        => $"{StyleName(style)}: {Count(key, new TcgPrinting(style, false))}, foil {Count(key, new TcgPrinting(style, true))}";

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
