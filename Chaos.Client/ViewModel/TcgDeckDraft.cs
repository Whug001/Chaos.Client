#region
using System.Globalization;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>One row of the deck list: a card and how many copies of it the deck holds.</summary>
public sealed record TcgDeckRow(string Key, string Name, TcgElement Element, int Cost, int Count);

/// <summary>
///     The deck being edited in the deck builder. Copies are added only when owned, at most 3 of a card (normal and foil
///     together) and 40 in all; the deck can be saved at any size. The server checks everything again on save.
/// </summary>
public sealed class TcgDeckDraft
{
    public const int CURVE_BUCKETS = 6;
    public const string DEFAULT_NAME = "Untitled deck";
    public const int DECK_SIZE = 40;
    public const int MAX_COPIES = 3;
    public const int MAX_NAME_LENGTH = 24;

    //sorts cards no longer in the catalog after every real cost
    private const int UNKNOWN_COST = 99;

    private readonly List<string> Cards;
    private readonly List<string> InitialCards;
    private readonly string InitialName;

    private TcgDeckDraft(string originalName, string name, IEnumerable<string> cards)
    {
        OriginalName = originalName;
        Name = name;
        InitialName = name;
        Cards = cards.ToList();
        InitialCards = Cards.ToList();
    }

    public bool CanSave => NameProblem is null;
    public int Count => Cards.Count;
    public string CountText => $"{Count} / {DECK_SIZE}";

    /// <summary>The name or the cards changed since the draft was opened (the order of the cards does not count).</summary>
    public bool IsDirty => !Name.Trim().Equals(InitialName.Trim(), StringComparison.Ordinal) || !SameCards(Cards, InitialCards);

    public bool IsNew => OriginalName.Length == 0;
    public IReadOnlyList<string> Keys => Cards;
    public string Name { get; set; }

    public string? NameProblem
    {
        get
        {
            var name = Name.Trim();

            if (name.Length == 0)
                return "The deck needs a name";

            return name.Length > MAX_NAME_LENGTH ? $"Names can be {MAX_NAME_LENGTH} characters" : null;
        }
    }

    /// <summary>The deck's saved name, or empty for a new deck.</summary>
    public string OriginalName { get; }

    /// <summary>Null when one more copy can be added, otherwise why not.</summary>
    /// <param name="owned">The player's normal plus foil copies of the card.</param>
    public string? AddProblem(string key, int owned)
    {
        if (Count >= DECK_SIZE)
            return $"A deck holds {DECK_SIZE} cards.";

        var inDeck = InDeck(key);

        if (inDeck >= MAX_COPIES)
            return $"A deck can hold {MAX_COPIES} copies of a card.";

        if (inDeck < owned)
            return null;

        return owned == 0 ? "You don't own this card." : $"You own only {owned}.";
    }

    /// <summary>The cost-curve bar a cost falls in: 1, 2, 3, 4, 5, then 6 or more. Free cards count as 1.</summary>
    public static int Bucket(int cost) => Math.Clamp(cost, 1, CURVE_BUCKETS) - 1;

    public static string BucketLabel(int bucket)
        => bucket >= (CURVE_BUCKETS - 1) ? $"{CURVE_BUCKETS}+" : (bucket + 1).ToString(CultureInfo.InvariantCulture);

    public int[] CostCurve(Func<string, TcgCatalogCardView?> lookup)
    {
        var buckets = new int[CURVE_BUCKETS];

        foreach (var key in Cards)
            if (lookup(key) is { } card)
                buckets[Bucket(card.TotalCost)]++;

        return buckets;
    }

    /// <summary>"Untitled deck", or "Untitled deck 2", "Untitled deck 3" and so on when the name is taken.</summary>
    public static string DefaultName(IEnumerable<string> existingNames)
    {
        var taken = new HashSet<string>(existingNames.Select(name => name.Trim()), StringComparer.OrdinalIgnoreCase);

        if (!taken.Contains(DEFAULT_NAME))
            return DEFAULT_NAME;

        for (var number = 2;; number++)
        {
            var candidate = $"{DEFAULT_NAME} {number}";

            if (!taken.Contains(candidate))
                return candidate;
        }
    }

    public static TcgDeckDraft Edit(TcgDeckView deck) => new(deck.Name, deck.Name, deck.Cards);

    /// <summary>The first thing keeping the deck from play, or "Ready to play".</summary>
    /// <param name="copiesOwned">The player's normal plus foil copies of a card.</param>
    public string FirstProblem(Func<string, int> copiesOwned, Func<string, TcgCatalogCardView?> lookup)
    {
        if (NameProblem is { } nameProblem)
            return nameProblem;

        if (Count < DECK_SIZE)
        {
            var needed = DECK_SIZE - Count;

            return needed == 1 ? "Needs 1 more card" : $"Needs {needed} more cards";
        }

        foreach (var row in Rows(lookup))
        {
            var owned = copiesOwned(row.Key);

            if (row.Count > owned)
                return $"You own only {owned} {row.Name}";
        }

        return "Ready to play";
    }

    public int InDeck(string key) => Cards.Count(card => card.Equals(key, StringComparison.OrdinalIgnoreCase));

    /// <summary>A snapshot holds this draft's name and cards, so a save has landed (another snapshot can arrive first).</summary>
    public bool IsSavedIn(IReadOnlyList<TcgDeckView> decks)
        => decks.Any(deck => deck.Name.Equals(Name.Trim(), StringComparison.OrdinalIgnoreCase) && SameCards(deck.Cards, Cards));

    public static TcgDeckDraft New(IEnumerable<string> existingNames) => new(string.Empty, DefaultName(existingNames), []);

    public bool Remove(string key)
    {
        var index = Cards.FindLastIndex(card => card.Equals(key, StringComparison.OrdinalIgnoreCase));

        if (index < 0)
            return false;

        Cards.RemoveAt(index);

        return true;
    }

    /// <summary>One row per card, sorted by cost then name.</summary>
    public IReadOnlyList<TcgDeckRow> Rows(Func<string, TcgCatalogCardView?> lookup)
        => Cards.GroupBy(card => card, StringComparer.OrdinalIgnoreCase)
                .Select(
                    group => lookup(group.Key) is { } card
                        ? new TcgDeckRow(card.Key, card.Name, card.Element, card.TotalCost, group.Count())
                        : new TcgDeckRow(group.Key, group.Key, TcgElement.Neutral, UNKNOWN_COST, group.Count()))
                .OrderBy(row => row.Cost)
                .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

    public TcgCollectionActionArgs SaveAction()
        => new()
        {
            Type = TcgCollectionActionType.SaveDeck,
            DeckName = OriginalName,
            NewName = Name.Trim(),
            Keys = Cards.ToList()
        };

    public bool TryAdd(string key, int owned)
    {
        if (AddProblem(key, owned) is not null)
            return false;

        Cards.Add(key);

        return true;
    }

    private static bool SameCards(List<string> left, List<string> right)
        => (left.Count == right.Count)
           && left.Order(StringComparer.OrdinalIgnoreCase)
                  .SequenceEqual(right.Order(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
}
