# Mileth College Word Games (Hangman and Word Scramble) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add two College class tools, Hangman (shared turn-based board with a buzzer) and Word Scramble (a race), played from Teacher word lists, a shared word bank or a quick list typed at the lectern.

**Architecture:** Each game copies the quiz layer by layer: a pure rule class (`HangmanSession`, `ScrambleSession`) on a live object kept on the running class, `CollegeService` methods that hold the lock and apply College rules, `CollegePanel` that turns packets into service calls and service views into per-viewer packets, lectern dialog steps, and client windows fed by `CollegeDisplay`. Word lists copy the saved-quiz stack (model, rules, store, service, editor).

**Tech Stack:** C# 14 / .NET 10, TUnit + FluentAssertions, MonoGame client, Chaos-Server networking converters, Unora JSON dialog templates.

**Spec:** `docs/superpowers/specs/2026-10-12-mileth-college-word-games-design.md` (client repo, commit b5d2e95b1). Read it before any task. Where this plan and the spec disagree, the spec wins unless the plan lists the change under "Plan-time adjustments".

## Global Constraints

- Repos and worktrees (Task 0 makes them, branch `feat/college-word-games` in each):
  - Server: `C:\Users\Michael\Documents\GitHub\worktrees\word-games-server` (from the `Chaos.Client\Chaos-Server` repo).
  - Unora: `C:\Users\Michael\Documents\GitHub\worktrees\word-games-unora`.
  - Client: `C:\Users\Michael\Documents\GitHub\worktrees\word-games-client`.
  - File server: `C:\Users\Michael\Documents\GitHub\worktrees\word-games-fs` (from `Chaos.FileServer`).
- **Never edit the shared checkouts** (`Chaos.Client`, `Chaos.Client\Chaos-Server`, `Unora`, `Chaos.FileServer`). Other sessions work there. Use absolute worktree paths with the built-in Read/Edit/Write tools; do not use Serena edit tools in worktrees (a past implementer edited the main checkout through Serena by mistake).
- **Do not commit.** Leave all changes in the worktree; the final task makes one commit per repo.
- `CLIENT_VERSION` becomes **782** (from 781) in `Chaos.DarkAges/Definitions/CONSTANTS.cs`, and the pinned value in `Tests/Chaos.Tests/Networking/GuildEmblemPacketConverterTests.cs` changes with it.
- Wire numbers (append only; never renumber existing values):
  - `CollegeTool`: `Hangman = 2`, `Scramble = 3`.
  - `CollegeActionType`: `WordListOpen = 24`, `WordListSave = 25`, `WordListDelete = 26`, `WordListCopy = 27`, `WordBankPage = 28`, `WordBankCopy = 29`, `QuickListStart = 30`, `HangmanOrder = 31`, `HangmanPick = 32`, `HangmanBuzz = 33`, `HangmanSolve = 34`, `HangmanControl = 35`, `ScrambleGuess = 36`, `ScrambleControl = 37`, `WordGameCancel = 38`.
  - `CollegeDisplayType`: `WordLists = 19`, `WordListEdit = 20`, `WordBank = 21`, `WordListResult = 22`, `HangmanOrder = 23`, `HangmanBoard = 24`, `ScrambleCard = 25`.
- Limits (new constants in `CollegeProtocol`): `MAX_WORD_LIST_TITLE_CHARS = 40`, `MIN_LIST_WORDS = 5`, `MAX_LIST_WORDS = 30`, `MIN_QUICK_WORDS = 1`, `MAX_QUICK_WORDS = 10`, `MIN_WORD_CHARS = 3`, `MAX_WORD_CHARS = 20`, `MAX_HINT_CHARS = 60`, `MAX_WORD_LISTS_PER_OWNER = 20`, `WORD_BANK_PAGE_ROWS = 10`, `WORD_GAME_GUARD_WORDS = 5`, `HANGMAN_TURN_SECONDS = 20`, `HANGMAN_SOLVE_SECONDS = 10`, `HANGMAN_MAX_MISSES = 6`, `HANGMAN_LETTER_POINTS = 1`, `HANGMAN_SOLVE_POINTS = 3`, `SCRAMBLE_GUESS_GAP_MS = 500`, `MAX_GAME_EVENTS = 3`. Scramble seconds come from the existing `QuizSeconds`.
- Scramble points by finish order: 1st 3, 2nd 2, every later 1.
- A word is upper-case A-Z with single spaces, 3-20 characters, not starting or ending with a space. Hints up to 60 characters. Comparison of answers uses `WordListRules.Normalize`.
- Attendance and bonus follow the quiz exactly (`MAX_BONUS_STUDENTS = 3`, one bonus per student per class, only when the guard of 5 ended words is met).
- Dialog text limits (Unora): option text 35 characters, dialog text 360 characters, no em dashes.
- Test commands (TUnit; never `dotnet test`):
  - Server: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/<Class>/*"` from the server worktree. Full suite: same without the filter. Baseline is 0 failures.
  - Client: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\word-games-server -- --no-ansi` from the client worktree.
- Match the surrounding code: XML summary comments only where the quiz code has them; no explanatory comments in tests.

**User decisions (already made):**
- Hangman is a shared board; Word Scramble is a race on each student's own card.
- Words come from Teacher-written lists (20 each), plus a shared Word bank (copy to mine), plus a quick list typed at the lectern (1-10 words, not saved).
- Rewards work like the quiz: "Counts for attendance" and "Bonus", 5-word guard.
- Hangman: Teacher sets a turn order at start that carries across words; 20 s turns, skipped if absent; right letter 1 point; Buzz to solve out of turn, first buzz gets 10 s while the board pauses, right 3 points, wrong counts as a miss and locks that player out for the word.
- Hangman attendance: turns taken × 2 ≥ turns given; players left off the list are excused.
- Casual (no miss limit) and Survival (6 misses loses the word) modes; no teams.
- Scramble: Teacher picks seconds from 15/20/30/45/60; unlimited guesses; only right/wrong shown; points 3/2/1; spaces kept; scramble never equals the word.
- Screens: Hangman board centred like the quiz card (mockup option A); Scramble card as mocked; Teacher turn-order window and side panel as mocked; word-list editor two pages like the quiz editor with Mine / Word bank tabs.
- Spec review accepted the agent's own choices: chat spoiler block, only listed players play Hangman, last-letter reveal scores only the letter point, wrong buzz counts toward Survival misses, early Scramble reveal, 2 guesses per second cap, quick lists of 1-10 words.

---

## File structure

Server worktree (`word-games-server`):

| File | Change | Responsibility |
|---|---|---|
| `Chaos.DarkAges/Definitions/Enums.cs` | modify | new enums and enum values |
| `Chaos.DarkAges/Definitions/CollegeProtocol.cs` | modify | new limits |
| `Chaos.DarkAges/Definitions/CONSTANTS.cs` | modify | CLIENT_VERSION 782 |
| `Chaos.Networking/Entities/Server/CollegeInfos.cs` | modify | new info records |
| `Chaos.Networking/Entities/Client/CollegeActionArgs.cs` | modify | new fields |
| `Chaos.Networking/Entities/Server/CollegeDisplayArgs.cs` | modify | new fields |
| `Chaos.Networking/Converters/CollegeCodec.cs` | modify | read/write of new infos |
| `Chaos.Networking/Converters/Client/CollegeActionConverter.cs` | modify | new action types |
| `Chaos.Networking/Converters/Server/CollegeDisplayConverter.cs` | modify | new display types |
| `Chaos/Services/College/CollegeWordList.cs` | create | `CollegeWord`, `CollegeWordList`, `CollegeWordListIndex` |
| `Chaos/Services/College/WordListRules.cs` | create | normalise, check save, check quick list, spoiler match |
| `Chaos/Services/College/CollegeWordListStore.cs` | create | `ICollegeWordListStore` + file store |
| `Chaos/Services/College/CollegeService.WordLists.cs` | create | list/save/delete/copy/bank |
| `Chaos/Services/College/WordGameScores.cs` | create | top scorers and leaders over a score map |
| `Chaos/Services/College/HangmanSession.cs` | create | Hangman rules |
| `Chaos/Services/College/ScrambleSession.cs` | create | Scramble rules |
| `Chaos/Services/College/CollegeTools.cs` | modify | `LiveHangman`, `LiveScramble` |
| `Chaos/Services/College/CollegeState.cs` | modify | `CollegeClass.Hangman/Scramble`, `CollegeState.WordLists` |
| `Chaos/Services/College/ClassSession.cs` | modify | `Shift` moves the new clocks |
| `Chaos/Services/College/CollegeService.WordGames.cs` | create | start, play, control, tick, views, spoiler block |
| `Chaos/Services/College/CollegeService.Tools.cs` | modify | status, refusal, views, attendance and bonus over four tools |
| `Chaos/Services/College/CollegeService.cs` | modify | `FinishLocked` ends and scores the new games; store injection |
| `Chaos/Services/College/CollegeText.cs` | modify | new texts |
| `Chaos/Services/College/CollegePanel.cs` | modify | new actions and per-viewer displays |
| `Chaos/Scripting/DialogScripts/Temuair/College/CollegeLecternScript.cs` | modify | Start Hangman / Start Scramble / My word lists |
| Registrar dialog script (the one with "My quizzes") | modify | My word lists |
| `Chaos/Services/Servers/WorldServer.cs` (public-message handler next to `FloorBlock`) | modify | spoiler block |
| `Tests/Chaos.Tests/College/*` | create/modify | tests per task |

Unora worktree: `Data/Configuration/Templates/Dialogs/Temauir/mileth/college/college_lectern_{hangman,scramble}_*.json`, `college_lectern_wordlists.json`, the registrar's dialog, and `college_lectern.json` menus.

Client worktree (`Chaos.Client/`):

| File | Change |
|---|---|
| `ViewModel/College/WordListDocument.cs` | create |
| `ViewModel/College/HangmanBoardState.cs` | create |
| `ViewModel/College/ClassToolText.cs` | modify |
| `Controls/World/Popups/College/WordListEditorControl.cs` | create |
| `Controls/World/Popups/College/HangmanTurnOrderControl.cs` | create |
| `Controls/World/Popups/College/HangmanBoardControl.cs` | create |
| `Controls/World/Popups/College/HangmanTeacherPanel.cs` | create |
| `Controls/World/Popups/College/ScrambleCardControl.cs` | create |
| `Controls/World/Popups/College/ScrambleTeacherPanel.cs` | create |
| `Screens/WorldScreen.College.cs` | modify |
| `Tests/Chaos.Client.Tests/College/*Tests.cs` | create |
| `CLAUDE.md` (UI Control System, College list) | modify |

File server worktree: `AislingRenamer.College.cs` and its test.

---

### Task 0: Create the worktrees

**Goal:** Four git worktrees on branch `feat/college-word-games`, the client one pointing its `Chaos-Server` gitlink at the server worktree's commit.

**Files:**
- Create: the four worktree folders listed in Global Constraints.

**Acceptance Criteria:**
- [ ] `git -C <each worktree> branch --show-current` prints `feat/college-word-games`.
- [ ] The server worktree builds: `dotnet build Chaos/Chaos.csproj` succeeds.
- [ ] The client worktree builds against it with `-p:UnoraServerPath=<server worktree>`.

**Verify:** `git -C C:/Users/Michael/Documents/GitHub/worktrees/word-games-server branch --show-current` → `feat/college-word-games`

**Steps:**

- [ ] **Step 1: Create the worktrees.** Unora needs `-c core.longpaths=true` (one template path is too long); pass it with `-c` only, never `git config`.

```bash
cd C:/Users/Michael/Documents/GitHub
git -C Chaos.Client/Chaos-Server -c core.longpaths=true worktree add -b feat/college-word-games ../../worktrees/word-games-server master
git -C Unora -c core.longpaths=true worktree add -b feat/college-word-games ../worktrees/word-games-unora main
git -C Chaos.Client worktree add -b feat/college-word-games ../worktrees/word-games-client main
git -C Chaos.FileServer worktree add -b feat/college-word-games ../worktrees/word-games-fs master
```

- [ ] **Step 2: Build both code worktrees.**

```bash
cd C:/Users/Michael/Documents/GitHub/worktrees/word-games-server && dotnet build Chaos/Chaos.csproj -v q
cd C:/Users/Michael/Documents/GitHub/worktrees/word-games-client && dotnet build Chaos.Client/Chaos.Client.csproj -v q -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/word-games-server
```

Expected: both `Build succeeded`. The client worktree's `Chaos-Server` submodule folder stays empty; that is normal.

```json:metadata
{"files": [], "verifyCommand": "git -C C:/Users/Michael/Documents/GitHub/worktrees/word-games-server branch --show-current", "acceptanceCriteria": ["four worktrees on feat/college-word-games", "server worktree builds", "client worktree builds against server worktree"], "modelTier": "mechanical"}
```

---

### Task 1: Protocol: enums, limits, packets, CLIENT_VERSION 782 (server repo)

**Goal:** Every new wire type, field and converter case, with round-trip tests, so later tasks only fill behaviour.

**Files:**
- Modify: `Chaos.DarkAges/Definitions/Enums.cs`, `Chaos.DarkAges/Definitions/CollegeProtocol.cs`, `Chaos.DarkAges/Definitions/CONSTANTS.cs`
- Modify: `Chaos.Networking/Entities/Server/CollegeInfos.cs`, `Chaos.Networking/Entities/Client/CollegeActionArgs.cs`, `Chaos.Networking/Entities/Server/CollegeDisplayArgs.cs`
- Modify: `Chaos.Networking/Converters/CollegeCodec.cs`, `Chaos.Networking/Converters/Client/CollegeActionConverter.cs`, `Chaos.Networking/Converters/Server/CollegeDisplayConverter.cs`
- Modify: `Tests/Chaos.Tests/Networking/CollegePacketConverterTests.cs`, `Tests/Chaos.Tests/Networking/GuildEmblemPacketConverterTests.cs`

**Acceptance Criteria:**
- [ ] Enum values and constants match Global Constraints exactly.
- [ ] Each of the 15 new action types and 7 new display types round-trips through its converter with every field it uses.
- [ ] `CLIENT_VERSION` is 782 and the pinned test passes.
- [ ] `CollegePacketConverterTests` and `GuildEmblemPacketConverterTests` pass.

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/CollegePacketConverterTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Enums** in `Enums.cs`, inside the College region next to `CollegeTool`/`QuizResultCode`:

```csharp
public enum CollegeTool : byte
{
    Quiz = 0,
    Debate = 1,
    Hangman = 2,
    Scramble = 3
}

public enum HangmanMode : byte
{
    Casual = 0,
    Survival = 1
}

public enum HangmanPhase : byte
{
    Waiting = 0,
    Open = 1,
    Solving = 2,
    Revealed = 3,
    Ended = 4
}

public enum HangmanOutcome : byte
{
    None = 0,
    Solved = 1,
    Lost = 2
}

public enum ScramblePhase : byte
{
    Waiting = 0,
    Open = 1,
    Revealed = 2,
    Ended = 3
}

public enum HangmanControlType : byte
{
    Next = 0,
    Skip = 1,
    AddPlayer = 2,
    RemovePlayer = 3,
    End = 4
}

public enum ScrambleControlType : byte
{
    Next = 0,
    End = 1
}

/// <summary>How the word-list editor opens: one of the viewer's lists, a bank list read-only, or a quick list for one game.</summary>
public enum WordListEditMode : byte
{
    Saved = 0,
    Preview = 1,
    Quick = 2
}

public enum WordListResultCode : byte
{
    Saved = 0,
    Deleted = 1,
    Copied = 2,
    Refused = 3
}
```

Append to `CollegeActionType` and `CollegeDisplayType` the values in Global Constraints, each with a one-line `/// <summary>` in the style of the existing members (e.g. `/// <summary>The Teacher's turn order: Names in order; starts the pending Hangman.</summary>`).

- [ ] **Step 2: Limits** in `CollegeProtocol` after `DEBATE_GUARD_SPEAKERS`, every constant from Global Constraints, e.g.:

```csharp
    public const int MAX_WORD_LIST_TITLE_CHARS = 40;
    public const int MIN_LIST_WORDS = 5;
    public const int MAX_LIST_WORDS = 30;
    public const int MIN_QUICK_WORDS = 1;
    public const int MAX_QUICK_WORDS = 10;
    public const int MIN_WORD_CHARS = 3;
    public const int MAX_WORD_CHARS = 20;
    public const int MAX_HINT_CHARS = 60;
    public const int MAX_WORD_LISTS_PER_OWNER = 20;
    public const int WORD_BANK_PAGE_ROWS = 10;
    public const int WORD_GAME_GUARD_WORDS = 5;
    public const int HANGMAN_TURN_SECONDS = 20;
    public const int HANGMAN_SOLVE_SECONDS = 10;
    public const int HANGMAN_MAX_MISSES = 6;
    public const int HANGMAN_LETTER_POINTS = 1;
    public const int HANGMAN_SOLVE_POINTS = 3;
    public const int SCRAMBLE_GUESS_GAP_MS = 500;
    public const int MAX_GAME_EVENTS = 3;
```

and `CONSTANTS.CLIENT_VERSION = 782`.

- [ ] **Step 3: Info records** in `CollegeInfos.cs` (same namespace and style as `CollegeQuizInfo`):

```csharp
public sealed record CollegeWordInfo
{
    public string Text { get; set; } = string.Empty;
    public string Hint { get; set; } = string.Empty;
}

public sealed record CollegeWordListDraftInfo
{
    public string Title { get; set; } = string.Empty;
    public List<CollegeWordInfo> Words { get; set; } = [];
}

public sealed record CollegeWordListRowInfo
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public byte Count { get; set; }
}

public sealed record CollegeHangmanPlayerInfo
{
    public string Name { get; set; } = string.Empty;
    public byte Score { get; set; }
    public bool Removed { get; set; }
    public bool IsTurn { get; set; }
    public bool LockedOut { get; set; }
}

public sealed record CollegeHangmanInfo
{
    public string Title { get; set; } = string.Empty;
    public byte Number { get; set; }
    public byte Count { get; set; }
    public HangmanMode Mode { get; set; }
    public HangmanPhase Phase { get; set; }
    public HangmanOutcome Outcome { get; set; }
    public bool IsTeacher { get; set; }

    /// <summary>The whole word: for the Teacher always, for everyone else only once the word has ended.</summary>
    public string Word { get; set; } = string.Empty;

    /// <summary>The word with unrevealed letters as '_' and spaces kept.</summary>
    public string Mask { get; set; } = string.Empty;

    public string Hint { get; set; } = string.Empty;
    public string Tried { get; set; } = string.Empty;
    public byte Misses { get; set; }
    public List<CollegeHangmanPlayerInfo> Players { get; set; } = [];
    public byte TurnSecondsLeft { get; set; }
    public string Solver { get; set; } = string.Empty;
    public byte SolveSecondsLeft { get; set; }

    /// <summary>The viewer is on the list, not removed, and may buzz on this word.</summary>
    public bool CanBuzz { get; set; }

    public bool MyTurn { get; set; }
    public bool ISolve { get; set; }
    public byte MyScore { get; set; }
    public List<string> Events { get; set; } = [];
    public List<CollegeScoreInfo> Top { get; set; } = [];

    /// <summary>Teacher only: students in the room who are not on the list.</summary>
    public List<string> Addable { get; set; } = [];
}

public sealed record CollegeScrambleInfo
{
    public string Title { get; set; } = string.Empty;
    public byte Number { get; set; }
    public byte Count { get; set; }
    public ScramblePhase Phase { get; set; }
    public bool IsTeacher { get; set; }
    public string Scrambled { get; set; } = string.Empty;

    /// <summary>The Teacher always; everyone else after the reveal.</summary>
    public string Word { get; set; } = string.Empty;

    public string Hint { get; set; } = string.Empty;
    public byte Seconds { get; set; }
    public byte SecondsLeft { get; set; }
    public byte SolvedCount { get; set; }

    /// <summary>Students in the room, not counting the Teacher.</summary>
    public byte Students { get; set; }

    /// <summary>The viewer's finish place on this word, 0 when not solved.</summary>
    public byte MyPlace { get; set; }

    public byte MyScore { get; set; }

    /// <summary>Set only on the card sent back to a student whose guess was wrong.</summary>
    public bool WrongGuess { get; set; }

    /// <summary>Solvers in finish order: the Teacher always, everyone else after the reveal.</summary>
    public List<string> Solvers { get; set; } = [];

    public List<CollegeScoreInfo> Top { get; set; } = [];
}
```

- [ ] **Step 4: Args fields.** In `CollegeActionArgs` add:

```csharp
    /// <summary>WordListOpen (empty asks for the list), WordListSave (empty for a new list), WordListDelete, WordListCopy, WordBankCopy.</summary>
    public string WordListId { get; set; } = string.Empty;

    /// <summary>WordListSave, QuickListStart.</summary>
    public CollegeWordListDraftInfo? WordListDraft { get; set; }

    /// <summary>WordBankPage: 0-based.</summary>
    public byte Page { get; set; }

    /// <summary>HangmanPick: 'A'-'Z'.</summary>
    public byte Letter { get; set; }

    public HangmanControlType HangmanControl { get; set; }
    public ScrambleControlType ScrambleControl { get; set; }
```

`Text` carries HangmanSolve / ScrambleGuess text; `Names` carries the HangmanOrder list and the one name of AddPlayer/RemovePlayer. Update the `Text` and `Names` summaries to say so.

In `CollegeDisplayArgs` add:

```csharp
    public List<CollegeWordListRowInfo> WordListRows { get; set; } = [];

    /// <summary>WordListEdit, WordListResult.</summary>
    public string WordListId { get; set; } = string.Empty;

    /// <summary>WordListEdit (empty words for a new list).</summary>
    public CollegeWordListDraftInfo? WordListDraft { get; set; }

    public WordListEditMode WordListMode { get; set; }

    /// <summary>WordListResult; its text is <see cref="Message" />.</summary>
    public WordListResultCode WordListResult { get; set; }

    /// <summary>WordBank.</summary>
    public byte Page { get; set; }

    public byte PageCount { get; set; }

    /// <summary>HangmanOrder: everyone in the room except the Teacher.</summary>
    public List<string> OrderNames { get; set; } = [];

    public CollegeHangmanInfo Hangman { get; set; } = new();
    public CollegeScrambleInfo Scramble { get; set; } = new();
```

`Reopen` applies to HangmanBoard and ScrambleCard too; `Message` carries the HangmanOrder subtitle ("8 words · Survival"). Update those summaries.

- [ ] **Step 5: Codec and converters.** In `CollegeCodec` add `WriteWord/ReadWord`, `WriteWordListDraft/ReadWordListDraft`, `WriteWordListRow/ReadWordListRow`, `WriteHangman/ReadHangman`, `WriteScramble/ReadScramble`, written in field order exactly as declared above, using the same primitives the quiz info uses (strings as the codec's string writer, bytes, booleans, lists with a byte count, `CollegeScoreInfo` via the existing score writer). In `CollegeActionConverter.Deserialize/Serialize` add a case per new action type:

| Action | Fields after Type |
|---|---|
| WordListOpen, WordListDelete, WordListCopy, WordBankCopy | WordListId |
| WordListSave | WordListId, WordListDraft |
| WordBankPage | Page |
| QuickListStart | WordListDraft |
| HangmanOrder | Names |
| HangmanPick | Letter |
| HangmanBuzz, WordGameCancel | (none) |
| HangmanSolve, ScrambleGuess | Text |
| HangmanControl | HangmanControl, Names |
| ScrambleControl | ScrambleControl |

In `CollegeDisplayConverter` add:

| Display | Fields after Type |
|---|---|
| WordLists | WordListRows |
| WordListEdit | WordListId, WordListMode, WordListDraft, Message |
| WordBank | Page, PageCount, WordListRows |
| WordListResult | WordListId, WordListResult, Message |
| HangmanOrder | Message, OrderNames |
| HangmanBoard | Reopen, Hangman |
| ScrambleCard | Reopen, Scramble |

(`WordListEdit`'s `Message` is the editor's status line, e.g. the "fewer than 5 words" note for quick mode.) Lists use the converter's existing list pattern; cap each list's count at `byte.MaxValue` on write, as the quiz rows do.

- [ ] **Step 6: Tests.** In `CollegePacketConverterTests`, add one round-trip test per new action and per new display, built like the existing quiz round-trip tests (serialize with the converter, deserialize, `Should().BeEquivalentTo(original)`). Fill every field the type sends with a non-default value, e.g.:

```csharp
    [Test]
    public void HangmanBoard_round_trips()
    {
        var args = new CollegeDisplayArgs
        {
            Type = CollegeDisplayType.HangmanBoard,
            Reopen = true,
            Hangman = new CollegeHangmanInfo
            {
                Title = "Mileth",
                Number = 2,
                Count = 8,
                Mode = HangmanMode.Survival,
                Phase = HangmanPhase.Solving,
                Outcome = HangmanOutcome.None,
                IsTeacher = true,
                Word = "GLIOCA",
                Mask = "GL__CA",
                Hint = "a goddess of Mileth",
                Tried = "AGCLERS",
                Misses = 3,
                Players = [new CollegeHangmanPlayerInfo { Name = "Aroha", Score = 3, IsTurn = true }, new CollegeHangmanPlayerInfo { Name = "Kael", Removed = true, LockedOut = true }],
                TurnSecondsLeft = 14,
                Solver = "Kael",
                SolveSecondsLeft = 8,
                CanBuzz = true,
                MyTurn = true,
                ISolve = true,
                MyScore = 2,
                Events = ["Aroha found L."],
                Top = [new CollegeScoreInfo { Name = "Aroha", Score = 3 }],
                Addable = ["Dorn"]
            }
        };

        RoundTrip(args).Should().BeEquivalentTo(args);
    }
```

(Use the file's existing round-trip helper; if it has none, write `RoundTrip` the way its quiz tests serialize and read back.) Update `GuildEmblemPacketConverterTests` line pinning 781 to 782.

- [ ] **Step 7: Run** the Verify command, then the full suite once. Expected: all pass.

```json:metadata
{"files": ["Chaos.DarkAges/Definitions/Enums.cs", "Chaos.DarkAges/Definitions/CollegeProtocol.cs", "Chaos.DarkAges/Definitions/CONSTANTS.cs", "Chaos.Networking/Entities/Server/CollegeInfos.cs", "Chaos.Networking/Entities/Client/CollegeActionArgs.cs", "Chaos.Networking/Entities/Server/CollegeDisplayArgs.cs", "Chaos.Networking/Converters/CollegeCodec.cs", "Chaos.Networking/Converters/Client/CollegeActionConverter.cs", "Chaos.Networking/Converters/Server/CollegeDisplayConverter.cs", "Tests/Chaos.Tests/Networking/CollegePacketConverterTests.cs", "Tests/Chaos.Tests/Networking/GuildEmblemPacketConverterTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/CollegePacketConverterTests/*\"", "acceptanceCriteria": ["enum values and constants match Global Constraints", "every new action and display round-trips", "CLIENT_VERSION 782 and pinned test passes", "converter tests pass"], "modelTier": "standard"}
```

---

### Task 2: Word lists on the server: model, rules, store, service (server repo)

**Goal:** Teachers can list, save, delete and copy their own word lists, page the Word bank, and copy any bank list; quick lists can be checked.

**Files:**
- Create: `Chaos/Services/College/CollegeWordList.cs`, `Chaos/Services/College/WordListRules.cs`, `Chaos/Services/College/CollegeWordListStore.cs`, `Chaos/Services/College/CollegeService.WordLists.cs`
- Modify: `Chaos/Services/College/CollegeState.cs` (add `public List<CollegeWordListIndex> WordLists { get; set; } = [];` next to `Quizzes`), `Chaos/Services/College/CollegeService.cs` (constructor takes `ICollegeWordListStore WordLists`, like `ICollegeQuizStore Quizzes`), the DI registration that registers `CollegeQuizStore` (register `CollegeWordListStore` the same way), `Chaos/Services/College/CollegeText.cs`
- Create: `Tests/Chaos.Tests/College/WordListRulesTests.cs`, `Tests/Chaos.Tests/College/MemoryWordListStore.cs`, `Tests/Chaos.Tests/College/CollegeWordListServiceTests.cs`
- Modify: every test helper that constructs `CollegeService` (search `new CollegeService(`) to pass a `MemoryWordListStore`.

**Acceptance Criteria:**
- [ ] `WordListRules.Normalize(" mileth   village ")` returns `"MILETH VILLAGE"`.
- [ ] Saving refuses: title empty or over 40; fewer than 5 or more than 30 words; a word under 3 or over 20 characters or with anything but A-Z and single spaces; a hint over 60; duplicate words; a 21st list.
- [ ] Copy of an own list and bank copy of another Teacher's list create a new list owned by the caller titled "Copy of …" cut to 40, counted against 20.
- [ ] Only the owner may delete or overwrite; bank pages hold 10 rows sorted by owner then title.
- [ ] `WordListRulesTests` and `CollegeWordListServiceTests` pass; the full College test folder still passes.

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/CollegeWordListServiceTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Model** (`CollegeWordList.cs`), mirroring `CollegeQuiz.cs`:

```csharp
namespace Chaos.Services.College;

public sealed class CollegeWordList
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public List<CollegeWord> Words { get; set; } = [];
}

public sealed class CollegeWord
{
    public string Text { get; set; } = "";
    public string Hint { get; set; } = "";
}

/// <summary>One saved list as the College index sees it, so list screens never open the list files.</summary>
public sealed class CollegeWordListIndex
{
    public string Id { get; set; } = "";
    public string Owner { get; set; } = "";
    public string Title { get; set; } = "";
    public int Count { get; set; }
}
```

- [ ] **Step 2: Write the failing rules tests** (`WordListRulesTests.cs`):

```csharp
using Chaos.Services.College;
using FluentAssertions;

namespace Chaos.Tests.College;

public sealed class WordListRulesTests
{
    private static CollegeWordList List(params string[] words)
        => new() { Title = "Mileth", Words = words.Select(w => new CollegeWord { Text = w }).ToList() };

    private static CollegeWordList Ready() => List("GLIOCA", "MILETH", "DANAAN", "SGRIOS", "MILETH VILLAGE");

    //formatter:off
    [Test]
    [Arguments(" mileth   village ", "MILETH VILLAGE")]
    [Arguments("Glioca", "GLIOCA")]
    [Arguments("", "")]
    //formatter:on
    public void Normalize_trims_collapses_and_upper_cases(string text, string expected)
        => WordListRules.Normalize(text).Should().Be(expected);

    [Test]
    public void A_ready_list_passes() => WordListRules.CheckSave(Ready()).IsOk.Should().BeTrue();

    [Test]
    public void Save_normalizes_words_and_trims_hints()
    {
        var list = List("glioca", "mileth", "danaan", "sgrios", "mileth  village");
        list.Words[0].Hint = "  a goddess  ";

        WordListRules.Normalize(list);

        list.Words.Select(w => w.Text).Should().Equal("GLIOCA", "MILETH", "DANAAN", "SGRIOS", "MILETH VILLAGE");
        list.Words[0].Hint.Should().Be("a goddess");
    }

    //formatter:off
    [Test]
    [Arguments("AB", WordListCheck.WordLength)]
    [Arguments("ABCDEFGHIJKLMNOPQRSTU", WordListCheck.WordLength)]
    [Arguments("MILETH1", WordListCheck.WordLetters)]
    [Arguments("ÉCOLE", WordListCheck.WordLetters)]
    //formatter:on
    public void Bad_words_are_refused(string word, WordListCheck expected)
    {
        var list = Ready();
        list.Words[0].Text = word;

        WordListRules.CheckSave(list).Check.Should().Be(expected);
    }

    [Test]
    public void Too_few_and_too_many_words_are_refused()
    {
        WordListRules.CheckSave(List("GLIOCA", "MILETH", "DANAAN", "SGRIOS")).Check.Should().Be(WordListCheck.WordCount);
        WordListRules.CheckSave(List(Enumerable.Range(0, 31).Select(i => "WORD" + (char)('A' + i % 26) + (char)('A' + i / 26)).ToArray()))
                     .Check.Should().Be(WordListCheck.WordCount);
    }

    [Test]
    public void Duplicates_long_hints_and_bad_titles_are_refused()
    {
        WordListRules.CheckSave(List("GLIOCA", "GLIOCA", "DANAAN", "SGRIOS", "MILETH")).Check.Should().Be(WordListCheck.Duplicate);

        var hint = Ready();
        hint.Words[1].Hint = new string('x', 61);
        WordListRules.CheckSave(hint).Check.Should().Be(WordListCheck.HintLength);

        var title = Ready();
        title.Title = " ";
        WordListRules.CheckSave(title).Check.Should().Be(WordListCheck.Title);
        title.Title = new string('t', 41);
        WordListRules.CheckSave(title).Check.Should().Be(WordListCheck.Title);
    }

    [Test]
    public void Quick_lists_take_one_to_ten_words_and_no_title()
    {
        var one = List("GLIOCA");
        one.Title = "";
        WordListRules.CheckQuick(one).IsOk.Should().BeTrue();

        var eleven = List(Enumerable.Range(0, 11).Select(i => "WORD" + (char)('A' + i)).ToArray());
        WordListRules.CheckQuick(eleven).Check.Should().Be(WordListCheck.WordCount);
        WordListRules.CheckQuick(List()).Check.Should().Be(WordListCheck.WordCount);
    }

    //formatter:off
    [Test]
    [Arguments("the answer is glioca!", "GLIOCA", true)]
    [Arguments("mileth village", "MILETH VILLAGE", true)]
    [Arguments("milethvillage", "MILETH VILLAGE", true)]
    [Arguments("g l i o c a", "GLIOCA", true)]
    [Arguments("no idea", "GLIOCA", false)]
    //formatter:on
    public void Spoils_matches_ignoring_case_and_spaces(string message, string answer, bool expected)
        => WordListRules.Spoils(message, answer).Should().Be(expected);
}
```

Run: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/WordListRulesTests/*"`. Expected: build fails (`WordListRules` missing).

- [ ] **Step 3: Rules** (`WordListRules.cs`):

```csharp
using System.Text;
using Chaos.DarkAges.Definitions;

namespace Chaos.Services.College;

public enum WordListCheck
{
    Ok,
    Title,
    WordCount,
    WordLength,
    WordLetters,
    HintLength,
    Duplicate
}

public readonly record struct WordListProblem(WordListCheck Check, int Word)
{
    public bool IsOk => Check == WordListCheck.Ok;
    public static WordListProblem Ok => new(WordListCheck.Ok, -1);
}

/// <summary>What makes a word list valid, and how words are compared.</summary>
public static class WordListRules
{
    public static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);

        foreach (var part in text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (builder.Length > 0)
                builder.Append(' ');

            builder.Append(part.ToUpperInvariant());
        }

        return builder.ToString();
    }

    /// <summary>Normalises every word and trims every hint and the title, in place, before a save or a quick start.</summary>
    public static void Normalize(CollegeWordList list)
    {
        list.Title = list.Title.Trim();

        foreach (var word in list.Words)
        {
            word.Text = Normalize(word.Text);
            word.Hint = word.Hint.ReplaceLineEndings(" ").Trim();
        }
    }

    public static WordListProblem CheckSave(CollegeWordList list)
    {
        if ((list.Title.Trim().Length == 0) || (list.Title.Length > CollegeProtocol.MAX_WORD_LIST_TITLE_CHARS))
            return new WordListProblem(WordListCheck.Title, -1);

        return CheckWords(list, CollegeProtocol.MIN_LIST_WORDS, CollegeProtocol.MAX_LIST_WORDS);
    }

    public static WordListProblem CheckQuick(CollegeWordList list)
        => CheckWords(list, CollegeProtocol.MIN_QUICK_WORDS, CollegeProtocol.MAX_QUICK_WORDS);

    /// <summary>True when the message holds the answer, ignoring case and spaces.</summary>
    public static bool Spoils(string message, string answer)
    {
        var target = Compact(answer);

        return (target.Length > 0) && Compact(message).Contains(target, StringComparison.Ordinal);
    }

    private static string Compact(string text) => string.Concat(text.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();

    private static WordListProblem CheckWords(CollegeWordList list, int min, int max)
    {
        if ((list.Words.Count < min) || (list.Words.Count > max))
            return new WordListProblem(WordListCheck.WordCount, -1);

        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < list.Words.Count; i++)
        {
            var text = Normalize(list.Words[i].Text);

            if ((text.Length < CollegeProtocol.MIN_WORD_CHARS) || (text.Length > CollegeProtocol.MAX_WORD_CHARS))
                return new WordListProblem(WordListCheck.WordLength, i);

            if (!text.All(c => c is ' ' or >= 'A' and <= 'Z'))
                return new WordListProblem(WordListCheck.WordLetters, i);

            if (list.Words[i].Hint.Trim().Length > CollegeProtocol.MAX_HINT_CHARS)
                return new WordListProblem(WordListCheck.HintLength, i);

            if (!seen.Add(text))
                return new WordListProblem(WordListCheck.Duplicate, i);
        }

        return WordListProblem.Ok;
    }
}
```

Note: `ToUpperInvariant` turns `é` into `É`, which the A-Z check refuses, as the test expects.

- [ ] **Step 4: Store.** Copy `CollegeQuizStore.cs` to `CollegeWordListStore.cs`: interface `ICollegeWordListStore` with the same members as `ICollegeQuizStore` (load by id, save, delete) typed on `CollegeWordList`, folder `WordLists` under the same works directory. Copy `Tests/Chaos.Tests/College/MemoryQuizStore.cs` to `MemoryWordListStore.cs` the same way.

- [ ] **Step 5: Texts** in `CollegeText.cs` next to the quiz texts (orange-bar texts at most 45 characters):

```csharp
    public const string WORD_LIST_NOT_ALLOWED = "Only College Teachers keep word lists.";
    public const string WORD_LIST_NOT_YOURS = "That word list is not yours.";
    public const string WORD_LIST_LIMIT = "You already keep 20 word lists.";
    public const string WORD_LIST_GONE = "That word list is gone.";
    public const string WORD_LIST_SAVED = "Saved.";
    public const string WORD_LIST_DELETED = "Word list deleted.";
    public const string WORD_LIST_COPIED = "Copied to your word lists.";
    public const string QUICK_LIST_SHORT = "Under 5 words: no attendance or bonus.";

    public static string WordListProblemText(WordListProblem problem)
        => problem.Check switch
        {
            WordListCheck.Title       => "Give the list a title of up to 40 letters.",
            WordListCheck.WordCount   => "A list needs 5 to 30 words.",
            WordListCheck.WordLength  => $"Word {problem.Word + 1}: 3 to 20 letters.",
            WordListCheck.WordLetters => $"Word {problem.Word + 1}: letters A-Z and spaces only.",
            WordListCheck.HintLength  => $"Word {problem.Word + 1}: hint up to 60 letters.",
            WordListCheck.Duplicate   => $"Word {problem.Word + 1} is already in the list.",
            _                         => string.Empty
        };
```

`WordCount` for a quick list uses its own text in the service: "A quick list needs 1 to 10 words."

- [ ] **Step 6: Service** (`CollegeService.WordLists.cs`), copying `CollegeService.Quizzes.cs` method by method (read it first). Public surface:

```csharp
public sealed record WordListOutcome(WordListResultCode Code, string Message, string Id = "");

public sealed partial class CollegeService
{
    private const string WORD_LIST_COPY_PREFIX = "Copy of ";

    public IReadOnlyList<CollegeWordListIndex> WordListsOf(string name);
    public CollegeWordList? OpenWordList(string name, bool isKnight, bool isAdmin, string id, out bool readOnly); //own list: readOnly false; another Teacher's: readOnly true, only if CanPost
    public WordListOutcome SaveWordList(string name, bool isKnight, bool isAdmin, string id, CollegeWordList list); //id "" = new
    public WordListOutcome DeleteWordList(string name, bool isKnight, bool isAdmin, string id);
    public WordListOutcome CopyWordList(string name, bool isKnight, bool isAdmin, string id); //own lists only, like CopyQuiz
    public WordListOutcome CopyBankWordList(string name, bool isKnight, bool isAdmin, string id); //any list
    public (IReadOnlyList<CollegeWordListIndex> Rows, int Page, int PageCount) WordBankPage(string name, bool isKnight, bool isAdmin, int page); //empty when not CanPost
    public IReadOnlyList<CollegeWord>? OwnedWords(string name, string id); //a copy of an owned list's words, for starting a game; null when not owned or gone
}
```

Rules: `CollegeRoles.CanPost` gate on every method except `WordListsOf`/`OwnedWords`; `WordListRules.Normalize(list)` then `CheckSave` before writing; count against `MAX_WORD_LISTS_PER_OWNER` on new and copies; index row `Count = Words.Count`; bank sorted `Owner` then `Title` (ordinal ignore case), page clamped to `[0, PageCount-1]`, `PageCount = max(1, ceil(n / 10))`; copy title `WORD_LIST_COPY_PREFIX + Title` cut to 40.

- [ ] **Step 7: Service tests** (`CollegeWordListServiceTests.cs`), built like `CollegeQuizServiceTests.cs` (read it for its service factory and a Teacher who `CanPost`). Cover, one test each:
  1. Save new then `WordListsOf` lists it with Count 5 and words stored upper-case.
  2. Save refused for a non-Teacher (`WORD_LIST_NOT_ALLOWED`).
  3. Save over another Teacher's id refused (`WORD_LIST_NOT_YOURS`).
  4. 21st save and 21st copy refused (`WORD_LIST_LIMIT`).
  5. Delete by owner removes file and index; by another Teacher refused.
  6. `CopyWordList` on another Teacher's list refused; on own list makes "Copy of Mileth".
  7. `CopyBankWordList` on another Teacher's list makes a new list owned by the caller; the original is unchanged.
  8. `WordBankPage` with 12 lists: page 0 has 10 rows sorted by owner then title, page 1 has 2, page 5 clamps to 1, `PageCount` 2.
  9. `OpenWordList` returns readOnly true for another Teacher's list and false for own.

- [ ] **Step 8: Run** the Verify command and the `WordListRulesTests` filter. Expected: all pass. Then run `--treenode-filter "/*/*/College*/*"` to confirm existing College tests still pass with the new constructor parameter.

```json:metadata
{"files": ["Chaos/Services/College/CollegeWordList.cs", "Chaos/Services/College/WordListRules.cs", "Chaos/Services/College/CollegeWordListStore.cs", "Chaos/Services/College/CollegeService.WordLists.cs", "Chaos/Services/College/CollegeState.cs", "Chaos/Services/College/CollegeService.cs", "Chaos/Services/College/CollegeText.cs", "Tests/Chaos.Tests/College/WordListRulesTests.cs", "Tests/Chaos.Tests/College/MemoryWordListStore.cs", "Tests/Chaos.Tests/College/CollegeWordListServiceTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/CollegeWordListServiceTests/*\"", "acceptanceCriteria": ["Normalize trims, collapses, upper-cases", "save refusals per spec", "copy and bank copy make caller-owned Copy of lists", "owner-only delete/overwrite; bank pages of 10 sorted", "rules and service tests pass; College tests still pass"], "modelTier": "standard"}
```

---

### Task 3: Hangman rules (server repo)

**Goal:** `HangmanSession`, a pure rule class over `LiveHangman`, implementing turns, picks, buzzing, solving, modes, word ends and attendance exactly as the spec says.

**Files:**
- Modify: `Chaos/Services/College/CollegeTools.cs` (add `LiveHangman`)
- Create: `Chaos/Services/College/WordGameScores.cs`, `Chaos/Services/College/HangmanSession.cs`
- Modify: `Chaos/Services/College/CollegeText.cs` (event texts)
- Create: `Tests/Chaos.Tests/College/HangmanSessionTests.cs`

**Acceptance Criteria:**
- [ ] Every test in Step 1 passes.
- [ ] `HangmanSession` holds no lock, does no I/O and takes the time as a parameter.

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/HangmanSessionTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests** (`HangmanSessionTests.cs`):

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Services.College;
using FluentAssertions;

namespace Chaos.Tests.College;

public sealed class HangmanSessionTests
{
    private static readonly DateTime Noon = new(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
    private static readonly HashSet<string> All = ["aroha", "kael", "mira"];

    private static LiveHangman Start(HangmanMode mode = HangmanMode.Survival, params string[] words)
    {
        var list = (words.Length == 0 ? ["GLIOCA", "MILETH", "DANAAN", "SGRIOS", "TAGOR", "ABEL"] : words)
                   .Select(w => new CollegeWord { Text = w, Hint = "hint " + w })
                   .ToList();

        return HangmanSession.Start("list1", "Mileth", list, mode, true, true, ["Aroha", "Kael", "Mira"]);
    }

    private static LiveHangman Open(HangmanMode mode = HangmanMode.Survival, params string[] words)
    {
        var game = Start(mode, words);
        HangmanSession.Next(game, Noon).Should().Be(HangmanStep.Done);

        return game;
    }

    [Test]
    public void Start_copies_the_words_and_waits()
    {
        var game = Start();

        game.Phase.Should().Be(HangmanPhase.Waiting);
        game.Players.Should().Equal("aroha", "kael", "mira");
        game.Names["kael"].Should().Be("Kael");
        game.Current.Should().Be(-1);
    }

    [Test]
    public void Next_opens_the_first_word_on_the_first_players_turn()
    {
        var game = Open();

        game.Phase.Should().Be(HangmanPhase.Open);
        HangmanSession.TurnKey(game).Should().Be("aroha");
        game.TurnUntilUtc.Should().Be(Noon.AddSeconds(20));
        game.TurnsGiven["aroha"].Should().Be(1);
        HangmanSession.Mask(game).Should().Be("______");
    }

    [Test]
    public void Only_the_turn_player_may_pick_and_only_untried_letters()
    {
        var game = Open();

        HangmanSession.Pick(game, "Kael", 'G', Noon).Should().BeFalse();
        HangmanSession.Pick(game, "Aroha", '1', Noon).Should().BeFalse();
        HangmanSession.Pick(game, "Aroha", 'g', Noon).Should().BeTrue();
        HangmanSession.Pick(game, "Kael", 'G', Noon).Should().BeFalse();
    }

    [Test]
    public void A_right_letter_scores_one_reveals_every_copy_and_passes_the_turn()
    {
        var game = Open(HangmanMode.Survival, "DANAAN", "MILETH", "GLIOCA", "SGRIOS", "TAGOR");

        HangmanSession.Pick(game, "Aroha", 'A', Noon).Should().BeTrue();

        HangmanSession.Mask(game).Should().Be("_A_AA_");
        game.Scores["aroha"].Should().Be(1);
        game.TurnsTaken["aroha"].Should().Be(1);
        HangmanSession.TurnKey(game).Should().Be("kael");
        game.Events.Should().Equal("Aroha found A.");
    }

    [Test]
    public void A_wrong_letter_is_a_miss_without_points()
    {
        var game = Open();

        HangmanSession.Pick(game, "Aroha", 'Z', Noon).Should().BeTrue();

        game.Misses.Should().Be(1);
        game.Scores.GetValueOrDefault("aroha").Should().Be(0);
        game.Events.Should().Equal("Aroha missed Z.");
    }

    [Test]
    public void Events_keep_the_last_three()
    {
        var game = Open();

        foreach (var (name, letter) in new[] { ("Aroha", 'Z'), ("Kael", 'X'), ("Mira", 'Q'), ("Aroha", 'W') })
            HangmanSession.Pick(game, name, letter, Noon).Should().BeTrue();

        game.Events.Should().Equal("Kael missed X.", "Mira missed Q.", "Aroha missed W.");
    }

    [Test]
    public void A_turn_times_out_as_a_skip_that_still_counts_as_given()
    {
        var game = Open();

        HangmanSession.Tick(game, Noon.AddSeconds(19), All).Should().BeFalse();
        HangmanSession.Tick(game, Noon.AddSeconds(20), All).Should().BeTrue();

        HangmanSession.TurnKey(game).Should().Be("kael");
        game.TurnsGiven["aroha"].Should().Be(1);
        game.TurnsTaken.GetValueOrDefault("aroha").Should().Be(0);
        game.Events.Should().Equal("Aroha's turn was skipped.");
    }

    [Test]
    public void An_absent_players_turn_is_skipped_at_once()
    {
        var game = Open();

        HangmanSession.Tick(game, Noon.AddSeconds(1), new HashSet<string> { "kael", "mira" }).Should().BeTrue();

        HangmanSession.TurnKey(game).Should().Be("kael");
    }

    [Test]
    public void The_first_buzz_pauses_the_turn_and_blocks_other_buzzes()
    {
        var game = Open();

        HangmanSession.Buzz(game, "Kael", Noon.AddSeconds(6)).Should().Be(BuzzResult.Buzzed);
        HangmanSession.Buzz(game, "Mira", Noon.AddSeconds(6)).Should().Be(BuzzResult.Busy);
        HangmanSession.Buzz(game, "Dorn", Noon.AddSeconds(6)).Should().Be(BuzzResult.NotPlaying);
        HangmanSession.Pick(game, "Aroha", 'G', Noon.AddSeconds(6)).Should().BeFalse();

        game.Phase.Should().Be(HangmanPhase.Solving);
        game.Solver.Should().Be("kael");
        game.SolveUntilUtc.Should().Be(Noon.AddSeconds(16));
        game.PausedSeconds.Should().Be(14);
    }

    [Test]
    public void A_right_solve_scores_three_and_ends_the_word()
    {
        var game = Open();
        HangmanSession.Buzz(game, "Kael", Noon).Should().Be(BuzzResult.Buzzed);

        HangmanSession.Solve(game, "Kael", " glioca ", Noon.AddSeconds(3)).Should().BeTrue();

        game.Phase.Should().Be(HangmanPhase.Revealed);
        game.Outcome.Should().Be(HangmanOutcome.Solved);
        game.Scores["kael"].Should().Be(3);
        game.WordsEnded.Should().Be(1);
        HangmanSession.TurnKey(game).Should().BeNull();
    }

    [Test]
    public void A_wrong_solve_is_a_miss_locks_the_player_out_and_resumes_the_turn_clock()
    {
        var game = Open();
        HangmanSession.Buzz(game, "Kael", Noon.AddSeconds(6)).Should().Be(BuzzResult.Buzzed);

        HangmanSession.Solve(game, "Kael", "MILETH", Noon.AddSeconds(8)).Should().BeTrue();

        game.Phase.Should().Be(HangmanPhase.Open);
        game.Misses.Should().Be(1);
        game.TurnUntilUtc.Should().Be(Noon.AddSeconds(22));
        HangmanSession.TurnKey(game).Should().Be("aroha");
        HangmanSession.Buzz(game, "Kael", Noon.AddSeconds(9)).Should().Be(BuzzResult.LockedOut);
        HangmanSession.Buzz(game, "Mira", Noon.AddSeconds(9)).Should().Be(BuzzResult.Buzzed);
    }

    [Test]
    public void No_answer_in_the_solve_window_counts_as_wrong()
    {
        var game = Open();
        HangmanSession.Buzz(game, "Kael", Noon).Should().Be(BuzzResult.Buzzed);

        HangmanSession.Solve(game, "Kael", "GLIOCA", Noon.AddSeconds(10)).Should().BeFalse();
        HangmanSession.Tick(game, Noon.AddSeconds(10), All).Should().BeTrue();

        game.Misses.Should().Be(1);
        game.LockedOut.Should().Contain("kael");
        game.Phase.Should().Be(HangmanPhase.Open);
    }

    [Test]
    public void Revealing_the_last_letter_solves_the_word_with_only_the_letter_point()
    {
        var game = Open(HangmanMode.Survival, "ABA", "MILETH", "GLIOCA", "SGRIOS", "TAGOR");

        HangmanSession.Pick(game, "Aroha", 'A', Noon).Should().BeTrue();
        HangmanSession.Pick(game, "Kael", 'B', Noon).Should().BeTrue();

        game.Outcome.Should().Be(HangmanOutcome.Solved);
        game.Scores["kael"].Should().Be(1);
    }

    [Test]
    public void Survival_loses_the_word_at_six_misses_counting_wrong_solves()
    {
        var game = Open();

        foreach (var (name, letter) in new[] { ("Aroha", 'Z'), ("Kael", 'X'), ("Mira", 'Q'), ("Aroha", 'W'), ("Kael", 'V') })
            HangmanSession.Pick(game, name, letter, Noon).Should().BeTrue();

        HangmanSession.Buzz(game, "Aroha", Noon).Should().Be(BuzzResult.Buzzed);
        HangmanSession.Solve(game, "Aroha", "WRONG", Noon).Should().BeTrue();

        game.Phase.Should().Be(HangmanPhase.Revealed);
        game.Outcome.Should().Be(HangmanOutcome.Lost);
        game.Misses.Should().Be(6);
    }

    [Test]
    public void Casual_has_no_miss_limit()
    {
        var game = Open(HangmanMode.Casual);

        foreach (var letter in "ZXQWVBUKJ")
            HangmanSession.Pick(game, HangmanSession.NameOf(game, HangmanSession.TurnKey(game)!), letter, Noon).Should().BeTrue();

        game.Misses.Should().Be(9);
        game.Phase.Should().Be(HangmanPhase.Open);
    }

    [Test]
    public void The_next_word_starts_with_the_player_after_the_last_mover()
    {
        var game = Open();
        HangmanSession.Buzz(game, "Kael", Noon).Should().Be(BuzzResult.Buzzed);
        HangmanSession.Solve(game, "Kael", "GLIOCA", Noon).Should().BeTrue();

        HangmanSession.Next(game, Noon.AddSeconds(30)).Should().Be(HangmanStep.Done);

        HangmanSession.TurnKey(game).Should().Be("mira");
        game.Tried.Should().BeEmpty();
        game.Misses.Should().Be(0);
        game.LockedOut.Should().BeEmpty();
    }

    [Test]
    public void Next_is_refused_while_a_word_is_open_and_after_the_last_word()
    {
        var game = Open(HangmanMode.Survival, "ABA");

        HangmanSession.Next(game, Noon).Should().Be(HangmanStep.NotNow);
        HangmanSession.Buzz(game, "Aroha", Noon).Should().Be(BuzzResult.Buzzed);
        HangmanSession.Solve(game, "Aroha", "ABA", Noon).Should().BeTrue();
        HangmanSession.Next(game, Noon).Should().Be(HangmanStep.NoMoreWords);
    }

    [Test]
    public void Removed_players_get_no_turns_and_may_not_buzz_and_added_players_join_the_end()
    {
        var game = Open();

        HangmanSession.RemovePlayer(game, "Aroha", Noon).Should().BeTrue();
        HangmanSession.TurnKey(game).Should().Be("kael");
        HangmanSession.Buzz(game, "Aroha", Noon).Should().Be(BuzzResult.NotPlaying);

        HangmanSession.AddPlayer(game, "Dorn", Noon).Should().BeTrue();
        HangmanSession.AddPlayer(game, "Dorn", Noon).Should().BeFalse();
        game.Players.Should().Equal("aroha", "kael", "mira", "dorn");

        HangmanSession.Pick(game, "Kael", 'Z', Noon).Should().BeTrue();
        HangmanSession.Pick(game, "Mira", 'X', Noon).Should().BeTrue();
        HangmanSession.TurnKey(game).Should().Be("dorn");
        HangmanSession.Pick(game, "Dorn", 'Q', Noon).Should().BeTrue();
        HangmanSession.TurnKey(game).Should().Be("kael");
    }

    [Test]
    public void The_solver_may_not_be_removed_while_answering()
    {
        var game = Open();
        HangmanSession.Buzz(game, "Kael", Noon).Should().Be(BuzzResult.Buzzed);

        HangmanSession.RemovePlayer(game, "Kael", Noon).Should().BeFalse();
    }

    [Test]
    public void Teacher_skip_passes_the_turn_but_not_while_someone_is_answering()
    {
        var game = Open();

        HangmanSession.Skip(game, Noon).Should().BeTrue();
        HangmanSession.TurnKey(game).Should().Be("kael");

        HangmanSession.Buzz(game, "Mira", Noon).Should().Be(BuzzResult.Buzzed);
        HangmanSession.Skip(game, Noon).Should().BeFalse();
    }

    [Test]
    public void End_drops_an_open_word_and_stops_play()
    {
        var game = Open();

        HangmanSession.End(game);

        game.Phase.Should().Be(HangmanPhase.Ended);
        game.WordsEnded.Should().Be(0);
        HangmanSession.Buzz(game, "Kael", Noon).Should().Be(BuzzResult.NotNow);
    }

    [Test]
    public void Attendance_needs_half_the_turns_given_taken_and_excuses_players_with_no_turns()
    {
        var game = Open();
        game.TurnsGiven["aroha"] = 4;
        game.TurnsTaken["aroha"] = 2;
        game.TurnsGiven["kael"] = 3;
        game.TurnsTaken["kael"] = 1;

        HangmanSession.PassedAttendance(game, "Aroha").Should().BeTrue();
        HangmanSession.PassedAttendance(game, "Kael").Should().BeFalse();
        HangmanSession.PassedAttendance(game, "Dorn").Should().BeTrue();
    }

    [Test]
    public void The_guard_needs_five_ended_words()
    {
        var game = Open();
        game.WordsEnded = 4;
        HangmanSession.GuardMet(game).Should().BeFalse();
        game.WordsEnded = 5;
        HangmanSession.GuardMet(game).Should().BeTrue();
    }

    [Test]
    public void Mask_keeps_spaces()
    {
        var game = Open(HangmanMode.Survival, "MILETH VILLAGE");

        HangmanSession.Pick(game, "Aroha", 'L', Noon).Should().BeTrue();

        HangmanSession.Mask(game).Should().Be("__L___ __LL___");
    }

    [Test]
    public void Top_scorers_follow_the_quiz_rule()
    {
        var scores = new Dictionary<string, int> { ["aroha"] = 3, ["kael"] = 3, ["mira"] = 1, ["dorn"] = 9 };

        WordGameScores.TopScorers(scores, ["Aroha", "Kael", "Mira"]).Should().BeEquivalentTo("aroha", "kael");
        WordGameScores.TopScorers(new Dictionary<string, int>(), ["Aroha"]).Should().BeEmpty();
        WordGameScores.TopScorers(new Dictionary<string, int> { ["a"] = 1, ["b"] = 1, ["c"] = 1, ["d"] = 1 }, ["a", "b", "c", "d"])
                      .Should().BeEmpty();
    }
}
```

Run the Verify command. Expected: build fails (`HangmanSession` missing).

- [ ] **Step 2: Live object** in `CollegeTools.cs` after `LiveDebate`. Lists, not sets, so `CollegeState` JSON stays simple, as `LiveDebate` does:

```csharp
public sealed class LiveHangman
{
    public string ListId { get; set; } = "";
    public string Title { get; set; } = "";
    public List<CollegeWord> Words { get; set; } = [];
    public HangmanMode Mode { get; set; }
    public bool Attendance { get; set; }
    public bool Bonus { get; set; }

    /// <summary>The word being played or last ended; -1 before the first.</summary>
    public int Current { get; set; } = -1;

    public HangmanPhase Phase { get; set; } = HangmanPhase.Waiting;
    public HangmanOutcome Outcome { get; set; }

    /// <summary>The turn list, keys in order. A removed player keeps their place, so indexes never move.</summary>
    public List<string> Players { get; set; } = [];

    public List<string> Removed { get; set; } = [];
    public Dictionary<string, string> Names { get; set; } = [];

    /// <summary>Index in <see cref="Players" /> of the player whose turn it is; -1 when nobody's.</summary>
    public int Turn { get; set; } = -1;

    /// <summary>Where the next word's first turn is looked for.</summary>
    public int NextStarter { get; set; }

    public DateTime TurnUntilUtc { get; set; }
    public string? Solver { get; set; }
    public DateTime SolveUntilUtc { get; set; }

    /// <summary>What was left of the turn when the buzz paused it.</summary>
    public double PausedSeconds { get; set; }

    /// <summary>Letters tried on this word, in order.</summary>
    public string Tried { get; set; } = "";

    public int Misses { get; set; }
    public List<string> LockedOut { get; set; } = [];
    public List<string> Events { get; set; } = [];
    public Dictionary<string, int> Scores { get; set; } = [];
    public Dictionary<string, int> TurnsGiven { get; set; } = [];
    public Dictionary<string, int> TurnsTaken { get; set; } = [];
    public int WordsEnded { get; set; }
}
```

- [ ] **Step 3: Shared scores** (`WordGameScores.cs`), the quiz's rule over any score map:

```csharp
using Chaos.DarkAges.Definitions;

namespace Chaos.Services.College;

/// <summary>Bonus and leader picks for the word games, by the same rule as <see cref="QuizSession.TopScorers" />.</summary>
public static class WordGameScores
{
    public static IReadOnlyList<string> TopScorers(IReadOnlyDictionary<string, int> scores, IEnumerable<string> qualified)
    {
        var keys = qualified.Select(CollegeState.Key).ToHashSet();
        var mine = scores.Where(kv => keys.Contains(kv.Key)).ToList();
        var best = mine.Select(kv => kv.Value).DefaultIfEmpty(0).Max();

        if (best <= 0)
            return [];

        var top = mine.Where(kv => kv.Value == best).Select(kv => kv.Key).ToList();

        return top.Count <= CollegeProtocol.MAX_BONUS_STUDENTS ? top : [];
    }

    public static IReadOnlyList<(string Name, int Score)> Leaders(IReadOnlyDictionary<string, int> scores, IReadOnlyDictionary<string, string> names)
        => scores.Where(kv => kv.Value > 0)
                 .OrderByDescending(kv => kv.Value)
                 .ThenBy(kv => kv.Key, StringComparer.Ordinal)
                 .Take(CollegeProtocol.MAX_BONUS_STUDENTS)
                 .Select(kv => (names.GetValueOrDefault(kv.Key, kv.Key), kv.Value))
                 .ToList();
}
```

(`CollegeState.Key` must lower-case: check its body; the tests pass names like "Aroha" and expect "aroha".)

- [ ] **Step 4: Event texts** in `CollegeText.cs`:

```csharp
    public static string HangmanFound(string name, char letter) => $"{name} found {letter}.";
    public static string HangmanMissed(string name, char letter) => $"{name} missed {letter}.";
    public static string HangmanSkipped(string name) => $"{name}'s turn was skipped.";
    public static string HangmanBuzzed(string name) => $"{name} buzzed in.";
    public static string HangmanSolved(string name) => $"{name} solved it!";
    public static string HangmanWrongSolve(string name) => $"{name} guessed wrong.";
    public const string HANGMAN_LOST = "The word was lost.";
```

- [ ] **Step 5: Rules** (`HangmanSession.cs`):

```csharp
using Chaos.DarkAges.Definitions;

namespace Chaos.Services.College;

public enum HangmanStep
{
    Done,
    NotNow,
    NoMoreWords
}

public enum BuzzResult
{
    Buzzed,
    NotNow,
    Busy,
    LockedOut,
    NotPlaying
}

/// <summary>The rules of a class Hangman game, on a <see cref="LiveHangman" />. Not thread-safe; <see cref="CollegeService" /> holds the lock.</summary>
public static class HangmanSession
{
    public static LiveHangman Start(
        string listId,
        string title,
        IReadOnlyList<CollegeWord> words,
        HangmanMode mode,
        bool attendance,
        bool bonus,
        IReadOnlyList<string> players)
    {
        var game = new LiveHangman
        {
            ListId = listId,
            Title = title,
            Words = words.Select(w => new CollegeWord { Text = w.Text, Hint = w.Hint }).ToList(),
            Mode = mode,
            Attendance = attendance,
            Bonus = bonus
        };

        foreach (var name in players)
            AddPlayer(game, name, default);

        return game;
    }

    public static string Word(LiveHangman game) => game.Words[game.Current].Text;

    public static string Hint(LiveHangman game) => game.Current >= 0 ? game.Words[game.Current].Hint : string.Empty;

    public static bool IsLast(LiveHangman game) => game.Current >= game.Words.Count - 1;

    public static string? TurnKey(LiveHangman game) => game.Turn >= 0 ? game.Players[game.Turn] : null;

    public static string NameOf(LiveHangman game, string key) => game.Names.GetValueOrDefault(key, key);

    public static bool IsPlaying(LiveHangman game, string key) => game.Players.Contains(key) && !game.Removed.Contains(key);

    public static string Mask(LiveHangman game)
        => game.Current < 0 ? string.Empty : string.Concat(Word(game).Select(c => (c == ' ') || game.Tried.Contains(c) ? c : '_'));

    public static HangmanStep Next(LiveHangman game, DateTime now)
    {
        if (game.Phase is HangmanPhase.Open or HangmanPhase.Solving or HangmanPhase.Ended)
            return HangmanStep.NotNow;

        if (IsLast(game))
            return HangmanStep.NoMoreWords;

        game.Current++;
        game.Phase = HangmanPhase.Open;
        game.Outcome = HangmanOutcome.None;
        game.Tried = string.Empty;
        game.Misses = 0;
        game.Solver = null;
        game.LockedOut.Clear();
        game.Events.Clear();
        BeginTurnFrom(game, game.NextStarter, now);

        return HangmanStep.Done;
    }

    public static bool Pick(LiveHangman game, string name, char letter, DateTime now)
    {
        if ((game.Phase != HangmanPhase.Open) || TurnKey(game) is not { } turn || (turn != CollegeState.Key(name)))
            return false;

        letter = char.ToUpperInvariant(letter);

        if (letter is < 'A' or > 'Z' || game.Tried.Contains(letter))
            return false;

        game.Tried += letter;
        game.TurnsTaken[turn] = game.TurnsTaken.GetValueOrDefault(turn) + 1;
        var hit = Word(game).Contains(letter);

        if (hit)
        {
            game.Scores[turn] = game.Scores.GetValueOrDefault(turn) + CollegeProtocol.HANGMAN_LETTER_POINTS;
            AddEvent(game, CollegeText.HangmanFound(NameOf(game, turn), letter));

            if (IsRevealed(game))
            {
                EndWord(game, HangmanOutcome.Solved, game.Turn);

                return true;
            }
        } else
        {
            game.Misses++;
            AddEvent(game, CollegeText.HangmanMissed(NameOf(game, turn), letter));

            if (SurvivalLost(game))
            {
                EndWord(game, HangmanOutcome.Lost, game.Turn);

                return true;
            }
        }

        BeginTurnFrom(game, game.Turn + 1, now);

        return true;
    }

    public static BuzzResult Buzz(LiveHangman game, string name, DateTime now)
    {
        var key = CollegeState.Key(name);

        if (!IsPlaying(game, key))
            return BuzzResult.NotPlaying;

        if (game.Phase == HangmanPhase.Solving)
            return BuzzResult.Busy;

        if (game.Phase != HangmanPhase.Open)
            return BuzzResult.NotNow;

        if (game.LockedOut.Contains(key))
            return BuzzResult.LockedOut;

        game.Phase = HangmanPhase.Solving;
        game.Solver = key;
        game.SolveUntilUtc = now.AddSeconds(CollegeProtocol.HANGMAN_SOLVE_SECONDS);
        game.PausedSeconds = game.Turn >= 0 ? Math.Max(0, (game.TurnUntilUtc - now).TotalSeconds) : 0;
        AddEvent(game, CollegeText.HangmanBuzzed(NameOf(game, key)));

        return BuzzResult.Buzzed;
    }

    /// <summary>True when the answer was taken, right or wrong.</summary>
    public static bool Solve(LiveHangman game, string name, string answer, DateTime now)
    {
        var key = CollegeState.Key(name);

        if ((game.Phase != HangmanPhase.Solving) || (game.Solver != key) || (now >= game.SolveUntilUtc))
            return false;

        if (WordListRules.Normalize(answer) == Word(game))
        {
            game.Scores[key] = game.Scores.GetValueOrDefault(key) + CollegeProtocol.HANGMAN_SOLVE_POINTS;
            AddEvent(game, CollegeText.HangmanSolved(NameOf(game, key)));
            EndWord(game, HangmanOutcome.Solved, game.Players.IndexOf(key));
        } else
            WrongSolve(game, key, now);

        return true;
    }

    /// <summary>Ends a solve window that ran out and skips a turn that ran out or whose player left. True when something changed.</summary>
    public static bool Tick(LiveHangman game, DateTime now, IReadOnlySet<string> present)
    {
        if (game.Phase == HangmanPhase.Solving)
        {
            if (now < game.SolveUntilUtc)
                return false;

            WrongSolve(game, game.Solver!, now);

            return true;
        }

        if ((game.Phase != HangmanPhase.Open) || TurnKey(game) is not { } turn)
            return false;

        if (present.Contains(turn) && (now < game.TurnUntilUtc))
            return false;

        SkipTurn(game, now);

        return true;
    }

    public static bool Skip(LiveHangman game, DateTime now)
    {
        if ((game.Phase != HangmanPhase.Open) || (game.Turn < 0))
            return false;

        SkipTurn(game, now);

        return true;
    }

    public static bool AddPlayer(LiveHangman game, string name, DateTime now)
    {
        var key = CollegeState.Key(name);

        if (IsPlaying(game, key))
            return false;

        game.Names[key] = name;

        if (!game.Removed.Remove(key))
            game.Players.Add(key);

        if ((game.Phase == HangmanPhase.Open) && (game.Turn < 0))
            BeginTurnFrom(game, game.Players.IndexOf(key), now);

        return true;
    }

    public static bool RemovePlayer(LiveHangman game, string name, DateTime now)
    {
        var key = CollegeState.Key(name);

        if (!IsPlaying(game, key) || (game.Solver == key))
            return false;

        game.Removed.Add(key);

        if ((game.Phase == HangmanPhase.Open) && (TurnKey(game) == key))
            BeginTurnFrom(game, game.Turn + 1, now);

        return true;
    }

    /// <summary>A word still open is dropped: it does not count as ended.</summary>
    public static void End(LiveHangman game)
    {
        game.Phase = HangmanPhase.Ended;
        game.Turn = -1;
        game.Solver = null;
    }

    public static bool GuardMet(LiveHangman game) => game.WordsEnded >= CollegeProtocol.WORD_GAME_GUARD_WORDS;

    public static bool PassedAttendance(LiveHangman game, string name)
    {
        var key = CollegeState.Key(name);

        return game.TurnsTaken.GetValueOrDefault(key) * 2 >= game.TurnsGiven.GetValueOrDefault(key);
    }

    private static bool IsRevealed(LiveHangman game) => Word(game).All(c => (c == ' ') || game.Tried.Contains(c));

    private static bool SurvivalLost(LiveHangman game)
        => (game.Mode == HangmanMode.Survival) && (game.Misses >= CollegeProtocol.HANGMAN_MAX_MISSES);

    private static void SkipTurn(LiveHangman game, DateTime now)
    {
        AddEvent(game, CollegeText.HangmanSkipped(NameOf(game, TurnKey(game)!)));
        BeginTurnFrom(game, game.Turn + 1, now);
    }

    private static void WrongSolve(LiveHangman game, string key, DateTime now)
    {
        game.Misses++;
        game.LockedOut.Add(key);
        game.Solver = null;
        game.Phase = HangmanPhase.Open;
        AddEvent(game, CollegeText.HangmanWrongSolve(NameOf(game, key)));

        if (SurvivalLost(game))
        {
            EndWord(game, HangmanOutcome.Lost, game.Players.IndexOf(key));

            return;
        }

        if (game.Turn >= 0)
            game.TurnUntilUtc = now.AddSeconds(game.PausedSeconds);
    }

    /// <summary>Gives the turn to the first player still on the list at or after <paramref name="start" />, wrapping round.</summary>
    private static void BeginTurnFrom(LiveHangman game, int start, DateTime now)
    {
        game.Turn = -1;
        var count = game.Players.Count;

        for (var i = 0; i < count; i++)
        {
            var index = (((start + i) % count) + count) % count;
            var key = game.Players[index];

            if (game.Removed.Contains(key))
                continue;

            game.Turn = index;
            game.TurnsGiven[key] = game.TurnsGiven.GetValueOrDefault(key) + 1;
            game.TurnUntilUtc = now.AddSeconds(CollegeProtocol.HANGMAN_TURN_SECONDS);

            return;
        }
    }

    private static void EndWord(LiveHangman game, HangmanOutcome outcome, int mover)
    {
        game.Phase = HangmanPhase.Revealed;
        game.Outcome = outcome;
        game.WordsEnded++;
        game.Turn = -1;
        game.Solver = null;
        game.NextStarter = mover + 1;

        if (outcome == HangmanOutcome.Lost)
            AddEvent(game, CollegeText.HANGMAN_LOST);
    }

    private static void AddEvent(LiveHangman game, string text)
    {
        game.Events.Add(text);

        if (game.Events.Count > CollegeProtocol.MAX_GAME_EVENTS)
            game.Events.RemoveAt(0);
    }
}
```

Check against the tests: `Survival_loses...` adds the "The word was lost." event, which is fine because that test checks only phase, outcome and misses. `Start` calls `AddPlayer` while the phase is Waiting, so no turn begins.

- [ ] **Step 6: Run** the Verify command. Expected: all pass. Fix the implementation, not the tests, unless a test contradicts the spec (then report it).

```json:metadata
{"files": ["Chaos/Services/College/CollegeTools.cs", "Chaos/Services/College/WordGameScores.cs", "Chaos/Services/College/HangmanSession.cs", "Chaos/Services/College/CollegeText.cs", "Tests/Chaos.Tests/College/HangmanSessionTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/HangmanSessionTests/*\"", "acceptanceCriteria": ["every HangmanSessionTests test passes", "HangmanSession is pure: no lock, no I/O, time passed in"], "modelTier": "standard"}
```

---

### Task 4: Word Scramble rules (server repo)

**Goal:** `ScrambleSession`, a pure rule class over `LiveScramble`: scramble making, guesses, finish-order points, reveal and attendance.

**Files:**
- Modify: `Chaos/Services/College/CollegeTools.cs` (add `LiveScramble`)
- Create: `Chaos/Services/College/ScrambleSession.cs`
- Create: `Tests/Chaos.Tests/College/ScrambleSessionTests.cs`

**Acceptance Criteria:**
- [ ] Every test in Step 1 passes.
- [ ] The scramble differs from the word whenever any part has two different letters, keeps spaces, and "AAA" comes back as it is.

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/ScrambleSessionTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests** (`ScrambleSessionTests.cs`):

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Services.College;
using FluentAssertions;

namespace Chaos.Tests.College;

public sealed class ScrambleSessionTests
{
    private static readonly DateTime Noon = new(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);

    private static LiveScramble Open(params string[] words)
    {
        var list = (words.Length == 0 ? ["GLIOCA", "MILETH", "DANAAN", "SGRIOS", "TAGOR", "ABEL"] : words)
                   .Select(w => new CollegeWord { Text = w })
                   .ToList();
        var game = ScrambleSession.Start("list1", "Mileth", list, 20, true, true);
        ScrambleSession.Next(game, Noon, new Random(1)).Should().Be(ScrambleStep.Done);

        return game;
    }

    //formatter:off
    [Test]
    [Arguments("GLIOCA")]
    [Arguments("AB")]
    [Arguments("MILETH VILLAGE")]
    [Arguments("AAB")]
    //formatter:on
    public void Scramble_always_differs_and_keeps_spaces_and_letters(string word)
    {
        for (var seed = 0; seed < 200; seed++)
        {
            var scrambled = ScrambleSession.Scramble(word, new Random(seed));

            scrambled.Should().NotBe(word);
            scrambled.Length.Should().Be(word.Length);

            for (var i = 0; i < word.Length; i++)
                (scrambled[i] == ' ').Should().Be(word[i] == ' ');

            scrambled.OrderBy(c => c).Should().Equal(word.OrderBy(c => c));
        }
    }

    [Test]
    public void A_word_that_cannot_change_is_shown_as_it_is()
        => ScrambleSession.Scramble("AAA", new Random(3)).Should().Be("AAA");

    [Test]
    public void Next_opens_a_word_with_a_deadline()
    {
        var game = Open();

        game.Phase.Should().Be(ScramblePhase.Open);
        game.DeadlineUtc.Should().Be(Noon.AddSeconds(20));
        game.Scrambled.Should().NotBe("GLIOCA");
    }

    [Test]
    public void Points_go_by_finish_order()
    {
        var game = Open();

        ScrambleSession.Guess(game, "Aroha", "glioca", Noon.AddSeconds(1)).Should().Be(ScrambleGuessResult.Right);
        ScrambleSession.Guess(game, "Kael", "GLIOCA", Noon.AddSeconds(2)).Should().Be(ScrambleGuessResult.Right);
        ScrambleSession.Guess(game, "Mira", "GLIOCA", Noon.AddSeconds(3)).Should().Be(ScrambleGuessResult.Right);
        ScrambleSession.Guess(game, "Bryn", "GLIOCA", Noon.AddSeconds(4)).Should().Be(ScrambleGuessResult.Right);

        game.Scores.Should().BeEquivalentTo(new Dictionary<string, int> { ["aroha"] = 3, ["kael"] = 2, ["mira"] = 1, ["bryn"] = 1 });
        game.Solvers.Should().Equal("aroha", "kael", "mira", "bryn");
    }

    [Test]
    public void Wrong_guesses_may_be_repeated_but_not_faster_than_twice_a_second()
    {
        var game = Open();

        ScrambleSession.Guess(game, "Aroha", "GOLICA", Noon).Should().Be(ScrambleGuessResult.Wrong);
        ScrambleSession.Guess(game, "Aroha", "GLICOA", Noon.AddMilliseconds(400)).Should().Be(ScrambleGuessResult.TooFast);
        ScrambleSession.Guess(game, "Aroha", "GLIOCA", Noon.AddMilliseconds(500)).Should().Be(ScrambleGuessResult.Right);
        ScrambleSession.Guess(game, "Aroha", "GLIOCA", Noon.AddSeconds(2)).Should().Be(ScrambleGuessResult.AlreadySolved);
    }

    [Test]
    public void Guesses_after_the_deadline_are_refused()
    {
        var game = Open();

        ScrambleSession.Guess(game, "Aroha", "GLIOCA", Noon.AddSeconds(20)).Should().Be(ScrambleGuessResult.NotNow);
    }

    [Test]
    public void The_word_is_revealed_at_the_deadline()
    {
        var game = Open();
        ScrambleSession.Guess(game, "Aroha", "WRONG", Noon).Should().Be(ScrambleGuessResult.Wrong);

        ScrambleSession.Tick(game, Noon.AddSeconds(19), ["aroha", "kael"]).Should().BeFalse();
        ScrambleSession.Tick(game, Noon.AddSeconds(20), ["aroha", "kael"]).Should().BeTrue();

        game.Phase.Should().Be(ScramblePhase.Revealed);
        game.WordsEnded.Should().Be(1);
        game.GuessedWords["aroha"].Should().Be(1);
        game.GuessedWords.GetValueOrDefault("kael").Should().Be(0);
    }

    [Test]
    public void The_word_is_revealed_early_once_everyone_present_has_solved_it()
    {
        var game = Open();
        ScrambleSession.Guess(game, "Aroha", "GLIOCA", Noon).Should().Be(ScrambleGuessResult.Right);

        ScrambleSession.Tick(game, Noon.AddSeconds(1), ["aroha", "kael"]).Should().BeFalse();
        ScrambleSession.Guess(game, "Kael", "GLIOCA", Noon.AddSeconds(2)).Should().Be(ScrambleGuessResult.Right);
        ScrambleSession.Tick(game, Noon.AddSeconds(2), ["aroha", "kael"]).Should().BeTrue();

        game.Phase.Should().Be(ScramblePhase.Revealed);
    }

    [Test]
    public void An_empty_room_never_reveals_early()
    {
        var game = Open();

        ScrambleSession.Tick(game, Noon.AddSeconds(1), []).Should().BeFalse();
    }

    [Test]
    public void Next_is_refused_while_open_and_after_the_last_word()
    {
        var game = Open("GLIOCA");

        ScrambleSession.Next(game, Noon, new Random(1)).Should().Be(ScrambleStep.NotNow);
        ScrambleSession.Tick(game, Noon.AddSeconds(20), []).Should().BeTrue();
        ScrambleSession.Next(game, Noon, new Random(1)).Should().Be(ScrambleStep.NoMoreWords);
    }

    [Test]
    public void End_drops_an_open_word()
    {
        var game = Open();
        ScrambleSession.Guess(game, "Aroha", "WRONG", Noon).Should().Be(ScrambleGuessResult.Wrong);

        ScrambleSession.End(game);

        game.Phase.Should().Be(ScramblePhase.Ended);
        game.WordsEnded.Should().Be(0);
        game.GuessedWords.GetValueOrDefault("aroha").Should().Be(0);
    }

    [Test]
    public void Attendance_needs_a_guess_on_half_the_ended_words()
    {
        var game = Open();
        game.WordsEnded = 5;
        game.GuessedWords["aroha"] = 3;
        game.GuessedWords["kael"] = 2;

        ScrambleSession.PassedAttendance(game, "Aroha").Should().BeTrue();
        ScrambleSession.PassedAttendance(game, "Kael").Should().BeFalse();
        ScrambleSession.GuardMet(game).Should().BeTrue();
    }
}
```

Run the Verify command. Expected: build fails.

- [ ] **Step 2: Live object** in `CollegeTools.cs`:

```csharp
public sealed class LiveScramble
{
    public string ListId { get; set; } = "";
    public string Title { get; set; } = "";
    public List<CollegeWord> Words { get; set; } = [];
    public int Seconds { get; set; }
    public bool Attendance { get; set; }
    public bool Bonus { get; set; }
    public int Current { get; set; } = -1;
    public ScramblePhase Phase { get; set; } = ScramblePhase.Waiting;
    public DateTime DeadlineUtc { get; set; }
    public string Scrambled { get; set; } = "";

    /// <summary>This word's solvers, in finish order.</summary>
    public List<string> Solvers { get; set; } = [];

    /// <summary>Everyone who guessed this word at least once.</summary>
    public List<string> Guessers { get; set; } = [];

    public Dictionary<string, DateTime> LastGuessUtc { get; set; } = [];
    public Dictionary<string, int> Scores { get; set; } = [];

    /// <summary>Per student, how many ended words they guessed at least once.</summary>
    public Dictionary<string, int> GuessedWords { get; set; } = [];

    public Dictionary<string, string> Names { get; set; } = [];
    public int WordsEnded { get; set; }
}
```

- [ ] **Step 3: Rules** (`ScrambleSession.cs`):

```csharp
using Chaos.DarkAges.Definitions;

namespace Chaos.Services.College;

public enum ScrambleStep
{
    Done,
    NotNow,
    NoMoreWords
}

public enum ScrambleGuessResult
{
    NotNow,
    TooFast,
    Wrong,
    Right,
    AlreadySolved
}

/// <summary>The rules of a class Word Scramble, on a <see cref="LiveScramble" />. Not thread-safe; <see cref="CollegeService" /> holds the lock.</summary>
public static class ScrambleSession
{
    public static LiveScramble Start(string listId, string title, IReadOnlyList<CollegeWord> words, int seconds, bool attendance, bool bonus)
        => new()
        {
            ListId = listId,
            Title = title,
            Words = words.Select(w => new CollegeWord { Text = w.Text, Hint = w.Hint }).ToList(),
            Seconds = seconds,
            Attendance = attendance,
            Bonus = bonus
        };

    public static string Word(LiveScramble game) => game.Words[game.Current].Text;

    public static string Hint(LiveScramble game) => game.Current >= 0 ? game.Words[game.Current].Hint : string.Empty;

    public static bool IsLast(LiveScramble game) => game.Current >= game.Words.Count - 1;

    public static string NameOf(LiveScramble game, string key) => game.Names.GetValueOrDefault(key, key);

    public static ScrambleStep Next(LiveScramble game, DateTime now, Random random)
    {
        if (game.Phase is ScramblePhase.Open or ScramblePhase.Ended)
            return ScrambleStep.NotNow;

        if (IsLast(game))
            return ScrambleStep.NoMoreWords;

        game.Current++;
        game.Phase = ScramblePhase.Open;
        game.DeadlineUtc = now.AddSeconds(game.Seconds);
        game.Scrambled = Scramble(Word(game), random);
        game.Solvers.Clear();
        game.Guessers.Clear();
        game.LastGuessUtc.Clear();

        return ScrambleStep.Done;
    }

    public static ScrambleGuessResult Guess(LiveScramble game, string name, string text, DateTime now)
    {
        if ((game.Phase != ScramblePhase.Open) || (now >= game.DeadlineUtc))
            return ScrambleGuessResult.NotNow;

        var key = CollegeState.Key(name);

        if (game.Solvers.Contains(key))
            return ScrambleGuessResult.AlreadySolved;

        if (game.LastGuessUtc.TryGetValue(key, out var last) && ((now - last).TotalMilliseconds < CollegeProtocol.SCRAMBLE_GUESS_GAP_MS))
            return ScrambleGuessResult.TooFast;

        game.LastGuessUtc[key] = now;
        game.Names[key] = name;

        if (!game.Guessers.Contains(key))
            game.Guessers.Add(key);

        if (WordListRules.Normalize(text) != Word(game))
            return ScrambleGuessResult.Wrong;

        game.Solvers.Add(key);
        game.Scores[key] = game.Scores.GetValueOrDefault(key) + Points(game.Solvers.Count);

        return ScrambleGuessResult.Right;
    }

    public static int Points(int place) => place switch
    {
        1 => 3,
        2 => 2,
        _ => 1
    };

    /// <summary>Reveals the word at the deadline, or once every player <paramref name="present" /> has solved it. True when something changed.</summary>
    public static bool Tick(LiveScramble game, DateTime now, IReadOnlyCollection<string> present)
    {
        if (game.Phase != ScramblePhase.Open)
            return false;

        var everyone = (present.Count > 0) && present.All(game.Solvers.Contains);

        if ((now < game.DeadlineUtc) && !everyone)
            return false;

        foreach (var key in game.Guessers)
            game.GuessedWords[key] = game.GuessedWords.GetValueOrDefault(key) + 1;

        game.WordsEnded++;
        game.Phase = ScramblePhase.Revealed;

        return true;
    }

    /// <summary>A word still open is dropped: it does not count as ended and its guesses do not count.</summary>
    public static void End(LiveScramble game) => game.Phase = ScramblePhase.Ended;

    public static bool GuardMet(LiveScramble game) => game.WordsEnded >= CollegeProtocol.WORD_GAME_GUARD_WORDS;

    public static bool PassedAttendance(LiveScramble game, string name)
        => game.GuessedWords.GetValueOrDefault(CollegeState.Key(name)) * 2 >= game.WordsEnded;

    /// <summary>
    ///     Shuffles each space-separated part on its own. When the shuffle gives the word back, the first part with two
    ///     different letters is turned by one place, which always changes it; a word with no such part comes back as it is.
    /// </summary>
    public static string Scramble(string word, Random random)
    {
        var parts = word.Split(' ');
        var shuffled = parts.Select(part =>
                            {
                                var letters = part.ToCharArray();
                                random.Shuffle(letters);

                                return new string(letters);
                            })
                            .ToArray();
        var result = string.Join(' ', shuffled);

        if (result != word)
            return result;

        for (var i = 0; i < parts.Length; i++)
            if (parts[i].Distinct().Count() > 1)
            {
                shuffled[i] = parts[i][1..] + parts[i][0];

                return string.Join(' ', shuffled);
            }

        return word;
    }
}
```

Note on the "AB" test: when the shuffle gives "AB" back, turning it by one gives "BA". On the "MILETH VILLAGE" test: if only one part comes back unchanged, the whole result still differs, so it's returned as it is.

- [ ] **Step 4: Run** the Verify command. Expected: all pass.

```json:metadata
{"files": ["Chaos/Services/College/CollegeTools.cs", "Chaos/Services/College/ScrambleSession.cs", "Tests/Chaos.Tests/College/ScrambleSessionTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/ScrambleSessionTests/*\"", "acceptanceCriteria": ["every ScrambleSessionTests test passes", "scramble differs when possible, keeps spaces, AAA unchanged"], "modelTier": "standard"}
```

---

### Task 5: Service: start, play, control, tick, views, rewards, spoiler block (server repo)

**Goal:** `CollegeService` runs Hangman and Scramble as class tools under its lock, with the same refusals, clock, view and reward paths as the quiz.

**Files:**
- Create: `Chaos/Services/College/CollegeService.WordGames.cs`
- Modify: `Chaos/Services/College/CollegeService.Tools.cs`, `Chaos/Services/College/CollegeService.cs` (`FinishLocked`), `Chaos/Services/College/CollegeState.cs` (`CollegeClass.Hangman`, `CollegeClass.Scramble`), `Chaos/Services/College/ClassSession.cs` (`Shift`), `Chaos/Services/College/CollegeText.cs`
- Create: `Tests/Chaos.Tests/College/CollegeWordGameServiceTests.cs`
- Modify: `Tests/Chaos.Tests/College/CollegeToolServiceTests.cs` only where `ToolStatus`/`ToolView` constructors changed

**Acceptance Criteria:**
- [ ] Starting any tool is refused while any of the four runs, and a tool already used this class is refused; Activity classes refuse both new games.
- [ ] A Hangman start goes Prepare → (quick list) → StartHangman with a turn list; a cancel before StartHangman leaves the tool unused.
- [ ] A Scramble start from a saved list starts at once; from a quick list after QuickListStart.
- [ ] `ToolTick` drives both games; `ClassSession.Shift` moves `TurnUntilUtc`, `SolveUntilUtc` and Scramble `DeadlineUtc`.
- [ ] At class end, open games are ended; attendance failures and the one bonus per student include both games, under the 5-word guard.
- [ ] `SpoilerBlock` returns the no-spoiler text only for a non-Teacher's public message in the class room containing the open word.
- [ ] `CollegeWordGameServiceTests` and all College tests pass.

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/CollegeWordGameServiceTests/*"` → all pass

**Steps:**

- [ ] **Step 1: State.** In `CollegeClass` add after `Debate`:

```csharp
    public LiveHangman? Hangman { get; set; }
    public LiveScramble? Scramble { get; set; }
```

In `ClassSession.Shift` add:

```csharp
        if (session.Hangman is { } hangman)
        {
            hangman.TurnUntilUtc += by;
            hangman.SolveUntilUtc += by;
        }

        if (session.Scramble is { } scramble)
            scramble.DeadlineUtc += by;
```

- [ ] **Step 2: Tools file changes** (`CollegeService.Tools.cs`):
  - `ToolStatus` becomes `public sealed record ToolStatus(CollegeTool? Running, bool QuizUsed, bool DebateUsed, bool HangmanUsed, bool ScrambleUsed);` and `ToolStatusOf` fills it, looking for `ActiveHangman`/`ActiveScramble` after quiz and debate.
  - Add `private static LiveHangman? ActiveHangman(CollegeClass running) => running.Hangman is { Phase: not HangmanPhase.Ended } game ? game : null;` and the same for `ActiveScramble`.
  - `RefuseToolStartLocked`: the "running" check includes `ActiveHangman`/`ActiveScramble`; the "already used" check becomes a switch on `tool` (`Quiz` → `running.Quiz`, `Debate` → `running.Debate`, `Hangman` → `running.Hangman`, `Scramble` → `running.Scramble`) with texts `QUIZ_USED`, `DEBATE_USED`, `HANGMAN_USED`, `SCRAMBLE_USED`.
  - `ToolView` becomes `(string Room, string Teacher, CollegeTool Tool, bool Closed, QuizView? Quiz, DebateView? Debate, HangmanView? Hangman = null, ScrambleView? Scramble = null)`; `ToolViewLocked` adds the two active games after debate.
  - `ToolTick`: after the debate block, but restructured so the debate's early `return` no longer skips the new games. Use `if (ActiveDebate(running) is { } debate) { ...existing body... }`, then:

```csharp
        var present = namesInRoom.Select(CollegeState.Key).ToHashSet();

        if (ActiveHangman(running) is { } hangman && HangmanSession.Tick(hangman, now, present))
            ToolChangedLocked(running, CollegeTool.Hangman);

        if (ActiveScramble(running) is { } scramble)
        {
            present.Remove(CollegeState.Key(running.Teacher));

            if (ScrambleSession.Tick(scramble, now, present))
                ToolChangedLocked(running, CollegeTool.Scramble);
        }
```

  - `FailedToolCheck` and `BonusFor` take `CollegeClass running` instead of the separate quiz and debate, plus `IReadOnlyList<string> qualified`, plus a `ToolTops tops` record holding the precomputed top scorers per scored tool:

```csharp
    private sealed record ToolTops(IReadOnlyList<string> Quiz, IReadOnlyList<string> Hangman, IReadOnlyList<string> Scramble);

    private static string? FailedToolCheck(CollegeClass running, IReadOnlyList<string> qualified, string name)
    {
        if (running.Quiz is { Attendance: true } quiz && QuizSession.GuardMet(quiz) && !QuizSession.PassedAttendance(quiz, name))
            return CollegeText.QUIZ_ATTENDANCE_FAILED;

        if (running.Debate is { Attendance: true } debate && DebateSession.GuardMet(debate, qualified) && !DebateSession.PassedAttendance(debate, name))
            return CollegeText.DEBATE_ATTENDANCE_FAILED;

        if (running.Hangman is { Attendance: true } hangman && HangmanSession.GuardMet(hangman) && !HangmanSession.PassedAttendance(hangman, name))
            return CollegeText.HANGMAN_ATTENDANCE_FAILED;

        if (running.Scramble is { Attendance: true } scramble && ScrambleSession.GuardMet(scramble) && !ScrambleSession.PassedAttendance(scramble, name))
            return CollegeText.SCRAMBLE_ATTENDANCE_FAILED;

        return null;
    }

    private static string? BonusFor(CollegeClass running, IReadOnlyList<string> qualified, ToolTops tops, string name)
    {
        var key = CollegeState.Key(name);

        if (running.Quiz is { Bonus: true } quiz && QuizSession.GuardMet(quiz) && tops.Quiz.Contains(key))
            return CollegeText.QUIZ_BONUS;

        if (running.Debate is { Bonus: true } debate && DebateSession.GuardMet(debate, qualified) && debate.BestSpeakers.Contains(key))
            return CollegeText.DEBATE_BONUS;

        if (running.Hangman is { Bonus: true } hangman && HangmanSession.GuardMet(hangman) && tops.Hangman.Contains(key))
            return CollegeText.HANGMAN_BONUS;

        if (running.Scramble is { Bonus: true } scramble && ScrambleSession.GuardMet(scramble) && tops.Scramble.Contains(key))
            return CollegeText.SCRAMBLE_BONUS;

        return null;
    }
```

  Hangman tops are taken over `qualified` students who are also on the turn list (`hangman.Players`), because unlisted students never score anyway.

- [ ] **Step 3: FinishLocked** (`CollegeService.cs`): after ending quiz and debate add `if (running.Hangman is { Phase: not HangmanPhase.Ended } hangman) HangmanSession.End(hangman);` and the same for Scramble. Replace the `topScorers` line with:

```csharp
            var tops = new ToolTops(
                running.Quiz is { } finished ? QuizSession.TopScorers(finished, result.Qualified) : [],
                running.Hangman is { } played ? WordGameScores.TopScorers(played.Scores, result.Qualified) : [],
                running.Scramble is { } raced ? WordGameScores.TopScorers(raced.Scores, result.Qualified) : []);
```

and update the two calls to the new signatures. Also clear any pending word-game setup for the class's Teacher (`PendingWordGames.Remove(CollegeState.Key(running.Teacher))`).

- [ ] **Step 4: Texts** (`CollegeText.cs`), each at most 45 characters:

```csharp
    public const string HANGMAN_USED = "Hangman was already played this class.";
    public const string SCRAMBLE_USED = "Scramble was already played this class.";
    public const string HANGMAN_READY = "Hangman is ready. Press Next word.";
    public const string SCRAMBLE_READY = "Scramble is ready. Press Next word.";
    public const string NO_HANGMAN = "No Hangman game is running.";
    public const string NO_SCRAMBLE = "No Scramble game is running.";
    public const string NO_PENDING_GAME = "Start the game at the lectern first.";
    public const string NO_PLAYERS = "Tick at least one player.";
    public const string NOT_YOUR_TURN = "It is not your turn.";
    public const string SOMEONE_ANSWERING = "Someone is answering.";
    public const string BUZZ_LOCKED = "You already guessed this word.";
    public const string NOT_PLAYING = "You are not on the turn list.";
    public const string WORD_OPEN = "Wait for this word to end.";
    public const string LAST_WORD = "That was the last word.";
    public const string GUESS_TOO_FAST = "Slow down a little.";
    public const string NO_SPOILERS = "No spoilers while the word is open.";
    public const string HANGMAN_ATTENDANCE_FAILED = "You sat out Hangman: no class reward.";
    public const string SCRAMBLE_ATTENDANCE_FAILED = "You sat out Scramble: no class reward.";
    public const string HANGMAN_BONUS = "Hangman champion! A bonus roll.";
    public const string SCRAMBLE_BONUS = "Scramble champion! A bonus roll.";
    public const string QUICK_LIST_COUNT = "A quick list needs 1 to 10 words.";
```

(Match the wording style of the quiz's existing `QUIZ_ATTENDANCE_FAILED` and `QUIZ_BONUS`; read them first.)

- [ ] **Step 5: Service methods** (`CollegeService.WordGames.cs`). The pending setup lives in memory only (not saved), keyed by Teacher key:

```csharp
using Chaos.DarkAges.Definitions;

namespace Chaos.Services.College;

/// <summary>A word game chosen at the lectern but not started yet: the Teacher still has to type a quick list or set the turn order.</summary>
public sealed record WordGameSetup(
    CollegeTool Tool,
    string Room,
    string ListId,
    string Title,
    IReadOnlyList<CollegeWord> Words,
    HangmanMode Mode,
    int Seconds,
    bool Attendance,
    bool Bonus);

/// <summary>What the lectern or panel shows next after a word-game step.</summary>
public enum WordGameNext
{
    Refused,
    QuickList,
    TurnOrder,
    Started
}

public sealed record WordGameStep(WordGameNext Next, string Message, int WordCount = 0);

public sealed record HangmanView(
    string Title,
    int Number,
    int Count,
    HangmanMode Mode,
    HangmanPhase Phase,
    HangmanOutcome Outcome,
    string Word,
    string Mask,
    string Hint,
    string Tried,
    int Misses,
    IReadOnlyList<string> Players,
    IReadOnlyList<string> Removed,
    IReadOnlyDictionary<string, string> Names,
    IReadOnlyDictionary<string, int> Scores,
    string? TurnKey,
    int TurnSecondsLeft,
    string? Solver,
    int SolveSecondsLeft,
    IReadOnlyList<string> LockedOut,
    IReadOnlyList<string> Events,
    IReadOnlyList<(string Name, int Score)> Leaders);

public sealed record ScrambleView(
    string Title,
    int Number,
    int Count,
    ScramblePhase Phase,
    string Word,
    string Scrambled,
    string Hint,
    int Seconds,
    int SecondsLeft,
    IReadOnlyList<string> Solvers,
    IReadOnlyDictionary<string, string> Names,
    IReadOnlyDictionary<string, int> Scores,
    IReadOnlyList<(string Name, int Score)> Leaders);

public sealed partial class CollegeService
{
    private readonly Dictionary<string, WordGameSetup> PendingWordGames = [];
    private readonly Random WordGameRandom = new();

    /// <summary>Start Hangman at the lectern. <paramref name="listId" /> "" means a quick list.</summary>
    public WordGameStep PrepareHangman(string teacher, string room, string listId, HangmanMode mode, bool attendance, bool bonus);

    /// <summary>Start Scramble at the lectern. A saved list starts at once; "" asks for a quick list.</summary>
    public WordGameStep PrepareScramble(string teacher, string room, string listId, int seconds, bool attendance, bool bonus);

    /// <summary>The quick-list editor's Start: TurnOrder for Hangman, Started for Scramble.</summary>
    public WordGameStep QuickListStart(string teacher, string room, CollegeWordList list);

    /// <summary>The turn-order window's Start. <paramref name="inRoom" /> is everyone on the map; names not in it are dropped.</summary>
    public WordGameStep StartHangman(string teacher, string room, IReadOnlyList<string> order, IReadOnlyCollection<string> inRoom);

    public void CancelWordGame(string teacher);

    /// <summary>The pending setup, for the panel to build the turn-order window and the quick-list editor.</summary>
    public WordGameSetup? PendingWordGame(string teacher);

    /// <summary>Empty when it worked; otherwise the orange-bar text.</summary>
    public string PickLetter(string name, string room, char letter);
    public string Buzz(string name, string room);
    public string SolveHangman(string name, string room, string answer);
    public string ControlHangman(string by, bool isAdmin, string room, HangmanControlType control, string playerName);
    public ScrambleGuessResult GuessScramble(string name, string room, string text);
    public string ControlScramble(string by, bool isAdmin, string room, ScrambleControlType control);

    /// <summary>The no-spoiler text when <paramref name="message" /> would give away the open word on that map; otherwise null.</summary>
    public string? SpoilerBlock(string name, string mapInstanceId, string message);
}
```

Behaviour each method must have (all under `using var scope = Sync.EnterScope();`):

  - **PrepareHangman / PrepareScramble:** `RefuseToolStartLocked(teacher, room, tool)` → `Refused` with its message. Scramble `seconds` must be in `QuizSeconds` and Hangman `mode` must be defined; otherwise `Refused` with `TOOL_BAD_SETTINGS`. For a saved list, use the `OwnedWords`-style lookup under the lock (owned and present, else `Refused` with `WORD_LIST_GONE`), and `WordListRules.CheckSave` on it (else `Refused` with `WordListProblemText`). Store `PendingWordGames[key] = new WordGameSetup(...)`, with empty words for a quick list. Return `QuickList` for a quick list, otherwise `TurnOrder` for Hangman. For Scramble from a saved list, start right away: `running.Scramble = ScrambleSession.Start(...)`, then `ToolChangedLocked(running, CollegeTool.Scramble)`, `SaveLocked()`, remove the pending setup, and return `Started` with `SCRAMBLE_READY`.
  - **QuickListStart:** needs a pending setup for this Teacher in this room, else `Refused` with `NO_PENDING_GAME`. Run `WordListRules.Normalize(list)`, then `CheckQuick`. On a `WordCount` failure return `Refused` with `QUICK_LIST_COUNT`; on any other failure return `Refused` with `WordListProblemText`. Run `RefuseToolStartLocked` again. Set the setup's Words, Title "Quick list" and ListId "". For Hangman, return `TurnOrder` with `WordCount`. For Scramble, start it as above and return `Started`.
  - **StartHangman:** needs a pending Hangman setup with words, else `Refused` with `NO_PENDING_GAME`. Run `RefuseToolStartLocked` again. The players are `order`, keeping only names in `inRoom` (ignoring case), never the Teacher, with no duplicates. With no players left, return `Refused` with `NO_PLAYERS`. Otherwise set `running.Hangman = HangmanSession.Start(...)`, remove the pending setup, call `ToolChangedLocked`, call `SaveLocked`, and return `Started` with `HANGMAN_READY`.
  - **CancelWordGame:** removes the pending setup.
  - **PickLetter, Buzz, SolveHangman:** the class must run in `room` and the caller must not be its Teacher. Otherwise return `NO_CLASS_HERE`, or empty string for the Teacher. Call the session method. On success call `ToolChangedLocked` and return empty string. On failure:
    - Pick: `NOT_YOUR_TURN`.
    - Buzz: `NotPlaying` → `NOT_PLAYING`, `Busy` → `SOMEONE_ANSWERING`, `LockedOut` → `BUZZ_LOCKED`, `NotNow` → empty string.
    - Solve: empty string.
  - **ControlHangman:** needs `ControlsLocked`, else `TOOL_TEACHER_ONLY`; needs `ActiveHangman`, else `NO_HANGMAN`.
    - `Next` maps `NotNow` → `WORD_OPEN` and `NoMoreWords` → `LAST_WORD`.
    - `Skip` returns empty either way.
    - `AddPlayer`/`RemovePlayer` use `playerName`. AddPlayer refuses the Teacher's own name.
    - `End` calls `HangmanSession.End` and then `SaveLocked`.
    - Every change calls `ToolChangedLocked`.
  - **GuessScramble:** the class must run in the room, the caller must not be its Teacher, and a Scramble must be active; otherwise `NotNow`. Call `ToolChangedLocked` on `Right` only, so the solved count updates for everyone. A `Wrong` guess changes nothing anyone else sees; the panel answers only the guesser (Task 6).
  - **ControlScramble:** works like ControlQuiz. `Next` uses `ScrambleSession.Next(game, Now, WordGameRandom)` and maps `NotNow` → `WORD_OPEN`, `NoMoreWords` → `LAST_WORD`.
  - **SpoilerBlock:** return early when `!Options.IsRoom(mapInstanceId)`, the same way `FloorBlock` does. Then take the lock and return null in any of these cases:
    - no class runs in that room
    - the caller is the Teacher
    - no Hangman with Phase Open or Solving, and no Scramble with Phase Open, is running

    Otherwise return `NO_SPOILERS` when `WordListRules.Spoils(message, word)`, else null.
  - **Views:** `HangmanViewOf(LiveHangman, DateTime now)` and `ScrambleViewOf(LiveScramble, DateTime now)` work like `QuizViewOf`. They hold the full word and the Names/Scores copies, and leaders come from `WordGameScores.Leaders`. Seconds-left fields are 0 unless that clock is running. The panel strips the word for students in Task 6.

- [ ] **Step 6: Tests** (`CollegeWordGameServiceTests.cs`). Build them like `CollegeToolServiceTests` (read its fixture: how it books and starts a Lecture class in a room, moves the clock, and finishes a class). One test each:
  1. PrepareHangman with a saved list returns TurnOrder; StartHangman with ["Aroha","Kael","Ghost","Teacher"] and inRoom ["Aroha","Kael","Teacher"] gives Players ["aroha","kael"]; `ToolStatusOf` reports Running Hangman.
  2. CancelWordGame before StartHangman: `ToolStatusOf` shows HangmanUsed false, and a new PrepareHangman works.
  3. While Hangman runs, PrepareScramble and StartQuiz are refused with `TOOL_RUNNING`. After End, PrepareHangman is refused with `HANGMAN_USED`.
  4. In an Activity class, PrepareScramble is refused with `TOOL_WRONG_FORMAT`.
  5. PrepareScramble with a saved list returns Started; with "" it returns QuickList, then QuickListStart with 3 words returns Started; QuickListStart with 11 words returns Refused `QUICK_LIST_COUNT`.
  6. ToolTick skips a Hangman turn after 20 s, and ends a Scramble word at the deadline.
  7. FinishLocked path, using the fixture's finish helper: Hangman with Attendance, 5 ended words, and a qualified student who took 0 of 3 turns → that student gets `HANGMAN_ATTENDANCE_FAILED` and no roll. With Bonus, the top scorer gets one bonus roll, even when they also top a quiz with Bonus on in the same class (one bonus only).
  8. The guard: with only 4 ended words, nobody fails attendance or gets a bonus from Hangman.
  9. SpoilerBlock: a student's "it's glioca" while the word is open → `NO_SPOILERS`; the Teacher → null; after the word is revealed → null; on another map → null.
  10. `ClassSession.Shift` moves the Hangman turn and solve clocks and the Scramble deadline.

- [ ] **Step 7: Run** the Verify command, then `--treenode-filter "/*/*/College*/*"`. Expected: all pass.

```json:metadata
{"files": ["Chaos/Services/College/CollegeService.WordGames.cs", "Chaos/Services/College/CollegeService.Tools.cs", "Chaos/Services/College/CollegeService.cs", "Chaos/Services/College/CollegeState.cs", "Chaos/Services/College/ClassSession.cs", "Chaos/Services/College/CollegeText.cs", "Tests/Chaos.Tests/College/CollegeWordGameServiceTests.cs", "Tests/Chaos.Tests/College/CollegeToolServiceTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/CollegeWordGameServiceTests/*\"", "acceptanceCriteria": ["one tool at a time, once per class, Activity refused", "Hangman prepare/quick/order/start flow; cancel leaves unused", "Scramble saved starts at once, quick after QuickListStart", "ToolTick drives both; Shift moves new clocks", "class end: attendance and one bonus include both games under guard", "SpoilerBlock only for non-Teacher in class room with open word", "service and College tests pass"], "modelTier": "frontier"}
```

---

### Task 6: Delivery: panel actions, per-viewer displays, room ticks, chat block (server repo)

**Goal:** Packets reach the service, and every viewer gets the right board or card, with the word hidden from students until a word ends.

**Files:**
- Modify: `Chaos/Services/College/CollegePanel.cs`
- Modify: the College room map script that calls `College.ToolTick` and `Panel.BroadcastTool` (find it with a search for `ToolTick(`); it already passes everyone on the map, so only check it needs nothing new
- Modify: `Chaos/Services/Servers/WorldServer.cs` (public message handler: the `FloorBlock` call)
- Create: `Tests/Chaos.Tests/College/CollegeWordGameMappingTests.cs` (like `CollegeToolMappingTests.cs`)

**Acceptance Criteria:**
- [ ] Each new action type calls its service method; refusals and results come back to the sender as orange-bar text or a `WordListResult`.
- [ ] `HangmanBoard` to a student never carries `Word` while the phase is Open or Solving; the Teacher's always does. `ScrambleCard` to a student carries `Word` and `Solvers` only once Revealed or Ended.
- [ ] Per-viewer fields are right: `MyTurn`, `ISolve`, `CanBuzz`, `MyScore`, `MyPlace`, `IsTeacher`, `Addable` (Teacher only), `Students`.
- [ ] A spoiler message is not sent, and its sender sees `NO_SPOILERS`.
- [ ] Mapping tests pass.

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/CollegeWordGameMappingTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Read** `CollegePanel.cs` around `HandleClassTool` (about line 355), its quiz-list handlers (QuizOpen/QuizSave/QuizDelete/QuizCopy), `BroadcastTool`, and the static mapping functions that `CollegeToolMappingTests` covers.
- [ ] **Step 2: Word-list actions** next to the quiz-list ones:
  - `WordListOpen` with an empty id sends `WordLists` (the caller's rows).
  - `WordListOpen` with an id sends `WordListEdit` in Saved or Preview mode, depending on `readOnly`.
  - `WordListSave`, `WordListDelete`, `WordListCopy` and `WordBankCopy` send `WordListResult`. After a success they also send `WordLists`, the way the quiz handlers resend `QuizList`.
  - `WordBankPage` sends `WordBank`.

  A new list is opened with an empty `WordListEdit` (Saved mode, empty id), built on the client. The server does nothing for "New list".
- [ ] **Step 3: Game actions** in `HandleClassTool`:
  - `QuickListStart`: call `College.QuickListStart`. `Refused` → orange bar; `TurnOrder` → send the order window (step 4); `Started` → `BroadcastTool(map)` plus an orange bar.
  - `HangmanOrder`: call `College.StartHangman(source.Name, map.InstanceId, args.Names, <names of aislings on the map>)`, then broadcast or show the orange bar.
  - `WordGameCancel`: call `College.CancelWordGame`.
  - `HangmanPick`, `HangmanBuzz` and `HangmanSolve`: call the service with `(char)args.Letter` and `args.Text`. Non-empty text goes to the orange bar.
  - `HangmanControl`: `args.Names.FirstOrDefault() ?? ""` is the player name.
  - `ScrambleGuess`: `TooFast` → `GUESS_TOO_FAST` on the orange bar. `Wrong` → send only the guesser their own `ScrambleCard` with `WrongGuess = true` (built from `College.ToolNow(room)` with `ScrambleInfoFor`). `Right` → broadcast as usual.
  - `ScrambleControl`.
  - After any change, call `BroadcastTool(map)`, as the quiz does.
- [ ] **Step 4: Order window and quick editor.** Add `public void ShowTurnOrder(Aisling teacher)`. It sends `HangmanOrder`, with `OrderNames` set to everyone on the teacher's map except the teacher, and `Message` set to `$"{setup.Words.Count} words · {setup.Mode}"`. Add `public void ShowQuickList(Aisling teacher, CollegeTool tool)`. It sends `WordListEdit` in Quick mode with an empty draft and `Message` set to `CollegeText.QUICK_LIST_SHORT`. The lectern script (Task 7) calls both.
- [ ] **Step 5: Per-viewer infos.** In the place where `BroadcastTool` turns a `ToolView` into `CollegeDisplayArgs` for each aisling on the map, add the two new tools. Write the mapping as two internal static functions so tests can call them:

```csharp
    internal static CollegeHangmanInfo HangmanInfoFor(HangmanView view, string viewer, bool isTeacher, IReadOnlyCollection<string> roomNames)
    internal static CollegeScrambleInfo ScrambleInfoFor(ScrambleView view, string viewer, bool isTeacher, int students)
```

  Rules:
  - **Hangman `Word`:** sent when `isTeacher` or the phase is Revealed or Ended; otherwise "".
  - **Hangman `Players`:** in list order, with `IsTurn` = the key equals `TurnKey`, `LockedOut` from the view, and `Removed` set for removed keys.
  - **Hangman viewer fields:**
    - `MyTurn` = the viewer's key equals `TurnKey` and the phase is Open.
    - `ISolve` = the viewer is `Solver`.
    - `CanBuzz` = the phase is Open, the viewer is on the list and not removed, and not locked out.
    - `MyScore` comes from `Scores`.
    - `Solver` is the solver's display name.
    - `Addable` (Teacher only) = `roomNames` minus the teacher minus playing players.
  - **Scramble `Word` and `Solvers`:** `Word` is sent when `isTeacher` or Revealed/Ended. `Solvers` (as names) uses the same condition.
  - **Scramble viewer fields:**
    - `MyPlace` = the viewer's 1-based position in `Solvers`, or 0.
    - `SolvedCount` = `Solvers.Count`.
    - `Students` = everyone on the map minus the Teacher.

  Clamp every byte field with `(byte)Math.Min(x, byte.MaxValue)`. `ToolClosed` for Hangman and Scramble already works through `ToolView.Closed`; check that the close builder doesn't switch only over Quiz and Debate.
- [ ] **Step 6: Chat block.** In `WorldServer`, next to the `FloorBlock` check on public messages, add:

```csharp
        if (CollegeService.SpoilerBlock(aisling.Name, aisling.MapInstance.InstanceId, message) is { } spoiler)
        {
            aisling.SendOrangeBarMessage(spoiler);

            return;
        }
```

  Use the same service accessor and message variable that the `FloorBlock` code uses. Run it after `FloorBlock`.
- [ ] **Step 7: Mapping tests** (`CollegeWordGameMappingTests.cs`). Build a `HangmanView` and a `ScrambleView` by hand, then assert:
  1. A student's info in Open has `Word == ""` and the right `Mask`; the Teacher's has the word.
  2. Revealed: the student gets the word.
  3. `MyTurn`, `CanBuzz` false when locked out, `ISolve`, and `Addable` (Teacher only, excludes players and the Teacher).
  4. Scramble student in Open: `Word == ""`, `Solvers` empty, `SolvedCount == 2`, and `MyPlace == 2` for the second solver.
  5. Scramble Revealed: `Word` and `Solvers` (names) are present.

- [ ] **Step 8: Run** the Verify command, then build `Chaos/Chaos.csproj`. Expected: tests pass and the build succeeds.

```json:metadata
{"files": ["Chaos/Services/College/CollegePanel.cs", "Chaos/Services/Servers/WorldServer.cs", "Tests/Chaos.Tests/College/CollegeWordGameMappingTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/CollegeWordGameMappingTests/*\"", "acceptanceCriteria": ["each new action reaches its service method", "student board/card never hold the word before the end", "per-viewer fields correct", "spoiler messages blocked with NO_SPOILERS", "mapping tests pass"], "modelTier": "standard"}
```

---

### Task 7: Lectern and Registrar dialogs, Unora templates (server and Unora repos)

**Goal:** A Teacher can start Hangman and Scramble from the lectern, and open "My word lists" from the lectern and the Registrar.

**Files:**
- Modify: `Chaos/Scripting/DialogScripts/Temuair/College/CollegeLecternScript.cs` (server worktree)
- Modify: the Registrar's dialog script, i.e. the one that offers "My quizzes" (search `college_lectern_quizzes` and the Registrar's equivalent key)
- Create (Unora worktree, `Data/Configuration/Templates/Dialogs/Temauir/mileth/college/`): `college_lectern_hangman_pick.json`, `college_lectern_hangman_mode.json`, `college_lectern_hangman_attend.json`, `college_lectern_hangman_bonus.json`, `college_lectern_scramble_pick.json`, `college_lectern_scramble_time.json`, `college_lectern_scramble_attend.json`, `college_lectern_scramble_bonus.json`, `college_lectern_wordlists.json`, and the Registrar's word-list dialog, named after its quiz one
- Modify (Unora): `Data/Configuration/Templates/Merchants/Temauir/mileth/college_lectern.json` only if menu options live there rather than in the script

**Acceptance Criteria:**
- [ ] The lectern menu gains "Start Hangman", "Start Scramble" and "My word lists". The rules for showing them are the same as "Start a quiz" (Teacher of the running class in this room) and "My quizzes".
- [ ] Hangman steps: pick a list (own lists plus "Type a quick list") → mode (Casual, Survival) → attendance → bonus. Then the panel's `ShowQuickList` or `ShowTurnOrder` runs, or an orange bar shows the refusal.
- [ ] Scramble steps: pick a list → time (15/20/30/45/60) → attendance → bonus. Then a start, `ShowQuickList`, or a refusal.
- [ ] Every option is at most 35 characters, every text at most 360, and there are no em dashes.
- [ ] The server builds, and a Unora JSON check passes (each new file parses; keys match file names).

**Verify:** `dotnet build Chaos/Chaos.csproj -v q` in the server worktree → Build succeeded; then `python -c "import json,glob;[json.load(open(f,encoding='utf-8')) for f in glob.glob(r'C:/Users/Michael/Documents/GitHub/worktrees/word-games-unora/Data/Configuration/Templates/Dialogs/Temauir/mileth/college/college_lectern_*.json')]"` → no error

**Steps:**

- [ ] **Step 1: Read** `CollegeLecternScript.cs` fully. It carries `QuizSetup` through the dialog with context/`MenuArgs`; the quiz steps are `college_lectern_quiz_pick`, `_quiz_time`, `_quiz_attend` and `_quiz_bonus`. Also read those four Unora templates.
- [ ] **Step 2: Templates.** Copy each quiz template to its Hangman/Scramble counterpart, changing `templateKey`, text and options:
  - `hangman_pick`: "Which word list?"; the options are built by the script, the same way quiz_pick lists quizzes.
  - `hangman_mode`: "Casual: no miss limit. Survival: six misses and the word is lost." Options "Casual" and "Survival".
  - `hangman_attend` / `scramble_attend`: copy quiz_attend's text, with "the game" instead of "the quiz".
  - `hangman_bonus` / `scramble_bonus`: copy quiz_bonus's text the same way.
  - `scramble_time`: "How long for each word?"; options as quiz_time.
  - `wordlists`: what `college_lectern_quizzes` does (it opens the editor), pointing at word lists.
- [ ] **Step 3: Script.** Add the menu options with `InsertOption` where "Start a quiz" is inserted. Add a `WordGameChoice` record (tool, list id, mode, seconds, attendance, bonus), carried the same way as `QuizSetup`.
  - The pick step lists `College.WordListsOf(source.Name)` titles plus a last option "Type a quick list".
  - After the bonus step:
    - Hangman: call `College.PrepareHangman(...)`.
    - Scramble: call `College.PrepareScramble(...)`.
  - Then act on the step's `Next`:
    - `QuickList` → `Panel.ShowQuickList(source, tool)`.
    - `TurnOrder` → `Panel.ShowTurnOrder(source)`.
    - `Started` → `Panel.BroadcastTool(map)` plus an orange bar.
    - `Refused` → orange bar.

    Close the dialog in every case.
  - "My word lists" sends `WordLists`, the same way "My quizzes" sends `QuizList`.
  - With no saved lists, the pick step still offers "Type a quick list".
- [ ] **Step 4: Registrar.** Add "My word lists" next to "My quizzes", with the same visibility rule.
- [ ] **Step 5: Run** the Verify commands.

```json:metadata
{"files": ["Chaos/Scripting/DialogScripts/Temuair/College/CollegeLecternScript.cs", "Data/Configuration/Templates/Dialogs/Temauir/mileth/college/college_lectern_hangman_pick.json", "Data/Configuration/Templates/Dialogs/Temauir/mileth/college/college_lectern_hangman_mode.json", "Data/Configuration/Templates/Dialogs/Temauir/mileth/college/college_lectern_hangman_attend.json", "Data/Configuration/Templates/Dialogs/Temauir/mileth/college/college_lectern_hangman_bonus.json", "Data/Configuration/Templates/Dialogs/Temauir/mileth/college/college_lectern_scramble_pick.json", "Data/Configuration/Templates/Dialogs/Temauir/mileth/college/college_lectern_scramble_time.json", "Data/Configuration/Templates/Dialogs/Temauir/mileth/college/college_lectern_scramble_attend.json", "Data/Configuration/Templates/Dialogs/Temauir/mileth/college/college_lectern_scramble_bonus.json", "Data/Configuration/Templates/Dialogs/Temauir/mileth/college/college_lectern_wordlists.json"], "verifyCommand": "dotnet build Chaos/Chaos.csproj -v q", "acceptanceCriteria": ["lectern menu gains Start Hangman, Start Scramble, My word lists", "Hangman steps list/mode/attend/bonus then quick list or turn order", "Scramble steps list/time/attend/bonus then start or quick list", "dialog limits respected", "server builds and Unora JSON parses"], "modelTier": "standard"}
```

---

### Task 8: Client models: WordListDocument, HangmanBoardState, texts (client repo)

**Goal:** Graphics-free state for the editor and the board, with tests, so the windows stay thin.

**Files:**
- Create: `Chaos.Client/ViewModel/College/WordListDocument.cs`, `Chaos.Client/ViewModel/College/HangmanBoardState.cs`
- Modify: `Chaos.Client/ViewModel/College/ClassToolText.cs`
- Create: `Tests/Chaos.Client.Tests/College/WordListDocumentTests.cs`, `Tests/Chaos.Client.Tests/College/HangmanBoardStateTests.cs`
- Modify: `Tests/Chaos.Client.Tests/College/ClassToolTextTests.cs`

**Acceptance Criteria:**
- [ ] `WordListDocument` loads from and saves to `CollegeWordListDraftInfo`. It supports Add, Remove, MoveUp, MoveDown and Select, and caps at 30 words (10 in Quick mode). It is read-only in Preview mode, and tracks Dirty.
- [ ] `WordListDocument.FilterWordInput` keeps only A-Z, a-z and single spaces, upper-cases, and caps at 20 characters.
- [ ] `HangmanBoardState` answers, per letter, Tried-right / Tried-wrong / Untried. It also gives letter buttons enabled (`MyTurn && Phase == Open`), Buzz shown (`CanBuzz && !ISolve`), the solve box shown (`ISolve`), and the gallows stage (`Misses` 0-6; Casual shows the count instead).
- [ ] `ClassToolText` gains the board and card lines below, with tests.
- [ ] Client tests pass.

**Verify:** `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/word-games-server -- --no-ansi` → 0 failed

**Steps:**

- [ ] **Step 1: Read** `ViewModel/College/QuizDocument.cs` and its tests, then `ClassToolText.cs` and its tests. Copy their shape.
- [ ] **Step 2: Write tests first.** `WordListDocumentTests`:
  - load and save round-trip
  - Add caps at 30, and at 10 in Quick mode
  - Remove keeps the selection in range
  - MoveUp at index 0 and MoveDown at the last index do nothing
  - Preview mode refuses edits (each mutator returns false and leaves Dirty false)
  - `FilterWordInput("mi1leth  vil")` is `"MILETH VIL"`
  - `FilterWordInput` of 25 letters gives 20

  `HangmanBoardStateTests` builds a `CollegeHangmanInfo` with Mask "GL__CA", Tried "AGCLERS" and Word "". Expected letter states: G, L, C, A = Right; E, R, S = Wrong; B = Untried. Also cover the enabled/visible flags for each combination of MyTurn/Phase/CanBuzz/ISolve, and the gallows stage 3 for Misses 3 in Survival.

  `ClassToolTextTests` additions:
  - `HangmanTitle(info)` → `"Hangman: Mileth (2/8) · Survival"`
  - `MissLine(info)` → `"3 of 6 misses"` (Survival) or `"3 misses"` (Casual)
  - `TurnLabel(name, seconds)` → `"Kael 0:14"`
  - `ScrambleStatus(info)` → `"2 classmates have solved it"` (Open, not solved by me); after the reveal, `"You got it 2nd: +2"` or `"Nobody's place"` text per `MyPlace`
  - `Ordinal(n)` → `"1st", "2nd", "3rd", "4th"`
- [ ] **Step 3: Implement** to make the tests pass. The letter state is right when the letter is in Tried and in the Mask (or in Word when Word isn't empty), and wrong when it's in Tried but not there.
- [ ] **Step 4: Run** the Verify command.

```json:metadata
{"files": ["Chaos.Client/ViewModel/College/WordListDocument.cs", "Chaos.Client/ViewModel/College/HangmanBoardState.cs", "Chaos.Client/ViewModel/College/ClassToolText.cs", "Tests/Chaos.Client.Tests/College/WordListDocumentTests.cs", "Tests/Chaos.Client.Tests/College/HangmanBoardStateTests.cs", "Tests/Chaos.Client.Tests/College/ClassToolTextTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/word-games-server -- --no-ansi", "acceptanceCriteria": ["WordListDocument load/save/edit/caps/preview/dirty", "FilterWordInput keeps A-Z and single spaces, max 20", "HangmanBoardState letter states and flags", "ClassToolText new lines with tests", "client tests pass"], "modelTier": "standard"}
```

---

### Task 9: Client word-list editor window (client repo)

**Goal:** "My word lists" opens a two-page editor with Mine and Word bank tabs, plus a Quick mode with a Start button.

**Files:**
- Create: `Chaos.Client/Controls/World/Popups/College/WordListEditorControl.cs`

**Acceptance Criteria:**
- [ ] Page 1, Mine tab: rows "Title · N words" with Open, New list, Copy and Delete.
- [ ] Page 1, Word bank tab: rows "Title · Owner · N words" with Preview, Copy to mine, and page arrows; switching to the tab sends `WordBankPage` 0.
- [ ] Page 2: a Title box, the word list on the left (Add, Up, Down, Remove), and on the right a Word box (filtered by `FilterWordInput`) and a Hint box (60 characters). It has Save, Back and a status line that shows the server's `WordListResult` message.
- [ ] Preview mode: every box is read-only, and Save is replaced by "Copy to mine".
- [ ] Quick mode: no Title box and no Back. Start sends `QuickListStart`; Cancel sends `WordGameCancel` and closes. The status line shows the server's Message (the under-5 note).
- [ ] The window is the same size and in the same place as the quiz editor, and the build succeeds.

**Verify:** `dotnet build Chaos.Client/Chaos.Client.csproj -v q -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/word-games-server` → Build succeeded

**Steps:**

- [ ] **Step 1: Read** `QuizEditorControl.cs` fully (19 KB). It is the template: frame, two pages, the list widget, text boxes, the status line, how it sends `CollegeActionArgs`, and how `WorldScreen.College.cs` feeds it `QuizList`/`QuizEdit`/`QuizResult`.
- [ ] **Step 2: Build** `WordListEditorControl` by copying that structure. Its public entry points mirror the quiz editor's:
  - `ShowList(IReadOnlyList<CollegeWordListRowInfo>)`
  - `ShowBank(byte page, byte pageCount, IReadOnlyList<CollegeWordListRowInfo>)`
  - `ShowEdit(string id, WordListEditMode mode, CollegeWordListDraftInfo? draft, string message)`
  - `ShowResult(string id, WordListResultCode code, string message)`
  - an event `Action<CollegeActionArgs>? Send`

  State lives in `WordListDocument`. The tab buttons are two text buttons drawn like the quiz editor's buttons.
- [ ] **Step 3: Build** and fix warnings (`EnforceCodeStyleInBuild` is on).

```json:metadata
{"files": ["Chaos.Client/Controls/World/Popups/College/WordListEditorControl.cs"], "verifyCommand": "dotnet build Chaos.Client/Chaos.Client.csproj -v q -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/word-games-server", "acceptanceCriteria": ["Mine tab rows and buttons", "Word bank tab rows, preview, copy, paging", "page 2 editor with filtered word box, hint, save, back, status", "Preview read-only with Copy to mine", "Quick mode Start/Cancel", "same frame as quiz editor; build succeeds"], "modelTier": "standard"}
```

---

### Task 10: Client Hangman windows: board, turn order, Teacher panel (client repo)

**Goal:** The centred Hangman board for players and watchers, the Teacher's turn-order window, and the Teacher side panel, all as mocked.

**Files:**
- Create: `Chaos.Client/Controls/World/Popups/College/HangmanBoardControl.cs`, `Chaos.Client/Controls/World/Popups/College/HangmanTurnOrderControl.cs`, `Chaos.Client/Controls/World/Popups/College/HangmanTeacherPanel.cs`

**Acceptance Criteria:**
- [ ] **Board** (centred like `QuizCardControl`), from top to bottom:
  - the title line from `ClassToolText.HangmanTitle`, with "You: N" on the right
  - a gallows drawn in 7 stages (Survival) or the miss line (Casual)
  - the mask in a large monospace font with spaced letters, and the hint
  - the turn strip: up to 5 names around the current one plus "+N", the current one in gold with its countdown, the viewer's name dashed
  - 26 letter buttons in two rows of 13, coloured right, wrong or untried, and dimmed unless `MyTurn`
  - the red BUZZ button, or the solve box with its countdown when `ISolve`
  - the event line
- [ ] Clicking a letter sends `HangmanPick`; BUZZ sends `HangmanBuzz`; Enter or Solve sends `HangmanSolve` with the typed text; typing a letter key while `MyTurn` (and the solve box isn't focused) picks it.
- [ ] Revealed: the full word is shown (green when solved, red when lost), with the top three. Ended: the final scores, then the board closes on `ToolClosed`.
- [ ] **Turn order window:** a tick box per name (all ticked), drag or Up/Down to reorder, Shuffle and Tick all. Start sends `HangmanOrder` with the ticked names in order; Cancel sends `WordGameCancel`.
- [ ] **Teacher panel** (a side panel like `QuizTeacherPanel`):
  - the answer in green, the mask and the miss line
  - the player rows (name, score), with the turn highlighted and removed players greyed
  - Add player (a small list of `Addable`), Remove (removes the selected row), Skip turn, Next word (lit in Waiting/Revealed) and End game, each sending `HangmanControl`
- [ ] The build succeeds.

**Verify:** `dotnet build Chaos.Client/Chaos.Client.csproj -v q -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/word-games-server` → Build succeeded

**Steps:**

- [ ] **Step 1: Read** `QuizCardControl.cs`, `QuizTeacherPanel.cs` and `DebatePanel.cs` (its student and Teacher views), plus the mockups `hangman-board-v2.html` (option A) and `hangman-teacher.html` in `Chaos.Client/.superpowers/brainstorm/139309-1791855981/content/` of the main client checkout (read-only; the folder is not in the worktree).
- [ ] **Step 2: Build** the three controls, with state from `HangmanBoardState` and texts from `ClassToolText`. Draw the gallows from rectangles and lines with the same primitives `ImageUtil`/`UIPanel` drawing already uses (look at how `DebatePanel` or `TuneGrid` draw shapes). Stage n draws the first n parts: base, post, beam, rope+head, body, arms, legs. The countdowns use `ToolCountdown`, as the quiz card does.
- [ ] **Step 3: Build.**

```json:metadata
{"files": ["Chaos.Client/Controls/World/Popups/College/HangmanBoardControl.cs", "Chaos.Client/Controls/World/Popups/College/HangmanTurnOrderControl.cs", "Chaos.Client/Controls/World/Popups/College/HangmanTeacherPanel.cs"], "verifyCommand": "dotnet build Chaos.Client/Chaos.Client.csproj -v q -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/word-games-server", "acceptanceCriteria": ["board layout as mocked option A", "letter/buzz/solve send the right actions", "revealed and ended states", "turn order window ticks/order/shuffle/start/cancel", "Teacher panel controls", "build succeeds"], "modelTier": "standard"}
```

---

### Task 11: Client Scramble card and Teacher panel (client repo)

**Goal:** The Scramble race card and its Teacher panel, as mocked.

**Files:**
- Create: `Chaos.Client/Controls/World/Popups/College/ScrambleCardControl.cs`, `Chaos.Client/Controls/World/Popups/College/ScrambleTeacherPanel.cs`

**Acceptance Criteria:**
- [ ] **Card**, in the same place and frame as `QuizCardControl`:
  - the title with "You: N"
  - a letter tile per character (a gap for spaces), shrinking to fit 20 characters on one line
  - the hint, a time bar, a typing box and Guess (Enter also sends)
  - the status line from `ClassToolText.ScrambleStatus`
- [ ] **Card result line:** a card with `WrongGuess` shows "Not quite. Keep trying." until the next guess or the next word. A right guess (`MyPlace > 0`) locks the box.
- [ ] **Revealed:** green tiles spelling the word, "You got it Nth: +P", the solvers in order, and the top scores.
- [ ] **Teacher panel:** the scrambled tiles, the answer, "Solved N of M · 0:SS left", then after the reveal the solve order and top scores, with Next word and End game sending `ScrambleControl`.
- [ ] The build succeeds.

**Verify:** `dotnet build Chaos.Client/Chaos.Client.csproj -v q -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/word-games-server` → Build succeeded

**Steps:**

- [ ] **Step 1: Read** `QuizCardControl.cs`, `QuizTeacherPanel.cs` and the mockup `scramble-card.html`.
- [ ] **Step 2: Build** both controls. Tile width = `min(18, (innerWidth - gaps) / chars)`.
- [ ] **Step 3: Build.**

```json:metadata
{"files": ["Chaos.Client/Controls/World/Popups/College/ScrambleCardControl.cs", "Chaos.Client/Controls/World/Popups/College/ScrambleTeacherPanel.cs"], "verifyCommand": "dotnet build Chaos.Client/Chaos.Client.csproj -v q -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/word-games-server", "acceptanceCriteria": ["card layout as mocked", "guess flow and wrong/right display", "revealed state", "Teacher panel", "build succeeds"], "modelTier": "standard"}
```

---

### Task 12: Client wiring: build, route, place and close the windows (client repo)

**Goal:** `WorldScreen.College.cs` creates the new windows on first use, routes the 7 new display types, sends their actions, and closes them on `ToolClosed`.

**Files:**
- Modify: `Chaos.Client/Screens/WorldScreen.College.cs`
- Modify: `CLAUDE.md` (client repo: the College part of the UI Control System paragraph)

**Acceptance Criteria:**
- [ ] `WordLists`, `WordListEdit`, `WordBank` and `WordListResult` go to the word-list editor.
- [ ] `HangmanOrder` goes to the turn-order window.
- [ ] `HangmanBoard` goes to the board when `!IsTeacher`, and to the Teacher panel when `IsTeacher`.
- [ ] `ScrambleCard` goes to the card or the Teacher panel the same way.
- [ ] `Reopen` reopens a window the player closed, as the quiz does.
- [ ] `ToolClosed` with `Tool` Hangman or Scramble hides that tool's windows.
- [ ] On logout or map change, the windows close the same way the quiz windows do.
- [ ] `CLAUDE.md` names the new controls and models.
- [ ] The full client test suite passes and the client builds.

**Verify:** `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/word-games-server -- --no-ansi` → 0 failed

**Steps:**

- [ ] **Step 1: Read** `WorldScreen.College.cs` lines 173-276 (display routing) and how the quiz windows are created, placed, given `Send = ConnectionManager.SendCollegeAction`, and closed.
- [ ] **Step 2: Add** the new cases and fields, following that pattern exactly.
- [ ] **Step 3: Update** `CLAUDE.md`. After "DebateVoteCard — the opening and final vote card;" add: "WordListEditorControl — the word-list editor (Mine and Word bank tabs, saved, preview and quick modes; the model is ViewModel/College/WordListDocument); HangmanBoardControl — the Hangman board; HangmanTurnOrderControl — the Teacher's turn-order window; HangmanTeacherPanel — the Teacher's Hangman panel (board state is ViewModel/College/HangmanBoardState); ScrambleCardControl — the Word Scramble card; ScrambleTeacherPanel — the Teacher's Scramble panel;"
- [ ] **Step 4: Run** the Verify command and the client build.

```json:metadata
{"files": ["Chaos.Client/Screens/WorldScreen.College.cs", "CLAUDE.md"], "verifyCommand": "dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/word-games-server -- --no-ansi", "acceptanceCriteria": ["7 new displays routed", "Teacher vs student windows by IsTeacher", "Reopen and ToolClosed handled", "close on logout/map change", "CLAUDE.md updated", "client tests pass and build succeeds"], "modelTier": "standard"}
```

---

### Task 13: File server: rename owners in word lists (file-server repo)

**Goal:** AislingRenamer rewrites `CollegeState.WordLists[].Owner` the way it rewrites quiz owners.

**Files:**
- Modify: the file server's `AislingRenamer.College.cs` (find it under the `word-games-fs` worktree)
- Modify: its test file (search the tests for the quiz owner rename)

**Acceptance Criteria:**
- [ ] Renaming "Aroha" to "Arohanui" changes a word list's `Owner` from "Aroha" to "Arohanui", ignoring case, and leaves other owners alone.
- [ ] The file server's test suite passes.

**Verify:** the file server's test command (see its `CLAUDE.md`/README) → 0 failed

**Steps:**

- [ ] **Step 1: Read** `AislingRenamer.College.cs`, and find the `Quizzes` owner rewrite and its test.
- [ ] **Step 2: Write the test** for `WordLists`, copying the quiz one, and run it. Expected: it fails.
- [ ] **Step 3: Add** the same rewrite for `WordLists`, then run the suite.

```json:metadata
{"files": ["AislingRenamer.College.cs"], "verifyCommand": "file server test suite", "acceptanceCriteria": ["WordLists Owner renamed case-insensitively, others untouched", "file server tests pass"], "modelTier": "mechanical"}
```

---

### Task 14: Full checks (all repos)

**Goal:** Everything builds and every suite passes together, and the in-game checklist is ready for the user.

**Files:**
- none created (fix any breakage in the task that owns the file)

**Acceptance Criteria:**
- [ ] The full server suite has 0 failures.
- [ ] The full client suite has 0 failures, built against the server worktree.
- [ ] The file server suite has 0 failures.
- [ ] The server boots against the Unora worktree's data without College or template errors. Start it, wait for "World server started" (or the existing startup line), then stop it. Skip this step and say so if a running server holds the port.
- [ ] `git status` in each worktree lists only files this plan names, plus build outputs.

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi` (server worktree) → 0 failed

**Steps:**

- [ ] **Step 1:** Run the three suites and the server build.
- [ ] **Step 2:** Boot check. Read [[chaos-server-local-staging-dir]] in memory first: point the staging directory at the Unora worktree's `Data` only through a local, uncommitted override, and put it back afterwards.
- [ ] **Step 3:** List every changed file per worktree.

```json:metadata
{"files": [], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi", "acceptanceCriteria": ["server suite 0 failures", "client suite 0 failures", "file server suite 0 failures", "server boots on Unora worktree data", "only planned files changed"], "modelTier": "standard"}
```

---

### Task 15: Commit the full implementation

**Goal:** One commit per repo on `feat/college-word-games`, with the client's gitlink pointing at the server commit.

**Files:**
- all files changed by Tasks 1-14

**Acceptance Criteria:**
- [ ] Server, Unora and file server each have one new commit on `feat/college-word-games`.
- [ ] The client commit includes the `Chaos-Server` gitlink set to the server commit (`git update-index --cacheinfo 160000,<server sha>,Chaos-Server`).
- [ ] Each message ends with the Co-Authored-By and Claude-Session lines.
- [ ] Build outputs (`obj/`, `bin/`, Unora "Custom Client Mods" obj files) are not committed.

**Verify:** `git -C <each worktree> log --oneline -1` → the new commit

**Steps:**

- [ ] **Step 1:** In each worktree, stage only the plan's files by explicit path (`git add <paths>`, never `-A`). Then write the message to a file with UTF-8 without BOM, and run `git commit -F <file>`. Messages:
  - server: `College word games: Hangman and Word Scramble class tools, word lists and bank (CLIENT_VERSION 782)`
  - unora: `College word games: lectern and registrar dialogs for Hangman, Scramble and word lists`
  - client: `College word games: word-list editor, Hangman board and turn order, Scramble card, Teacher panels`
  - file server: `AislingRenamer: rename College word-list owners`
- [ ] **Step 2:** In the client worktree, before committing, run `git update-index --cacheinfo 160000,$(git -C ../word-games-server rev-parse HEAD),Chaos-Server`.

```json:metadata
{"files": [], "verifyCommand": "git -C C:/Users/Michael/Documents/GitHub/worktrees/word-games-client log --oneline -1", "acceptanceCriteria": ["one commit per repo on feat/college-word-games", "client gitlink = server commit", "attribution lines present", "no build outputs committed"], "modelTier": "mechanical"}
```

---

## Plan-time adjustments

- The spec lists "WordBankPage, WordBankCopy" actions and a "WordListResult" display; the plan adds `WordGameCancel` (38) so the turn-order window's and quick editor's Cancel can clear the pending setup.
- The Teacher views reuse the `HangmanBoard` and `ScrambleCard` displays with `IsTeacher`, as the spec allowed ("carry an IsTeacher flag"), instead of separate Teacher display types.
- A wrong Scramble guess is answered to the guesser alone (`CollegeScrambleInfo.WrongGuess`), so up to 2 guesses a second per student never rebroadcast every card in the room.
- Hangman top scorers use all qualified students; unlisted students have no score, so this equals "qualified and listed".
