# Mileth College Part 5 (Lecture Tools) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Teachers run quizzes and debates in Lecture and Discussion classes. Quizzes are written ahead of time in a quiz editor. Either tool can, at the Teacher's choice, act as an attendance check, give a bonus roll, or both.

**Architecture:**
- **Server rules:** pure units with no game-world access. `QuizRules` checks saved quizzes. `QuizSession` and `DebateSession` run the live tools on plain state (`LiveQuiz`, `LiveDebate`) kept in the running class, the way `ClassSession` runs `CollegeClass`.
- **Server service:** `CollegeService` gains two partial files. `CollegeService.Quizzes.cs` keeps saved quizzes (one file each through `CollegeQuizStore`, plus an index in `CollegeState`). `CollegeService.Tools.cs` starts and steers the tools, ticks them, builds views, and blocks room chat while someone else holds the floor. `FinishLocked` applies the attendance checks and bonus rolls.
- **Server delivery:** `CollegePanel` maps views to messages and sends them to everyone in the classroom. A player's action is sent at once, under the sender's map lock. Timers are sent from `CollegeRoomScript`'s new 1-second step, on the room's own update.
- **Messages:** new sub-types on the existing College message pair: actions 14-22, displays 10-17. `CLIENT_VERSION` 775.
- **Client:** a pure `QuizDocument` model and two-page `QuizEditorControl`; `QuizCardControl` and `QuizTeacherPanel`; `DebatePanel` and `DebateVoteCard`; side markers drawn by `EntityOverlayManager` from `DebateMarkTable`.

**Tech Stack:** C# 14 / .NET 10, TUnit + FluentAssertions + Moq (server and client tests), MonoGame (client).

**Spec:** `Chaos.Client/docs/superpowers/specs/2026-10-05-mileth-college-tools-design.md`. Also read the parent design `2026-10-04-mileth-college-design.md` and the part 1 and part 2 specs (`2026-10-05-mileth-college-core-design.md`, `2026-10-05-mileth-college-entries-design.md`) in the same folder.

## Global Constraints

- **Branches and folders.** Every path in a task is relative to the repo that task names:
  - server: `C:\Users\Michael\Documents\GitHub\worktrees\college-tools-server` (branch `feat/college-tools` from server `master` 3d12ce2db);
  - client: `C:\Users\Michael\Documents\GitHub\worktrees\college-tools-client` (branch `feat/college-tools` from client `main`);
  - Unora: `C:\Users\Michael\Documents\GitHub\worktrees\college-tools-unora` (branch `feat/college-tools` from Unora `main`).

  Task 0 creates them.
- **Shared checkouts.** Other Claude sessions use the main checkouts.
  - Never run `git stash`, `git reset`, `git add -A` or `git add .` anywhere. Stage by explicit path.
  - Never delete files with `rm`. Leave deletions to the user.
  - Subagents working in a worktree use absolute paths and the built-in Read/Edit/Write tools. They do NOT use Serena, which is bound to the main checkout and once edited it by mistake.
- **Builds.**
  - A running `Chaos.exe` or game client locks `bin/`. If a build fails with a file lock, ask the user to stop it.
  - Never build the server and the client at the same time.
  - The client worktree builds `Chaos.Client/Chaos.Client.csproj`, not the `.slnx`, with `-p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\college-tools-server`.
  - Client tasks compile only after server Task 1 (the shared enums and info records live in the server repo).
- **Commits:** one commit per repo at the end (Task 14). Implementers must NOT commit. Leave all changes in the working tree.
- **Tests:** `dotnet test` does not work. Run TUnit through `dotnet run`:
  - server: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/<Class>/*"`;
  - client: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\college-tools-server -- --treenode-filter "/*/*/<Class>/*"`.
- **Known server baseline:** the full server suite fails only `OnItemDroppedOn` stackable, plus `GiveAbility` if it fails on master. Any other failure is a regression.
- **Code style:**
  - Match the surrounding code. Use `Lock` with `EnterScope()`, never `lock`.
  - Comments only for a non-obvious "why". No explanatory comments in tests.
  - Test methods use the `Snake_case_sentence` names the existing College tests use.
- **Limits both sides agree on (`CollegeProtocol`):** quiz title 40 characters; 5-20 questions; question 150 characters; 2-4 answers of at most 40 characters; 20 quizzes per owner; quiz seconds 15/20/30/45/60; motion 120 characters; debate turns 1/2/3 minutes; opening vote 60 s, final vote 30 s; at most 3 bonus students; guards 5 questions and 3 speakers; `NO_ANSWER` = 255.
- **Messages:** `CollegeActionType` 14-22 (`QuizOpen`, `QuizSave`, `QuizDelete`, `QuizCopy`, `QuizAnswer`, `QuizControl`, `DebateSide`, `DebateHand`, `DebateControl`); `CollegeDisplayType` 10-17 (`QuizList`, `QuizEdit`, `QuizResult`, `QuizCard`, `QuizTeacher`, `DebateState`, `DebateMarks`, `ToolClosed`). `CLIENT_VERSION` 774 -> 775. No new opcodes.
- **Strings on the wire:** question text and the motion use `String16` (150 Korean characters can pass 255 bytes); titles, answers and names use `String8`.
- **Text limits:** dialog option text 35 characters, dialog text 360, orange bar 45, no em dashes in Unora JSON.

**User decisions (already made):**
- Both tools work in Lecture and Discussion classes of every subject.
- The Teacher picks per quiz or debate: **Counts for attendance**, **Bonus**, both or neither.
- The bonus is a second roll at an Educated Mark, at the normal chance.
- Quiz top scorers: the best score, ties included, at most 3 students, at least one right answer. Lower scores never get the bonus; 4 or more tied at the top get none.
- Quizzes are written ahead of time in a quiz editor, saved and reusable. Editor layout B: two pages (list, then one quiz).
- Student quiz view: a quiz card in the middle of the view. Teacher quiz view: a small side panel.
- A timer the Teacher picks per quiz closes each question and shows the answer; the Teacher moves on with **Next question**. One point per right answer; speed doesn't count.
- Debate: motion with opening (60 s) and final (30 s) votes, speaking turns with the floor, side markers over heads. The bonus goes to up to 3 best speakers picked by the Teacher from students who held the floor.
- Switches count only if at least 5 questions were asked or 3 different students held the floor. At most one bonus roll per student per class. A failed tool check never sinks the class's 3-student rule or the Teacher's mark.
- While someone holds the floor, other students' say and shout in that room are refused with `{Name} has the floor.`; the Teacher, the Director and admins can always talk.
- One quiz and one debate per class, one at a time. Ending a quiz mid-question drops that question.

**Plan-time adjustments to the spec (Task 13 records them in the spec):**
- Unora needs 12 new dialog templates: the lectern's quiz and debate setup chains, "My quizzes" at the lectern and the Registrar, and the lectern's "Show panel". The lectern menus are built from Unora templates.
- The quiz card lists the answers in one column of four, not a 2 x 2 grid: two columns can't fit 40-character answers.
- The debate's switches also need the final vote to have been held. A debate ended before its final vote checks no one and gives no bonus.
- Only the class's Teacher gets the Teacher panel and the Teacher's debate view. The server still accepts control actions from the Director and admins.
- Class actions (answers, sides, hands, controls) skip the 250 ms save throttle. They are limited to 120 a minute per player instead.
- Answers, sides and hands are not written to disk at once; the 30-second College tick saves them. Starting and ending a tool saves at once.
- The Teacher panel and the debate panel close with their own OK button and don't take keyboard focus. The lectern's **Show quiz panel** or **Show debate panel** brings them back. Two wire fields make this work: `CollegeDisplayArgs.Reopen` (on QuizCard, QuizTeacher and DebateState: show a window the player closed, sent to an arrival and by "Show panel") and `CollegeDebateInfo.IsTeacher` (which debate view to show).
- Side markers draw 86 px above the tile centre, over the name tag. The floor holder's marker ends in " *", since the game font has no star.
- **Delete** in the quiz editor asks with a second click and a status line, not a popup.
- The rule state is in `LiveQuiz` / `LiveDebate` and the rules in `QuizSession` / `DebateSession`, like `CollegeClass` / `ClassSession`. The spec's `LiveQuizTests` / `LiveDebateTests` are `QuizSessionTests` / `DebateSessionTests`.
- The Teacher's debate view lists the first 3 raised hands, then "+N more".
- The lectern offers **Start a quiz** and **Start a debate** only to the class's Teacher. It offers **My quizzes** to anyone who may keep quizzes, whether or not a class is running.

**Shared names (all tasks):**
- Server views: `ToolView(Room, Teacher, Tool, Closed, Quiz, Debate)`, `QuizView`, `DebateView` (Task 4). `CollegePanel.BroadcastTool(MapInstance)` and `CollegePanel.SendToolTo(Aisling)` (Task 5).
- Display fields: `CollegeDisplayArgs.QuizRows`, `.QuizId`, `.QuizDraft`, `.QuizResult`, `.QuizView`, `.Debate`, `.DebateMarks`, `.Tool`, `.Reopen` (show a window the player closed: an arrival or the lectern's "Show panel"). `CollegeDebateInfo.IsTeacher` picks the debate panel's view. Action fields: `CollegeActionArgs.QuizId`, `.QuizDraft`, `.Number`, `.Answer`, `.QuizControl`, `.Side`, `.Raised`, `.DebateControl`, `.Names`.
- Client: `QuizDocument` (Task 7) exposes `Version`, `IsDirty`, `MarkClean()` and `event Action? Changed`. `ToolCountdown` and `ClassToolText` (Task 7) are pure. `DebateMarkTable` (Task 10) holds the markers by entity id.

## File structure

**Server (`worktrees/college-tools-server`)**

| File | Change |
|---|---|
| `Chaos.DarkAges/Definitions/Enums.cs` | `QuizPhase`, `DebatePhase`, `DebateSide`, `DebateResult`, `QuizControlType`, `DebateControlType`, `CollegeTool`, `QuizResultCode`; action types 14-22; display types 10-17 (Task 1) |
| `Chaos.DarkAges/Definitions/CollegeProtocol.cs` | Quiz and debate limits (Task 1) |
| `Chaos.DarkAges/Definitions/CONSTANTS.cs` | `CLIENT_VERSION` 775 (Task 1) |
| `Chaos.Networking/Entities/Server/CollegeInfos.cs` | Quiz and debate info records (Task 1) |
| `Chaos.Networking/Entities/Client/CollegeActionArgs.cs` | Quiz and debate fields (Task 1) |
| `Chaos.Networking/Entities/Server/CollegeDisplayArgs.cs` | Quiz and debate fields (Task 1) |
| `Chaos.Networking/Converters/CollegeCodec.cs` | Read/write quiz drafts, rows, views, debates, marks, names (Task 1) |
| `Chaos.Networking/Converters/Client/CollegeActionConverter.cs` | Actions 14-22 (Task 1) |
| `Chaos.Networking/Converters/Server/CollegeDisplayConverter.cs` | Displays 10-17 (Task 1) |
| `Chaos/Services/College/CollegeQuiz.cs` (new) | `CollegeQuiz`, `QuizQuestion`, `CollegeQuizIndex` (Task 2) |
| `Chaos/Services/College/QuizRules.cs` (new) | `QuizCheck`, `QuizProblem`, `Normalize`, `CheckSave`, `CheckReady` (Task 2) |
| `Chaos/Services/College/CollegeQuizStore.cs` (new) | One JSON file per quiz (Task 2) |
| `Chaos/Services/College/CollegeService.Quizzes.cs` (new) | Keep, load, save, copy and delete quizzes (Task 2) |
| `Chaos/Services/College/CollegeService.cs` | Quiz store in the constructor (Task 2); `FinishLocked` checks and bonus rolls, tool close (Task 4) |
| `Chaos/Services/College/CollegeState.cs` | `Quizzes` index (Task 2); `CollegeClass.Quiz`, `.Debate`, `.LastTool`; history counts (Task 4) |
| `Chaos/Services/College/CollegeText.cs` | Quiz and debate texts (Tasks 2 and 4) |
| `Chaos/Extensions/ServiceCollectionExtensions.cs` | Register `ICollegeQuizStore` (Task 2) |
| `Chaos/Services/College/CollegeTools.cs` (new) | `LiveQuiz`, `LiveDebate` (Task 3) |
| `Chaos/Services/College/QuizSession.cs` (new) | Quiz rules (Task 3) |
| `Chaos/Services/College/DebateSession.cs` (new) | Debate rules (Task 3) |
| `Chaos/Services/College/CollegeService.Tools.cs` (new) | Start, steer, tick, views, floor block (Task 4) |
| `Chaos/Services/College/CollegePanel.cs` | Editor actions, class actions, tool messages, broadcast (Task 5) |
| `Chaos/Scripting/MapScripts/Temuair/College/CollegeRoomScript.cs` | 1-second tool step, state for arrivals (Task 5) |
| `Chaos/Services/Servers/WorldServer.cs` | Floor rule in the public-message handler (Task 5) |
| `Chaos/Scripting/DialogScripts/Temuair/College/CollegeLecternScript.cs` | Quiz and debate setup chains, panels, My quizzes (Task 6) |
| `Chaos/Scripting/DialogScripts/Temuair/College/CollegeRegistrarScript.cs` | My quizzes (Task 6) |
| `Tests/Chaos.Tests/Networking/CollegePacketConverterTests.cs`, `GuildEmblemPacketConverterTests.cs` | Task 1 |
| `Tests/Chaos.Tests/College/QuizRulesTests.cs`, `CollegeQuizStoreTests.cs`, `CollegeQuizServiceTests.cs` (new) | Task 2 |
| `Tests/Chaos.Tests/College/QuizSessionTests.cs`, `DebateSessionTests.cs` (new) | Task 3 |
| `Tests/Chaos.Tests/College/CollegeToolServiceTests.cs` (new) | Task 4 |
| `Tests/Chaos.Tests/College/CollegeToolMappingTests.cs` (new) | Task 5 |
| `Tests/Chaos.Tests/College/CollegeServiceTests.cs`, `CollegeEntryServiceTests.cs` | Constructor argument (Task 2) |

**Unora (`worktrees/college-tools-unora`)**

| File | Change |
|---|---|
| `Data/Configuration/Templates/Dialogs/Temauir/mileth/college/college_lectern_*.json` (11 new) | Quiz and debate chains, panel, My quizzes (Task 6) |
| `Data/Configuration/Templates/Dialogs/Temauir/mileth/college/college_registrar_quizzes.json` (new) | My quizzes (Task 6) |

**Client (`worktrees/college-tools-client`)**

| File | Change |
|---|---|
| `Chaos.Client/ViewModel/College/QuizDocument.cs` (new) | The editable quiz (Task 7) |
| `Chaos.Client/ViewModel/College/ToolCountdown.cs` (new) | Seconds left since a message arrived (Task 7) |
| `Chaos.Client/ViewModel/College/ClassToolText.cs` (new) | Card, panel and marker texts (Task 7) |
| `Chaos.Client/Controls/World/Popups/College/QuizEditorControl.cs` (new) | Two-page quiz editor (Task 8) |
| `Chaos.Client/Controls/World/Popups/College/QuizCardControl.cs` (new) | The student's quiz card (Task 9) |
| `Chaos.Client/Controls/World/Popups/College/QuizTeacherPanel.cs` (new) | The Teacher's quiz panel (Task 9) |
| `Chaos.Client/Controls/World/Popups/College/DebatePanel.cs` (new) | Student and Teacher debate panel (Task 10) |
| `Chaos.Client/Controls/World/Popups/College/DebateVoteCard.cs` (new) | Opening and final vote card (Task 10) |
| `Chaos.Client/Systems/College/DebateMarkTable.cs` (new) | Side markers by entity id (Task 10) |
| `Chaos.Client/Rendering/EntityOverlayManager.cs` | `DrawDebateMarks` (Task 10) |
| `Chaos.Client/Screens/WorldScreen.College.cs` | Build, route, place and close the tool windows (Task 11) |
| `Chaos.Client/Screens/WorldScreen.Update.cs`, `WorldScreen.Draw.cs`, `WorldScreen.Map.cs`, `WorldScreen.cs` | Placement, markers, map change, logout (Task 11) |
| `CLAUDE.md` | College control list (Task 11) |
| `Tests/Chaos.Client.Tests/College/QuizDocumentTests.cs`, `ToolCountdownTests.cs`, `ClassToolTextTests.cs` (new) | Task 7 |
| `Tests/Chaos.Client.Tests/College/DebateMarkTableTests.cs` (new) | Task 10 |

---

### Task 0: Create the worktrees

**Goal:** Three worktrees on branch `feat/college-tools`, building on the local mains.

**Files:**
- Create: `C:\Users\Michael\Documents\GitHub\worktrees\college-tools-server`, `...\college-tools-client`, `...\college-tools-unora`

**Acceptance Criteria:**
- [ ] `git -C <worktree> branch --show-current` prints `feat/college-tools` in all three
- [ ] The server worktree's HEAD is 3d12ce2db; the client's and Unora's are their `main` tips
- [ ] The server worktree builds

**Verify:** `dotnet build C:\Users\Michael\Documents\GitHub\worktrees\college-tools-server\Chaos.slnx` -> Build succeeded

**Steps:**

- [ ] **Step 1: Check the mains.**

```bash
git -C C:/Users/Michael/Documents/GitHub/Chaos.Client/Chaos-Server log --oneline -1 master
git -C C:/Users/Michael/Documents/GitHub/Chaos.Client log --oneline -1 main
git -C C:/Users/Michael/Documents/GitHub/Unora log --oneline -1 main
```

Expected: `3d12ce2db ...`, client main's tip (0ba233eb or later; docs-only commits on top are fine) and Unora main's tip. If server master moved past 3d12ce2db, stop and ask the user which commit to build on.

- [ ] **Step 2: Create the worktrees.**

```bash
git -C C:/Users/Michael/Documents/GitHub/Chaos.Client/Chaos-Server worktree add C:/Users/Michael/Documents/GitHub/worktrees/college-tools-server -b feat/college-tools master
git -C C:/Users/Michael/Documents/GitHub/Chaos.Client worktree add C:/Users/Michael/Documents/GitHub/worktrees/college-tools-client -b feat/college-tools main
git -C C:/Users/Michael/Documents/GitHub/Unora worktree add C:/Users/Michael/Documents/GitHub/worktrees/college-tools-unora -b feat/college-tools main
```

- [ ] **Step 3: Build the server worktree.** Run `dotnet build Chaos.slnx` in the server worktree. Expected: Build succeeded.

```json:metadata
{"files": [], "verifyCommand": "dotnet build C:\\Users\\Michael\\Documents\\GitHub\\worktrees\\college-tools-server\\Chaos.slnx", "acceptanceCriteria": ["three worktrees on feat/college-tools", "server at 3d12ce2db, client and Unora at main's tip", "server builds"], "modelTier": "mechanical"}
```

---

### Task 1: Protocol: quiz and debate messages, CLIENT_VERSION 775 (server repo)

**Goal:** Both College message converters carry the nine new actions and eight new displays. The shared enums and limits exist. `CLIENT_VERSION` is 775.

**Files:**
- Modify: `Chaos.DarkAges/Definitions/Enums.cs` (the `#region Mileth College` block)
- Modify: `Chaos.DarkAges/Definitions/CollegeProtocol.cs`
- Modify: `Chaos.DarkAges/Definitions/CONSTANTS.cs:23`
- Modify: `Chaos.Networking/Entities/Server/CollegeInfos.cs`
- Modify: `Chaos.Networking/Entities/Client/CollegeActionArgs.cs`
- Modify: `Chaos.Networking/Entities/Server/CollegeDisplayArgs.cs`
- Modify: `Chaos.Networking/Converters/CollegeCodec.cs`
- Modify: `Chaos.Networking/Converters/Client/CollegeActionConverter.cs`
- Modify: `Chaos.Networking/Converters/Server/CollegeDisplayConverter.cs`
- Test: `Tests/Chaos.Tests/Networking/CollegePacketConverterTests.cs`, `Tests/Chaos.Tests/Networking/GuildEmblemPacketConverterTests.cs`

**Acceptance Criteria:**
- [ ] Every action and display sub-type round-trips, including all nine new actions and eight new displays
- [ ] A quiz draft with 21 questions, a question with 5 answers, or `PickSpeakers` with 4 names throws `ArgumentOutOfRangeException` when read
- [ ] A question of 150 Korean characters round-trips (`String16`)
- [ ] `CLIENT_VERSION` is 775

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/CollegePacketConverterTests/*"` -> all pass; same for `GuildEmblemPacketConverterTests`

**Steps:**

- [ ] **Step 1: Write the failing tests.** In `CollegePacketConverterTests.cs`, add after `MusicPiece()`:

```csharp
    private static CollegeQuizDraftInfo QuizDraft()
        => new()
        {
            Title = "Mileth history",
            Questions =
            [
                new CollegeQuizQuestionInfo { Text = "Who founded the Mileth Temple of Glioca?", Answers = ["Danaan", "Sgrios", "The first Aislings"], Right = 2 },
                new CollegeQuizQuestionInfo { Text = "Is Mileth by the sea?", Answers = ["Yes", "No"], Right = CollegeProtocol.NO_ANSWER }
            ]
        };

    private static CollegeQuizInfo QuizView()
        => new()
        {
            Title = "Mileth history",
            Number = 3,
            Count = 8,
            Question = "Who founded the Mileth Temple of Glioca?",
            Answers = ["Danaan", "Sgrios", "The first Aislings"],
            Seconds = 20,
            SecondsLeft = 14,
            Phase = QuizPhase.Revealed,
            MyAnswer = 1,
            Right = 2,
            RightCount = 4,
            AnsweredCount = 9,
            MyScore = 2,
            Students = 9,
            Top = [new CollegeScoreInfo { Name = "Aroha", Score = 3 }, new CollegeScoreInfo { Name = "Kael", Score = 3 }]
        };

    private static CollegeDebateInfo DebateView()
        => new()
        {
            Motion = "Sgrios is needed for balance",
            Phase = DebatePhase.Floor,
            SecondsLeft = 0,
            TurnMinutes = 2,
            For = 2,
            Against = 2,
            Undecided = 1,
            OpeningFor = 1,
            OpeningAgainst = 2,
            OpeningUndecided = 2,
            Floor = "Kael",
            FloorSecondsLeft = 72,
            MySide = DebateSide.Against,
            MyHand = true,
            Result = DebateResult.None,
            Bonus = true,
            IsTeacher = true,
            Hands = ["Mira", "Aroha"],
            Speakers = ["Kael"]
        };
```

In `Actions()`, add at the end:

```csharp
        yield return new CollegeActionArgs { Type = CollegeActionType.QuizOpen, QuizId = new string('b', 32) };
        yield return new CollegeActionArgs { Type = CollegeActionType.QuizSave, QuizId = "", QuizDraft = QuizDraft() };
        yield return new CollegeActionArgs { Type = CollegeActionType.QuizDelete, QuizId = new string('c', 32) };
        yield return new CollegeActionArgs { Type = CollegeActionType.QuizCopy, QuizId = new string('d', 32) };
        yield return new CollegeActionArgs { Type = CollegeActionType.QuizAnswer, Number = 3, Answer = 1 };
        yield return new CollegeActionArgs { Type = CollegeActionType.QuizControl, QuizControl = QuizControlType.End };
        yield return new CollegeActionArgs { Type = CollegeActionType.DebateSide, Side = DebateSide.Undecided };
        yield return new CollegeActionArgs { Type = CollegeActionType.DebateHand, Raised = true };
        yield return new CollegeActionArgs { Type = CollegeActionType.DebateControl, DebateControl = DebateControlType.PickSpeakers, Names = ["Kael", "Mira"] };
```

In `Displays()`, add at the end:

```csharp
        yield return new CollegeDisplayArgs
        {
            Type = CollegeDisplayType.QuizList,
            QuizRows = [new CollegeQuizRowInfo { Id = new string('b', 32), Title = "Mileth history", Questions = 8 }]
        };
        yield return new CollegeDisplayArgs { Type = CollegeDisplayType.QuizEdit, QuizId = new string('b', 32), QuizDraft = QuizDraft() };
        yield return new CollegeDisplayArgs { Type = CollegeDisplayType.QuizResult, QuizResult = QuizResultCode.Saved, Message = "Quiz saved.", QuizId = new string('b', 32) };
        yield return new CollegeDisplayArgs { Type = CollegeDisplayType.QuizCard, QuizView = QuizView() };
        yield return new CollegeDisplayArgs { Type = CollegeDisplayType.QuizTeacher, QuizView = QuizView(), Reopen = true };
        yield return new CollegeDisplayArgs { Type = CollegeDisplayType.DebateState, Debate = DebateView(), Reopen = true };
        yield return new CollegeDisplayArgs
        {
            Type = CollegeDisplayType.DebateMarks,
            DebateMarks =
            [
                new CollegeDebateMarkInfo { EntityId = 4_000_001, Side = DebateSide.For, Floor = true },
                new CollegeDebateMarkInfo { EntityId = 4_000_002, Side = DebateSide.Undecided }
            ]
        };
        yield return new CollegeDisplayArgs { Type = CollegeDisplayType.ToolClosed, Tool = CollegeTool.Debate };
```

Add these tests at the end of the class:

```csharp
    [Test]
    public void A_quiz_with_21_questions_is_refused()
    {
        var draft = QuizDraft();
        draft.Questions = Enumerable.Range(0, CollegeProtocol.MAX_QUIZ_QUESTIONS + 1)
                                    .Select(_ => new CollegeQuizQuestionInfo { Text = "Q", Answers = ["A", "B"], Right = 0 })
                                    .ToList();

        var act = () => RoundTrip(new CollegeActionConverter(), new CollegeActionArgs { Type = CollegeActionType.QuizSave, QuizDraft = draft });

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void A_question_with_5_answers_is_refused()
    {
        var draft = QuizDraft();
        draft.Questions[0].Answers = ["A", "B", "C", "D", "E"];

        var act = () => RoundTrip(new CollegeActionConverter(), new CollegeActionArgs { Type = CollegeActionType.QuizSave, QuizDraft = draft });

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void Picking_4_speakers_is_refused()
    {
        var args = new CollegeActionArgs
        {
            Type = CollegeActionType.DebateControl,
            DebateControl = DebateControlType.PickSpeakers,
            Names = ["A", "B", "C", "D"]
        };

        var act = () => RoundTrip(new CollegeActionConverter(), args);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void A_long_korean_question_round_trips()
    {
        var draft = QuizDraft();
        draft.Questions[0].Text = new string('가', CollegeProtocol.MAX_QUESTION_CHARS);
        var original = new CollegeActionArgs { Type = CollegeActionType.QuizSave, QuizDraft = draft };

        RoundTrip(new CollegeActionConverter(), original).Should().BeEquivalentTo(original);
    }
```

In `GuildEmblemPacketConverterTests.cs`, rename `Client_version_is_774` to `Client_version_is_775` and change the expected value to 775.

- [ ] **Step 2: Run the tests to see them fail.** Run the Verify command. Expected: compile errors (`CollegeQuizDraftInfo`, `QuizOpen` and the other new names don't exist).

- [ ] **Step 3: Add the enums.** In `Chaos.DarkAges/Definitions/Enums.cs`, extend `CollegeActionType` after `TuneFetch = 13`:

```csharp
    TuneFetch = 13,
    QuizOpen = 14,
    QuizSave = 15,
    QuizDelete = 16,
    QuizCopy = 17,
    QuizAnswer = 18,
    QuizControl = 19,
    DebateSide = 20,
    DebateHand = 21,
    DebateControl = 22
```

Extend `CollegeDisplayType` after `Tune = 9`:

```csharp
    Tune = 9,
    QuizList = 10,
    QuizEdit = 11,
    QuizResult = 12,
    QuizCard = 13,
    QuizTeacher = 14,
    DebateState = 15,
    DebateMarks = 16,
    ToolClosed = 17
```

Add before the region's `#endregion`, after `TuneLayer`:

```csharp
/// <summary>Where a class quiz is: before its first question, a question open, its answer shown, or over.</summary>
public enum QuizPhase : byte
{
    Waiting = 0,
    Open = 1,
    Revealed = 2,
    Ended = 3
}

public enum DebatePhase : byte
{
    Opening = 0,
    Floor = 1,
    Final = 2,
    Result = 3,
    Ended = 4
}

/// <summary>A student's side in a debate. <see cref="None" /> means they have not picked one.</summary>
public enum DebateSide : byte
{
    None = 0,
    For = 1,
    Against = 2,
    Undecided = 3
}

public enum DebateResult : byte
{
    None = 0,
    For = 1,
    Against = 2,
    Draw = 3
}

public enum QuizControlType : byte
{
    Next = 0,
    End = 1
}

public enum DebateControlType : byte
{
    GiveFloor = 0,
    TakeBack = 1,
    FinalVote = 2,
    PickSpeakers = 3,
    End = 4
}

public enum CollegeTool : byte
{
    Quiz = 0,
    Debate = 1
}

public enum QuizResultCode : byte
{
    Saved = 0,
    Deleted = 1,
    Copied = 2,
    Refused = 3
}
```

- [ ] **Step 4: Add the limits.** In `CollegeProtocol.cs`, add after `MIN_HAND_IN_NOTES`:

```csharp

    public const int MAX_QUIZ_TITLE_CHARS = 40;
    public const int MIN_QUIZ_QUESTIONS = 5;
    public const int MAX_QUIZ_QUESTIONS = 20;
    public const int MAX_QUESTION_CHARS = 150;
    public const int MIN_ANSWERS = 2;
    public const int MAX_ANSWERS = 4;
    public const int MAX_ANSWER_CHARS = 40;
    public const int MAX_QUIZZES_PER_OWNER = 20;
    public const int MAX_MOTION_CHARS = 120;
    public const int DEBATE_OPENING_SECONDS = 60;
    public const int DEBATE_FINAL_SECONDS = 30;
    public const int MAX_BONUS_STUDENTS = 3;
    public const int QUIZ_GUARD_QUESTIONS = 5;
    public const int DEBATE_GUARD_SPEAKERS = 3;

    /// <summary>The answer byte for "none": no answer picked, or the right answer not shown.</summary>
    public const byte NO_ANSWER = byte.MaxValue;

    public static IReadOnlyList<int> QuizSeconds { get; } = [15, 20, 30, 45, 60];
    public static IReadOnlyList<int> DebateTurnMinutes { get; } = [1, 2, 3];
```

In `CONSTANTS.cs`, change `CLIENT_VERSION = 774` to `775`.

- [ ] **Step 5: Add the info records.** Append to `Chaos.Networking/Entities/Server/CollegeInfos.cs`:

```csharp

/// <summary>One quiz question as the editor sends it. <see cref="Right" /> is <see cref="CollegeProtocol.NO_ANSWER" /> until one is marked.</summary>
public sealed record CollegeQuizQuestionInfo
{
    public string Text { get; set; } = string.Empty;
    public List<string> Answers { get; set; } = [];
    public byte Right { get; set; } = CollegeProtocol.NO_ANSWER;
}

/// <summary>A saved quiz as the editor sends and receives it.</summary>
public sealed record CollegeQuizDraftInfo
{
    public string Title { get; set; } = string.Empty;
    public List<CollegeQuizQuestionInfo> Questions { get; set; } = [];
}

/// <summary>One row of the editor's quiz list.</summary>
public sealed record CollegeQuizRowInfo
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public byte Questions { get; set; }
}

public sealed record CollegeScoreInfo
{
    public string Name { get; set; } = string.Empty;
    public byte Score { get; set; }
}

/// <summary>
///     A class quiz as one viewer sees it. A student's card gets <see cref="Right" /> only once the question is revealed;
///     the Teacher's panel gets it while the question is open, with <see cref="AnsweredCount" /> counting live.
/// </summary>
public sealed record CollegeQuizInfo
{
    public string Title { get; set; } = string.Empty;
    public byte Number { get; set; }
    public byte Count { get; set; }
    public string Question { get; set; } = string.Empty;
    public List<string> Answers { get; set; } = [];
    public byte Seconds { get; set; }
    public byte SecondsLeft { get; set; }
    public QuizPhase Phase { get; set; }
    public byte MyAnswer { get; set; } = CollegeProtocol.NO_ANSWER;
    public byte Right { get; set; } = CollegeProtocol.NO_ANSWER;
    public byte RightCount { get; set; }
    public byte AnsweredCount { get; set; }
    public byte MyScore { get; set; }

    /// <summary>Students in the room, not counting the Teacher.</summary>
    public byte Students { get; set; }

    public List<CollegeScoreInfo> Top { get; set; } = [];
}

/// <summary>A class debate as one viewer sees it. Only the Teacher's copy lists the raised hands and the speakers.</summary>
public sealed record CollegeDebateInfo
{
    public string Motion { get; set; } = string.Empty;
    public DebatePhase Phase { get; set; }

    /// <summary>Opening or final vote: the seconds left in the vote.</summary>
    public byte SecondsLeft { get; set; }

    public byte TurnMinutes { get; set; }
    public byte For { get; set; }
    public byte Against { get; set; }
    public byte Undecided { get; set; }
    public byte OpeningFor { get; set; }
    public byte OpeningAgainst { get; set; }
    public byte OpeningUndecided { get; set; }

    /// <summary>Who holds the floor, or empty.</summary>
    public string Floor { get; set; } = string.Empty;

    public ushort FloorSecondsLeft { get; set; }
    public DebateSide MySide { get; set; }
    public bool MyHand { get; set; }
    public DebateResult Result { get; set; }
    public bool Bonus { get; set; }

    /// <summary>This copy is for the class's Teacher: the panel shows the Teacher's view.</summary>
    public bool IsTeacher { get; set; }

    public List<string> Hands { get; set; } = [];
    public List<string> Speakers { get; set; } = [];
}

/// <summary>The side marker over one player in the room.</summary>
public sealed record CollegeDebateMarkInfo
{
    public uint EntityId { get; set; }
    public DebateSide Side { get; set; }
    public bool Floor { get; set; }
}
```

- [ ] **Step 6: Add the args fields.** In `CollegeActionArgs.cs`, append inside the record after `Text`:

```csharp

    /// <summary>QuizOpen (empty asks for the list), QuizSave (empty for a new quiz), QuizDelete, QuizCopy.</summary>
    public string QuizId { get; set; } = string.Empty;

    /// <summary>QuizSave.</summary>
    public CollegeQuizDraftInfo? QuizDraft { get; set; }

    /// <summary>QuizAnswer: the question number (1-based) and the answer index.</summary>
    public byte Number { get; set; }

    public byte Answer { get; set; }
    public QuizControlType QuizControl { get; set; }
    public DebateSide Side { get; set; }

    /// <summary>DebateHand: true raises the hand, false lowers it.</summary>
    public bool Raised { get; set; }

    public DebateControlType DebateControl { get; set; }

    /// <summary>DebateControl: one name for GiveFloor, up to three for PickSpeakers.</summary>
    public List<string> Names { get; set; } = [];
```

In `CollegeDisplayArgs.cs`, append inside the record after `HandInRows`:

```csharp

    public List<CollegeQuizRowInfo> QuizRows { get; set; } = [];

    /// <summary>QuizEdit, QuizResult.</summary>
    public string QuizId { get; set; } = string.Empty;

    /// <summary>QuizEdit.</summary>
    public CollegeQuizDraftInfo? QuizDraft { get; set; }

    /// <summary>QuizResult; its text is <see cref="Message" />.</summary>
    public QuizResultCode QuizResult { get; set; }

    /// <summary>QuizCard, QuizTeacher.</summary>
    public CollegeQuizInfo QuizView { get; set; } = new();

    public CollegeDebateInfo Debate { get; set; } = new();
    public List<CollegeDebateMarkInfo> DebateMarks { get; set; } = [];

    /// <summary>ToolClosed.</summary>
    public CollegeTool Tool { get; set; }

    /// <summary>QuizCard, QuizTeacher, DebateState: show the window even if the player closed it (an arrival, or "Show panel" at the lectern).</summary>
    public bool Reopen { get; set; }
```

- [ ] **Step 7: Add the codec.** Append inside `CollegeCodec` (before the class's closing brace):

```csharp

    public static CollegeQuizDraftInfo ReadQuizDraft(ref SpanReader reader)
    {
        var quiz = new CollegeQuizDraftInfo { Title = reader.ReadString8() };
        var count = reader.ReadByte();

        if (count > CollegeProtocol.MAX_QUIZ_QUESTIONS)
            throw new ArgumentOutOfRangeException(nameof(reader), count, "Too many quiz questions");

        for (var i = 0; i < count; i++)
        {
            var question = new CollegeQuizQuestionInfo { Text = reader.ReadString16() };
            var answers = reader.ReadByte();

            if (answers > CollegeProtocol.MAX_ANSWERS)
                throw new ArgumentOutOfRangeException(nameof(reader), answers, "Too many quiz answers");

            for (var j = 0; j < answers; j++)
                question.Answers.Add(reader.ReadString8());

            question.Right = reader.ReadByte();
            quiz.Questions.Add(question);
        }

        return quiz;
    }

    public static void WriteQuizDraft(ref SpanWriter writer, CollegeQuizDraftInfo quiz)
    {
        writer.WriteString8(quiz.Title);
        var questions = quiz.Questions.Take(byte.MaxValue).ToList();
        writer.WriteByte((byte)questions.Count);

        foreach (var question in questions)
        {
            writer.WriteString16(question.Text);
            var answers = question.Answers.Take(byte.MaxValue).ToList();
            writer.WriteByte((byte)answers.Count);

            foreach (var answer in answers)
                writer.WriteString8(answer);

            writer.WriteByte(question.Right);
        }
    }

    public static List<CollegeQuizRowInfo> ReadQuizRows(ref SpanReader reader)
    {
        var count = reader.ReadByte();
        var rows = new List<CollegeQuizRowInfo>(count);

        for (var i = 0; i < count; i++)
            rows.Add(
                new CollegeQuizRowInfo
                {
                    Id = reader.ReadString8(),
                    Title = reader.ReadString8(),
                    Questions = reader.ReadByte()
                });

        return rows;
    }

    public static void WriteQuizRows(ref SpanWriter writer, List<CollegeQuizRowInfo> rows)
    {
        var shown = rows.Take(byte.MaxValue).ToList();
        writer.WriteByte((byte)shown.Count);

        foreach (var row in shown)
        {
            writer.WriteString8(row.Id);
            writer.WriteString8(row.Title);
            writer.WriteByte(row.Questions);
        }
    }

    public static CollegeQuizInfo ReadQuizView(ref SpanReader reader)
    {
        var view = new CollegeQuizInfo
        {
            Title = reader.ReadString8(),
            Number = reader.ReadByte(),
            Count = reader.ReadByte(),
            Question = reader.ReadString16(),
            Answers = ReadNames(ref reader, byte.MaxValue),
            Seconds = reader.ReadByte(),
            SecondsLeft = reader.ReadByte(),
            Phase = (QuizPhase)reader.ReadByte(),
            MyAnswer = reader.ReadByte(),
            Right = reader.ReadByte(),
            RightCount = reader.ReadByte(),
            AnsweredCount = reader.ReadByte(),
            MyScore = reader.ReadByte(),
            Students = reader.ReadByte()
        };

        var top = reader.ReadByte();

        for (var i = 0; i < top; i++)
            view.Top.Add(new CollegeScoreInfo { Name = reader.ReadString8(), Score = reader.ReadByte() });

        return view;
    }

    public static void WriteQuizView(ref SpanWriter writer, CollegeQuizInfo view)
    {
        writer.WriteString8(view.Title);
        writer.WriteByte(view.Number);
        writer.WriteByte(view.Count);
        writer.WriteString16(view.Question);
        WriteNames(ref writer, view.Answers);
        writer.WriteByte(view.Seconds);
        writer.WriteByte(view.SecondsLeft);
        writer.WriteByte((byte)view.Phase);
        writer.WriteByte(view.MyAnswer);
        writer.WriteByte(view.Right);
        writer.WriteByte(view.RightCount);
        writer.WriteByte(view.AnsweredCount);
        writer.WriteByte(view.MyScore);
        writer.WriteByte(view.Students);

        var top = view.Top.Take(byte.MaxValue).ToList();
        writer.WriteByte((byte)top.Count);

        foreach (var score in top)
        {
            writer.WriteString8(score.Name);
            writer.WriteByte(score.Score);
        }
    }

    public static CollegeDebateInfo ReadDebate(ref SpanReader reader)
        => new()
        {
            Motion = reader.ReadString16(),
            Phase = (DebatePhase)reader.ReadByte(),
            SecondsLeft = reader.ReadByte(),
            TurnMinutes = reader.ReadByte(),
            For = reader.ReadByte(),
            Against = reader.ReadByte(),
            Undecided = reader.ReadByte(),
            OpeningFor = reader.ReadByte(),
            OpeningAgainst = reader.ReadByte(),
            OpeningUndecided = reader.ReadByte(),
            Floor = reader.ReadString8(),
            FloorSecondsLeft = reader.ReadUInt16(),
            MySide = (DebateSide)reader.ReadByte(),
            MyHand = reader.ReadBoolean(),
            Result = (DebateResult)reader.ReadByte(),
            Bonus = reader.ReadBoolean(),
            IsTeacher = reader.ReadBoolean(),
            Hands = ReadNames(ref reader, byte.MaxValue),
            Speakers = ReadNames(ref reader, byte.MaxValue)
        };

    public static void WriteDebate(ref SpanWriter writer, CollegeDebateInfo debate)
    {
        writer.WriteString16(debate.Motion);
        writer.WriteByte((byte)debate.Phase);
        writer.WriteByte(debate.SecondsLeft);
        writer.WriteByte(debate.TurnMinutes);
        writer.WriteByte(debate.For);
        writer.WriteByte(debate.Against);
        writer.WriteByte(debate.Undecided);
        writer.WriteByte(debate.OpeningFor);
        writer.WriteByte(debate.OpeningAgainst);
        writer.WriteByte(debate.OpeningUndecided);
        writer.WriteString8(debate.Floor);
        writer.WriteUInt16(debate.FloorSecondsLeft);
        writer.WriteByte((byte)debate.MySide);
        writer.WriteBoolean(debate.MyHand);
        writer.WriteByte((byte)debate.Result);
        writer.WriteBoolean(debate.Bonus);
        writer.WriteBoolean(debate.IsTeacher);
        WriteNames(ref writer, debate.Hands);
        WriteNames(ref writer, debate.Speakers);
    }

    public static List<CollegeDebateMarkInfo> ReadDebateMarks(ref SpanReader reader)
    {
        var count = reader.ReadByte();
        var marks = new List<CollegeDebateMarkInfo>(count);

        for (var i = 0; i < count; i++)
            marks.Add(
                new CollegeDebateMarkInfo
                {
                    EntityId = reader.ReadUInt32(),
                    Side = (DebateSide)reader.ReadByte(),
                    Floor = reader.ReadBoolean()
                });

        return marks;
    }

    public static void WriteDebateMarks(ref SpanWriter writer, List<CollegeDebateMarkInfo> marks)
    {
        var shown = marks.Take(byte.MaxValue).ToList();
        writer.WriteByte((byte)shown.Count);

        foreach (var mark in shown)
        {
            writer.WriteUInt32(mark.EntityId);
            writer.WriteByte((byte)mark.Side);
            writer.WriteBoolean(mark.Floor);
        }
    }

    /// <summary>A byte count, then that many <c>String8</c>s. More than <paramref name="max" /> is refused.</summary>
    public static List<string> ReadNames(ref SpanReader reader, int max)
    {
        var count = reader.ReadByte();

        if (count > max)
            throw new ArgumentOutOfRangeException(nameof(reader), count, "Too many College names");

        var names = new List<string>(count);

        for (var i = 0; i < count; i++)
            names.Add(reader.ReadString8());

        return names;
    }

    public static void WriteNames(ref SpanWriter writer, List<string> names)
    {
        var shown = names.Take(byte.MaxValue).ToList();
        writer.WriteByte((byte)shown.Count);

        foreach (var name in shown)
            writer.WriteString8(name);
    }
```

`SpanReader.ReadUInt16`/`ReadUInt32` and `SpanWriter.WriteUInt16`/`WriteUInt32` exist in `Chaos.IO/Memory`; `ReadGalleryRows` already uses the 32-bit pair.

- [ ] **Step 8: Wire the action converter.** In `CollegeActionConverter.Deserialize`, add before the switch's closing brace:

```csharp
            case CollegeActionType.QuizOpen:
            case CollegeActionType.QuizDelete:
            case CollegeActionType.QuizCopy:
                args.QuizId = reader.ReadString8();

                break;
            case CollegeActionType.QuizSave:
                args.QuizId = reader.ReadString8();
                args.QuizDraft = CollegeCodec.ReadQuizDraft(ref reader);

                break;
            case CollegeActionType.QuizAnswer:
                args.Number = reader.ReadByte();
                args.Answer = reader.ReadByte();

                break;
            case CollegeActionType.QuizControl:
                args.QuizControl = (QuizControlType)reader.ReadByte();

                break;
            case CollegeActionType.DebateSide:
                args.Side = (DebateSide)reader.ReadByte();

                break;
            case CollegeActionType.DebateHand:
                args.Raised = reader.ReadBoolean();

                break;
            case CollegeActionType.DebateControl:
                args.DebateControl = (DebateControlType)reader.ReadByte();
                args.Names = CollegeCodec.ReadNames(ref reader, CollegeProtocol.MAX_BONUS_STUDENTS);

                break;
```

In `Serialize`, add before the switch's closing brace:

```csharp
            case CollegeActionType.QuizOpen:
            case CollegeActionType.QuizDelete:
            case CollegeActionType.QuizCopy:
                writer.WriteString8(args.QuizId);

                break;
            case CollegeActionType.QuizSave:
                writer.WriteString8(args.QuizId);
                CollegeCodec.WriteQuizDraft(ref writer, args.QuizDraft!);

                break;
            case CollegeActionType.QuizAnswer:
                writer.WriteByte(args.Number);
                writer.WriteByte(args.Answer);

                break;
            case CollegeActionType.QuizControl:
                writer.WriteByte((byte)args.QuizControl);

                break;
            case CollegeActionType.DebateSide:
                writer.WriteByte((byte)args.Side);

                break;
            case CollegeActionType.DebateHand:
                writer.WriteBoolean(args.Raised);

                break;
            case CollegeActionType.DebateControl:
                writer.WriteByte((byte)args.DebateControl);
                CollegeCodec.WriteNames(ref writer, args.Names);

                break;
```

- [ ] **Step 9: Wire the display converter.** In `CollegeDisplayConverter.Deserialize`, add before the switch's closing brace:

```csharp
            case CollegeDisplayType.QuizList:
                args.QuizRows = CollegeCodec.ReadQuizRows(ref reader);

                break;
            case CollegeDisplayType.QuizEdit:
                args.QuizId = reader.ReadString8();
                args.QuizDraft = CollegeCodec.ReadQuizDraft(ref reader);

                break;
            case CollegeDisplayType.QuizResult:
                args.QuizResult = (QuizResultCode)reader.ReadByte();
                args.Message = reader.ReadString8();
                args.QuizId = reader.ReadString8();

                break;
            case CollegeDisplayType.QuizCard:
            case CollegeDisplayType.QuizTeacher:
                args.QuizView = CollegeCodec.ReadQuizView(ref reader);
                args.Reopen = reader.ReadBoolean();

                break;
            case CollegeDisplayType.DebateState:
                args.Debate = CollegeCodec.ReadDebate(ref reader);
                args.Reopen = reader.ReadBoolean();

                break;
            case CollegeDisplayType.DebateMarks:
                args.DebateMarks = CollegeCodec.ReadDebateMarks(ref reader);

                break;
            case CollegeDisplayType.ToolClosed:
                args.Tool = (CollegeTool)reader.ReadByte();

                break;
```

In `Serialize`, add before the switch's closing brace:

```csharp
            case CollegeDisplayType.QuizList:
                CollegeCodec.WriteQuizRows(ref writer, args.QuizRows);

                break;
            case CollegeDisplayType.QuizEdit:
                writer.WriteString8(args.QuizId);
                CollegeCodec.WriteQuizDraft(ref writer, args.QuizDraft!);

                break;
            case CollegeDisplayType.QuizResult:
                writer.WriteByte((byte)args.QuizResult);
                writer.WriteString8(args.Message);
                writer.WriteString8(args.QuizId);

                break;
            case CollegeDisplayType.QuizCard:
            case CollegeDisplayType.QuizTeacher:
                CollegeCodec.WriteQuizView(ref writer, args.QuizView);
                writer.WriteBoolean(args.Reopen);

                break;
            case CollegeDisplayType.DebateState:
                CollegeCodec.WriteDebate(ref writer, args.Debate);
                writer.WriteBoolean(args.Reopen);

                break;
            case CollegeDisplayType.DebateMarks:
                CollegeCodec.WriteDebateMarks(ref writer, args.DebateMarks);

                break;
            case CollegeDisplayType.ToolClosed:
                writer.WriteByte((byte)args.Tool);

                break;
```

- [ ] **Step 10: Run the tests to see them pass.** Run the Verify commands. Expected: all `CollegePacketConverterTests` and `GuildEmblemPacketConverterTests` pass. Then run `dotnet build Chaos.slnx`. Expected: Build succeeded.

```json:metadata
{"files": ["Chaos.DarkAges/Definitions/Enums.cs", "Chaos.DarkAges/Definitions/CollegeProtocol.cs", "Chaos.DarkAges/Definitions/CONSTANTS.cs", "Chaos.Networking/Entities/Server/CollegeInfos.cs", "Chaos.Networking/Entities/Client/CollegeActionArgs.cs", "Chaos.Networking/Entities/Server/CollegeDisplayArgs.cs", "Chaos.Networking/Converters/CollegeCodec.cs", "Chaos.Networking/Converters/Client/CollegeActionConverter.cs", "Chaos.Networking/Converters/Server/CollegeDisplayConverter.cs", "Tests/Chaos.Tests/Networking/CollegePacketConverterTests.cs", "Tests/Chaos.Tests/Networking/GuildEmblemPacketConverterTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/CollegePacketConverterTests/*\"", "acceptanceCriteria": ["every action and display sub-type round-trips, including the 9 new actions and 8 new displays", "21 questions, 5 answers or 4 speaker names throw ArgumentOutOfRangeException", "a 150-character Korean question round-trips", "CLIENT_VERSION is 775"], "modelTier": "mechanical"}
```

---
### Task 2: Saved quizzes on the server: model, rules, store, service (server repo)

**Goal:** Anyone who may teach can keep up to 20 quizzes. Each quiz is a file in `Works/Quizzes`, with an index in `CollegeState`. `QuizRules` checks a quiz for saving and for starting in class.

**Files:**
- Create: `Chaos/Services/College/CollegeQuiz.cs`
- Create: `Chaos/Services/College/QuizRules.cs`
- Create: `Chaos/Services/College/CollegeQuizStore.cs`
- Create: `Chaos/Services/College/CollegeService.Quizzes.cs`
- Modify: `Chaos/Services/College/CollegeService.cs` (constructor)
- Modify: `Chaos/Services/College/CollegeState.cs` (`Quizzes`)
- Modify: `Chaos/Services/College/CollegeText.cs`
- Modify: `Chaos/Extensions/ServiceCollectionExtensions.cs` (`AddCollege`)
- Modify: `Tests/Chaos.Tests/College/CollegeServiceTests.cs`, `Tests/Chaos.Tests/College/CollegeEntryServiceTests.cs` (constructor argument)
- Test: new `Tests/Chaos.Tests/College/MemoryQuizStore.cs`, `QuizRulesTests.cs`, `CollegeQuizStoreTests.cs`, `CollegeQuizServiceTests.cs`

**Acceptance Criteria:**
- [ ] `CheckSave` refuses a 41-character title, 21 questions, a 151-character question, 5 answers, a 41-character answer and a right answer outside the list; it accepts an unfinished quiz (no title, one question, no right answer)
- [ ] `CheckReady` also needs a title, 5-20 questions, question text, 2-4 non-empty answers and a right answer; it names the question number
- [ ] `Normalize` turns line breaks into spaces and trims
- [ ] The store round-trips a quiz, refuses a bad id, and deletes
- [ ] The service saves a new quiz with a 32-character id and an index row; saves again in place; refuses a 21st quiz, someone else's quiz and a player who can't teach; copies as "Copy of {title}"; deletes the file and the index; leaves the index unchanged when the disk write fails
- [ ] The existing College test files still compile and pass

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/QuizRulesTests/*"` -> all pass; same for `CollegeQuizStoreTests`, `CollegeQuizServiceTests`, `CollegeServiceTests`, `CollegeEntryServiceTests`

**Steps:**

- [ ] **Step 1: Write the test helpers.** Create `Tests/Chaos.Tests/College/MemoryQuizStore.cs`:

```csharp
using Chaos.Services.College;

namespace Chaos.Tests.College;

internal sealed class MemoryQuizStore : ICollegeQuizStore
{
    public Dictionary<string, CollegeQuiz> Files { get; } = [];
    public bool FailSaves { get; set; }

    public CollegeQuiz? Load(string id) => Files.TryGetValue(id, out var quiz) ? Copy(quiz) : null;

    public void Save(CollegeQuiz quiz)
    {
        if (FailSaves)
            throw new IOException("disk full");

        Files[quiz.Id] = Copy(quiz);
    }

    public void Delete(string id) => Files.Remove(id);

    private static CollegeQuiz Copy(CollegeQuiz quiz)
        => new()
        {
            Id = quiz.Id,
            Title = quiz.Title,
            UpdatedUtc = quiz.UpdatedUtc,
            Questions = quiz.Questions
                            .Select(q => new QuizQuestion { Text = q.Text, Answers = q.Answers.ToList(), Right = q.Right })
                            .ToList()
        };
}

internal static class QuizSamples
{
    /// <summary>A quiz that passes <c>CheckReady</c>: every question's right answer is the first.</summary>
    public static CollegeQuiz Ready(int questions = 5, string title = "Mileth history")
        => new()
        {
            Title = title,
            Questions = Enumerable.Range(1, questions)
                                  .Select(i => new QuizQuestion { Text = $"Question {i}?", Answers = ["Right", "Wrong", "Also wrong"], Right = 0 })
                                  .ToList()
        };
}
```

- [ ] **Step 2: Write the failing rule tests.** Create `Tests/Chaos.Tests/College/QuizRulesTests.cs`:

```csharp
using Chaos.Services.College;
using FluentAssertions;

namespace Chaos.Tests.College;

public sealed class QuizRulesTests
{
    private static CollegeQuiz Unfinished() => new() { Questions = [new QuizQuestion { Text = "Who?", Answers = ["A"] }] };

    [Test]
    public void An_unfinished_quiz_can_be_saved()
        => QuizRules.CheckSave(Unfinished()).IsOk.Should().BeTrue();

    [Test]
    public void A_40_character_title_saves_and_41_does_not()
    {
        var quiz = Unfinished();
        quiz.Title = new string('t', 40);
        QuizRules.CheckSave(quiz).IsOk.Should().BeTrue();

        quiz.Title = new string('t', 41);
        QuizRules.CheckSave(quiz).Check.Should().Be(QuizCheck.TitleTooLong);
    }

    [Test]
    public void Twenty_questions_save_and_21_do_not()
    {
        QuizRules.CheckSave(QuizSamples.Ready(20)).IsOk.Should().BeTrue();
        QuizRules.CheckSave(QuizSamples.Ready(21)).Check.Should().Be(QuizCheck.TooManyQuestions);
    }

    [Test]
    public void A_question_of_151_characters_is_refused_with_its_number()
    {
        var quiz = QuizSamples.Ready();
        quiz.Questions[2].Text = new string('q', 150);
        QuizRules.CheckSave(quiz).IsOk.Should().BeTrue();

        quiz.Questions[2].Text = new string('q', 151);
        QuizRules.CheckSave(quiz).Should().Be(new QuizProblem(QuizCheck.QuestionTooLong, 3));
    }

    [Test]
    public void Five_answers_or_a_41_character_answer_are_refused()
    {
        var quiz = QuizSamples.Ready();
        quiz.Questions[0].Answers = ["A", "B", "C", "D", "E"];
        QuizRules.CheckSave(quiz).Should().Be(new QuizProblem(QuizCheck.TooManyAnswers, 1));

        quiz = QuizSamples.Ready();
        quiz.Questions[1].Answers[1] = new string('a', 41);
        QuizRules.CheckSave(quiz).Should().Be(new QuizProblem(QuizCheck.AnswerTooLong, 2));
    }

    [Test]
    public void A_right_answer_outside_the_list_is_refused()
    {
        var quiz = QuizSamples.Ready();
        quiz.Questions[4].Right = 3;

        QuizRules.CheckSave(quiz).Should().Be(new QuizProblem(QuizCheck.BadRightAnswer, 5));
    }

    [Test]
    public void A_ready_quiz_passes_both_checks()
    {
        QuizRules.CheckReady(QuizSamples.Ready(5)).IsOk.Should().BeTrue();
        QuizRules.CheckReady(QuizSamples.Ready(20)).IsOk.Should().BeTrue();
    }

    [Test]
    public void Starting_needs_a_title_and_5_questions()
    {
        var quiz = QuizSamples.Ready();
        quiz.Title = "  ";
        QuizRules.CheckReady(quiz).Check.Should().Be(QuizCheck.NoTitle);

        QuizRules.CheckReady(QuizSamples.Ready(4)).Check.Should().Be(QuizCheck.TooFewQuestions);
    }

    [Test]
    public void Starting_needs_text_two_filled_answers_and_a_right_answer()
    {
        var quiz = QuizSamples.Ready();
        quiz.Questions[0].Text = "";
        QuizRules.CheckReady(quiz).Should().Be(new QuizProblem(QuizCheck.EmptyQuestion, 1));

        quiz = QuizSamples.Ready();
        quiz.Questions[1].Answers = ["Only"];
        QuizRules.CheckReady(quiz).Should().Be(new QuizProblem(QuizCheck.TooFewAnswers, 2));

        quiz = QuizSamples.Ready();
        quiz.Questions[2].Answers[1] = " ";
        QuizRules.CheckReady(quiz).Should().Be(new QuizProblem(QuizCheck.EmptyAnswer, 3));

        quiz = QuizSamples.Ready();
        quiz.Questions[3].Right = -1;
        QuizRules.CheckReady(quiz).Should().Be(new QuizProblem(QuizCheck.NoRightAnswer, 4));
    }

    [Test]
    public void Normalize_turns_line_breaks_into_spaces_and_trims()
    {
        var quiz = QuizSamples.Ready();
        quiz.Title = " Mileth\nhistory ";
        quiz.Questions[0].Text = "Who\r\nfounded it?";
        quiz.Questions[0].Answers[0] = " Danaan\n";

        var clean = QuizRules.Normalize(quiz);

        clean.Title.Should().Be("Mileth history");
        clean.Questions[0].Text.Should().Be("Who founded it?");
        clean.Questions[0].Answers[0].Should().Be("Danaan");
        clean.Questions[0].Right.Should().Be(0);
    }

    [Test]
    public void Problem_texts_fit_the_orange_bar()
    {
        foreach (var check in Enum.GetValues<QuizCheck>().Where(c => c != QuizCheck.Ok))
        {
            var text = CollegeText.QuizProblemText(new QuizProblem(check, 20));

            text.Should().NotBeEmpty(check.ToString());
            text.Length.Should().BeLessThanOrEqualTo(45, text);
        }
    }
}
```

- [ ] **Step 3: Write the failing store tests.** Create `Tests/Chaos.Tests/College/CollegeQuizStoreTests.cs`:

```csharp
using Chaos.Services.College;
using FluentAssertions;

namespace Chaos.Tests.College;

public sealed class CollegeQuizStoreTests
{
    private static CollegeQuizStore Create(out string dir)
    {
        dir = Path.Combine(Path.GetTempPath(), "college-quizzes-" + Guid.NewGuid().ToString("N"));

        return new CollegeQuizStore(dir);
    }

    [Test]
    public void A_quiz_round_trips()
    {
        var store = Create(out var dir);
        var quiz = QuizSamples.Ready();
        quiz.Id = CollegePiece.NewId();
        quiz.UpdatedUtc = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

        store.Save(quiz);

        store.Load(quiz.Id).Should().BeEquivalentTo(quiz);
        File.Exists(Path.Combine(dir, quiz.Id + ".json")).Should().BeTrue();
    }

    [Test]
    public void A_bad_id_reads_as_missing_and_cannot_be_saved()
    {
        var store = Create(out _);
        var quiz = QuizSamples.Ready();
        quiz.Id = "../escape";

        store.Load("../escape").Should().BeNull();

        var act = () => store.Save(quiz);

        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Delete_removes_the_file()
    {
        var store = Create(out _);
        var quiz = QuizSamples.Ready();
        quiz.Id = CollegePiece.NewId();
        store.Save(quiz);

        store.Delete(quiz.Id);

        store.Load(quiz.Id).Should().BeNull();
    }
}
```

- [ ] **Step 4: Write the failing service tests.** Create `Tests/Chaos.Tests/College/CollegeQuizServiceTests.cs`:

```csharp
#region
using Chaos.DarkAges.Definitions;
using Chaos.Services.College;
using Chaos.Storage.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using static Chaos.Tests.Towns.TownTestSupport;
#endregion

namespace Chaos.Tests.College;

public sealed class CollegeQuizServiceTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    private static CollegeService Create(out CollegeState state, out MemoryQuizStore store)
    {
        state = new CollegeState();
        store = new MemoryQuizStore();
        var storage = new Mock<IStorage<CollegeState>>();
        storage.SetupGet(s => s.Value).Returns(state);
        state.SetAward("Aroha", CollegeSubject.Lore, AwardTier.Village);
        state.SetAward("Kael", CollegeSubject.History, AwardTier.Kingdom);

        return new CollegeService(
            storage.Object,
            Options.Create(new CollegeOptions()),
            new Mock<ICollegeNotifier>().Object,
            new Mock<ICollegePieceStore>().Object,
            new Mock<ICollegePictureStore>().Object,
            store,
            new Mock<ICollegeMail>().Object,
            new FixedTime(Noon),
            NullLogger<CollegeService>.Instance);
    }

    [Test]
    public void A_teacher_saves_a_new_quiz_and_gets_its_id()
    {
        var service = Create(out var state, out var store);

        var outcome = service.SaveQuiz("Aroha", false, false, "", QuizSamples.Ready());

        outcome.Result.Should().Be(QuizResultCode.Saved);
        outcome.Message.Should().Be(CollegeText.QUIZ_SAVED);
        outcome.Id.Should().HaveLength(32);
        store.Files.Should().ContainKey(outcome.Id);
        state.Quizzes.Should().ContainSingle()
             .Which.Should().BeEquivalentTo(new { Id = outcome.Id, Owner = "Aroha", Title = "Mileth history", QuestionCount = 5 });
    }

    [Test]
    public void Saving_again_updates_the_same_quiz()
    {
        var service = Create(out var state, out var store);
        var id = service.SaveQuiz("Aroha", false, false, "", QuizSamples.Ready()).Id;

        var outcome = service.SaveQuiz("Aroha", false, false, id, QuizSamples.Ready(7, "Gods of Temuair"));

        outcome.Result.Should().Be(QuizResultCode.Saved);
        outcome.Id.Should().Be(id);
        state.Quizzes.Should().ContainSingle().Which.Title.Should().Be("Gods of Temuair");
        store.Files[id].Questions.Should().HaveCount(7);
    }

    [Test]
    public void A_player_who_cannot_teach_cannot_keep_quizzes()
    {
        var service = Create(out var state, out var store);

        var outcome = service.SaveQuiz("Tamsin", false, false, "", QuizSamples.Ready());

        outcome.Result.Should().Be(QuizResultCode.Refused);
        outcome.Message.Should().Be(CollegeText.QUIZ_NOT_ALLOWED);
        state.Quizzes.Should().BeEmpty();
        store.Files.Should().BeEmpty();
        service.MayKeepQuizzes("Tamsin", false, false).Should().BeFalse();
        service.MayKeepQuizzes("Tamsin", true, false).Should().BeTrue();
    }

    [Test]
    public void The_21st_quiz_is_refused()
    {
        var service = Create(out var state, out _);

        for (var i = 0; i < 20; i++)
            service.SaveQuiz("Aroha", false, false, "", QuizSamples.Ready()).Result.Should().Be(QuizResultCode.Saved);

        var outcome = service.SaveQuiz("Aroha", false, false, "", QuizSamples.Ready());

        outcome.Result.Should().Be(QuizResultCode.Refused);
        outcome.Message.Should().Be(CollegeText.QUIZ_LIMIT);
        state.Quizzes.Should().HaveCount(20);
    }

    [Test]
    public void Someone_else_cannot_load_save_copy_or_delete_a_quiz()
    {
        var service = Create(out var state, out _);
        var id = service.SaveQuiz("Aroha", false, false, "", QuizSamples.Ready()).Id;

        service.LoadQuiz("Kael", false, false, id).Should().BeNull();
        service.SaveQuiz("Kael", false, false, id, QuizSamples.Ready()).Message.Should().Be(CollegeText.QUIZ_NOT_YOURS);
        service.CopyQuiz("Kael", false, false, id).Result.Should().Be(QuizResultCode.Refused);
        service.DeleteQuiz("Kael", false, false, id).Result.Should().Be(QuizResultCode.Refused);

        state.Quizzes.Should().ContainSingle().Which.Owner.Should().Be("Aroha");
        service.LoadQuiz("aroha", false, false, id).Should().NotBeNull();
    }

    [Test]
    public void A_too_long_question_is_refused_but_an_unfinished_quiz_saves()
    {
        var service = Create(out _, out _);
        var quiz = QuizSamples.Ready();
        quiz.Questions[0].Text = new string('q', 151);

        var refused = service.SaveQuiz("Aroha", false, false, "", quiz);

        refused.Result.Should().Be(QuizResultCode.Refused);
        refused.Message.Should().Be("Question 1 is over 150 characters.");

        var unfinished = new CollegeQuiz { Questions = [new QuizQuestion { Text = "Who?", Answers = ["A"] }] };
        service.SaveQuiz("Aroha", false, false, "", unfinished).Result.Should().Be(QuizResultCode.Saved);
    }

    [Test]
    public void Copy_makes_a_new_quiz_named_copy_of()
    {
        var service = Create(out var state, out var store);
        var id = service.SaveQuiz("Aroha", false, false, "", QuizSamples.Ready(5, "A title that is exactly forty characters")).Id;

        var outcome = service.CopyQuiz("Aroha", false, false, id);

        outcome.Result.Should().Be(QuizResultCode.Copied);
        outcome.Message.Should().Be(CollegeText.QUIZ_COPIED);
        outcome.Id.Should().NotBe(id);
        store.Files[outcome.Id].Title.Should().Be("Copy of A title that is exactly forty ch");
        store.Files[outcome.Id].Questions.Should().HaveCount(5);
        state.Quizzes.Should().HaveCount(2);
    }

    [Test]
    public void Delete_removes_the_file_and_the_index()
    {
        var service = Create(out var state, out var store);
        var id = service.SaveQuiz("Aroha", false, false, "", QuizSamples.Ready()).Id;

        var outcome = service.DeleteQuiz("Aroha", false, false, id);

        outcome.Result.Should().Be(QuizResultCode.Deleted);
        outcome.Message.Should().Be(CollegeText.QUIZ_DELETED);
        state.Quizzes.Should().BeEmpty();
        store.Files.Should().BeEmpty();
    }

    [Test]
    public void A_failed_disk_write_leaves_the_index_unchanged()
    {
        var service = Create(out var state, out var store);
        store.FailSaves = true;

        var outcome = service.SaveQuiz("Aroha", false, false, "", QuizSamples.Ready());

        outcome.Result.Should().Be(QuizResultCode.Refused);
        outcome.Message.Should().Be(CollegeText.QUIZ_SAVE_FAILED);
        state.Quizzes.Should().BeEmpty();
    }

    [Test]
    public void Quizzes_of_lists_only_the_owners_quizzes_by_title()
    {
        var service = Create(out _, out _);
        service.SaveQuiz("Aroha", false, false, "", QuizSamples.Ready(5, "Zebra lore"));
        service.SaveQuiz("Kael", false, false, "", QuizSamples.Ready(5, "Kael's"));
        service.SaveQuiz("Aroha", false, false, "", QuizSamples.Ready(5, "Apple lore"));

        service.QuizzesOf("AROHA").Select(q => q.Title).Should().Equal("Apple lore", "Zebra lore");
    }
}
```

- [ ] **Step 5: Run the tests to see them fail.** Run the Verify commands. Expected: compile errors (`CollegeQuiz`, `QuizRules`, `ICollegeQuizStore` don't exist).

- [ ] **Step 6: Add the model.** Create `Chaos/Services/College/CollegeQuiz.cs`:

```csharp
namespace Chaos.Services.College;

/// <summary>A Teacher's saved quiz, one file per quiz in <c>WorksDirectory/Quizzes</c>.</summary>
public sealed class CollegeQuiz
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public DateTime UpdatedUtc { get; set; }
    public List<QuizQuestion> Questions { get; set; } = [];
}

public sealed class QuizQuestion
{
    public string Text { get; set; } = "";
    public List<string> Answers { get; set; } = [];

    /// <summary>The index of the right answer, or -1 while none is marked.</summary>
    public int Right { get; set; } = -1;
}

/// <summary>One saved quiz in <see cref="CollegeState.Quizzes" />, so lists never read the quiz files.</summary>
public sealed class CollegeQuizIndex
{
    public string Id { get; set; } = "";
    public string Owner { get; set; } = "";
    public string Title { get; set; } = "";
    public int QuestionCount { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
```

In `CollegeState.cs`, add after `HandIns`:

```csharp

    /// <summary>Every saved quiz; the questions live in the quiz files.</summary>
    public List<CollegeQuizIndex> Quizzes { get; set; } = [];
```

- [ ] **Step 7: Add the rules.** Create `Chaos/Services/College/QuizRules.cs`:

```csharp
using Chaos.DarkAges.Definitions;

namespace Chaos.Services.College;

public enum QuizCheck
{
    Ok,
    TitleTooLong,
    TooManyQuestions,
    QuestionTooLong,
    TooManyAnswers,
    AnswerTooLong,
    BadRightAnswer,
    NoTitle,
    TooFewQuestions,
    EmptyQuestion,
    TooFewAnswers,
    EmptyAnswer,
    NoRightAnswer
}

/// <summary>What is wrong with a quiz. <see cref="Question" /> is the 1-based question number, or 0 for the whole quiz.</summary>
public readonly record struct QuizProblem(QuizCheck Check, int Question = 0)
{
    public static QuizProblem Ok => new(QuizCheck.Ok);
    public bool IsOk => Check == QuizCheck.Ok;
}

/// <summary>Quiz checks. <see cref="CheckSave" /> allows an unfinished quiz; <see cref="CheckReady" /> is for starting it in class.</summary>
public static class QuizRules
{
    public static CollegeQuiz Normalize(CollegeQuiz quiz)
        => new()
        {
            Id = quiz.Id,
            UpdatedUtc = quiz.UpdatedUtc,
            Title = OneLine(quiz.Title),
            Questions = quiz.Questions
                            .Select(q => new QuizQuestion { Text = OneLine(q.Text), Answers = q.Answers.Select(OneLine).ToList(), Right = q.Right })
                            .ToList()
        };

    private static string OneLine(string text) => text.ReplaceLineEndings(" ").Trim();

    public static QuizProblem CheckSave(CollegeQuiz quiz)
    {
        if (quiz.Title.Length > CollegeProtocol.MAX_QUIZ_TITLE_CHARS)
            return new QuizProblem(QuizCheck.TitleTooLong);

        if (quiz.Questions.Count > CollegeProtocol.MAX_QUIZ_QUESTIONS)
            return new QuizProblem(QuizCheck.TooManyQuestions);

        for (var i = 0; i < quiz.Questions.Count; i++)
        {
            var question = quiz.Questions[i];
            var number = i + 1;

            if (question.Text.Length > CollegeProtocol.MAX_QUESTION_CHARS)
                return new QuizProblem(QuizCheck.QuestionTooLong, number);

            if (question.Answers.Count > CollegeProtocol.MAX_ANSWERS)
                return new QuizProblem(QuizCheck.TooManyAnswers, number);

            if (question.Answers.Any(a => a.Length > CollegeProtocol.MAX_ANSWER_CHARS))
                return new QuizProblem(QuizCheck.AnswerTooLong, number);

            if ((question.Right < -1) || (question.Right >= question.Answers.Count))
                return new QuizProblem(QuizCheck.BadRightAnswer, number);
        }

        return QuizProblem.Ok;
    }

    public static QuizProblem CheckReady(CollegeQuiz quiz)
    {
        var saved = CheckSave(quiz);

        if (!saved.IsOk)
            return saved;

        if (string.IsNullOrWhiteSpace(quiz.Title))
            return new QuizProblem(QuizCheck.NoTitle);

        if (quiz.Questions.Count < CollegeProtocol.MIN_QUIZ_QUESTIONS)
            return new QuizProblem(QuizCheck.TooFewQuestions);

        for (var i = 0; i < quiz.Questions.Count; i++)
        {
            var question = quiz.Questions[i];
            var number = i + 1;

            if (string.IsNullOrWhiteSpace(question.Text))
                return new QuizProblem(QuizCheck.EmptyQuestion, number);

            if (question.Answers.Count < CollegeProtocol.MIN_ANSWERS)
                return new QuizProblem(QuizCheck.TooFewAnswers, number);

            if (question.Answers.Any(string.IsNullOrWhiteSpace))
                return new QuizProblem(QuizCheck.EmptyAnswer, number);

            if (question.Right < 0)
                return new QuizProblem(QuizCheck.NoRightAnswer, number);
        }

        return QuizProblem.Ok;
    }
}
```

- [ ] **Step 8: Add the store.** Create `Chaos/Services/College/CollegeQuizStore.cs`:

```csharp
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Chaos.Services.College;

public interface ICollegeQuizStore
{
    CollegeQuiz? Load(string id);
    void Save(CollegeQuiz quiz);
    void Delete(string id);
}

/// <summary>One JSON file per quiz in <c>WorksDirectory/Quizzes</c>, written through a temp file like the pieces.</summary>
public sealed class CollegeQuizStore : ICollegeQuizStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string Directory;
    private readonly Lock Sync = new();

    public CollegeQuizStore(IOptions<CollegeOptions> options)
        : this(Path.Combine(options.Value.WorksDirectory, "Quizzes")) { }

    public CollegeQuizStore(string directory)
    {
        Directory = directory;
        System.IO.Directory.CreateDirectory(directory);
    }

    private string PathOf(string id) => Path.Combine(Directory, id + ".json");

    public CollegeQuiz? Load(string id)
    {
        if (!CollegePieceStore.IsId(id))
            return null;

        using var scope = Sync.EnterScope();

        try
        {
            return File.Exists(PathOf(id)) ? JsonSerializer.Deserialize<CollegeQuiz>(File.ReadAllText(PathOf(id)), Json) : null;
        } catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Save(CollegeQuiz quiz)
    {
        if (!CollegePieceStore.IsId(quiz.Id))
            throw new ArgumentException($"Bad quiz id '{quiz.Id}'", nameof(quiz));

        using var scope = Sync.EnterScope();

        var path = PathOf(quiz.Id);
        var temp = path + ".tmp";

        try
        {
            //flushed to disk so a power cut can't leave the renamed file empty
            using (var stream = File.Create(temp))
            {
                stream.Write(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(quiz, Json)));
                stream.Flush(true);
            }

            File.Move(temp, path, true);
        } catch
        {
            try
            {
                File.Delete(temp);
            } catch (IOException)
            {
            } catch (UnauthorizedAccessException)
            {
            }

            throw;
        }
    }

    public void Delete(string id)
    {
        if (!CollegePieceStore.IsId(id))
            return;

        using var scope = Sync.EnterScope();

        File.Delete(PathOf(id));
    }
}
```

- [ ] **Step 9: Add the texts.** In `CollegeText.cs`, add before the closing brace:

```csharp

    public const string QUIZ_SAVED = "Quiz saved.";
    public const string QUIZ_DELETED = "Quiz deleted.";
    public const string QUIZ_COPIED = "Quiz copied.";
    public const string QUIZ_LIMIT = "You can keep 20 quizzes.";
    public const string QUIZ_NOT_YOURS = "That quiz is not yours.";
    public const string QUIZ_GONE = "That quiz is gone.";
    public const string QUIZ_NOT_ALLOWED = "Only Teachers can keep quizzes.";
    public const string QUIZ_SAVE_FAILED = "Your quiz could not be saved.";

    public static string QuizProblemText(QuizProblem problem)
    {
        var n = problem.Question;

        return problem.Check switch
        {
            QuizCheck.TitleTooLong     => "Quiz titles can be 40 characters at most.",
            QuizCheck.TooManyQuestions => "A quiz can hold 20 questions at most.",
            QuizCheck.QuestionTooLong  => $"Question {n} is over 150 characters.",
            QuizCheck.TooManyAnswers   => $"Question {n} has more than 4 answers.",
            QuizCheck.AnswerTooLong    => $"An answer in question {n} is too long.",
            QuizCheck.BadRightAnswer   => $"Question {n} marks a missing answer.",
            QuizCheck.NoTitle          => "Give your quiz a title.",
            QuizCheck.TooFewQuestions  => "A quiz needs at least 5 questions.",
            QuizCheck.EmptyQuestion    => $"Question {n} has no text.",
            QuizCheck.TooFewAnswers    => $"Question {n} needs at least 2 answers.",
            QuizCheck.EmptyAnswer      => $"Question {n} has an empty answer.",
            QuizCheck.NoRightAnswer    => $"Question {n} needs a right answer.",
            _                          => string.Empty
        };
    }
```

- [ ] **Step 10: Add the store to the service.** In `CollegeService.cs`:
  - add the field `private readonly ICollegeQuizStore Quizzes;` after `Pieces`;
  - add the constructor parameter `ICollegeQuizStore quizzes,` right after `ICollegePictureStore pictures,`;
  - add `Quizzes = quizzes;` after `Pictures = pictures;`.

  In `Tests/Chaos.Tests/College/CollegeServiceTests.cs` and `CollegeEntryServiceTests.cs`, add `new MemoryQuizStore(),` as the argument right after the `ICollegePictureStore` argument of every `new CollegeService(...)` call.

  In `Chaos/Extensions/ServiceCollectionExtensions.cs`, `AddCollege`, add after the picture store line:

```csharp
        services.AddSingleton<ICollegeQuizStore, CollegeQuizStore>();
```

- [ ] **Step 11: Add the quiz service.** Create `Chaos/Services/College/CollegeService.Quizzes.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Microsoft.Extensions.Logging;

namespace Chaos.Services.College;

public sealed record QuizOutcome(QuizResultCode Result, string Message, string Id = "");

public sealed partial class CollegeService
{
    private const string COPY_PREFIX = "Copy of ";

    /// <summary>Anyone who may teach some subject may keep quizzes.</summary>
    public bool MayKeepQuizzes(string name, bool isKnight, bool isAdmin)
    {
        using var scope = Sync.EnterScope();

        return CollegeRoles.CanPost(State, name, isKnight, isAdmin);
    }

    /// <summary>Copies, sorted by title.</summary>
    public IReadOnlyList<CollegeQuizIndex> QuizzesOf(string name)
    {
        using var scope = Sync.EnterScope();

        return State.Quizzes
                    .Where(q => q.Owner.Equals(name, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(q => q.Title, StringComparer.OrdinalIgnoreCase)
                    .Select(
                        q => new CollegeQuizIndex
                        {
                            Id = q.Id,
                            Owner = q.Owner,
                            Title = q.Title,
                            QuestionCount = q.QuestionCount,
                            UpdatedUtc = q.UpdatedUtc
                        })
                    .ToList();
    }

    public CollegeQuiz? LoadQuiz(string name, bool isKnight, bool isAdmin, string id)
    {
        using var scope = Sync.EnterScope();

        if (!CollegeRoles.CanPost(State, name, isKnight, isAdmin) || OwnedQuizLocked(name, id) is null)
            return null;

        return Quizzes.Load(id);
    }

    public QuizOutcome SaveQuiz(string name, bool isKnight, bool isAdmin, string id, CollegeQuiz quiz)
    {
        using var scope = Sync.EnterScope();

        if (!CollegeRoles.CanPost(State, name, isKnight, isAdmin))
            return new QuizOutcome(QuizResultCode.Refused, CollegeText.QUIZ_NOT_ALLOWED);

        var clean = QuizRules.Normalize(quiz);
        var problem = QuizRules.CheckSave(clean);

        if (!problem.IsOk)
            return new QuizOutcome(QuizResultCode.Refused, CollegeText.QuizProblemText(problem));

        var isNew = string.IsNullOrEmpty(id);
        var index = isNew ? null : OwnedQuizLocked(name, id);

        if (!isNew && index is null)
            return new QuizOutcome(QuizResultCode.Refused, CollegeText.QUIZ_NOT_YOURS);

        if (isNew && (QuizCountLocked(name) >= CollegeProtocol.MAX_QUIZZES_PER_OWNER))
            return new QuizOutcome(QuizResultCode.Refused, CollegeText.QUIZ_LIMIT);

        clean.Id = isNew ? CollegePiece.NewId() : id;

        return WriteQuizLocked(name, clean, index, QuizResultCode.Saved, CollegeText.QUIZ_SAVED);
    }

    public QuizOutcome CopyQuiz(string name, bool isKnight, bool isAdmin, string id)
    {
        using var scope = Sync.EnterScope();

        if (!CollegeRoles.CanPost(State, name, isKnight, isAdmin))
            return new QuizOutcome(QuizResultCode.Refused, CollegeText.QUIZ_NOT_ALLOWED);

        if (OwnedQuizLocked(name, id) is null)
            return new QuizOutcome(QuizResultCode.Refused, CollegeText.QUIZ_NOT_YOURS);

        if (QuizCountLocked(name) >= CollegeProtocol.MAX_QUIZZES_PER_OWNER)
            return new QuizOutcome(QuizResultCode.Refused, CollegeText.QUIZ_LIMIT);

        if (Quizzes.Load(id) is not { } original)
            return new QuizOutcome(QuizResultCode.Refused, CollegeText.QUIZ_GONE);

        var title = COPY_PREFIX + original.Title;
        original.Id = CollegePiece.NewId();
        original.Title = title.Length > CollegeProtocol.MAX_QUIZ_TITLE_CHARS ? title[..CollegeProtocol.MAX_QUIZ_TITLE_CHARS] : title;

        return WriteQuizLocked(name, original, null, QuizResultCode.Copied, CollegeText.QUIZ_COPIED);
    }

    public QuizOutcome DeleteQuiz(string name, bool isKnight, bool isAdmin, string id)
    {
        using var scope = Sync.EnterScope();

        if (!CollegeRoles.CanPost(State, name, isKnight, isAdmin))
            return new QuizOutcome(QuizResultCode.Refused, CollegeText.QUIZ_NOT_ALLOWED);

        if (OwnedQuizLocked(name, id) is not { } index)
            return new QuizOutcome(QuizResultCode.Refused, CollegeText.QUIZ_NOT_YOURS);

        State.Quizzes.Remove(index);
        SaveLocked();

        try
        {
            Quizzes.Delete(id);
        } catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            //the index no longer names it, so the file is only an orphan on disk
            Logger.LogWarning(e, "College quiz file {Id} could not be deleted", id);
        }

        return new QuizOutcome(QuizResultCode.Deleted, CollegeText.QUIZ_DELETED, id);
    }

    /// <summary>Writes the file first, so a failed write leaves the index as it was.</summary>
    private QuizOutcome WriteQuizLocked(string owner, CollegeQuiz quiz, CollegeQuizIndex? index, QuizResultCode result, string message)
    {
        quiz.UpdatedUtc = Now;

        try
        {
            Quizzes.Save(quiz);
        } catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Logger.LogError(e, "College quiz {Id} by {Owner} could not be saved", quiz.Id, owner);

            return new QuizOutcome(QuizResultCode.Refused, CollegeText.QUIZ_SAVE_FAILED);
        }

        if (index is null)
        {
            index = new CollegeQuizIndex { Id = quiz.Id, Owner = owner };
            State.Quizzes.Add(index);
        }

        index.Title = quiz.Title;
        index.QuestionCount = quiz.Questions.Count;
        index.UpdatedUtc = quiz.UpdatedUtc;
        SaveLocked();

        return new QuizOutcome(result, message, quiz.Id);
    }

    private CollegeQuizIndex? OwnedQuizLocked(string name, string id)
        => State.Quizzes.FirstOrDefault(q => q.Id == id && q.Owner.Equals(name, StringComparison.OrdinalIgnoreCase));

    private int QuizCountLocked(string name) => State.Quizzes.Count(q => q.Owner.Equals(name, StringComparison.OrdinalIgnoreCase));
}
```

- [ ] **Step 12: Run the tests to see them pass.** Run the Verify commands. Expected: all pass. Then run `dotnet build Chaos.slnx`. Expected: Build succeeded.

```json:metadata
{"files": ["Chaos/Services/College/CollegeQuiz.cs", "Chaos/Services/College/QuizRules.cs", "Chaos/Services/College/CollegeQuizStore.cs", "Chaos/Services/College/CollegeService.Quizzes.cs", "Chaos/Services/College/CollegeService.cs", "Chaos/Services/College/CollegeState.cs", "Chaos/Services/College/CollegeText.cs", "Chaos/Extensions/ServiceCollectionExtensions.cs", "Tests/Chaos.Tests/College/MemoryQuizStore.cs", "Tests/Chaos.Tests/College/QuizRulesTests.cs", "Tests/Chaos.Tests/College/CollegeQuizStoreTests.cs", "Tests/Chaos.Tests/College/CollegeQuizServiceTests.cs", "Tests/Chaos.Tests/College/CollegeServiceTests.cs", "Tests/Chaos.Tests/College/CollegeEntryServiceTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/CollegeQuizServiceTests/*\"", "acceptanceCriteria": ["CheckSave edges (title 41, 21 questions, question 151, 5 answers, answer 41, bad right) and unfinished quiz saves", "CheckReady needs title, 5-20 questions, text, 2-4 filled answers, a right answer; names the question", "Normalize turns line breaks into spaces and trims", "store round-trips, refuses bad ids, deletes", "service: new id + index, save in place, 21st refused, owner checks, non-teacher refused, copy 'Copy of', delete, failed write leaves index", "existing College tests compile and pass"], "modelTier": "standard"}
```

---

### Task 3: Live quiz and debate rules (server repo)

**Goal:** Pure units run a class quiz and a class debate on plain state: opening and revealing questions, scores, top scorers and the attendance share; debate phases, sides, hands, the floor, gains, the winner and best speakers.

**Files:**
- Create: `Chaos/Services/College/CollegeTools.cs`
- Create: `Chaos/Services/College/QuizSession.cs`
- Create: `Chaos/Services/College/DebateSession.cs`
- Test: new `Tests/Chaos.Tests/College/QuizSessionTests.cs`, `Tests/Chaos.Tests/College/DebateSessionTests.cs`

**Acceptance Criteria:**
- [ ] Quiz: `Next` opens a question with a deadline and counts it as asked; it is refused while a question is open and after the last one
- [ ] Quiz: an answer before the deadline replaces an earlier one; a late answer, a wrong question number or an answer index outside the list is refused
- [ ] Quiz: the deadline reveals the question, adds answered counts and scores, and records how many were right
- [ ] Quiz: `End` during an open question drops it (not scored, not counted as asked)
- [ ] Quiz: top scorers are the best score's group when it has 1-3 students and the score is at least 1; scores 5, 5, 4 give both 5s; four tied give none
- [ ] Quiz: the attendance check passes at exactly half the questions asked (5 of 10, 3 of 5) and fails below; the guard needs 5 asked
- [ ] Debate: the opening vote ends at 60 s and saves the opening counts and voters; the final vote ends 30 s after it starts and sets the result
- [ ] Debate: hands keep their order; giving the floor drops that hand and adds the speaker once; a turn ends at its deadline; a student who leaves loses their hand and the floor but keeps their side
- [ ] Debate: the side with the bigger gain wins; equal gains is a draw; Undecided never wins
- [ ] Debate: best speakers must be past speakers, at most 3, and only with the bonus on; the guard needs the final vote and 3 speakers; attendance needs both votes
- [ ] Debate: `Blocks` names the floor holder for anyone else, and nothing for the holder or when no one holds the floor

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/QuizSessionTests/*"` -> all pass; same for `DebateSessionTests`

**Steps:**

- [ ] **Step 1: Write the failing quiz tests.** Create `Tests/Chaos.Tests/College/QuizSessionTests.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Services.College;
using FluentAssertions;

namespace Chaos.Tests.College;

public sealed class QuizSessionTests
{
    private static readonly DateTime Noon = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    private static LiveQuiz Start(int questions = 5, int seconds = 20)
        => QuizSession.Start(QuizSamples.Ready(questions), seconds, true, true);

    private static void Ask(LiveQuiz quiz, DateTime now, params (string Name, int Answer)[] answers)
    {
        QuizSession.Next(quiz, now).Should().Be(QuizStep.Done);

        foreach (var (name, answer) in answers)
            QuizSession.Answer(quiz, name, quiz.Current + 1, answer, now).Should().BeTrue();

        QuizSession.Tick(quiz, now.AddSeconds(quiz.Seconds)).Should().BeTrue();
    }

    [Test]
    public void Start_copies_the_quiz_and_waits()
    {
        var source = QuizSamples.Ready();
        var quiz = QuizSession.Start(source, 30, true, false);
        source.Questions[0].Text = "changed";

        quiz.Phase.Should().Be(QuizPhase.Waiting);
        quiz.Current.Should().Be(-1);
        quiz.Questions[0].Text.Should().Be("Question 1?");
        quiz.Seconds.Should().Be(30);
        quiz.Attendance.Should().BeTrue();
        quiz.Bonus.Should().BeFalse();
    }

    [Test]
    public void Next_opens_a_question_with_a_deadline_and_counts_it()
    {
        var quiz = Start();

        QuizSession.Next(quiz, Noon).Should().Be(QuizStep.Done);

        quiz.Phase.Should().Be(QuizPhase.Open);
        quiz.Current.Should().Be(0);
        quiz.DeadlineUtc.Should().Be(Noon.AddSeconds(20));
        quiz.Asked.Should().Be(1);
        QuizSession.Next(quiz, Noon).Should().Be(QuizStep.NotNow);
    }

    [Test]
    public void Next_after_the_last_question_is_refused()
    {
        var quiz = Start(5);

        for (var i = 0; i < 5; i++)
            Ask(quiz, Noon.AddMinutes(i));

        QuizSession.IsLast(quiz).Should().BeTrue();
        QuizSession.Next(quiz, Noon.AddMinutes(9)).Should().Be(QuizStep.NoMoreQuestions);
    }

    [Test]
    public void An_answer_before_the_deadline_replaces_the_earlier_one()
    {
        var quiz = Start();
        QuizSession.Next(quiz, Noon);

        QuizSession.Answer(quiz, "Aroha", 1, 1, Noon.AddSeconds(5)).Should().BeTrue();
        QuizSession.Answer(quiz, "aroha", 1, 0, Noon.AddSeconds(10)).Should().BeTrue();

        quiz.Answers.Should().Equal(new Dictionary<string, int> { ["aroha"] = 0 });
        quiz.Names["aroha"].Should().Be("aroha");
    }

    [Test]
    public void Late_or_wrong_answers_are_refused()
    {
        var quiz = Start();

        QuizSession.Answer(quiz, "Aroha", 1, 0, Noon).Should().BeFalse();

        QuizSession.Next(quiz, Noon);

        QuizSession.Answer(quiz, "Aroha", 2, 0, Noon).Should().BeFalse();
        QuizSession.Answer(quiz, "Aroha", 1, 3, Noon).Should().BeFalse();
        QuizSession.Answer(quiz, "Aroha", 1, -1, Noon).Should().BeFalse();
        QuizSession.Answer(quiz, "Aroha", 1, 0, Noon.AddSeconds(20)).Should().BeFalse();
        quiz.Answers.Should().BeEmpty();
    }

    [Test]
    public void The_deadline_reveals_and_scores_the_question()
    {
        var quiz = Start();
        QuizSession.Next(quiz, Noon);
        QuizSession.Answer(quiz, "Aroha", 1, 0, Noon);
        QuizSession.Answer(quiz, "Kael", 1, 2, Noon);

        QuizSession.Tick(quiz, Noon.AddSeconds(19)).Should().BeFalse();
        QuizSession.Tick(quiz, Noon.AddSeconds(20)).Should().BeTrue();

        quiz.Phase.Should().Be(QuizPhase.Revealed);
        quiz.LastRight.Should().Be(1);
        quiz.LastAnswered.Should().Be(2);
        quiz.Scores.Should().Equal(new Dictionary<string, int> { ["aroha"] = 1, ["kael"] = 0 });
        quiz.Answered.Should().Equal(new Dictionary<string, int> { ["aroha"] = 1, ["kael"] = 1 });
        quiz.Answers.Should().HaveCount(2);
        QuizSession.Tick(quiz, Noon.AddSeconds(40)).Should().BeFalse();
    }

    [Test]
    public void Ending_during_an_open_question_drops_it()
    {
        var quiz = Start();
        Ask(quiz, Noon, ("Aroha", 0));
        QuizSession.Next(quiz, Noon.AddMinutes(1));
        QuizSession.Answer(quiz, "Aroha", 2, 0, Noon.AddMinutes(1));

        QuizSession.End(quiz);

        quiz.Phase.Should().Be(QuizPhase.Ended);
        quiz.Asked.Should().Be(1);
        quiz.Scores["aroha"].Should().Be(1);
        quiz.Answered["aroha"].Should().Be(1);
    }

    [Test]
    public void Top_scorers_are_the_best_scores_group_of_up_to_three()
    {
        var quiz = Start();
        quiz.Scores = new Dictionary<string, int> { ["a"] = 5, ["b"] = 5, ["c"] = 4 };
        QuizSession.TopScorers(quiz).Should().BeEquivalentTo(["a", "b"]);

        quiz.Scores = new Dictionary<string, int> { ["a"] = 3, ["b"] = 3, ["c"] = 3 };
        QuizSession.TopScorers(quiz).Should().BeEquivalentTo(["a", "b", "c"]);

        quiz.Scores = new Dictionary<string, int> { ["a"] = 5, ["b"] = 5, ["c"] = 5, ["d"] = 5, ["e"] = 4 };
        QuizSession.TopScorers(quiz).Should().BeEmpty();

        quiz.Scores = new Dictionary<string, int> { ["a"] = 0, ["b"] = 0 };
        QuizSession.TopScorers(quiz).Should().BeEmpty();
    }

    [Test]
    public void Leaders_are_the_three_best_by_score_then_name()
    {
        var quiz = Start();
        quiz.Scores = new Dictionary<string, int> { ["mira"] = 2, ["kael"] = 3, ["aroha"] = 3, ["tam"] = 1, ["zed"] = 0 };
        quiz.Names = new Dictionary<string, string> { ["mira"] = "Mira", ["kael"] = "Kael", ["aroha"] = "Aroha", ["tam"] = "Tam" };

        QuizSession.Leaders(quiz).Should().Equal(("Aroha", 3), ("Kael", 3), ("Mira", 2));
    }

    [Test]
    public void Attendance_passes_at_exactly_half_the_questions()
    {
        var quiz = Start(10);

        for (var i = 0; i < 10; i++)
        {
            (string, int)[] answers = i < 5 ? [("Aroha", 1), ("Kael", 1)] : i < 9 ? [("Kael", 1)] : [];
            Ask(quiz, Noon.AddMinutes(i), answers);
        }

        quiz.Asked.Should().Be(10);
        QuizSession.PassedAttendance(quiz, "Aroha").Should().BeTrue();
        QuizSession.PassedAttendance(quiz, "Kael").Should().BeTrue();
        QuizSession.PassedAttendance(quiz, "Mira").Should().BeFalse();

        var five = Start(5);

        for (var i = 0; i < 5; i++)
        {
            (string, int)[] answers = i < 3 ? [("Aroha", 0)] : i < 4 ? [("Kael", 0)] : [];
            Ask(five, Noon.AddMinutes(i), answers);
        }

        QuizSession.PassedAttendance(five, "Aroha").Should().BeTrue();
        QuizSession.PassedAttendance(five, "Kael").Should().BeFalse();
    }

    [Test]
    public void The_guard_needs_five_questions_asked()
    {
        var quiz = Start(5);

        for (var i = 0; i < 4; i++)
            Ask(quiz, Noon.AddMinutes(i));

        QuizSession.GuardMet(quiz).Should().BeFalse();

        Ask(quiz, Noon.AddMinutes(5));

        QuizSession.GuardMet(quiz).Should().BeTrue();
    }
}
```

- [ ] **Step 2: Write the failing debate tests.** Create `Tests/Chaos.Tests/College/DebateSessionTests.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Services.College;
using FluentAssertions;

namespace Chaos.Tests.College;

public sealed class DebateSessionTests
{
    private static readonly DateTime Noon = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);
    private static readonly HashSet<string> Everyone = ["aroha", "kael", "mira", "tam"];

    private static LiveDebate Start(bool bonus = true) => DebateSession.Start("Sgrios is needed for balance", 2, true, bonus, Noon);

    private static LiveDebate OnTheFloor(bool bonus = true)
    {
        var debate = Start(bonus);
        DebateSession.PickSide(debate, "Aroha", DebateSide.For);
        DebateSession.PickSide(debate, "Kael", DebateSide.Against);
        DebateSession.PickSide(debate, "Mira", DebateSide.Undecided);
        DebateSession.Tick(debate, Noon.AddSeconds(60), Everyone);

        return debate;
    }

    [Test]
    public void The_opening_vote_ends_at_60_seconds_and_saves_the_counts()
    {
        var debate = Start();
        debate.Phase.Should().Be(DebatePhase.Opening);
        DebateSession.PickSide(debate, "Aroha", DebateSide.For).Should().BeTrue();
        DebateSession.PickSide(debate, "Kael", DebateSide.Undecided).Should().BeTrue();

        DebateSession.Tick(debate, Noon.AddSeconds(59), Everyone).Changed.Should().BeFalse();
        DebateSession.Tick(debate, Noon.AddSeconds(60), Everyone).Changed.Should().BeTrue();

        debate.Phase.Should().Be(DebatePhase.Floor);
        (debate.OpeningFor, debate.OpeningAgainst, debate.OpeningUndecided).Should().Be((1, 0, 1));
        debate.OpeningVoters.Should().BeEquivalentTo(["aroha", "kael"]);
    }

    [Test]
    public void No_side_or_a_side_of_none_is_refused()
    {
        var debate = Start();

        DebateSession.PickSide(debate, "Aroha", DebateSide.None).Should().BeFalse();
        debate.Sides.Should().BeEmpty();
    }

    [Test]
    public void Hands_keep_their_order_and_the_floor_takes_one()
    {
        var debate = OnTheFloor();

        DebateSession.Hand(debate, "Mira", true).Should().BeTrue();
        DebateSession.Hand(debate, "Aroha", true).Should().BeTrue();
        DebateSession.Hand(debate, "Mira", true).Should().BeFalse();
        debate.Hands.Should().Equal("mira", "aroha");

        DebateSession.GiveFloor(debate, "Aroha", Noon.AddMinutes(2)).Should().BeTrue();

        debate.Floor.Should().Be("aroha");
        debate.FloorUntilUtc.Should().Be(Noon.AddMinutes(4));
        debate.Hands.Should().Equal("mira");
        debate.Speakers.Should().Equal("aroha");
        DebateSession.Hand(debate, "Aroha", true).Should().BeFalse();
        DebateSession.GiveFloor(debate, "Kael", Noon.AddMinutes(2)).Should().BeFalse();
    }

    [Test]
    public void A_speaker_is_listed_once()
    {
        var debate = OnTheFloor();

        DebateSession.Hand(debate, "Aroha", true);
        DebateSession.GiveFloor(debate, "Aroha", Noon.AddMinutes(2));
        DebateSession.TakeBack(debate).Should().BeTrue();
        DebateSession.Hand(debate, "Aroha", true);
        DebateSession.GiveFloor(debate, "Aroha", Noon.AddMinutes(3));

        debate.Speakers.Should().Equal("aroha");
    }

    [Test]
    public void A_turn_ends_at_its_deadline()
    {
        var debate = OnTheFloor();
        DebateSession.Hand(debate, "Kael", true);
        DebateSession.GiveFloor(debate, "Kael", Noon.AddMinutes(2));

        DebateSession.Tick(debate, Noon.AddMinutes(3), Everyone).TurnEnded.Should().BeNull();
        var tick = DebateSession.Tick(debate, Noon.AddMinutes(4), Everyone);

        tick.Changed.Should().BeTrue();
        tick.TurnEnded.Should().Be("kael");
        debate.Floor.Should().BeNull();
    }

    [Test]
    public void Leaving_the_room_drops_the_hand_and_the_floor_but_keeps_the_side()
    {
        var debate = OnTheFloor();
        DebateSession.Hand(debate, "Kael", true);
        DebateSession.GiveFloor(debate, "Kael", Noon.AddMinutes(2));
        DebateSession.Hand(debate, "Mira", true);

        var tick = DebateSession.Tick(debate, Noon.AddMinutes(2), new HashSet<string> { "aroha" });

        tick.Changed.Should().BeTrue();
        tick.TurnEnded.Should().Be("kael");
        debate.Floor.Should().BeNull();
        debate.Hands.Should().BeEmpty();
        debate.Sides["kael"].Should().Be(DebateSide.Against);
    }

    [Test]
    public void The_final_vote_ends_after_30_seconds_with_the_bigger_gain_winning()
    {
        var debate = OnTheFloor();
        DebateSession.StartFinal(debate, Noon.AddMinutes(10)).Should().BeTrue();
        debate.Phase.Should().Be(DebatePhase.Final);
        DebateSession.PickSide(debate, "Mira", DebateSide.For);
        DebateSession.PickSide(debate, "Tam", DebateSide.For);

        DebateSession.Tick(debate, Noon.AddMinutes(10).AddSeconds(30), Everyone).Changed.Should().BeTrue();

        debate.Phase.Should().Be(DebatePhase.Result);
        debate.FinalHeld.Should().BeTrue();
        debate.Result.Should().Be(DebateResult.For);
        DebateSession.Gains(debate).Should().Be((2, 0));
        debate.FinalVoters.Should().BeEquivalentTo(["aroha", "kael", "mira", "tam"]);
    }

    [Test]
    public void Equal_gains_are_a_draw_and_undecided_never_wins()
    {
        var debate = OnTheFloor();
        DebateSession.StartFinal(debate, Noon.AddMinutes(10));
        DebateSession.PickSide(debate, "Tam", DebateSide.Undecided);
        DebateSession.Tick(debate, Noon.AddMinutes(11), Everyone);

        debate.Result.Should().Be(DebateResult.Draw);
    }

    [Test]
    public void The_final_vote_starts_only_from_the_floor_and_clears_hands_and_floor()
    {
        var debate = Start();
        DebateSession.StartFinal(debate, Noon).Should().BeFalse();

        debate = OnTheFloor();
        DebateSession.Hand(debate, "Kael", true);
        DebateSession.Hand(debate, "Mira", true);
        DebateSession.GiveFloor(debate, "Kael", Noon.AddMinutes(2));

        DebateSession.StartFinal(debate, Noon.AddMinutes(3)).Should().BeTrue();

        debate.Floor.Should().BeNull();
        debate.Hands.Should().BeEmpty();
        DebateSession.Hand(debate, "Mira", true).Should().BeFalse();
    }

    [Test]
    public void Best_speakers_must_have_spoken_and_number_at_most_three()
    {
        var debate = OnTheFloor();

        foreach (var name in new[] { "Aroha", "Kael", "Mira" })
        {
            DebateSession.Hand(debate, name, true);
            DebateSession.GiveFloor(debate, name, Noon.AddMinutes(2));
        }

        DebateSession.StartFinal(debate, Noon.AddMinutes(10));
        DebateSession.PickSpeakers(debate, ["Aroha"]).Should().BeFalse();
        DebateSession.Tick(debate, Noon.AddMinutes(11), Everyone);

        DebateSession.PickSpeakers(debate, ["Tam"]).Should().BeFalse();
        DebateSession.PickSpeakers(debate, ["Aroha", "Kael", "Mira", "Aroha"]).Should().BeTrue();

        debate.BestSpeakers.Should().Equal("aroha", "kael", "mira");
        debate.Phase.Should().Be(DebatePhase.Ended);
    }

    [Test]
    public void Without_the_bonus_no_best_speakers_are_picked()
    {
        var debate = OnTheFloor(bonus: false);
        DebateSession.Hand(debate, "Aroha", true);
        DebateSession.GiveFloor(debate, "Aroha", Noon.AddMinutes(2));
        DebateSession.StartFinal(debate, Noon.AddMinutes(10));
        DebateSession.Tick(debate, Noon.AddMinutes(11), Everyone);

        DebateSession.PickSpeakers(debate, ["Aroha"]).Should().BeFalse();
    }

    [Test]
    public void The_guard_needs_the_final_vote_and_three_speakers()
    {
        var debate = OnTheFloor();

        foreach (var name in new[] { "Aroha", "Kael", "Mira" })
        {
            DebateSession.Hand(debate, name, true);
            DebateSession.GiveFloor(debate, name, Noon.AddMinutes(2));
        }

        DebateSession.GuardMet(debate).Should().BeFalse();

        DebateSession.StartFinal(debate, Noon.AddMinutes(10));
        DebateSession.Tick(debate, Noon.AddMinutes(11), Everyone);

        DebateSession.GuardMet(debate).Should().BeTrue();
    }

    [Test]
    public void Attendance_needs_both_votes()
    {
        var debate = OnTheFloor();
        DebateSession.StartFinal(debate, Noon.AddMinutes(10));
        DebateSession.PickSide(debate, "Tam", DebateSide.For);
        DebateSession.Tick(debate, Noon.AddMinutes(11), Everyone);

        DebateSession.PassedAttendance(debate, "Aroha").Should().BeTrue();
        DebateSession.PassedAttendance(debate, "Tam").Should().BeFalse();
    }

    [Test]
    public void Only_the_floor_holder_is_not_blocked()
    {
        var debate = OnTheFloor();
        DebateSession.Blocks(debate, "Aroha").Should().BeNull();

        DebateSession.Hand(debate, "Kael", true);
        DebateSession.GiveFloor(debate, "Kael", Noon.AddMinutes(2));

        DebateSession.Blocks(debate, "Aroha").Should().Be("Kael");
        DebateSession.Blocks(debate, "KAEL").Should().BeNull();
    }

    [Test]
    public void End_closes_the_debate_from_any_phase()
    {
        var debate = OnTheFloor();
        DebateSession.Hand(debate, "Kael", true);

        DebateSession.End(debate);

        debate.Phase.Should().Be(DebatePhase.Ended);
        debate.Hands.Should().BeEmpty();
        DebateSession.PickSide(debate, "Kael", DebateSide.For).Should().BeFalse();
    }
}
```

- [ ] **Step 3: Run the tests to see them fail.** Run the Verify commands. Expected: compile errors (`LiveQuiz`, `QuizSession`, `DebateSession` don't exist).

- [ ] **Step 4: Add the state.** Create `Chaos/Services/College/CollegeTools.cs`:

```csharp
using Chaos.DarkAges.Definitions;

namespace Chaos.Services.College;

/// <summary>A quiz running in a class. Dictionaries are keyed by lower-case character name; <see cref="QuizSession" /> holds the rules.</summary>
public sealed class LiveQuiz
{
    public string QuizId { get; set; } = "";
    public string Title { get; set; } = "";
    public List<QuizQuestion> Questions { get; set; } = [];
    public int Seconds { get; set; }
    public bool Attendance { get; set; }
    public bool Bonus { get; set; }

    /// <summary>The question being asked or last revealed; -1 before the first.</summary>
    public int Current { get; set; } = -1;

    public QuizPhase Phase { get; set; } = QuizPhase.Waiting;
    public DateTime DeadlineUtc { get; set; }
    public int Asked { get; set; }

    /// <summary>Answers to the current question. Kept after the reveal, so each card can show its pick.</summary>
    public Dictionary<string, int> Answers { get; set; } = [];

    public Dictionary<string, int> Scores { get; set; } = [];
    public Dictionary<string, int> Answered { get; set; } = [];
    public Dictionary<string, string> Names { get; set; } = [];
    public int LastRight { get; set; }
    public int LastAnswered { get; set; }
}

/// <summary>A debate running in a class. Keys are lower-case character names; <see cref="DebateSession" /> holds the rules.</summary>
public sealed class LiveDebate
{
    public string Motion { get; set; } = "";
    public int TurnMinutes { get; set; }
    public bool Attendance { get; set; }
    public bool Bonus { get; set; }
    public DebatePhase Phase { get; set; } = DebatePhase.Opening;

    /// <summary>When the opening or final vote ends.</summary>
    public DateTime DeadlineUtc { get; set; }

    public Dictionary<string, DebateSide> Sides { get; set; } = [];
    public Dictionary<string, string> Names { get; set; } = [];
    public List<string> Hands { get; set; } = [];
    public string? Floor { get; set; }
    public DateTime FloorUntilUtc { get; set; }
    public List<string> Speakers { get; set; } = [];
    public List<string> OpeningVoters { get; set; } = [];
    public List<string> FinalVoters { get; set; } = [];
    public int OpeningFor { get; set; }
    public int OpeningAgainst { get; set; }
    public int OpeningUndecided { get; set; }
    public bool FinalHeld { get; set; }
    public DebateResult Result { get; set; }
    public List<string> BestSpeakers { get; set; } = [];
}
```

- [ ] **Step 5: Add the quiz rules.** Create `Chaos/Services/College/QuizSession.cs`:

```csharp
using Chaos.DarkAges.Definitions;

namespace Chaos.Services.College;

public enum QuizStep
{
    Done,
    NotNow,
    NoMoreQuestions
}

/// <summary>The rules of a class quiz, on a <see cref="LiveQuiz" />. Not thread-safe; <see cref="CollegeService" /> holds the lock.</summary>
public static class QuizSession
{
    public static LiveQuiz Start(CollegeQuiz quiz, int seconds, bool attendance, bool bonus)
        => new()
        {
            QuizId = quiz.Id,
            Title = quiz.Title,
            Questions = quiz.Questions
                            .Select(q => new QuizQuestion { Text = q.Text, Answers = q.Answers.ToList(), Right = q.Right })
                            .ToList(),
            Seconds = seconds,
            Attendance = attendance,
            Bonus = bonus
        };

    public static bool IsLast(LiveQuiz quiz) => quiz.Current >= quiz.Questions.Count - 1;

    public static QuizStep Next(LiveQuiz quiz, DateTime now)
    {
        if (quiz.Phase is QuizPhase.Open or QuizPhase.Ended)
            return QuizStep.NotNow;

        if (IsLast(quiz))
            return QuizStep.NoMoreQuestions;

        quiz.Current++;
        quiz.Phase = QuizPhase.Open;
        quiz.DeadlineUtc = now.AddSeconds(quiz.Seconds);
        quiz.Answers.Clear();
        quiz.Asked++;

        return QuizStep.Done;
    }

    /// <summary><paramref name="number" /> is 1-based, so an answer meant for an earlier question is never counted on this one.</summary>
    public static bool Answer(LiveQuiz quiz, string name, int number, int answer, DateTime now)
    {
        if ((quiz.Phase != QuizPhase.Open) || (number != quiz.Current + 1) || (now >= quiz.DeadlineUtc))
            return false;

        if ((answer < 0) || (answer >= quiz.Questions[quiz.Current].Answers.Count))
            return false;

        var key = CollegeState.Key(name);
        quiz.Answers[key] = answer;
        quiz.Names[key] = name;

        return true;
    }

    /// <summary>Reveals the open question once its deadline has passed. True when something changed.</summary>
    public static bool Tick(LiveQuiz quiz, DateTime now)
    {
        if ((quiz.Phase != QuizPhase.Open) || (now < quiz.DeadlineUtc))
            return false;

        var right = quiz.Questions[quiz.Current].Right;
        quiz.LastRight = 0;

        foreach ((var key, var answer) in quiz.Answers)
        {
            var isRight = answer == right;
            quiz.Answered[key] = quiz.Answered.GetValueOrDefault(key) + 1;
            quiz.Scores[key] = quiz.Scores.GetValueOrDefault(key) + (isRight ? 1 : 0);

            if (isRight)
                quiz.LastRight++;
        }

        quiz.LastAnswered = quiz.Answers.Count;
        quiz.Phase = QuizPhase.Revealed;

        return true;
    }

    /// <summary>A question still open is dropped: it is not scored and does not count as asked.</summary>
    public static void End(LiveQuiz quiz)
    {
        if (quiz.Phase == QuizPhase.Open)
        {
            quiz.Asked--;
            quiz.Answers.Clear();
        }

        quiz.Phase = QuizPhase.Ended;
    }

    public static bool GuardMet(LiveQuiz quiz) => quiz.Asked >= CollegeProtocol.QUIZ_GUARD_QUESTIONS;

    public static bool PassedAttendance(LiveQuiz quiz, string name)
        => quiz.Answered.GetValueOrDefault(CollegeState.Key(name)) * 2 >= quiz.Asked;

    /// <summary>The students sharing the best score, if it is at least 1 and no more than three share it. Keys.</summary>
    public static IReadOnlyList<string> TopScorers(LiveQuiz quiz)
    {
        var best = quiz.Scores.Values.DefaultIfEmpty(0).Max();

        if (best <= 0)
            return [];

        var top = quiz.Scores.Where(kv => kv.Value == best).Select(kv => kv.Key).ToList();

        return top.Count <= CollegeProtocol.MAX_BONUS_STUDENTS ? top : [];
    }

    /// <summary>The three best scores above zero, ties by name, for the card and the Teacher panel.</summary>
    public static IReadOnlyList<(string Name, int Score)> Leaders(LiveQuiz quiz)
        => quiz.Scores
               .Where(kv => kv.Value > 0)
               .OrderByDescending(kv => kv.Value)
               .ThenBy(kv => kv.Key, StringComparer.Ordinal)
               .Take(CollegeProtocol.MAX_BONUS_STUDENTS)
               .Select(kv => (quiz.Names.GetValueOrDefault(kv.Key, kv.Key), kv.Value))
               .ToList();
}
```

- [ ] **Step 6: Add the debate rules.** Create `Chaos/Services/College/DebateSession.cs`:

```csharp
using Chaos.DarkAges.Definitions;

namespace Chaos.Services.College;

/// <summary>What one debate tick did. <see cref="TurnEnded" /> is the key of a speaker whose turn just ended.</summary>
public readonly record struct DebateTick(bool Changed, string? TurnEnded);

/// <summary>The rules of a class debate, on a <see cref="LiveDebate" />. Not thread-safe; <see cref="CollegeService" /> holds the lock.</summary>
public static class DebateSession
{
    public static LiveDebate Start(string motion, int turnMinutes, bool attendance, bool bonus, DateTime now)
        => new()
        {
            Motion = motion,
            TurnMinutes = turnMinutes,
            Attendance = attendance,
            Bonus = bonus,
            Phase = DebatePhase.Opening,
            DeadlineUtc = now.AddSeconds(CollegeProtocol.DEBATE_OPENING_SECONDS)
        };

    public static bool PickSide(LiveDebate debate, string name, DebateSide side)
    {
        if ((debate.Phase is not (DebatePhase.Opening or DebatePhase.Floor or DebatePhase.Final))
            || (side is not (DebateSide.For or DebateSide.Against or DebateSide.Undecided)))
            return false;

        var key = CollegeState.Key(name);
        debate.Sides[key] = side;
        debate.Names[key] = name;

        return true;
    }

    public static bool Hand(LiveDebate debate, string name, bool raised)
    {
        if (debate.Phase != DebatePhase.Floor)
            return false;

        var key = CollegeState.Key(name);

        if (!raised)
            return debate.Hands.Remove(key);

        if (debate.Hands.Contains(key) || (debate.Floor == key))
            return false;

        debate.Hands.Add(key);
        debate.Names[key] = name;

        return true;
    }

    /// <summary>Only a raised hand can be given the floor; giving it to someone else ends the current turn.</summary>
    public static bool GiveFloor(LiveDebate debate, string name, DateTime now)
    {
        var key = CollegeState.Key(name);

        if ((debate.Phase != DebatePhase.Floor) || !debate.Hands.Remove(key))
            return false;

        debate.Floor = key;
        debate.FloorUntilUtc = now.AddMinutes(debate.TurnMinutes);

        if (!debate.Speakers.Contains(key))
            debate.Speakers.Add(key);

        return true;
    }

    public static bool TakeBack(LiveDebate debate)
    {
        if (debate.Floor is null)
            return false;

        debate.Floor = null;

        return true;
    }

    public static bool StartFinal(LiveDebate debate, DateTime now)
    {
        if (debate.Phase != DebatePhase.Floor)
            return false;

        debate.Phase = DebatePhase.Final;
        debate.DeadlineUtc = now.AddSeconds(CollegeProtocol.DEBATE_FINAL_SECONDS);
        debate.Floor = null;
        debate.Hands.Clear();

        return true;
    }

    /// <summary>Ends a vote whose time is up, ends a turn whose time is up, and drops hands and the floor of anyone not in <paramref name="inRoom" /> (keys).</summary>
    public static DebateTick Tick(LiveDebate debate, DateTime now, IReadOnlySet<string> inRoom)
    {
        var changed = false;
        string? turnEnded = null;

        if ((debate.Phase == DebatePhase.Opening) && (now >= debate.DeadlineUtc))
        {
            (debate.OpeningFor, debate.OpeningAgainst, debate.OpeningUndecided) = Count(debate);
            debate.OpeningVoters = debate.Sides.Keys.ToList();
            debate.Phase = DebatePhase.Floor;
            changed = true;
        }

        if ((debate.Phase == DebatePhase.Final) && (now >= debate.DeadlineUtc))
        {
            debate.FinalVoters = debate.Sides.Keys.ToList();
            debate.FinalHeld = true;
            debate.Result = Winner(debate);
            debate.Phase = DebatePhase.Result;
            changed = true;
        }

        if (debate.Floor is { } holder && ((now >= debate.FloorUntilUtc) || !inRoom.Contains(holder)))
        {
            debate.Floor = null;
            turnEnded = holder;
            changed = true;
        }

        if (debate.Hands.RemoveAll(key => !inRoom.Contains(key)) > 0)
            changed = true;

        return new DebateTick(changed, turnEnded);
    }

    public static (int For, int Against, int Undecided) Count(LiveDebate debate)
        => (debate.Sides.Values.Count(s => s == DebateSide.For),
            debate.Sides.Values.Count(s => s == DebateSide.Against),
            debate.Sides.Values.Count(s => s == DebateSide.Undecided));

    public static (int For, int Against) Gains(LiveDebate debate)
    {
        var (forCount, againstCount, _) = Count(debate);

        return (forCount - debate.OpeningFor, againstCount - debate.OpeningAgainst);
    }

    private static DebateResult Winner(LiveDebate debate)
    {
        var (forGain, againstGain) = Gains(debate);

        return forGain > againstGain ? DebateResult.For : againstGain > forGain ? DebateResult.Against : DebateResult.Draw;
    }

    public static bool PickSpeakers(LiveDebate debate, IReadOnlyList<string> names)
    {
        if ((debate.Phase != DebatePhase.Result) || !debate.Bonus)
            return false;

        var keys = names.Select(CollegeState.Key).Distinct().ToList();

        if ((keys.Count > CollegeProtocol.MAX_BONUS_STUDENTS) || keys.Any(key => !debate.Speakers.Contains(key)))
            return false;

        debate.BestSpeakers = keys;
        debate.Phase = DebatePhase.Ended;

        return true;
    }

    public static void End(LiveDebate debate)
    {
        debate.Floor = null;
        debate.Hands.Clear();
        debate.Phase = DebatePhase.Ended;
    }

    /// <summary>A debate's switches count only if the final vote was held and at least three students spoke.</summary>
    public static bool GuardMet(LiveDebate debate)
        => debate.FinalHeld && (debate.Speakers.Count >= CollegeProtocol.DEBATE_GUARD_SPEAKERS);

    public static bool PassedAttendance(LiveDebate debate, string name)
    {
        var key = CollegeState.Key(name);

        return debate.OpeningVoters.Contains(key) && debate.FinalVoters.Contains(key);
    }

    /// <summary>The floor holder's name when <paramref name="name" /> may not speak, otherwise null.</summary>
    public static string? Blocks(LiveDebate debate, string name)
        => debate.Floor is { } holder && (holder != CollegeState.Key(name)) ? debate.Names.GetValueOrDefault(holder, holder) : null;
}
```

- [ ] **Step 7: Run the tests to see them pass.** Run the Verify commands. Expected: all pass.

```json:metadata
{"files": ["Chaos/Services/College/CollegeTools.cs", "Chaos/Services/College/QuizSession.cs", "Chaos/Services/College/DebateSession.cs", "Tests/Chaos.Tests/College/QuizSessionTests.cs", "Tests/Chaos.Tests/College/DebateSessionTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/QuizSessionTests/*\"", "acceptanceCriteria": ["Next opens with deadline and counts asked; refused while open and after the last", "answers replace before deadline; late, wrong number or bad index refused", "deadline reveals and scores; End mid-question drops it", "top scorers = best group of 1-3 with score >= 1; 5,5,4 -> both 5s; 4 tied -> none", "attendance passes at exactly half; guard needs 5 asked", "debate opening 60 s saves counts and voters; final 30 s sets result", "hands keep order; floor drops the hand, speakers listed once; turn ends at deadline; leaving drops hand and floor, keeps side", "bigger gain wins, equal is draw, undecided never wins", "best speakers: past speakers, max 3, bonus on; guard needs final vote + 3 speakers; attendance needs both votes", "Blocks names the floor holder for others only"], "modelTier": "mechanical"}
```

---
### Task 4: Service: class tools, views, the floor rule and rewards (server repo)

**Goal:** `CollegeService` starts and steers quizzes and debates in the running class, ticks their timers, builds views for the panel, says who may not talk, and at the end of a class applies the attendance checks and bonus rolls.

**Files:**
- Create: `Chaos/Services/College/CollegeService.Tools.cs`
- Modify: `Chaos/Services/College/CollegeService.cs` (`BeginLocked`, `FinishLocked`)
- Modify: `Chaos/Services/College/CollegeState.cs` (`CollegeClass`, `CollegeClassRecord`)
- Modify: `Chaos/Services/College/CollegeText.cs`
- Test: new `Tests/Chaos.Tests/College/CollegeToolServiceTests.cs`

**Acceptance Criteria:**
- [ ] A quiz or debate starts only for the class's Teacher, in the class's room, in a Lecture or Discussion class, with allowed settings, while no tool runs, and once per class per tool; a quiz must pass `CheckReady`
- [ ] Only students in the class's room can answer, pick a side or raise a hand; the Teacher can't
- [ ] Controls need the class's Teacher, the Director or an admin, in the room
- [ ] `ToolTick` reveals a quiz question at its deadline and runs the debate's timers; a turn that ends tells the speaker; giving the floor tells the new speaker
- [ ] `TakeToolChanges` returns a view once per change; after the class ends it returns a closed view for that room; a new class clears it
- [ ] At the end of a completed class: a student failing an attendance check (guard met) doesn't roll and is told; top quiz scorers and best speakers (guard met) roll twice; at most one bonus roll each; the 3-student rule and the Teacher's mark are unchanged by tool checks; history records bonus rolls and failed checks
- [ ] `FloorBlock` names the floor holder for students in the room; never blocks the holder, the Teacher, the Director, admins or another map

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/CollegeToolServiceTests/*"` -> all pass; then the whole `/*/*/*College*/*` group passes

**Steps:**

- [ ] **Step 1: Write the failing tests.** Create `Tests/Chaos.Tests/College/CollegeToolServiceTests.cs`:

```csharp
#region
using Chaos.DarkAges.Definitions;
using Chaos.Services.College;
using Chaos.Storage.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using static Chaos.Tests.Towns.TownTestSupport;
#endregion

namespace Chaos.Tests.College;

public sealed class CollegeToolServiceTests
{
    private const string Room = "college_codex";
    private const string Teacher = "Aroha";
    private static readonly DateTimeOffset Noon = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
    private static readonly string[] Students = ["Bryn", "Cato", "Dara", "Eli"];

    private sealed class FixedRandom(double value) : Random
    {
        public override double NextDouble() => value;
    }

    private static CollegeService Create(out CollegeState state, out FixedTime time, out Mock<ICollegeNotifier> notifier, ClassFormat format = ClassFormat.Lecture)
    {
        state = new CollegeState();
        time = new FixedTime(Noon);
        notifier = new Mock<ICollegeNotifier>();
        var storage = new Mock<IStorage<CollegeState>>();
        storage.SetupGet(s => s.Value).Returns(state);
        state.SetAward(Teacher, CollegeSubject.Lore, AwardTier.Village);

        var service = new CollegeService(
            storage.Object,
            Options.Create(new CollegeOptions()),
            notifier.Object,
            new Mock<ICollegePieceStore>().Object,
            new Mock<ICollegePictureStore>().Object,
            new MemoryQuizStore(),
            new Mock<ICollegeMail>().Object,
            time,
            NullLogger<CollegeService>.Instance) { Random = new FixedRandom(0.0) };

        service.StartWalkIn(Teacher, "m0", CollegeSubject.Lore, format, Room, false, false).Result.Should().Be(StartResult.Started);

        return service;
    }

    private static string SaveQuiz(CollegeService service, int questions = 5)
        => service.SaveQuiz(Teacher, false, false, "", QuizSamples.Ready(questions)).Id;

    private static IReadOnlyList<string> Everyone() => [Teacher, ..Students];

    private static void Attend(CollegeService service, FixedTime time, params string[] people)
    {
        for (var t = 0; t < 40; t++)
        {
            time.Later(TimeSpan.FromSeconds(30));
            service.RecordPresence(Room, people.Select((name, i) => new PresenceSample(name, name == Teacher ? "m0" : $"m{i + 1}")).ToList());
        }
    }

    private static void AskAll(CollegeService service, FixedTime time, int questions, Func<int, string, int?> answerOf)
    {
        for (var number = 1; number <= questions; number++)
        {
            service.ControlQuiz(Teacher, false, Room, QuizControlType.Next).Should().BeEmpty();

            foreach (var student in Students)
                if (answerOf(number, student) is { } answer)
                    service.AnswerQuiz(student, Room, number, answer).Should().BeTrue();

            time.Later(TimeSpan.FromSeconds(20));
            service.ToolTick(Room, Everyone());
        }
    }

    [Test]
    public void A_quiz_starts_only_for_the_teacher_in_the_room_with_allowed_settings()
    {
        var service = Create(out var state, out _, out _);
        var id = SaveQuiz(service);
        var short4 = SaveQuiz(service, 4);

        service.StartQuiz("Kael", Room, id, 20, true, true).Result.Should().Be(ToolStartResult.NotYourClass);
        service.StartQuiz(Teacher, "college_quills", id, 20, true, true).Result.Should().Be(ToolStartResult.WrongRoom);
        service.StartQuiz(Teacher, Room, id, 25, true, true).Result.Should().Be(ToolStartResult.BadSettings);
        service.StartQuiz(Teacher, Room, "0123456789abcdef0123456789abcdef", 20, true, true).Result.Should().Be(ToolStartResult.NoSuchQuiz);

        var notReady = service.StartQuiz(Teacher, Room, short4, 20, true, true);
        notReady.Result.Should().Be(ToolStartResult.NotReady);
        notReady.Message.Should().Be("A quiz needs at least 5 questions.");

        service.StartQuiz(Teacher, Room, id, 20, true, true).Result.Should().Be(ToolStartResult.Started);
        state.RunningClass!.Quiz!.Seconds.Should().Be(20);
        service.ToolStatusOf(Room).Should().Be(new ToolStatus(CollegeTool.Quiz, true, false));
        service.StartDebate(Teacher, Room, "Motion", 1, true, true).Result.Should().Be(ToolStartResult.ToolRunning);
    }

    [Test]
    public void Tools_are_refused_in_activity_classes()
    {
        var service = Create(out _, out _, out _, ClassFormat.Activity);

        service.StartQuiz(Teacher, Room, SaveQuiz(service), 20, false, false).Result.Should().Be(ToolStartResult.WrongFormat);
        service.StartDebate(Teacher, Room, "Motion", 1, false, false).Result.Should().Be(ToolStartResult.WrongFormat);
    }

    [Test]
    public void A_class_runs_one_quiz_and_one_debate()
    {
        var service = Create(out _, out _, out _);
        var id = SaveQuiz(service);

        service.StartQuiz(Teacher, Room, id, 20, false, false);
        service.ControlQuiz(Teacher, false, Room, QuizControlType.End).Should().BeEmpty();

        service.StartQuiz(Teacher, Room, id, 20, false, false).Result.Should().Be(ToolStartResult.AlreadyUsed);
        service.StartDebate(Teacher, Room, "  ", 1, false, false).Message.Should().Be(CollegeText.EMPTY_MOTION);
        service.StartDebate(Teacher, Room, "Sgrios is needed", 4, false, false).Result.Should().Be(ToolStartResult.BadSettings);
        service.StartDebate(Teacher, Room, "Sgrios is needed", 1, false, false).Result.Should().Be(ToolStartResult.Started);
        service.ControlDebate(Teacher, false, Room, DebateControlType.End, []).Should().BeEmpty();
        service.StartDebate(Teacher, Room, "Again", 1, false, false).Result.Should().Be(ToolStartResult.AlreadyUsed);
        service.ToolStatusOf(Room).Should().Be(new ToolStatus(null, true, true));
    }

    [Test]
    public void Only_students_in_the_room_answer_and_only_the_teacher_steers()
    {
        var service = Create(out _, out _, out _);
        service.StartQuiz(Teacher, Room, SaveQuiz(service), 20, false, false);

        service.ControlQuiz("Bryn", false, Room, QuizControlType.Next).Should().Be(CollegeText.TOOL_TEACHER_ONLY);
        service.ControlQuiz("Bryn", true, Room, QuizControlType.Next).Should().BeEmpty();
        service.ControlQuiz(Teacher, false, "college_quills", QuizControlType.Next).Should().Be(CollegeText.NO_CLASS_HERE);

        service.AnswerQuiz(Teacher, Room, 1, 0).Should().BeFalse();
        service.AnswerQuiz("Bryn", "college_quills", 1, 0).Should().BeFalse();
        service.AnswerQuiz("Bryn", Room, 1, 0).Should().BeTrue();
        service.ControlQuiz(Teacher, false, Room, QuizControlType.Next).Should().Be(CollegeText.WAIT_FOR_ANSWER);
    }

    [Test]
    public void A_question_reveals_at_its_deadline_and_each_change_is_taken_once()
    {
        var service = Create(out _, out var time, out _);
        service.StartQuiz(Teacher, Room, SaveQuiz(service), 20, false, false);
        service.TakeToolChanges(Room).Should().NotBeNull();
        service.TakeToolChanges(Room).Should().BeNull();

        service.ControlQuiz(Teacher, false, Room, QuizControlType.Next);
        var open = service.TakeToolChanges(Room)!;

        open.Tool.Should().Be(CollegeTool.Quiz);
        open.Teacher.Should().Be(Teacher);
        open.Quiz!.Phase.Should().Be(QuizPhase.Open);
        open.Quiz.SecondsLeft.Should().Be(20);
        open.Quiz.Number.Should().Be(1);
        open.Quiz.Choices.Should().Equal("Right", "Wrong", "Also wrong");
        service.TakeToolChanges("college_quills").Should().BeNull();

        time.Later(TimeSpan.FromSeconds(20));
        service.ToolTick(Room, Everyone());

        service.TakeToolChanges(Room)!.Quiz!.Phase.Should().Be(QuizPhase.Revealed);
    }

    [Test]
    public void Quiz_attendance_and_bonus_change_the_rolls()
    {
        var service = Create(out var state, out var time, out var notifier);
        service.StartQuiz(Teacher, Room, SaveQuiz(service), 20, true, true);

        AskAll(
            service,
            time,
            5,
            (number, student) => student switch
            {
                "Bryn"            => 0,
                "Cato" when number <= 3 => 0,
                "Dara"            => 1,
                "Eli" when number <= 2  => 0,
                _                 => null
            });

        service.ControlQuiz(Teacher, false, Room, QuizControlType.Next).Should().Be(CollegeText.LAST_QUESTION);
        service.ControlQuiz(Teacher, false, Room, QuizControlType.End);
        Attend(service, time, [Teacher, ..Students]);

        service.EndClass(Teacher, false).Should().Be(EndResult.Ended);

        service.MarksOf("Bryn").Should().Be(2);
        service.MarksOf("Cato").Should().Be(1);
        service.MarksOf("Dara").Should().Be(1);
        service.MarksOf("Eli").Should().Be(0);
        service.MarksOf(Teacher).Should().Be(1);
        notifier.Verify(n => n.Tell("Eli", CollegeText.QUIZ_ATTENDANCE_FAILED));
        notifier.Verify(n => n.Tell("Bryn", CollegeText.QUIZ_BONUS));
        state.History.Last().Should().BeEquivalentTo(new { Outcome = ClassOutcome.Completed, BonusRolls = 1, ToolFailed = 1 });
    }

    [Test]
    public void A_quiz_with_fewer_than_5_questions_asked_changes_no_rolls()
    {
        var service = Create(out _, out var time, out var notifier);
        service.StartQuiz(Teacher, Room, SaveQuiz(service), 20, true, true);

        AskAll(service, time, 4, (_, student) => student == "Bryn" ? 0 : null);
        service.ControlQuiz(Teacher, false, Room, QuizControlType.End);
        Attend(service, time, [Teacher, ..Students]);
        service.EndClass(Teacher, false);

        service.MarksOf("Bryn").Should().Be(1);
        service.MarksOf("Eli").Should().Be(1);
        notifier.Verify(n => n.Tell(It.IsAny<string>(), CollegeText.QUIZ_ATTENDANCE_FAILED), Times.Never);
        notifier.Verify(n => n.Tell(It.IsAny<string>(), CollegeText.QUIZ_BONUS), Times.Never);
    }

    [Test]
    public void A_failed_tool_check_does_not_sink_the_class()
    {
        var service = Create(out var state, out var time, out _);
        service.StartQuiz(Teacher, Room, SaveQuiz(service), 20, true, false);

        AskAll(service, time, 5, (_, student) => student is "Bryn" or "Cato" ? 0 : null);
        service.ControlQuiz(Teacher, false, Room, QuizControlType.End);
        Attend(service, time, Teacher, "Bryn", "Cato", "Dara");
        service.EndClass(Teacher, false);

        state.History.Last().Outcome.Should().Be(ClassOutcome.Completed);
        service.MarksOf(Teacher).Should().Be(1);
        service.MarksOf("Bryn").Should().Be(1);
        service.MarksOf("Dara").Should().Be(0);
    }

    [Test]
    public void Debate_attendance_and_best_speakers_change_the_rolls()
    {
        var service = Create(out var state, out var time, out var notifier);
        service.StartDebate(Teacher, Room, "Sgrios is needed for balance", 1, true, true);

        foreach (var student in Students.Take(3))
            service.PickDebateSide(student, Room, DebateSide.For).Should().BeTrue();

        time.Later(TimeSpan.FromSeconds(60));
        service.ToolTick(Room, Everyone());

        foreach (var student in Students.Take(3))
        {
            service.RaiseHand(student, Room, true).Should().BeTrue();
            service.ControlDebate(Teacher, false, Room, DebateControlType.GiveFloor, [student]).Should().BeEmpty();
        }

        notifier.Verify(n => n.Tell("Bryn", CollegeText.FloorGiven(1)));
        notifier.Verify(n => n.Tell("Bryn", CollegeText.TURN_OVER));

        service.ControlDebate(Teacher, false, Room, DebateControlType.FinalVote, []).Should().BeEmpty();
        time.Later(TimeSpan.FromSeconds(30));
        service.ToolTick(Room, Everyone());
        service.ControlDebate(Teacher, false, Room, DebateControlType.PickSpeakers, ["Eli"]).Should().Be(CollegeText.PICK_SPEAKERS);
        service.ControlDebate(Teacher, false, Room, DebateControlType.PickSpeakers, ["bryn"]).Should().BeEmpty();

        Attend(service, time, [Teacher, ..Students]);
        service.EndClass(Teacher, false);

        service.MarksOf("Bryn").Should().Be(2);
        service.MarksOf("Cato").Should().Be(1);
        service.MarksOf("Eli").Should().Be(0);
        notifier.Verify(n => n.Tell("Eli", CollegeText.DEBATE_ATTENDANCE_FAILED));
        notifier.Verify(n => n.Tell("Bryn", CollegeText.DEBATE_BONUS));
        state.History.Last().BonusRolls.Should().Be(1);
    }

    [Test]
    public void A_student_gets_at_most_one_bonus_roll()
    {
        var service = Create(out _, out var time, out _);
        service.StartQuiz(Teacher, Room, SaveQuiz(service), 20, false, true);
        AskAll(service, time, 5, (_, student) => student == "Bryn" ? 0 : null);
        service.ControlQuiz(Teacher, false, Room, QuizControlType.End);

        service.StartDebate(Teacher, Room, "Motion", 1, false, true);
        time.Later(TimeSpan.FromSeconds(60));
        service.ToolTick(Room, Everyone());

        foreach (var student in Students.Take(3))
        {
            service.RaiseHand(student, Room, true);
            service.ControlDebate(Teacher, false, Room, DebateControlType.GiveFloor, [student]);
        }

        service.ControlDebate(Teacher, false, Room, DebateControlType.FinalVote, []);
        time.Later(TimeSpan.FromSeconds(30));
        service.ToolTick(Room, Everyone());
        service.ControlDebate(Teacher, false, Room, DebateControlType.PickSpeakers, ["Bryn"]);

        Attend(service, time, [Teacher, ..Students]);
        service.EndClass(Teacher, false);

        service.MarksOf("Bryn").Should().Be(2);
    }

    [Test]
    public void Only_the_floor_holder_and_staff_may_talk_while_someone_speaks()
    {
        var service = Create(out var state, out var time, out _);
        state.Director = "Mira";
        service.StartDebate(Teacher, Room, "Motion", 2, false, false);
        time.Later(TimeSpan.FromSeconds(60));
        service.ToolTick(Room, Everyone());

        service.FloorBlock("Bryn", Room, false).Should().BeNull();

        service.RaiseHand("Cato", Room, true);
        service.ControlDebate(Teacher, false, Room, DebateControlType.GiveFloor, ["Cato"]);

        service.FloorBlock("Bryn", Room, false).Should().Be("Cato");
        service.FloorBlock("Cato", Room, false).Should().BeNull();
        service.FloorBlock(Teacher, Room, false).Should().BeNull();
        service.FloorBlock("Mira", Room, false).Should().BeNull();
        service.FloorBlock("Bryn", Room, true).Should().BeNull();
        service.FloorBlock("Bryn", "college_quills", false).Should().BeNull();
    }

    [Test]
    public void Ending_the_class_closes_the_tool_for_its_room_until_a_new_class_begins()
    {
        var service = Create(out _, out _, out _);
        service.StartQuiz(Teacher, Room, SaveQuiz(service), 20, false, false);
        service.TakeToolChanges(Room);

        service.ForceEnd();

        service.TakeToolChanges("college_quills").Should().BeNull();
        service.TakeToolChanges(Room).Should().BeEquivalentTo(new { Closed = true, Tool = CollegeTool.Quiz });
        service.ToolNow(Room)!.Closed.Should().BeTrue();

        service.StartWalkIn(Teacher, "m0", CollegeSubject.Lore, ClassFormat.Lecture, Room, false, false);

        service.ToolNow(Room).Should().BeNull();
    }
}
```

- [ ] **Step 2: Run the tests to see them fail.** Run the Verify command. Expected: compile errors (`StartQuiz`, `ToolStartResult` and the other names don't exist).

- [ ] **Step 3: Extend the state.** In `CollegeState.cs`, add `using Chaos.DarkAges.Definitions;` at the top. In `CollegeClass`, add after `Prompt`:

```csharp
    public LiveQuiz? Quiz { get; set; }
    public LiveDebate? Debate { get; set; }

    /// <summary>The tool that changed last, so a room is told which window to close.</summary>
    public CollegeTool? LastTool { get; set; }
```

In `CollegeClassRecord`, add after `Won`:

```csharp
    public int BonusRolls { get; set; }
    public int ToolFailed { get; set; }
```

- [ ] **Step 4: Add the texts.** In `CollegeText.cs`, add before the closing brace:

```csharp

    public const string NO_CLASS_HERE = "No class is running here.";
    public const string TOOL_TEACHER_ONLY = "Only the class's Teacher can do that.";
    public const string TOOL_WRONG_ROOM = "Start it from your classroom.";
    public const string TOOL_WRONG_FORMAT = "Quizzes and debates are for lectures and discussions.";
    public const string TOOL_RUNNING = "Finish the quiz or debate first.";
    public const string QUIZ_USED = "This class has had its quiz.";
    public const string DEBATE_USED = "This class has had its debate.";
    public const string TOOL_BAD_SETTINGS = "Those settings are not allowed.";
    public const string EMPTY_MOTION = "Please write a motion.";
    public const string QUIZ_READY = "The quiz is ready. Use your quiz panel to ask the first question.";
    public const string DEBATE_READY = "The debate has begun. Students have 60 seconds to pick a side.";
    public const string NO_QUIZ = "No quiz is running.";
    public const string NO_DEBATE = "No debate is running.";
    public const string WAIT_FOR_ANSWER = "Wait for the answer first.";
    public const string LAST_QUESTION = "That was the last question.";
    public const string NO_HAND = "They no longer have a hand up.";
    public const string FINAL_NOT_NOW = "The final vote can't start now.";
    public const string PICK_SPEAKERS = "Pick up to 3 students who spoke.";
    public const string TURN_OVER = "Your turn is over.";
    public const string DEBATE_BEGUN = "A debate begins! Pick your side.";
    public const string QUIZ_ATTENDANCE_FAILED = "No roll: too few quiz answers.";
    public const string DEBATE_ATTENDANCE_FAILED = "No roll: you missed a debate vote.";
    public const string QUIZ_BONUS = "Bonus roll for your quiz score!";
    public const string DEBATE_BONUS = "Bonus roll for your debate speech!";

    public static string FloorGiven(int minutes) => minutes == 1 ? "You have the floor for 1 minute." : $"You have the floor for {minutes} minutes.";

    /// <summary>Under 45 characters; a very long name falls back to a general line.</summary>
    public static string HasTheFloor(string name)
    {
        var text = $"{name} has the floor.";

        return text.Length <= 45 ? text : "Someone else has the floor.";
    }

    /// <summary>"A quiz begins: {title}", the title cut with "..." to stay under 45 characters.</summary>
    public static string QuizBegun(string title)
    {
        const string PREFIX = "A quiz begins: ";
        var text = PREFIX + title;

        return text.Length <= 45 ? text : PREFIX + title[..(45 - PREFIX.Length - 3)] + "...";
    }
```

- [ ] **Step 5: Add the tool service.** Create `Chaos/Services/College/CollegeService.Tools.cs`:

```csharp
using Chaos.DarkAges.Definitions;

namespace Chaos.Services.College;

public enum ToolStartResult
{
    Started,
    NoClass,
    NotYourClass,
    WrongRoom,
    WrongFormat,
    ToolRunning,
    AlreadyUsed,
    NoSuchQuiz,
    NotReady,
    BadSettings
}

public sealed record ToolStart(ToolStartResult Result, string Message);

/// <summary>The tool running in a class (null when none), and whether each tool has been used.</summary>
public sealed record ToolStatus(CollegeTool? Running, bool QuizUsed, bool DebateUsed);

/// <summary>A class quiz as the service sees it. <see cref="Picks" /> and <see cref="Scores" /> are keyed by lower-case name.</summary>
public sealed record QuizView(
    string Title,
    int Number,
    int Count,
    string Question,
    IReadOnlyList<string> Choices,
    int Right,
    QuizPhase Phase,
    int Seconds,
    int SecondsLeft,
    IReadOnlyDictionary<string, int> Picks,
    IReadOnlyDictionary<string, int> Scores,
    int RightCount,
    int AnsweredCount,
    int OpenAnswers,
    IReadOnlyList<(string Name, int Score)> Leaders);

/// <summary>A class debate as the service sees it. <see cref="Sides" /> and <see cref="HandKeys" /> use lower-case names.</summary>
public sealed record DebateView(
    string Motion,
    DebatePhase Phase,
    int SecondsLeft,
    int TurnMinutes,
    int For,
    int Against,
    int Undecided,
    int OpeningFor,
    int OpeningAgainst,
    int OpeningUndecided,
    string? FloorKey,
    string Floor,
    int FloorSecondsLeft,
    IReadOnlyDictionary<string, DebateSide> Sides,
    IReadOnlyList<string> HandKeys,
    IReadOnlyList<string> Hands,
    IReadOnlyList<string> Speakers,
    DebateResult Result,
    bool Bonus);

/// <summary>What a classroom should show: one running tool, or <see cref="Closed" /> to close <see cref="Tool" />'s window.</summary>
public sealed record ToolView(string Room, string Teacher, CollegeTool Tool, bool Closed, QuizView? Quiz, DebateView? Debate);

public sealed partial class CollegeService
{
    //one class runs at a time, so one counter covers every room; a view is taken once per change
    private long ToolVersion;
    private long SentToolVersion;
    private (string Room, CollegeTool Tool)? PendingToolClose;

    public ToolStatus? ToolStatusOf(string room)
    {
        using var scope = Sync.EnterScope();

        if (ClassInRoomLocked(room) is not { } running)
            return null;

        CollegeTool? active = ActiveQuiz(running) is not null ? CollegeTool.Quiz
            : ActiveDebate(running) is not null ? CollegeTool.Debate
            : null;

        return new ToolStatus(active, running.Quiz is not null, running.Debate is not null);
    }

    public ToolStart StartQuiz(string teacher, string room, string quizId, int seconds, bool attendance, bool bonus)
    {
        using var scope = Sync.EnterScope();

        if (RefuseToolStartLocked(teacher, room, CollegeTool.Quiz) is { } refused)
            return refused;

        if (!CollegeProtocol.QuizSeconds.Contains(seconds))
            return new ToolStart(ToolStartResult.BadSettings, CollegeText.TOOL_BAD_SETTINGS);

        if ((OwnedQuizLocked(teacher, quizId) is null) || Quizzes.Load(quizId) is not { } quiz)
            return new ToolStart(ToolStartResult.NoSuchQuiz, CollegeText.QUIZ_GONE);

        var problem = QuizRules.CheckReady(quiz);

        if (!problem.IsOk)
            return new ToolStart(ToolStartResult.NotReady, CollegeText.QuizProblemText(problem));

        var running = State.RunningClass!;
        running.Quiz = QuizSession.Start(quiz, seconds, attendance, bonus);
        ToolChangedLocked(running, CollegeTool.Quiz);
        SaveLocked();

        return new ToolStart(ToolStartResult.Started, CollegeText.QUIZ_READY);
    }

    public ToolStart StartDebate(string teacher, string room, string motion, int turnMinutes, bool attendance, bool bonus)
    {
        using var scope = Sync.EnterScope();

        if (RefuseToolStartLocked(teacher, room, CollegeTool.Debate) is { } refused)
            return refused;

        var clean = motion.ReplaceLineEndings(" ").Trim();

        if (clean.Length == 0)
            return new ToolStart(ToolStartResult.BadSettings, CollegeText.EMPTY_MOTION);

        if ((clean.Length > CollegeProtocol.MAX_MOTION_CHARS) || !CollegeProtocol.DebateTurnMinutes.Contains(turnMinutes))
            return new ToolStart(ToolStartResult.BadSettings, CollegeText.TOOL_BAD_SETTINGS);

        var running = State.RunningClass!;
        running.Debate = DebateSession.Start(clean, turnMinutes, attendance, bonus, Now);
        ToolChangedLocked(running, CollegeTool.Debate);
        SaveLocked();

        return new ToolStart(ToolStartResult.Started, CollegeText.DEBATE_READY);
    }

    public bool AnswerQuiz(string name, string room, int number, int answer)
    {
        using var scope = Sync.EnterScope();

        if (ClassInRoomLocked(room) is not { } running || IsTeacherOf(running, name) || ActiveQuiz(running) is not { } quiz)
            return false;

        if (!QuizSession.Answer(quiz, name, number, answer, Now))
            return false;

        ToolChangedLocked(running, CollegeTool.Quiz);

        return true;
    }

    /// <summary>Empty when it worked; otherwise the orange-bar text saying why not.</summary>
    public string ControlQuiz(string by, bool isAdmin, string room, QuizControlType control)
    {
        using var scope = Sync.EnterScope();

        if (ClassInRoomLocked(room) is not { } running)
            return CollegeText.NO_CLASS_HERE;

        if (!ControlsLocked(running, by, isAdmin))
            return CollegeText.TOOL_TEACHER_ONLY;

        if (ActiveQuiz(running) is not { } quiz)
            return CollegeText.NO_QUIZ;

        if (control == QuizControlType.End)
        {
            QuizSession.End(quiz);
            ToolChangedLocked(running, CollegeTool.Quiz);
            SaveLocked();

            return string.Empty;
        }

        switch (QuizSession.Next(quiz, Now))
        {
            case QuizStep.Done:
                ToolChangedLocked(running, CollegeTool.Quiz);

                return string.Empty;
            case QuizStep.NotNow:
                return CollegeText.WAIT_FOR_ANSWER;
            default:
                return CollegeText.LAST_QUESTION;
        }
    }

    public bool PickDebateSide(string name, string room, DebateSide side)
    {
        using var scope = Sync.EnterScope();

        if (ClassInRoomLocked(room) is not { } running || IsTeacherOf(running, name) || ActiveDebate(running) is not { } debate)
            return false;

        if (!DebateSession.PickSide(debate, name, side))
            return false;

        ToolChangedLocked(running, CollegeTool.Debate);

        return true;
    }

    public bool RaiseHand(string name, string room, bool raised)
    {
        using var scope = Sync.EnterScope();

        if (ClassInRoomLocked(room) is not { } running || IsTeacherOf(running, name) || ActiveDebate(running) is not { } debate)
            return false;

        if (!DebateSession.Hand(debate, name, raised))
            return false;

        ToolChangedLocked(running, CollegeTool.Debate);

        return true;
    }

    /// <summary>Empty when it worked; otherwise the orange-bar text saying why not.</summary>
    public string ControlDebate(string by, bool isAdmin, string room, DebateControlType control, IReadOnlyList<string> names)
    {
        using var scope = Sync.EnterScope();

        if (ClassInRoomLocked(room) is not { } running)
            return CollegeText.NO_CLASS_HERE;

        if (!ControlsLocked(running, by, isAdmin))
            return CollegeText.TOOL_TEACHER_ONLY;

        if (ActiveDebate(running) is not { } debate)
            return CollegeText.NO_DEBATE;

        var holder = debate.Floor;

        switch (control)
        {
            case DebateControlType.GiveFloor:
                if ((names.Count != 1) || !DebateSession.GiveFloor(debate, names[0], Now))
                    return CollegeText.NO_HAND;

                if ((holder is not null) && (holder != debate.Floor))
                    Notifier.Tell(NameIn(debate, holder), CollegeText.TURN_OVER);

                Notifier.Tell(NameIn(debate, debate.Floor!), CollegeText.FloorGiven(debate.TurnMinutes));

                break;
            case DebateControlType.TakeBack:
                if (!DebateSession.TakeBack(debate))
                    return string.Empty;

                Notifier.Tell(NameIn(debate, holder!), CollegeText.TURN_OVER);

                break;
            case DebateControlType.FinalVote:
                if (!DebateSession.StartFinal(debate, Now))
                    return CollegeText.FINAL_NOT_NOW;

                if (holder is not null)
                    Notifier.Tell(NameIn(debate, holder), CollegeText.TURN_OVER);

                break;
            case DebateControlType.PickSpeakers:
                if (!DebateSession.PickSpeakers(debate, names))
                    return CollegeText.PICK_SPEAKERS;

                SaveLocked();

                break;
            case DebateControlType.End:
                DebateSession.End(debate);
                SaveLocked();

                break;
            default:
                return string.Empty;
        }

        ToolChangedLocked(running, CollegeTool.Debate);

        return string.Empty;
    }

    /// <summary>Called each second by the room's map script, with everyone on the map.</summary>
    public void ToolTick(string room, IReadOnlyCollection<string> namesInRoom)
    {
        using var scope = Sync.EnterScope();

        if (ClassInRoomLocked(room) is not { } running)
            return;

        var now = Now;

        if (ActiveQuiz(running) is { } quiz && QuizSession.Tick(quiz, now))
            ToolChangedLocked(running, CollegeTool.Quiz);

        if (ActiveDebate(running) is not { } debate)
            return;

        var tick = DebateSession.Tick(debate, now, namesInRoom.Select(CollegeState.Key).ToHashSet());

        if (tick.Changed)
            ToolChangedLocked(running, CollegeTool.Debate);

        if (tick.TurnEnded is { } ended)
            Notifier.Tell(NameIn(debate, ended), CollegeText.TURN_OVER);
    }

    /// <summary>The room's view if anything changed since the last one was taken; otherwise null.</summary>
    public ToolView? TakeToolChanges(string room)
    {
        using var scope = Sync.EnterScope();

        if ((ToolVersion == SentToolVersion) || ToolViewLocked(room) is not { } view)
            return null;

        SentToolVersion = ToolVersion;

        return view;
    }

    /// <summary>The room's view now, for someone who just arrived or asked to see their panel again.</summary>
    public ToolView? ToolNow(string room)
    {
        using var scope = Sync.EnterScope();

        return ToolViewLocked(room);
    }

    /// <summary>The floor holder's name when <paramref name="name" /> may not talk on that map, otherwise null.</summary>
    public string? FloorBlock(string name, string mapInstanceId, bool isAdmin)
    {
        using var scope = Sync.EnterScope();

        if (ClassInRoomLocked(mapInstanceId) is not { } running
            || IsTeacherOf(running, name)
            || CollegeRoles.CanModerate(State, name, isAdmin)
            || ActiveDebate(running) is not { } debate)
            return null;

        return DebateSession.Blocks(debate, name);
    }

    private ToolStart? RefuseToolStartLocked(string teacher, string room, CollegeTool tool)
    {
        if (State.RunningClass is not { } running)
            return new ToolStart(ToolStartResult.NoClass, CollegeText.NO_CLASS_HERE);

        if (!IsTeacherOf(running, teacher))
            return new ToolStart(ToolStartResult.NotYourClass, CollegeText.TOOL_TEACHER_ONLY);

        if (!running.Room.Equals(room, StringComparison.OrdinalIgnoreCase))
            return new ToolStart(ToolStartResult.WrongRoom, CollegeText.TOOL_WRONG_ROOM);

        if (running.Format == ClassFormat.Activity)
            return new ToolStart(ToolStartResult.WrongFormat, CollegeText.TOOL_WRONG_FORMAT);

        if ((ActiveQuiz(running) is not null) || (ActiveDebate(running) is not null))
            return new ToolStart(ToolStartResult.ToolRunning, CollegeText.TOOL_RUNNING);

        if (tool == CollegeTool.Quiz ? running.Quiz is not null : running.Debate is not null)
            return new ToolStart(ToolStartResult.AlreadyUsed, tool == CollegeTool.Quiz ? CollegeText.QUIZ_USED : CollegeText.DEBATE_USED);

        return null;
    }

    private ToolView? ToolViewLocked(string room)
    {
        if (ClassInRoomLocked(room) is { } running)
        {
            if (ActiveQuiz(running) is { } quiz)
                return new ToolView(running.Room, running.Teacher, CollegeTool.Quiz, false, QuizViewOf(quiz, Now), null);

            if (ActiveDebate(running) is { } debate)
                return new ToolView(running.Room, running.Teacher, CollegeTool.Debate, false, null, DebateViewOf(debate, Now));

            return running.LastTool is { } last ? new ToolView(running.Room, running.Teacher, last, true, null, null) : null;
        }

        return PendingToolClose is { } close && close.Room.Equals(room, StringComparison.OrdinalIgnoreCase)
            ? new ToolView(close.Room, string.Empty, close.Tool, true, null, null)
            : null;
    }

    private static QuizView QuizViewOf(LiveQuiz quiz, DateTime now)
    {
        var question = quiz.Current >= 0 ? quiz.Questions[quiz.Current] : null;

        return new QuizView(
            quiz.Title,
            quiz.Current + 1,
            quiz.Questions.Count,
            question?.Text ?? string.Empty,
            question?.Answers.ToList() ?? [],
            question?.Right ?? -1,
            quiz.Phase,
            quiz.Seconds,
            quiz.Phase == QuizPhase.Open ? SecondsUntil(quiz.DeadlineUtc, now) : 0,
            new Dictionary<string, int>(quiz.Answers),
            new Dictionary<string, int>(quiz.Scores),
            quiz.LastRight,
            quiz.LastAnswered,
            quiz.Answers.Count,
            QuizSession.Leaders(quiz));
    }

    private static DebateView DebateViewOf(LiveDebate debate, DateTime now)
    {
        var (forCount, againstCount, undecided) = DebateSession.Count(debate);

        return new DebateView(
            debate.Motion,
            debate.Phase,
            debate.Phase is DebatePhase.Opening or DebatePhase.Final ? SecondsUntil(debate.DeadlineUtc, now) : 0,
            debate.TurnMinutes,
            forCount,
            againstCount,
            undecided,
            debate.OpeningFor,
            debate.OpeningAgainst,
            debate.OpeningUndecided,
            debate.Floor,
            debate.Floor is { } holder ? NameIn(debate, holder) : string.Empty,
            debate.Floor is not null ? SecondsUntil(debate.FloorUntilUtc, now) : 0,
            new Dictionary<string, DebateSide>(debate.Sides),
            debate.Hands.ToList(),
            debate.Hands.Select(key => NameIn(debate, key)).ToList(),
            debate.Speakers.Select(key => NameIn(debate, key)).ToList(),
            debate.Result,
            debate.Bonus);
    }

    private static int SecondsUntil(DateTime deadline, DateTime now) => Math.Max(0, (int)Math.Ceiling((deadline - now).TotalSeconds));

    private static string NameIn(LiveDebate debate, string key) => debate.Names.GetValueOrDefault(key, key);

    private CollegeClass? ClassInRoomLocked(string room)
        => State.RunningClass is { } running && running.Room.Equals(room, StringComparison.OrdinalIgnoreCase) ? running : null;

    private static bool IsTeacherOf(CollegeClass running, string name) => running.Teacher.Equals(name, StringComparison.OrdinalIgnoreCase);

    private bool ControlsLocked(CollegeClass running, string name, bool isAdmin)
        => IsTeacherOf(running, name) || CollegeRoles.CanModerate(State, name, isAdmin);

    private static LiveQuiz? ActiveQuiz(CollegeClass running) => running.Quiz is { Phase: not QuizPhase.Ended } quiz ? quiz : null;

    private static LiveDebate? ActiveDebate(CollegeClass running)
        => running.Debate is { Phase: not DebatePhase.Ended } debate ? debate : null;

    private void ToolChangedLocked(CollegeClass running, CollegeTool tool)
    {
        running.LastTool = tool;
        ToolVersion++;
    }

    /// <summary>Null when the student passes every attendance check that counts, otherwise the text saying which one failed.</summary>
    private static string? FailedToolCheck(LiveQuiz? quiz, LiveDebate? debate, string name)
    {
        if (quiz is { Attendance: true } && QuizSession.GuardMet(quiz) && !QuizSession.PassedAttendance(quiz, name))
            return CollegeText.QUIZ_ATTENDANCE_FAILED;

        if (debate is { Attendance: true } && DebateSession.GuardMet(debate) && !DebateSession.PassedAttendance(debate, name))
            return CollegeText.DEBATE_ATTENDANCE_FAILED;

        return null;
    }

    /// <summary>The bonus text when the student earned a second roll, otherwise null. One bonus at most, whichever tool gave it.</summary>
    private static string? BonusFor(LiveQuiz? quiz, LiveDebate? debate, IReadOnlyList<string> topScorers, string name)
    {
        var key = CollegeState.Key(name);

        if (quiz is { Bonus: true } && QuizSession.GuardMet(quiz) && topScorers.Contains(key))
            return CollegeText.QUIZ_BONUS;

        if (debate is { Bonus: true } && DebateSession.GuardMet(debate) && debate.BestSpeakers.Contains(key))
            return CollegeText.DEBATE_BONUS;

        return null;
    }
}
```

- [ ] **Step 6: Apply the checks and bonus at the end of a class.** In `CollegeService.cs`:

  In `BeginLocked`, add as its first line:

```csharp
        PendingToolClose = null;
```

  Replace the whole `FinishLocked` method with:

```csharp
    private ClassResult FinishLocked()
    {
        var running = State.RunningClass!;
        var result = ClassSession.Finish(running, Now, Options);
        var won = new List<string>();
        var notifications = new List<Action>();
        var bonusRolls = 0;
        var toolFailed = 0;

        //a tool still open when the class ends is closed first, so an unrevealed question is never scored
        if (running.Quiz is { } quiz)
            QuizSession.End(quiz);

        if (running.Debate is { Phase: not DebatePhase.Ended } debate)
            DebateSession.End(debate);

        if (result.Outcome == ClassOutcome.Completed)
        {
            IReadOnlyList<string> topScorers = running.Quiz is { } finished ? QuizSession.TopScorers(finished) : [];

            foreach (var name in result.Qualified)
            {
                if (FailedToolCheck(running.Quiz, running.Debate, name) is { } failed)
                {
                    toolFailed++;
                    notifications.Add(() => Notifier.Tell(name, failed));

                    continue;
                }

                RollLocked(name, won, notifications);

                if (BonusFor(running.Quiz, running.Debate, topScorers, name) is { } bonus)
                {
                    bonusRolls++;
                    notifications.Add(() => Notifier.Tell(name, bonus));
                    RollLocked(name, won, notifications);
                }
            }

            Ledger.Give(State, running.Teacher, 1);
            var teacherMarks = State.StudentFor(running.Teacher).Marks;
            notifications.Add(() => Notifier.RefreshEducatedMark(running.Teacher, teacherMarks));
            notifications.Add(() => Notifier.Tell(running.Teacher, CollegeText.TEACHER_PAID));
        } else
        {
            var message = CollegeText.Outcome(result.Outcome);
            notifications.Add(() => Notifier.Tell(running.Teacher, message));

            foreach (var presence in running.Presence.Values)
            {
                var presenceName = presence.Name;
                notifications.Add(() => Notifier.Tell(presenceName, message));
            }
        }

        State.History.Add(
            new CollegeClassRecord
            {
                Teacher = running.Teacher,
                Subject = running.Subject,
                StartedUtc = running.StartedUtc,
                Minutes = result.Minutes,
                Outcome = result.Outcome,
                Qualified = result.Qualified.ToList(),
                Won = won,
                BonusRolls = bonusRolls,
                ToolFailed = toolFailed
            });

        if (State.History.Count > Options.HistoryLength)
            State.History.RemoveRange(0, State.History.Count - Options.HistoryLength);

        if (running.LastTool is { } lastTool)
        {
            PendingToolClose = (running.Room, lastTool);
            ToolVersion++;
        }

        State.RunningClass = null;
        SaveLocked();
        Logger.LogInformation("College class by {Teacher} ended {Outcome} after {Minutes}m", running.Teacher, result.Outcome, result.Minutes);

        //State is final and saved by now, so a failing notification must not undo or repeat the payout
        foreach (var notify in notifications)
        {
            try
            {
                notify();
            } catch (Exception e)
            {
                Logger.LogError(e, "College notification failed after class by {Teacher}", running.Teacher);
            }
        }

        return result;
    }

    private void RollLocked(string name, List<string> won, List<Action> notifications)
    {
        var success = Ledger.Roll(State, name, Random);

        if (success)
        {
            won.Add(name);
            var marks = State.StudentFor(name).Marks;
            notifications.Add(() => Notifier.RefreshEducatedMark(name, marks));
        }

        var rollText = success ? CollegeText.ROLL_WON : CollegeText.ROLL_MISSED;
        notifications.Add(() => Notifier.Tell(name, rollText));
    }
```

  Add `using Chaos.DarkAges.Definitions;` to `CollegeService.cs` if it isn't there.

- [ ] **Step 7: Run the tests to see them pass.** Run the Verify commands. Expected: `CollegeToolServiceTests` all pass, and every existing College test still passes (`--treenode-filter "/*/*/*College*/*"` plus `QuizSessionTests`, `DebateSessionTests`).

```json:metadata
{"files": ["Chaos/Services/College/CollegeService.Tools.cs", "Chaos/Services/College/CollegeService.cs", "Chaos/Services/College/CollegeState.cs", "Chaos/Services/College/CollegeText.cs", "Tests/Chaos.Tests/College/CollegeToolServiceTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/CollegeToolServiceTests/*\"", "acceptanceCriteria": ["tools start only for the class Teacher, in the room, in Lecture/Discussion, with allowed settings, one tool at a time, once per class; quiz must be ready", "only students in the room answer, pick sides, raise hands; the Teacher can't", "controls need Teacher/Director/admin in the room", "ToolTick reveals at the deadline and runs debate timers; turn over and floor given are told", "TakeToolChanges once per change; closed view after class end until a new class", "attendance failure skips the roll and tells why; top scorers and best speakers roll twice; one bonus max; 3-student rule and Teacher mark unchanged; history counts", "FloorBlock names the holder for students only, never for holder, Teacher, Director, admins or another map"], "modelTier": "standard"}
```

---

### Task 5: Delivery: panel messages, room script, the floor rule (server repo)

**Goal:** Quiz editor actions and class actions reach the service. Tool views go out to everyone in the classroom: at once after a player's action, each second from the room script, and to each arrival. Say and shout are refused while someone else holds the floor.

**Files:**
- Modify: `Chaos/Services/College/CollegePanel.cs`
- Modify: `Chaos/Scripting/MapScripts/Temuair/College/CollegeRoomScript.cs`
- Modify: `Chaos/Services/Servers/WorldServer.cs` (`OnPublicMessage`'s inner handler, about line 1555)
- Test: new `Tests/Chaos.Tests/College/CollegeToolMappingTests.cs`

**Acceptance Criteria:**
- [ ] `QuizInfo` hides the right answer from a student until the reveal and always shows it to the Teacher; shows the Teacher the live answered count and a student none; fills my answer, my score, the top 3 and the student count
- [ ] `DebateInfo` gives my side and my hand, and lists hands and speakers only for the Teacher
- [ ] `DebateMarks` lists only people with a side, with the floor holder flagged
- [ ] `ToQuiz` / `ToDraftInfo` turn `NO_ANSWER` into -1 and back
- [ ] The editor actions open the list, open one quiz, save, copy and delete, and answer with `QuizResult`; a throttled save is answered with "Too fast."
- [ ] Class actions skip the save throttle, are limited to 120 a minute, and are followed by a room broadcast
- [ ] The room script ticks the tools each second, broadcasts changes, and sends the current state to each arrival
- [ ] A Normal or Shout message from a blocked student is refused with `{Name} has the floor.`; commands still work

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/CollegeToolMappingTests/*"` -> all pass; `dotnet build Chaos.slnx` -> Build succeeded

**Steps:**

- [ ] **Step 1: Write the failing mapping tests.** Create `Tests/Chaos.Tests/College/CollegeToolMappingTests.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using Chaos.Services.College;
using FluentAssertions;

namespace Chaos.Tests.College;

public sealed class CollegeToolMappingTests
{
    private static QuizView Quiz(QuizPhase phase)
        => new(
            "Mileth history",
            3,
            8,
            "Who founded the temple?",
            ["Danaan", "Sgrios", "The first Aislings"],
            2,
            phase,
            20,
            phase == QuizPhase.Open ? 14 : 0,
            new Dictionary<string, int> { ["bryn"] = 1, ["cato"] = 2 },
            new Dictionary<string, int> { ["bryn"] = 2, ["cato"] = 3 },
            1,
            2,
            2,
            [("Cato", 3), ("Bryn", 2)]);

    private static DebateView Debate()
        => new(
            "Sgrios is needed",
            DebatePhase.Floor,
            0,
            2,
            2,
            1,
            1,
            1,
            1,
            2,
            "cato",
            "Cato",
            72,
            new Dictionary<string, DebateSide> { ["bryn"] = DebateSide.For, ["cato"] = DebateSide.Against },
            ["bryn"],
            ["Bryn"],
            ["Cato"],
            DebateResult.None,
            true);

    [Test]
    public void A_student_sees_the_right_answer_only_after_the_reveal()
    {
        var open = CollegePanel.QuizInfo(Quiz(QuizPhase.Open), "Bryn", false, 9);

        open.Right.Should().Be(CollegeProtocol.NO_ANSWER);
        open.MyAnswer.Should().Be(1);
        open.AnsweredCount.Should().Be(0);
        open.RightCount.Should().Be(0);
        open.SecondsLeft.Should().Be(14);
        open.Students.Should().Be(9);

        var revealed = CollegePanel.QuizInfo(Quiz(QuizPhase.Revealed), "bryn", false, 9);

        revealed.Right.Should().Be(2);
        revealed.RightCount.Should().Be(1);
        revealed.AnsweredCount.Should().Be(2);
        revealed.MyScore.Should().Be(2);
        revealed.Top.Select(t => (t.Name, (int)t.Score)).Should().Equal(("Cato", 3), ("Bryn", 2));
    }

    [Test]
    public void The_teacher_sees_the_right_answer_and_the_live_count()
    {
        var open = CollegePanel.QuizInfo(Quiz(QuizPhase.Open), "Aroha", true, 9);

        open.Right.Should().Be(2);
        open.AnsweredCount.Should().Be(2);
        open.MyAnswer.Should().Be(CollegeProtocol.NO_ANSWER);
        open.Answers.Should().Equal("Danaan", "Sgrios", "The first Aislings");
    }

    [Test]
    public void Hands_and_speakers_go_only_to_the_teacher()
    {
        var student = CollegePanel.DebateInfo(Debate(), "Bryn", false);

        student.MySide.Should().Be(DebateSide.For);
        student.MyHand.Should().BeTrue();
        student.Floor.Should().Be("Cato");
        student.FloorSecondsLeft.Should().Be(72);
        student.Hands.Should().BeEmpty();
        student.Speakers.Should().BeEmpty();
        student.IsTeacher.Should().BeFalse();

        var teacher = CollegePanel.DebateInfo(Debate(), "Aroha", true);

        teacher.IsTeacher.Should().BeTrue();
        teacher.MySide.Should().Be(DebateSide.None);
        teacher.Hands.Should().Equal("Bryn");
        teacher.Speakers.Should().Equal("Cato");
        (teacher.For, teacher.Against, teacher.Undecided).Should().Be(((byte)2, (byte)1, (byte)1));
        (teacher.OpeningFor, teacher.OpeningAgainst, teacher.OpeningUndecided).Should().Be(((byte)1, (byte)1, (byte)2));
    }

    [Test]
    public void Markers_cover_only_people_with_a_side()
    {
        var marks = CollegePanel.DebateMarks(Debate(), [(1u, "Bryn"), (2u, "Cato"), (3u, "Aroha")]);

        marks.Should().BeEquivalentTo(
            [
                new CollegeDebateMarkInfo { EntityId = 1, Side = DebateSide.For, Floor = false },
                new CollegeDebateMarkInfo { EntityId = 2, Side = DebateSide.Against, Floor = true }
            ]);
    }

    [Test]
    public void A_quiz_draft_maps_no_answer_to_minus_one_and_back()
    {
        var info = new CollegeQuizDraftInfo
        {
            Title = "T",
            Questions =
            [
                new CollegeQuizQuestionInfo { Text = "A?", Answers = ["x", "y"], Right = 1 },
                new CollegeQuizQuestionInfo { Text = "B?", Answers = ["x"], Right = CollegeProtocol.NO_ANSWER }
            ]
        };

        var quiz = CollegePanel.ToQuiz(info);

        quiz.Questions.Select(q => q.Right).Should().Equal(1, -1);
        CollegePanel.ToDraftInfo(quiz).Should().BeEquivalentTo(info);
    }

    [Test]
    public void Quiz_rows_carry_id_title_and_count()
        => CollegePanel.QuizRows([new CollegeQuizIndex { Id = "abc", Title = "T", QuestionCount = 7 }])
                       .Should().BeEquivalentTo([new CollegeQuizRowInfo { Id = "abc", Title = "T", Questions = 7 }]);
}
```

- [ ] **Step 2: Run the tests to see them fail.** Run the Verify command. Expected: compile errors (`CollegePanel.QuizInfo` and the others don't exist).

- [ ] **Step 3: Add the mapping and the editor messages to the panel.** In `CollegePanel.cs`:

  Add the usings `using Chaos.Collections;` (for `MapInstance`) if missing.

  Add a field after `Throttle`:

```csharp
    //answers, sides and hands are cheap and must not be dropped by the 250 ms save throttle
    private const int CLASS_ACTIONS_PER_MINUTE = 120;
    private readonly CollegeFetchLimit ClassActions = new(CLASS_ACTIONS_PER_MINUTE);
```

  Add after `OpenPiece` (in the "opening windows" region):

```csharp
    public void OpenQuizzes(Aisling viewer)
    {
        if (!college.MayKeepQuizzes(viewer.Name, viewer.IsKnight, viewer.IsAdmin))
        {
            viewer.SendOrangeBarMessage(CollegeText.QUIZ_NOT_ALLOWED);

            return;
        }

        viewer.Client.SendCollegeDisplay(
            new CollegeDisplayArgs
            {
                Type = CollegeDisplayType.QuizList,
                QuizRows = QuizRows(college.QuizzesOf(viewer.Name))
            });
    }

    /// <summary>Sends the room's quiz or debate to one player: someone who just arrived, or a Teacher who closed their panel.</summary>
    public void SendToolTo(Aisling viewer)
    {
        if (college.ToolNow(viewer.MapInstance.InstanceId) is { } view)
            SendTool([viewer], view, viewer.MapInstance, true);
    }

    /// <summary>Sends the room's quiz or debate to everyone on the map if it changed. Call only under that map's synchronization.</summary>
    public void BroadcastTool(MapInstance room)
    {
        if (college.TakeToolChanges(room.InstanceId) is { } view)
            SendTool(room.GetEntities<Aisling>().ToList(), view, room, false);
    }

    /// <summary>The floor holder's name when <paramref name="speaker" /> may not say or shout, otherwise null.</summary>
    public string? FloorHolder(Aisling speaker) => college.FloorBlock(speaker.Name, speaker.MapInstance.InstanceId, speaker.IsAdmin);
```

  In `Handle`, add to the first `switch` (the one that returns before the throttle), after the `TuneFetch` case:

```csharp
            case CollegeActionType.QuizAnswer:
            case CollegeActionType.QuizControl:
            case CollegeActionType.DebateSide:
            case CollegeActionType.DebateHand:
            case CollegeActionType.DebateControl:
                HandleClassTool(viewer, args);

                return;
```

  In the throttled branch, change

```csharp
            if (isWrite)
                SendResult(viewer, new SaveOutcome(CollegeWriterResult.Refused, CollegeText.TOO_FAST), mode, subject);

            return;
```

  to

```csharp
            if (isWrite)
                SendResult(viewer, new SaveOutcome(CollegeWriterResult.Refused, CollegeText.TOO_FAST), mode, subject);
            else if (args.Type == CollegeActionType.QuizSave)
                SendQuizResult(viewer, new QuizOutcome(QuizResultCode.Refused, CollegeText.TOO_FAST));

            return;
```

  In the second `switch`, add after the `ShowToClass` case:

```csharp
            case CollegeActionType.QuizOpen:
                OpenQuiz(viewer, args.QuizId);

                break;
            case CollegeActionType.QuizSave when args.QuizDraft is not null:
                SendQuizResult(viewer, college.SaveQuiz(viewer.Name, viewer.IsKnight, viewer.IsAdmin, args.QuizId, ToQuiz(args.QuizDraft)));

                break;
            case CollegeActionType.QuizDelete:
                SendQuizResult(viewer, college.DeleteQuiz(viewer.Name, viewer.IsKnight, viewer.IsAdmin, args.QuizId));
                OpenQuizzes(viewer);

                break;
            case CollegeActionType.QuizCopy:
                SendQuizResult(viewer, college.CopyQuiz(viewer.Name, viewer.IsKnight, viewer.IsAdmin, args.QuizId));
                OpenQuizzes(viewer);

                break;
```

  Add these private methods after `ShowToClass`:

```csharp
    private void OpenQuiz(Aisling viewer, string id)
    {
        if (id.Length == 0)
        {
            OpenQuizzes(viewer);

            return;
        }

        if (college.LoadQuiz(viewer.Name, viewer.IsKnight, viewer.IsAdmin, id) is { } quiz)
            viewer.Client.SendCollegeDisplay(
                new CollegeDisplayArgs
                {
                    Type = CollegeDisplayType.QuizEdit,
                    QuizId = id,
                    QuizDraft = ToDraftInfo(quiz)
                });
        else
            SendQuizResult(viewer, new QuizOutcome(QuizResultCode.Refused, CollegeText.QUIZ_GONE));
    }

    private static void SendQuizResult(Aisling viewer, QuizOutcome outcome)
        => viewer.Client.SendCollegeDisplay(
            new CollegeDisplayArgs
            {
                Type = CollegeDisplayType.QuizResult,
                QuizResult = outcome.Result,
                Message = outcome.Message,
                QuizId = outcome.Id
            });

    private void HandleClassTool(Aisling viewer, CollegeActionArgs args)
    {
        if (!ClassActions.TryTake(viewer.Name, Now))
            return;

        var room = viewer.MapInstance.InstanceId;
        var message = string.Empty;

        switch (args.Type)
        {
            case CollegeActionType.QuizAnswer:
                college.AnswerQuiz(viewer.Name, room, args.Number, args.Answer);

                break;
            case CollegeActionType.QuizControl:
                message = college.ControlQuiz(viewer.Name, viewer.IsAdmin, room, args.QuizControl);

                break;
            case CollegeActionType.DebateSide:
                college.PickDebateSide(viewer.Name, room, args.Side);

                break;
            case CollegeActionType.DebateHand:
                college.RaiseHand(viewer.Name, room, args.Raised);

                break;
            case CollegeActionType.DebateControl:
                message = college.ControlDebate(viewer.Name, viewer.IsAdmin, room, args.DebateControl, args.Names);

                break;
        }

        if (message.Length > 0)
            viewer.SendOrangeBarMessage(message);

        //the packet handler runs under the sender's map synchronization, so this map's aislings can be read here
        BroadcastTool(viewer.MapInstance);
    }

    /// <summary><paramref name="reopen" /> shows the window again for a player who closed it.</summary>
    private void SendTool(IReadOnlyList<Aisling> viewers, ToolView view, MapInstance map, bool reopen)
    {
        if (view.Closed)
        {
            foreach (var viewer in viewers)
                viewer.Client.SendCollegeDisplay(new CollegeDisplayArgs { Type = CollegeDisplayType.ToolClosed, Tool = view.Tool });

            return;
        }

        var everyone = map.GetEntities<Aisling>().ToList();
        var students = everyone.Count(a => !a.Name.Equals(view.Teacher, StringComparison.OrdinalIgnoreCase));
        List<CollegeDebateMarkInfo> marks = view.Debate is { } debateView ? DebateMarks(debateView, everyone.Select(a => (a.Id, a.Name))) : [];

        foreach (var viewer in viewers)
        {
            var isTeacher = viewer.Name.Equals(view.Teacher, StringComparison.OrdinalIgnoreCase);

            if (view.Quiz is { } quiz)
                viewer.Client.SendCollegeDisplay(
                    new CollegeDisplayArgs
                    {
                        Type = isTeacher ? CollegeDisplayType.QuizTeacher : CollegeDisplayType.QuizCard,
                        QuizView = QuizInfo(quiz, viewer.Name, isTeacher, students),
                        Reopen = reopen
                    });

            if (view.Debate is { } debate)
            {
                viewer.Client.SendCollegeDisplay(
                    new CollegeDisplayArgs
                    {
                        Type = CollegeDisplayType.DebateState,
                        Debate = DebateInfo(debate, viewer.Name, isTeacher),
                        Reopen = reopen
                    });

                viewer.Client.SendCollegeDisplay(new CollegeDisplayArgs { Type = CollegeDisplayType.DebateMarks, DebateMarks = marks });
            }
        }
    }
```

  Add these public static mapping methods after `HandInRows`:

```csharp
    public static CollegeQuiz ToQuiz(CollegeQuizDraftInfo info)
        => new()
        {
            Title = info.Title,
            Questions = info.Questions
                            .Select(
                                q => new QuizQuestion
                                {
                                    Text = q.Text,
                                    Answers = q.Answers.ToList(),
                                    Right = q.Right == CollegeProtocol.NO_ANSWER ? -1 : q.Right
                                })
                            .ToList()
        };

    public static CollegeQuizDraftInfo ToDraftInfo(CollegeQuiz quiz)
        => new()
        {
            Title = quiz.Title,
            Questions = quiz.Questions
                            .Select(
                                q => new CollegeQuizQuestionInfo
                                {
                                    Text = q.Text,
                                    Answers = q.Answers.ToList(),
                                    Right = q.Right is >= 0 and < CollegeProtocol.NO_ANSWER ? (byte)q.Right : CollegeProtocol.NO_ANSWER
                                })
                            .ToList()
        };

    public static List<CollegeQuizRowInfo> QuizRows(IReadOnlyList<CollegeQuizIndex> quizzes)
        => quizzes.Select(q => new CollegeQuizRowInfo { Id = q.Id, Title = q.Title, Questions = Byte(q.QuestionCount) }).ToList();

    /// <summary>A student's card gets the right answer only once revealed; the Teacher's panel always, with the live answered count.</summary>
    public static CollegeQuizInfo QuizInfo(QuizView view, string viewer, bool isTeacher, int students)
    {
        var key = CollegeState.Key(viewer);
        var revealed = view.Phase == QuizPhase.Revealed;

        return new CollegeQuizInfo
        {
            Title = view.Title,
            Number = Byte(view.Number),
            Count = Byte(view.Count),
            Question = view.Question,
            Answers = view.Choices.ToList(),
            Seconds = Byte(view.Seconds),
            SecondsLeft = Byte(view.SecondsLeft),
            Phase = view.Phase,
            MyAnswer = view.Picks.TryGetValue(key, out var pick) ? Byte(pick) : CollegeProtocol.NO_ANSWER,
            Right = (isTeacher || revealed) && (view.Right >= 0) ? Byte(view.Right) : CollegeProtocol.NO_ANSWER,
            RightCount = revealed ? Byte(view.RightCount) : (byte)0,
            AnsweredCount = Byte(revealed ? view.AnsweredCount : isTeacher ? view.OpenAnswers : 0),
            MyScore = Byte(view.Scores.GetValueOrDefault(key)),
            Students = Byte(students),
            Top = view.Leaders.Select(l => new CollegeScoreInfo { Name = l.Name, Score = Byte(l.Score) }).ToList()
        };
    }

    /// <summary>Only the Teacher's copy lists the raised hands and the speakers.</summary>
    public static CollegeDebateInfo DebateInfo(DebateView view, string viewer, bool isTeacher)
    {
        var key = CollegeState.Key(viewer);

        return new CollegeDebateInfo
        {
            Motion = view.Motion,
            Phase = view.Phase,
            SecondsLeft = Byte(view.SecondsLeft),
            TurnMinutes = Byte(view.TurnMinutes),
            For = Byte(view.For),
            Against = Byte(view.Against),
            Undecided = Byte(view.Undecided),
            OpeningFor = Byte(view.OpeningFor),
            OpeningAgainst = Byte(view.OpeningAgainst),
            OpeningUndecided = Byte(view.OpeningUndecided),
            Floor = view.Floor,
            FloorSecondsLeft = (ushort)Math.Clamp(view.FloorSecondsLeft, 0, ushort.MaxValue),
            MySide = view.Sides.GetValueOrDefault(key, DebateSide.None),
            MyHand = view.HandKeys.Contains(key),
            Result = view.Result,
            Bonus = view.Bonus,
            IsTeacher = isTeacher,
            Hands = isTeacher ? view.Hands.ToList() : [],
            Speakers = isTeacher ? view.Speakers.ToList() : []
        };
    }

    public static List<CollegeDebateMarkInfo> DebateMarks(DebateView view, IEnumerable<(uint Id, string Name)> people)
        => people.Select(p => (p.Id, Key: CollegeState.Key(p.Name)))
                 .Where(p => view.Sides.ContainsKey(p.Key))
                 .Select(p => new CollegeDebateMarkInfo { EntityId = p.Id, Side = view.Sides[p.Key], Floor = p.Key == view.FloorKey })
                 .ToList();

    private static byte Byte(int value) => (byte)Math.Clamp(value, 0, byte.MaxValue);
```

- [ ] **Step 4: Tick tools in the room script.** Replace the body of `Chaos/Scripting/MapScripts/Temuair/College/CollegeRoomScript.cs` with:

```csharp
#region
using Chaos.Collections;
using Chaos.Models.World;
using Chaos.Models.World.Abstractions;
using Chaos.Scripting.MapScripts.Abstractions;
using Chaos.Services.College;
using Chaos.Time;
using Chaos.Time.Abstractions;
using Microsoft.Extensions.Options;
#endregion

namespace Chaos.Scripting.MapScripts.Temuair.College;

/// <summary>
///     Reports who is in a College subject room every tick, runs the class quiz or debate's timers each second, and
///     sends the room the tool's changes. Everything runs on the map's own update, so reading the map is thread-safe.
/// </summary>
public sealed class CollegeRoomScript : MapScriptBase
{
    private readonly CollegeService College;
    private readonly CollegePanel Panel;
    private readonly IIntervalTimer SampleTimer;
    private readonly IIntervalTimer ToolTimer = new IntervalTimer(TimeSpan.FromSeconds(1), false);

    public CollegeRoomScript(MapInstance subject, CollegeService college, CollegePanel panel, IOptions<CollegeOptions> options)
        : base(subject)
    {
        College = college;
        Panel = panel;
        SampleTimer = new IntervalTimer(TimeSpan.FromSeconds(options.Value.TickSeconds), false);
    }

    public override void OnEntered(Creature creature)
    {
        if (creature is Aisling aisling)
            Panel.SendToolTo(aisling);
    }

    public override void Update(TimeSpan delta)
    {
        ToolTimer.Update(delta);

        if (ToolTimer.IntervalElapsed)
        {
            College.ToolTick(Subject.InstanceId, Subject.GetEntities<Aisling>().Select(a => a.Name).ToList());
            Panel.BroadcastTool(Subject);
        }

        SampleTimer.Update(delta);

        if (!SampleTimer.IntervalElapsed)
            return;

        var samples = Subject.GetEntities<Aisling>()
                             .Select(a => new PresenceSample(a.Name, MachineKeys.For(a.Client.LoginId1, a.Client.RemoteIp.ToString())))
                             .ToList();

        College.RecordPresence(Subject.InstanceId, samples);
    }
}
```


- [ ] **Step 5: Add the floor rule.** In `Chaos/Services/Servers/WorldServer.cs`, inside `InnerOnPublicMessage`, add right after the `if (CommandInterceptor.IsCommand(localArgs.Message)) { ... }` block and before `var filterResult = ...`:

```csharp
            if ((localArgs.PublicMessageType is PublicMessageType.Normal or PublicMessageType.Shout)
                && CollegePanel.FloorHolder(localClient.Aisling) is { } holder)
            {
                localClient.SendServerMessage(ServerMessageType.OrangeBar1, CollegeText.HasTheFloor(holder));

                return;
            }
```

  Add `using Chaos.Services.College;` at the top if it isn't there.

- [ ] **Step 6: Run the tests and build.** Run the Verify commands. Expected: `CollegeToolMappingTests` pass; the solution builds.

```json:metadata
{"files": ["Chaos/Services/College/CollegePanel.cs", "Chaos/Scripting/MapScripts/Temuair/College/CollegeRoomScript.cs", "Chaos/Services/Servers/WorldServer.cs", "Tests/Chaos.Tests/College/CollegeToolMappingTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/CollegeToolMappingTests/*\"", "acceptanceCriteria": ["QuizInfo hides the right answer from students until revealed, shows the Teacher it and the live count; fills my answer, score, top 3, students", "DebateInfo gives my side and hand; hands/speakers only to the Teacher", "DebateMarks only for people with a side, floor flagged", "ToQuiz/ToDraftInfo map NO_ANSWER <-> -1", "editor actions answered with QuizResult; throttled save gets Too fast", "class actions skip the throttle, 120/min limit, then room broadcast", "room script ticks each second, broadcasts, sends state to arrivals", "blocked say/shout refused with '{Name} has the floor.'; commands still work"], "modelTier": "standard"}
```

---
### Task 6: Lectern and Registrar dialogs, Unora templates (server and Unora repos)

**Goal:** At the lectern, the class's Teacher starts a quiz or a debate through short dialog chains, and anyone running a tool can bring its panel back. "My quizzes" opens the editor from the lectern and the Registrar.

**Files:**
- Modify (server): `Chaos/Scripting/DialogScripts/Temuair/College/CollegeLecternScript.cs`
- Modify (server): `Chaos/Scripting/DialogScripts/Temuair/College/CollegeRegistrarScript.cs`
- Create (Unora): in `Data/Configuration/Templates/Dialogs/Temauir/mileth/college/`: `college_lectern_quiz_pick.json`, `college_lectern_quiz_time.json`, `college_lectern_quiz_attend.json`, `college_lectern_quiz_bonus.json`, `college_lectern_debate_motion.json`, `college_lectern_debate_turn.json`, `college_lectern_debate_attend.json`, `college_lectern_debate_bonus.json`, `college_lectern_tool_done.json`, `college_lectern_quizzes.json`, `college_lectern_tool_panel.json`, `college_registrar_quizzes.json`

**Acceptance Criteria:**
- [ ] During the speaker's own Lecture or Discussion class, the lectern offers **Start a quiz** and **Start a debate** (each until used, and only while no tool runs); while a tool runs it offers **Show quiz panel** or **Show debate panel**
- [ ] **My quizzes** shows at the lectern (with or without a class) and the Registrar for anyone who may keep quizzes, and opens the editor
- [ ] The quiz chain asks quiz, seconds, attendance, bonus, then starts the quiz; the debate chain asks motion, turn length, attendance, bonus, then starts the debate; the last dialog shows the service's message
- [ ] A started tool tells the room (`A quiz begins: {title}` / `A debate begins! Pick your side.`) and broadcasts its first state
- [ ] Every new template is valid JSON; option texts are at most 35 characters, dialog texts at most 360, no em dashes

**Verify:** `dotnet build Chaos.slnx` in the server worktree -> Build succeeded. In the Unora worktree: `python -c "import json,glob; fs=glob.glob('Data/Configuration/Templates/Dialogs/Temauir/mileth/college/college_*.json'); [json.load(open(f,encoding='utf-8')) for f in fs]; print(len(fs))"` -> prints the file count with no error.

**Steps:**

- [ ] **Step 1: Add the Unora templates.** In the Unora worktree, create these files in `Data/Configuration/Templates/Dialogs/Temauir/mileth/college/`.

`college_lectern_quiz_pick.json`:

```json
{
  "contextual": true,
  "options": [],
  "scriptKeys": [
    "collegeLectern"
  ],
  "scriptVars": {},
  "templateKey": "college_lectern_quiz_pick",
  "text": "Which quiz? The number is how many questions it has.",
  "type": "DialogMenu"
}
```

`college_lectern_quiz_time.json`:

```json
{
  "contextual": true,
  "options": [
    {
      "dialogKey": "college_lectern_quiz_attend",
      "optionText": "15 seconds"
    },
    {
      "dialogKey": "college_lectern_quiz_attend",
      "optionText": "20 seconds"
    },
    {
      "dialogKey": "college_lectern_quiz_attend",
      "optionText": "30 seconds"
    },
    {
      "dialogKey": "college_lectern_quiz_attend",
      "optionText": "45 seconds"
    },
    {
      "dialogKey": "college_lectern_quiz_attend",
      "optionText": "60 seconds"
    }
  ],
  "scriptKeys": [
    "collegeLectern"
  ],
  "scriptVars": {},
  "templateKey": "college_lectern_quiz_time",
  "text": "How long should students have for each question?",
  "type": "DialogMenu"
}
```

`college_lectern_quiz_attend.json`:

```json
{
  "contextual": true,
  "options": [
    {
      "dialogKey": "college_lectern_quiz_bonus",
      "optionText": "Yes"
    },
    {
      "dialogKey": "college_lectern_quiz_bonus",
      "optionText": "No"
    }
  ],
  "scriptKeys": [
    "collegeLectern"
  ],
  "scriptVars": {},
  "templateKey": "college_lectern_quiz_attend",
  "text": "Should the quiz count for attendance? Students who answer fewer than half of the questions would get no roll.",
  "type": "DialogMenu"
}
```

`college_lectern_quiz_bonus.json`:

```json
{
  "contextual": true,
  "options": [
    {
      "dialogKey": "college_lectern_tool_done",
      "optionText": "Yes"
    },
    {
      "dialogKey": "college_lectern_tool_done",
      "optionText": "No"
    }
  ],
  "scriptKeys": [
    "collegeLectern"
  ],
  "scriptVars": {},
  "templateKey": "college_lectern_quiz_bonus",
  "text": "Should the best scorers get a bonus roll? Up to 3 students sharing the best score would roll twice.",
  "type": "DialogMenu"
}
```

`college_lectern_debate_motion.json`:

```json
{
  "contextual": true,
  "nextDialogKey": "college_lectern_debate_turn",
  "options": [],
  "scriptKeys": [
    "collegeLectern"
  ],
  "scriptVars": {},
  "templateKey": "college_lectern_debate_motion",
  "text": "What is the motion? Students will argue for or against it.",
  "textBoxLength": 120,
  "type": "DialogTextEntry"
}
```

`college_lectern_debate_turn.json`:

```json
{
  "contextual": true,
  "options": [
    {
      "dialogKey": "college_lectern_debate_attend",
      "optionText": "1 minute"
    },
    {
      "dialogKey": "college_lectern_debate_attend",
      "optionText": "2 minutes"
    },
    {
      "dialogKey": "college_lectern_debate_attend",
      "optionText": "3 minutes"
    }
  ],
  "scriptKeys": [
    "collegeLectern"
  ],
  "scriptVars": {},
  "templateKey": "college_lectern_debate_turn",
  "text": "How long may each speaker hold the floor?",
  "type": "DialogMenu"
}
```

`college_lectern_debate_attend.json`:

```json
{
  "contextual": true,
  "options": [
    {
      "dialogKey": "college_lectern_debate_bonus",
      "optionText": "Yes"
    },
    {
      "dialogKey": "college_lectern_debate_bonus",
      "optionText": "No"
    }
  ],
  "scriptKeys": [
    "collegeLectern"
  ],
  "scriptVars": {},
  "templateKey": "college_lectern_debate_attend",
  "text": "Should the debate count for attendance? Students who miss the opening or the final vote would get no roll.",
  "type": "DialogMenu"
}
```

`college_lectern_debate_bonus.json`:

```json
{
  "contextual": true,
  "options": [
    {
      "dialogKey": "college_lectern_tool_done",
      "optionText": "Yes"
    },
    {
      "dialogKey": "college_lectern_tool_done",
      "optionText": "No"
    }
  ],
  "scriptKeys": [
    "collegeLectern"
  ],
  "scriptVars": {},
  "templateKey": "college_lectern_debate_bonus",
  "text": "Will you pick up to 3 best speakers for a bonus roll when the debate ends?",
  "type": "DialogMenu"
}
```

`college_lectern_tool_done.json`, `college_lectern_quizzes.json` and `college_lectern_tool_panel.json` are the same apart from their `templateKey`:

```json
{
  "contextual": true,
  "nextDialogKey": "close",
  "options": [],
  "scriptKeys": [
    "collegeLectern"
  ],
  "scriptVars": {},
  "templateKey": "college_lectern_tool_done",
  "text": "",
  "type": "Normal"
}
```

`college_registrar_quizzes.json`:

```json
{
  "contextual": true,
  "nextDialogKey": "close",
  "options": [],
  "scriptKeys": [
    "collegeRegistrar"
  ],
  "scriptVars": {},
  "templateKey": "college_registrar_quizzes",
  "text": "",
  "type": "Normal"
}
```

- [ ] **Step 2: Add the lectern chains.** In `CollegeLecternScript.cs`:

  Add `using Chaos.DarkAges.Definitions;` to the usings.

  Add constants after `CHANGE_PROMPT`:

```csharp
    private const string START_QUIZ = "Start a quiz";
    private const string START_DEBATE = "Start a debate";
    private const string MY_QUIZZES = "My quizzes";
    private const string SHOW_QUIZ = "Show quiz panel";
    private const string SHOW_DEBATE = "Show debate panel";
    private const string YES = "Yes";
    private const int MAX_OPTION_CHARS = 35;
```

  Add these nested records at the start of the class body (before the fields):

```csharp
    /// <summary>The quiz chain's choices so far, carried from dialog to dialog in <c>Context</c>.</summary>
    private sealed record QuizSetup(IReadOnlyList<string> QuizIds, string QuizId = "", int Seconds = 0, bool Attendance = false);

    private sealed record DebateSetup(string Motion, int Minutes = 0, bool Attendance = false);
```

  In `OnDisplaying`'s switch, add:

```csharp
            case "college_lectern_quiz_pick":
                ShowQuizzes(source);

                break;
            case "college_lectern_quizzes":
                Panel.OpenQuizzes(source);
                Subject.Close(source);

                break;
            case "college_lectern_tool_panel":
                Panel.SendToolTo(source);
                Subject.Close(source);

                break;
            case "college_lectern_tool_done":
                Subject.Text = Subject.Context as string ?? "Done.";

                break;
```

  In `OnNext`'s switch, add:

```csharp
            case "college_lectern_quiz_pick" when optionIndex is { } quizPick && Subject.Context is QuizSetup picking:
                Subject.Context = picking with { QuizId = picking.QuizIds.ElementAtOrDefault(quizPick - 1) ?? string.Empty };

                break;
            case "college_lectern_quiz_time" when Subject.Context is QuizSetup timing && LeadingNumber(option) is { } seconds:
                Subject.Context = timing with { Seconds = seconds };

                break;
            case "college_lectern_quiz_attend" when Subject.Context is QuizSetup attending:
                Subject.Context = attending with { Attendance = option == YES };

                break;
            case "college_lectern_quiz_bonus" when Subject.Context is QuizSetup ready:
                Subject.Context = StartQuiz(source, ready, option == YES);

                break;
            case "college_lectern_debate_motion":
                Subject.Context = new DebateSetup(TryFetchArgs<string>(out var motion) ? motion ?? string.Empty : string.Empty);

                break;
            case "college_lectern_debate_turn" when Subject.Context is DebateSetup turning && LeadingNumber(option) is { } minutes:
                Subject.Context = turning with { Minutes = minutes };

                break;
            case "college_lectern_debate_attend" when Subject.Context is DebateSetup debateAttending:
                Subject.Context = debateAttending with { Attendance = option == YES };

                break;
            case "college_lectern_debate_bonus" when Subject.Context is DebateSetup debateReady:
                Subject.Context = StartDebate(source, debateReady, option == YES);

                break;
```

  In `ShowMain`, in the `running is null` branch, add before `Subject.Text = Subject.Options.Count > 0 ? ...`:

```csharp
            if (College.MayKeepQuizzes(source.Name, source.IsKnight, source.IsAdmin))
                Subject.AddOption(MY_QUIZZES, "college_lectern_quizzes");
```

  At the end of `ShowMain` (after the `if (running.Format == ClassFormat.Activity) { ... }` block), add:

```csharp
        if ((running.Format != ClassFormat.Activity) && College.ToolStatusOf(here) is { } tools)
        {
            if (tools.Running is { } tool)
                Subject.AddOption(tool == CollegeTool.Quiz ? SHOW_QUIZ : SHOW_DEBATE, "college_lectern_tool_panel");
            else if (running.Teacher.Equals(source.Name, StringComparison.OrdinalIgnoreCase))
            {
                if (!tools.QuizUsed)
                    Subject.AddOption(START_QUIZ, "college_lectern_quiz_pick");

                if (!tools.DebateUsed)
                    Subject.AddOption(START_DEBATE, "college_lectern_debate_motion");
            }
        }

        if (College.MayKeepQuizzes(source.Name, source.IsKnight, source.IsAdmin))
            Subject.AddOption(MY_QUIZZES, "college_lectern_quizzes");
```

  Add to the `Menus` region:

```csharp
    private void ShowQuizzes(Aisling source)
    {
        var quizzes = College.QuizzesOf(source.Name);

        if (quizzes.Count == 0)
        {
            Subject.Reply(source, "You have no saved quizzes. Write one under My quizzes.");

            return;
        }

        Subject.Options.Clear();

        foreach (var quiz in quizzes)
            Subject.AddOption(QuizLabel(quiz), "college_lectern_quiz_time");

        Subject.Context = new QuizSetup(quizzes.Select(q => q.Id).ToList());
    }
```

  Add to the `Actions` region:

```csharp
    private string StartQuiz(Aisling source, QuizSetup setup, bool bonus)
    {
        var start = College.StartQuiz(source.Name, source.MapInstance.InstanceId, setup.QuizId, setup.Seconds, setup.Attendance, bonus);

        if (start.Result == ToolStartResult.Started)
        {
            var title = College.QuizzesOf(source.Name).FirstOrDefault(q => q.Id == setup.QuizId)?.Title ?? string.Empty;
            TellRoom(source, CollegeText.QuizBegun(title));
            Panel.BroadcastTool(source.MapInstance);
        }

        return start.Message;
    }

    private string StartDebate(Aisling source, DebateSetup setup, bool bonus)
    {
        var start = College.StartDebate(source.Name, source.MapInstance.InstanceId, setup.Motion, setup.Minutes, setup.Attendance, bonus);

        if (start.Result == ToolStartResult.Started)
        {
            TellRoom(source, CollegeText.DEBATE_BEGUN);
            Panel.BroadcastTool(source.MapInstance);
        }

        return start.Message;
    }

    //dialog answers run under the speaker's map synchronization, so the room's aislings can be read here
    private static void TellRoom(Aisling source, string message)
    {
        foreach (var aisling in source.MapInstance.GetEntities<Aisling>())
            aisling.SendOrangeBarMessage(message);
    }
```

  Add to the `Helpers` region:

```csharp
    private static string QuizLabel(CollegeQuizIndex quiz)
    {
        var label = $"{quiz.Title} ({quiz.QuestionCount})";

        return label.Length <= MAX_OPTION_CHARS ? label : quiz.Title[..Math.Min(quiz.Title.Length, MAX_OPTION_CHARS - 3)] + "...";
    }

    /// <summary>The number at the start of "20 seconds" or "2 minutes".</summary>
    private static int? LeadingNumber(string? option)
        => option is not null && int.TryParse(option.Split(' ')[0], out var number) ? number : null;
```

- [ ] **Step 3: Add "My quizzes" to the Registrar.** In `CollegeRegistrarScript.cs`:
  - add the constant `private const string MY_QUIZZES = "My quizzes";` after `HAND_INS`;
  - in `ShowMain`, inside the `if (College.TeachableSubjects(...).Count > 0)` block, after the "My bookings" line, add `Subject.InsertOption(position++, MY_QUIZZES, "college_registrar_quizzes");`;
  - in `OnDisplaying`'s switch, add:

```csharp
            case "college_registrar_quizzes":
                Panel.OpenQuizzes(source);
                Subject.Close(source);

                break;
```

- [ ] **Step 4: Check the texts.** In the Unora worktree, run:

```bash
python -c "import json,glob; fs=glob.glob('Data/Configuration/Templates/Dialogs/Temauir/mileth/college/college_*.json'); bad=[(f,o['optionText']) for f in fs for o in json.load(open(f,encoding='utf-8')).get('options',[]) if len(o['optionText'])>35]; long=[f for f in fs if len(json.load(open(f,encoding='utf-8'))['text'])>360]; dash=[f for f in fs if '\u2014' in open(f,encoding='utf-8').read()]; print(bad, long, dash)"
```

  Expected: `[] [] []`.

- [ ] **Step 5: Build.** Run `dotnet build Chaos.slnx` in the server worktree. Expected: Build succeeded.

```json:metadata
{"files": ["Chaos/Scripting/DialogScripts/Temuair/College/CollegeLecternScript.cs", "Chaos/Scripting/DialogScripts/Temuair/College/CollegeRegistrarScript.cs", "Data/Configuration/Templates/Dialogs/Temauir/mileth/college/college_lectern_quiz_pick.json", "Data/Configuration/Templates/Dialogs/Temauir/mileth/college/college_lectern_quiz_time.json", "Data/Configuration/Templates/Dialogs/Temauir/mileth/college/college_lectern_quiz_attend.json", "Data/Configuration/Templates/Dialogs/Temauir/mileth/college/college_lectern_quiz_bonus.json", "Data/Configuration/Templates/Dialogs/Temauir/mileth/college/college_lectern_debate_motion.json", "Data/Configuration/Templates/Dialogs/Temauir/mileth/college/college_lectern_debate_turn.json", "Data/Configuration/Templates/Dialogs/Temauir/mileth/college/college_lectern_debate_attend.json", "Data/Configuration/Templates/Dialogs/Temauir/mileth/college/college_lectern_debate_bonus.json", "Data/Configuration/Templates/Dialogs/Temauir/mileth/college/college_lectern_tool_done.json", "Data/Configuration/Templates/Dialogs/Temauir/mileth/college/college_lectern_quizzes.json", "Data/Configuration/Templates/Dialogs/Temauir/mileth/college/college_lectern_tool_panel.json", "Data/Configuration/Templates/Dialogs/Temauir/mileth/college/college_registrar_quizzes.json"], "verifyCommand": "dotnet build Chaos.slnx", "acceptanceCriteria": ["lectern offers Start a quiz / Start a debate to the class Teacher in Lecture/Discussion, each until used, while no tool runs; Show panel while one runs", "My quizzes at the lectern and the Registrar opens the editor", "quiz chain: quiz, seconds, attendance, bonus, start; debate chain: motion, turn, attendance, bonus, start; last dialog shows the message", "a started tool tells the room and broadcasts its first state", "templates valid JSON; options <= 35, texts <= 360, no em dashes"], "modelTier": "standard"}
```

---

### Task 7: Client models: QuizDocument, ToolCountdown, ClassToolText (client repo)

**Goal:** Pure, tested pieces for the client windows: the editable quiz, a countdown from a server message's "seconds left", and every line of text the card, the panels and the markers show.

**Files:**
- Create: `Chaos.Client/ViewModel/College/QuizDocument.cs`
- Create: `Chaos.Client/ViewModel/College/ToolCountdown.cs`
- Create: `Chaos.Client/ViewModel/College/ClassToolText.cs`
- Test: new `Tests/Chaos.Client.Tests/College/QuizDocumentTests.cs`, `ToolCountdownTests.cs`, `ClassToolTextTests.cs`

**Acceptance Criteria:**
- [ ] `QuizDocument`: `New` gives one blank question; `Add` stops at 20 and selects the new question; `MoveUp`/`MoveDown` move the selected question and keep it selected; `Remove` never leaves the quiz empty; edits mark it dirty and raise `Changed`; `Select` does not mark it dirty
- [ ] `ToInfo` drops blank answers after the last filled one, keeps blanks in between, and sends `NO_ANSWER` when the right answer is unmarked or was dropped; `Load` turns `NO_ANSWER` into no mark and cuts over-long text
- [ ] `ToolCountdown` counts whole seconds down from the start and never below 0
- [ ] `ClassToolText` gives the clock, right and top lines (the viewer shown as "you"), the debate counts and gains, the result line, the floor line, the side markers and the captions

**Verify:** `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\college-tools-server -- --treenode-filter "/*/*/QuizDocumentTests/*"` -> all pass; same for `ToolCountdownTests` and `ClassToolTextTests`

**Steps:**

- [ ] **Step 1: Write the failing tests.** Create `Tests/Chaos.Client.Tests/College/QuizDocumentTests.cs`:

```csharp
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class QuizDocumentTests
{
    private static QuizDocument Fresh()
    {
        var document = new QuizDocument();
        document.New();

        return document;
    }

    [Test]
    public void New_gives_one_blank_question_that_is_clean()
    {
        var document = Fresh();

        document.Questions.Should().ContainSingle();
        document.Selected.Should().Be(0);
        document.Id.Should().BeEmpty();
        document.IsDirty.Should().BeFalse();
    }

    [Test]
    public void Add_stops_at_20_and_selects_the_new_question()
    {
        var document = Fresh();

        for (var i = 1; i < 20; i++)
            document.Add().Should().BeTrue();

        document.Add().Should().BeFalse();
        document.Questions.Should().HaveCount(20);
        document.Selected.Should().Be(19);
    }

    [Test]
    public void Moving_keeps_the_question_selected()
    {
        var document = Fresh();
        document.SetText("first");
        document.Add();
        document.SetText("second");

        document.MoveUp().Should().BeTrue();

        document.Questions.Select(q => q.Text).Should().Equal("second", "first");
        document.Selected.Should().Be(0);
        document.MoveUp().Should().BeFalse();
        document.MoveDown().Should().BeTrue();
        document.Selected.Should().Be(1);
        document.MoveDown().Should().BeFalse();
    }

    [Test]
    public void Removing_the_last_question_leaves_a_blank_one()
    {
        var document = Fresh();
        document.SetText("only");

        document.Remove().Should().BeTrue();

        document.Questions.Should().ContainSingle().Which.Text.Should().BeEmpty();
        document.Selected.Should().Be(0);
    }

    [Test]
    public void Removing_the_bottom_question_selects_the_one_above()
    {
        var document = Fresh();
        document.Add();
        document.Add();

        document.Remove();

        document.Selected.Should().Be(1);
        document.Questions.Should().HaveCount(2);
    }

    [Test]
    public void Edits_mark_it_dirty_and_raise_changed_but_selecting_does_not_mark_it()
    {
        var document = Fresh();
        document.Add();
        document.MarkClean();
        var raised = 0;
        document.Changed += () => raised++;

        document.Select(0);
        document.IsDirty.Should().BeFalse();

        document.SetTitle("Mileth history");
        document.SetText("Who?");
        document.SetAnswer(0, "Danaan");
        document.MarkRight(0);
        document.SetText("Who?");

        document.IsDirty.Should().BeTrue();
        raised.Should().Be(5);
    }

    [Test]
    public void To_info_drops_trailing_blank_answers_and_keeps_inner_ones()
    {
        var document = Fresh();
        document.SetTitle("T");
        document.SetText("Q?");
        document.SetAnswer(0, "a");
        document.SetAnswer(2, "c");
        document.MarkRight(2);

        var question = document.ToInfo().Questions.Single();

        question.Answers.Should().Equal("a", "", "c");
        question.Right.Should().Be(2);
    }

    [Test]
    public void A_right_answer_that_was_dropped_or_never_marked_is_sent_as_none()
    {
        var document = Fresh();
        document.SetAnswer(0, "a");
        document.MarkRight(3);

        document.ToInfo().Questions.Single().Right.Should().Be(CollegeProtocol.NO_ANSWER);

        document.Add();
        document.SetAnswer(0, "a");

        document.ToInfo().Questions[1].Right.Should().Be(CollegeProtocol.NO_ANSWER);
    }

    [Test]
    public void Load_reads_the_quiz_and_cuts_long_text()
    {
        var document = new QuizDocument();

        document.Load(
            "abc",
            new CollegeQuizDraftInfo
            {
                Title = new string('t', 50),
                Questions =
                [
                    new CollegeQuizQuestionInfo { Text = "One?", Answers = ["a", "b"], Right = 1 },
                    new CollegeQuizQuestionInfo { Text = new string('q', 200), Answers = ["x"], Right = CollegeProtocol.NO_ANSWER }
                ]
            });

        document.Id.Should().Be("abc");
        document.Title.Should().HaveLength(40);
        document.Questions.Should().HaveCount(2);
        document.Questions[0].Answers.Should().Equal("a", "b", "", "");
        document.Questions[0].Right.Should().Be(1);
        document.Questions[1].Text.Should().HaveLength(150);
        document.Questions[1].Right.Should().Be(-1);
        document.IsDirty.Should().BeFalse();
    }

    [Test]
    public void Labels_number_the_questions()
    {
        var document = Fresh();
        document.SetText("Who founded the temple?");
        document.Add();

        document.Label(0).Should().Be("1. Who founded the temple?");
        document.Label(1).Should().Be("2. (empty)");
    }
}
```

Create `Tests/Chaos.Client.Tests/College/ToolCountdownTests.cs`:

```csharp
using Chaos.Client.ViewModel.College;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class ToolCountdownTests
{
    [Test]
    public void It_counts_whole_seconds_down_and_stops_at_zero()
    {
        var countdown = new ToolCountdown();
        countdown.Start(20);

        countdown.Left.Should().Be(20);

        countdown.Advance(0.9);
        countdown.Left.Should().Be(20);

        countdown.Advance(0.2);
        countdown.Left.Should().Be(19);

        countdown.Advance(30);
        countdown.Left.Should().Be(0);
    }

    [Test]
    public void Starting_again_resets_it()
    {
        var countdown = new ToolCountdown();
        countdown.Start(10);
        countdown.Advance(5);

        countdown.Start(30);

        countdown.Left.Should().Be(30);
    }
}
```

Create `Tests/Chaos.Client.Tests/College/ClassToolTextTests.cs`:

```csharp
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class ClassToolTextTests
{
    private static CollegeDebateInfo Debate()
        => new()
        {
            For = 2,
            Against = 2,
            Undecided = 1,
            OpeningFor = 1,
            OpeningAgainst = 2,
            OpeningUndecided = 2
        };

    [Test]
    public void Clock_shows_minutes_and_seconds()
    {
        ClassToolText.Clock(72).Should().Be("1:12");
        ClassToolText.Clock(5).Should().Be("0:05");
    }

    [Test]
    public void Right_line_counts_who_got_it()
    {
        ClassToolText.RightLine(4, 9).Should().Be("4 of 9 got it right.");
        ClassToolText.RightLine(0, 0).Should().Be("No one answered.");
    }

    [Test]
    public void My_result_says_right_wrong_or_none_with_the_score()
    {
        ClassToolText.MyResult(2, 2, 3).Should().Be("Right! Your score: 3.");
        ClassToolText.MyResult(1, 2, 2).Should().Be("Wrong. Your score: 2.");
        ClassToolText.MyResult(CollegeProtocol.NO_ANSWER, 2, 2).Should().Be("No answer. Your score: 2.");
    }

    [Test]
    public void Top_line_shows_the_viewer_as_you()
    {
        var top = new List<CollegeScoreInfo>
        {
            new() { Name = "Aroha", Score = 3 },
            new() { Name = "Kael", Score = 3 },
            new() { Name = "Mira", Score = 2 }
        };

        ClassToolText.TopLine(top, "mira").Should().Be("Top: Aroha 3, Kael 3, you 2");
        ClassToolText.TopLine([], "mira").Should().BeEmpty();
    }

    [Test]
    public void Answered_line_counts_and_shows_the_time()
        => ClassToolText.AnsweredLine(7, 9, 14).Should().Be("Answered 7 of 9 - 0:14 left");

    [Test]
    public void Debate_counts_and_gains()
    {
        ClassToolText.Counts(Debate()).Should().Be("For 2  Against 2  Undecided 1");
        ClassToolText.CountsWithGains(Debate()).Should().Be("For 2 (+1)  Against 2 (0)  Und. 1");
        ClassToolText.Opening(Debate()).Should().Be("Opening vote: For 1, Against 2, Undecided 2");
    }

    [Test]
    public void Result_line_names_the_winner_and_its_gain()
    {
        ClassToolText.ResultLine(DebateResult.For, Debate()).Should().Be("For wins (+1)");
        ClassToolText.ResultLine(DebateResult.Draw, Debate()).Should().Be("A draw.");
        ClassToolText.Leading(Debate()).Should().Be(DebateResult.For);
    }

    [Test]
    public void Floor_line_shows_the_speaker_or_open()
    {
        ClassToolText.FloorLine("Kael", 72).Should().Be("Floor: Kael 1:12 left");
        ClassToolText.FloorLine("", 0).Should().Be("Floor: open");
    }

    [Test]
    public void Markers_name_the_side_and_star_the_speaker()
    {
        ClassToolText.Marker(DebateSide.For, false).Should().Be("For");
        ClassToolText.Marker(DebateSide.Against, true).Should().Be("Against *");
        ClassToolText.Marker(DebateSide.Undecided, false).Should().Be("?");
        ClassToolText.Marker(DebateSide.None, true).Should().BeEmpty();
    }

    [Test]
    public void Captions_name_the_quiz_and_the_vote()
    {
        ClassToolText.QuizCaption(new CollegeQuizInfo { Title = "Mileth history", Number = 3, Count = 8 }).Should().Be("Quiz: Mileth history (3/8)");
        ClassToolText.VoteCaption(DebatePhase.Opening, 48).Should().Be("Opening vote: 0:48");
        ClassToolText.VoteCaption(DebatePhase.Final, 18).Should().Be("Final vote: 0:18");
        ClassToolText.HandsMore(3, 5).Should().Be("+2 more");
        ClassToolText.HandsMore(3, 3).Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run the tests to see them fail.** Run the Verify commands. Expected: compile errors (`QuizDocument`, `ToolCountdown`, `ClassToolText` don't exist).

- [ ] **Step 3: Add the quiz model.** Create `Chaos.Client/ViewModel/College/QuizDocument.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;

namespace Chaos.Client.ViewModel.College;

/// <summary>One question being edited. It always has four answer slots; blank slots after the last filled one are not sent.</summary>
public sealed class QuizQuestionDraft
{
    public string Text { get; set; } = string.Empty;
    public string[] Answers { get; } = ["", "", "", ""];

    /// <summary>The right answer's slot, or -1 while none is marked.</summary>
    public int Right { get; set; } = -1;
}

/// <summary>The quiz editor's model: a title and up to 20 questions, one of them selected for editing.</summary>
public sealed class QuizDocument
{
    private readonly List<QuizQuestionDraft> Items = [];

    /// <summary>The server's id, or empty until the first save of a new quiz.</summary>
    public string Id { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;
    public IReadOnlyList<QuizQuestionDraft> Questions => Items;
    public int Selected { get; private set; } = -1;
    public QuizQuestionDraft? Current => (Selected >= 0) && (Selected < Items.Count) ? Items[Selected] : null;

    /// <summary>Goes up on every change, including selection, so the window knows when to refresh its boxes.</summary>
    public int Version { get; private set; }

    public bool IsDirty { get; private set; }

    /// <summary>Raised each time <see cref="Version" /> goes up.</summary>
    public event Action? Changed;

    public void New()
    {
        Id = string.Empty;
        Title = string.Empty;
        Items.Clear();
        Items.Add(new QuizQuestionDraft());
        Selected = 0;
        IsDirty = false;
        Bump();
    }

    public void Load(string id, CollegeQuizDraftInfo info)
    {
        Id = id;
        Title = Cut(info.Title, CollegeProtocol.MAX_QUIZ_TITLE_CHARS);
        Items.Clear();

        foreach (var question in info.Questions.Take(CollegeProtocol.MAX_QUIZ_QUESTIONS))
        {
            var draft = new QuizQuestionDraft
            {
                Text = Cut(question.Text, CollegeProtocol.MAX_QUESTION_CHARS),
                Right = question.Right < CollegeProtocol.MAX_ANSWERS ? question.Right : -1
            };

            for (var i = 0; i < Math.Min(question.Answers.Count, CollegeProtocol.MAX_ANSWERS); i++)
                draft.Answers[i] = Cut(question.Answers[i], CollegeProtocol.MAX_ANSWER_CHARS);

            Items.Add(draft);
        }

        if (Items.Count == 0)
            Items.Add(new QuizQuestionDraft());

        Selected = 0;
        IsDirty = false;
        Bump();
    }

    public void SetId(string id) => Id = id;

    public void MarkClean() => IsDirty = false;

    public void SetTitle(string title)
    {
        title = Cut(title, CollegeProtocol.MAX_QUIZ_TITLE_CHARS);

        if (title == Title)
            return;

        Title = title;
        Touch();
    }

    public void SetText(string text)
    {
        text = Cut(text, CollegeProtocol.MAX_QUESTION_CHARS);

        if (Current is not { } question || (question.Text == text))
            return;

        question.Text = text;
        Touch();
    }

    public void SetAnswer(int slot, string text)
    {
        text = Cut(text, CollegeProtocol.MAX_ANSWER_CHARS);

        if (Current is not { } question || (slot < 0) || (slot >= CollegeProtocol.MAX_ANSWERS) || (question.Answers[slot] == text))
            return;

        question.Answers[slot] = text;
        Touch();
    }

    public void MarkRight(int slot)
    {
        if (Current is not { } question || (slot < 0) || (slot >= CollegeProtocol.MAX_ANSWERS) || (question.Right == slot))
            return;

        question.Right = slot;
        Touch();
    }

    public void Select(int index)
    {
        if ((index < 0) || (index >= Items.Count) || (index == Selected))
            return;

        Selected = index;
        Bump();
    }

    public bool Add()
    {
        if (Items.Count >= CollegeProtocol.MAX_QUIZ_QUESTIONS)
            return false;

        Items.Add(new QuizQuestionDraft());
        Selected = Items.Count - 1;
        Touch();

        return true;
    }

    public bool MoveUp()
    {
        if (Selected <= 0)
            return false;

        (Items[Selected - 1], Items[Selected]) = (Items[Selected], Items[Selected - 1]);
        Selected--;
        Touch();

        return true;
    }

    public bool MoveDown()
    {
        if ((Selected < 0) || (Selected >= Items.Count - 1))
            return false;

        (Items[Selected + 1], Items[Selected]) = (Items[Selected], Items[Selected + 1]);
        Selected++;
        Touch();

        return true;
    }

    /// <summary>Removes the selected question. The quiz never ends up empty: the last one is replaced by a blank question.</summary>
    public bool Remove()
    {
        if (Current is null)
            return false;

        Items.RemoveAt(Selected);

        if (Items.Count == 0)
            Items.Add(new QuizQuestionDraft());

        Selected = Math.Min(Selected, Items.Count - 1);
        Touch();

        return true;
    }

    public string Label(int index)
    {
        var text = Items[index].Text;

        return $"{index + 1}. {(string.IsNullOrWhiteSpace(text) ? "(empty)" : text)}";
    }

    public CollegeQuizDraftInfo ToInfo()
        => new()
        {
            Title = Title,
            Questions = Items.Select(ToInfo).ToList()
        };

    private static CollegeQuizQuestionInfo ToInfo(QuizQuestionDraft draft)
    {
        var last = Array.FindLastIndex(draft.Answers, answer => !string.IsNullOrWhiteSpace(answer));

        return new CollegeQuizQuestionInfo
        {
            Text = draft.Text,
            Answers = draft.Answers.Take(last + 1).ToList(),
            Right = (draft.Right >= 0) && (draft.Right <= last) ? (byte)draft.Right : CollegeProtocol.NO_ANSWER
        };
    }

    private void Touch()
    {
        IsDirty = true;
        Bump();
    }

    private void Bump()
    {
        Version++;
        Changed?.Invoke();
    }

    private static string Cut(string text, int max) => text.Length <= max ? text : text[..max];
}
```

- [ ] **Step 4: Add the countdown.** Create `Chaos.Client/ViewModel/College/ToolCountdown.cs`:

```csharp
namespace Chaos.Client.ViewModel.College;

/// <summary>Counts down from the "seconds left" a server message gave, on the game's own clock after it arrived.</summary>
public sealed class ToolCountdown
{
    private double Elapsed;
    private int Seconds;

    public int Left => Math.Max(0, Seconds - (int)Math.Floor(Elapsed));

    public void Start(int seconds)
    {
        Seconds = seconds;
        Elapsed = 0;
    }

    public void Advance(double seconds) => Elapsed += seconds;
}
```

- [ ] **Step 5: Add the texts.** Create `Chaos.Client/ViewModel/College/ClassToolText.cs`:

```csharp
using System.Globalization;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;

namespace Chaos.Client.ViewModel.College;

/// <summary>Every line of text the quiz card, the Teacher panel, the debate panel and the side markers show.</summary>
public static class ClassToolText
{
    public static string Clock(int seconds) => $"{seconds / 60}:{seconds % 60:00}";

    public static string QuizCaption(CollegeQuizInfo quiz) => $"Quiz: {quiz.Title} ({quiz.Number}/{quiz.Count})";

    public static string RightLine(int right, int answered) => answered == 0 ? "No one answered." : $"{right} of {answered} got it right.";

    public static string MyResult(byte myAnswer, byte right, int myScore)
        => myAnswer == CollegeProtocol.NO_ANSWER ? $"No answer. Your score: {myScore}."
            : myAnswer == right ? $"Right! Your score: {myScore}."
            : $"Wrong. Your score: {myScore}.";

    public static string TopLine(IReadOnlyList<CollegeScoreInfo> top, string viewer)
        => top.Count == 0
            ? string.Empty
            : "Top: " + string.Join(", ", top.Select(t => $"{(t.Name.Equals(viewer, StringComparison.OrdinalIgnoreCase) ? "you" : t.Name)} {t.Score}"));

    public static string AnsweredLine(int answered, int students, int secondsLeft) => $"Answered {answered} of {students} - {Clock(secondsLeft)} left";

    public static string Counts(CollegeDebateInfo debate) => $"For {debate.For}  Against {debate.Against}  Undecided {debate.Undecided}";

    public static string CountsWithGains(CollegeDebateInfo debate)
        => $"For {debate.For} ({Gain(debate.For - debate.OpeningFor)})  Against {debate.Against} ({Gain(debate.Against - debate.OpeningAgainst)})  Und. {debate.Undecided}";

    public static string Opening(CollegeDebateInfo debate)
        => $"Opening vote: For {debate.OpeningFor}, Against {debate.OpeningAgainst}, Undecided {debate.OpeningUndecided}";

    public static string Gain(int gain) => gain > 0 ? $"+{gain}" : gain.ToString(CultureInfo.InvariantCulture);

    /// <summary>Who would win if the vote ended now.</summary>
    public static DebateResult Leading(CollegeDebateInfo debate)
    {
        var forGain = debate.For - debate.OpeningFor;
        var againstGain = debate.Against - debate.OpeningAgainst;

        return forGain > againstGain ? DebateResult.For : againstGain > forGain ? DebateResult.Against : DebateResult.Draw;
    }

    public static string ResultLine(DebateResult result, CollegeDebateInfo debate)
        => result switch
        {
            DebateResult.For     => $"For wins ({Gain(debate.For - debate.OpeningFor)})",
            DebateResult.Against => $"Against wins ({Gain(debate.Against - debate.OpeningAgainst)})",
            DebateResult.Draw    => "A draw.",
            _                    => string.Empty
        };

    public static string FloorLine(string floor, int secondsLeft) => floor.Length == 0 ? "Floor: open" : $"Floor: {floor} {Clock(secondsLeft)} left";

    public static string VoteCaption(DebatePhase phase, int secondsLeft)
        => phase == DebatePhase.Opening ? $"Opening vote: {Clock(secondsLeft)}" : $"Final vote: {Clock(secondsLeft)}";

    public static string HandsMore(int shown, int total) => total > shown ? $"+{total - shown} more" : string.Empty;

    /// <summary>The label over a player's head. The game font has no star, so the floor holder gets " *".</summary>
    public static string Marker(DebateSide side, bool floor)
    {
        var text = side switch
        {
            DebateSide.For       => "For",
            DebateSide.Against   => "Against",
            DebateSide.Undecided => "?",
            _                    => string.Empty
        };

        return floor && (text.Length > 0) ? text + " *" : text;
    }
}
```

- [ ] **Step 6: Run the tests to see them pass.** Run the Verify commands. Expected: all pass.

```json:metadata
{"files": ["Chaos.Client/ViewModel/College/QuizDocument.cs", "Chaos.Client/ViewModel/College/ToolCountdown.cs", "Chaos.Client/ViewModel/College/ClassToolText.cs", "Tests/Chaos.Client.Tests/College/QuizDocumentTests.cs", "Tests/Chaos.Client.Tests/College/ToolCountdownTests.cs", "Tests/Chaos.Client.Tests/College/ClassToolTextTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\\Users\\Michael\\Documents\\GitHub\\worktrees\\college-tools-server -- --treenode-filter \"/*/*/QuizDocumentTests/*\"", "acceptanceCriteria": ["New gives one blank clean question; Add stops at 20 and selects; Move keeps selection; Remove never empties; edits dirty + Changed; Select not dirty", "ToInfo drops trailing blanks, keeps inner ones, NO_ANSWER when unmarked/dropped; Load maps NO_ANSWER to -1 and cuts long text", "ToolCountdown counts whole seconds down, never below 0", "ClassToolText clock, right/top (you), answered, debate counts/gains/opening, result, floor, markers, captions"], "modelTier": "mechanical"}
```

---
### Task 8: Client quiz editor window (client repo)

**Goal:** A 560 x 464 window with two pages. Page 1 lists the player's quizzes with **Open**, **New quiz**, **Copy** and **Delete**. Page 2 edits one quiz: title, questions list, the question, four answers with a tick for the right one. **Back**, **Save** and **OK** save first when there are changes.

**Files:**
- Create: `Chaos.Client/Controls/World/Popups/College/QuizEditorControl.cs`

**Acceptance Criteria:**
- [ ] `OpenList` shows page 1 with one row per quiz (title, "N questions"), "My quizzes (n/20)" and an empty-list line; **Open**, **Copy**, **Delete** are off with no row selected
- [ ] **Delete** asks with a second click ("Delete {title}? Click Delete again.") before sending `QuizDelete`
- [ ] **New quiz** opens page 2 with one blank question, or says "You can keep 20 quizzes." at 20
- [ ] `OpenQuiz` loads the quiz into page 2; selecting, adding, moving and removing questions keeps the boxes in step with the document
- [ ] **Save** sends `QuizSave` with the document's id (empty for a new quiz) and won't send a second save of a new quiz before its id arrives
- [ ] `ShowResult` shows the message; a successful save stores the new id and marks the document clean (only if nothing changed since); a refused save shows a hidden editor again and returns true
- [ ] **Back** with changes saves, then returns to page 1 and asks for the list; **OK**/Esc with changes saves, then hides; `SaveIfDirty` saves on logout

**Verify:** `dotnet build Chaos.Client/Chaos.Client.csproj -p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\college-tools-server` -> Build succeeded

**Steps:**

- [ ] **Step 1: Write the window.** Create `Chaos.Client/Controls/World/Popups/College/QuizEditorControl.cs`:

```csharp
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.Extensions;
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>
///     The quiz editor. Page 1 lists the Teacher's saved quizzes; page 2 edits one. The server's QuizList and QuizEdit open
///     it; every save goes to the server, which checks the limits again.
/// </summary>
public sealed class QuizEditorControl : GuildCloakDialogBase
{
    private const int WIDTH = 560;
    private const int HEIGHT = 464;
    private const int LEFT = 16;
    private const int INNER_WIDTH = WIDTH - (2 * LEFT);
    private const int CAPTION_TOP = 10;
    private const int FOOTER_BOTTOM = HEIGHT - BORDER_BOTTOM_HEIGHT - 4;
    private const int BUTTONS_TOP = FOOTER_BOTTOM - CustomButton.HEIGHT;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;
    private const int STATUS_X = LEFT + 276;
    private const int LIST_HEADER_TOP = 30;
    private const int LIST_ROWS_TOP = 46;
    private const int COUNT_X = 424;
    private const int SIDE_WIDTH = 170;
    private const int TITLE_TOP = 28;
    private const int QUESTIONS_LABEL_TOP = 54;
    private const int QUESTION_ROWS_TOP = 70;
    private const int EDIT_X = 200;
    private const int EDIT_WIDTH = LEFT + INNER_WIDTH - EDIT_X;
    private const int QUESTION_TOP = 44;
    private const int QUESTION_HEIGHT = 50;
    private const int ANSWERS_LABEL_TOP = 100;
    private const int ANSWERS_TOP = 116;
    private const int ANSWER_STEP = 28;
    private const int ANSWER_BOX_X = EDIT_X + 24;
    private const int EDIT_BUTTONS_TOP = ANSWERS_TOP + (CollegeProtocol.MAX_ANSWERS * ANSWER_STEP) + 4;

    private readonly CustomTextBox[] AnswerBoxes = new CustomTextBox[CollegeProtocol.MAX_ANSWERS];
    private readonly UILabel CaptionLabel;
    private readonly CustomButton CopyButton;
    private readonly CustomButton DeleteButton;
    private readonly List<UIElement> EditPage = [];
    private readonly UILabel EmptyLabel;
    private readonly List<UIElement> ListPage = [];
    private readonly CustomButton OpenButton;
    private readonly CustomTextBox QuestionBox;
    private readonly UILabel QuestionLabel;
    private readonly CollegeListRow[] QuestionRows = new CollegeListRow[CollegeProtocol.MAX_QUIZ_QUESTIONS];
    private readonly UILabel QuestionsLabel;
    private readonly CollegeListRow[] QuizRowsShown = new CollegeListRow[CollegeProtocol.MAX_QUIZZES_PER_OWNER];
    private readonly CustomCheckBox[] RightBoxes = new CustomCheckBox[CollegeProtocol.MAX_ANSWERS];
    private readonly UILabel Status;
    private readonly CustomTextBox TitleBox;

    private bool AwaitingSave;
    private bool BackAfterSave;
    private string DeleteArmed = string.Empty;
    private bool EditingPage;
    private List<CollegeQuizRowInfo> Quizzes = [];
    private int SelectedRow = -1;
    private int SentVersion;
    private int ShownVersion = -1;

    public QuizDocument Document { get; } = new();

    public event Action<CollegeActionArgs>? ActionRequested;

    public QuizEditorControl()
        : base("_nsett", false)
    {
        Name = "QuizEditor";
        Visible = false;
        UsesControlStack = true;
        Width = WIDTH;
        Height = HEIGHT;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(OnCloseClicked, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);
        CaptionLabel = Caption(string.Empty, LEFT, CAPTION_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gold);
        var footerTextY = BUTTONS_TOP + ((CustomButton.HEIGHT - TextRenderer.CHAR_HEIGHT) / 2);
        Status = Caption(string.Empty, STATUS_X, footerTextY, LEFT + INNER_WIDTH - STATUS_X);

        //page 1: the list
        ListPage.Add(Caption("Title", LEFT, LIST_HEADER_TOP, 200, color: LegendColors.Gold));
        ListPage.Add(Caption("Questions", LEFT + COUNT_X, LIST_HEADER_TOP, 100, color: LegendColors.Gold));

        for (var i = 0; i < QuizRowsShown.Length; i++)
        {
            var index = i;
            var row = new CollegeListRow(INNER_WIDTH, [0, COUNT_X]) { X = LEFT, Y = LIST_ROWS_TOP + (i * CollegeListRow.HEIGHT) };
            row.Clicked += () => SelectRow(index);

            row.DoubleClicked += () =>
            {
                SelectRow(index);
                OpenSelected();
            };

            AddChild(row);
            QuizRowsShown[i] = row;
            ListPage.Add(row);
        }

        EmptyLabel = Caption("No quizzes yet. Press New quiz to write one.", LEFT, LIST_ROWS_TOP, INNER_WIDTH, color: LegendColors.Gray);
        ListPage.Add(EmptyLabel);
        OpenButton = AddButton("Open", 60, LEFT, BUTTONS_TOP, OpenSelected);
        ListPage.Add(OpenButton);
        ListPage.Add(AddButton("New quiz", 80, LEFT + 66, BUTTONS_TOP, NewQuiz));
        CopyButton = AddButton("Copy", 50, LEFT + 152, BUTTONS_TOP, CopySelected);
        ListPage.Add(CopyButton);
        DeleteButton = AddButton("Delete", 60, LEFT + 208, BUTTONS_TOP, DeleteSelected);
        ListPage.Add(DeleteButton);

        //page 2: one quiz
        TitleBox = new CustomTextBox
        {
            X = LEFT,
            Y = TITLE_TOP,
            Width = SIDE_WIDTH,
            Height = CustomButton.HEIGHT,
            MaxLength = CollegeProtocol.MAX_QUIZ_TITLE_CHARS,
            IsSelectable = true,
            IsTabStop = true,
            HintText = "Quiz title"
        };

        AddChild(TitleBox);
        EditPage.Add(TitleBox);

        QuestionsLabel = Caption(string.Empty, LEFT, QUESTIONS_LABEL_TOP, SIDE_WIDTH, color: LegendColors.Gold);
        EditPage.Add(QuestionsLabel);

        for (var i = 0; i < QuestionRows.Length; i++)
        {
            var index = i;
            var row = new CollegeListRow(SIDE_WIDTH, [0]) { X = LEFT, Y = QUESTION_ROWS_TOP + (i * CollegeListRow.HEIGHT) };
            row.Clicked += () => SelectQuestion(index);
            AddChild(row);
            QuestionRows[i] = row;
            EditPage.Add(row);
        }

        QuestionLabel = Caption(string.Empty, EDIT_X, TITLE_TOP, EDIT_WIDTH, color: LegendColors.Gold);
        EditPage.Add(QuestionLabel);

        QuestionBox = new CustomTextBox
        {
            X = EDIT_X,
            Y = QUESTION_TOP,
            Width = EDIT_WIDTH,
            Height = QUESTION_HEIGHT,
            IsMultiLine = true,
            IsSelectable = true,
            IsTabStop = true,
            MaxLength = CollegeProtocol.MAX_QUESTION_CHARS,
            HintText = "Question"
        };

        AddChild(QuestionBox);
        EditPage.Add(QuestionBox);
        EditPage.Add(Caption("Answers (tick the right one)", EDIT_X, ANSWERS_LABEL_TOP, EDIT_WIDTH, color: LegendColors.Gold));

        for (var i = 0; i < CollegeProtocol.MAX_ANSWERS; i++)
        {
            var slot = i;
            var y = ANSWERS_TOP + (i * ANSWER_STEP);

            var right = new CustomCheckBox
            {
                X = EDIT_X,
                Y = y + 2,
                Width = CustomCheckBox.CHECKBOX_SIZE,
                Height = CustomCheckBox.CHECKBOX_SIZE
            };

            right.Clicked += () => MarkRight(slot);
            AddChild(right);
            RightBoxes[i] = right;
            EditPage.Add(right);

            var box = new CustomTextBox
            {
                X = ANSWER_BOX_X,
                Y = y,
                Width = LEFT + INNER_WIDTH - ANSWER_BOX_X,
                Height = CustomButton.HEIGHT,
                MaxLength = CollegeProtocol.MAX_ANSWER_CHARS,
                IsSelectable = true,
                IsTabStop = true,
                HintText = $"Answer {(char)('A' + i)}"
            };

            AddChild(box);
            AnswerBoxes[i] = box;
            EditPage.Add(box);
        }

        EditPage.Add(AddButton("Add question", 100, EDIT_X, EDIT_BUTTONS_TOP, () => Edit(Document.Add)));
        EditPage.Add(AddButton("Up", 40, EDIT_X + 106, EDIT_BUTTONS_TOP, () => Edit(Document.MoveUp)));
        EditPage.Add(AddButton("Down", 50, EDIT_X + 152, EDIT_BUTTONS_TOP, () => Edit(Document.MoveDown)));
        EditPage.Add(AddButton("Remove question", 120, EDIT_X + 208, EDIT_BUTTONS_TOP, () => Edit(Document.Remove)));
        EditPage.Add(AddButton("Back", 50, LEFT, BUTTONS_TOP, OnBackClicked));
        EditPage.Add(AddButton("Save", 50, LEFT + 56, BUTTONS_TOP, () => Save(false)));
    }

    private CollegeQuizRowInfo? SelectedQuiz => (SelectedRow >= 0) && (SelectedRow < Quizzes.Count) ? Quizzes[SelectedRow] : null;

    public void OpenList(CollegeDisplayArgs args)
    {
        Quizzes = args.QuizRows;
        SelectedRow = Quizzes.Count == 0 ? -1 : Math.Clamp(SelectedRow, 0, Quizzes.Count - 1);
        DeleteArmed = string.Empty;
        ShowPage(false);

        if (!Visible)
        {
            Status.Text = string.Empty;
            Show();
        }
    }

    public void OpenQuiz(CollegeDisplayArgs args)
    {
        if (args.QuizDraft is null)
            return;

        Document.Load(args.QuizId, args.QuizDraft);
        AwaitingSave = false;
        BackAfterSave = false;
        Status.Text = string.Empty;
        ShowPage(true);

        if (!Visible)
            Show();
    }

    /// <summary>Answers a QuizResult. True when a refused save showed the hidden editor again, so the caller brings it to the front.</summary>
    public bool ShowResult(CollegeDisplayArgs args)
    {
        Status.Text = args.Message;

        //Copy and Delete results only need the status line; the list that follows refreshes page 1
        if (!AwaitingSave)
            return false;

        AwaitingSave = false;

        if (args.QuizResult == QuizResultCode.Saved)
        {
            if (Document.Id.Length == 0)
                Document.SetId(args.QuizId);

            //typing after Save was pressed is still unsaved
            if (Document.Version == SentVersion)
                Document.MarkClean();

            if (BackAfterSave)
            {
                BackAfterSave = false;
                GoToList();
            }

            return false;
        }

        BackAfterSave = false;

        if (Visible)
            return false;

        ShowPage(true);
        Show();

        return true;
    }

    /// <summary>Logging out takes the editor away without its Close, so a dirty quiz is saved here.</summary>
    public void SaveIfDirty()
    {
        if (!EditingPage)
            return;

        Pull();

        if (Document.IsDirty)
            Save(false);
    }

    public override void Update(GameTime gameTime)
    {
        if (Visible && EditingPage)
        {
            Pull();

            if (Document.Version != ShownVersion)
                RefreshEdit();
        }

        base.Update(gameTime);
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            OnCloseClicked();
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    public override void Hide()
    {
        if (!Visible)
            return;

        TitleBox.IsFocused = false;
        QuestionBox.IsFocused = false;

        foreach (var box in AnswerBoxes)
            box.IsFocused = false;

        base.Hide();
    }

    private void OnCloseClicked()
    {
        if (EditingPage)
        {
            Pull();

            if (Document.IsDirty)
                Save(false);
        }

        Hide();
    }

    private void OnBackClicked()
    {
        Pull();

        if (Document.IsDirty)
            Save(true);
        else
            GoToList();
    }

    private void GoToList()
    {
        ShowPage(false);

        //asks for the list again, so a saved title or question count shows
        Raise(new CollegeActionArgs { Type = CollegeActionType.QuizOpen });
    }

    private void Save(bool thenBack)
    {
        Pull();

        //a new quiz's first save is on its way; a second would make a second quiz
        if (AwaitingSave && (Document.Id.Length == 0))
            return;

        AwaitingSave = true;
        BackAfterSave = thenBack;
        SentVersion = Document.Version;
        Status.Text = "Saving...";

        Raise(
            new CollegeActionArgs
            {
                Type = CollegeActionType.QuizSave,
                QuizId = Document.Id,
                QuizDraft = Document.ToInfo()
            });
    }

    private void NewQuiz()
    {
        DeleteArmed = string.Empty;

        if (Quizzes.Count >= CollegeProtocol.MAX_QUIZZES_PER_OWNER)
        {
            Status.Text = "You can keep 20 quizzes.";

            return;
        }

        Document.New();
        AwaitingSave = false;
        BackAfterSave = false;
        Status.Text = string.Empty;
        ShowPage(true);
        TitleBox.IsFocused = true;
    }

    private void OpenSelected()
    {
        DeleteArmed = string.Empty;

        if (SelectedQuiz is { } quiz)
            Raise(new CollegeActionArgs { Type = CollegeActionType.QuizOpen, QuizId = quiz.Id });
    }

    private void CopySelected()
    {
        DeleteArmed = string.Empty;

        if (SelectedQuiz is { } quiz)
            Raise(new CollegeActionArgs { Type = CollegeActionType.QuizCopy, QuizId = quiz.Id });
    }

    private void DeleteSelected()
    {
        if (SelectedQuiz is not { } quiz)
            return;

        if (DeleteArmed != quiz.Id)
        {
            DeleteArmed = quiz.Id;
            Status.Text = $"Delete {quiz.Title}? Click Delete again.";

            return;
        }

        DeleteArmed = string.Empty;
        Raise(new CollegeActionArgs { Type = CollegeActionType.QuizDelete, QuizId = quiz.Id });
    }

    private void SelectRow(int index)
    {
        if (index >= Quizzes.Count)
            return;

        if (index != SelectedRow)
            DeleteArmed = string.Empty;

        SelectedRow = index;
        RefreshList();
    }

    private void SelectQuestion(int index)
    {
        if (index >= Document.Questions.Count)
            return;

        Pull();
        Document.Select(index);
        Push();
    }

    private void MarkRight(int slot)
    {
        Pull();
        Document.MarkRight(slot);
        RefreshEdit();
    }

    private void Edit(Func<bool> change)
    {
        Pull();
        change();
        Push();
    }

    /// <summary>Copies the boxes into the document.</summary>
    private void Pull()
    {
        if (!EditingPage)
            return;

        Document.SetTitle(TitleBox.Text);
        Document.SetText(QuestionBox.Text);

        for (var i = 0; i < AnswerBoxes.Length; i++)
            Document.SetAnswer(i, AnswerBoxes[i].Text);
    }

    /// <summary>Copies the document's title and selected question into the boxes.</summary>
    private void Push()
    {
        TitleBox.Text = Document.Title;
        var current = Document.Current;
        QuestionBox.Text = current?.Text ?? string.Empty;

        for (var i = 0; i < AnswerBoxes.Length; i++)
            AnswerBoxes[i].Text = current?.Answers[i] ?? string.Empty;

        RefreshEdit();
    }

    private void ShowPage(bool edit)
    {
        EditingPage = edit;

        foreach (var element in ListPage)
            element.Visible = !edit;

        foreach (var element in EditPage)
            element.Visible = edit;

        if (edit)
            Push();
        else
            RefreshList();
    }

    private void RefreshList()
    {
        CaptionLabel.Text = $"My quizzes ({Quizzes.Count}/{CollegeProtocol.MAX_QUIZZES_PER_OWNER})";

        for (var i = 0; i < QuizRowsShown.Length; i++)
        {
            var row = QuizRowsShown[i];
            row.Visible = !EditingPage && (i < Quizzes.Count);

            if (!row.Visible)
                continue;

            row.Set(i + 1, (Fit(Quizzes[i].Title, COUNT_X - 8), LegendColors.White), ($"{Quizzes[i].Questions} questions", LegendColors.Gray));
            row.Selected = i == SelectedRow;
        }

        EmptyLabel.Visible = !EditingPage && (Quizzes.Count == 0);
        var selected = SelectedQuiz is not null;
        OpenButton.Enabled = selected;
        CopyButton.Enabled = selected;
        DeleteButton.Enabled = selected;
    }

    private void RefreshEdit()
    {
        ShownVersion = Document.Version;
        CaptionLabel.Text = Document.Title.Length == 0 ? "New quiz" : $"Quiz: {Document.Title}";
        QuestionsLabel.Text = $"Questions ({Document.Questions.Count}/{CollegeProtocol.MAX_QUIZ_QUESTIONS})";

        for (var i = 0; i < QuestionRows.Length; i++)
        {
            var row = QuestionRows[i];
            row.Visible = EditingPage && (i < Document.Questions.Count);

            if (!row.Visible)
                continue;

            row.Set(i + 1, (Fit(Document.Label(i), SIDE_WIDTH - 4), LegendColors.White));
            row.Selected = i == Document.Selected;
        }

        QuestionLabel.Text = $"Question {Document.Selected + 1} ({QuestionBox.Text.Length}/{CollegeProtocol.MAX_QUESTION_CHARS})";
        var right = Document.Current?.Right ?? -1;

        for (var i = 0; i < RightBoxes.Length; i++)
            RightBoxes[i].Checked = i == right;
    }

    private static string Fit(string text, int width)
    {
        if (TextRenderer.MeasureWidth(text) <= width)
            return text;

        while ((text.Length > 0) && (TextRenderer.MeasureWidth(text + "...") > width))
            text = text[..^1];

        return text + "...";
    }

    private void Raise(CollegeActionArgs args) => ActionRequested?.Invoke(args);
}
```

- [ ] **Step 2: Build.** Run the Verify command. Expected: Build succeeded.

```json:metadata
{"files": ["Chaos.Client/Controls/World/Popups/College/QuizEditorControl.cs"], "verifyCommand": "dotnet build Chaos.Client/Chaos.Client.csproj -p:UnoraServerPath=C:\\Users\\Michael\\Documents\\GitHub\\worktrees\\college-tools-server", "acceptanceCriteria": ["page 1 rows, caption n/20, empty line; Open/Copy/Delete off without a selection", "Delete asks with a second click", "New quiz opens one blank question or says 20 limit", "OpenQuiz loads page 2; question list actions keep boxes in step", "Save sends id (empty for new) and blocks a second save of a new quiz before its id", "ShowResult: message; saved stores id and marks clean only if unchanged; refused re-shows hidden editor and returns true", "Back/OK/Esc with changes save first; SaveIfDirty for logout"], "modelTier": "standard"}
```

---

### Task 9: Client quiz card and Teacher panel (client repo)

**Goal:** Students see a centred quiz card with the question, a timer bar and the answers; after the reveal it marks the right answer and their pick. The class's Teacher sees a small side panel with the question, its answer, the answered count and **Next question** / **End quiz**.

**Files:**
- Create: `Chaos.Client/Controls/World/Popups/College/QuizCardControl.cs`
- Create: `Chaos.Client/Controls/World/Popups/College/QuizTeacherPanel.cs`

**Acceptance Criteria:**
- [ ] The card shows "Quiz: {title} ({n}/{count})", the question wrapped to three lines, a timer bar and "N seconds left. You can change your answer.", and one button per answer ("A. ..."); the picked answer is lit
- [ ] Clicking an answer while the question is open and time is left sends `QuizAnswer` with the question number; answers are off once time is up or the answer is shown
- [ ] After the reveal the right answer has a green outline and a wrong pick a red one; three lines show the student's result, the right count and the top 3 (the viewer as "you")
- [ ] OK or Esc hides the card until the next question; `Reopen` shows it again; the Waiting and Ended phases hide it; `Close` hides it and forgets the quiz
- [ ] The Teacher panel shows the title and number, the question (two lines), the right answer in green, "Answered 7 of 9 - 0:14 left" while open or the right count after the reveal, and the top 3
- [ ] **Next question** reads "First question" before the first, is off while a question is open, and reads "Finish quiz" after the last reveal (sending `End`); **End quiz** asks with a second click
- [ ] The Teacher panel doesn't take keyboard focus; OK hides it until `Reopen`; `Place` puts it at the viewport's top right

**Verify:** `dotnet build Chaos.Client/Chaos.Client.csproj -p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\college-tools-server` -> Build succeeded

**Steps:**

- [ ] **Step 1: Write the card.** Create `Chaos.Client/Controls/World/Popups/College/QuizCardControl.cs`:

```csharp
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.Extensions;
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>
///     The student's quiz card in the middle of the view: the question, a timer bar and up to four answers. After the
///     reveal it outlines the right answer and the student's pick and shows the class's top scores. OK or Esc hides it
///     until the next question.
/// </summary>
public sealed class QuizCardControl : GuildCloakDialogBase
{
    private const int WIDTH = 370;
    private const int HEIGHT = 300;
    private const int LEFT = 16;
    private const int INNER_WIDTH = WIDTH - (2 * LEFT);
    private const int CAPTION_TOP = 10;
    private const int LINE_HEIGHT = TextRenderer.CHAR_HEIGHT + 2;
    private const int QUESTION_TOP = 28;
    private const int QUESTION_LINES = 3;
    private const int BAR_TOP = QUESTION_TOP + (QUESTION_LINES * LINE_HEIGHT) + 4;
    private const int BAR_HEIGHT = 4;
    private const int TIMER_TOP = BAR_TOP + 8;
    private const int ANSWERS_TOP = TIMER_TOP + LINE_HEIGHT + 4;
    private const int ANSWER_STEP = CustomButton.HEIGHT + 4;
    private const int RESULT_TOP = ANSWERS_TOP + (CollegeProtocol.MAX_ANSWERS * ANSWER_STEP) + 2;
    private const int RESULT_LINES = 3;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;

    private readonly CustomButton[] AnswerButtons = new CustomButton[CollegeProtocol.MAX_ANSWERS];
    private readonly UILabel CaptionLabel;
    private readonly ToolCountdown Countdown = new();
    private readonly UILabel[] QuestionLines = new UILabel[QUESTION_LINES];
    private readonly UILabel[] ResultLines = new UILabel[RESULT_LINES];
    private readonly UILabel TimerLabel;
    private int DismissedNumber;
    private CollegeQuizInfo? Quiz;
    private string Viewer = string.Empty;

    public event Action<CollegeActionArgs>? ActionRequested;

    public QuizCardControl()
        : base("_nsett", false)
    {
        Name = "QuizCard";
        Visible = false;
        UsesControlStack = true;
        Width = WIDTH;
        Height = HEIGHT;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(Dismiss, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);
        CaptionLabel = Caption(string.Empty, LEFT, CAPTION_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gold);

        for (var i = 0; i < QUESTION_LINES; i++)
            QuestionLines[i] = Caption(string.Empty, LEFT, QUESTION_TOP + (i * LINE_HEIGHT), INNER_WIDTH);

        TimerLabel = Caption(string.Empty, LEFT, TIMER_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gray);

        for (var i = 0; i < AnswerButtons.Length; i++)
        {
            var slot = i;
            AnswerButtons[i] = AddButton(string.Empty, INNER_WIDTH, LEFT, ANSWERS_TOP + (i * ANSWER_STEP), () => Pick(slot));
        }

        for (var i = 0; i < RESULT_LINES; i++)
            ResultLines[i] = Caption(string.Empty, LEFT, RESULT_TOP + (i * LINE_HEIGHT), INNER_WIDTH, HorizontalAlignment.Center);
    }

    /// <summary>A QuizCard message. <paramref name="reopen" /> shows a card the student closed.</summary>
    public void Apply(CollegeQuizInfo quiz, string viewer, bool reopen)
    {
        Quiz = quiz;
        Viewer = viewer;
        Countdown.Start(quiz.SecondsLeft);

        if (reopen)
            DismissedNumber = 0;

        if (quiz.Phase is QuizPhase.Waiting or QuizPhase.Ended)
        {
            Hide();

            return;
        }

        Refresh();

        if (!Visible && (DismissedNumber != quiz.Number))
            Show();
    }

    public void Close()
    {
        Quiz = null;
        DismissedNumber = 0;
        Hide();
    }

    public override void Update(GameTime gameTime)
    {
        if (Visible && Quiz is { Phase: QuizPhase.Open })
        {
            Countdown.Advance(gameTime.ElapsedGameTime.TotalSeconds);
            RefreshTimer();
        }

        base.Update(gameTime);
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            Dismiss();
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);

        if (!Visible || Quiz is not { } quiz)
            return;

        var bar = new Rectangle(ScreenX + LEFT, ScreenY + BAR_TOP, INNER_WIDTH, BAR_HEIGHT);
        DrawRect(spriteBatch, bar, LegendColors.DimGray);

        if (quiz is { Phase: QuizPhase.Open, Seconds: > 0 })
            DrawRect(spriteBatch, bar with { Width = INNER_WIDTH * Countdown.Left / quiz.Seconds }, LegendColors.Gold);

        if (quiz.Phase != QuizPhase.Revealed)
            return;

        Outline(spriteBatch, quiz.Right, LegendColors.Lime);

        if (quiz.MyAnswer != quiz.Right)
            Outline(spriteBatch, quiz.MyAnswer, LegendColors.Red);
    }

    private void Dismiss()
    {
        if (Quiz is { } quiz)
            DismissedNumber = quiz.Number;

        Hide();
    }

    private void Pick(int slot)
    {
        if (Quiz is not { Phase: QuizPhase.Open } quiz || (Countdown.Left == 0))
            return;

        quiz.MyAnswer = (byte)slot;
        Refresh();

        ActionRequested?.Invoke(
            new CollegeActionArgs
            {
                Type = CollegeActionType.QuizAnswer,
                Number = quiz.Number,
                Answer = (byte)slot
            });
    }

    private void Refresh()
    {
        if (Quiz is not { } quiz)
            return;

        CaptionLabel.Text = ClassToolText.QuizCaption(quiz);
        var lines = TextRenderer.WrapLines(quiz.Question, INNER_WIDTH);

        for (var i = 0; i < QUESTION_LINES; i++)
            QuestionLines[i].Text = i >= lines.Count ? string.Empty
                : (i == QUESTION_LINES - 1) && (lines.Count > QUESTION_LINES) ? lines[i] + "..."
                : lines[i];

        for (var i = 0; i < AnswerButtons.Length; i++)
        {
            var button = AnswerButtons[i];
            button.Visible = i < quiz.Answers.Count;

            if (!button.Visible)
                continue;

            button.Caption = $"{(char)('A' + i)}. {quiz.Answers[i]}";
            button.Selected = quiz.MyAnswer == i;
        }

        var revealed = quiz.Phase == QuizPhase.Revealed;
        ResultLines[0].Text = revealed ? ClassToolText.MyResult(quiz.MyAnswer, quiz.Right, quiz.MyScore) : string.Empty;
        ResultLines[1].Text = revealed ? ClassToolText.RightLine(quiz.RightCount, quiz.AnsweredCount) : string.Empty;
        ResultLines[2].Text = revealed ? ClassToolText.TopLine(quiz.Top, Viewer) : string.Empty;
        RefreshTimer();
    }

    private void RefreshTimer()
    {
        if (Quiz is not { } quiz)
            return;

        var answering = (quiz.Phase == QuizPhase.Open) && (Countdown.Left > 0);
        TimerLabel.Text = answering ? $"{Countdown.Left} seconds left. You can change your answer." : "Time is up.";

        foreach (var button in AnswerButtons)
            button.Enabled = answering;
    }

    private void Outline(SpriteBatch spriteBatch, byte slot, Color color)
    {
        if ((slot >= AnswerButtons.Length) || !AnswerButtons[slot].Visible)
            return;

        var button = AnswerButtons[slot];
        var outer = new Rectangle(button.ScreenX - 2, button.ScreenY - 2, button.Width + 4, button.Height + 4);
        DrawBorder(spriteBatch, outer, color);
        DrawBorder(spriteBatch, new Rectangle(outer.X + 1, outer.Y + 1, outer.Width - 2, outer.Height - 2), color);
    }
}
```

- [ ] **Step 2: Write the Teacher panel.** Create `Chaos.Client/Controls/World/Popups/College/QuizTeacherPanel.cs`:

```csharp
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>
///     The class Teacher's quiz panel at the top right of the view: the question and its answer, how many have answered,
///     and the buttons that run the quiz. It never takes keyboard focus, so the Teacher can keep moving and talking.
/// </summary>
public sealed class QuizTeacherPanel : GuildCloakDialogBase
{
    private const int WIDTH = 250;
    private const int HEIGHT = 200;
    private const int LEFT = 16;
    private const int INNER_WIDTH = WIDTH - (2 * LEFT);
    private const int LINE_HEIGHT = TextRenderer.CHAR_HEIGHT + 2;
    private const int CAPTION_TOP = 10;
    private const int QUESTION_TOP = 28;
    private const int QUESTION_LINES = 2;
    private const int RIGHT_TOP = QUESTION_TOP + (QUESTION_LINES * LINE_HEIGHT) + 2;
    private const int STATUS_TOP = RIGHT_TOP + LINE_HEIGHT;
    private const int TOP_TOP = STATUS_TOP + LINE_HEIGHT;
    private const int ARMED_TOP = TOP_TOP + LINE_HEIGHT;
    private const int BUTTONS_TOP = HEIGHT - BORDER_BOTTOM_HEIGHT - 4 - CustomButton.HEIGHT;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;

    private readonly UILabel ArmedLabel;
    private readonly UILabel CaptionLabel;
    private readonly ToolCountdown Countdown = new();
    private readonly CustomButton EndButton;
    private readonly CustomButton NextButton;
    private readonly UILabel[] QuestionLines = new UILabel[QUESTION_LINES];
    private readonly UILabel RightLabel;
    private readonly UILabel StatusLabel;
    private readonly UILabel TopLabel;
    private bool Dismissed;
    private bool EndArmed;
    private CollegeQuizInfo? Quiz;

    public event Action<CollegeActionArgs>? ActionRequested;

    public QuizTeacherPanel()
        : base("_nsett", false)
    {
        Name = "QuizTeacherPanel";
        Visible = false;
        UsesControlStack = false;
        Width = WIDTH;
        Height = HEIGHT;

        OkButton = CreateCloseButton(Dismiss, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);
        CaptionLabel = Caption(string.Empty, LEFT, CAPTION_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gold);

        for (var i = 0; i < QUESTION_LINES; i++)
            QuestionLines[i] = Caption(string.Empty, LEFT, QUESTION_TOP + (i * LINE_HEIGHT), INNER_WIDTH);

        RightLabel = Caption(string.Empty, LEFT, RIGHT_TOP, INNER_WIDTH, color: LegendColors.Lime);
        StatusLabel = Caption(string.Empty, LEFT, STATUS_TOP, INNER_WIDTH, color: LegendColors.Gray);
        TopLabel = Caption(string.Empty, LEFT, TOP_TOP, INNER_WIDTH);
        ArmedLabel = Caption(string.Empty, LEFT, ARMED_TOP, INNER_WIDTH, color: LegendColors.Gold);
        NextButton = AddButton("Next question", 110, LEFT, BUTTONS_TOP, OnNext);
        EndButton = AddButton("End quiz", 80, LEFT + 116, BUTTONS_TOP, OnEnd);
    }

    /// <summary>Puts the panel at the viewport's top right, under the poll box when one shows.</summary>
    public void Place(Rectangle viewport, int top)
    {
        X = viewport.Right - Width - 2;
        Y = top;
    }

    /// <summary>A QuizTeacher message. <paramref name="reopen" /> shows a panel the Teacher closed.</summary>
    public void Apply(CollegeQuizInfo quiz, bool reopen)
    {
        if (reopen)
            Dismissed = false;

        if (quiz.Phase == QuizPhase.Ended)
        {
            Close();

            return;
        }

        Quiz = quiz;
        Countdown.Start(quiz.SecondsLeft);
        EndArmed = false;
        Refresh();

        if (!Visible && !Dismissed)
            Show();
    }

    public void Close()
    {
        Quiz = null;
        Dismissed = false;
        EndArmed = false;
        Hide();
    }

    public override void Update(GameTime gameTime)
    {
        if (Visible && Quiz is { Phase: QuizPhase.Open } quiz)
        {
            Countdown.Advance(gameTime.ElapsedGameTime.TotalSeconds);
            StatusLabel.Text = ClassToolText.AnsweredLine(quiz.AnsweredCount, quiz.Students, Countdown.Left);
        }

        base.Update(gameTime);
    }

    private void Dismiss()
    {
        Dismissed = true;
        Hide();
    }

    private bool IsLastRevealed(CollegeQuizInfo quiz) => (quiz.Phase == QuizPhase.Revealed) && (quiz.Number >= quiz.Count);

    private void OnNext()
    {
        if (Quiz is not { } quiz || (quiz.Phase == QuizPhase.Open))
            return;

        Raise(IsLastRevealed(quiz) ? QuizControlType.End : QuizControlType.Next);
    }

    private void OnEnd()
    {
        if (!EndArmed)
        {
            EndArmed = true;
            ArmedLabel.Text = "Click End quiz again to end it.";

            return;
        }

        EndArmed = false;
        ArmedLabel.Text = string.Empty;
        Raise(QuizControlType.End);
    }

    private void Raise(QuizControlType control)
        => ActionRequested?.Invoke(new CollegeActionArgs { Type = CollegeActionType.QuizControl, QuizControl = control });

    private void Refresh()
    {
        if (Quiz is not { } quiz)
            return;

        var waiting = quiz.Phase == QuizPhase.Waiting;
        CaptionLabel.Text = $"{quiz.Title} {quiz.Number}/{quiz.Count}";
        List<string> lines = waiting ? ["Ask the first question when you are ready."] : TextRenderer.WrapLines(quiz.Question, INNER_WIDTH);

        for (var i = 0; i < QUESTION_LINES; i++)
            QuestionLines[i].Text = i >= lines.Count ? string.Empty
                : (i == QUESTION_LINES - 1) && (lines.Count > QUESTION_LINES) ? lines[i] + "..."
                : lines[i];

        RightLabel.Text = !waiting && (quiz.Right < quiz.Answers.Count) ? $"{(char)('A' + quiz.Right)}. {quiz.Answers[quiz.Right]}" : string.Empty;

        StatusLabel.Text = quiz.Phase switch
        {
            QuizPhase.Open     => ClassToolText.AnsweredLine(quiz.AnsweredCount, quiz.Students, Countdown.Left),
            QuizPhase.Revealed => ClassToolText.RightLine(quiz.RightCount, quiz.AnsweredCount),
            _                  => string.Empty
        };

        TopLabel.Text = quiz.Phase == QuizPhase.Revealed ? ClassToolText.TopLine(quiz.Top, string.Empty) : string.Empty;
        ArmedLabel.Text = string.Empty;
        NextButton.Caption = waiting ? "First question" : IsLastRevealed(quiz) ? "Finish quiz" : "Next question";
        NextButton.Enabled = quiz.Phase != QuizPhase.Open;
    }
}
```

- [ ] **Step 3: Build.** Run the Verify command. Expected: Build succeeded.

```json:metadata
{"files": ["Chaos.Client/Controls/World/Popups/College/QuizCardControl.cs", "Chaos.Client/Controls/World/Popups/College/QuizTeacherPanel.cs"], "verifyCommand": "dotnet build Chaos.Client/Chaos.Client.csproj -p:UnoraServerPath=C:\\Users\\Michael\\Documents\\GitHub\\worktrees\\college-tools-server", "acceptanceCriteria": ["card: caption, 3 question lines, timer bar and line, A-D answer buttons, pick lit", "clicking an open answer with time left sends QuizAnswer; answers off when time is up or revealed", "reveal: green outline on right answer, red on a wrong pick; result, right count, top 3 with 'you'", "OK/Esc hides until next question; Reopen shows; Waiting/Ended hide; Close forgets", "Teacher panel: title n/count, 2 question lines, right answer green, answered line or right count, top 3", "Next reads First question / off while open / Finish quiz after last; End quiz needs a second click", "panel takes no keyboard focus; OK hides until Reopen; Place at viewport top right"], "modelTier": "standard"}
```

---

### Task 10: Client debate panel, vote card and side markers (client repo)

**Goal:** Students and the Teacher see a debate panel at the top right; students also get a centred card for the opening and final votes. Side markers show over each head in the room.

**Files:**
- Create: `Chaos.Client/Controls/World/Popups/College/DebatePanel.cs`
- Create: `Chaos.Client/Controls/World/Popups/College/DebateVoteCard.cs`
- Create: `Chaos.Client/Systems/College/DebateMarkTable.cs`
- Modify: `Chaos.Client/Rendering/EntityOverlayManager.cs`
- Test: new `Tests/Chaos.Client.Tests/College/DebateMarkTableTests.cs`

**Acceptance Criteria:**
- [ ] `DebateMarkTable.Set` replaces every marker and skips players with no side; `Clear` empties it
- [ ] Student panel: motion (three lines), counts, the floor line (or the vote's clock, or the result), **For** / **Against** / **Undecided** with the chosen one lit, **Raise hand** / **Lower hand** (only on the floor phase), and "Only {name} and the Teacher can speak." while someone holds the floor
- [ ] Teacher panel: counts with gains (plain counts during the opening vote), the floor line with **Take back**, the first 3 raised hands each with **Give floor** and "+N more", **Final vote** (only on the floor phase) and **End debate** (second click to confirm)
- [ ] In the result phase with the bonus on, the Teacher picks up to 3 speakers with tick boxes and **Confirm** sends `PickSpeakers`
- [ ] The vote card shows for students during the opening and final votes with the clock, motion, counts and side buttons; in the final vote it also shows the opening counts and who would win now; OK/Esc hides it for that vote
- [ ] Markers draw 86 px above each marked player's tile: "For" (green), "Against" (red), "?" (grey), with " *" for the floor holder; the marker cache clears with the other overlays

**Verify:** `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\college-tools-server -- --treenode-filter "/*/*/DebateMarkTableTests/*"` -> all pass; the client build succeeds

**Steps:**

- [ ] **Step 1: Write the failing marker table test.** Create `Tests/Chaos.Client.Tests/College/DebateMarkTableTests.cs`:

```csharp
using Chaos.Client.Systems.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class DebateMarkTableTests
{
    [Test]
    public void Set_replaces_every_marker_and_skips_players_with_no_side()
    {
        var table = new DebateMarkTable();
        table.Set([new CollegeDebateMarkInfo { EntityId = 9, Side = DebateSide.For }]);

        table.Set(
            [
                new CollegeDebateMarkInfo { EntityId = 1, Side = DebateSide.Against, Floor = true },
                new CollegeDebateMarkInfo { EntityId = 2, Side = DebateSide.None }
            ]);

        table.All.Keys.Should().Equal(1u);
        table.All[1].Floor.Should().BeTrue();
        table.Count.Should().Be(1);
    }

    [Test]
    public void Clear_empties_it()
    {
        var table = new DebateMarkTable();
        table.Set([new CollegeDebateMarkInfo { EntityId = 1, Side = DebateSide.For }]);

        table.Clear();

        table.Count.Should().Be(0);
    }
}
```

- [ ] **Step 2: Run it to see it fail.** Run the Verify test command. Expected: compile error (`DebateMarkTable` doesn't exist).

- [ ] **Step 3: Add the marker table.** Create `Chaos.Client/Systems/College/DebateMarkTable.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;

namespace Chaos.Client.Systems.College;

/// <summary>The debate side markers for the players in the room, by entity id. Each DebateMarks message replaces them all.</summary>
public sealed class DebateMarkTable
{
    private readonly Dictionary<uint, CollegeDebateMarkInfo> Marks = [];

    public IReadOnlyDictionary<uint, CollegeDebateMarkInfo> All => Marks;
    public int Count => Marks.Count;

    public void Set(IReadOnlyList<CollegeDebateMarkInfo> marks)
    {
        Marks.Clear();

        foreach (var mark in marks)
            if (mark.Side != DebateSide.None)
                Marks[mark.EntityId] = mark;
    }

    public void Clear() => Marks.Clear();
}
```

- [ ] **Step 4: Draw the markers.** In `Chaos.Client/Rendering/EntityOverlayManager.cs`:
  - add `using Chaos.Client.ViewModel.College;` and `using Chaos.Networking.Entities.Server;`;
  - add after `GROUP_BOX_Y_OFFSET`:

```csharp

    //debate side markers sit above the name tag
    private const int DEBATE_MARK_Y_OFFSET = 86;
```

  - add a field next to `NameTagCache`: `private readonly Dictionary<uint, TextElement> DebateMarkCache = [];`
  - in `Clear()`, add `DebateMarkCache.Clear();` after `NameTagCache.Clear();`;
  - add this method after `DrawNameTags`:

```csharp
    /// <summary>Draws the debate side markers. Call within the same camera-transformed batch as <see cref="Draw" />.</summary>
    public void DrawDebateMarks(SpriteBatch spriteBatch, Camera camera, int mapHeight, IReadOnlyDictionary<uint, CollegeDebateMarkInfo> marks)
    {
        foreach ((var id, var mark) in marks)
        {
            if (WorldState.GetEntity(id) is not { } entity)
                continue;

            if (!DebateMarkCache.TryGetValue(id, out var text))
            {
                text = new TextElement
                {
                    ShadowStyle = ShadowStyle.BothSides,
                    ShadowColor = NAME_TAG_SHADOW_COLOR
                };
                DebateMarkCache[id] = text;
            }

            text.Update(ClassToolText.Marker(mark.Side, mark.Floor), MarkColor(mark.Side));

            if (!text.HasContent)
                continue;

            var tileWorld = Camera.TileToWorld(entity.TileX, entity.TileY, mapHeight);
            var worldX = tileWorld.X + DaLibConstants.HALF_TILE_WIDTH + entity.VisualOffset.X;
            var worldY = tileWorld.Y + DaLibConstants.HALF_TILE_HEIGHT + entity.VisualOffset.Y - DEBATE_MARK_Y_OFFSET;

            text.Draw(spriteBatch, camera.WorldToScreen(new Vector2(worldX - text.Width / 2, worldY)));
        }
    }

    private static Color MarkColor(DebateSide side)
        => side switch
        {
            DebateSide.For     => LegendColors.Lime,
            DebateSide.Against => LegendColors.Red,
            _                  => LegendColors.Gray
        };
```

- [ ] **Step 5: Write the debate panel.** Create `Chaos.Client/Controls/World/Popups/College/DebatePanel.cs`:

```csharp
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>
///     The debate panel at the top right of the view. Students pick a side and raise a hand; the Teacher's view lists the
///     raised hands, gives and takes back the floor, starts the final vote and picks the best speakers. It never takes
///     keyboard focus.
/// </summary>
public sealed class DebatePanel : GuildCloakDialogBase
{
    private const int WIDTH = 260;
    private const int STUDENT_HEIGHT = 224;
    private const int TEACHER_HEIGHT = 296;
    private const int LEFT = 16;
    private const int INNER_WIDTH = WIDTH - (2 * LEFT);
    private const int LINE_HEIGHT = TextRenderer.CHAR_HEIGHT + 2;
    private const int CAPTION_TOP = 10;
    private const int MOTION_TOP = 28;
    private const int MOTION_LINES = 3;
    private const int COUNTS_TOP = MOTION_TOP + (MOTION_LINES * LINE_HEIGHT) + 2;
    private const int FLOOR_TOP = COUNTS_TOP + LINE_HEIGHT + 4;
    private const int ROW_TOP = FLOOR_TOP + LINE_HEIGHT + 8;
    private const int HAND_TOP = ROW_TOP + CustomButton.HEIGHT + 4;
    private const int HINT_TOP = HAND_TOP + CustomButton.HEIGHT + 4;
    private const int HAND_ROWS = 3;
    private const int HAND_STEP = CustomButton.HEIGHT + 4;
    private const int HANDS_TOP = ROW_TOP + LINE_HEIGHT + 2;
    private const int MORE_TOP = HANDS_TOP + (HAND_ROWS * HAND_STEP);
    private const int SPEAKER_COLUMNS = 2;
    private const int SPEAKER_ROWS = 4;
    private const int SPEAKER_STEP = 22;
    private const int SPEAKER_WIDTH = INNER_WIDTH / SPEAKER_COLUMNS;
    private const int TEACHER_BUTTONS_TOP = TEACHER_HEIGHT - BORDER_BOTTOM_HEIGHT - 4 - CustomButton.HEIGHT;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;

    private readonly CustomButton AgainstButton;
    private readonly UILabel ArmedLabel;
    private readonly UILabel CaptionLabel;
    private readonly CustomButton ConfirmButton;
    private readonly ToolCountdown Countdown = new();
    private readonly UILabel CountsLabel;
    private readonly CustomButton EndButton;
    private readonly CustomButton FinalButton;
    private readonly UILabel FloorLabel;
    private readonly CustomButton ForButton;
    private readonly CustomButton HandButton;
    private readonly UILabel[] HandNames = new UILabel[HAND_ROWS];
    private readonly CustomButton[] GiveButtons = new CustomButton[HAND_ROWS];
    private readonly UILabel HandsLabel;
    private readonly UILabel HintLabel;
    private readonly UILabel MoreLabel;
    private readonly UILabel[] MotionLines = new UILabel[MOTION_LINES];
    private readonly CustomCheckBox[] SpeakerBoxes = new CustomCheckBox[SPEAKER_COLUMNS * SPEAKER_ROWS];
    private readonly List<UIElement> StudentView = [];
    private readonly CustomButton TakeBackButton;
    private readonly List<UIElement> TeacherView = [];
    private readonly CustomButton UndecidedButton;
    private CollegeDebateInfo? Debate;
    private bool Dismissed;
    private bool EndArmed;

    public event Action<CollegeActionArgs>? ActionRequested;

    public DebatePanel()
        : base("_nsett", false)
    {
        Name = "DebatePanel";
        Visible = false;
        UsesControlStack = false;
        Width = WIDTH;
        Height = TEACHER_HEIGHT;

        OkButton = CreateCloseButton(Dismiss, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);
        CaptionLabel = Caption(string.Empty, LEFT, CAPTION_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gold);

        for (var i = 0; i < MOTION_LINES; i++)
            MotionLines[i] = Caption(string.Empty, LEFT, MOTION_TOP + (i * LINE_HEIGHT), INNER_WIDTH);

        CountsLabel = Caption(string.Empty, LEFT, COUNTS_TOP, INNER_WIDTH);
        FloorLabel = Caption(string.Empty, LEFT, FLOOR_TOP, INNER_WIDTH - 72, color: LegendColors.Gold);

        //student view
        ForButton = AddButton("For", 66, LEFT, ROW_TOP, () => PickSide(DebateSide.For));
        AgainstButton = AddButton("Against", 72, LEFT + 70, ROW_TOP, () => PickSide(DebateSide.Against));
        UndecidedButton = AddButton("Undecided", 82, LEFT + 146, ROW_TOP, () => PickSide(DebateSide.Undecided));
        HandButton = AddButton("Raise hand", 100, LEFT, HAND_TOP, ToggleHand);
        HintLabel = Caption(string.Empty, LEFT, HINT_TOP, INNER_WIDTH, color: LegendColors.Gray);
        StudentView.AddRange([ForButton, AgainstButton, UndecidedButton, HandButton, HintLabel]);

        //teacher view
        TakeBackButton = AddButton("Take back", 68, LEFT + INNER_WIDTH - 68, FLOOR_TOP - 5, () => Raise(DebateControlType.TakeBack));
        HandsLabel = Caption("Hands up:", LEFT, ROW_TOP, INNER_WIDTH, color: LegendColors.Gold);
        TeacherView.AddRange([TakeBackButton, HandsLabel]);

        for (var i = 0; i < HAND_ROWS; i++)
        {
            var index = i;
            var y = HANDS_TOP + (i * HAND_STEP);
            HandNames[i] = Caption(string.Empty, LEFT, y + 5, INNER_WIDTH - 90);
            GiveButtons[i] = AddButton("Give floor", 84, LEFT + INNER_WIDTH - 84, y, () => GiveFloor(index));
            TeacherView.AddRange([HandNames[i], GiveButtons[i]]);
        }

        MoreLabel = Caption(string.Empty, LEFT, MORE_TOP, 100, color: LegendColors.Gray);
        ArmedLabel = Caption(string.Empty, LEFT + 104, MORE_TOP, INNER_WIDTH - 104, color: LegendColors.Gold);
        TeacherView.AddRange([MoreLabel, ArmedLabel]);

        for (var i = 0; i < SpeakerBoxes.Length; i++)
        {
            var box = new CustomCheckBox
            {
                X = LEFT + ((i % SPEAKER_COLUMNS) * SPEAKER_WIDTH),
                Y = HANDS_TOP + ((i / SPEAKER_COLUMNS) * SPEAKER_STEP),
                Width = SPEAKER_WIDTH - 4,
                Height = CustomCheckBox.CHECKBOX_SIZE
            };

            box.Clicked += () => ToggleSpeaker(box);
            AddChild(box);
            SpeakerBoxes[i] = box;
            TeacherView.Add(box);
        }

        FinalButton = AddButton("Final vote", 90, LEFT, TEACHER_BUTTONS_TOP, () => Raise(DebateControlType.FinalVote));
        ConfirmButton = AddButton("Confirm", 90, LEFT, TEACHER_BUTTONS_TOP, ConfirmSpeakers);
        EndButton = AddButton("End debate", 90, LEFT + 96, TEACHER_BUTTONS_TOP, OnEnd);
        TeacherView.AddRange([FinalButton, ConfirmButton, EndButton]);
    }

    /// <summary>Puts the panel at the viewport's top right, under the poll box when one shows.</summary>
    public void Place(Rectangle viewport, int top)
    {
        X = viewport.Right - Width - 2;
        Y = top;
    }

    /// <summary>A DebateState message. <paramref name="reopen" /> shows a panel the player closed.</summary>
    public void Apply(CollegeDebateInfo debate, bool reopen)
    {
        if (reopen)
            Dismissed = false;

        if (debate.Phase == DebatePhase.Ended)
        {
            Close();

            return;
        }

        var phaseChanged = Debate?.Phase != debate.Phase;
        Debate = debate;
        Countdown.Start(debate.Phase is DebatePhase.Opening or DebatePhase.Final ? debate.SecondsLeft : debate.FloorSecondsLeft);

        if (phaseChanged)
        {
            EndArmed = false;

            foreach (var box in SpeakerBoxes)
                box.Checked = false;
        }

        Resize(debate.IsTeacher ? TEACHER_HEIGHT : STUDENT_HEIGHT);
        Refresh();

        if (!Visible && !Dismissed)
            Show();
    }

    public void Close()
    {
        Debate = null;
        Dismissed = false;
        EndArmed = false;
        Hide();
    }

    public override void Update(GameTime gameTime)
    {
        if (Visible && Debate is { } debate)
        {
            Countdown.Advance(gameTime.ElapsedGameTime.TotalSeconds);
            FloorLabel.Text = FloorText(debate);
        }

        base.Update(gameTime);
    }

    private string FloorText(CollegeDebateInfo debate)
        => debate.Phase switch
        {
            DebatePhase.Opening or DebatePhase.Final => ClassToolText.VoteCaption(debate.Phase, Countdown.Left),
            DebatePhase.Result                       => ClassToolText.ResultLine(debate.Result, debate),
            _                                        => ClassToolText.FloorLine(debate.Floor, Countdown.Left)
        };

    private void Resize(int height)
    {
        if (Height == height)
            return;

        //the OK button was placed from the bottom edge, so it moves with it
        if (OkButton is not null)
            OkButton.Y += height - Height;

        Height = height;
    }

    private void Refresh()
    {
        if (Debate is not { } debate)
            return;

        var teacher = debate.IsTeacher;
        CaptionLabel.Text = teacher ? "Debate (Teacher)" : "Debate";
        var lines = TextRenderer.WrapLines($"\"{debate.Motion}\"", INNER_WIDTH);

        for (var i = 0; i < MOTION_LINES; i++)
            MotionLines[i].Text = i >= lines.Count ? string.Empty
                : (i == MOTION_LINES - 1) && (lines.Count > MOTION_LINES) ? lines[i] + "..."
                : lines[i];

        CountsLabel.Text = teacher && (debate.Phase != DebatePhase.Opening) ? ClassToolText.CountsWithGains(debate) : ClassToolText.Counts(debate);
        FloorLabel.Text = FloorText(debate);

        foreach (var element in StudentView)
            element.Visible = !teacher;

        foreach (var element in TeacherView)
            element.Visible = teacher;

        if (teacher)
            RefreshTeacher(debate);
        else
            RefreshStudent(debate);
    }

    private void RefreshStudent(CollegeDebateInfo debate)
    {
        var voting = debate.Phase is DebatePhase.Opening or DebatePhase.Floor or DebatePhase.Final;
        ForButton.Selected = debate.MySide == DebateSide.For;
        AgainstButton.Selected = debate.MySide == DebateSide.Against;
        UndecidedButton.Selected = debate.MySide == DebateSide.Undecided;
        ForButton.Enabled = AgainstButton.Enabled = UndecidedButton.Enabled = voting;
        HandButton.Caption = debate.MyHand ? "Lower hand" : "Raise hand";
        HandButton.Enabled = debate.Phase == DebatePhase.Floor;
        HintLabel.Text = (debate.Phase == DebatePhase.Floor) && (debate.Floor.Length > 0) ? $"Only {debate.Floor} and the Teacher can speak." : string.Empty;
    }

    private void RefreshTeacher(CollegeDebateInfo debate)
    {
        var onFloor = debate.Phase == DebatePhase.Floor;
        var picking = (debate.Phase == DebatePhase.Result) && debate.Bonus;
        TakeBackButton.Visible = onFloor && (debate.Floor.Length > 0);
        HandsLabel.Text = picking ? "Pick up to 3 best speakers:" : "Hands up:";
        HandsLabel.Visible = onFloor || picking;

        for (var i = 0; i < HAND_ROWS; i++)
        {
            var shown = onFloor && (i < debate.Hands.Count);
            HandNames[i].Visible = shown;
            GiveButtons[i].Visible = shown;
            HandNames[i].Text = shown ? debate.Hands[i] : string.Empty;
        }

        MoreLabel.Text = onFloor ? ClassToolText.HandsMore(HAND_ROWS, debate.Hands.Count) : string.Empty;

        for (var i = 0; i < SpeakerBoxes.Length; i++)
        {
            var shown = picking && (i < debate.Speakers.Count);
            SpeakerBoxes[i].Visible = shown;
            SpeakerBoxes[i].Text = shown ? debate.Speakers[i] : string.Empty;
        }

        FinalButton.Visible = !picking;
        FinalButton.Enabled = onFloor;
        ConfirmButton.Visible = picking;
        ArmedLabel.Text = EndArmed ? "Click again to end." : string.Empty;
    }

    private void Dismiss()
    {
        Dismissed = true;
        Hide();
    }

    private void PickSide(DebateSide side)
    {
        if (Debate is not { } debate)
            return;

        debate.MySide = side;
        Refresh();
        ActionRequested?.Invoke(new CollegeActionArgs { Type = CollegeActionType.DebateSide, Side = side });
    }

    private void ToggleHand()
    {
        if (Debate is not { Phase: DebatePhase.Floor } debate)
            return;

        debate.MyHand = !debate.MyHand;
        Refresh();
        ActionRequested?.Invoke(new CollegeActionArgs { Type = CollegeActionType.DebateHand, Raised = debate.MyHand });
    }

    private void GiveFloor(int index)
    {
        if (Debate is not { } debate || (index >= debate.Hands.Count))
            return;

        Raise(DebateControlType.GiveFloor, [debate.Hands[index]]);
    }

    private void ToggleSpeaker(CustomCheckBox box)
    {
        if (!box.Checked && (SpeakerBoxes.Count(b => b.Checked) >= CollegeProtocol.MAX_BONUS_STUDENTS))
            return;

        box.Checked = !box.Checked;
    }

    private void ConfirmSpeakers()
        => Raise(DebateControlType.PickSpeakers, SpeakerBoxes.Where(b => b.Visible && b.Checked).Select(b => b.Text).ToList());

    private void OnEnd()
    {
        if (!EndArmed)
        {
            EndArmed = true;
            ArmedLabel.Text = "Click again to end.";

            return;
        }

        EndArmed = false;
        ArmedLabel.Text = string.Empty;
        Raise(DebateControlType.End);
    }

    private void Raise(DebateControlType control, List<string>? names = null)
        => ActionRequested?.Invoke(
            new CollegeActionArgs
            {
                Type = CollegeActionType.DebateControl,
                DebateControl = control,
                Names = names ?? []
            });
}
```

- [ ] **Step 6: Write the vote card.** Create `Chaos.Client/Controls/World/Popups/College/DebateVoteCard.cs`:

```csharp
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.Extensions;
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>
///     The centred card for a debate's opening and final votes, for students only. In the final vote it also shows the
///     opening counts and who would win if the vote ended now. OK or Esc hides it for that vote.
/// </summary>
public sealed class DebateVoteCard : GuildCloakDialogBase
{
    private const int WIDTH = 300;
    private const int HEIGHT = 180;
    private const int LEFT = 16;
    private const int INNER_WIDTH = WIDTH - (2 * LEFT);
    private const int LINE_HEIGHT = TextRenderer.CHAR_HEIGHT + 2;
    private const int CAPTION_TOP = 10;
    private const int MOTION_TOP = 28;
    private const int MOTION_LINES = 2;
    private const int COUNTS_TOP = MOTION_TOP + (MOTION_LINES * LINE_HEIGHT) + 4;
    private const int OPENING_TOP = COUNTS_TOP + LINE_HEIGHT;
    private const int LEADING_TOP = OPENING_TOP + LINE_HEIGHT;
    private const int BUTTONS_TOP = LEADING_TOP + LINE_HEIGHT + 4;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;

    private readonly CustomButton AgainstButton;
    private readonly UILabel CaptionLabel;
    private readonly ToolCountdown Countdown = new();
    private readonly UILabel CountsLabel;
    private readonly CustomButton ForButton;
    private readonly UILabel LeadingLabel;
    private readonly UILabel[] MotionLines = new UILabel[MOTION_LINES];
    private readonly UILabel OpeningLabel;
    private readonly CustomButton UndecidedButton;
    private CollegeDebateInfo? Debate;
    private DebatePhase? DismissedPhase;

    public event Action<CollegeActionArgs>? ActionRequested;

    public DebateVoteCard()
        : base("_nsett", false)
    {
        Name = "DebateVoteCard";
        Visible = false;
        UsesControlStack = true;
        Width = WIDTH;
        Height = HEIGHT;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(Dismiss, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);
        CaptionLabel = Caption(string.Empty, LEFT, CAPTION_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gold);

        for (var i = 0; i < MOTION_LINES; i++)
            MotionLines[i] = Caption(string.Empty, LEFT, MOTION_TOP + (i * LINE_HEIGHT), INNER_WIDTH, HorizontalAlignment.Center);

        CountsLabel = Caption(string.Empty, LEFT, COUNTS_TOP, INNER_WIDTH, HorizontalAlignment.Center);
        OpeningLabel = Caption(string.Empty, LEFT, OPENING_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gray);
        LeadingLabel = Caption(string.Empty, LEFT, LEADING_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gold);
        ForButton = AddButton("For", 70, LEFT, BUTTONS_TOP, () => PickSide(DebateSide.For));
        AgainstButton = AddButton("Against", 76, LEFT + 76, BUTTONS_TOP, () => PickSide(DebateSide.Against));
        UndecidedButton = AddButton("Undecided", 86, LEFT + 158, BUTTONS_TOP, () => PickSide(DebateSide.Undecided));
    }

    /// <summary>A DebateState message. Shows only to students, only during a vote.</summary>
    public void Apply(CollegeDebateInfo debate, bool reopen)
    {
        if (reopen)
            DismissedPhase = null;

        if (debate.IsTeacher || (debate.Phase is not (DebatePhase.Opening or DebatePhase.Final)))
        {
            Debate = null;
            Hide();

            return;
        }

        Debate = debate;
        Countdown.Start(debate.SecondsLeft);
        Refresh();

        if (!Visible && (DismissedPhase != debate.Phase))
            Show();
    }

    public void Close()
    {
        Debate = null;
        DismissedPhase = null;
        Hide();
    }

    public override void Update(GameTime gameTime)
    {
        if (Visible && Debate is { } debate)
        {
            Countdown.Advance(gameTime.ElapsedGameTime.TotalSeconds);
            CaptionLabel.Text = ClassToolText.VoteCaption(debate.Phase, Countdown.Left);
        }

        base.Update(gameTime);
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            Dismiss();
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    private void Dismiss()
    {
        DismissedPhase = Debate?.Phase;
        Hide();
    }

    private void PickSide(DebateSide side)
    {
        if (Debate is not { } debate)
            return;

        debate.MySide = side;
        Refresh();
        ActionRequested?.Invoke(new CollegeActionArgs { Type = CollegeActionType.DebateSide, Side = side });
    }

    private void Refresh()
    {
        if (Debate is not { } debate)
            return;

        CaptionLabel.Text = ClassToolText.VoteCaption(debate.Phase, Countdown.Left);
        var lines = TextRenderer.WrapLines($"\"{debate.Motion}\"", INNER_WIDTH);

        for (var i = 0; i < MOTION_LINES; i++)
            MotionLines[i].Text = i >= lines.Count ? string.Empty
                : (i == MOTION_LINES - 1) && (lines.Count > MOTION_LINES) ? lines[i] + "..."
                : lines[i];

        CountsLabel.Text = ClassToolText.Counts(debate);
        var final = debate.Phase == DebatePhase.Final;
        OpeningLabel.Text = final ? ClassToolText.Opening(debate) : string.Empty;
        LeadingLabel.Text = final ? "If it ended now: " + ClassToolText.ResultLine(ClassToolText.Leading(debate), debate) : string.Empty;
        ForButton.Selected = debate.MySide == DebateSide.For;
        AgainstButton.Selected = debate.MySide == DebateSide.Against;
        UndecidedButton.Selected = debate.MySide == DebateSide.Undecided;
    }
}
```

- [ ] **Step 7: Run the test and build.** Run the Verify test command and build the client. Expected: `DebateMarkTableTests` pass; Build succeeded.

```json:metadata
{"files": ["Chaos.Client/Controls/World/Popups/College/DebatePanel.cs", "Chaos.Client/Controls/World/Popups/College/DebateVoteCard.cs", "Chaos.Client/Systems/College/DebateMarkTable.cs", "Chaos.Client/Rendering/EntityOverlayManager.cs", "Tests/Chaos.Client.Tests/College/DebateMarkTableTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\\Users\\Michael\\Documents\\GitHub\\worktrees\\college-tools-server -- --treenode-filter \"/*/*/DebateMarkTableTests/*\"", "acceptanceCriteria": ["DebateMarkTable.Set replaces and skips no-side; Clear empties", "student panel: motion, counts, floor/vote/result line, side buttons lit, hand button only on floor, only-X-can-speak hint", "teacher panel: counts with gains (plain in opening), floor line with Take back, 3 hands with Give floor and +N more, Final vote only on floor, End debate with second click", "result + bonus: up to 3 speaker tick boxes and Confirm sends PickSpeakers", "vote card for students in opening/final: clock, motion, counts, side buttons; final adds opening counts and leader; OK/Esc hides for that vote", "markers 86 px above the tile, For green / Against red / ? grey, ' *' for the floor holder; cache clears with overlays"], "modelTier": "standard"}
```

---

### Task 11: Client wiring: build, route, place and close the tool windows (client repo)

**Goal:** The College screen builds the five new windows and the marker table, routes the eight new displays, keeps the side panels at the viewport's top right under the poll box, draws the markers, and closes everything on `ToolClosed`, a map change and logout.

**Files:**
- Modify: `Chaos.Client/Screens/WorldScreen.College.cs`
- Modify: `Chaos.Client/Screens/WorldScreen.Update.cs` (after the `Overlays.Update(...)` call, about line 246)
- Modify: `Chaos.Client/Screens/WorldScreen.Draw.cs` (after `Overlays.Draw(spriteBatch, Camera, MapFile.Height);`, about line 221)
- Modify: `Chaos.Client/Screens/WorldScreen.Map.cs` (new-map path, after `Game.TunePlayer.Stop();`)
- Modify: `Chaos.Client/Screens/WorldScreen.cs` (`UnloadContent`, after `College?.Tunes.Clear();`)
- Modify: `CLAUDE.md` (the `College/` entry under Popups)

**Acceptance Criteria:**
- [ ] `QuizList`, `QuizEdit` and `QuizResult` reach the editor; `QuizCard` the card (with `WorldState.PlayerName` for "you"); `QuizTeacher` the Teacher panel; `DebateState` the debate panel and the vote card; `DebateMarks` the marker table; `ToolClosed` closes all four tool windows and clears the markers
- [ ] Every new window's actions go to `SendCollegeAction`
- [ ] The Teacher panel and debate panel sit at the viewport's top right, below the poll box while it shows
- [ ] Markers draw in the overlay batch; a new map closes the tool windows and clears the markers; logout clears the markers and saves a dirty quiz
- [ ] `CLAUDE.md` lists the new controls and models
- [ ] The whole client suite passes

**Verify:** `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\college-tools-server -- --no-ansi` -> all pass

**Steps:**

- [ ] **Step 1: Build the windows.** In `WorldScreen.College.cs`:

  Add to the `CollegeWindows` class:

```csharp
        public required DebatePanel Debate { get; init; }
        public required DebateVoteCard DebateVote { get; init; }
        public required DebateMarkTable Marks { get; init; }
        public required QuizCardControl QuizCard { get; init; }
        public required QuizEditorControl QuizEditor { get; init; }
        public required QuizTeacherPanel QuizTeacher { get; init; }
```

  In `BuildCollege`'s `new CollegeWindows { ... }` initializer, add after `Reader = ...`:

```csharp
            QuizEditor = new QuizEditorControl
            {
                ZIndex = 2
            },
            QuizCard = new QuizCardControl
            {
                ZIndex = 2
            },
            QuizTeacher = new QuizTeacherPanel
            {
                ZIndex = 2
            },
            Debate = new DebatePanel
            {
                ZIndex = 2
            },
            DebateVote = new DebateVoteCard
            {
                ZIndex = 2
            },
            Marks = new DebateMarkTable()
```

  After `windows.Composer.ActionRequested += SendCollegeAction;`, add:

```csharp
        windows.QuizEditor.ActionRequested += SendCollegeAction;
        windows.QuizCard.ActionRequested += SendCollegeAction;
        windows.QuizTeacher.ActionRequested += SendCollegeAction;
        windows.Debate.ActionRequested += SendCollegeAction;
        windows.DebateVote.ActionRequested += SendCollegeAction;
```

  Before `Root.AddChild(votesPopup);`, add:

```csharp
        Root.AddChild(windows.QuizTeacher);
        Root.AddChild(windows.Debate);
        Root.AddChild(windows.QuizEditor);
        Root.AddChild(windows.QuizCard);
        Root.AddChild(windows.DebateVote);
```

  Update the file's summary to: `/// <summary>The Mileth College windows: writer, Art canvas, Music composer, reader, judging list, gallery, hand-in list, quiz editor and the class quiz and debate windows.</summary>`.

- [ ] **Step 2: Route the displays.** In `HandleCollegeDisplay`'s switch, add before its closing brace:

```csharp
            case CollegeDisplayType.QuizList:
                ShowList(College.QuizEditor, () => College.QuizEditor.OpenList(args));

                break;
            case CollegeDisplayType.QuizEdit:
                BringToFront(College.QuizEditor);
                College.QuizEditor.OpenQuiz(args);

                break;
            case CollegeDisplayType.QuizResult:
                if (College.QuizEditor.ShowResult(args))
                    BringToFront(College.QuizEditor);

                break;
            case CollegeDisplayType.QuizCard:
                College.QuizCard.Apply(args.QuizView, WorldState.PlayerName, args.Reopen);

                break;
            case CollegeDisplayType.QuizTeacher:
                College.QuizTeacher.Apply(args.QuizView, args.Reopen);

                break;
            case CollegeDisplayType.DebateState:
                College.Debate.Apply(args.Debate, args.Reopen);
                College.DebateVote.Apply(args.Debate, args.Reopen);

                break;
            case CollegeDisplayType.DebateMarks:
                College.Marks.Set(args.DebateMarks);

                break;
            case CollegeDisplayType.ToolClosed:
                CloseClassTools();

                break;
```

  Add these methods after `ShowList`:

```csharp
    /// <summary>Closes the class quiz and debate windows and clears the side markers: the tool ended, or the player left the room.</summary>
    private void CloseClassTools()
    {
        if (College is null)
            return;

        College.QuizCard.Close();
        College.QuizTeacher.Close();
        College.Debate.Close();
        College.DebateVote.Close();
        College.Marks.Clear();
    }

    /// <summary>Keeps the Teacher's quiz panel and the debate panel at the viewport's top right, under the poll box while it shows.</summary>
    private void PlaceClassToolPanels()
    {
        if (College is null)
            return;

        var viewport = WorldHud.ViewportBounds;
        var top = viewport.Top + 2 + (WorldState.Poll.ShouldShow ? VotePanel.Height + 2 : 0);
        College.QuizTeacher.Place(viewport, top);
        College.Debate.Place(viewport, top);
    }
```

  In `SaveCollegeDraft`, add `College?.QuizEditor.SaveIfDirty();` as its last line.

  Add `using Chaos.Client.Collections;` if `WorldState` doesn't resolve.

- [ ] **Step 3: Place, draw, map change, logout.**
  - `WorldScreen.Update.cs`: right after the `if (MapFile is not null) Overlays.Update(...);` statement, add `PlaceClassToolPanels();`.
  - `WorldScreen.Draw.cs`: right after `Overlays.Draw(spriteBatch, Camera, MapFile.Height);` (before that batch's `spriteBatch.End();`), add:

```csharp

            if (College is { Marks.Count: > 0 } college)
                Overlays.DrawDebateMarks(spriteBatch, Camera, MapFile.Height, college.Marks.All);
```

  - `WorldScreen.Map.cs`: in the new-map path, right after `Game.TunePlayer.Stop();`, add:

```csharp

        //a class quiz or debate belongs to its room; the room sends it again on the way back in
        CloseClassTools();
```

  - `WorldScreen.cs`, `UnloadContent`: after `College?.Tunes.Clear();`, add `College?.Marks.Clear();`.

- [ ] **Step 4: Update CLAUDE.md.** In `CLAUDE.md`, in the `College/` entry under **Popups**, before its closing `)`, add: `; QuizEditorControl — the two-page quiz editor (list, then one quiz; the model is ViewModel/College/QuizDocument); QuizCardControl — the student's quiz card; QuizTeacherPanel — the class Teacher's quiz panel; DebatePanel — the debate panel, with a student view and a Teacher view; DebateVoteCard — the opening and final vote card; the texts and countdowns are ViewModel/College/ClassToolText and ToolCountdown; side markers come from Systems/College/DebateMarkTable and are drawn by EntityOverlayManager.DrawDebateMarks`.

- [ ] **Step 5: Build and test.** Run the Verify command. Expected: Build succeeded; the whole client suite passes.

```json:metadata
{"files": ["Chaos.Client/Screens/WorldScreen.College.cs", "Chaos.Client/Screens/WorldScreen.Update.cs", "Chaos.Client/Screens/WorldScreen.Draw.cs", "Chaos.Client/Screens/WorldScreen.Map.cs", "Chaos.Client/Screens/WorldScreen.cs", "CLAUDE.md"], "verifyCommand": "dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\\Users\\Michael\\Documents\\GitHub\\worktrees\\college-tools-server -- --no-ansi", "acceptanceCriteria": ["8 new displays routed to editor, card (PlayerName), Teacher panel, debate panel + vote card, marker table, ToolClosed closes all", "new windows' actions go to SendCollegeAction", "side panels at viewport top right, under the poll box while it shows", "markers drawn in the overlay batch; new map closes tools and clears markers; logout clears markers and saves a dirty quiz", "CLAUDE.md lists the new controls", "whole client suite passes"], "modelTier": "standard"}
```

---
### Task 12: Full checks (all three repos)

**Goal:** Both test suites are at their baselines and both builds are clean before anything is committed.

**Files:**
- None (checks only; fix any regression in the file that caused it)

**Acceptance Criteria:**
- [ ] Server: `dotnet build Chaos.slnx` succeeds; the full suite fails only the known baseline tests
- [ ] Client: the `Chaos.Client.csproj` build succeeds; the full client suite passes
- [ ] Unora: the template check from Task 6 prints `[] [] []`

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj` in the server worktree -> only baseline failures; `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\college-tools-server -- --no-ansi` in the client worktree -> all pass

**Steps:**

- [ ] **Step 1: Server.** In the server worktree, run `dotnet build Chaos.slnx`, then the full suite. Record the pass and fail counts. Any failure outside the baseline (`OnItemDroppedOn` stackable, and `GiveAbility` if it fails on master) is a regression: fix it before going on.

- [ ] **Step 2: Client.** Do not build while the server build runs. In the client worktree, build `Chaos.Client/Chaos.Client.csproj` with `-p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\college-tools-server`, then run the full client suite. Record the counts.

- [ ] **Step 3: Unora.** Run the Task 6 Step 4 check in the Unora worktree. Expected: `[] [] []`.

```json:metadata
{"files": [], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj", "acceptanceCriteria": ["server build + full suite at baseline", "client build + full suite pass", "Unora template check clean"], "modelTier": "standard"}
```

---

### Task 13: Spec record (client main checkout)

**Goal:** The part 5 spec records the plan-time adjustments, so the spec matches what was built.

**Files:**
- Modify (client main checkout `C:\Users\Michael\Documents\GitHub\Chaos.Client`): `docs/superpowers/specs/2026-10-05-mileth-college-tools-design.md`

**Acceptance Criteria:**
- [ ] The spec has a "Plan-time adjustments" section before "## Testing" listing every adjustment from this plan's header
- [ ] The spec's Repositories table says Unora gets 12 dialog templates

**Verify:** `grep -n "Plan-time adjustments" docs/superpowers/specs/2026-10-05-mileth-college-tools-design.md` in the main client checkout -> one match

**Steps:**

- [ ] **Step 1: Add the section.** In the main client checkout, add before "## Testing" in `docs/superpowers/specs/2026-10-05-mileth-college-tools-design.md`:

```markdown
## Plan-time adjustments

- Unora gets 12 dialog templates: the lectern's quiz and debate setup chains, "My quizzes" at the lectern and the
  Registrar, and the lectern's "Show panel". The lectern menus are built from Unora templates.
- The quiz card lists the answers in one column of four, not a 2 x 2 grid: two columns can't fit 40-character answers.
- The debate's switches also need the final vote to have been held. A debate ended before its final vote checks no one
  and gives no bonus.
- Only the class's Teacher gets the Teacher panel and the Teacher's debate view. The server still accepts control
  actions from the Director and admins.
- Class actions (answers, sides, hands, controls) skip the 250 ms save throttle and are limited to 120 a minute per
  player instead.
- Answers, sides and hands are not written to disk at once; the 30-second College tick saves them. Starting and ending
  a tool saves at once.
- The Teacher panel and the debate panel close with their own OK button and don't take keyboard focus. The lectern's
  **Show quiz panel** or **Show debate panel** brings them back. `CollegeDisplayArgs.Reopen` (on QuizCard, QuizTeacher
  and DebateState) shows a window the player closed; it is sent to arrivals and by "Show panel".
  `CollegeDebateInfo.IsTeacher` tells the panel which view to show.
- Side markers draw 86 px above the tile centre, over the name tag. The floor holder's marker ends in " *", since the
  game font has no star.
- **Delete** in the quiz editor asks with a second click and a status line, not a popup.
- The rule state is in `LiveQuiz` / `LiveDebate` and the rules in `QuizSession` / `DebateSession`, like
  `CollegeClass` / `ClassSession`. The tests are `QuizSessionTests` / `DebateSessionTests`.
- The Teacher's debate view lists the first 3 raised hands, then "+N more".
- The lectern offers **Start a quiz** and **Start a debate** only to the class's Teacher. It offers **My quizzes** to
  anyone who may keep quizzes, whether or not a class is running.
```

- [ ] **Step 2: Update the Repositories table.** In the same spec, change the Unora row's last cell to "Unora main (12 new dialog templates)" and remove the sentence "The Registrar and lectern dialogs are server scripts, so Unora is expected to need no change.".

  Leave the spec uncommitted; Task 14 commits it with the plan.

```json:metadata
{"files": ["docs/superpowers/specs/2026-10-05-mileth-college-tools-design.md"], "verifyCommand": "grep -n \"Plan-time adjustments\" docs/superpowers/specs/2026-10-05-mileth-college-tools-design.md", "acceptanceCriteria": ["spec lists the plan-time adjustments before Testing", "Repositories table names the 12 Unora templates"], "modelTier": "mechanical"}
```

---

### Task 14: Commit the full implementation

**Goal:** One commit per repo on `feat/college-tools`. The client's `Chaos-Server` gitlink points at the server commit. The spec record and plan status are committed on client main.

**Files:**
- All changes from Tasks 1-13

**Acceptance Criteria:**
- [ ] Server, client and Unora each have exactly one new commit on `feat/college-tools`, staged by explicit path only
- [ ] The client commit's `Chaos-Server` gitlink equals the server commit
- [ ] Nothing is pushed

**Verify:** `git -C <each worktree> log --oneline -2` shows the new commit on top of its base. `git -C C:\Users\Michael\Documents\GitHub\worktrees\college-tools-client ls-tree HEAD Chaos-Server` shows the server commit's SHA.

**Steps:**

- [ ] **Step 1: Server commit.** In the server worktree, run `git status --short` and stage each changed and new file by its path (Tasks 1-6). Write the message to a file with UTF-8 without BOM, then run `git commit -F <file>`:

```
Mileth College part 5: quizzes and debates in class

Teachers keep up to 20 quizzes (5-20 multiple-choice questions) and run one quiz and one debate per Lecture or
Discussion class. A quiz asks one question at a time on a timer and reveals the answer; a debate has an opening vote,
speaking turns on the floor (other students' say and shout are refused while someone holds it) and a final vote. Each
tool can count for attendance and give a bonus roll (top scorers or best speakers), with guards of 5 questions and 3
speakers. The lectern runs the setup chains and the room script ticks the timers. CLIENT_VERSION 775.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
```

- [ ] **Step 2: Unora commit.** In the Unora worktree, stage the 12 new templates by path and commit:

```
Mileth College part 5: lectern quiz and debate dialogs

The lectern's quiz and debate setup chains, "My quizzes" at the lectern and the Registrar, and the lectern's
"Show panel".

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
```

- [ ] **Step 3: Client commit.** In the client worktree, point the submodule at the server commit with `git update-index --cacheinfo 160000,<server sha>,Chaos-Server`. Stage the client files by path (Tasks 7-11, including `CLAUDE.md`), then commit:

```
Mileth College part 5: quiz editor, quiz card and debate panels

QuizEditorControl (list, then one quiz) on a pure QuizDocument; the student's QuizCardControl and the Teacher's
QuizTeacherPanel; DebatePanel (student and Teacher views) and DebateVoteCard for the opening and final votes; side
markers over heads from DebateMarkTable. Chaos-Server points at the part 5 server commit.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
```

- [ ] **Step 4: Spec commit.** In the main client checkout, run `git add -f docs/superpowers/specs/2026-10-05-mileth-college-tools-design.md docs/superpowers/plans/2026-10-05-mileth-college-tools.md docs/superpowers/plans/2026-10-05-mileth-college-tools.md.tasks.json`. Commit with the message `Mileth College part 5: spec record and plan status` plus the attribution line.

- [ ] **Step 5: Report.** Tell the user:
  - the four commit SHAs;
  - that nothing is pushed;
  - the merge order: server, then Unora, then client;
  - the in-game checklist from the spec's Testing section.

```json:metadata
{"files": [], "verifyCommand": "git -C C:\\Users\\Michael\\Documents\\GitHub\\worktrees\\college-tools-client ls-tree HEAD Chaos-Server", "acceptanceCriteria": ["one commit per repo on feat/college-tools", "client gitlink = server commit", "spec/plan committed on client main", "nothing pushed"], "modelTier": "mechanical"}
```
