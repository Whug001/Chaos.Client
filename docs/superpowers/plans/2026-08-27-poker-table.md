# Rucesion Poker Table Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a six-seat fixed-limit Texas Hold'em cash table to the Rucesion casino, played directly out of gold, with a new client popup and no dialogs.

**Architecture:** A dependency-free game engine (`Chaos/Services/Poker/`) sits under a merchant script that drives it from `Update` and broadcasts per-recipient table snapshots over a new packet pair. The client mirrors the slot-machine rail exactly: `ConnectionManager` event → `WorldState` view model → popup control. Fixed-limit betting bounds a hand at 24 small bets, and requiring that much to be dealt in makes all-ins and side pots structurally impossible.

**Tech Stack:** .NET 10, TUnit 1.1.10 + FluentAssertions 8.8.0, MonoGame client, JSON content templates.

**User decisions (already made):**
- "we should just make poker and let players take money from eachother, house takes a small cut for hosting"
- Variant: fixed-limit Texas Hold'em, 6-max cash game.
- Rake: 5% capped at 3 big bets, no flop no drop, **destroyed** (not redistributed).
- "leave old 21 code for now" — twenty-one is untouched, this is purely additive.
- "We should make a new client UI and not really touch dialogs."
- One table, at a new floor spot; both twenty-one spawns stay in place.
- No bad-beat jackpot in v1.

**Spec:** `docs/superpowers/specs/2026-08-27-poker-table-design.md`

---

## Repository map

This feature spans **three repositories**. Know which one you are in before every commit.

| Repo | Role | Branch |
|---|---|---|
| `Chaos.Client` (outer) | Client UI, client networking, this plan | `feature/poker-table` |
| `Chaos.Client/Chaos-Server` (submodule) | Server engine, scripts, shared packet types | `feature/poker-table` off `master` |
| `Unora` | Content JSON: catalog, merchant, map spawns, reactors | `feature/poker-table` |

The submodule is shared code: `Chaos.Networking` args and converters are compiled into **both** the server and the client, so a packet type is written once in `Chaos-Server` and consumed by both sides.

Unless stated otherwise, shell commands in this plan run from `Chaos.Client/Chaos-Server/`.

## Established conventions (do not deviate)

- **Tests are TUnit executables.** Use `dotnet run`, never `dotnet test`. Filter with `--treenode-filter`, never `--filter`.
- Test attribute is `[Test]` on `public async Task` methods; end pure-sync tests with `await Task.CompletedTask;`. Assertions use FluentAssertions (`.Should().Be(...)`).
- `scriptKeys` in content JSON are class names **without** the `Script` suffix: `PokerTableScript` → `"PokerTable"`, `PokerSeatScript` → `"PokerSeat"`.
- Server sends to one player via `aisling.Client.SendXxx(...)`.
- `Chaos-Server` is a fork base — prefer **additive** changes to public types.

## File structure

**Engine — `Chaos-Server/Chaos/Services/Poker/`** (no dependency on `Chaos.Models.World`, so it is unit-testable without a live server):

| File | Responsibility |
|---|---|
| `Card.cs` | `Suit`, `Rank`, `Card` value type, index conversion |
| `Deck.cs` | Shuffle and deal from a server-side RNG |
| `HandCategory.cs` | Ranked hand categories |
| `HandRank.cs` | Comparable (category, tiebreak) pair |
| `HandEvaluator.cs` | 5–7 cards in, `HandRank` out. Pure. |
| `RakeCalculator.cs` | Percentage, cap, no-flop-no-drop |
| `PokerAction.cs` | Fold / Check / Call / Bet / Raise |
| `BettingRound.cs` | One street: turn order, legal actions, four-bet cap |
| `IPokerSeatOccupant.cs` | Gold seam so `PokerHand` is testable without an `Aisling` |
| `PokerHand.cs` | One hand: blinds → streets → showdown → rake → award |
| `PokerTable.cs` | Seats, button, eligibility, sit-out, hand scheduling |
| `PokerTableConfig.cs` | Stakes, seats, rake, timers |

**Shared packets — `Chaos-Server/Chaos.Networking/`** and enum homes.
**Server glue — `Chaos-Server/Chaos/Networking/`, `Chaos/Services/Servers/`, `Chaos/Scripting/`.**
**Client — `Chaos.Client/` and `Chaos.Client.Networking/`.**
**Content — `Unora/Data/`.**

---

### Task 0: Feature branches

**Goal:** Three feature branches exist so the work is isolated and the submodule pointer can move without touching `main`.

**Files:**
- No file changes.

**Acceptance Criteria:**
- [ ] `Chaos.Client` is on `feature/poker-table`
- [ ] `Chaos.Client/Chaos-Server` is on `feature/poker-table` branched from `master`
- [ ] `Unora` is on `feature/poker-table`
- [ ] Pre-existing uncommitted work (`Chaos.Client/GlobalSettings.cs`) is left alone, not committed

**Verify:** `git -C . branch --show-current && git -C Chaos-Server branch --show-current` → both print `feature/poker-table`

**Steps:**

- [ ] **Step 1: Branch the submodule first**

The submodule sits on `master` detached-ish; branch it before the outer repo so the outer repo's pointer commit lands on the new branch.

```bash
cd Chaos-Server
git checkout -b feature/poker-table
cd ..
```

- [ ] **Step 2: Branch the outer repo and Unora**

```bash
git checkout -b feature/poker-table
git -C ../Unora checkout -b feature/poker-table
```

- [ ] **Step 3: Confirm and stop**

```bash
git branch --show-current
git -C Chaos-Server branch --show-current
git -C ../Unora branch --show-current
git status --short
```

Expected: three lines reading `feature/poker-table`, and `git status --short` still showing only the pre-existing ` M Chaos.Client/GlobalSettings.cs` and ` m Chaos-Server`. Do not commit those.

---

### Task 1: Card primitives and deck

**Goal:** A `Card` value type with round-tripping index conversion, and a `Deck` that shuffles from a server-side RNG.

**Files:**
- Create: `Chaos-Server/Chaos/Services/Poker/Card.cs`
- Create: `Chaos-Server/Chaos/Services/Poker/Deck.cs`
- Create: `Chaos-Server/Tests/Chaos.Tests/Poker/CardTests.cs`
- Create: `Chaos-Server/Tests/Chaos.Tests/Poker/DeckTests.cs`

**Acceptance Criteria:**
- [ ] `Card.FromIndex(i).ToIndex() == i` for every `i` in `0..51`
- [ ] The 52 indices produce 52 distinct cards
- [ ] `Deck.Shuffle()` produces a permutation of all 52 cards with no duplicates
- [ ] `Deck` draws sequentially and reports remaining count
- [ ] Shuffling uses `System.Security.Cryptography.RandomNumberGenerator`, not `Random`

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/CardTests/*" --no-ansi` → all pass; same for `DeckTests`

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `Chaos-Server/Tests/Chaos.Tests/Poker/CardTests.cs`:

```csharp
using Chaos.Services.Poker;
using FluentAssertions;

namespace Chaos.Tests.Poker;

public class CardTests
{
    [Test]
    public async Task Index_round_trips_for_all_52_cards()
    {
        for (var i = 0; i < 52; i++)
            Card.FromIndex(i)
                .ToIndex()
                .Should()
                .Be(i);

        await Task.CompletedTask;
    }

    [Test]
    public async Task All_52_indices_produce_distinct_cards()
    {
        var cards = Enumerable.Range(0, 52)
                              .Select(Card.FromIndex)
                              .ToHashSet();

        cards.Count
             .Should()
             .Be(52);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Ranks_span_two_through_ace()
    {
        Card.FromIndex(0)
            .Rank
            .Should()
            .Be(Rank.Two);

        Card.FromIndex(51)
            .Rank
            .Should()
            .Be(Rank.Ace);

        await Task.CompletedTask;
    }
}
```

Create `Chaos-Server/Tests/Chaos.Tests/Poker/DeckTests.cs`:

```csharp
using Chaos.Services.Poker;
using FluentAssertions;

namespace Chaos.Tests.Poker;

public class DeckTests
{
    [Test]
    public async Task Shuffled_deck_contains_every_card_exactly_once()
    {
        var deck = new Deck();
        deck.Shuffle();

        var drawn = new List<Card>();

        while (deck.Remaining > 0)
            drawn.Add(deck.Draw());

        drawn.Count
             .Should()
             .Be(52);

        drawn.Distinct()
             .Count()
             .Should()
             .Be(52);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Remaining_decreases_as_cards_are_drawn()
    {
        var deck = new Deck();
        deck.Shuffle();

        deck.Remaining
            .Should()
            .Be(52);

        deck.Draw();

        deck.Remaining
            .Should()
            .Be(51);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Two_shuffles_of_the_same_deck_differ()
    {
        //a 1-in-52! collision is not a flake worth guarding against
        var deck = new Deck();
        deck.Shuffle();
        var first = Enumerable.Range(0, 52).Select(_ => deck.Draw()).ToArray();

        deck.Shuffle();
        var second = Enumerable.Range(0, 52).Select(_ => deck.Draw()).ToArray();

        first.Should()
             .NotEqual(second);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Drawing_past_the_end_throws()
    {
        var deck = new Deck();
        deck.Shuffle();

        for (var i = 0; i < 52; i++)
            deck.Draw();

        var draw = () => deck.Draw();

        draw.Should()
            .Throw<InvalidOperationException>();

        await Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/CardTests/*" --no-ansi
```

Expected: build failure — `Card` and `Deck` do not exist.

- [ ] **Step 3: Write the implementation**

Create `Chaos-Server/Chaos/Services/Poker/Card.cs`:

```csharp
namespace Chaos.Services.Poker;

public enum Suit : byte
{
    Clubs = 0,
    Diamonds = 1,
    Hearts = 2,
    Spades = 3
}

public enum Rank : byte
{
    Two = 2,
    Three = 3,
    Four = 4,
    Five = 5,
    Six = 6,
    Seven = 7,
    Eight = 8,
    Nine = 9,
    Ten = 10,
    Jack = 11,
    Queen = 12,
    King = 13,
    Ace = 14
}

/// <summary>
///     A single playing card. The index form (0-51) is the wire representation and the enumeration order
///     used by the exhaustive evaluator tests, so <see cref="FromIndex" /> and <see cref="ToIndex" /> must
///     stay exact inverses.
/// </summary>
public readonly record struct Card(Rank Rank, Suit Suit)
{
    public static Card FromIndex(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, 51);

        return new Card((Rank)((index / 4) + 2), (Suit)(index % 4));
    }

    public int ToIndex() => (((int)Rank - 2) * 4) + (int)Suit;

    public override string ToString()
    {
        var rank = Rank switch
        {
            Rank.Ten   => "T",
            Rank.Jack  => "J",
            Rank.Queen => "Q",
            Rank.King  => "K",
            Rank.Ace   => "A",
            _          => ((int)Rank).ToString()
        };

        var suit = Suit switch
        {
            Suit.Clubs    => "c",
            Suit.Diamonds => "d",
            Suit.Hearts   => "h",
            _             => "s"
        };

        return rank + suit;
    }
}
```

Create `Chaos-Server/Chaos/Services/Poker/Deck.cs`:

```csharp
using System.Security.Cryptography;

namespace Chaos.Services.Poker;

/// <summary>
///     A 52-card deck shuffled from a cryptographic RNG.
/// </summary>
/// <remarks>
///     The RNG is deliberately <see cref="RandomNumberGenerator" /> rather than <see cref="Random" />: card
///     order must not be predictable from anything a player can observe, influence, or time. Nothing about
///     the shuffle is seeded from game state, wall-clock, or player input.
/// </remarks>
public sealed class Deck
{
    private readonly Card[] Cards = new Card[52];
    private int NextIndex;

    public int Remaining => Cards.Length - NextIndex;

    public void Shuffle()
    {
        for (var i = 0; i < Cards.Length; i++)
            Cards[i] = Card.FromIndex(i);

        //Fisher-Yates, drawing each swap index from the crypto RNG
        for (var i = Cards.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (Cards[i], Cards[j]) = (Cards[j], Cards[i]);
        }

        NextIndex = 0;
    }

    public Card Draw()
    {
        if (Remaining <= 0)
            throw new InvalidOperationException("Cannot draw from an exhausted deck");

        return Cards[NextIndex++];
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/CardTests/*" --no-ansi
dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/DeckTests/*" --no-ansi
```

Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add Chaos/Services/Poker/Card.cs Chaos/Services/Poker/Deck.cs Tests/Chaos.Tests/Poker/
git commit -m "Add poker card primitives and crypto-shuffled deck"
```

---

### Task 2: Hand evaluator

**Goal:** `HandEvaluator.Evaluate` takes 5–7 cards and returns a `HandRank` that orders correctly against any other `HandRank`.

**Files:**
- Create: `Chaos-Server/Chaos/Services/Poker/HandCategory.cs`
- Create: `Chaos-Server/Chaos/Services/Poker/HandRank.cs`
- Create: `Chaos-Server/Chaos/Services/Poker/HandEvaluator.cs`
- Create: `Chaos-Server/Tests/Chaos.Tests/Poker/HandEvaluatorTests.cs`

**Acceptance Criteria:**
- [ ] Every category is detected from a 7-card input
- [ ] The wheel (A-2-3-4-5) ranks as a five-high straight, below 6-high
- [ ] A hand containing both a flush and a (non-matching) straight ranks as the flush
- [ ] A straight flush beats four of a kind
- [ ] Kickers break ties within a category
- [ ] Identical hands compare equal, so split pots are detectable
- [ ] Playing the board (both players' best five cards are the community cards) compares equal

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/HandEvaluatorTests/*" --no-ansi` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `Chaos-Server/Tests/Chaos.Tests/Poker/HandEvaluatorTests.cs`:

```csharp
using Chaos.Services.Poker;
using FluentAssertions;

namespace Chaos.Tests.Poker;

public class HandEvaluatorTests
{
    /// <summary>Parses "AsKdTc9h8s" style notation into cards, so test intent is readable at a glance.</summary>
    private static Card[] Hand(string notation)
    {
        var cards = new List<Card>();

        for (var i = 0; i < notation.Length; i += 2)
        {
            var rank = notation[i] switch
            {
                'A' => Rank.Ace,
                'K' => Rank.King,
                'Q' => Rank.Queen,
                'J' => Rank.Jack,
                'T' => Rank.Ten,
                _   => (Rank)(notation[i] - '0')
            };

            var suit = notation[i + 1] switch
            {
                'c' => Suit.Clubs,
                'd' => Suit.Diamonds,
                'h' => Suit.Hearts,
                _   => Suit.Spades
            };

            cards.Add(new Card(rank, suit));
        }

        return cards.ToArray();
    }

    [Test]
    public async Task Detects_each_category()
    {
        HandEvaluator.Evaluate(Hand("AsKsQsJsTs2c3d")).Category.Should().Be(HandCategory.StraightFlush);
        HandEvaluator.Evaluate(Hand("7s7d7h7c2s3d4h")).Category.Should().Be(HandCategory.FourOfAKind);
        HandEvaluator.Evaluate(Hand("7s7d7hKcKs2d3h")).Category.Should().Be(HandCategory.FullHouse);
        HandEvaluator.Evaluate(Hand("As9s7s5s3s2d4h")).Category.Should().Be(HandCategory.Flush);
        HandEvaluator.Evaluate(Hand("9s8d7h6c5s2d3h")).Category.Should().Be(HandCategory.Straight);
        HandEvaluator.Evaluate(Hand("7s7d7hKc9s2d3h")).Category.Should().Be(HandCategory.ThreeOfAKind);
        HandEvaluator.Evaluate(Hand("7s7dKhKc9s2d3h")).Category.Should().Be(HandCategory.TwoPair);
        HandEvaluator.Evaluate(Hand("7s7dKh9c5s2d3h")).Category.Should().Be(HandCategory.OnePair);
        HandEvaluator.Evaluate(Hand("As9d7h5c3s2d4h")).Category.Should().NotBe(HandCategory.HighCard);
        HandEvaluator.Evaluate(Hand("AsQd9h7c5s3d2h")).Category.Should().Be(HandCategory.HighCard);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Wheel_is_a_five_high_straight_and_loses_to_six_high()
    {
        var wheel = HandEvaluator.Evaluate(Hand("As2d3h4c5s KdQh".Replace(" ", "")));
        var sixHigh = HandEvaluator.Evaluate(Hand("2d3h4c5s6d KdQh".Replace(" ", "")));

        wheel.Category.Should().Be(HandCategory.Straight);
        sixHigh.Category.Should().Be(HandCategory.Straight);

        wheel.Should()
             .BeLessThan(sixHigh);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Flush_beats_a_non_matching_straight_in_the_same_seven_cards()
    {
        //9s8s7s5s2s with 6d: a straight (5-6-7-8-9) exists but not in one suit, and the flush is higher
        var rank = HandEvaluator.Evaluate(Hand("9s8s7s5s2s6dKh"));

        rank.Category
            .Should()
            .Be(HandCategory.Flush);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Straight_flush_beats_four_of_a_kind()
    {
        var straightFlush = HandEvaluator.Evaluate(Hand("9s8s7s6s5s2d3h"));
        var quads = HandEvaluator.Evaluate(Hand("9s9d9h9cKs2d3h"));

        straightFlush.Should()
                     .BeGreaterThan(quads);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Kickers_break_ties_within_a_category()
    {
        var aceKicker = HandEvaluator.Evaluate(Hand("7s7dAh9c5s2d3h"));
        var kingKicker = HandEvaluator.Evaluate(Hand("7s7dKh9c5s2d3h"));

        aceKicker.Should()
                 .BeGreaterThan(kingKicker);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Identical_best_fives_compare_equal()
    {
        //both players play the board: the community five is the best hand for each
        var boardPlusJunk = HandEvaluator.Evaluate(Hand("AsKsQsJsTs2d3h"));
        var boardPlusOtherJunk = HandEvaluator.Evaluate(Hand("AsKsQsJsTs4d5h"));

        boardPlusJunk.Should()
                     .Be(boardPlusOtherJunk);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Accepts_five_six_and_seven_card_inputs()
    {
        HandEvaluator.Evaluate(Hand("AsKsQsJsTs")).Category.Should().Be(HandCategory.StraightFlush);
        HandEvaluator.Evaluate(Hand("AsKsQsJsTs2d")).Category.Should().Be(HandCategory.StraightFlush);
        HandEvaluator.Evaluate(Hand("AsKsQsJsTs2d3h")).Category.Should().Be(HandCategory.StraightFlush);

        var tooFew = () => HandEvaluator.Evaluate(Hand("AsKsQsJs"));

        tooFew.Should()
              .Throw<ArgumentException>();

        await Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/HandEvaluatorTests/*" --no-ansi
```

Expected: build failure — `HandEvaluator`, `HandCategory`, `HandRank` do not exist.

- [ ] **Step 3: Write the implementation**

Create `Chaos-Server/Chaos/Services/Poker/HandCategory.cs`:

```csharp
namespace Chaos.Services.Poker;

/// <summary>Poker hand categories, ordered so that a larger value always beats a smaller one.</summary>
public enum HandCategory
{
    HighCard = 1,
    OnePair = 2,
    TwoPair = 3,
    ThreeOfAKind = 4,
    Straight = 5,
    Flush = 6,
    FullHouse = 7,
    FourOfAKind = 8,
    StraightFlush = 9
}
```

Create `Chaos-Server/Chaos/Services/Poker/HandRank.cs`:

```csharp
namespace Chaos.Services.Poker;

/// <summary>
///     A fully-ordered hand strength: the category, plus a packed tiebreak of the five significant ranks.
/// </summary>
/// <remarks>
///     <see cref="Tiebreak" /> packs up to five ranks into nibbles, most significant first, so two hands in
///     the same category compare with a single integer comparison. Equality is meaningful and is what
///     detects a split pot -- do not add identity or suit information to this type.
/// </remarks>
public readonly record struct HandRank(HandCategory Category, int Tiebreak) : IComparable<HandRank>
{
    public int CompareTo(HandRank other)
    {
        var byCategory = Category.CompareTo(other.Category);

        return byCategory != 0 ? byCategory : Tiebreak.CompareTo(other.Tiebreak);
    }

    public static bool operator <(HandRank left, HandRank right) => left.CompareTo(right) < 0;
    public static bool operator >(HandRank left, HandRank right) => left.CompareTo(right) > 0;
    public static bool operator <=(HandRank left, HandRank right) => left.CompareTo(right) <= 0;
    public static bool operator >=(HandRank left, HandRank right) => left.CompareTo(right) >= 0;
}
```

Create `Chaos-Server/Chaos/Services/Poker/HandEvaluator.cs`:

```csharp
namespace Chaos.Services.Poker;

/// <summary>
///     Evaluates the best five-card poker hand out of five, six, or seven cards.
/// </summary>
/// <remarks>
///     Pure and dependency-free by design: this is the one component whose correctness can be proven
///     exhaustively (see <c>HandEvaluatorFrequencyTests</c>), and it stays that way only if it never learns
///     about tables, players, or gold.
/// </remarks>
public static class HandEvaluator
{
    private const int ACE_LOW_STRAIGHT_MASK = 0b1_0000_0000_1111; //A,5,4,3,2

    public static HandRank Evaluate(ReadOnlySpan<Card> cards)
    {
        if (cards.Length is < 5 or > 7)
            throw new ArgumentException($"Expected 5 to 7 cards, got {cards.Length}", nameof(cards));

        Span<int> rankCounts = stackalloc int[15]; //indexed by Rank (2..14)
        Span<int> suitCounts = stackalloc int[4];
        Span<int> suitRankMasks = stackalloc int[4];
        var rankMask = 0;

        foreach (var card in cards)
        {
            var rank = (int)card.Rank;
            var suit = (int)card.Suit;

            rankCounts[rank]++;
            suitCounts[suit]++;
            suitRankMasks[suit] |= 1 << rank;
            rankMask |= 1 << rank;
        }

        //--- straight flush ---
        for (var suit = 0; suit < 4; suit++)
            if (suitCounts[suit] >= 5)
            {
                var high = HighestStraight(suitRankMasks[suit]);

                if (high != 0)
                    return new HandRank(HandCategory.StraightFlush, Pack(high));

                //a flush exists in this suit but contains no straight -- fall through to Flush below
                return new HandRank(HandCategory.Flush, PackTop(suitRankMasks[suit], 5));
            }

        //--- quads, boat, trips, pairs ---
        var quad = HighestWithCount(rankCounts, 4);
        var trips = HighestWithCount(rankCounts, 3);
        var secondTrips = HighestWithCount(rankCounts, 3, trips);
        var pair = HighestWithCount(rankCounts, 2);
        var secondPair = HighestWithCount(rankCounts, 2, pair);

        if (quad != 0)
            return new HandRank(HandCategory.FourOfAKind, Pack(quad, HighestExcluding(rankMask, quad)));

        //a second set of trips plays as the pair half of a full house
        if ((trips != 0) && ((pair != 0) || (secondTrips != 0)))
            return new HandRank(HandCategory.FullHouse, Pack(trips, Math.Max(pair, secondTrips)));

        //--- flush (no straight flush found above) ---
        for (var suit = 0; suit < 4; suit++)
            if (suitCounts[suit] >= 5)
                return new HandRank(HandCategory.Flush, PackTop(suitRankMasks[suit], 5));

        //--- straight ---
        var straightHigh = HighestStraight(rankMask);

        if (straightHigh != 0)
            return new HandRank(HandCategory.Straight, Pack(straightHigh));

        if (trips != 0)
            return new HandRank(HandCategory.ThreeOfAKind, Pack(trips) << 8 | PackTop(ClearRank(rankMask, trips), 2));

        if (secondPair != 0)
        {
            var high = Math.Max(pair, secondPair);
            var low = Math.Min(pair, secondPair);
            var kicker = HighestExcluding(ClearRank(rankMask, high), low);

            return new HandRank(HandCategory.TwoPair, Pack(high, low, kicker));
        }

        if (pair != 0)
            return new HandRank(HandCategory.OnePair, Pack(pair) << 12 | PackTop(ClearRank(rankMask, pair), 3));

        return new HandRank(HandCategory.HighCard, PackTop(rankMask, 5));
    }

    /// <summary>Returns the high rank of the best straight in <paramref name="mask" />, or 0 if there is none.</summary>
    private static int HighestStraight(int mask)
    {
        //ace plays low: mirror the ace bit down so A-2-3-4-5 is detectable as a run ending at 5
        for (var high = 14; high >= 6; high--)
        {
            var run = 0b11111 << (high - 4);

            if ((mask & run) == run)
                return high;
        }

        return (mask & ACE_LOW_STRAIGHT_MASK) == ACE_LOW_STRAIGHT_MASK ? 5 : 0;
    }

    private static int HighestWithCount(ReadOnlySpan<int> rankCounts, int count, int excluding = 0)
    {
        for (var rank = 14; rank >= 2; rank--)
            if ((rankCounts[rank] == count) && (rank != excluding))
                return rank;

        return 0;
    }

    private static int HighestExcluding(int mask, int excludedRank)
    {
        var cleared = ClearRank(mask, excludedRank);

        for (var rank = 14; rank >= 2; rank--)
            if ((cleared & (1 << rank)) != 0)
                return rank;

        return 0;
    }

    private static int ClearRank(int mask, int rank) => mask & ~(1 << rank);

    /// <summary>Packs the top <paramref name="count" /> set ranks of <paramref name="mask" /> into nibbles.</summary>
    private static int PackTop(int mask, int count)
    {
        var packed = 0;
        var taken = 0;

        for (var rank = 14; (rank >= 2) && (taken < count); rank--)
            if ((mask & (1 << rank)) != 0)
            {
                packed = (packed << 4) | rank;
                taken++;
            }

        return packed;
    }

    private static int Pack(params int[] ranks)
    {
        var packed = 0;

        foreach (var rank in ranks)
            packed = (packed << 4) | rank;

        return packed;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/HandEvaluatorTests/*" --no-ansi
```

Expected: all pass. If `Detects_each_category` fails on the two-pair or trips rows, the tiebreak packing widths are the likely culprit — those two branches shift by a fixed amount and must not collide with the packed kicker nibbles.

- [ ] **Step 5: Commit**

```bash
git add Chaos/Services/Poker/HandCategory.cs Chaos/Services/Poker/HandRank.cs Chaos/Services/Poker/HandEvaluator.cs Tests/Chaos.Tests/Poker/HandEvaluatorTests.cs
git commit -m "Add 5-to-7 card poker hand evaluator"
```

---

### Task 3: Exhaustive evaluator verification

**Goal:** Prove the evaluator against the published poker hand frequency tables by full enumeration, not by sampling.

**Files:**
- Create: `Chaos-Server/Tests/Chaos.Tests/Poker/HandEvaluatorFrequencyTests.cs`

**Acceptance Criteria:**
- [ ] All `C(52,5) = 2,598,960` five-card hands are enumerated and each category count matches the published 5-card table exactly
- [ ] All `C(52,7) = 133,784,560` seven-card hands are enumerated and each category count matches the published 7-card table exactly
- [ ] Both tests assert their own totals equal `C(52,5)` and `C(52,7)` respectively
- [ ] The 7-card test lives in its own class so it can be excluded from routine runs by treenode filter

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -c Release -- --treenode-filter "/*/*/HandEvaluatorFrequencyTests/*" --no-ansi` → both pass

**Steps:**

- [ ] **Step 1: Write the tests**

The 5-card test runs in seconds. The 7-card test enumerates 133 million hands and takes minutes even in Release — that is expected and is the point.

Create `Chaos-Server/Tests/Chaos.Tests/Poker/HandEvaluatorFrequencyTests.cs`:

```csharp
using Chaos.Services.Poker;
using FluentAssertions;

namespace Chaos.Tests.Poker;

/// <summary>
///     Exhaustive verification of <see cref="HandEvaluator" /> against the published hand frequency tables.
/// </summary>
/// <remarks>
///     These are slow by construction -- the 7-card case evaluates every one of 133,784,560 hands. They live
///     in their own class so routine runs can exclude them with a treenode filter, and they are the reason
///     the evaluator is allowed to be the only unmocked dependency of the showdown code.
/// </remarks>
[NotInParallel]
public class HandEvaluatorFrequencyTests
{
    private static Dictionary<HandCategory, long> Enumerate(int handSize)
    {
        var counts = new Dictionary<HandCategory, long>();

        foreach (HandCategory category in Enum.GetValues<HandCategory>())
            counts[category] = 0;

        var deck = new Card[52];

        for (var i = 0; i < 52; i++)
            deck[i] = Card.FromIndex(i);

        var indices = new int[handSize];
        var hand = new Card[handSize];

        void Recurse(int depth, int start)
        {
            if (depth == handSize)
            {
                for (var i = 0; i < handSize; i++)
                    hand[i] = deck[indices[i]];

                counts[HandEvaluator.Evaluate(hand).Category]++;

                return;
            }

            for (var i = start; i <= 52 - (handSize - depth); i++)
            {
                indices[depth] = i;
                Recurse(depth + 1, i + 1);
            }
        }

        Recurse(0, 0);

        return counts;
    }

    [Test]
    public async Task Five_card_frequencies_match_the_published_table()
    {
        var counts = Enumerate(5);

        counts[HandCategory.StraightFlush].Should().Be(40);
        counts[HandCategory.FourOfAKind].Should().Be(624);
        counts[HandCategory.FullHouse].Should().Be(3_744);
        counts[HandCategory.Flush].Should().Be(5_108);
        counts[HandCategory.Straight].Should().Be(10_200);
        counts[HandCategory.ThreeOfAKind].Should().Be(54_912);
        counts[HandCategory.TwoPair].Should().Be(123_552);
        counts[HandCategory.OnePair].Should().Be(1_098_240);
        counts[HandCategory.HighCard].Should().Be(1_302_540);

        counts.Values
              .Sum()
              .Should()
              .Be(2_598_960); //C(52,5)

        await Task.CompletedTask;
    }

    [Test]
    public async Task Seven_card_frequencies_match_the_published_table()
    {
        var counts = Enumerate(7);

        counts[HandCategory.StraightFlush].Should().Be(41_584);
        counts[HandCategory.FourOfAKind].Should().Be(224_848);
        counts[HandCategory.FullHouse].Should().Be(3_473_184);
        counts[HandCategory.Flush].Should().Be(4_047_644);
        counts[HandCategory.Straight].Should().Be(6_180_020);
        counts[HandCategory.ThreeOfAKind].Should().Be(6_461_620);
        counts[HandCategory.TwoPair].Should().Be(31_433_400);
        counts[HandCategory.OnePair].Should().Be(58_627_800);
        counts[HandCategory.HighCard].Should().Be(23_294_460);

        counts.Values
              .Sum()
              .Should()
              .Be(133_784_560); //C(52,7)

        await Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Run the fast case first**

```bash
dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -c Release -- --treenode-filter "/*/*/HandEvaluatorFrequencyTests/Five_card_frequencies_match_the_published_table" --no-ansi
```

Expected: PASS in seconds. A mismatch here localises the bug by category — e.g. flush count high and straight-flush count low means `HighestStraight` is not being consulted on the flush suit.

- [ ] **Step 3: Run the slow case**

```bash
dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -c Release -- --treenode-filter "/*/*/HandEvaluatorFrequencyTests/Seven_card_frequencies_match_the_published_table" --no-ansi
```

Expected: PASS. Minutes, not seconds.

- [ ] **Step 4: Fix the evaluator if either fails**

Do not adjust the expected counts — they are verified arithmetic and sum exactly to `C(52,5)` and `C(52,7)`. Any mismatch is an evaluator bug.

- [ ] **Step 5: Commit**

```bash
git add Tests/Chaos.Tests/Poker/HandEvaluatorFrequencyTests.cs
git commit -m "Verify hand evaluator against exhaustive 5- and 7-card frequency tables"
```

---

### Task 4: Rake calculator

**Goal:** A pure function implementing 5% of pot, capped at 3 big bets, nothing raked without a flop.

**Files:**
- Create: `Chaos-Server/Chaos/Services/Poker/RakeCalculator.cs`
- Create: `Chaos-Server/Tests/Chaos.Tests/Poker/RakeCalculatorTests.cs`

**Acceptance Criteria:**
- [ ] 5% of the pot, rounded down to whole gold
- [ ] Capped at the configured maximum
- [ ] Returns 0 when the hand ended before the flop, regardless of pot size
- [ ] Never returns more than the pot
- [ ] Negative or zero pots return 0 rather than throwing

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/RakeCalculatorTests/*" --no-ansi` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `Chaos-Server/Tests/Chaos.Tests/Poker/RakeCalculatorTests.cs`:

```csharp
using Chaos.Services.Poker;
using FluentAssertions;

namespace Chaos.Tests.Poker;

public class RakeCalculatorTests
{
    private const decimal RATE = 0.05m;
    private const int CAP = 12_000;

    [Test]
    public async Task Takes_five_percent_of_a_contested_pot()
    {
        RakeCalculator.Calculate(100_000, true, RATE, CAP)
                      .Should()
                      .Be(5_000);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Rounds_down_to_whole_gold()
    {
        //5% of 4,010 is 200.5
        RakeCalculator.Calculate(4_010, true, RATE, CAP)
                      .Should()
                      .Be(200);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Caps_at_three_big_bets()
    {
        //5% of 500,000 would be 25,000
        RakeCalculator.Calculate(500_000, true, RATE, CAP)
                      .Should()
                      .Be(CAP);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Takes_nothing_when_the_hand_ended_before_the_flop()
    {
        RakeCalculator.Calculate(500_000, false, RATE, CAP)
                      .Should()
                      .Be(0);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Never_exceeds_the_pot()
    {
        RakeCalculator.Calculate(10, true, RATE, CAP)
                      .Should()
                      .BeLessThanOrEqualTo(10);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Empty_pot_rakes_nothing()
    {
        RakeCalculator.Calculate(0, true, RATE, CAP).Should().Be(0);
        RakeCalculator.Calculate(-5, true, RATE, CAP).Should().Be(0);

        await Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/RakeCalculatorTests/*" --no-ansi
```

Expected: build failure — `RakeCalculator` does not exist.

- [ ] **Step 3: Write the implementation**

Create `Chaos-Server/Chaos/Services/Poker/RakeCalculator.cs`:

```csharp
namespace Chaos.Services.Poker;

/// <summary>
///     The house's cut of a pot. This gold is destroyed, not redistributed -- it is the casino's gold sink.
/// </summary>
public static class RakeCalculator
{
    /// <param name="sawFlop">
    ///     False when the hand ended before the flop. "No flop, no drop" is a cardroom convention worth
    ///     keeping: without it, a table where everyone folds preflop still bleeds the blinds to the house,
    ///     which punishes exactly the tight play that keeps new players solvent.
    /// </param>
    public static int Calculate(int pot, bool sawFlop, decimal rate, int cap)
    {
        if (!sawFlop || (pot <= 0))
            return 0;

        var rake = (int)(pot * rate);

        return Math.Min(Math.Min(rake, cap), pot);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/RakeCalculatorTests/*" --no-ansi
```

Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add Chaos/Services/Poker/RakeCalculator.cs Tests/Chaos.Tests/Poker/RakeCalculatorTests.cs
git commit -m "Add poker rake calculator with cap and no-flop-no-drop"
```

---

### Task 5: Betting round state machine

**Goal:** One street's betting: whose turn it is, which actions are legal, when the round closes.

**Files:**
- Create: `Chaos-Server/Chaos/Services/Poker/PokerAction.cs`
- Create: `Chaos-Server/Chaos/Services/Poker/BettingRound.cs`
- Create: `Chaos-Server/Tests/Chaos.Tests/Poker/BettingRoundTests.cs`

**Acceptance Criteria:**
- [ ] Action passes clockwise from the configured first-to-act
- [ ] With no bet outstanding, legal actions are Check, Bet, Fold; Call and Raise are rejected
- [ ] With a bet outstanding, legal actions are Call, Raise, Fold; Check is rejected
- [ ] The four-bet cap closes raising: after bet, raise, re-raise, cap, only Call and Fold remain
- [ ] The round closes when every non-folded player has acted and matched the outstanding bet
- [ ] A raise reopens action for players who had already called
- [ ] The round closes immediately when only one player remains unfolded

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/BettingRoundTests/*" --no-ansi` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `Chaos-Server/Tests/Chaos.Tests/Poker/BettingRoundTests.cs`:

```csharp
using Chaos.Services.Poker;
using FluentAssertions;

namespace Chaos.Tests.Poker;

public class BettingRoundTests
{
    private const int BET_SIZE = 2_000;

    private static BettingRound ThreeHanded(int firstToAct = 0)
        => new(
            playerCount: 3,
            firstToAct: firstToAct,
            betSize: BET_SIZE,
            outstandingBet: 0,
            maxBets: 4);

    [Test]
    public async Task Action_passes_clockwise_from_first_to_act()
    {
        var round = ThreeHanded();

        round.ActorIndex.Should().Be(0);

        round.Apply(0, PokerAction.Check, out _);
        round.ActorIndex.Should().Be(1);

        round.Apply(1, PokerAction.Check, out _);
        round.ActorIndex.Should().Be(2);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Check_is_legal_with_no_bet_outstanding_and_call_is_not()
    {
        var round = ThreeHanded();

        round.IsLegal(0, PokerAction.Check).Should().BeTrue();
        round.IsLegal(0, PokerAction.Bet).Should().BeTrue();
        round.IsLegal(0, PokerAction.Fold).Should().BeTrue();
        round.IsLegal(0, PokerAction.Call).Should().BeFalse();
        round.IsLegal(0, PokerAction.Raise).Should().BeFalse();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Call_is_legal_with_a_bet_outstanding_and_check_is_not()
    {
        var round = ThreeHanded();
        round.Apply(0, PokerAction.Bet, out _);

        round.IsLegal(1, PokerAction.Call).Should().BeTrue();
        round.IsLegal(1, PokerAction.Raise).Should().BeTrue();
        round.IsLegal(1, PokerAction.Fold).Should().BeTrue();
        round.IsLegal(1, PokerAction.Check).Should().BeFalse();
        round.IsLegal(1, PokerAction.Bet).Should().BeFalse();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Four_bet_cap_closes_raising()
    {
        var round = ThreeHanded();

        round.Apply(0, PokerAction.Bet, out _);   //1
        round.Apply(1, PokerAction.Raise, out _); //2
        round.Apply(2, PokerAction.Raise, out _); //3
        round.Apply(0, PokerAction.Raise, out _); //4 -- capped

        round.IsLegal(1, PokerAction.Raise).Should().BeFalse();
        round.IsLegal(1, PokerAction.Call).Should().BeTrue();
        round.IsLegal(1, PokerAction.Fold).Should().BeTrue();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Round_closes_when_everyone_has_acted_and_matched()
    {
        var round = ThreeHanded();

        round.Apply(0, PokerAction.Check, out _).Should().BeFalse();
        round.Apply(1, PokerAction.Check, out _).Should().BeFalse();
        round.Apply(2, PokerAction.Check, out _).Should().BeTrue();

        round.IsComplete
             .Should()
             .BeTrue();

        await Task.CompletedTask;
    }

    [Test]
    public async Task A_raise_reopens_action_for_players_who_already_called()
    {
        var round = ThreeHanded();

        round.Apply(0, PokerAction.Bet, out _);
        round.Apply(1, PokerAction.Call, out _);
        round.Apply(2, PokerAction.Raise, out _).Should().BeFalse();

        //player 0 must answer the raise
        round.ActorIndex.Should().Be(0);
        round.IsComplete.Should().BeFalse();

        round.Apply(0, PokerAction.Call, out _);
        round.Apply(1, PokerAction.Call, out _).Should().BeTrue();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Round_closes_immediately_when_only_one_player_remains()
    {
        var round = ThreeHanded();

        round.Apply(0, PokerAction.Bet, out _);
        round.Apply(1, PokerAction.Fold, out _);
        round.Apply(2, PokerAction.Fold, out _)
             .Should()
             .BeTrue();

        round.RemainingPlayers
             .Should()
             .Be(1);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Contribution_reports_the_gold_a_bet_or_call_costs()
    {
        var round = ThreeHanded();

        round.Apply(0, PokerAction.Bet, out var betCost);
        betCost.Should().Be(BET_SIZE);

        round.Apply(1, PokerAction.Call, out var callCost);
        callCost.Should().Be(BET_SIZE);

        round.Apply(2, PokerAction.Raise, out var raiseCost);
        raiseCost.Should().Be(BET_SIZE * 2);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Acting_out_of_turn_throws()
    {
        var round = ThreeHanded();

        var outOfTurn = () => round.Apply(2, PokerAction.Check, out _);

        outOfTurn.Should()
                 .Throw<InvalidOperationException>();

        await Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/BettingRoundTests/*" --no-ansi
```

Expected: build failure — `BettingRound` and `PokerAction` do not exist.

- [ ] **Step 3: Write the implementation**

Create `Chaos-Server/Chaos/Services/Poker/PokerAction.cs`:

```csharp
namespace Chaos.Services.Poker;

public enum PokerAction : byte
{
    Fold = 0,
    Check = 1,
    Call = 2,
    Bet = 3,
    Raise = 4
}
```

Create `Chaos-Server/Chaos/Services/Poker/BettingRound.cs`:

```csharp
namespace Chaos.Services.Poker;

/// <summary>
///     One street of fixed-limit betting: whose turn it is, what they may do, and when the street closes.
/// </summary>
/// <remarks>
///     Deliberately knows nothing about gold, players, or cards -- it deals in seat indices and bet units, and
///     reports the cost of each action so the caller can move the money. That seam is what makes every
///     ordering rule here testable without a live table.
/// </remarks>
public sealed class BettingRound
{
    private readonly int BetSize;
    private readonly bool[] Folded;
    private readonly bool[] HasActed;
    private readonly int MaxBets;
    private readonly int[] Committed;

    private int BetsThisRound;

    public BettingRound(int playerCount, int firstToAct, int betSize, int outstandingBet, int maxBets)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(playerCount, 2);

        Folded = new bool[playerCount];
        HasActed = new bool[playerCount];
        Committed = new int[playerCount];
        BetSize = betSize;
        MaxBets = maxBets;
        OutstandingBet = outstandingBet;
        ActorIndex = firstToAct;
        BetsThisRound = outstandingBet > 0 ? 1 : 0;
    }

    public int ActorIndex { get; private set; }

    public bool IsComplete { get; private set; }

    public int OutstandingBet { get; private set; }

    public int RemainingPlayers => Folded.Count(folded => !folded);

    public bool HasFolded(int seatIndex) => Folded[seatIndex];

    public bool IsLegal(int seatIndex, PokerAction action)
    {
        if (IsComplete || Folded[seatIndex] || (seatIndex != ActorIndex))
            return false;

        var owes = OutstandingBet - Committed[seatIndex];

        return action switch
        {
            PokerAction.Fold  => true,
            PokerAction.Check => owes == 0,
            PokerAction.Call  => owes > 0,
            PokerAction.Bet   => (OutstandingBet == 0) && (BetsThisRound < MaxBets),
            PokerAction.Raise => (OutstandingBet > 0) && (BetsThisRound < MaxBets),
            _                 => false
        };
    }

    /// <summary>Applies <paramref name="action" />, reports its gold cost, and returns whether the street closed.</summary>
    public bool Apply(int seatIndex, PokerAction action, out int cost)
    {
        if (!IsLegal(seatIndex, action))
            throw new InvalidOperationException($"Seat {seatIndex} may not {action} right now");

        cost = 0;

        switch (action)
        {
            case PokerAction.Fold:
                Folded[seatIndex] = true;

                break;
            case PokerAction.Check:
                break;
            case PokerAction.Call:
                cost = OutstandingBet - Committed[seatIndex];
                Committed[seatIndex] += cost;

                break;
            case PokerAction.Bet:
            case PokerAction.Raise:
                OutstandingBet += BetSize;
                cost = OutstandingBet - Committed[seatIndex];
                Committed[seatIndex] += cost;
                BetsThisRound++;

                //a raise puts the decision back to everyone who had already settled
                Array.Fill(HasActed, false);

                break;
        }

        HasActed[seatIndex] = true;

        if (RemainingPlayers <= 1)
        {
            IsComplete = true;

            return true;
        }

        AdvanceActor();

        IsComplete = Enumerable.Range(0, Folded.Length)
                               .Where(i => !Folded[i])
                               .All(i => HasActed[i] && (Committed[i] == OutstandingBet));

        return IsComplete;
    }

    private void AdvanceActor()
    {
        for (var step = 1; step <= Folded.Length; step++)
        {
            var candidate = (ActorIndex + step) % Folded.Length;

            if (!Folded[candidate])
            {
                ActorIndex = candidate;

                return;
            }
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/BettingRoundTests/*" --no-ansi
```

Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add Chaos/Services/Poker/PokerAction.cs Chaos/Services/Poker/BettingRound.cs Tests/Chaos.Tests/Poker/BettingRoundTests.cs
git commit -m "Add fixed-limit betting round state machine"
```

---

### Task 6: Hand orchestration and gold movement

**Goal:** One complete hand — blinds, four streets, showdown, rake, award — with gold moving through a testable seam.

**Files:**
- Create: `Chaos-Server/Chaos/Services/Poker/IPokerSeatOccupant.cs`
- Create: `Chaos-Server/Chaos/Services/Poker/PokerHand.cs`
- Create: `Chaos-Server/Tests/Chaos.Tests/Poker/PokerHandTests.cs`

**Acceptance Criteria:**
- [ ] Blinds are posted before cards are dealt; heads-up, the button posts the small blind
- [ ] Heads-up, the button acts first preflop and last on every later street
- [ ] Streets use the small bet preflop and on the flop, the big bet on the turn and river
- [ ] **Gold in equals gold out plus rake**, asserted on every hand the tests run
- [ ] Rake is taken before the pot is split
- [ ] On an exact tie, odd gold goes to the first tied winner clockwise from the button
- [ ] A hand won before the flop takes no rake
- [ ] Folding forfeits gold already committed

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/PokerHandTests/*" --no-ansi` → all pass

**Steps:**

- [ ] **Step 1: Write the seam and the failing tests**

Create `Chaos-Server/Chaos/Services/Poker/IPokerSeatOccupant.cs`:

```csharp
namespace Chaos.Services.Poker;

/// <summary>
///     The gold seam between the poker engine and the world.
/// </summary>
/// <remarks>
///     Exists so <see cref="PokerHand" /> -- the only component that moves gold -- can be tested exhaustively
///     without constructing an <c>Aisling</c>, a map, or a client. The production implementation is a thin
///     adapter over <c>Aisling.TryTakeGold</c> / <c>Aisling.TryGiveGold</c>.
/// </remarks>
public interface IPokerSeatOccupant
{
    uint Id { get; }

    string Name { get; }

    int Gold { get; }

    bool TryTakeGold(int amount);

    bool TryGiveGold(int amount);
}
```

Create `Chaos-Server/Tests/Chaos.Tests/Poker/PokerHandTests.cs`:

```csharp
using Chaos.Services.Poker;
using FluentAssertions;

namespace Chaos.Tests.Poker;

public class PokerHandTests
{
    private const int SMALL_BET = 2_000;
    private const int BIG_BET = 4_000;
    private const int SMALL_BLIND = 1_000;
    private const int BIG_BLIND = 2_000;

    private sealed class FakeOccupant(uint id, string name, int gold) : IPokerSeatOccupant
    {
        public uint Id { get; } = id;
        public string Name { get; } = name;
        public int Gold { get; private set; } = gold;

        public bool TryTakeGold(int amount)
        {
            if (amount > Gold)
                return false;

            Gold -= amount;

            return true;
        }

        public bool TryGiveGold(int amount)
        {
            Gold += amount;

            return true;
        }
    }

    private static PokerHand StartHand(params FakeOccupant[] players)
        => new(
            players.Cast<IPokerSeatOccupant>().ToArray(),
            buttonIndex: 0,
            smallBlind: SMALL_BLIND,
            bigBlind: BIG_BLIND,
            smallBet: SMALL_BET,
            bigBet: BIG_BET,
            rakeRate: 0.05m,
            rakeCap: 12_000,
            maxBetsPerStreet: 4);

    [Test]
    public async Task Blinds_are_posted_when_the_hand_starts()
    {
        var a = new FakeOccupant(1, "A", 100_000);
        var b = new FakeOccupant(2, "B", 100_000);

        StartHand(a, b);

        //heads-up: the button (seat 0) posts the small blind
        a.Gold.Should().Be(100_000 - SMALL_BLIND);
        b.Gold.Should().Be(100_000 - BIG_BLIND);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Heads_up_button_acts_first_preflop_and_last_afterwards()
    {
        var a = new FakeOccupant(1, "A", 100_000);
        var b = new FakeOccupant(2, "B", 100_000);

        var hand = StartHand(a, b);

        hand.ActorIndex.Should().Be(0);

        hand.Act(0, PokerAction.Call);
        hand.Act(1, PokerAction.Check);

        //flop: the non-button acts first
        hand.Street.Should().Be(PokerStreet.Flop);
        hand.ActorIndex.Should().Be(1);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Gold_in_equals_gold_out_plus_rake()
    {
        var a = new FakeOccupant(1, "A", 100_000);
        var b = new FakeOccupant(2, "B", 100_000);

        var hand = StartHand(a, b);

        hand.Act(0, PokerAction.Call);
        hand.Act(1, PokerAction.Check);
        hand.Act(1, PokerAction.Check);  //flop
        hand.Act(0, PokerAction.Check);
        hand.Act(1, PokerAction.Check);  //turn
        hand.Act(0, PokerAction.Check);
        hand.Act(1, PokerAction.Check);  //river
        hand.Act(0, PokerAction.Check);

        hand.IsComplete.Should().BeTrue();

        (a.Gold + b.Gold + hand.RakeTaken)
            .Should()
            .Be(200_000);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Winning_before_the_flop_takes_no_rake()
    {
        var a = new FakeOccupant(1, "A", 100_000);
        var b = new FakeOccupant(2, "B", 100_000);

        var hand = StartHand(a, b);
        hand.Act(0, PokerAction.Fold);

        hand.IsComplete.Should().BeTrue();
        hand.RakeTaken.Should().Be(0);

        //the big blind takes the small blind, uncut
        b.Gold.Should().Be(100_000 + SMALL_BLIND);
        a.Gold.Should().Be(100_000 - SMALL_BLIND);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Folding_forfeits_gold_already_committed()
    {
        var a = new FakeOccupant(1, "A", 100_000);
        var b = new FakeOccupant(2, "B", 100_000);

        var hand = StartHand(a, b);

        hand.Act(0, PokerAction.Call);   //a now in for 2,000
        hand.Act(1, PokerAction.Check);
        hand.Act(1, PokerAction.Bet);    //flop
        hand.Act(0, PokerAction.Fold);

        hand.IsComplete.Should().BeTrue();
        a.Gold.Should().Be(100_000 - BIG_BLIND);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Rake_is_taken_before_the_pot_is_split()
    {
        var a = new FakeOccupant(1, "A", 100_000);
        var b = new FakeOccupant(2, "B", 100_000);

        var hand = StartHand(a, b);
        hand.ForceBoardForTesting("AsKsQsJsTs");
        hand.ForceHoleCardsForTesting(0, "2c3d");
        hand.ForceHoleCardsForTesting(1, "4c5d");

        hand.Act(0, PokerAction.Call);
        hand.Act(1, PokerAction.Check);
        hand.Act(1, PokerAction.Bet);   //flop
        hand.Act(0, PokerAction.Call);
        hand.Act(1, PokerAction.Check); //turn
        hand.Act(0, PokerAction.Check);
        hand.Act(1, PokerAction.Check); //river
        hand.Act(0, PokerAction.Check);

        //both play the board -- an exact tie
        hand.Winners.Count.Should().Be(2);
        hand.RakeTaken.Should().BeGreaterThan(0);

        (a.Gold + b.Gold + hand.RakeTaken)
            .Should()
            .Be(200_000);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Odd_gold_on_a_tie_goes_clockwise_from_the_button()
    {
        var a = new FakeOccupant(1, "A", 100_000);
        var b = new FakeOccupant(2, "B", 100_000);

        var hand = StartHand(a, b);
        hand.ForceBoardForTesting("AsKsQsJsTs");
        hand.ForceHoleCardsForTesting(0, "2c3d");
        hand.ForceHoleCardsForTesting(1, "4c5d");
        hand.ForceOddPotForTesting(1); //make the raked pot odd

        hand.Act(0, PokerAction.Call);
        hand.Act(1, PokerAction.Check);
        hand.Act(1, PokerAction.Check);
        hand.Act(0, PokerAction.Check);
        hand.Act(1, PokerAction.Check);
        hand.Act(0, PokerAction.Check);
        hand.Act(1, PokerAction.Check);
        hand.Act(0, PokerAction.Check);

        //seat 1 is the first seat clockwise from the button at seat 0
        hand.OddGoldRecipientIndex
            .Should()
            .Be(1);

        await Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/PokerHandTests/*" --no-ansi
```

Expected: build failure — `PokerHand` and `PokerStreet` do not exist.

- [ ] **Step 3: Write the implementation**

Create `Chaos-Server/Chaos/Services/Poker/PokerHand.cs`. Required public surface, exactly as the tests use it:

```csharp
namespace Chaos.Services.Poker;

public enum PokerStreet : byte
{
    Preflop = 0,
    Flop = 1,
    Turn = 2,
    River = 3,
    Showdown = 4
}
```

`PokerHand` must expose:

| Member | Meaning |
| --- | --- |
| `PokerHand(IPokerSeatOccupant[] players, int buttonIndex, int smallBlind, int bigBlind, int smallBet, int bigBet, decimal rakeRate, int rakeCap, int maxBetsPerStreet)` | Posts blinds, shuffles, deals hole cards, opens preflop betting |
| `int ActorIndex { get; }` | Seat index whose turn it is |
| `PokerStreet Street { get; }` | Current street |
| `bool IsComplete { get; }` | Hand is settled and gold has moved |
| `int Pot { get; }` | Gold currently escrowed |
| `int RakeTaken { get; }` | 0 until settlement |
| `IReadOnlyList<int> Winners { get; }` | Seat indices, populated at settlement |
| `int OddGoldRecipientIndex { get; }` | −1 unless an odd split occurred |
| `IReadOnlyList<Card> Board { get; }` | Community cards revealed so far |
| `IReadOnlyList<Card> HoleCardsOf(int seatIndex)` | Owner-only accessor; callers must not broadcast |
| `bool HasFolded(int seatIndex)` | Used by the snapshot builder in Task 13 |
| `int CommittedBy(int seatIndex)` | Gold this seat has put in the pot this hand; used by Task 13 |
| `void Act(int seatIndex, PokerAction action)` | Applies the action, advances the street, settles when done |
| `void ForceBoardForTesting(string notation)` | Test seam; see Step 4 |
| `void ForceHoleCardsForTesting(int seatIndex, string notation)` | Test seam |
| `void ForceOddPotForTesting(int extraGold)` | Test seam |

Implementation rules — encode each of these explicitly:

1. **Blind posting.** Heads-up (`players.Length == 2`): the button posts the small blind, the other seat posts the big blind. Three or more: small blind is the seat clockwise of the button, big blind the next.
2. **First to act.** Heads-up preflop: the button. Heads-up postflop: the non-button. Three or more preflop: the seat clockwise of the big blind. Three or more postflop: the seat clockwise of the button.
3. **Bet sizes.** `Preflop` and `Flop` use `smallBet`; `Turn` and `River` use `bigBet`.
4. **Preflop `outstandingBet`** starts at `bigBlind`, and blind posts pre-seed each blind seat's committed amount so the big blind may check.
5. **Street advance.** When a `BettingRound` completes with two or more players remaining, deal the next community cards (3 / 1 / 1) and open a fresh round.
6. **Early end.** When a round completes with exactly one player remaining, settle immediately; that player is the sole winner and `sawFlop` is `Street >= PokerStreet.Flop`.
7. **Settlement.** `rake = RakeCalculator.Calculate(Pot, sawFlop, rakeRate, rakeCap)`. Subtract it from the pot **first**. Evaluate `HandEvaluator.Evaluate([..HoleCardsOf(i), ..Board])` for each unfolded seat, take the maximum, and collect every seat equal to it as `Winners`. Split `Pot - rake` by integer division; award the remainder to the first winner clockwise from the button, recording that seat in `OddGoldRecipientIndex`.
8. **Gold movement.** Every bet, blind, and call calls `TryTakeGold` and throws `InvalidOperationException` if it returns false — eligibility is checked before the hand starts, so a failure here is a bug, not a game state.

- [ ] **Step 4: Keep the test seams honest**

The three `*ForTesting` methods exist because the shuffle is cryptographic and therefore unstealable. Constrain them so they cannot leak into production:

```csharp
    /// <summary>
    ///     Overrides the community cards. Test-only: the production shuffle is cryptographic and
    ///     deliberately unseedable, so showdown tests have no other way to construct a known board.
    /// </summary>
    /// <exception cref="InvalidOperationException">If called after the flop has been dealt.</exception>
    public void ForceBoardForTesting(string notation)
    {
        if (Street > PokerStreet.Preflop)
            throw new InvalidOperationException("Board can only be forced before the flop");

        ForcedBoard = ParseNotation(notation);
    }
```

`ForceHoleCardsForTesting` throws if the hand has advanced past preflop. `ForceOddPotForTesting` adds dead gold to the pot and throws if the hand is complete. None of them are called from any non-test file — verify with grep in Step 6.

- [ ] **Step 5: Run tests to verify they pass**

```bash
dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/PokerHandTests/*" --no-ansi
```

Expected: all pass. `Gold_in_equals_gold_out_plus_rake` is the load-bearing one — if it fails, gold is being minted or destroyed somewhere other than the rake, and nothing downstream should be built until it passes.

- [ ] **Step 6: Confirm the test seams are unused in production**

```bash
grep -rn "ForTesting" Chaos/ --include=*.cs
```

Expected: matches only inside `Chaos/Services/Poker/PokerHand.cs` (the definitions). Any match under `Chaos/Scripting/` or `Chaos/Networking/` is a defect — remove it.

- [ ] **Step 7: Commit**

```bash
git add Chaos/Services/Poker/IPokerSeatOccupant.cs Chaos/Services/Poker/PokerHand.cs Tests/Chaos.Tests/Poker/PokerHandTests.cs
git commit -m "Add poker hand orchestration with rake-then-split settlement"
```

---

### Task 7: Table configuration and validation

**Goal:** Stakes and timers come from content JSON, and startup refuses a configuration where the minimum buy-in does not cover the maximum exposure.

**Files:**
- Create: `Chaos-Server/Chaos/Services/Poker/PokerTableConfig.cs`
- Create: `Chaos-Server/Chaos/Services/Poker/PokerConfigValidator.cs`
- Create: `Chaos-Server/Tests/Chaos.Tests/Poker/PokerConfigValidatorTests.cs`

**Acceptance Criteria:**
- [ ] `MaxExposure` computes as `maxBetsPerStreet × (2 × smallBet + 2 × bigBet)`
- [ ] Validation fails when `MinimumBuyIn < MaxExposure`
- [ ] Validation fails when `bigBet != 2 × smallBet`
- [ ] Validation fails when `seatCount` is outside 2–6
- [ ] Validation fails when `rakeRate` is outside 0–1 or `rakeCap` is negative
- [ ] The shipped configuration (2,000 / 4,000, 6 seats, 48,000 buy-in) passes

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/PokerConfigValidatorTests/*" --no-ansi` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `Chaos-Server/Tests/Chaos.Tests/Poker/PokerConfigValidatorTests.cs`:

```csharp
using Chaos.Services.Poker;
using FluentAssertions;

namespace Chaos.Tests.Poker;

public class PokerConfigValidatorTests
{
    private static PokerTableConfig Shipped()
        => new()
        {
            TableName = "Rucesion Hold'em",
            SeatCount = 6,
            SmallBlind = 1_000,
            BigBlind = 2_000,
            SmallBet = 2_000,
            BigBet = 4_000,
            MaxBetsPerStreet = 4,
            MinimumBuyIn = 48_000,
            RakeRate = 0.05m,
            RakeCap = 12_000,
            ActionTimeoutSeconds = 20,
            TimeoutsBeforeSitOut = 2,
            SitOutHandsBeforeRelease = 3
        };

    [Test]
    public async Task Max_exposure_is_four_bets_across_four_streets()
    {
        Shipped()
            .MaxExposure
            .Should()
            .Be(48_000);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Shipped_configuration_is_valid()
    {
        PokerConfigValidator.Validate(Shipped())
                            .Should()
                            .BeEmpty();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Buy_in_below_max_exposure_is_rejected()
    {
        var config = Shipped() with { MinimumBuyIn = 40_000 };

        PokerConfigValidator.Validate(config)
                            .Should()
                            .ContainSingle()
                            .Which
                            .Should()
                            .Contain("MinimumBuyIn");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Big_bet_must_be_double_the_small_bet()
    {
        var config = Shipped() with { BigBet = 5_000 };

        PokerConfigValidator.Validate(config)
                            .Should()
                            .NotBeEmpty();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Seat_count_outside_two_to_six_is_rejected()
    {
        PokerConfigValidator.Validate(Shipped() with { SeatCount = 1 }).Should().NotBeEmpty();
        PokerConfigValidator.Validate(Shipped() with { SeatCount = 7 }).Should().NotBeEmpty();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Nonsense_rake_settings_are_rejected()
    {
        PokerConfigValidator.Validate(Shipped() with { RakeRate = 1.5m }).Should().NotBeEmpty();
        PokerConfigValidator.Validate(Shipped() with { RakeRate = -0.1m }).Should().NotBeEmpty();
        PokerConfigValidator.Validate(Shipped() with { RakeCap = -1 }).Should().NotBeEmpty();

        await Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/PokerConfigValidatorTests/*" --no-ansi
```

Expected: build failure — the types do not exist.

- [ ] **Step 3: Write the implementation**

Create `Chaos-Server/Chaos/Services/Poker/PokerTableConfig.cs`:

```csharp
namespace Chaos.Services.Poker;

public sealed record PokerTableConfig
{
    public required string TableName { get; init; }
    public required int SeatCount { get; init; }
    public required int SmallBlind { get; init; }
    public required int BigBlind { get; init; }
    public required int SmallBet { get; init; }
    public required int BigBet { get; init; }
    public required int MaxBetsPerStreet { get; init; }
    public required int MinimumBuyIn { get; init; }
    public required decimal RakeRate { get; init; }
    public required int RakeCap { get; init; }
    public required int ActionTimeoutSeconds { get; init; }
    public required int TimeoutsBeforeSitOut { get; init; }
    public required int SitOutHandsBeforeRelease { get; init; }

    /// <summary>
    ///     The most gold one hand can cost a player: the four-bet cap across two small-bet streets and two
    ///     big-bet streets. The whole no-side-pots design rests on this number being reachable by everyone
    ///     dealt in, which is why <see cref="PokerConfigValidator" /> refuses a smaller
    ///     <see cref="MinimumBuyIn" />.
    /// </summary>
    public int MaxExposure => MaxBetsPerStreet * ((2 * SmallBet) + (2 * BigBet));
}
```

Create `Chaos-Server/Chaos/Services/Poker/PokerConfigValidator.cs`:

```csharp
namespace Chaos.Services.Poker;

public static class PokerConfigValidator
{
    public static IReadOnlyList<string> Validate(PokerTableConfig config)
    {
        var errors = new List<string>();

        if (config.MinimumBuyIn < config.MaxExposure)
            errors.Add(
                $"MinimumBuyIn ({config.MinimumBuyIn}) is below MaxExposure ({config.MaxExposure}). "
                + "A player could run out of gold mid-hand, which this table has no side-pot logic to handle.");

        if (config.BigBet != (config.SmallBet * 2))
            errors.Add($"BigBet ({config.BigBet}) must be exactly double SmallBet ({config.SmallBet})");

        if (config.SeatCount is < 2 or > 6)
            errors.Add($"SeatCount ({config.SeatCount}) must be between 2 and 6");

        if (config.RakeRate is < 0m or > 1m)
            errors.Add($"RakeRate ({config.RakeRate}) must be between 0 and 1");

        if (config.RakeCap < 0)
            errors.Add($"RakeCap ({config.RakeCap}) cannot be negative");

        if (config.MaxBetsPerStreet < 1)
            errors.Add($"MaxBetsPerStreet ({config.MaxBetsPerStreet}) must be at least 1");

        if (config.ActionTimeoutSeconds < 5)
            errors.Add($"ActionTimeoutSeconds ({config.ActionTimeoutSeconds}) is too short to be playable");

        return errors;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/PokerConfigValidatorTests/*" --no-ansi
```

Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add Chaos/Services/Poker/PokerTableConfig.cs Chaos/Services/Poker/PokerConfigValidator.cs Tests/Chaos.Tests/Poker/PokerConfigValidatorTests.cs
git commit -m "Add poker table configuration with buy-in coverage validation"
```

---

### Task 8: Table seating and hand scheduling

**Goal:** Seats, button rotation, eligibility, sit-out, and the shot clock — everything that decides *when* a hand starts and who is in it.

**Files:**
- Create: `Chaos-Server/Chaos/Services/Poker/PokerTable.cs`
- Create: `Chaos-Server/Tests/Chaos.Tests/Poker/PokerTableTests.cs`

**Acceptance Criteria:**
- [ ] Seating and standing up work only between hands
- [ ] A hand starts when two or more occupants are eligible (seated, not sitting out, holding `MinimumBuyIn`)
- [ ] An occupant short of `MinimumBuyIn` at deal time is sat out, not removed
- [ ] The button advances one occupied seat clockwise per hand
- [ ] Timing out folds; `TimeoutsBeforeSitOut` consecutive timeouts sits the player out
- [ ] `SitOutHandsBeforeRelease` hands sat out releases the seat
- [ ] A player leaving mid-hand folds at their turn and forfeits committed gold
- [ ] Heads-up play works end to end

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/PokerTableTests/*" --no-ansi` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

Create `Chaos-Server/Tests/Chaos.Tests/Poker/PokerTableTests.cs` covering, one test each:

```csharp
using Chaos.Services.Poker;
using FluentAssertions;

namespace Chaos.Tests.Poker;

public class PokerTableTests
{
    private sealed class FakeOccupant(uint id, string name, int gold) : IPokerSeatOccupant
    {
        public uint Id { get; } = id;
        public string Name { get; } = name;
        public int Gold { get; private set; } = gold;

        public bool TryTakeGold(int amount)
        {
            if (amount > Gold)
                return false;

            Gold -= amount;

            return true;
        }

        public bool TryGiveGold(int amount)
        {
            Gold += amount;

            return true;
        }
    }

    private static PokerTableConfig Config()
        => new()
        {
            TableName = "Test",
            SeatCount = 6,
            SmallBlind = 1_000,
            BigBlind = 2_000,
            SmallBet = 2_000,
            BigBet = 4_000,
            MaxBetsPerStreet = 4,
            MinimumBuyIn = 48_000,
            RakeRate = 0.05m,
            RakeCap = 12_000,
            ActionTimeoutSeconds = 20,
            TimeoutsBeforeSitOut = 2,
            SitOutHandsBeforeRelease = 3
        };

    [Test]
    public async Task One_eligible_player_does_not_start_a_hand()
    {
        var table = new PokerTable(Config());
        table.TrySit(new FakeOccupant(1, "A", 100_000), 0).Should().BeTrue();

        table.Update(TimeSpan.FromSeconds(1));

        table.CurrentHand.Should().BeNull();
        table.IsWaitingForPlayers.Should().BeTrue();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Two_eligible_players_start_a_hand()
    {
        var table = new PokerTable(Config());
        table.TrySit(new FakeOccupant(1, "A", 100_000), 0);
        table.TrySit(new FakeOccupant(2, "B", 100_000), 1);

        table.Update(TimeSpan.FromSeconds(1));

        table.CurrentHand.Should().NotBeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Player_short_of_the_buy_in_is_sat_out_not_removed()
    {
        var table = new PokerTable(Config());
        table.TrySit(new FakeOccupant(1, "A", 100_000), 0);
        table.TrySit(new FakeOccupant(2, "B", 100_000), 1);
        table.TrySit(new FakeOccupant(3, "Broke", 10_000), 2);

        table.Update(TimeSpan.FromSeconds(1));

        table.IsSittingOut(2).Should().BeTrue();
        table.IsSeated(2).Should().BeTrue();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Button_advances_one_occupied_seat_per_hand()
    {
        var table = new PokerTable(Config());
        table.TrySit(new FakeOccupant(1, "A", 500_000), 0);
        table.TrySit(new FakeOccupant(2, "B", 500_000), 3);

        table.Update(TimeSpan.FromSeconds(1));
        var first = table.ButtonIndex;

        table.AbandonHandForTesting();
        table.Update(TimeSpan.FromSeconds(1));

        table.ButtonIndex.Should().NotBe(first);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Timing_out_folds_and_repeat_timeouts_sit_the_player_out()
    {
        var table = new PokerTable(Config());
        table.TrySit(new FakeOccupant(1, "A", 500_000), 0);
        table.TrySit(new FakeOccupant(2, "B", 500_000), 1);
        table.Update(TimeSpan.FromSeconds(1));

        var actor = table.CurrentHand!.ActorIndex;

        //two consecutive timeouts across two hands
        table.Update(TimeSpan.FromSeconds(21));
        table.Update(TimeSpan.FromSeconds(1));
        table.Update(TimeSpan.FromSeconds(21));

        table.ConsecutiveTimeouts(actor)
             .Should()
             .BeGreaterThanOrEqualTo(1);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Sitting_out_too_long_releases_the_seat()
    {
        var table = new PokerTable(Config());
        table.TrySit(new FakeOccupant(1, "A", 500_000), 0);
        table.TrySit(new FakeOccupant(2, "B", 500_000), 1);
        table.TrySit(new FakeOccupant(3, "Idle", 500_000), 2);
        table.SitOut(2);

        for (var i = 0; i < 4; i++)
        {
            table.Update(TimeSpan.FromSeconds(1));
            table.AbandonHandForTesting();
        }

        table.IsSeated(2)
             .Should()
             .BeFalse();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Sitting_is_refused_while_a_hand_is_running()
    {
        var table = new PokerTable(Config());
        table.TrySit(new FakeOccupant(1, "A", 500_000), 0);
        table.TrySit(new FakeOccupant(2, "B", 500_000), 1);
        table.Update(TimeSpan.FromSeconds(1));

        table.TrySit(new FakeOccupant(3, "Late", 500_000), 2)
             .Should()
             .BeFalse();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Leaving_mid_hand_folds_at_your_turn()
    {
        var table = new PokerTable(Config());
        table.TrySit(new FakeOccupant(1, "A", 500_000), 0);
        var leaver = new FakeOccupant(2, "B", 500_000);
        table.TrySit(leaver, 1);
        table.Update(TimeSpan.FromSeconds(1));

        table.Leave(1);

        table.IsSeated(1).Should().BeFalse();
        table.CurrentHand.Should().BeNull(); //heads-up: the hand ends when one player leaves

        await Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/PokerTableTests/*" --no-ansi
```

Expected: build failure — `PokerTable` does not exist.

- [ ] **Step 3: Write the implementation**

Create `Chaos-Server/Chaos/Services/Poker/PokerTable.cs` with this surface:

| Member | Meaning |
| --- | --- |
| `PokerTable(PokerTableConfig config)` | Empty table, no hand running |
| `PokerTableConfig Config { get; }` | |
| `PokerHand? CurrentHand { get; }` | Null between hands |
| `int ButtonIndex { get; }` | |
| `bool IsWaitingForPlayers { get; }` | True when fewer than two occupants are eligible |
| `bool TrySit(IPokerSeatOccupant occupant, int seatIndex)` | False if the seat is taken, out of range, or a hand is running |
| `void Leave(int seatIndex)` | Between hands: immediate. Mid-hand: folds at their turn, forfeits committed gold |
| `void SitOut(int seatIndex)` / `void SitIn(int seatIndex)` | |
| `bool IsSeated(int seatIndex)` / `bool IsSittingOut(int seatIndex)` | |
| `int ConsecutiveTimeouts(int seatIndex)` | |
| `IPokerSeatOccupant? OccupantAt(int seatIndex)` | |
| `int StateVersion { get; }` | Incremented on every state change: any action, timeout, seat change, or hand start/end. Task 13 broadcasts only when this moves, so snapshots do not go out every tick. |
| `void Act(int seatIndex, PokerAction action)` | Delegates to `CurrentHand` |
| `void Update(TimeSpan delta)` | Drives the shot clock and starts hands |
| `void AbandonHandForTesting()` | Test seam; drops the current hand without settling |

Implementation rules:

1. **Eligibility** is `IsSeated && !IsSittingOut && Occupant.Gold >= Config.MinimumBuyIn`. Re-checked at the top of every hand; a seated occupant who fails it is sat out with a reason, never unseated.
2. **Hand start.** In `Update`, if `CurrentHand is null` and two or more seats are eligible, advance the button to the next eligible seat clockwise, build the occupant array in seat order, and construct a `PokerHand`.
3. **Shot clock.** Accumulate `delta` while a hand is running. On exceeding `ActionTimeoutSeconds`, apply `PokerAction.Fold` for `CurrentHand.ActorIndex`, increment that seat's `ConsecutiveTimeouts`, and reset the clock. Any voluntary action resets that seat's counter to zero.
4. **Sit-out escalation.** `ConsecutiveTimeouts >= Config.TimeoutsBeforeSitOut` → `SitOut`. A seat that has been sitting out for `Config.SitOutHandsBeforeRelease` completed hands is released.
5. **Hand completion.** When `CurrentHand.IsComplete`, increment the sat-out counters, clear `CurrentHand`, and let the next `Update` start the next hand.
6. **Heads-up departure.** If a leave or release drops eligibility below two mid-hand, settle the hand immediately in favour of the remaining player and clear `CurrentHand`.

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/PokerTableTests/*" --no-ansi
```

Expected: all pass.

- [ ] **Step 5: Run the whole engine suite together**

```bash
dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/*Poker*/*" --no-ansi
dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/CardTests/*" --no-ansi
dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/DeckTests/*" --no-ansi
dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/HandEvaluatorTests/*" --no-ansi
dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/RakeCalculatorTests/*" --no-ansi
dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/BettingRoundTests/*" --no-ansi
```

Expected: all green. The engine is now complete and provable with no server running.

- [ ] **Step 6: Commit**

```bash
git add Chaos/Services/Poker/PokerTable.cs Tests/Chaos.Tests/Poker/PokerTableTests.cs
git commit -m "Add poker table seating, button rotation and shot clock"
```

---

### Task 9: Packet enums and argument records

**Goal:** The shared wire types, compiled into both server and client.

**Files:**
- Modify: `Chaos-Server/Chaos.Networking.Abstractions/Definitions/Enums.cs`
- Modify: `Chaos-Server/Chaos.DarkAges/Definitions/Enums.cs`
- Create: `Chaos-Server/Chaos.Networking/Entities/Server/PokerTableDisplayArgs.cs`
- Create: `Chaos-Server/Chaos.Networking/Entities/Client/PokerTableInteractionArgs.cs`

**Acceptance Criteria:**
- [ ] `ClientOpCode.PokerTableInteraction = 118` (verified unused)
- [ ] `ServerOpCode.PokerTableDisplay = 120` (verified unused)
- [ ] `PokerDisplayType`, `PokerInteractionType`, `PokerRejectReason` added next to the slot equivalents
- [ ] `PokerTableDisplayArgs` carries a full snapshot including a per-seat roster
- [ ] Hole cards appear on the snapshot only as *the recipient's own*, plus a showdown-only reveal list
- [ ] Solution builds

**Verify:** `dotnet build Chaos.slnx` → succeeds with no new warnings

**Steps:**

- [ ] **Step 1: Confirm the opcodes are free**

```bash
grep -n "= 118\|= 119\|= 120\|= 121" Chaos.Networking.Abstractions/Definitions/Enums.cs
```

Expected: `ClientOpCode` has nothing at 118–120 (117 is `SynchronizeTicksResponse`, 121 is `SocialStatus`); `ServerOpCode` has nothing at 120–123 (119 is `WheelDisplay`, 124 is `Poll`). If either has moved since this plan was written, pick the next free value and update every reference in Tasks 9–11 and 14.

- [ ] **Step 2: Add the opcodes**

In `Chaos.Networking.Abstractions/Definitions/Enums.cs`, add to `ClientOpCode` beside `SlotMachineInteraction = 116`:

```csharp
    /// <summary>
    ///     Sent when a player acts at a poker table. Carries no table id and no bet size -- the server
    ///     resolves both from the seat claim it already holds, so a modified client has nothing to lie about.
    /// </summary>
    PokerTableInteraction = 118,
```

And to `ServerOpCode` beside `WheelDisplay = 119`:

```csharp
    /// <summary>
    ///     A complete poker table snapshot, built per recipient.
    /// </summary>
    PokerTableDisplay = 120,
```

- [ ] **Step 3: Add the display and interaction enums**

In `Chaos.DarkAges/Definitions/Enums.cs`, beside `SlotDisplayType`:

```csharp
public enum PokerDisplayType : byte
{
    /// <summary>The player has taken a seat; open the panel and send the first snapshot.</summary>
    Open = 0,

    /// <summary>A complete authoritative table state. Snapshots are absolute, never deltas.</summary>
    Snapshot = 1,

    /// <summary>The last interaction was refused.</summary>
    Rejected = 2,

    /// <summary>Close the panel.</summary>
    Close = 3
}

public enum PokerInteractionType : byte
{
    Leave = 0,
    SitOut = 1,
    SitIn = 2,
    Act = 3,
    Close = 4
}

public enum PokerRejectReason : byte
{
    NotSeated = 0,
    NotYourTurn = 1,
    IllegalAction = 2,
    InsufficientGold = 3,
    HandInProgress = 4
}
```

`PokerActionType` is not a new enum — the wire carries `Chaos.Services.Poker.PokerAction` values as a raw byte, because that enum is already `: byte` with stable members.

- [ ] **Step 4: Add the argument records**

Create `Chaos-Server/Chaos.Networking/Entities/Server/PokerTableDisplayArgs.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Entities.Server;

/// <summary>One seat's public state, as seen by any recipient.</summary>
public sealed record PokerSeatEntry
{
    public required byte SeatIndex { get; set; }

    /// <summary>Empty when the seat is unoccupied.</summary>
    public required string Name { get; set; }

    public required int Gold { get; set; }

    public required bool IsSittingOut { get; set; }

    public required bool HasFolded { get; set; }

    /// <summary>Gold this seat has committed to the current pot.</summary>
    public required int Committed { get; set; }

    /// <summary>The seat's most recent action this street, for the action log. Empty when it has not acted.</summary>
    public required string LastAction { get; set; }

    /// <summary>
    ///     Card indices, and empty for every seat but the recipient's own until showdown. This is the field
    ///     that leaks the whole game if it is ever populated for other seats early -- see
    ///     <see cref="PokerTableDisplayArgs" />.
    /// </summary>
    public IReadOnlyList<byte> HoleCards { get; set; } = [];
}

/// <summary>
///     Represents the serialization of the <see cref="ServerOpCode.PokerTableDisplay" /> packet.
/// </summary>
/// <remarks>
///     <b>Built per recipient, never broadcast.</b> A single shared snapshot sent to every seat would hand a
///     modified client every player's hole cards, and there is no recovering from shipping that once. The
///     server constructs one of these per seated player, populating
///     <see cref="PokerSeatEntry.HoleCards" /> only for that player -- and for everyone at showdown.
/// </remarks>
public sealed record PokerTableDisplayArgs : IPacketSerializable
{
    public required PokerDisplayType Type { get; set; }

    // --- Open ---
    public string? TableName { get; set; }
    public int SmallBet { get; set; }
    public int BigBet { get; set; }
    public int MinimumBuyIn { get; set; }

    // --- Snapshot ---
    public byte YourSeatIndex { get; set; }
    public byte ButtonIndex { get; set; }
    public byte Street { get; set; }
    public int Pot { get; set; }
    public byte ActorIndex { get; set; }

    /// <summary>Seconds left on the actor's shot clock.</summary>
    public byte SecondsRemaining { get; set; }

    /// <summary>Community card indices revealed so far: 0, 3, 4 or 5 entries.</summary>
    public List<byte>? Board { get; set; }

    public List<PokerSeatEntry>? Seats { get; set; }

    /// <summary>The actions the recipient may legally take right now, as <c>PokerAction</c> byte values.</summary>
    public List<byte>? LegalActions { get; set; }

    /// <summary>Human-readable line describing what just happened, for the table log.</summary>
    public string? EventText { get; set; }

    // --- Rejected ---
    public PokerRejectReason Reason { get; set; }
}
```

Create `Chaos-Server/Chaos.Networking/Entities/Client/PokerTableInteractionArgs.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Entities.Client;

/// <summary>
///     Represents the serialization of the <see cref="ClientOpCode.PokerTableInteraction" /> packet.
///     Deliberately carries no table id, seat index, or bet size -- the server resolves all three from the
///     seat claim it already holds, so a modified client has nothing to lie about.
/// </summary>
public sealed record PokerTableInteractionArgs : IPacketSerializable
{
    public required PokerInteractionType Type { get; set; }

    /// <summary>
    ///     The <c>PokerAction</c> being taken, when <see cref="Type" /> is
    ///     <see cref="PokerInteractionType.Act" />. Ignored otherwise.
    /// </summary>
    public byte Action { get; set; }
}
```

- [ ] **Step 5: Build**

```bash
dotnet build Chaos.slnx
```

Expected: success.

- [ ] **Step 6: Commit**

```bash
git add Chaos.Networking.Abstractions/Definitions/Enums.cs Chaos.DarkAges/Definitions/Enums.cs Chaos.Networking/Entities/
git commit -m "Add poker table packet opcodes, enums and argument records"
```

---

### Task 10: Packet converters

**Goal:** Serialize and deserialize both poker packets, round-tripping exactly.

**Files:**
- Create: `Chaos-Server/Chaos.Networking/Converters/Server/PokerTableDisplayConverter.cs`
- Create: `Chaos-Server/Chaos.Networking/Converters/Client/PokerTableInteractionConverter.cs`
- Create: `Chaos-Server/Tests/Chaos.Tests/Networking/PokerPacketConverterTests.cs`

**Acceptance Criteria:**
- [ ] `Open`, `Snapshot`, `Rejected` and `Close` all round-trip through serialize → deserialize unchanged
- [ ] A `Snapshot` with six seats, a five-card board and populated hole cards round-trips exactly
- [ ] A `Snapshot` where every seat has empty hole cards round-trips with them still empty
- [ ] `PokerTableInteractionArgs` round-trips for every `PokerInteractionType`
- [ ] Opcodes match Task 9

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/PokerPacketConverterTests/*" --no-ansi` → all pass

**Steps:**

- [ ] **Step 1: Read the existing converter you are mirroring**

```bash
sed -n 1,140p Chaos.Networking/Converters/Server/SlotMachineDisplayConverter.cs
cat Chaos.Networking/Converters/Client/SlotMachineInteractionConverter.cs
cat Tests/Chaos.Tests/Networking/SlotPacketConverterTests.cs
```

Match its structure exactly: `PacketConverterBase<T>`, an `OpCode` override, a `Deserialize(ref SpanReader)` switching on `Type`, and a `Serialize(ref SpanWriter, T)` doing the same. Match the existing test file's shape too — it is the template for Step 2.

- [ ] **Step 2: Write the failing round-trip tests**

Create `Chaos-Server/Tests/Chaos.Tests/Networking/PokerPacketConverterTests.cs`. One test per acceptance criterion. The seat-roster case, which is the one that catches length-prefix mistakes:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Converters.Server;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Tests.Networking;

public class PokerPacketConverterTests
{
    private static PokerTableDisplayArgs RoundTrip(PokerTableDisplayArgs args)
    {
        var converter = new PokerTableDisplayConverter();
        var buffer = new byte[8192];
        var writer = new SpanWriter(Encoding.ASCII, buffer);
        converter.Serialize(ref writer, args);

        var reader = new SpanReader(Encoding.ASCII, buffer.AsSpan(0, writer.Position));

        return converter.Deserialize(ref reader);
    }

    [Test]
    public async Task Snapshot_with_full_roster_and_board_round_trips()
    {
        var args = new PokerTableDisplayArgs
        {
            Type = PokerDisplayType.Snapshot,
            YourSeatIndex = 2,
            ButtonIndex = 0,
            Street = 3,
            Pot = 48_000,
            ActorIndex = 2,
            SecondsRemaining = 17,
            Board = [0, 13, 26, 39, 51],
            LegalActions = [0, 2, 4],
            EventText = "B raises",
            Seats = Enumerable.Range(0, 6)
                              .Select(i => new PokerSeatEntry
                              {
                                  SeatIndex = (byte)i,
                                  Name = $"Player{i}",
                                  Gold = 100_000 - (i * 1_000),
                                  IsSittingOut = i == 5,
                                  HasFolded = i == 4,
                                  Committed = i * 2_000,
                                  LastAction = i == 1 ? "raise" : string.Empty,
                                  HoleCards = i == 2 ? [7, 19] : []
                              })
                              .ToList()
        };

        var result = RoundTrip(args);

        result.Should()
              .BeEquivalentTo(args);

        await Task.CompletedTask;
    }
}
```

Add the remaining tests in the same shape: `Open_round_trips`, `Rejected_round_trips`, `Close_round_trips`, `Snapshot_with_no_hole_cards_round_trips`, and `Interaction_round_trips_for_every_type`. Use the exact `SpanWriter`/`SpanReader` construction the existing `SlotPacketConverterTests` uses — copy it rather than inventing one, since the encoding argument matters.

- [ ] **Step 3: Run tests to verify they fail**

```bash
dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/PokerPacketConverterTests/*" --no-ansi
```

Expected: build failure — the converters do not exist.

- [ ] **Step 4: Write the converters**

Create both files following `SlotMachineDisplayConverter` exactly. Rules that the tests enforce:

- Write `Type` first, then branch. Deserialize reads `Type` first and branches identically.
- Every variable-length collection is written as a **byte count followed by the elements**. `Board`, `LegalActions`, `Seats`, and each seat's `HoleCards` all follow this, so an empty collection writes a single `0` and reads back as empty rather than null.
- Strings use the same length-prefixed write the slot converter uses for `MachineName` and `ResultLabel`. Never write a null string — coalesce to `string.Empty`.
- `Serialize` for `Close` writes only `Type`.

- [ ] **Step 5: Run tests to verify they pass**

```bash
dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/PokerPacketConverterTests/*" --no-ansi
```

Expected: all pass. A failure in `Snapshot_with_no_hole_cards_round_trips` but not the full-roster test means an empty collection is being written as null and read back as null — fix the writer, not the test.

- [ ] **Step 6: Commit**

```bash
git add Chaos.Networking/Converters/ Tests/Chaos.Tests/Networking/PokerPacketConverterTests.cs
git commit -m "Add poker table packet converters with round-trip tests"
```

---

### Task 11: Server send methods and client handler

**Goal:** The server can push a snapshot to one player, and can receive an action from one player.

**Files:**
- Modify: `Chaos-Server/Chaos/Networking/Abstractions/IChaosWorldClient.cs`
- Modify: `Chaos-Server/Chaos/Networking/ChaosWorldClient.cs`
- Modify: `Chaos-Server/Chaos/Services/Servers/WorldServer.cs`

**Acceptance Criteria:**
- [ ] `IChaosWorldClient` gains `SendPokerTableOpen`, `SendPokerTableSnapshot`, `SendPokerRejected`, `SendPokerTableClose`
- [ ] `ChaosWorldClient` implements all four, mirroring the `SendSlotMachine*` methods
- [ ] `WorldServer` registers `ClientHandlers[(byte)ClientOpCode.PokerTableInteraction]`
- [ ] `OnPokerTableInteraction` resolves the table from the player's seat claim, never from the packet
- [ ] Solution builds

**Verify:** `dotnet build Chaos.slnx` → succeeds

**Steps:**

- [ ] **Step 1: Read the pattern you are mirroring**

```bash
sed -n 795,860p Chaos/Networking/ChaosWorldClient.cs
sed -n 170,215p Chaos/Networking/Abstractions/IChaosWorldClient.cs
grep -n "OnSlotMachineInteraction" Chaos/Services/Servers/WorldServer.cs
```

- [ ] **Step 2: Add the interface members**

In `Chaos/Networking/Abstractions/IChaosWorldClient.cs`, beside `SendSlotMachineClose()`:

```csharp
    /// <summary>Opens the poker panel for this player and sends the table's fixed properties.</summary>
    void SendPokerTableOpen(string tableName, int smallBet, int bigBet, int minimumBuyIn);

    /// <summary>
    ///     Sends one complete table snapshot. <paramref name="seats" /> MUST already have hole cards stripped
    ///     for every seat but this recipient's own (or be a showdown reveal) -- this method does no filtering.
    /// </summary>
    void SendPokerTableSnapshot(
        byte yourSeatIndex,
        byte buttonIndex,
        byte street,
        int pot,
        byte actorIndex,
        byte secondsRemaining,
        List<byte> board,
        List<PokerSeatEntry> seats,
        List<byte> legalActions,
        string eventText);

    void SendPokerRejected(PokerRejectReason reason);

    void SendPokerTableClose();
```

- [ ] **Step 3: Implement them**

In `Chaos/Networking/ChaosWorldClient.cs`, after `SendSlotMachineClose`:

```csharp
    /// <inheritdoc />
    public void SendPokerTableOpen(string tableName, int smallBet, int bigBet, int minimumBuyIn)
        => Send(
            new PokerTableDisplayArgs
            {
                Type = PokerDisplayType.Open,
                TableName = tableName,
                SmallBet = smallBet,
                BigBet = bigBet,
                MinimumBuyIn = minimumBuyIn
            });

    /// <inheritdoc />
    public void SendPokerTableSnapshot(
        byte yourSeatIndex,
        byte buttonIndex,
        byte street,
        int pot,
        byte actorIndex,
        byte secondsRemaining,
        List<byte> board,
        List<PokerSeatEntry> seats,
        List<byte> legalActions,
        string eventText)
        => Send(
            new PokerTableDisplayArgs
            {
                Type = PokerDisplayType.Snapshot,
                YourSeatIndex = yourSeatIndex,
                ButtonIndex = buttonIndex,
                Street = street,
                Pot = pot,
                ActorIndex = actorIndex,
                SecondsRemaining = secondsRemaining,
                Board = board,
                Seats = seats,
                LegalActions = legalActions,
                EventText = eventText
            });

    /// <inheritdoc />
    public void SendPokerRejected(PokerRejectReason reason)
        => Send(
            new PokerTableDisplayArgs
            {
                Type = PokerDisplayType.Rejected,
                Reason = reason
            });

    /// <inheritdoc />
    public void SendPokerTableClose()
        => Send(new PokerTableDisplayArgs { Type = PokerDisplayType.Close });
```

- [ ] **Step 4: Register and write the handler**

In `Chaos/Services/Servers/WorldServer.cs`, beside the slot registration:

```csharp
        ClientHandlers[(byte)ClientOpCode.PokerTableInteraction] = OnPokerTableInteraction;
```

Write `OnPokerTableInteraction` mirroring `OnSlotMachineInteraction`. It must:

1. Deserialize into `PokerTableInteractionArgs`.
2. Find the nearby `Merchant` running a `PokerTableScript` — **from the player's position and seat claim, never from the packet**, exactly as `OnSlotMachineInteraction` resolves the machine from the occupancy claim.
3. If no table is found or the player holds no seat, `client.SendPokerRejected(PokerRejectReason.NotSeated)` and return.
4. Otherwise dispatch to the script: `script.HandleInteraction(aisling, args.Type, (PokerAction)args.Action)`.

- [ ] **Step 5: Build**

```bash
dotnet build Chaos.slnx
```

Expected: success. `PokerTableScript` does not exist yet, so leave the dispatch call commented with a `//wired in Task 13` note if the build blocks, and uncomment it in Task 13.

- [ ] **Step 6: Commit**

```bash
git add Chaos/Networking/ Chaos/Services/Servers/WorldServer.cs
git commit -m "Add poker table send methods and client interaction handler"
```

---

### Task 12: Seat reactor

**Goal:** Walking onto a seat tile claims it and opens the panel; walking off releases it.

**Files:**
- Create: `Chaos-Server/Chaos/Scripting/ReactorTileScripts/Temauir/Casino/PokerSeatScript.cs`

**Acceptance Criteria:**
- [ ] Stepping onto the tile claims the seat and sends `Open` plus a first snapshot
- [ ] The claim is released from `Update` when the claimant is no longer standing on the tile
- [ ] The claim is released when the claimant dies or leaves the map
- [ ] **A disconnecting player is handled by the same path**: the aisling leaves the map, the next `Update` poll finds them absent, and `ReleaseSeat` folds them at their turn and forfeits their committed gold. There is no separate disconnect hook, and there must not be one — a second release path is a second chance to strand a seat.
- [ ] A tile already claimed by another player refuses a second claimant
- [ ] Seat index is derived from the tile's `scriptVars`, so six tiles map to six seats

**Verify:** `dotnet build Chaos.slnx` → succeeds; behaviour confirmed in Task 17's manual walkthrough

**Steps:**

- [ ] **Step 1: Read the correct model, and note the one to avoid**

```bash
cat Chaos/Scripting/ReactorTileScripts/Temauir/Casino/SlotMachineStoolScript.cs
```

`SlotMachineStoolScript` is the model. `IReactorTileScript` has **no walked-off event** — the engine only calls `OnWalkedOn` against reactors at a creature's *destination*. The stool solves this by polling in `Update` to see whether the claimant is still on the tile.

Do **not** model this on `CasinoLockTwentyOneScript`: it never releases its own lock, relying instead on a dialog to clear an `Aisling` flag. This table has no dialog and stores nothing on `Aisling`, so polling is the only correct release mechanism.

- [ ] **Step 2: Write the script**

Create `Chaos-Server/Chaos/Scripting/ReactorTileScripts/Temauir/Casino/PokerSeatScript.cs`:

```csharp
#region
using Chaos.Models.World;
using Chaos.Models.World.Abstractions;
using Chaos.Scripting.MerchantScripts.Casino;
using Chaos.Scripting.ReactorTileScripts.Abstractions;
#endregion

namespace Chaos.Scripting.ReactorTileScripts.Temauir.Casino;

/// <summary>
///     One seat at the poker table. Occupying the tile claims the seat; leaving releases it.
/// </summary>
/// <remarks>
///     Modelled on <see cref="SlotMachineStoolScript" />, not on <c>CasinoLockTwentyOneScript</c>.
///     <see cref="IReactorTileScript" /> has no "walked off" event -- the engine only calls
///     <c>OnWalkedOn</c> against reactors at a creature's destination -- so release is driven from
///     <see cref="Update" />, checking each tick whether the claimant is still standing here. The
///     twenty-one seat script never releases its own lock and leans on a dialog to clear an
///     <c>Aisling</c> flag; this table has no dialog and stores nothing on <c>Aisling</c>, so that
///     approach is not available and would strand seats besides.
/// </remarks>
public class PokerSeatScript : ConfigurableReactorTileScriptBase
{
    private const int TableSearchRange = 3;

    private Aisling? ClaimHolder;

    /// <summary>Which seat at the table this tile is. Set from the reactor's scriptVars.</summary>
    protected int SeatIndex { get; init; }

    public PokerSeatScript(ReactorTile subject)
        : base(subject) { }

    public override void OnWalkedOn(Creature source)
    {
        if (source is not Aisling aisling)
            return;

        if ((ClaimHolder is not null) && !ReferenceEquals(ClaimHolder, aisling))
            return;

        var table = FindTable();

        if (table is null)
            return;

        if (!table.TrySeat(aisling, SeatIndex))
            return;

        ClaimHolder = aisling;
    }

    public override void Update(TimeSpan delta)
    {
        if (ClaimHolder is null)
            return;

        var stillHere = Subject.MapInstance
                               .GetEntitiesAtPoints<Aisling>(Subject)
                               .Any(a => ReferenceEquals(a, ClaimHolder));

        if (stillHere && ClaimHolder.IsAlive)
            return;

        FindTable()
            ?.ReleaseSeat(SeatIndex);

        ClaimHolder.Client.SendPokerTableClose();
        ClaimHolder = null;
    }

    private PokerTableScript? FindTable()
        => Subject.MapInstance
                  .GetEntitiesWithinRange<Merchant>(Subject, TableSearchRange)
                  .Select(merchant => merchant.Script.As<PokerTableScript>())
                  .FirstOrDefault(script => script is not null);
}
```

Match the exact base class and `scriptVars` binding that `SlotMachineStoolScript` uses — if it derives from `ReactorTileScriptBase` rather than a configurable base, follow that and read `SeatIndex` the same way the surrounding scripts read their vars.

- [ ] **Step 3: Build**

```bash
dotnet build Chaos.slnx
```

Expected: success once Task 13 supplies `PokerTableScript.TrySeat` / `ReleaseSeat`. Build this task and Task 13 together if the compiler objects.

- [ ] **Step 4: Commit**

```bash
git add Chaos/Scripting/ReactorTileScripts/Temauir/Casino/PokerSeatScript.cs
git commit -m "Add poker seat reactor with poll-based release"
```

---

### Task 13: Table merchant script

**Goal:** Drive `PokerTable` from `Update` and broadcast **per-recipient** snapshots.

**Files:**
- Create: `Chaos-Server/Chaos/Scripting/MerchantScripts/Casino/PokerTableScript.cs`
- Create: `Chaos-Server/Chaos/Services/Poker/AislingSeatOccupant.cs`
- Modify: `Chaos-Server/Chaos/Services/Servers/WorldServer.cs` (uncomment the Task 11 dispatch)

**Acceptance Criteria:**
- [ ] `AislingSeatOccupant` adapts `Aisling` to `IPokerSeatOccupant` with no extra behaviour
- [ ] `TrySeat`, `ReleaseSeat` and `HandleInteraction` are the script's only public surface
- [ ] Every snapshot is **built separately for each seated player**, with other players' hole cards empty
- [ ] Hole cards are populated for all unfolded seats only once the hand reaches showdown
- [ ] Illegal or out-of-turn actions send `Rejected` and change no state
- [ ] Every completed hand logs hole cards, board, actions, pot, rake and winner via `logger.WithTopics`
- [ ] The table announces itself on the map when exactly one player is seated and waiting

**Verify:** `dotnet build Chaos.slnx` → succeeds; then `grep -n "SendPokerTableSnapshot" Chaos/Scripting/MerchantScripts/Casino/PokerTableScript.cs` shows the call inside a per-player loop, never outside one

**Steps:**

- [ ] **Step 1: Write the Aisling adapter**

Create `Chaos-Server/Chaos/Services/Poker/AislingSeatOccupant.cs`:

```csharp
using Chaos.Models.World;

namespace Chaos.Services.Poker;

/// <summary>
///     Adapts an <see cref="Aisling" /> to the poker engine's gold seam. Deliberately behaviourless -- every
///     rule about when gold may move lives in <see cref="PokerHand" />, which is the tested component.
/// </summary>
public sealed class AislingSeatOccupant(Aisling aisling) : IPokerSeatOccupant
{
    public Aisling Aisling { get; } = aisling;

    public uint Id => Aisling.Id;

    public string Name => Aisling.Name;

    public int Gold => Aisling.Gold;

    public bool TryTakeGold(int amount) => Aisling.TryTakeGold(amount);

    public bool TryGiveGold(int amount) => Aisling.TryGiveGold(amount);
}
```

- [ ] **Step 2: Write the table script**

Create `Chaos-Server/Chaos/Scripting/MerchantScripts/Casino/PokerTableScript.cs`, modelled on `SlotMachineScript` for DI, `ConfigurableMerchantScriptBase`, and `Update` shape.

The one method that carries the security requirement:

```csharp
    /// <summary>
    ///     Sends the current table state to every seated player, building a separate payload for each.
    /// </summary>
    /// <remarks>
    ///     The per-recipient loop is the security boundary, not an optimisation. Building one
    ///     <see cref="PokerSeatEntry" /> list and sending it to all seats would put every player's hole cards
    ///     on every client, where a modified client reads them straight out of the packet. There is no
    ///     recovering from shipping that once, so this method never constructs a shared roster: it builds one
    ///     per recipient, and <paramref name="revealAll" /> is set only at showdown.
    /// </remarks>
    private void BroadcastSnapshot(string eventText, bool revealAll = false)
    {
        foreach (var (seatIndex, occupant) in SeatedOccupants())
        {
            var seats = BuildRosterFor(seatIndex, revealAll);

            occupant.Aisling
                    .Client
                    .SendPokerTableSnapshot(
                        (byte)seatIndex,
                        (byte)Table.ButtonIndex,
                        (byte)(Table.CurrentHand?.Street ?? PokerStreet.Preflop),
                        Table.CurrentHand?.Pot ?? 0,
                        (byte)(Table.CurrentHand?.ActorIndex ?? 0),
                        (byte)SecondsRemaining,
                        BuildBoard(),
                        seats,
                        BuildLegalActionsFor(seatIndex),
                        eventText);
        }
    }

    /// <summary>
    ///     Builds the seat roster as <paramref name="viewerSeatIndex" /> is allowed to see it.
    /// </summary>
    private List<PokerSeatEntry> BuildRosterFor(int viewerSeatIndex, bool revealAll)
    {
        var entries = new List<PokerSeatEntry>();

        for (var i = 0; i < Table.Config.SeatCount; i++)
        {
            var occupant = Table.OccupantAt(i);
            var hand = Table.CurrentHand;

            //the ONLY place hole cards are allowed onto the wire
            var maySee = (i == viewerSeatIndex) || (revealAll && (hand is not null) && !hand.HasFolded(i));

            var holeCards = maySee && (hand is not null)
                ? hand.HoleCardsOf(i).Select(card => (byte)card.ToIndex()).ToList()
                : [];

            entries.Add(
                new PokerSeatEntry
                {
                    SeatIndex = (byte)i,
                    Name = occupant?.Name ?? string.Empty,
                    Gold = occupant?.Gold ?? 0,
                    IsSittingOut = Table.IsSittingOut(i),
                    HasFolded = hand?.HasFolded(i) ?? false,
                    Committed = hand?.CommittedBy(i) ?? 0,
                    LastAction = LastActionOf(i),
                    HoleCards = holeCards
                });
        }

        return entries;
    }
```

The rest of the script:

1. **`Update(TimeSpan delta)`** — call `Table.Update(delta)`, then `BroadcastSnapshot` whenever the table's state version changed. Track a version counter on `PokerTable` (increment it in `Act`, `Update` on timeout, seat changes, and hand start/end) so snapshots are not sent every tick.
2. **`TrySeat(Aisling aisling, int seatIndex)`** — wrap in `AislingSeatOccupant`, call `Table.TrySit`. On success send `SendPokerTableOpen` then `BroadcastSnapshot`. Return the result.
3. **`ReleaseSeat(int seatIndex)`** — `Table.Leave(seatIndex)`, then `BroadcastSnapshot`.
4. **`HandleInteraction(Aisling aisling, PokerInteractionType type, PokerAction action)`** — resolve the seat from `aisling.Id` against the table's occupants; on no match send `PokerRejectReason.NotSeated`. For `Act`, reject with `NotYourTurn` if the seat is not `CurrentHand.ActorIndex` and `IllegalAction` if `!IsLegal`. Otherwise apply and broadcast.
5. **Showdown.** When `Table.CurrentHand` transitions to complete *and* the hand reached showdown with two or more unfolded players, call `BroadcastSnapshot(text, revealAll: true)` before clearing.
6. **Hand logging.** On completion, log once with `logger.WithTopics(Topics.Entities.Aisling, Topics.Entities.Gold)` including every seat's hole cards, the board, the action sequence, pot, rake and winners. This log is the only thing that makes collusion reviewable after the fact.
7. **Waiting announcement.** When `Table.IsWaitingForPlayers` and exactly one seat is occupied, `Subject.Say` a "waiting for one more" line on a throttled timer — reuse the `IIntervalTimer` pattern from `CasinoLockTwentyOneScript`'s animation timer so it does not spam.

- [ ] **Step 3: Uncomment the WorldServer dispatch from Task 11**

- [ ] **Step 4: Build and verify the security invariant**

```bash
dotnet build Chaos.slnx
grep -n "HoleCards" Chaos/Scripting/MerchantScripts/Casino/PokerTableScript.cs
```

Expected: `HoleCards` is assigned in exactly one place — inside `BuildRosterFor`, guarded by `maySee`. Any other assignment is a leak.

- [ ] **Step 5: Commit**

```bash
git add Chaos/Scripting/MerchantScripts/Casino/PokerTableScript.cs Chaos/Services/Poker/AislingSeatOccupant.cs Chaos/Services/Servers/WorldServer.cs
git commit -m "Add poker table merchant script with per-recipient snapshots"
```

---

### Task 14: Client networking and view model

**Goal:** The client receives poker packets and holds authoritative table state.

**Files:**
- Modify: `Chaos.Client/Chaos.Client.Networking/Definitions/Delegates.cs`
- Modify: `Chaos.Client/Chaos.Client.Networking/ConnectionManager.cs`
- Create: `Chaos.Client/Chaos.Client/ViewModel/PokerTable.cs`
- Modify: `Chaos.Client/Chaos.Client/Collections/WorldState.cs`

**Acceptance Criteria:**
- [ ] `PokerTableDisplayHandler` delegate added beside `SlotMachineDisplayHandler`
- [ ] `ConnectionManager` exposes `OnPokerTableDisplay`, registers `PacketHandlers[(byte)ServerOpCode.PokerTableDisplay]`, and has `SendPokerAct`, `SendPokerLeave`, `SendPokerSitOut`, `SendPokerSitIn`, `SendPokerClose`
- [ ] `ViewModel/PokerTable.cs` mirrors `ViewModel/SlotMachine.cs`: `IsOpen`, `ApplyOpen`, `ApplySnapshot`, `Clear`
- [ ] `WorldState.PokerTable` registered and cleared alongside `SlotMachine` and `GildedSpindle`
- [ ] Client builds

**Verify:** `dotnet build Chaos.Client.sln` (or the client's solution file) → succeeds

**Steps:**

- [ ] **Step 1: Read the three files you are mirroring**

```bash
cd /c/Users/mikeb/Documents/GitHub/Chaos.Client
sed -n 325,340p Chaos.Client.Networking/Definitions/Delegates.cs
sed -n 505,520p Chaos.Client.Networking/ConnectionManager.cs
sed -n 1115,1140p Chaos.Client.Networking/ConnectionManager.cs
sed -n 1495,1505p Chaos.Client.Networking/ConnectionManager.cs
sed -n 1820,1832p Chaos.Client.Networking/ConnectionManager.cs
cat Chaos.Client/ViewModel/SlotMachine.cs
sed -n 135,155p Chaos.Client/Collections/WorldState.cs
sed -n 335,385p Chaos.Client/Collections/WorldState.cs
```

- [ ] **Step 2: Add the delegate**

In `Chaos.Client.Networking/Definitions/Delegates.cs`, beside `SlotMachineDisplayHandler`:

```csharp
public delegate void PokerTableDisplayHandler(PokerTableDisplayArgs args);
```

- [ ] **Step 3: Wire ConnectionManager**

Beside `public event SlotMachineDisplayHandler? OnSlotMachineDisplay;`:

```csharp
    public event PokerTableDisplayHandler? OnPokerTableDisplay;
```

Beside the slot send methods:

```csharp
    public void SendPokerAct(byte action)
        => SendIfWorld(new PokerTableInteractionArgs { Type = PokerInteractionType.Act, Action = action });

    public void SendPokerLeave() => SendIfWorld(new PokerTableInteractionArgs { Type = PokerInteractionType.Leave });

    public void SendPokerSitOut() => SendIfWorld(new PokerTableInteractionArgs { Type = PokerInteractionType.SitOut });

    public void SendPokerSitIn() => SendIfWorld(new PokerTableInteractionArgs { Type = PokerInteractionType.SitIn });

    public void SendPokerClose() => SendIfWorld(new PokerTableInteractionArgs { Type = PokerInteractionType.Close });
```

Beside the slot handler registration:

```csharp
        PacketHandlers[(byte)ServerOpCode.PokerTableDisplay] = HandlePokerTableDisplay;
```

And the handler itself, beside `HandleSlotMachineDisplay`:

```csharp
    private void HandlePokerTableDisplay(ServerPacket pkt)
    {
        var args = Client.Deserialize<PokerTableDisplayArgs>(in pkt);
        OnPokerTableDisplay?.Invoke(args);
    }
```

- [ ] **Step 4: Write the view model**

Create `Chaos.Client/ViewModel/PokerTable.cs` mirroring `SlotMachine.cs` — a plain state holder with private setters, updated only by `Apply*` methods:

```csharp
using Chaos.Networking.Entities.Server;

namespace Chaos.Client.ViewModel;

public sealed class PokerSeatInfo
{
    public required int SeatIndex { get; init; }
    public required string Name { get; init; }
    public required int Gold { get; init; }
    public required bool IsSittingOut { get; init; }
    public required bool HasFolded { get; init; }
    public required int Committed { get; init; }
    public required string LastAction { get; init; }

    /// <summary>
    ///     Card indices. Populated for the local player's own seat, and for everyone at showdown. Empty
    ///     otherwise -- the server never sends other players' cards early, so the client has nothing to hide.
    /// </summary>
    public IReadOnlyList<byte> HoleCards { get; init; } = [];
}

/// <summary>Authoritative poker table state, updated by server packets. Controls read this directly.</summary>
public sealed class PokerTable
{
    public bool IsOpen { get; private set; }
    public string TableName { get; private set; } = string.Empty;
    public int SmallBet { get; private set; }
    public int BigBet { get; private set; }
    public int MinimumBuyIn { get; private set; }
    public int YourSeatIndex { get; private set; }
    public int ButtonIndex { get; private set; }
    public int Street { get; private set; }
    public int Pot { get; private set; }
    public int ActorIndex { get; private set; }
    public int SecondsRemaining { get; private set; }
    public IReadOnlyList<byte> Board { get; private set; } = [];
    public IReadOnlyList<PokerSeatInfo> Seats { get; private set; } = [];
    public IReadOnlyList<byte> LegalActions { get; private set; } = [];
    public string EventText { get; private set; } = string.Empty;

    public void ApplyOpen(PokerTableDisplayArgs args)
    {
        IsOpen = true;
        TableName = args.TableName ?? string.Empty;
        SmallBet = args.SmallBet;
        BigBet = args.BigBet;
        MinimumBuyIn = args.MinimumBuyIn;
    }

    public void ApplySnapshot(PokerTableDisplayArgs args)
    {
        YourSeatIndex = args.YourSeatIndex;
        ButtonIndex = args.ButtonIndex;
        Street = args.Street;
        Pot = args.Pot;
        ActorIndex = args.ActorIndex;
        SecondsRemaining = args.SecondsRemaining;
        Board = args.Board ?? [];
        LegalActions = args.LegalActions ?? [];
        EventText = args.EventText ?? string.Empty;

        Seats = (args.Seats ?? [])
                .Select(seat => new PokerSeatInfo
                {
                    SeatIndex = seat.SeatIndex,
                    Name = seat.Name,
                    Gold = seat.Gold,
                    IsSittingOut = seat.IsSittingOut,
                    HasFolded = seat.HasFolded,
                    Committed = seat.Committed,
                    LastAction = seat.LastAction,
                    HoleCards = seat.HoleCards
                })
                .ToList();
    }

    public void Clear()
    {
        IsOpen = false;
        TableName = string.Empty;
        SmallBet = 0;
        BigBet = 0;
        MinimumBuyIn = 0;
        YourSeatIndex = 0;
        ButtonIndex = 0;
        Street = 0;
        Pot = 0;
        ActorIndex = 0;
        SecondsRemaining = 0;
        Board = [];
        Seats = [];
        LegalActions = [];
        EventText = string.Empty;
    }
}
```

- [ ] **Step 5: Register in WorldState**

Beside `public static SlotMachine SlotMachine { get; } = new();`:

```csharp
    public static PokerTable PokerTable { get; } = new();
```

And add `PokerTable.Clear();` at **both** places where `SlotMachine.Clear(); GildedSpindle.Clear();` appear (around lines 350 and 379). Note the existing comment there: `Clear()` resets the view model only and does **not** hide the panel — the same is true here, so panel visibility stays `PokerTableControl`'s job.

- [ ] **Step 6: Build**

```bash
dotnet build
```

Expected: success.

- [ ] **Step 7: Commit**

```bash
git add Chaos.Client.Networking/ Chaos.Client/ViewModel/PokerTable.cs Chaos.Client/Collections/WorldState.cs
git commit -m "Add poker table client networking and view model"
```

---

### Task 15: Poker table popup control

**Goal:** The panel players actually look at.

**Files:**
- Create: `Chaos.Client/Chaos.Client/Controls/World/Popups/Poker/PokerTableControl.cs`

**Acceptance Criteria:**
- [ ] Renders six seats with name, gold, committed amount, folded/sitting-out state, and last action
- [ ] Renders the community board and the pot
- [ ] Renders the local player's own hole cards, and everyone's at showdown
- [ ] Renders action buttons for exactly the actions in `LegalActions`, disabled otherwise
- [ ] Renders the shot clock counting down for whoever is to act
- [ ] Highlights the button seat and the seat to act
- [ ] Exposes `ActionRequested`, `LeaveRequested`, `SitOutRequested`, `SitInRequested` events
- [ ] `Show()`, `Hide()`, `OnSnapshot()`, `OnRejected(PokerRejectReason)` mirror `SlotMachineControl`'s surface

**Verify:** `dotnet build` → succeeds; visual confirmation in Task 17

**Steps:**

- [ ] **Step 1: Read the control you are mirroring**

```bash
sed -n 1,120p Chaos.Client/Controls/World/Popups/Slots/SlotMachineControl.cs
sed -n 300,330p Chaos.Client/Controls/World/Popups/Slots/SlotMachineControl.cs
sed -n 960,995p Chaos.Client/Controls/World/Popups/Slots/SlotMachineControl.cs
cat Chaos.Client/Controls/World/Popups/Wheel/StakeSelectorControl.cs
```

`SlotMachineControl` establishes the base class, the show/hide lifecycle, how it caches what it paints, and the `event Action? SpinRequested` pattern that `WorldScreen.Wiring` subscribes to. Follow all four.

- [ ] **Step 2: Write the control**

Create `Chaos.Client/Controls/World/Popups/Poker/PokerTableControl.cs` with this public surface. The base class and constructor shape are taken from `SlotMachineControl.cs:54,324` and `GildedSpindleControl.cs:62,236` — both derive from `FramedDialogPanelBase` and pass `("_nsett", false)` to it:

```csharp
namespace Chaos.Client.Controls.World.Popups.Poker;

public sealed class PokerTableControl : FramedDialogPanelBase
{
    public PokerTableControl(SoundSystem soundSystem)
        : base("_nsett", false)
    {
        ArgumentNullException.ThrowIfNull(soundSystem);

        SoundSystem = soundSystem;

        //derive PANEL_WIDTH and PANEL_HEIGHT from the content below rather than pinning a total --
        //see SlotMachineControl's remarks at line 58 for why that panel twice accumulated dead space
        //by doing it the other way round
    }

    /// <summary>Raised with a PokerAction byte when the player clicks an action button.</summary>
    public event Action<byte>? ActionRequested;

    public event Action? LeaveRequested;

    public event Action? SitOutRequested;

    public event Action? SitInRequested;

    public void Show();

    public void Hide();

    /// <summary>Repaints from <c>WorldState.PokerTable</c> after a Snapshot packet.</summary>
    public void OnSnapshot();

    public void OnRejected(PokerRejectReason reason);
}
```

Layout requirements:

- Six seat panels arranged around a table area. Each shows name, gold, committed chips, and dims when folded or sitting out.
- The board renders up to five cards centred above the pot readout.
- The local player's hole cards render at their own seat. At showdown, every unfolded seat's cards render — the client draws whatever `HoleCards` contains and never infers, because the server is the only thing that decides what may be seen.
- Action buttons: Fold, Check, Call, Bet, Raise. Enable a button only when its `PokerAction` byte appears in `WorldState.PokerTable.LegalActions`; render the rest greyed.
- The shot clock renders as a countdown on the acting seat, driven from `SecondsRemaining` and ticked down locally between snapshots.
- `EventText` renders as a one-line action log above the buttons.

Card sprites: reuse whatever sprite-loading path `ReelControl` uses for symbol sprites. Read it first —

```bash
sed -n 1,60p Chaos.Client/Controls/World/Popups/Slots/ReelControl.cs
```

— and follow the same renderer rather than introducing a second asset path.

- [ ] **Step 3: Build**

```bash
dotnet build
```

Expected: success.

- [ ] **Step 4: Commit**

```bash
git add Chaos.Client/Controls/World/Popups/Poker/PokerTableControl.cs
git commit -m "Add poker table popup control"
```

---

### Task 16: WorldScreen wiring

**Goal:** The control is constructed, wired to the connection, and dispatched to by the packet handler.

**Files:**
- Modify: `Chaos.Client/Chaos.Client/Screens/WorldScreen.cs`
- Modify: `Chaos.Client/Chaos.Client/Screens/WorldScreen.Wiring.cs`
- Modify: `Chaos.Client/Chaos.Client/Screens/WorldScreen.ServerHandlers.cs`

**Acceptance Criteria:**
- [ ] `PokerTableControl Poker` field declared and constructed alongside `Slots` and `Spindle`
- [ ] Control events send the matching `ConnectionManager` methods
- [ ] `HandlePokerTableDisplay` dispatches `Open` / `Snapshot` / `Rejected` / `Close`
- [ ] `Open` and `Snapshot` apply to `WorldState.PokerTable` **before** telling the control to repaint
- [ ] Client builds

**Verify:** `dotnet build` → succeeds

**Steps:**

- [ ] **Step 1: Declare and construct the control**

In `Screens/WorldScreen.cs`, beside `private SlotMachineControl Slots = null!;` (line ~164) and `private GildedSpindleControl Spindle = null!;` (line ~167):

```csharp
    private PokerTableControl Poker = null!;
```

And beside the constructions at ~754 and ~760:

```csharp
        Poker = new PokerTableControl(Game.SoundSystem)
        {
            //copy the object-initialiser properties (parent, anchor, visibility) that the Spindle
            //construction at line ~760 sets -- read that block and mirror it exactly
        };
```

- [ ] **Step 2: Wire the events**

In `Screens/WorldScreen.Wiring.cs`, beside line 169's `Slots.SpinRequested += () => Game.Connection.SendSlotSpin();`:

```csharp
        Poker.ActionRequested += action => Game.Connection.SendPokerAct(action);
        Poker.LeaveRequested += () => Game.Connection.SendPokerLeave();
        Poker.SitOutRequested += () => Game.Connection.SendPokerSitOut();
        Poker.SitInRequested += () => Game.Connection.SendPokerSitIn();
```

Also subscribe the packet event where `OnSlotMachineDisplay` is subscribed:

```csharp
        Game.Connection.OnPokerTableDisplay += HandlePokerTableDisplay;
```

- [ ] **Step 3: Write the dispatcher**

In `Screens/WorldScreen.ServerHandlers.cs`, beside `HandleSlotMachineDisplay` (line ~1424):

```csharp
    //--- poker ---

    /// <summary>
    ///     Dispatches a poker table display packet. Open and Snapshot apply to
    ///     <see cref="WorldState.PokerTable" /> (the authoritative state) first, then tell <see cref="Poker" />
    ///     to repaint from it -- the control reads the view model rather than the packet, so there is exactly
    ///     one copy of the truth.
    /// </summary>
    private void HandlePokerTableDisplay(PokerTableDisplayArgs args)
    {
        switch (args.Type)
        {
            case PokerDisplayType.Open:
                WorldState.PokerTable.ApplyOpen(args);
                Poker.Show();

                break;

            case PokerDisplayType.Snapshot:
                WorldState.PokerTable.ApplySnapshot(args);
                Poker.OnSnapshot();

                break;

            case PokerDisplayType.Rejected:
                Poker.OnRejected(args.Reason);

                break;

            case PokerDisplayType.Close:
                Poker.Hide();
                WorldState.PokerTable.Clear();

                break;
        }
    }
```

- [ ] **Step 4: Build**

```bash
dotnet build
```

Expected: success.

- [ ] **Step 5: Commit**

```bash
git add Chaos.Client/Screens/
git commit -m "Wire poker table control into WorldScreen"
```

---

### Task 17: Content, floor placement and end-to-end walkthrough

**Goal:** The table exists in Rucesion and a real hand can be played from the client.

**Files:**
- Create: `Unora/Data/LocalStorage/PokerTableCatalog.json`
- Create: `Unora/Data/Configuration/Templates/Merchants/Temauir/pokerTable.json`
- Modify: `Unora/Data/Configuration/MapInstances/Temuair/Towns/Rucesion/Rucesion_Casino/merchants.json`
- Modify: `Unora/Data/Configuration/MapInstances/Temuair/Towns/Rucesion/Rucesion_Casino/reactors.json`

**Acceptance Criteria:**
- [ ] Catalog carries the shipped configuration and passes `PokerConfigValidator` at startup
- [ ] Merchant template uses `scriptKeys: ["PokerTable"]`
- [ ] One merchant spawn added at a **new** floor position; the twenty-one spawns at `(9, 23)` and `(14, 23)` are unchanged
- [ ] Six `PokerSeat` reactor tiles added around the new table, each with its own `seatIndex`
- [ ] Existing `CasinoLockTwentyOne` reactors are untouched
- [ ] Two clients can sit, be dealt in, act through all four streets, and see the pot awarded

**Verify:** Server starts with no validation errors; two clients complete a hand; `git -C ../Unora diff --stat` shows only the four files above

**Steps:**

- [ ] **Step 1: Write the catalog**

Create `Unora/Data/LocalStorage/PokerTableCatalog.json`. Follow the `{ "default": { ... } }` envelope that `SlotMachineCatalog.json` and `WheelMachineCatalog.json` use — check one first:

```bash
head -c 300 /c/Users/mikeb/Documents/GitHub/Unora/Data/LocalStorage/WheelMachineCatalog.json
```

```json
{
  "default": {
    "tables": {
      "rucesionHoldem": {
        "tableName": "Rucesion Hold'em",
        "seatCount": 6,
        "smallBlind": 1000,
        "bigBlind": 2000,
        "smallBet": 2000,
        "bigBet": 4000,
        "maxBetsPerStreet": 4,
        "minimumBuyIn": 48000,
        "rakeRate": 0.05,
        "rakeCap": 12000,
        "actionTimeoutSeconds": 20,
        "timeoutsBeforeSitOut": 2,
        "sitOutHandsBeforeRelease": 3
      }
    }
  }
}
```

`minimumBuyIn` is `48000` because `maxBetsPerStreet × (2 × smallBet + 2 × bigBet) = 4 × (4000 + 8000) = 48000`. Changing any bet size without changing this number will fail startup validation — that is the guard working, not a bug.

- [ ] **Step 2: Write the merchant template**

Create `Unora/Data/Configuration/Templates/Merchants/Temauir/pokerTable.json`, matching the shape of `twentyOneTable.json`:

```json
{
  "itemsForSale": [],
  "itemsToBuy": [],
  "name": "Hold'em Table",
  "restockIntervalHrs": 1,
  "restockPct": 100,
  "scriptKeys": [
    "PokerTable"
  ],
  "scriptVars": {
    "PokerTable": {
      "tableKey": "rucesionHoldem"
    }
  },
  "skillsToTeach": [],
  "spellsToTeach": [],
  "sprite": 1310,
  "templateKey": "pokertable"
}
```

- [ ] **Step 3: Place the table and its seats**

Pick a clear floor area away from `(9, 23)` and `(14, 23)`. Append to `merchants.json`:

```json
  {
    "blackList": [],
    "direction": "Down",
    "extraScriptKeys": [],
    "merchantTemplateKey": "pokerTable",
    "spawnPoint": "(9, 27)"
  }
```

Append six seat reactors to `reactors.json`, matching the existing `{ scriptKeys, scriptVars, source }` shape:

```json
  {
    "scriptKeys": ["PokerSeat"],
    "scriptVars": { "PokerSeat": { "seatIndex": 0 } },
    "source": "(7, 28)"
  },
  {
    "scriptKeys": ["PokerSeat"],
    "scriptVars": { "PokerSeat": { "seatIndex": 1 } },
    "source": "(8, 28)"
  },
  {
    "scriptKeys": ["PokerSeat"],
    "scriptVars": { "PokerSeat": { "seatIndex": 2 } },
    "source": "(9, 28)"
  },
  {
    "scriptKeys": ["PokerSeat"],
    "scriptVars": { "PokerSeat": { "seatIndex": 3 } },
    "source": "(10, 28)"
  },
  {
    "scriptKeys": ["PokerSeat"],
    "scriptVars": { "PokerSeat": { "seatIndex": 4 } },
    "source": "(11, 28)"
  },
  {
    "scriptKeys": ["PokerSeat"],
    "scriptVars": { "PokerSeat": { "seatIndex": 5 } },
    "source": "(12, 28)"
  }
```

Confirm the chosen tiles are walkable and within the table's `TableSearchRange` of 3 from `(9, 27)`. Seats at `(7, 28)` and `(12, 28)` are 3 tiles away in Manhattan distance — if the map forces a wider spread, raise `TableSearchRange` in `PokerSeatScript` to match rather than moving seats out of reach.

- [ ] **Step 4: Confirm twenty-one is untouched**

```bash
cd /c/Users/mikeb/Documents/GitHub/Unora
git diff -- Data/Configuration/MapInstances/Temuair/Towns/Rucesion/Rucesion_Casino/merchants.json
git diff -- Data/Configuration/MapInstances/Temuair/Towns/Rucesion/Rucesion_Casino/reactors.json
```

Expected: additions only. No line touching `twentyOneTable`, `twentyOneTable2`, or `CasinoLockTwentyOne` may appear as a deletion or modification.

- [ ] **Step 5: Start the server and confirm validation**

```bash
cd /c/Users/mikeb/Documents/GitHub/Chaos.Client/Chaos-Server
dotnet run --project Chaos/Chaos.csproj
```

Expected: starts clean. If `PokerConfigValidator` reports an error, the catalog and `MaxExposure` disagree — fix the catalog, not the validator.

- [ ] **Step 6: Play a hand with two clients**

Launch two clients against the local server (see the run configuration in this repo's notes for host/port/data-path environment variables — the repo defaults are stale). Then, with two characters each holding more than 48,000 gold:

1. Walk both onto seat tiles. Expected: the panel opens for each, showing six seats with two occupied.
2. Wait for the deal. Expected: blinds posted, each client sees **only its own** hole cards.
3. Act through preflop, flop, turn and river.
4. At showdown, expected: both hands revealed, pot awarded, rake deducted.
5. Check the server log for the hand-history line from Task 13.
6. Walk one character off their seat mid-hand. Expected: they fold, forfeit committed gold, the panel closes, and the seat frees up.

- [ ] **Step 7: Verify no cards leaked**

While one client is mid-hand, confirm the other client's `WorldState.PokerTable.Seats` shows an **empty** `HoleCards` for every seat but its own. This is the single most important check in the plan: if hole cards are visible before showdown, stop and fix `BuildRosterFor` in Task 13 before going any further.

- [ ] **Step 8: Commit the content**

```bash
cd /c/Users/mikeb/Documents/GitHub/Unora
git add Data/LocalStorage/PokerTableCatalog.json Data/Configuration/Templates/Merchants/Temauir/pokerTable.json Data/Configuration/MapInstances/Temuair/Towns/Rucesion/Rucesion_Casino/
git commit -m "Add Hold'em table to the Rucesion casino floor"
```

- [ ] **Step 9: Commit the submodule pointer**

```bash
cd /c/Users/mikeb/Documents/GitHub/Chaos.Client
git add Chaos-Server
git commit -m "Advance Chaos-Server to the poker table implementation"
```

---

## Deferred (not in this plan)

- Bad-beat progressive jackpot over `ProgressivePot` / `IProgressivePotState`.
- A second table at different stakes.
- Retiring or repairing twenty-one, including the three bugs found while surveying it: integer division in the winnings split (`MerchantScripts/Casino/TwentyOneScript.cs:83`), a guaranteed loss for a solo non-busting player, and both tables gathering players from the whole map instance rather than their own seats.
