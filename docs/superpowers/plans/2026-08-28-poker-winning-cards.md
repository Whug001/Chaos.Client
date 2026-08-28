# Poker Winning Cards Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Hold the showdown on screen for 8 s, and frame the five cards that won (dimming the rest) on every client at the table.

**Architecture:** The server already decides the winner with `HandEvaluator.Evaluate`; a new `BestFive` picks the winning five by evaluating the 21 five-card subsets with that same function, `PokerHand` records them per winner at showdown, and `PokerSeatEntry` carries them (card index bytes) on the completion snapshot. The client's `CardView` gains an emphasis state (plain / winning / dimmed) painted at the end of its own `Draw`, set from the snapshot; no timers.

**Tech Stack:** C# 14 / .NET 10. Server tests: TUnit + FluentAssertions, run with `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/<Class>/*"` (**never** `dotnet test`). Client: MonoGame, no test project — `dotnet build Chaos.Client.slnx`. The client solution also builds the `Chaos` project, so a running local server locks the build output: stop it before building anything.

**User decisions (already made):**
- Reveal length: 8 seconds (`InterHandPause` constant; no catalog knob).
- Winning cards shown as gold outline on the five + dim on the other face-up cards. Not lines, not outline-only.
- Category-only text stays as is; this adds the cards, nothing else.

**Spec:** `docs/superpowers/specs/2026-08-28-poker-winning-cards-design.md`

---

## File structure

Server (`C:\Users\mikeb\Documents\GitHub\Chaos.Client\Chaos-Server`, branch `master`):
- `Chaos/Services/Poker/HandEvaluator.cs` — `BestFive`.
- `Chaos/Services/Poker/PokerHand.cs` — `WinningCardsOf`, recorded in `Showdown()`.
- `Chaos/Services/Poker/PokerTable.cs` — `InterHandPause` 8 s.
- `Chaos.Networking/Entities/Server/PokerTableDisplayArgs.cs`, `Chaos.Networking/Converters/Server/PokerTableDisplayConverter.cs` — `PokerSeatEntry.WinningCards`.
- `Chaos/Scripting/MerchantScripts/Casino/PokerTableScript.cs` — fills it in `BuildRosterFor`.
- Tests: `Tests/Chaos.Tests/Poker/HandEvaluatorTests.cs`, `PokerHandTests.cs`, `Tests/Chaos.Tests/Networking/PokerPacketConverterTests.cs`.

Client (`C:\Users\mikeb\Documents\GitHub\Chaos.Client`, branch `main`):
- `Chaos.Client/ViewModel/PokerTable.cs` — `PokerSeatInfo.WinningCards`.
- `Chaos.Client/Controls/World/Popups/Poker/PokerTableControl.cs` — `CardView.Emphasis`, `OnSnapshot` union, `SeatPanel.Apply`/`ApplyCards`.
- `docs/superpowers/plans/2026-08-27-poker-table-walkthrough.md` — Run I.

---

### Task 1: `HandEvaluator.BestFive`

**Goal:** A function that returns the five cards making the best hand out of five to seven, defined through the existing `Evaluate` so it cannot disagree with it.

**Files:**
- Modify: `Chaos/Services/Poker/HandEvaluator.cs`
- Test: `Tests/Chaos.Tests/Poker/HandEvaluatorTests.cs` (has a `Hand("AsKd…")` parser helper at the top)

**Acceptance Criteria:**
- [ ] `BestFive` of seven cards holding a full house returns exactly the full-house five.
- [ ] `BestFive` of six suited cards returns the five highest of the suit.
- [ ] Over 500 seeded random seven-card hands, `Evaluate(BestFive(cards)) == Evaluate(cards)`.
- [ ] Five in → the same five out; `< 5` or `> 7` throws `ArgumentException`.
- [ ] Tests watched to fail (compile with a stub that returns the first five) before the real implementation.

**Verify:** `cd C:\Users\mikeb\Documents\GitHub\Chaos.Client\Chaos-Server && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/HandEvaluatorTests/*"` → `Passed!`, `failed: 0`.

**Steps:**

- [ ] **Step 1: Add the tests**

Append to `HandEvaluatorTests` (uses the file's existing `Hand(string)` helper and `Card`/`Rank`/`Suit`/`HandCategory`):

```csharp
    [Test]
    public async Task BestFive_picks_the_full_house_out_of_seven()
    {
        //aces full of kings, with two junk cards that must be left out
        var cards = Hand("AsAdKcKhAh7c2d");

        var best = HandEvaluator.BestFive(cards);

        best.Should().HaveCount(5);
        best.Should().BeEquivalentTo(Hand("AsAdAhKcKh"));
        HandEvaluator.Evaluate(best).Category.Should().Be(HandCategory.FullHouse);

        await Task.CompletedTask;
    }

    [Test]
    public async Task BestFive_keeps_the_five_highest_of_a_six_card_flush()
    {
        var cards = Hand("As9s7s5s3s2sKd");

        var best = HandEvaluator.BestFive(cards);

        best.Should().BeEquivalentTo(Hand("As9s7s5s3s"));

        await Task.CompletedTask;
    }

    [Test]
    public async Task BestFive_returns_five_cards_unchanged()
    {
        var cards = Hand("2c5d9hJsKc");

        HandEvaluator.BestFive(cards)
                     .Should()
                     .Equal(cards);

        await Task.CompletedTask;
    }

    [Test]
    public async Task BestFive_never_disagrees_with_Evaluate()
    {
        //seeded so a failure is reproducible; 500 hands is enough to hit every category but the rarest
        var random = new Random(20260828);
        var deck = Enumerable.Range(0, 52)
                             .Select(Card.FromIndex)
                             .ToArray();

        for (var trial = 0; trial < 500; trial++)
        {
            var seven = deck.OrderBy(_ => random.Next())
                            .Take(7)
                            .ToArray();

            var best = HandEvaluator.BestFive(seven);

            best.Should().HaveCount(5);
            best.Should().OnlyContain(card => seven.Contains(card));
            best.Distinct().Should().HaveCount(5);
            HandEvaluator.Evaluate(best).Should().Be(HandEvaluator.Evaluate(seven), $"trial {trial}: {string.Join(' ', seven)}");
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task BestFive_rejects_the_wrong_number_of_cards()
    {
        var four = () => HandEvaluator.BestFive(Hand("2c5d9hJs"));
        var eight = () => HandEvaluator.BestFive(Hand("2c5d9hJsKcAd3h4s"));

        four.Should().Throw<ArgumentException>();
        eight.Should().Throw<ArgumentException>();

        await Task.CompletedTask;
    }
```

(If `Hand` returns `Card[]`, these compile as written; if it returns a `List<Card>`, call `.ToArray()` where a span is needed — `BestFive` takes `ReadOnlySpan<Card>`, and arrays convert implicitly.)

- [ ] **Step 2: Add a deliberately wrong stub so the tests compile, and watch them fail**

In `HandEvaluator`, after `Evaluate`:

```csharp
    /// <summary>
    ///     The five cards that make the best hand in <paramref name="cards" /> (five to seven of them).
    /// </summary>
    /// <remarks>
    ///     Defined through <see cref="Evaluate" />: every five-card subset is ranked with it and the highest kept,
    ///     first found on a tie. That is what makes the answer unable to disagree with the rank that decided the
    ///     pot -- there is no second evaluator to drift. Twenty-one subsets of seven, once per showdown winner.
    /// </remarks>
    public static Card[] BestFive(ReadOnlySpan<Card> cards)
    {
        if (cards.Length is < 5 or > 7)
            throw new ArgumentException($"Expected 5 to 7 cards, got {cards.Length}", nameof(cards));

        return cards[..5].ToArray(); //stub: replaced in Step 3
    }
```

Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/HandEvaluatorTests/BestFive*"`
Expected: `BestFive_picks_the_full_house_out_of_seven`, `…six_card_flush`, and `…never_disagrees_with_Evaluate` FAIL; `…returns_five_cards_unchanged` and `…rejects_the_wrong_number_of_cards` PASS.

- [ ] **Step 3: Real implementation**

Replace the stub body:

```csharp
    public static Card[] BestFive(ReadOnlySpan<Card> cards)
    {
        if (cards.Length is < 5 or > 7)
            throw new ArgumentException($"Expected 5 to 7 cards, got {cards.Length}", nameof(cards));

        if (cards.Length == 5)
            return cards.ToArray();

        Span<Card> candidate = stackalloc Card[5];
        var best = default(HandRank);
        Card[]? bestCards = null;

        //choose 5 of n by walking the index combinations in lexicographic order
        var n = cards.Length;

        for (var a = 0; a < n - 4; a++)
            for (var b = a + 1; b < n - 3; b++)
                for (var c = b + 1; c < n - 2; c++)
                    for (var d = c + 1; d < n - 1; d++)
                        for (var e = d + 1; e < n; e++)
                        {
                            candidate[0] = cards[a];
                            candidate[1] = cards[b];
                            candidate[2] = cards[c];
                            candidate[3] = cards[d];
                            candidate[4] = cards[e];

                            var rank = Evaluate(candidate);

                            if ((bestCards is null) || (rank > best))
                            {
                                best = rank;
                                bestCards = candidate.ToArray();
                            }
                        }

        return bestCards!;
    }
```

(`Card` is a readonly record struct; `stackalloc` of it is fine. If `Card` turns out to be a class, use `new Card[5]` instead.)

- [ ] **Step 4: Run the class**

Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/HandEvaluatorTests/*"`
Expected: `Passed!`, `failed: 0`.

- [ ] **Step 5: Commit**

```bash
git add Chaos/Services/Poker/HandEvaluator.cs Tests/Chaos.Tests/Poker/HandEvaluatorTests.cs
git commit -m "Name the five cards that make the best hand

Defined through Evaluate over every five-card subset, so it cannot disagree
with the rank that decided the pot. Sampled against Evaluate on 500 seeded
seven-card hands.

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01S8vbxZVquYHaZtdwfxiEef"
```

---

### Task 2: `PokerHand.WinningCardsOf` and the 8 s reveal

**Goal:** At showdown the hand records each winner's five; everyone else (and every seat in a fold win) has none. The settle pause becomes 8 s.

**Files:**
- Modify: `Chaos/Services/Poker/PokerHand.cs` (`Showdown()` ~line 383; fields near `WinnerSeats`; accessor near `HoleCardsOf` ~line 158)
- Modify: `Chaos/Services/Poker/PokerTable.cs:38` (`InterHandPause`)
- Test: `Tests/Chaos.Tests/Poker/PokerHandTests.cs`

**Acceptance Criteria:**
- [ ] After a showdown, the winner's `WinningCardsOf(i)` has 5 distinct cards, all from their hole cards + board, evaluating to `WinningCategory`; the loser's is empty.
- [ ] After a fold win, every seat's `WinningCardsOf` is empty.
- [ ] `PokerTable.InterHandPause == 8 s`.
- [ ] Showdown test watched to fail before `Showdown()` records the cards.

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/PokerHandTests|PokerTableTests/*"` → `Passed!`, `failed: 0`.

**Steps:**

- [ ] **Step 1: Tests** — append to `PokerHandTests` (same helpers as `A_showdown_records_the_winning_hands_category`):

```csharp
    [Test]
    public async Task A_showdown_records_each_winners_five_cards()
    {
        var a = new FakeOccupant(1, "A", 100_000);
        var b = new FakeOccupant(2, "B", 100_000);

        var hand = StartHand(a, b);
        hand.ForceBoardForTesting("AcKdKs9h4c");
        hand.ForceHoleCardsForTesting(0, "AsAd");
        hand.ForceHoleCardsForTesting(1, "2c7d");

        hand.Act(0, PokerAction.Call);
        hand.Act(1, PokerAction.Check);
        hand.Act(1, PokerAction.Check); //flop
        hand.Act(0, PokerAction.Check);
        hand.Act(1, PokerAction.Check); //turn
        hand.Act(0, PokerAction.Check);
        hand.Act(1, PokerAction.Check); //river
        hand.Act(0, PokerAction.Check);

        hand.Winners.Should().Equal(0);

        var winning = hand.WinningCardsOf(0);
        var available = hand.HoleCardsOf(0).Concat(hand.Board).ToList();

        winning.Should().HaveCount(5);
        winning.Distinct().Should().HaveCount(5);
        winning.Should().OnlyContain(card => available.Contains(card));
        HandEvaluator.Evaluate(winning.ToArray()).Category.Should().Be(HandCategory.FullHouse);

        hand.WinningCardsOf(1).Should().BeEmpty("the loser has no winning cards");

        await Task.CompletedTask;
    }

    [Test]
    public async Task A_hand_won_by_folding_records_no_winning_cards()
    {
        var a = new FakeOccupant(1, "A", 100_000);
        var b = new FakeOccupant(2, "B", 100_000);

        var hand = StartHand(a, b);
        hand.Act(0, PokerAction.Fold);

        hand.Winners.Should().Equal(1);
        hand.WinningCardsOf(0).Should().BeEmpty();
        hand.WinningCardsOf(1).Should().BeEmpty();

        await Task.CompletedTask;
    }
```

(`hand.Board` is the existing public board accessor — the file's `Rake_is_taken…` test uses `hand.Board`; if the name differs, use whatever `PokerHand` exposes for the community cards.)

- [ ] **Step 2: Accessor + storage (so it compiles), run, watch the showdown test fail**

In `PokerHand`, next to `HoleCards`' declaration add `private readonly Card[][] WinningCards;` and in the constructor where `HoleCards` is sized, `WinningCards = new Card[players.Length][]; Array.Fill(WinningCards, Array.Empty<Card>());` (place it directly after the `HoleCards` initialisation; if `HoleCards` is a jagged array sized `players.Length`, mirror that exactly). After `HoleCardsOf`:

```csharp
    /// <summary>
    ///     The five cards that won the pot for <paramref name="seatIndex" /> at showdown, or empty: for every
    ///     seat that did not win, and for every seat when the hand ended because everyone else folded.
    /// </summary>
    public IReadOnlyList<Card> WinningCardsOf(int seatIndex) => WinningCards[seatIndex];
```

Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/PokerHandTests/A_showdown_records_each_winners_five_cards"`
Expected: FAIL — `Expected winning to contain 5 item(s), but found 0`.

- [ ] **Step 3: Record in `Showdown()`**

`Showdown()` currently evaluates `Card[] cards = [..HoleCards[seat], ..BoardCards];` per seat and collects `winners`. After the loop, before `WinningCategory = best.Category;`:

```csharp
        foreach (var seat in winners)
            WinningCards[seat] = HandEvaluator.BestFive([..HoleCards[seat], ..BoardCards]);
```

- [ ] **Step 4: The pause** — `PokerTable.cs:38`: `TimeSpan.FromMilliseconds(4000)` → `TimeSpan.FromMilliseconds(8000)`, and update the sentence in its XML doc that mentions four seconds, if any (grep `4 s|four|4000` around it).

- [ ] **Step 5: Run** the verify command → `Passed!`, `failed: 0`.

- [ ] **Step 6: Commit**

```bash
git add Chaos/Services/Poker/PokerHand.cs Chaos/Services/Poker/PokerTable.cs Tests/Chaos.Tests/Poker/PokerHandTests.cs
git commit -m "Record each winner's five cards, and hold the showdown for eight seconds

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01S8vbxZVquYHaZtdwfxiEef"
```

---

### Task 3: `PokerSeatEntry.WinningCards` on the wire, filled by the script

**Goal:** Each seat entry on the completion snapshot carries that seat's winning five (empty for non-winners, fold wins, and running hands).

**Files:**
- Modify: `Chaos.Networking/Entities/Server/PokerTableDisplayArgs.cs` (`PokerSeatEntry`, after `HoleCards`)
- Modify: `Chaos.Networking/Converters/Server/PokerTableDisplayConverter.cs` (seat loop in both directions, after the hole-card loop)
- Modify: `Chaos/Scripting/MerchantScripts/Casino/PokerTableScript.cs` (`BuildRosterFor`, the `new PokerSeatEntry { … }` initializer ~line 980)
- Test: `Tests/Chaos.Tests/Networking/PokerPacketConverterTests.cs`

**Acceptance Criteria:**
- [ ] Full-roster round-trip: seat 2 carries `WinningCards = [7, 19, 26, 39, 51]`, others `[]`, `BeEquivalentTo` passes.
- [ ] The no-hole-cards and null-collections tests still pass (a seat with no `WinningCards` round-trips as `[]`).
- [ ] Round-trip watched to fail before the converter carried the field.
- [ ] `BuildRosterFor` sets it only when `hand is { IsComplete: true }` and the seat is in the hand.

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.Poker|Chaos.Tests.Networking/*/*"` → `Passed!`, `failed: 0`.

**Steps:**

- [ ] **Step 1: Field + tests.** In `PokerSeatEntry` after `HoleCards`:

```csharp
    /// <summary>
    ///     The five cards that won this seat the pot at showdown, as card indices. Empty for every seat that did
    ///     not win, for every seat when the hand was won by folds, and while a hand is running.
    /// </summary>
    /// <remarks>
    ///     Only ever cards the recipient can already see -- the winner's revealed hole cards and the board -- so
    ///     it discloses nothing <see cref="HoleCards" /> did not.
    /// </remarks>
    public IReadOnlyList<byte> WinningCards { get; set; } = [];
```

In `Snapshot_with_full_roster_and_board_round_trips`, inside the `new PokerSeatEntry { … }` lambda add `WinningCards = i == 2 ? [7, 19, 26, 39, 51] : [],` after `HoleCards = …`.

- [ ] **Step 2: RED** — run `--treenode-filter "/*/*/PokerPacketConverterTests/*"`; expect the full-roster test to fail on `Seats[2].WinningCards` (5 expected, 0 found).

- [ ] **Step 3: Converter.** Deserialize, after the hole-card loop inside the seat loop:

```csharp
                    var winningCardCount = reader.ReadByte();
                    var winningCards = new List<byte>(winningCardCount);

                    for (var w = 0; w < winningCardCount; w++)
                        winningCards.Add(reader.ReadByte());
```
and `WinningCards = winningCards` in the entry initializer after `HoleCards = holeCards`. Serialize, after the hole-card loop:

```csharp
                    var winningCards = seat.WinningCards;
                    writer.WriteByte(CountToByte(winningCards.Count, "seat winning cards"));

                    foreach (var card in winningCards)
                        writer.WriteByte(card);
```

- [ ] **Step 4: Script.** In `BuildRosterFor`'s entry initializer after `HoleCards = …`:

```csharp
                    //empty unless this seat won at showdown -- WinningCardsOf already answers empty for losers and
                    //fold wins, so completion is the only gate this needs
                    WinningCards = (hand is { IsComplete: true }) && inHand
                        ? hand.WinningCardsOf(handIndex)
                              .Select(card => (byte)card.ToIndex())
                              .ToList()
                        : []
```

- [ ] **Step 5: Verify** → `Passed!`, `failed: 0`. **Step 6: Commit**

```bash
git add Chaos.Networking/Entities/Server/PokerTableDisplayArgs.cs Chaos.Networking/Converters/Server/PokerTableDisplayConverter.cs Chaos/Scripting/MerchantScripts/Casino/PokerTableScript.cs Tests/Chaos.Tests/Networking/PokerPacketConverterTests.cs
git commit -m "Send each winner's five cards on the completion snapshot

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01S8vbxZVquYHaZtdwfxiEef"
```

---

### Task 4: Client — frame the five, dim the rest

**Goal:** During the reveal, every face-up card in the union of the winners' `WinningCards` wears a 2 px gold frame and every other face-up card is dimmed; outside the reveal all cards are plain.

**Files:**
- Modify: `Chaos.Client/ViewModel/PokerTable.cs` (`PokerSeatInfo` + `ApplySnapshot`)
- Modify: `Chaos.Client/Controls/World/Popups/Poker/PokerTableControl.cs` — `CardView` (fields ~line 2060, `ShowFace`/`ShowBack`/`ShowNothing` ~2287-2445, `Draw`), `OnSnapshot` (board loop ~1079 and the `Apply` call), `SeatPanel.Apply` (~1790) and `ApplyCards` (~1949)

**Acceptance Criteria:**
- [ ] `PokerSeatInfo.WinningCards` mirrors the packet.
- [ ] `CardView.Emphasis` ∈ {Plain, Winning, Dimmed}; `Winning` paints a 2 px `WinnerSeatColor` frame after the face; `Dimmed` paints `Color.Black * 0.55f` over the face; `ShowBack`/`ShowNothing` reset to Plain.
- [ ] `OnSnapshot`: union of all seats' `WinningCards`; empty → all Plain; else board and face-up hole cards are Winning if in the set, Dimmed otherwise.
- [ ] Build succeeds.

**Verify:** `cd C:\Users\mikeb\Documents\GitHub\Chaos.Client && dotnet build Chaos.Client.slnx -v q --nologo 2>&1 | grep -E "error CS|Build succeeded"` → `Build succeeded.` (stop the local server first — it locks the `Chaos` project output the solution rebuilds).

**Steps:**

- [ ] **Step 1: View model.** In `PokerSeatInfo` after `HoleCards`: `public IReadOnlyList<byte> WinningCards { get; init; } = [];` (with a one-line `<summary>`: "The five cards that won this seat the pot at showdown; empty otherwise."). In `ApplySnapshot`'s seat projection after `HoleCards = seat.HoleCards.ToList()`: `WinningCards = seat.WinningCards.ToList()`.

- [ ] **Step 2: `CardView` emphasis.** Inside `CardView` add, next to `FaceUp`:

```csharp
        /// <summary>How a face-up card is drawn during the reveal: framed as one of the winning five, dimmed as not, or plainly.</summary>
        public enum CardEmphasis
        {
            Plain,
            Winning,
            Dimmed
        }

        private static readonly Color DimColor = Color.Black * 0.55f;
        private const int WIN_FRAME = 2;

        public CardEmphasis Emphasis { get; set; }
```

In `ShowBack()` and `ShowNothing()` set `Emphasis = CardEmphasis.Plain;` (for `ShowNothing`, change the expression body to a block). Leave `ShowFace` alone — the caller sets `Emphasis` right after it.

At the very end of `CardView.Draw` (after the last pip draw), add:

```csharp
            switch (Emphasis)
            {
                case CardEmphasis.Winning:
                    DrawRectClipped(spriteBatch, new Rectangle(ScreenX, ScreenY, WIDTH, WIN_FRAME), WinnerSeatColor);
                    DrawRectClipped(spriteBatch, new Rectangle(ScreenX, ScreenY + HEIGHT - WIN_FRAME, WIDTH, WIN_FRAME), WinnerSeatColor);
                    DrawRectClipped(spriteBatch, new Rectangle(ScreenX, ScreenY, WIN_FRAME, HEIGHT), WinnerSeatColor);
                    DrawRectClipped(spriteBatch, new Rectangle(ScreenX + WIDTH - WIN_FRAME, ScreenY, WIN_FRAME, HEIGHT), WinnerSeatColor);

                    break;
                case CardEmphasis.Dimmed:
                    DrawRectClipped(spriteBatch, new Rectangle(ScreenX, ScreenY, WIDTH, HEIGHT), DimColor);

                    break;
            }
```

(`DrawRectClipped` is the `UIElement` helper `UIPanel.Draw` uses for its own border; `WinnerSeatColor` is the outer control's static field — `CardView` is a nested class, so it is in scope. If `DrawRectClipped` is `private` on `UIElement`, make it `protected` — it is a one-word change and the panel's own border code is the precedent.)

- [ ] **Step 3: `OnSnapshot`.** Before the board loop, build the set:

```csharp
        //the union of every winner's five: on a split, both winners' hole cards and the board cards they share
        var winning = new HashSet<byte>();

        foreach (var seatInfo in vm.Seats)
            foreach (var card in seatInfo.WinningCards)
                winning.Add(card);
```

Change the board loop's face branch to:

```csharp
            if (i < board.Count)
            {
                BoardCards[i]
                    .ShowFace(board[i]);

                BoardCards[i].Emphasis = EmphasisFor(board[i], winning);
            } else
```

and add next to `SeatAnchor`:

```csharp
    /// <summary>Framed if it is one of the winning five, dimmed if there is a winning five it is not part of, plain when there is no reveal.</summary>
    private static CardView.CardEmphasis EmphasisFor(byte cardIndex, IReadOnlySet<byte> winning)
        => winning.Count == 0
            ? CardView.CardEmphasis.Plain
            : winning.Contains(cardIndex)
                ? CardView.CardEmphasis.Winning
                : CardView.CardEmphasis.Dimmed;
```

Pass the set into the seat panels: add a final parameter `IReadOnlySet<byte> winningCards` to `SeatPanel.Apply(...)` (with a `<param>` doc), thread it to `ApplyCards(seat, inactive, handInProgress, winningCards)`, and in `ApplyCards`' face branch set `Cards[i].Emphasis = EmphasisFor(cards[i], winningCards);` right after `ShowFace`. `EmphasisFor` is `private static` on the outer class, so the nested `SeatPanel` can call it. Update the single `.Apply(` call site in `OnSnapshot` to pass `winning`.

- [ ] **Step 4: Build** (server stopped) → `Build succeeded.` **Step 5: Commit** (client repo, `git -c core.commitGraph=false commit`):

```
Frame the five cards that won, and dim the rest

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01S8vbxZVquYHaZtdwfxiEef
```

---

### Task 5: Pin, Run I, live

**Goal:** Client `main` pins the server commit from Task 3; the walkthrough gains Run I; server and two clients boot clean on the new builds.

**Files:**
- Modify: `Chaos-Server` (pointer), `docs/superpowers/plans/2026-08-27-poker-table-walkthrough.md` (insert before the `---` preceding `## 4. Known, already triaged`)

**Acceptance Criteria:**
- [ ] `git ls-tree HEAD Chaos-Server` equals `git -C Chaos-Server rev-parse HEAD` (Task 3's commit on `master`).
- [ ] Run I present.
- [ ] Server log shows `Listening on "0.0.0.0:4202"`, zero `"Level":"Error"`; two clients titled `Unora` connected to 4201.

**Verify:** `git ls-tree HEAD Chaos-Server && git -C Chaos-Server rev-parse HEAD` → same hash.

**Steps:**

- [ ] **Step 1: Run I** (insert before the `---` that precedes `## 4.`):

```markdown
### Run I — the winning five

1. **Showdown.** Check a hand down to the river. For the ~8 s the result stays up, exactly five
   cards on the felt must wear a gold frame — the winner's two hole cards on their plaque plus
   three board cards, or fewer hole cards and more board cards when the board plays — and every
   other face-up card must be dimmed. Count them: five framed, never more.
2. **Split pot.** If you can force a tie (both play the board), the five board cards frame once
   and both plaques' hole cards are dimmed; if the hole cards play for both, each plaque frames
   its own.
3. **Fold win.** Nothing frames, nothing dims.
4. **Next deal** clears both, and the pause before it is noticeably longer than before (8 s).
```

- [ ] **Step 2: Boot.** Stop any running server/clients (`Get-Process Chaos, Chaos.Client | Stop-Process -Force`). From `Chaos-Server/Chaos`: `dotnet run -c Debug` with output redirected to a file (never piped through `head`), wait for `Listening on "0.0.0.0:4202"` in `Chaos/bin/Debug/net10.0/logs/<today>.log`, confirm `grep -c '"Level":"Error"'` is 0. Launch two clients with the walkthrough's PowerShell block (`DA_LOBBY_HOST=127.0.0.1`, `DA_LOBBY_PORT=4200`, `DA_PATH=C:\Users\mikeb\Documents\Chaos\Unora`). Leave everything running.

- [ ] **Step 3: Pin + commit** (client repo):

```bash
git add Chaos-Server docs/superpowers/plans/2026-08-27-poker-table-walkthrough.md
git -c core.commitGraph=false commit -m "Pin the server that names the winning five, and add Run I

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01S8vbxZVquYHaZtdwfxiEef"
```

---

## Self-review

- **Spec coverage:** BestFive → T1; WinningCardsOf + 8 s → T2; wire + script → T3; client emphasis → T4; Run I + pin + live → T5.
- **Placeholders:** none.
- **Type consistency:** `BestFive(ReadOnlySpan<Card>) → Card[]`; `WinningCardsOf(int) → IReadOnlyList<Card>`; wire `IReadOnlyList<byte> WinningCards` (default `[]`, same shape as `HoleCards`); view model `IReadOnlyList<byte>`; `EmphasisFor(byte, IReadOnlySet<byte>)`; `SeatPanel.Apply(…, IReadOnlySet<byte> winningCards)`; `ApplyCards(seat, inactive, handInProgress, winningCards)`.
