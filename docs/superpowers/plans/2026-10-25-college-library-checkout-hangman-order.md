# College: Director self-approval, Library checkout, Hangman turn order — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let Directors approve their own Library books, let players carry a 24-hour copy of a shelved book, and give Hangman a fresh random turn order each round.

**Architecture:** All rules live on the server (`Chaos-Server` submodule, `Chaos/Services/College`). The client gets one new tick box in the Hangman turn-order window and one new button plus a read-only view in the Library book window. One new item template goes into Unora.

**Tech Stack:** C# / .NET 10, TUnit + FluentAssertions tests, Chaos-Server, Chaos.Client (MonoGame), Unora JSON templates.

**Spec:** `Chaos.Client/docs/superpowers/specs/2026-10-25-college-library-checkout-hangman-order-design.md`

## Global Constraints

- Repos: server code in `C:\Users\Michael\Documents\GitHub\Chaos.Client\Chaos-Server` (its own git repo), client in `C:\Users\Michael\Documents\GitHub\Chaos.Client`, template in `C:\Users\Michael\Documents\GitHub\Unora`.
- Server tests: `dotnet build Tests/Chaos.Tests/Chaos.Tests.csproj` then `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj --no-build -- --treenode-filter "/*/*/<ClassName>/*"` from the server repo root. Client tests: `dotnet build` then `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build` from the client root.
- Orange bar text max 45 characters. No em dashes in JSON.
- Library copy lifetime: exactly 24 hours of real time from checkout.
- Shelves that allow checkout: Guides, Lore, ShortStories, PoemsAndSongs. Never Artwork.
- At most one Library copy in a player's inventory.
- `CLIENT_VERSION` goes from 806 to 807 (`Chaos.DarkAges/Definitions/CONSTANTS.cs`), once, in Task 4.
- Do not commit in any task; the last task commits.
- Match surrounding code style: `using var scope = Sync.EnterScope();` locking, braces/blank lines as in the files touched, comments only for non-obvious "why".

**User decisions (already made):**
- "Directors may approve their own books; those still count toward the Library Author mark." Knights/admins stay blocked from their own books; reports on a Director's own book still go to others.
- Copy: inventory item, expires after 24 hours, cannot be traded/dropped/banked/sold, one per player, always shows the current shelved version, all shelves except Artwork, free.
- Hangman: reshuffle after every full pass (option 2), controlled by a "New order each round" tick box in the Teacher's turn-order window, ticked by default; unticked keeps today's fixed order.

## Review Focus

1. A Director with two characters on one machine: the self-approval waiver applies to their own book only; another author's book from the same machine is still refused.
2. Hangman: a player removed mid-pass, then the pass wraps: the removed player must never get a turn and the new pass must not crash with every player removed but one.
3. Hangman: the word is solved by a buzzer who is not the turn player; the next word must continue the pass from the turn player, not jump to the buzzer.
4. Library copy: the book is edited and re-approved after checkout; using the copy shows the new text (it reads `book.Shelved` at use time, never a stored piece id).
5. Library copy: checking out the same book again while holding its copy refreshes the expiry and does not leave two copies.

Tests for each line are in Tasks 1, 2 and 5.

---

### Task 1: Directors approve their own waiting books

**Goal:** A College Director can open, approve and reject their own waiting book; nothing else about reviewing changes.

**Files:**
- Modify: `Chaos-Server/Chaos/Services/College/LibraryRules.cs` (add `MayReviewWaiting`)
- Modify: `Chaos-Server/Chaos/Services/College/CollegeService.Library.cs` (`OpenBook`, `ReviewListLocked`, `ApproveLibrary`, `RejectLibrary`)
- Test: `Chaos-Server/Tests/Chaos.Tests/College/LibraryRulesTests.cs`, `Chaos-Server/Tests/Chaos.Tests/College/LibraryServiceTests.cs`

**Acceptance Criteria:**
- [ ] `LibraryRules.MayReviewWaiting(book, "Aroha", key, isDirector: true)` is true when Aroha wrote the book, even with a matching machine key.
- [ ] With `isDirector: false` it equals `MayReview(book, reviewer, key)`.
- [ ] A Director's own waiting book appears in their `ReviewList` as a Waiting row, never as a Reported row.
- [ ] `ApproveLibrary` by the Director-author shelves the book and sets `Counted`; the author's mark goes up by 1.
- [ ] `DismissLibrary` and `OpenBook(ReviewReported)` still refuse the Director-author.
- [ ] A Knight-author still gets `LibraryText.OWN_BOOK` from `ApproveLibrary`.
- [ ] A Director approving another author's book from the same machine is still refused.

**Verify:** server tests for `LibraryRulesTests` and `LibraryServiceTests` → all pass.

**Steps:**

- [ ] **Step 1: Write failing rule tests** in `LibraryRulesTests.cs`, using the existing book helpers in that file:

```csharp
[Test]
public void MayReviewWaiting_lets_a_director_review_their_own_book()
{
    var book = new LibraryBook { Id = 1, Author = "Aroha", AuthorMachineKey = "m1" };

    LibraryRules.MayReviewWaiting(book, "aroha", "m1", true).Should().BeTrue();
    LibraryRules.MayReviewWaiting(book, "aroha", "m1", false).Should().BeFalse();
}

[Test]
public void MayReviewWaiting_keeps_the_machine_check_for_other_authors()
{
    var book = new LibraryBook { Id = 1, Author = "Kael", AuthorMachineKey = "m1" };

    LibraryRules.MayReviewWaiting(book, "Aroha", "m1", true).Should().BeFalse();
    LibraryRules.MayReviewWaiting(book, "Aroha", "m2", true).Should().BeTrue();
}
```

- [ ] **Step 2: Run, expect a compile failure** (`MayReviewWaiting` missing).

- [ ] **Step 3: Add the rule** to `LibraryRules`, right after `MayReview`:

```csharp
/// <summary>Approve/reject of a waiting version. A Director may decide their own book; reports never use this.</summary>
public static bool MayReviewWaiting(LibraryBook book, string reviewer, string reviewerMachineKey, bool isDirector)
    => (isDirector && book.IsAuthor(reviewer)) || MayReview(book, reviewer, reviewerMachineKey);
```

- [ ] **Step 4: Wire the service.** In `CollegeService.Library.cs`:
  - `OpenBook`: compute `var director = CollegeRoles.IsDirector(State, name);`. Use `LibraryRules.MayReviewWaiting(book, name, reviewerMachineKey, director)` for the `ReviewWaiting` and `ReviewShelf` arms. Leave `ReviewReported` on `MayReview`. Change the `CanReview` argument of the returned view from `reviewer && !own` to `reviewer && (context == CollegePieceContext.LibraryReported ? !own : (!own || director))`.
  - `ReviewListLocked`: in the Waiting query use `MayReviewWaiting(b, name, reviewerMachineKey, CollegeRoles.IsDirector(State, name))` (compute the bool once before the query). Leave the Reported query on `MayReview`.
  - `ApproveLibrary` and `RejectLibrary`: replace `LibraryRules.MayReview(book, by, reviewerMachineKey)` with `LibraryRules.MayReviewWaiting(book, by, reviewerMachineKey, CollegeRoles.IsDirector(State, by))`.
  - `DismissLibrary`: unchanged.

- [ ] **Step 5: Service tests** in `LibraryServiceTests.cs`, following how that file already makes a Director (look for an existing test that calls `CollegeRoles`/sets a Director in state) and sends a book for review. Add:
  - `A_director_approves_their_own_book_and_it_counts`: Director Aroha writes and sends a Guides book, `ReviewList("Aroha", false, false)` contains one Waiting row with that id, `ApproveLibrary("Aroha", false, false, id)` returns `Ok == true`, the book is shelved, `LibraryMarkOf("Aroha") == 1`.
  - `A_director_cannot_dismiss_reports_on_their_own_book`: after shelving, another player reports it; `ReviewList("Aroha", ...)` has no Reported row for it; `DismissLibrary("Aroha", false, false, id).Message == LibraryText.OWN_BOOK`.
  - `A_knight_still_cannot_approve_their_own_book`: non-Director Kael (`isKnight: true`) sends a book; `ApproveLibrary("Kael", true, false, id).Message == LibraryText.OWN_BOOK`.

- [ ] **Step 6: Run** `LibraryRulesTests` and `LibraryServiceTests` → all pass, including the old `MayReview` tests.

```json:metadata
{"files": ["Chaos-Server/Chaos/Services/College/LibraryRules.cs", "Chaos-Server/Chaos/Services/College/CollegeService.Library.cs", "Chaos-Server/Tests/Chaos.Tests/College/LibraryRulesTests.cs", "Chaos-Server/Tests/Chaos.Tests/College/LibraryServiceTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj --no-build -- --treenode-filter \"/*/*/Library*Tests/*\"", "acceptanceCriteria": ["MayReviewWaiting true for Director-author", "Director own book in review list as Waiting only", "Director approve counts toward mark", "Dismiss still refused for Director-author", "Knight-author still refused", "Machine check kept for other authors"], "modelTier": "mechanical"}
```

---

### Task 2: Hangman new order each round (server)

**Goal:** When a game is started with "new order each round", the turn list is shuffled at the start and after every full pass, with no back-to-back turn across passes.

**Files:**
- Modify: `Chaos-Server/Chaos/Services/College/CollegeTools.cs` (`LiveHangman`)
- Modify: `Chaos-Server/Chaos/Services/College/HangmanSession.cs`
- Modify: `Chaos-Server/Chaos/Services/College/CollegeService.WordGames.cs` (`StartHangman`)
- Modify: `Chaos-Server/Chaos.Networking/Entities/Client/CollegeActionArgs.cs`, `Chaos-Server/Chaos.Networking/Converters/Client/CollegeActionConverter.cs` (HangmanOrder carries the flag)
- Modify: `Chaos-Server/Chaos/Services/College/CollegePanel.cs` (HangmanOrder case passes the flag)
- Test: `Chaos-Server/Tests/Chaos.Tests/College/HangmanSessionTests.cs`, plus the converter round-trip test file if one covers HangmanOrder (search `HangmanOrder` under `Tests/`)

**Acceptance Criteria:**
- [ ] With the flag off, every existing `HangmanSessionTests` test passes unchanged.
- [ ] With the flag on and 4 players, the first 4 turns are 4 different players, and turns 5-8 are again 4 different players.
- [ ] Across 20 seeds, the turn order of pass 1 and pass 2 differ for at least one seed, and no seed gives the same player the last turn of a pass and the first turn of the next.
- [ ] A player added mid-pass gets a turn before the pass ends.
- [ ] A player removed mid-pass gets no further turn; a pass with one active player keeps giving that player turns.
- [ ] After a buzz-solve by a non-turn player, the next word's first turn is the player after the turn player in the pass.
- [ ] `HangmanOrder` packets round-trip the new flag.

**Verify:** server tests for `HangmanSessionTests` (and the converter tests) → all pass.

**Steps:**

- [ ] **Step 1: Model fields.** In `LiveHangman` add:

```csharp
/// <summary>Reshuffle <see cref="Players" /> each time everyone has had a turn.</summary>
public bool NewOrderEachPass { get; set; }

[JsonIgnore]
public Random Random { get; set; } = Random.Shared;
```

Use `System.Text.Json.Serialization.JsonIgnore` if the College state is saved with System.Text.Json (check `CollegeState`'s store); use the matching attribute if it is another serializer. Update the `Players` summary to: "The turn list, keys in order. A removed player keeps their place; with NewOrderEachPass the list is reordered at the start of each pass."

- [ ] **Step 2: Write failing tests** in `HangmanSessionTests.cs` (add a helper; keep the existing `Start` helper as it is, flag off):

```csharp
private static LiveHangman StartShuffled(int seed, params string[] players)
{
    var list = new[] { "GLIOCA", "MILETH", "DANAAN", "SGRIOS", "TAGOR", "ABEL" }
               .Select(w => new CollegeWord { Text = w, Hint = "hint " + w })
               .ToList();
    var game = HangmanSession.Start("list1", "Mileth", list, HangmanMode.Classic, true, true,
        players.Length == 0 ? ["Aroha", "Kael", "Mira", "Tam"] : players, true, new Random(seed));
    HangmanSession.Next(game, Noon).Should().Be(HangmanStep.Done);

    return game;
}

private static List<string> TakeTurns(LiveHangman game, int count)
{
    var seen = new List<string>();

    for (var i = 0; i < count; i++)
    {
        seen.Add(HangmanSession.TurnKey(game)!);
        HangmanSession.Skip(game, Noon).Should().BeTrue();
    }

    return seen;
}

[Test]
public void New_order_each_pass_gives_everyone_one_turn_per_pass()
{
    var game = StartShuffled(3);
    var turns = TakeTurns(game, 8);

    turns.Take(4).Should().OnlyHaveUniqueItems().And.HaveCount(4);
    turns.Skip(4).Should().OnlyHaveUniqueItems().And.HaveCount(4);
}

[Test]
public void New_order_each_pass_changes_the_order_and_never_repeats_across_the_seam()
{
    var anyDifferent = false;

    for (var seed = 0; seed < 20; seed++)
    {
        var turns = TakeTurns(StartShuffled(seed), 8);
        turns[4].Should().NotBe(turns[3]);
        anyDifferent |= !turns.Take(4).SequenceEqual(turns.Skip(4));
    }

    anyDifferent.Should().BeTrue();
}

[Test]
public void A_late_joiner_gets_a_turn_in_the_current_pass()
{
    var game = StartShuffled(5);
    var first = TakeTurns(game, 1);
    HangmanSession.AddPlayer(game, "Nia", Noon).Should().BeTrue();

    TakeTurns(game, 4).Should().Contain("nia").And.NotContain(first[0]);
}

[Test]
public void A_removed_player_gets_no_more_turns_and_one_player_keeps_playing()
{
    var game = StartShuffled(7, "Aroha", "Kael");
    var other = HangmanSession.TurnKey(game) == "aroha" ? "Kael" : "Aroha";
    HangmanSession.RemovePlayer(game, other, Noon).Should().BeTrue();

    TakeTurns(game, 4).Should().AllBe(HangmanSession.TurnKey(game)!);
}

[Test]
public void A_new_word_continues_the_pass_from_the_turn_player()
{
    var game = StartShuffled(11, "Aroha", "Kael", "Mira", "Tam");
    var order = game.Players.ToList();
    var turnPlayer = HangmanSession.TurnKey(game)!;
    var buzzer = order.First(p => p != turnPlayer);

    HangmanSession.Buzz(game, buzzer, Noon).Should().Be(BuzzResult.Buzzed);
    HangmanSession.Solve(game, buzzer, "GLIOCA", Noon).Should().BeTrue();
    HangmanSession.Next(game, Noon).Should().Be(HangmanStep.Done);

    HangmanSession.TurnKey(game).Should().Be(order[order.IndexOf(turnPlayer) + 1]);
}
```

(The first word is GLIOCA; `Buzz`/`Solve` take the display name, and keys are lower-case; the names list above is lower-cased by `Start`, so pass the key — `Buzz` and `Solve` call `CollegeState.Key` themselves.) `HangmanMode.Classic` stands for the non-Survival mode; use the actual enum member if named differently.

- [ ] **Step 3: Run, expect compile failure** (new `Start` parameters missing).

- [ ] **Step 4: Implement in `HangmanSession`.**
  - `Start(...)` gains `bool newOrderEachPass = false, Random? random = null`. Set `NewOrderEachPass`, set `Random = random ?? Random.Shared`, add the players as today, then `if (newOrderEachPass) NewPass(game, null);`.
  - Add:

```csharp
/// <summary>Shuffles the players still in the game to the front; removed players go to the back. The last mover never opens the new pass.</summary>
private static void NewPass(LiveHangman game, string? lastMover)
{
    var active = game.Players.Where(p => !game.Removed.Contains(p)).ToArray();
    game.Random.Shuffle(active);

    if ((active.Length > 1) && (active[0] == lastMover))
    {
        var swap = game.Random.Next(1, active.Length);
        (active[0], active[swap]) = (active[swap], active[0]);
    }

    game.Players = active.Concat(game.Players.Where(game.Removed.Contains)).ToList();
    game.NextStarter = 0;
}
```

  - `BeginTurnFrom`: when `game.NewOrderEachPass`, look for the first non-removed player at index `start..Count-1` (no wrap). If none, call `NewPass(game, lastMover)` where `lastMover` is `game.Players[start - 1]` when `0 < start <= Count`, else null, then take the first non-removed player from index 0. Keep the existing wrap loop for the flag-off case. Set `Turn` and `TurnUntilUtc` as today.
  - `EndWord`: when `NewOrderEachPass`, set `NextStarter = (game.Turn >= 0 ? game.Turn : mover) + 1` before `Turn` is cleared; keep `mover + 1` when off.
  - `AddPlayer`: when `NewOrderEachPass` and the key is new (or rejoining), insert it at a random index in `[from, activeCount]`, where `from = game.Turn >= 0 ? game.Turn + 1 : game.NextStarter` (clamped to `[0, activeCount]`) and `activeCount` is the number of non-removed players in `Players` (removed players stay at the back). Then re-find `Turn` by the turn key as the existing code does, and keep `NextStarter` as an index (shift it by one if the insert index is below it). Keep the existing append path for the flag-off case.

- [ ] **Step 5: Service and packet.**
  - `CollegeActionArgs`: add `public bool NewOrderEachRound { get; set; }` with summary "HangmanOrder: reshuffle after each full pass."
  - `CollegeActionConverter`: in the HangmanOrder read case add `args.NewOrderEachRound = reader.ReadBoolean();` after the names; in the write case add `writer.WriteBoolean(args.NewOrderEachRound);`.
  - `CollegeService.StartHangman` gains `bool newOrderEachRound` and passes it to `HangmanSession.Start(..., players, newOrderEachRound)`.
  - `CollegePanel` HangmanOrder case: `college.StartHangman(viewer.Name, room, args.Names, onMap, args.NewOrderEachRound)`.
  - Fix every other caller of `StartHangman` (tests included) to pass `false` unless the test is about this feature.

- [ ] **Step 6: Run** `HangmanSessionTests`, the College service word-game tests and the converter tests → all pass.

```json:metadata
{"files": ["Chaos-Server/Chaos/Services/College/CollegeTools.cs", "Chaos-Server/Chaos/Services/College/HangmanSession.cs", "Chaos-Server/Chaos/Services/College/CollegeService.WordGames.cs", "Chaos-Server/Chaos.Networking/Entities/Client/CollegeActionArgs.cs", "Chaos-Server/Chaos.Networking/Converters/Client/CollegeActionConverter.cs", "Chaos-Server/Chaos/Services/College/CollegePanel.cs", "Chaos-Server/Tests/Chaos.Tests/College/HangmanSessionTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj --no-build -- --treenode-filter \"/*/*/Hangman*/*\"", "acceptanceCriteria": ["flag off: old tests unchanged", "one turn per player per pass", "order differs across passes, no back-to-back", "late joiner in current pass", "removed player skipped", "new word continues from turn player", "packet round-trips flag"], "modelTier": "standard"}
```

---

### Task 3: "New order each round" tick box (client)

**Goal:** The Teacher's Hangman turn-order window has a "New order each round" tick box, ticked each time the window opens, sent with Start.

**Files:**
- Modify: `Chaos.Client/Controls/World/Popups/College/HangmanTurnOrderControl.cs`
- Modify: the place that turns the window's Start into a `CollegeActionArgs` HangmanOrder (search `CollegeActionType.HangmanOrder` under `Chaos.Client/`)
- Test: the existing client test for the turn-order list if any (search `TurnOrderList` under `Tests/Chaos.Client.Tests`); otherwise none

**Acceptance Criteria:**
- [ ] The window shows a tick box labelled "New order each round" below the name rows, ticked every time the window is shown.
- [ ] Clicking it toggles it; Start sends `NewOrderEachRound` equal to its state.
- [ ] Existing drag, Up/Down, Shuffle and Tick all still work.
- [ ] Client builds; client tests pass.

**Verify:** `dotnet build` and client tests from the client root → build succeeds, tests pass.

**Steps:**

- [ ] **Step 1:** Read `HangmanTurnOrderControl` fully. Reuse the tick-box drawing the rows already use (`TurnOrderRow` has one) for a single standalone tick box with the label, placed in the gap above the info line (`INFO_TOP`); move `INFO_TOP` and the window height down by one row if there is no room.
- [ ] **Step 2:** Add `public bool NewOrderEachRound { get; private set; } = true;`, reset to `true` in the method that receives the HangmanOrder message (the one that builds `Order = new TurnOrderList(names)`).
- [ ] **Step 3:** Where Start builds the HangmanOrder `CollegeActionArgs`, set `NewOrderEachRound = <control>.NewOrderEachRound`.
- [ ] **Step 4:** Build and run client tests.

```json:metadata
{"files": ["Chaos.Client/Controls/World/Popups/College/HangmanTurnOrderControl.cs"], "verifyCommand": "dotnet build && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build", "acceptanceCriteria": ["tick box shown, ticked on open", "Start sends its state", "old controls still work", "build + tests pass"], "modelTier": "standard"}
```

---

### Task 4: Library copy protocol

**Goal:** The packet types know about checking out a book and showing a copy.

**Files:**
- Modify: `Chaos-Server/Chaos.DarkAges/Definitions/Enums.cs` (`CollegeActionType`, `CollegePieceContext`)
- Modify: `Chaos-Server/Chaos.DarkAges/Definitions/CONSTANTS.cs` (`CLIENT_VERSION` 806 → 807)
- Modify: `Chaos-Server/Chaos.Networking/Entities/Server/CollegeDisplayArgs.cs`
- Modify: `Chaos-Server/Chaos.Networking/Converters/Server/CollegeDisplayConverter.cs`, `Chaos-Server/Chaos.Networking/Converters/Client/CollegeActionConverter.cs`
- Test: the existing converter round-trip tests for `CollegeDisplayType.LibraryBook` and `CollegeActionType.LibraryApprove` (search under `Chaos-Server/Tests`)

**Acceptance Criteria:**
- [ ] `CollegeActionType.LibraryCheckOut = 64` exists, summary "Checks out a copy of a shelved library book.", and serializes `Id` like `LibraryApprove`.
- [ ] `CollegePieceContext.LibraryCopy = 10` exists.
- [ ] `CollegeDisplayArgs.CanCheckOut` (bool) is written right after `HasShelfVersion` in the LibraryBook case and read back.
- [ ] `CLIENT_VERSION == 807`.
- [ ] Converter tests cover both new fields and pass.

**Verify:** server tests filtered to the converter test classes → pass.

**Steps:**

- [ ] **Step 1:** Add the enum members after `LibraryDrawingFetch = 63` and `LibraryOwn = 9`.
- [ ] **Step 2:** Add `CanCheckOut` to `CollegeDisplayArgs` next to `CanReport`, summary "LibraryBook: show Check out."
- [ ] **Step 3:** `CollegeDisplayConverter` LibraryBook: `args.CanCheckOut = reader.ReadBoolean();` after `HasShelfVersion`; `writer.WriteBoolean(args.CanCheckOut);` after `HasShelfVersion`.
- [ ] **Step 4:** `CollegeActionConverter`: add `case CollegeActionType.LibraryCheckOut:` to the `LibraryApprove`/`LibraryDismiss` case lists in both read and write.
- [ ] **Step 5:** Bump `CLIENT_VERSION` to 807.
- [ ] **Step 6:** Extend the round-trip tests: LibraryBook with `CanCheckOut = true`, and a `LibraryCheckOut` action with `Id = 42`. Run.

```json:metadata
{"files": ["Chaos-Server/Chaos.DarkAges/Definitions/Enums.cs", "Chaos-Server/Chaos.DarkAges/Definitions/CONSTANTS.cs", "Chaos-Server/Chaos.Networking/Entities/Server/CollegeDisplayArgs.cs", "Chaos-Server/Chaos.Networking/Converters/Server/CollegeDisplayConverter.cs", "Chaos-Server/Chaos.Networking/Converters/Client/CollegeActionConverter.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj --no-build -- --treenode-filter \"/*/*/*Converter*/*\"", "acceptanceCriteria": ["LibraryCheckOut=64 serializes Id", "LibraryCopy=10", "CanCheckOut round-trips", "CLIENT_VERSION 807"], "modelTier": "mechanical"}
```

---

### Task 5: Library copy item and server logic

**Goal:** Players can check out, read and lose (by expiry or unshelving) a Library copy item.

**Files:**
- Modify: `Chaos-Server/Chaos/Models/Panel/Item.cs`, `Chaos-Server/Chaos.Schemas/Aisling/ItemSchema.cs`, `Chaos-Server/Chaos/Services/MapperProfiles/ItemMapperProfile.cs`, `Chaos-Server/Chaos/Utilities/RenamedItems.cs`, `Chaos-Server/Chaos/Services/BeautyShop/GenderSwapService.cs` (two new saved fields, copied everywhere `PortraitId` is)
- Modify: `Chaos-Server/Chaos/Services/College/LibraryRules.cs` (`MayCheckOut`)
- Modify: `Chaos-Server/Chaos/Services/College/CollegeService.Library.cs` (`LibraryBookView.CanCheckOut`, `LibraryCopyTitle`)
- Create: `Chaos-Server/Chaos/Services/College/LibraryCopies.cs`
- Modify: `Chaos-Server/Chaos/Services/College/LibraryText.cs` (messages)
- Modify: `Chaos-Server/Chaos/Services/College/CollegePanel.Library.cs` (`LibraryCheckOut` action, `ShowLibraryCopy`, `CanCheckOut` in `LibraryBookArgs`)
- Modify: `Chaos-Server/Chaos/Services/College/CollegePanel.cs` (constructor gains `IItemFactory itemFactory` if not present)
- Create: `Chaos-Server/Chaos/Scripting/ItemScripts/LibraryCopyScript.cs`
- Modify: `Chaos-Server/Chaos/Scripting/AislingScripts/DefaultAislingScript.cs` (login check + one-minute check)
- Test: `Chaos-Server/Tests/Chaos.Tests/College/LibraryRulesTests.cs`, `LibraryServiceTests.cs`, new `Chaos-Server/Tests/Chaos.Tests/College/LibraryCopiesTests.cs`

**Acceptance Criteria:**
- [ ] `Item.LibraryBookId` (`int?`) and `Item.LibraryCopyExpiresUtc` (`DateTime?`) save and load through `ItemSchema` and survive rename/gender-swap copies.
- [ ] `LibraryRules.MayCheckOut` is true only for a book with `Shelved` set, not `TakenDown`, on a shelf other than Artwork.
- [ ] `OpenBook(..., LibraryOpenMode.Shelf)` returns `CanCheckOut == MayCheckOut(book)`; other modes return false.
- [ ] `LibraryCopies.CheckOut` adds an item from template `librarycopy` named `Library Copy: <title>`, with the book id and expiry `now + 24h`.
- [ ] Checking out while holding a copy replaces it (one copy left, new book id, new expiry); the message names the returned title.
- [ ] A full inventory with no copy held refuses with `LibraryText.COPY_NO_ROOM`.
- [ ] `LibraryCopies.ReturnExpired` removes an expired copy and tells the player; leaves an unexpired one.
- [ ] Using a copy whose book is no longer checkout-able removes it with `LibraryText.COPY_GONE`.
- [ ] Using a copy shows the book's current `Shelved` version with context `LibraryCopy` and no review/report/take-down/check-out buttons.
- [ ] The copy cannot be dropped (`CanBeDropped`/`CanBeDroppedOn` false).
- [ ] Expired copies are returned on login and within a minute while online.

**Verify:** server build + `Library*Tests` → pass.

**Steps:**

- [ ] **Step 1: Item fields.** Add to `Item` next to `PortraitId`:

```csharp
public int? LibraryBookId { get; set; }
public DateTime? LibraryCopyExpiresUtc { get; set; }
```

Add the same two properties to `ItemSchema` next to `PortraitId`. In `ItemMapperProfile`, map them both ways next to every `PortraitId` line (schema → item and item → schema). In `RenamedItems.cs` and `GenderSwapService.cs`, copy them next to the `PortraitId` copy line.

- [ ] **Step 2: Text.** In `LibraryText` add (each under 45 characters where it is an orange bar):

```csharp
public const string COPY_NO_ROOM = "You have no room for a Library copy.";
public const string COPY_GONE = "That book is no longer on the shelves.";
public const string COPY_NOT_ALLOWED = "That book cannot be checked out.";
public static string CheckedOut(string title) => $"You check out {Short(title)}.";
public static string Swapped(string oldTitle, string title) => $"You return {Short(oldTitle)} for {Short(title)}.";
public static string Returned(string title) => $"Your copy of {Short(title)} was returned.";
```

with a private `Short(string)` that cuts the title so the whole line fits 45 characters (cut to the remaining length and end with ".." when cut). If `LibraryText` already has a title-shortening helper, reuse it.

- [ ] **Step 3: Rule + failing tests.** Add to `LibraryRules`:

```csharp
public static bool MayCheckOut(LibraryBook book)
    => book is { TakenDown: false, Shelved: { } shelved } && (shelved.Shelf != LibraryShelf.Artwork);
```

Tests in `LibraryRulesTests`: Guides shelved → true; Artwork shelved → false; never shelved → false; taken down → false; shelved with an edit waiting → true.

- [ ] **Step 4: Service.** Add `bool CanCheckOut` as the last parameter of `LibraryBookView`. In `OpenBook`, pass `context == CollegePieceContext.LibraryShelf && LibraryRules.MayCheckOut(book)`; in `LibraryBookForAdmin`, pass `false`. Add:

```csharp
/// <summary>The shelved title of a book that may be checked out, or null.</summary>
public string? LibraryCopyTitle(int bookId)
{
    using var scope = Sync.EnterScope();

    return State.LibraryBook(bookId) is { } book && LibraryRules.MayCheckOut(book) ? book.Shelved!.Title : null;
}
```

Service test: `LibraryCopyTitle` returns the title for a shelved Guides book, null for Artwork and for an unknown id; `OpenBook(Shelf).CanCheckOut` is true for Guides and false for Artwork.

- [ ] **Step 5: `LibraryCopies`.** Create `Chaos/Services/College/LibraryCopies.cs`:

```csharp
using Chaos.Models.Panel;
using Chaos.Models.World;
using Chaos.Services.Factories.Abstractions;

namespace Chaos.Services.College;

/// <summary>The one Library copy a player may carry. Call on the player's map thread.</summary>
public static class LibraryCopies
{
    public const string TEMPLATE_KEY = "librarycopy";
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    public static Item? Find(Aisling aisling)
        => aisling.Inventory.FirstOrDefault(i => i.Template.TemplateKey.Equals(TEMPLATE_KEY, StringComparison.OrdinalIgnoreCase));

    public static string TitleOf(Item copy)
    {
        const string PREFIX = "Library Copy: ";
        var name = copy.CustomNameOverride ?? string.Empty;

        return name.StartsWith(PREFIX, StringComparison.Ordinal) ? name[PREFIX.Length..] : name;
    }

    public static string CheckOut(Aisling aisling, IItemFactory factory, int bookId, string title, DateTime now)
    {
        var item = factory.Create(TEMPLATE_KEY);
        item.CustomNameOverride = $"Library Copy: {title}";
        item.LibraryBookId = bookId;
        item.LibraryCopyExpiresUtc = now + Lifetime;

        var old = Find(aisling);

        if ((old is null) && !aisling.CanCarry(item))
            return LibraryText.COPY_NO_ROOM;

        if (old is not null)
            aisling.Inventory.Remove(old.Slot);

        if (!aisling.Inventory.TryAddToNextSlot(item))
            return LibraryText.COPY_NO_ROOM;

        return old is null ? LibraryText.CheckedOut(title) : LibraryText.Swapped(TitleOf(old), title);
    }

    /// <summary>True when a copy was removed.</summary>
    public static bool ReturnExpired(Aisling aisling, DateTime now)
    {
        if (Find(aisling) is not { } copy || (copy.LibraryCopyExpiresUtc is { } until && (now < until)))
            return false;

        aisling.Inventory.Remove(copy.Slot);
        aisling.SendOrangeBarMessage(LibraryText.Returned(TitleOf(copy)));

        return true;
    }
}
```

Fix the namespaces/method names to the real ones (`IItemFactory` location, `Inventory.Remove(byte slot)` or `RemoveByTemplateKey`, `TryAddToNextSlot`) by checking how `PaintedPortraitService` and other services remove inventory items. A copy with no expiry (`null`) counts as expired.

Tests in `LibraryCopiesTests.cs`: build an `Aisling` and an item factory the way existing tests that touch inventory do (search `Inventory.TryAddToNextSlot` or `MockAisling` under `Tests/`). Cover: first checkout adds one named copy with id and expiry `now + 24h`; second checkout of another book leaves one copy with the new id and returns the `Swapped` text; checking out the same book again refreshes the expiry with one copy left; full inventory with no copy → `COPY_NO_ROOM` and inventory unchanged; full inventory while holding a copy → swap succeeds; `ReturnExpired` at `expiry - 1s` keeps it, at `expiry` removes it.

- [ ] **Step 6: Panel.** In `CollegePanel.Library.cs`:
  - `LibraryBookArgs`: set `CanCheckOut = view.CanCheckOut`.
  - New action case:

```csharp
case CollegeActionType.LibraryCheckOut:
    if (!Throttle.TryStart(viewer.Name))
    {
        viewer.SendOrangeBarMessage(CollegeText.TOO_FAST);

        return;
    }

    viewer.SendOrangeBarMessage(
        college.LibraryCopyTitle(args.Id) is { } title
            ? LibraryCopies.CheckOut(viewer, itemFactory, args.Id, title, time.GetUtcNow().UtcDateTime)
            : LibraryText.COPY_NOT_ALLOWED);

    break;
```

  - New public method:

```csharp
/// <summary>Shows a carried copy: the book's current shelf version, read-only. False when the book cannot be checked out any more.</summary>
public bool ShowLibraryCopy(Aisling viewer, int bookId)
{
    if (college.LibraryCopyTitle(bookId) is null
        || college.OpenBook(viewer.Name, false, false, bookId, LibraryOpenMode.Shelf) is not { } view)
        return false;

    viewer.Client.SendCollegeDisplay(
        LibraryBookArgs(view) with
        {
            Context = CollegePieceContext.LibraryCopy,
            CanTakeDown = false,
            CanReview = false,
            CanReport = false,
            CanCheckOut = false,
            Votes = []
        });

    return true;
}
```

If `CollegeDisplayArgs` is not a record, set the properties on the returned object instead of `with`. Add `IItemFactory itemFactory` to the `CollegePanel` primary constructor if it is not there (it is resolved from DI; check the registration needs no change).

- [ ] **Step 7: Item script.** Create `Chaos/Scripting/ItemScripts/LibraryCopyScript.cs`:

```csharp
using Chaos.Geometry.Abstractions;
using Chaos.Models.Panel;
using Chaos.Models.World;
using Chaos.Scripting.ItemScripts.Abstractions;
using Chaos.Services.College;

namespace Chaos.Scripting.ItemScripts;

public sealed class LibraryCopyScript(Item subject, CollegePanel panel, TimeProvider time) : ItemScriptBase(subject)
{
    public override bool CanBeDropped(Aisling source, Point targetPoint) => false;

    public override bool CanBeDroppedOn(Aisling source, Creature creature) => false;

    public override void OnUse(Aisling source)
    {
        if (LibraryCopies.ReturnExpired(source, time.GetUtcNow().UtcDateTime))
            return;

        if (Subject.LibraryBookId is { } id && panel.ShowLibraryCopy(source, id))
            return;

        source.Inventory.Remove(Subject.Slot);
        source.SendOrangeBarMessage(LibraryText.COPY_GONE);
    }
}
```

Match the real `Point` type and `Inventory.Remove` call used by other item scripts. The script key is `libraryCopy` (the convention strips `Script`; confirm against `PaintedPortraitScript` → `paintedPortrait`).

- [ ] **Step 8: Player hooks.** In `DefaultAislingScript`: add a field `private readonly IIntervalTimer LibraryCopyTimer = new IntervalTimer(TimeSpan.FromMinutes(1));` next to the other timers. In `OnLogin`, call `LibraryCopies.ReturnExpired(Subject, DateTime.UtcNow);` (use the same clock the script already uses for UTC times if it has one). In `Update`, `LibraryCopyTimer.Update(delta); if (LibraryCopyTimer.IntervalElapsed) LibraryCopies.ReturnExpired(Subject, DateTime.UtcNow);`.

- [ ] **Step 9:** Build the server solution and run `Library*Tests` → pass. Run the full College test folder once (`--treenode-filter "/*/Chaos.Tests.College/*/*"`) → pass.

```json:metadata
{"files": ["Chaos-Server/Chaos/Models/Panel/Item.cs", "Chaos-Server/Chaos.Schemas/Aisling/ItemSchema.cs", "Chaos-Server/Chaos/Services/MapperProfiles/ItemMapperProfile.cs", "Chaos-Server/Chaos/Utilities/RenamedItems.cs", "Chaos-Server/Chaos/Services/BeautyShop/GenderSwapService.cs", "Chaos-Server/Chaos/Services/College/LibraryRules.cs", "Chaos-Server/Chaos/Services/College/CollegeService.Library.cs", "Chaos-Server/Chaos/Services/College/LibraryCopies.cs", "Chaos-Server/Chaos/Services/College/LibraryText.cs", "Chaos-Server/Chaos/Services/College/CollegePanel.Library.cs", "Chaos-Server/Chaos/Services/College/CollegePanel.cs", "Chaos-Server/Chaos/Scripting/ItemScripts/LibraryCopyScript.cs", "Chaos-Server/Chaos/Scripting/AislingScripts/DefaultAislingScript.cs", "Chaos-Server/Tests/Chaos.Tests/College/LibraryCopiesTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj --no-build -- --treenode-filter \"/*/*/Library*Tests/*\"", "acceptanceCriteria": ["fields persist", "MayCheckOut rules", "CanCheckOut in Shelf view only", "checkout adds named copy with 24h expiry", "replace keeps one copy", "full bag refused", "ReturnExpired at expiry", "unshelved book removes copy on use", "copy view read-only with current text", "cannot be dropped", "login and minute checks"], "modelTier": "standard"}
```

---

### Task 6: Library Copy item template (Unora)

**Goal:** The `librarycopy` item template exists.

**Files:**
- Create: `Unora/Data/Configuration/Templates/Items/librarycopy.json`

**Acceptance Criteria:**
- [ ] Template key `librarycopy`, name "Library Copy", panel sprite 625 (the recipe book icon), weight 1, `maxStacks` 1, script key `libraryCopy`.
- [ ] Not tradeable, not bankable, not sellable to shops, using the exact JSON key names the item template schema uses (check `ItemTemplateSchema` in the server for `NoTrade`, `PreventBanking` and `NoNpcSale` or their equivalents).
- [ ] The server loads it without errors (run the server's template-load test if one exists, or the server's JSON schema check).

**Verify:** JSON parses (`python -c "import json;json.load(open('Data/Configuration/Templates/Items/librarycopy.json'))"` from the Unora root) and key names match `ItemTemplateSchema`.

**Steps:**

- [ ] **Step 1:** Write:

```json
{
  "category": "Misc",
  "color": "Default",
  "gender": "Unisex",
  "maxStacks": 1,
  "weight": 1,
  "description": "A copy from the Mileth College Library. It returns itself after a day.",
  "name": "Library Copy",
  "noTrade": true,
  "panelSprite": 625,
  "preventBanking": true,
  "scriptKeys": [
    "libraryCopy"
  ],
  "scriptVars": {},
  "templateKey": "librarycopy"
}
```

- [ ] **Step 2:** Add the no-shop-sale key if `ItemTemplateSchema` has one (for example `"noNpcSale": true`), with its exact name.

```json:metadata
{"files": ["Unora/Data/Configuration/Templates/Items/librarycopy.json"], "verifyCommand": "python -c \"import json;json.load(open('Data/Configuration/Templates/Items/librarycopy.json'))\"", "acceptanceCriteria": ["template key librarycopy", "sprite 625", "no trade/bank/shop sale", "script key libraryCopy"], "modelTier": "mechanical"}
```

---

### Task 7: Check out button and copy view (client)

**Goal:** The Library book window shows Check out when allowed and an empty footer for a copy.

**Files:**
- Modify: `Chaos.Client/Controls/World/Popups/College/CollegeReaderControl.cs`
- Test: existing reader footer tests if any (search `CollegeReaderControl` under `Tests/Chaos.Client.Tests`)

**Acceptance Criteria:**
- [ ] In context `LibraryShelf`, when `args.CanCheckOut`, a "Check out" footer button sends `CollegeActionType.LibraryCheckOut` with the book id (through the same `SendLibrary` helper Report uses).
- [ ] Context `LibraryCopy` shows the book with "by <author>" like `LibraryShelf`, and no footer buttons.
- [ ] Every switch over `CollegePieceContext` in the client that lists `LibraryShelf` treats `LibraryCopy` the same for display (header/author line, read-only piece), checked by searching `CollegePieceContext.LibraryShelf` under `Chaos.Client/`.
- [ ] Client builds against the updated server submodule; client tests pass.

**Verify:** `dotnet build` and client tests → pass.

**Steps:**

- [ ] **Step 1:** In the footer switch, in `case CollegePieceContext.LibraryShelf:`, add first: `if (args.CanCheckOut) FooterAction("Check out", 90, () => SendLibrary(CollegeActionType.LibraryCheckOut));`. Add `case CollegePieceContext.LibraryCopy: break;`.
- [ ] **Step 2:** Add `CollegePieceContext.LibraryCopy` next to `LibraryShelf` in the author-line pattern (line ~311) and in every other pattern found by the search.
- [ ] **Step 3:** Build and run client tests.

```json:metadata
{"files": ["Chaos.Client/Controls/World/Popups/College/CollegeReaderControl.cs"], "verifyCommand": "dotnet build && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj --no-build", "acceptanceCriteria": ["Check out button when CanCheckOut", "LibraryCopy read-only footer", "LibraryCopy displayed like LibraryShelf", "build + tests pass"], "modelTier": "mechanical"}
```

---

### Task 8: Commit the full implementation

**Goal:** One commit per repo holding this plan's changes.

**Files:** all files above.

**Acceptance Criteria:**
- [ ] Server commit on a branch in `Chaos-Server` with Tasks 1, 2, 4, 5.
- [ ] Unora commit with Task 6.
- [ ] Client commit with Tasks 3, 7 and the submodule pointer at the server commit.
- [ ] Only this plan's files staged (explicit paths; shared checkouts).

**Verify:** `git show --stat HEAD` in each repo lists only this plan's files.

**Steps:**

- [ ] **Step 1:** In each repo, `git status`, stage this plan's files by path, commit with a message ending in the `Co-Authored-By` line.

```json:metadata
{"files": [], "verifyCommand": "git show --stat HEAD", "acceptanceCriteria": ["server commit", "Unora commit", "client commit with submodule pointer", "only plan files"], "modelTier": "mechanical"}
```
