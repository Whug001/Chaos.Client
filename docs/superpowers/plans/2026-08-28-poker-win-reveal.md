# Poker Win Reveal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When a poker hand ends, show *why* it was won (the hand category, or "everyone else folded") under the pot, and slide the pot's coins to each winner's plaque.

**Architecture:** The server is the only party that knows who won and with what, so the completion snapshot gains two fields (`WinnerSeats`, `WinningHand`) filled from a new `PokerHand.WinningCategory`. The client view model copies them; the panel paints a second, gold line under the existing event label and runs the existing `ChipSlide` coin animation in reverse (pot → plaque) with a per-coin delay. Nothing is inferred from text.

**Tech Stack:** C# 14 / .NET 10. Server: `Chaos-Server` submodule (branch `feature/poker-table`), TUnit + FluentAssertions tests run with `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/<Class>/*"` (**not** `dotnet test`, which silently runs nothing on this SDK). Client: `Chaos.Client` MonoGame app, no test project — verified by `dotnet build Chaos.Client.slnx` and the two live clients.

**User decisions (already made):**
- Approach A: server sends who won and with what; no client-side derivation.
- Detail level: category only ("Full House", …, or "everyone else folded"). No high-card detail, no winning-five-cards highlight.
- Banner lives under the pot (not over the board); animation reuses the existing coin slide reversed; winner's plaque border flashes gold; no sound.

**Spec:** `docs/superpowers/specs/2026-08-28-poker-win-reveal-design.md`

---

## File structure

Server (`C:\Users\mikeb\Documents\GitHub\Chaos.Client\Chaos-Server`):
- `Chaos/Services/Poker/PokerHand.cs` — records `WinningCategory` at showdown.
- `Chaos.Networking/Entities/Server/PokerTableDisplayArgs.cs` — two new snapshot fields.
- `Chaos.Networking/Converters/Server/PokerTableDisplayConverter.cs` — serialises them.
- `Chaos/Scripting/MerchantScripts/Casino/PokerTableScript.cs` — fills them on the completion snapshot.
- `Tests/Chaos.Tests/Poker/PokerHandTests.cs`, `Tests/Chaos.Tests/Networking/PokerPacketConverterTests.cs` — tests.

Client (`C:\Users\mikeb\Documents\GitHub\Chaos.Client`):
- `Chaos.Client/ViewModel/PokerTable.cs` — copies the two fields.
- `Chaos.Client/Controls/World/Popups/Poker/PokerTableControl.cs` — banner line, pot slide, winner outline.
- `docs/superpowers/plans/2026-08-27-poker-table-walkthrough.md` — Run H.

All commands below assume the working directory named in the task.

---

### Task 1: `PokerHand.WinningCategory`

**Goal:** The hand remembers the category of the winning rank at showdown, and `null` when it was won by everyone else folding.

**Files:**
- Modify: `Chaos/Services/Poker/PokerHand.cs` (property near `Winners` at ~line 80; `Showdown()` at ~373-396)
- Test: `Tests/Chaos.Tests/Poker/PokerHandTests.cs`

**Acceptance Criteria:**
- [ ] A hand that reaches showdown exposes `WinningCategory == HandCategory.<best>`.
- [ ] A hand settled by `Forfeit` exposes `WinningCategory == null`.
- [ ] Both tests were watched to fail before the property existed / was set.

**Verify:** `cd C:\Users\mikeb\Documents\GitHub\Chaos.Client\Chaos-Server && dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/PokerHandTests/*"` → `Passed!`, `failed: 0`.

**Steps:**

- [ ] **Step 1: Write the failing tests**

Add to `PokerHandTests` (next to `Rake_is_taken_before_the_pot_is_split`, which is the template for driving a hand to showdown):

```csharp
    [Test]
    public async Task A_showdown_records_the_winning_hands_category()
    {
        var a = new FakeOccupant(1, "A", 100_000);
        var b = new FakeOccupant(2, "B", 100_000);

        var hand = StartHand(a, b);

        //A holds aces over the board's pair of kings: a full house, aces full of kings
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

        hand.IsComplete.Should().BeTrue();
        hand.Winners.Should().Equal(0);
        hand.WinningCategory.Should().Be(HandCategory.FullHouse);

        await Task.CompletedTask;
    }

    [Test]
    public async Task A_hand_won_by_everyone_else_folding_has_no_winning_category()
    {
        var a = new FakeOccupant(1, "A", 100_000);
        var b = new FakeOccupant(2, "B", 100_000);

        var hand = StartHand(a, b);

        //heads-up the button acts first preflop; a fold ends it there
        hand.Act(0, PokerAction.Fold);

        hand.IsComplete.Should().BeTrue();
        hand.Winners.Should().Equal(1);
        hand.WinningCategory.Should().BeNull();

        await Task.CompletedTask;
    }
```

- [ ] **Step 2: Add the property (declaration only) so the tests compile, then run them to see them fail**

In `PokerHand.cs`, directly after `public IReadOnlyList<int> Winners => WinnerSeats;`:

```csharp
    /// <summary>
    ///     The category of the hand that won at showdown, or null when the hand was won by everyone else
    ///     folding and no cards were ever compared.
    /// </summary>
    /// <remarks>
    ///     Recorded so the table can tell the players <i>why</i> the pot went where it did. Category only:
    ///     the tiebreak is not decoded back into card ranks here.
    /// </remarks>
    public HandCategory? WinningCategory { get; private set; }
```

Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/PokerHandTests/A_showdown_records_the_winning_hands_category"`
Expected: FAIL — `Expected hand.WinningCategory to be FullHouse, but found <null>.`

Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/PokerHandTests/A_hand_won_by_everyone_else_folding_has_no_winning_category"`
Expected: PASS already (null is the default) — that is fine; it pins the fold path so Step 3 cannot regress it.

- [ ] **Step 3: Record the category in `Showdown()`**

In `PokerHand.Showdown()`, just before `return winners;`:

```csharp
        WinningCategory = best.Category;

        return winners;
```

(`best` is the `HandRank` local the loop already keeps.)

- [ ] **Step 4: Run the whole class**

Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/PokerHandTests/*"`
Expected: `Passed!`, `failed: 0`.

- [ ] **Step 5: Commit**

```bash
git add Chaos/Services/Poker/PokerHand.cs Tests/Chaos.Tests/Poker/PokerHandTests.cs
git commit -m "Remember the winning hand's category at showdown

The table is about to tell players why a pot went where it did. Category
only; the tiebreak is not decoded. Null when everyone else folded.

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01S8vbxZVquYHaZtdwfxiEef"
```

---

### Task 2: `WinnerSeats` and `WinningHand` on the wire, filled by the script

**Goal:** The completion snapshot carries the winning seats (seat space) and the winning category byte; every other snapshot carries an empty list and `0`.

**Files:**
- Modify: `Chaos.Networking/Entities/Server/PokerTableDisplayArgs.cs` (after `EventText`)
- Modify: `Chaos.Networking/Converters/Server/PokerTableDisplayConverter.cs` (Snapshot branches of `Deserialize` and `Serialize`, right after `EventText`)
- Modify: `Chaos/Scripting/MerchantScripts/Casino/PokerTableScript.cs` (`BroadcastSnapshot`, ~line 909-947)
- Test: `Tests/Chaos.Tests/Networking/PokerPacketConverterTests.cs`

**Acceptance Criteria:**
- [ ] `Snapshot_with_full_roster_and_board_round_trips` sets `WinnerSeats = [2, 4]` and `WinningHand = 7` and still passes `BeEquivalentTo`.
- [ ] `Snapshot_with_null_collections_round_trips_as_empty` leaves `WinnerSeats` null and round-trips it as `[]` (extend its existing expectation the same way it handles `Board`).
- [ ] The round-trip test was watched to fail (`WinnerSeats` expected 2 items, found 0) before the converter carried the fields.
- [ ] `BroadcastSnapshot` sets both only when `hand is { IsComplete: true }`.

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.Poker|Chaos.Tests.Networking/*/*"` → `Passed!`, `failed: 0` (136 + 2 new = 138).

**Steps:**

- [ ] **Step 1: Add the fields (non-`required`, defaults) and extend the tests**

In `PokerTableDisplayArgs`, after `public string? EventText { get; set; }`:

```csharp
    /// <summary>
    ///     The seats paid by the hand that just completed, in seat space. Empty while a hand is running or
    ///     when no hand has completed.
    /// </summary>
    /// <remarks>
    ///     Together with <see cref="WinningHand" /> this is how the client knows where the pot went and why,
    ///     without parsing <see cref="EventText" /> -- which names the winners but cannot be relied on to
    ///     ("A and B split the pot").
    /// </remarks>
    public List<byte>? WinnerSeats { get; set; }

    /// <summary>
    ///     The <c>HandCategory</c> of the hand that won at showdown (1 = high card … 9 = straight flush), or 0
    ///     when the hand was won by everyone else folding or no hand has completed.
    /// </summary>
    public byte WinningHand { get; set; }
```

In `PokerPacketConverterTests.Snapshot_with_full_roster_and_board_round_trips`, add to the args initializer after `EventText = "B raises",`:

```csharp
            WinnerSeats = [2, 4],
            WinningHand = 7,
```

In `Snapshot_with_null_collections_round_trips_as_empty`, find where the test asserts the null collections came back empty and add the same for `WinnerSeats`. The existing test looks like `result.Board.Should().BeEmpty();` (with siblings for `Seats`/`LegalActions`); add:

```csharp
        result.WinnerSeats.Should().BeEmpty();
        result.WinningHand.Should().Be(0);
```

- [ ] **Step 2: Run to see RED**

Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/PokerPacketConverterTests/*"`
Expected: `Snapshot_with_full_roster_and_board_round_trips` FAILS with `Expected property result.WinnerSeats to be a collection with 2 item(s), but found <null>` (or `0u` for `WinningHand`). The null-collections test fails on `WinnerSeats` being null rather than empty.

- [ ] **Step 3: Serialise and deserialise**

In `PokerTableDisplayConverter.Deserialize`, Snapshot case, after `var eventText = reader.ReadString8();`:

```csharp
                var winnerCount = reader.ReadByte();
                var winnerSeats = new List<byte>(winnerCount);

                for (var i = 0; i < winnerCount; i++)
                    winnerSeats.Add(reader.ReadByte());

                var winningHand = reader.ReadByte();
```

and in the returned object after `EventText = eventText`:

```csharp
                    EventText = eventText,
                    WinnerSeats = winnerSeats,
                    WinningHand = winningHand
```

In `Serialize`, Snapshot case, after `writer.WriteString8(args.EventText ?? string.Empty);`:

```csharp
                var winnerSeats = args.WinnerSeats ?? [];
                writer.WriteByte(CountToByte(winnerSeats.Count, "winner seats"));

                foreach (var seat in winnerSeats)
                    writer.WriteByte(seat);

                writer.WriteByte(args.WinningHand);
```

- [ ] **Step 4: Fill them in the script**

In `PokerTableScript.BroadcastSnapshot`, in the `new PokerTableDisplayArgs { … }` initializer after `EventText = eventText`:

```csharp
                            EventText = eventText,

                            //only a finished hand has winners; an empty list is the client's "no reveal"
                            WinnerSeats = hand is { IsComplete: true }
                                ? hand.Winners
                                      .Select(SeatOfHandIndex)
                                      .Where(seatIndex => seatIndex >= 0)
                                      .Select(ToWireSeat)
                                      .ToList()
                                : [],
                            WinningHand = hand is { IsComplete: true } ? (byte)(hand.WinningCategory ?? 0) : (byte)0
```

(`SeatOfHandIndex` and `ToWireSeat` already exist in the script; `hand.Winners` is hand space, which is why the conversion is there — see `BuildResultText`'s remark.)

- [ ] **Step 5: Run poker + networking suites**

Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.Poker|Chaos.Tests.Networking/*/*"`
Expected: `Passed!`, `total: 138`, `failed: 0`.

- [ ] **Step 6: Commit**

```bash
git add Chaos.Networking/Entities/Server/PokerTableDisplayArgs.cs Chaos.Networking/Converters/Server/PokerTableDisplayConverter.cs Chaos/Scripting/MerchantScripts/Casino/PokerTableScript.cs Tests/Chaos.Tests/Networking/PokerPacketConverterTests.cs
git commit -m "Send the winning seats and the winning hand on the completion snapshot

The client could only learn who won by reading the event text, which cannot
be parsed reliably once two players split a pot. WinnerSeats is seat space;
WinningHand is the HandCategory byte, 0 for a hand won by folds. Both are
empty/0 on every snapshot but the one that completes a hand.

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01S8vbxZVquYHaZtdwfxiEef"
```

---

### Task 3: View model fields and the result banner's second line

**Goal:** The panel shows, under the existing "Mike wins 24,000 gold." line, a gold line naming the winning hand category or "everyone else folded", for exactly the reveal window.

**Files:**
- Modify: `Chaos.Client/ViewModel/PokerTable.cs` (`PokerTable` class: properties, `ApplySnapshot`, `Clear`)
- Modify: `Chaos.Client/Controls/World/Popups/Poker/PokerTableControl.cs` (constants near `EVENT_TOP` ~line 237; fields near `EventLabel`; constructor after `AddChild(EventLabel)` ~line 541; `OnSnapshot` where `EventLabel.Text = vm.EventText` ~line 1009)

**Acceptance Criteria:**
- [ ] `WorldState.PokerTable.WinnerSeats` / `WinningHand` mirror the packet and reset on `Clear()`.
- [ ] `dotnet build Chaos.Client.slnx` succeeds with no new warnings in the touched files.
- [ ] On a live showdown the second line reads e.g. "Full House" in gold; on a fold win it reads "everyone else folded"; on the next hand's first snapshot it is gone.

**Verify:** `cd C:\Users\mikeb\Documents\GitHub\Chaos.Client && dotnet build Chaos.Client.slnx -v q --nologo 2>&1 | grep -E "error CS|Build succeeded"` → `Build succeeded.`

**Steps:**

- [ ] **Step 1: View model**

In `Chaos.Client/ViewModel/PokerTable.cs`, class `PokerTable`, after `public string EventText { get; private set; } = string.Empty;`:

```csharp
    /// <summary>The seats paid by the hand that just completed. Empty until a hand completes; empty again on the next hand's first snapshot.</summary>
    public IReadOnlyList<int> WinnerSeats { get; private set; } = [];

    /// <summary>The winning hand's category byte (1–9), or 0 when the hand was won by everyone else folding. See <c>PokerTableDisplayArgs.WinningHand</c>.</summary>
    public byte WinningHand { get; private set; }
```

In `ApplySnapshot`, after `EventText = args.EventText ?? string.Empty;`:

```csharp
        WinnerSeats = (args.WinnerSeats ?? []).Select(seat => (int)seat).ToList();
        WinningHand = args.WinningHand;
```

In `Clear()`, after `EventText = string.Empty;`:

```csharp
        WinnerSeats = [];
        WinningHand = 0;
```

- [ ] **Step 2: Constants, field, and construction of the second line**

In `PokerTableControl.cs`, after `private const int EVENT_LEFT = FELT_CENTER_X - (EVENT_WIDTH / 2);`:

```csharp
    /// <summary>The "why" line sits directly under the event text, inside the felt, clear of the board above it.</summary>
    private const int WIN_REASON_TOP = EVENT_TOP + TextRenderer.CHAR_HEIGHT + 2;
```

Next to `private readonly UILabel EventLabel;` (search for that declaration):

```csharp
    private readonly UILabel WinReasonLabel;
```

Add a static table near the other static readonly fields (e.g. after `ActingSeatColor`):

```csharp
    /// <summary>
    ///     Names for <c>PokerTableDisplayArgs.WinningHand</c>, indexed by the wire byte. Index 0 is the
    ///     no-showdown case. Kept client-side so the server sends one byte rather than a string per client.
    /// </summary>
    private static readonly string[] WinningHandNames =
    [
        "everyone else folded",
        "High Card",
        "One Pair",
        "Two Pair",
        "Three of a Kind",
        "Straight",
        "Flush",
        "Full House",
        "Four of a Kind",
        "Straight Flush"
    ];
```

In the constructor, immediately after `AddChild(EventLabel);`:

```csharp
        //the second line of the result banner: why the pot went where it did. Painted only while the server
        //reports winners, which is exactly the settle pause, so it needs no timer of its own.
        WinReasonLabel = new UILabel
        {
            X = EVENT_LEFT,
            Y = WIN_REASON_TOP,
            Width = EVENT_WIDTH,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = LegendColors.Gold,
            IsHitTestVisible = false,
            Visible = false
        };
        AddChild(WinReasonLabel);
```

- [ ] **Step 3: Paint it from the snapshot**

In `OnSnapshot`, directly after the block that sets `EventLabel.Text = vm.EventText;` (the `if (RejectHoldRemaining <= 0f) { … }` block), add:

```csharp
        //── result banner, line two: present exactly when the server reports winners ──
        if (vm.WinnerSeats.Count > 0)
        {
            WinReasonLabel.Text = vm.WinningHand < WinningHandNames.Length ? WinningHandNames[vm.WinningHand] : "a winning hand";
            WinReasonLabel.Visible = true;
        } else
            WinReasonLabel.Visible = false;
```

(So the two lines read "Mike wins 24,000 gold." / "Full House", or "…" / "everyone else folded" -- the category name on its own, as the spec says; no article, so "Two Pair" and "High Card" read naturally.)

- [ ] **Step 4: Hide it with the panel**

In `Hide()`, after `ClearAnimations();`, add `WinReasonLabel.Visible = false;` so a reopened panel never shows a stale reason before its first snapshot.

- [ ] **Step 5: Build**

Run: `dotnet build Chaos.Client.slnx -v q --nologo 2>&1 | grep -E "error CS|Build succeeded"`
Expected: `Build succeeded.` (The submodule must already be at Task 2's commit for `WinnerSeats`/`WinningHand` to exist — it is, the submodule is a live checkout.)

- [ ] **Step 6: Commit (client repo)**

```bash
git add Chaos.Client/ViewModel/PokerTable.cs Chaos.Client/Controls/World/Popups/Poker/PokerTableControl.cs
git -c core.commitGraph=false commit -m "Say why the pot went where it did

A second, gold line under the result text names the winning hand's category
-- \"Full House\" -- or \"everyone else folded\". Painted while the
server reports winners, which is the settle pause, so it needs no timer.

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01S8vbxZVquYHaZtdwfxiEef"
```

---

### Task 4: Pot slide to the winner and the winner outline

**Goal:** On the snapshot that first reports winners, eight coins per winner slide from the pot box to that winner's plaque, staggered, and the winner's plaque border shows gold for the reveal.

**Files:**
- Modify: `Chaos.Client/Controls/World/Popups/Poker/PokerTableControl.cs` — `ChipSlide` class (~line 2645), `SpawnChipSlide` (~1162), `DetectActionAnimations` (~1131), `ClearAnimations` (~1225), `SeatPanel` (`ActingOutline` at ~1615, `Apply` at ~1679), the `Apply` call site in `OnSnapshot` (~983), constant `ActingSeatColor` (~294)

**Acceptance Criteria:**
- [ ] `ChipSlide` accepts a start delay; a delayed coin is not drawn and does not move until the delay has elapsed; `IsExpired` counts from the end of the delay.
- [ ] On the first snapshot with `WinnerSeats` non-empty, 8 coins per winner travel pot → plaque centre, 60 ms apart; no coins on the following snapshots of the same completed hand.
- [ ] The winner's plaque shows a gold outline while `WinnerSeats` contains it, in place of the acting outline.
- [ ] `dotnet build Chaos.Client.slnx` succeeds.
- [ ] Live: coins visibly leave the pot and land on the winning plaque (both plaques on a split pot); a new hand clears the outline.

**Verify:** `cd C:\Users\mikeb\Documents\GitHub\Chaos.Client && dotnet build Chaos.Client.slnx -v q --nologo 2>&1 | grep -E "error CS|Build succeeded"` → `Build succeeded.`

**Steps:**

- [ ] **Step 1: Give `ChipSlide` a delay**

Replace the `ChipSlide` constructor and the two members that read `Elapsed` so the class becomes:

```csharp
    private sealed class ChipSlide : UIElement
    {
        private const float DURATION_MS = 520f;
        private const int COIN_SIZE = 10;

        private readonly float FromX;
        private readonly float FromY;
        private readonly float ToX;
        private readonly float ToY;

        //negative while the coin is waiting its turn: a pot paid out as one blob reads as a glitch, a stream of
        //coins reads as gold changing hands
        private float Elapsed;

        public bool IsExpired => Elapsed >= DURATION_MS;

        public ChipSlide(
            string name,
            int fromX,
            int fromY,
            int toX,
            int toY,
            float delayMs = 0f)
        {
            Name = name;
            FromX = fromX;
            FromY = fromY;
            ToX = toX;
            ToY = toY;
            Elapsed = -delayMs;
            Width = COIN_SIZE;
            Height = COIN_SIZE;
            IsHitTestVisible = false;
            X = fromX;
            Y = fromY;
        }

        public override void Update(GameTime gameTime)
        {
            Elapsed += (float)gameTime.ElapsedGameTime.TotalMilliseconds;

            var t = Math.Clamp(Elapsed / DURATION_MS, 0f, 1f);

            //eased so the piece leaves briskly and settles rather than arriving at speed
            var eased = 1f - ((1f - t) * (1f - t));

            X = (int)float.Lerp(FromX, ToX, eased);
            Y = (int)float.Lerp(FromY, ToY, eased);
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (!Visible || IsExpired || (Elapsed < 0f))
                return;

            //fades over the last third rather than the whole trip, so it reads as landing rather than dissolving
            var t = Math.Clamp(Elapsed / DURATION_MS, 0f, 1f);
            var alpha = t < 0.66f ? 1f : 1f - ((t - 0.66f) / 0.34f);

            spriteBatch.Draw(
                CoinTexture,
                new Rectangle(ScreenX, ScreenY, COIN_SIZE, COIN_SIZE),
                Color.White * alpha);
        }
    }
```

(`Math.Clamp` of a negative `Elapsed / DURATION_MS` is 0, so a waiting coin stays at its origin; the `Elapsed < 0f` guard keeps it invisible until its turn.)

- [ ] **Step 2: Let `SpawnChipSlide` pass the delay through**

```csharp
    private void SpawnChipSlide(
        int fromX,
        int fromY,
        int toX,
        int toY,
        float delayMs = 0f)
    {
        var slide = new ChipSlide(
            $"PokerChip{AnimationSequence++}",
            fromX,
            fromY,
            toX,
            toY,
            delayMs)
        {
            //over the felt and the plaques, under the pickers and the confirmation
            ZIndex = 45
        };

        ChipSlides.Add(slide);
        AddChild(slide);
    }
```

- [ ] **Step 3: Track "already animated this payout" and spawn the reverse slides**

Add two constants next to the other animation constants (search for `AnimationSequence`):

```csharp
    /// <summary>Coins in the stream from the pot to each winner. Enough to read as a pot, few enough to land inside the reveal.</summary>
    private const int PAYOUT_COINS = 8;

    private const float PAYOUT_COIN_GAP_MS = 60f;
```

Add a field next to `AnimationsPrimed`:

```csharp
    //the winners the last snapshot reported, so the payout stream fires once per completed hand rather than on
    //every repaint of the reveal
    private bool PayoutAnimated;
```

At the end of `DetectActionAnimations()`, after `AnimationsPrimed = true;`, add:

```csharp
        DetectPayoutAnimation(potX, potY);
```

and add the method directly after `DetectActionAnimations`:

```csharp
    /// <summary>
    ///     Runs the coin stream from the pot to every winner, once, on the first snapshot that reports winners.
    /// </summary>
    /// <remarks>
    ///     Keyed on the server's <c>WinnerSeats</c>, never on the event text. The winners stay in every
    ///     snapshot of the reveal, so <see cref="PayoutAnimated" /> is what stops the stream re-firing on each
    ///     repaint; the next hand's first snapshot reports no winners and re-arms it.
    /// </remarks>
    private void DetectPayoutAnimation(int potX, int potY)
    {
        var winners = WorldState.PokerTable.WinnerSeats;

        if (winners.Count == 0)
        {
            PayoutAnimated = false;

            return;
        }

        if (PayoutAnimated)
            return;

        PayoutAnimated = true;

        foreach (var seat in winners)
        {
            if ((seat < 0) || (seat >= SEAT_COUNT))
                continue;

            var (seatX, seatY) = SeatAnchor(seat);
            var targetX = seatX + (SEAT_WIDTH / 2);
            var targetY = seatY + (SEAT_HEIGHT / 2);

            for (var coin = 0; coin < PAYOUT_COINS; coin++)
                SpawnChipSlide(potX, potY, targetX, targetY, coin * PAYOUT_COIN_GAP_MS);
        }
    }
```

In `ClearAnimations()`, after `AnimationsPrimed = false;`, add `PayoutAnimated = false;`.

- [ ] **Step 4: Winner outline on the plaque**

Next to `private static readonly Color ActingSeatColor = new(255, 200, 60, 220);` add:

```csharp
    private static readonly Color WinnerSeatColor = new(255, 215, 0, 240);
```

In `SeatPanel`, next to `private readonly UIPanel ActingOutline;`:

```csharp
        private readonly UIPanel WinnerOutline;
```

In the `SeatPanel` constructor, directly after `AddChild(ActingOutline);`:

```csharp
            //thicker and brighter than the acting outline, and drawn over it: for the reveal, this is the seat
            WinnerOutline = new UIPanel
            {
                X = 0,
                Y = 0,
                Width = SEAT_WIDTH,
                Height = SEAT_HEIGHT,
                Background = BuildBorder(
                    SEAT_WIDTH,
                    SEAT_HEIGHT,
                    WinnerSeatColor,
                    3),
                IsHitTestVisible = false,
                Visible = false
            };
            AddChild(WinnerOutline);
```

Change `Apply`'s signature and first lines to:

```csharp
        public void Apply(
            PokerSeatInfo? info,
            bool isButton,
            bool isActor,
            bool isYou,
            bool handInProgress,
            bool isWinner)
        {
            DealerBadge.Visible = isButton;
            ActingOutline.Visible = isActor && !isWinner;
            WinnerOutline.Visible = isWinner;
```

Add `/// <param name="isWinner">Whether the server lists this seat among the hand's winners; shown for the reveal.</param>` to the method's XML doc.

In `OnSnapshot`, update the call site:

```csharp
        for (var seat = 0; seat < SEAT_COUNT; seat++)
            SeatPanels[seat]
                .Apply(
                    SeatLookup[seat],
                    buttonIndex.HasValue && (buttonIndex.Value == seat),
                    actorIndex.HasValue && (actorIndex.Value == seat),
                    seat == vm.YourSeatIndex,
                    handInProgress,
                    vm.WinnerSeats.Contains(seat));
```

- [ ] **Step 5: Build**

Run: `dotnet build Chaos.Client.slnx -v q --nologo 2>&1 | grep -E "error CS|Build succeeded"`
Expected: `Build succeeded.`

- [ ] **Step 6: Commit (client repo)**

```bash
git add Chaos.Client/Controls/World/Popups/Poker/PokerTableControl.cs
git -c core.commitGraph=false commit -m "Slide the pot to the winner

Eight coins per winner stream from the pot box to the plaque, sixty
milliseconds apart, using the same ChipSlide that carries wagers the other
way; it gains a start delay for the stagger. The winning plaque wears a gold
outline for the reveal. Fires once per completed hand, keyed on the server's
WinnerSeats, never on the event text.

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01S8vbxZVquYHaZtdwfxiEef"
```

---

### Task 5: Pin the submodule, add Run H, live check

**Goal:** The client branch points at the server commit that carries the fields; the walkthrough tells the tester what to look for; both are seen working on the two live clients.

**Files:**
- Modify: `Chaos-Server` (submodule pointer)
- Modify: `docs/superpowers/plans/2026-08-27-poker-table-walkthrough.md` (insert before `## 4. Known, already triaged`)

**Acceptance Criteria:**
- [ ] `git ls-tree HEAD Chaos-Server` in the client repo shows Task 2's commit hash.
- [ ] Run H exists in the walkthrough.
- [ ] Server rebooted on the new build (`Listening on 0.0.0.0:4202`, zero `"Level":"Error"` lines in `Chaos/bin/Debug/net10.0/logs/<today>.log`), two clients launched and connected.

**Verify:** `cd C:\Users\mikeb\Documents\GitHub\Chaos.Client && git ls-tree HEAD Chaos-Server && git status --short` → pointer equals `git -C Chaos-Server rev-parse HEAD`; no ` m Chaos-Server` line except for the untracked local `appsettings*.json`.

**Steps:**

- [ ] **Step 1: Run H in the walkthrough**

Insert before the `---` that precedes `## 4. Known, already triaged — not worth reporting`:

```markdown
### Run H — the reveal

1. **Showdown.** Play a hand to the river with two live players and check it down. Under
   "Name wins N gold." a second gold line must name the category — e.g. "Full House",
   "Two Pair". Eight coins must stream from the
   pot box to the winner's plaque, and that plaque must wear a gold outline until the next deal.
2. **Fold win.** Next hand, fold to the other player at once. The second line must read
   "everyone else folded"; coins and outline as above.
3. **Split pot.** Force a tie if you can (both players playing the board — check every street on a
   board like `A K Q J T` of one suit is easiest with `ForceBoardForTesting` in a test, not live;
   live, just note whether a split ever happens). Both plaques must get coins and outlines.
4. **New hand clears it.** On the next deal the second line and the outline must be gone.
```

- [ ] **Step 2: Restart the server on the new build, relaunch two clients**

Stop the running server (it holds the build output locked): in PowerShell, `Get-Process Chaos -ErrorAction SilentlyContinue | Stop-Process -Force`. Then from `Chaos-Server/Chaos`: `dotnet run -c Debug > <scratchpad>/server-boot.log 2>&1` in the background (no `| head`, it breaks the pipe). Wait for `Listening on "0.0.0.0:4202"` in `Chaos/bin/Debug/net10.0/logs/<today>.log` and confirm `grep -c '"Level":"Error"'` is `0`.

Launch two clients (PowerShell):

```powershell
$env:DA_LOBBY_HOST = '127.0.0.1'; $env:DA_LOBBY_PORT = '4200'; $env:DA_PATH = 'C:\Users\mikeb\Documents\Chaos\Unora'
$exe = 'C:\Users\mikeb\Documents\GitHub\Chaos.Client\Chaos.Client\bin\Debug\net10.0\Chaos.Client.exe'
Start-Process $exe; Start-Sleep 1; Start-Process $exe
```

Both windows must title "Unora" and hold an ESTABLISHED connection to 127.0.0.1:4201 (`netstat -ano | findstr 4201`).

- [ ] **Step 3: Pin and commit (client repo)**

```bash
git add Chaos-Server docs/superpowers/plans/2026-08-27-poker-table-walkthrough.md
git -c core.commitGraph=false commit -m "Pin the server that names the winning hand, and add Run H

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01S8vbxZVquYHaZtdwfxiEef"
```

(`docs/` is gitignored but the walkthrough is already tracked, so a plain `git add` of the path works; if git warns "ignored", use `git add -f`.)

---

## Self-review

- **Spec coverage:** `WinningCategory` → Task 1. Wire fields + converter + script → Task 2. View model + banner → Task 3. Pot slide + outline → Task 4. Run H + pin → Task 5. Detection by `WinnerSeats`, never text → Tasks 3 and 4. `Clear()` resets → Task 3. `ClearAnimations` retires in-flight coins and re-arms → Task 4.
- **Placeholders:** none; every step has its code.
- **Type consistency:** `WinnerSeats` is `List<byte>?` on the wire, `IReadOnlyList<int>` on the view model (converted in `ApplySnapshot`), consumed as `int` seats in Tasks 3–4. `WinningHand` is `byte` throughout. `SpawnChipSlide(int, int, int, int, float = 0)` matches `ChipSlide(string, int, int, int, int, float = 0)`. `Apply` gains a sixth `bool isWinner` and its one call site passes `vm.WinnerSeats.Contains(seat)`.
