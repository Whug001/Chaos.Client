# Mileth College part 5: Lecture tools (quizzes and debates)

Date: 2026-10-05
Parent design: `2026-10-04-mileth-college-design.md`. Part 1 spec: `2026-10-05-mileth-college-core-design.md`.
Part 2 spec: `2026-10-05-mileth-college-entries-design.md`. Part 4 spec: `2026-10-05-mileth-college-music-design.md`
(all in this folder). Read them first; this spec only adds what part 5 needs.
Status: approved section by section in brainstorming on 2026-10-05.

## Scope

Part 5 delivers two class tools a Teacher can run during a Lecture or Discussion class:

- **Quiz:** the Teacher writes multiple-choice quizzes ahead of time in a quiz editor, then asks them one question at
  a time in class. Students answer on a quiz card.
- **Debate:** the Teacher sets a motion. Students take sides, raise hands and speak in turns, and vote again at the
  end. Side markers show over each head.

Either tool can, at the Teacher's choice, act as an attendance check, give a bonus roll, or both.

## Decisions made in brainstorming

| Topic | Decision |
|---|---|
| Subjects | Both tools work in Lecture and Discussion classes of every subject, not only History, Lore and Philosophy |
| Effect on marks | The Teacher picks per quiz or debate: **Counts for attendance**, **Bonus**, both or neither |
| Bonus reward | A second roll at an Educated Mark, using the normal chance |
| Quiz top scorers | The best score, ties included, at most 3 students, at least one right answer |
| Writing quizzes | Ahead of time, in a quiz editor; saved and reusable |
| Student quiz view | A quiz card in the middle of the view (mockup B) |
| Question pacing | A timer the Teacher picks per quiz closes each question and shows the answer; the Teacher moves on with **Next question** |
| Scoring | One point per right answer; speed doesn't count |
| Teacher quiz view | A small Teacher panel at the side (mockup B), not the lectern dialog |
| Quiz editor | Two pages in one window: the list, then one quiz (mockup B) |
| Debate parts | All three: motion with opening and final votes, speaking turns with the floor, side markers over heads |
| Debate bonus | Up to 3 best speakers, picked by the Teacher from students who held the floor |

Mockups from brainstorming: `.superpowers/brainstorm/1300326-1791250895/content/` in this repo
(`quiz-answer-box.html`, `quiz-teacher-control.html`, `quiz-editor.html`, `debate-panels.html`).

## Repositories and branches

| Repo | Worktree | Branch | Starts from |
|---|---|---|---|
| `Chaos-Server` | `worktrees/college-tools-server` | `feat/college-tools` | server master 3d12ce2db (parts 1-4, not pushed) |
| `Chaos.Client` | `worktrees/college-tools-client` | `feat/college-tools` | client main 2d8db6aa |
| `Unora` | `worktrees/college-tools-unora`, only if a dialog text file needs a change | `feat/college-tools` | Unora main |

The client worktree builds `Chaos.Client/Chaos.Client.csproj` with `UnoraServerPath` pointing at the server worktree,
as in parts 2-4. The `.slnx` fails in a worktree. Subagents working in a worktree use absolute paths and do not use
Serena, which edits the main checkout.

The Registrar and lectern dialogs are server scripts, so Unora is expected to need no change.

## Saved quizzes

### What a quiz holds

| Field | Rule |
|---|---|
| Title | 1-40 characters |
| Questions | 5-20 |
| Question text | 1-150 characters, no line breaks |
| Answers | 2-4 per question, each 1-40 characters |
| Right answer | Exactly one per question |

A quiz being edited may break the count and "right answer" rules; it must pass every rule to start in class.

### Storage

- Each quiz is a file `Works/Quizzes/<id>.json` (under `CollegeOptions.WorksDirectory`), with a 32-character random hex
  id, like pieces. `CollegeQuizStore` mirrors `CollegePieceStore`: `Load`, `Save`, `Delete`, a `Lock`, and a bad id or
  unreadable file reads as missing.
- `CollegeState` gains `Quizzes`: a list of `{ Id, Owner, Title, QuestionCount, UpdatedUtc }`. Owner names live only
  in `CollegeState.json`, so the open `AislingRenamer` follow-up covers quizzes too.
- At most 20 quizzes per owner. **Copy** makes "Copy of {title}" (cut to 40 characters) and counts toward the 20.

### Who may keep quizzes

Anyone for whom `CollegeRoles.IsTeacherAnywhere` holds: Teachers, Knights while they may teach, the Director and
admins. A player who loses that right keeps their saved quizzes, but can't open the editor until the right returns.

### `QuizRules` (pure, unit tested)

- `CheckSave(quiz)`: lengths and counts not exceeded (title at most 40, at most 20 questions, question at most 150,
  2-4 answers each at most 40). Empty text is allowed in a draft.
- `CheckReady(quiz)`: every rule in the table above.
- Each returns a result code that `CollegeText` turns into a player message, for example
  `Question 3 needs a right answer.`

## The quiz and debate in class (server)

### Where they live

The running class (`CollegeClass`) gains `Quiz` (`LiveQuiz?`) and `Debate` (`LiveDebate?`), saved with
`CollegeState` like the rest of the class. Both are pure units with no game-world access; every method takes `now`.

- A class may run one quiz and one debate, one at a time.
- Tools can start only in a Lecture or Discussion class, not Activity.
- Both end when the class ends, for any reason.

`CollegeClass` also gains `QuizUsed` and `DebateUsed` flags, so a finished tool can't be started again in that class,
and keeps each finished tool's result for the reward step.

### Starting a quiz (lectern, the class's Teacher only)

1. Pick a saved quiz from a list (title and question count). Only quizzes that pass `CheckReady` can be started; others
   show the first problem.
2. Time per question: 15, 20, 30, 45 or 60 seconds.
3. "Counts for attendance?" Yes / No.
4. "Bonus for top scorers?" Yes / No.

The quiz is copied into the class, so editing the saved quiz later doesn't change it.

### `LiveQuiz`

- **State:** the copied quiz, seconds per question, the two switches, the current question index (none before the
  first), phase (`Waiting`, `Open`, `Revealed`), deadline, answers for the current question (name to answer index),
  scores (name to right answers), answered counts (name to questions answered), questions asked.
- **Next:** allowed in `Waiting` or `Revealed` while questions remain. Opens the next question with a deadline of
  now plus the chosen time, and counts it as asked.
- **Answer(name, question, answer):** accepted only in `Open`, for the current question number, from a student (not
  the class's Teacher). A later answer replaces an earlier one.
- **Tick(now):** at the deadline the question is revealed. Every student with an answer gets +1 answered; every right
  answer gets +1 score.
- **End:** the Teacher confirms, at any time. After the last question is revealed, the Teacher panel's
  **Next question** becomes **Finish quiz**, which ends the quiz without asking. Ending while a question is open drops
  that question: it isn't scored and doesn't count as asked.
- A student who arrives late answers from the current question on. The attendance check still counts every question
  asked.

### Starting a debate (lectern, the class's Teacher only)

1. The motion: typed text, 1-120 characters.
2. Turn length: 1, 2 or 3 minutes.
3. "Counts for attendance?" Yes / No.
4. "Bonus for best speakers?" Yes / No.

### `LiveDebate`

- **Phases:**
  1. `Opening` (60 seconds): each student picks For, Against or Undecided. No one has a side until they pick.
  2. `Floor`: entered automatically when the opening vote ends. The opening counts are saved. Students may change side
     at any time.
  3. `Final` (30 seconds): started by the Teacher's **Final vote**. Students may still change side.
  4. `Result`: when the final vote ends, gains are worked out: each side's final count minus its opening count. The
     side with the bigger gain wins; equal gains is a draw. Undecided can't win. If the bonus switch is on, the
     Teacher picks best speakers here.
  5. The debate ends on **End debate**, or on **Confirm** of the best speakers.
- **Hands:** in `Floor`, students raise or lower a hand. Raised hands keep their order.
- **The floor:** the Teacher gives the floor to a student with a raised hand. Their hand drops, and they hold the floor
  until the turn length runs out or the Teacher uses **Take back**. Giving the floor to someone else ends the current
  turn. Each student who holds the floor is added to the speakers list.
- **Leaving the room:** a student's raised hand drops and they lose the floor, on the next room tick that doesn't see
  them. Their side is kept.
- **Votes counted for the attendance check:** a student "voted" in the opening if they had a side when `Opening`
  ended, and in the final if they had one when `Final` ended.

### The clock

`CollegeRoomScript` gains a 1-second step on its existing `Update`. It calls `CollegeService.ToolTick(roomInstanceId,
names in room, now)`. That call advances deadlines, drops hands and floors of students not in the room, and returns
the updates to send. The script sends them, through `CollegePanel`, to the players on its map. Sending from the map's
own update keeps it thread-safe.

The script's `OnEntered` asks the service for the current tool state and sends it to the player who entered, so a late
arrival or a reconnect sees the card or panel at once.

### Rewards (`CollegeService.FinishLocked`)

1. The end-of-class check is unchanged. The 3-student rule uses presence only, so a failed tool check never sinks the
   whole class or the Teacher's mark.
2. A tool's switches count only if its guard was met:
   - the quiz asked at least 5 questions;
   - at least 3 different students held the debate floor.
3. **Attendance:** for each finished tool with attendance on and its guard met, a qualifying student who fails the
   check doesn't roll and is told why:
   - quiz: answered fewer than half the questions asked (5 of 10 passes; 4 of 9 fails);
   - debate: missed the opening vote or the final vote.
4. **Bonus:** each qualifying student who still rolls gets at most one second roll per class:
   - quiz: students with at least one right answer are ranked by score. Whole tie groups are taken from the top while
     the total stays at 3 or fewer. Scores 5, 5, 4 give the bonus to both 5s. Scores 5, 5, 5, 5 give no bonus.
   - debate: the Teacher's best speakers.
5. The second roll uses `MarkLedger.Roll` like the first, so it moves the chance up or resets it.
6. `History` entries gain `BonusRolls` and `ToolFailed` counts.

### The floor rule

`CollegeService.MayTalk(name, mapInstanceId)` returns false when the map is the running class's room, a debate is in
`Floor` or `Final`, someone else holds the floor, and the speaker is not the class's Teacher, the Director or an
admin. The world server's public-message handler (`WorldServer.InnerOnPublicMessage`) checks it after the command
check, for Normal and Shout messages. It refuses the line with the orange bar `{Name} has the floor.` Whispers, group
and guild chat are not affected.

## Messages

All on the existing pair: `ClientOpCode.CollegeAction = 144` and `ServerOpCode.CollegeDisplay = 152`.
`CLIENT_VERSION` 774 -> 775; update the test that pins it (`GuildEmblemPacketConverterTests`).

### `CollegeActionType` (client to server)

| # | Sub-type | Fields |
|---|---|---|
| 14 | `QuizOpen` | quiz id (empty asks for the list) |
| 15 | `QuizSave` | quiz id (empty for a new quiz), quiz |
| 16 | `QuizDelete` | quiz id |
| 17 | `QuizCopy` | quiz id |
| 18 | `QuizAnswer` | question number, answer index |
| 19 | `QuizControl` | `Next`, `End` |
| 20 | `DebateSide` | `For`, `Against`, `Undecided` |
| 21 | `DebateHand` | raised (bool) |
| 22 | `DebateControl` | `GiveFloor` (name), `TakeBack`, `FinalVote`, `PickSpeakers` (up to 3 names), `End` |

### `CollegeDisplayType` (server to client)

| # | Sub-type | Fields |
|---|---|---|
| 10 | `QuizList` | rows: id, title, question count; quota used. Opens the editor at page 1. |
| 11 | `QuizEdit` | id, quiz. Opens the editor at page 2. |
| 12 | `QuizResult` | result code, message, id (for a new quiz's first save) |
| 13 | `QuizCard` | quiz title, question number and count, question, answers, seconds left, my answer, phase. When revealed: right answer, number right, number answered, my score, the top 3 (name, score). |
| 14 | `QuizTeacher` | the card's fields, plus the right answer while open and the number answered so far |
| 15 | `DebateState` | motion, phase, counts, opening counts, floor holder, seconds left, my side, my hand, result. The Teacher's copy adds the raised hands in order and the speakers list. |
| 16 | `DebateMarks` | rows: entity id, side, holds the floor |
| 17 | `ToolClosed` | `Quiz` or `Debate` |

- The server sends on every change, not every second. Clients count down from "seconds left".
- Class-tool messages go only to players in the class's room. Editor messages go only to the player who asked.
- The right answer goes to the Teacher, and to students only after the reveal.

New enums (`QuizPhase`, `DebatePhase`, `DebateSide`, `QuizControlType`, `DebateControlType`, `CollegeTool`,
`QuizResultCode`) live in `Chaos.DarkAges`. Limits shared by both sides go in `CollegeProtocol`:

| Constant | Value |
|---|---|
| `MAX_QUIZ_TITLE_CHARS` | 40 |
| `MIN_QUIZ_QUESTIONS` | 5 |
| `MAX_QUIZ_QUESTIONS` | 20 |
| `MAX_QUESTION_CHARS` | 150 |
| `MIN_ANSWERS` / `MAX_ANSWERS` | 2 / 4 |
| `MAX_ANSWER_CHARS` | 40 |
| `MAX_QUIZZES_PER_OWNER` | 20 |
| `QUIZ_SECONDS` | 15, 20, 30, 45, 60 |
| `MAX_MOTION_CHARS` | 120 |
| `DEBATE_TURN_MINUTES` | 1, 2, 3 |
| `DEBATE_OPENING_SECONDS` / `DEBATE_FINAL_SECONDS` | 60 / 30 |
| `MAX_BONUS_STUDENTS` | 3 |
| `QUIZ_GUARD_QUESTIONS` / `DEBATE_GUARD_SPEAKERS` | 5 / 3 |

### Checks on every action

- Editor actions need `IsTeacherAnywhere` and, for an existing quiz, ownership. Admins may open only their own quizzes.
- `QuizSave` runs `CheckSave` and the 20-quiz limit. Saves are throttled like `SaveDraft`.
- `QuizAnswer`, `DebateSide` and `DebateHand` need the sender in the class's room and not its Teacher.
- `QuizControl` and `DebateControl` need the class's Teacher, the Director or an admin.
- `GiveFloor` needs a student with a raised hand who is in the room. `PickSpeakers` names must be in the speakers list.
- Anything else gets a `QuizResult` refusal or, for class actions, an orange bar.

## The client

New controls go in `Controls/World/Popups/College/`. Routing goes in `Screens/WorldScreen.College.cs`.

### Quiz editor (`QuizEditorControl`)

560 x 464, built like the other College windows. Layout B from the mockup:

- **Page 1:** "My quizzes (3/20)", one row per quiz (title and question count), **Open**, **New quiz**, **Copy**,
  **Delete** (asks "Delete {title}?") and **OK**. Built on `CollegeListWindow`.
- **Page 2:**
  - left: the **Title** box, then the questions list "Questions (8/20)" with **Add**, **Up** and **Down**;
  - right: the question box with a counter "88/150", four answer boxes each with a round button to mark the right
    answer, and **Remove question**;
  - footer: **Back**, **Save**, a status line and **OK**.
- Going back or closing with changes saves. The window checks the save limits before sending.
- The model is `ViewModel/College/QuizDocument`, pure and unit tested. It handles add, move up and down, remove, edit,
  and the right-answer mark staying on its answer. Blank answer boxes after the last filled one are dropped when the
  quiz is sent.

### Quiz card (`QuizCardControl`)

Centred, about 370 x 230. Mockup B:

- caption: "Quiz: {title} ({n}/{count})";
- the question, wrapped;
- a timer bar and "14 seconds left. You can change your answer.";
- answers in a 2 x 2 grid. Clicking one sends `QuizAnswer` and lights it.
- **On reveal:** the right answer turns green, a wrong pick turns red, and lines read "Right!" or "Wrong.",
  "4 of 9 got it right." and "Top: Aroha 3, Kael 3, you 2".
- The card stays up until the next question or `ToolClosed`. **Esc** hides it until the next question.
- The Teacher doesn't get the card.

### Quiz Teacher panel (`QuizTeacherPanel`)

Top right, about 230 x 150: the quiz title with "3/8", the question (cut to fit), its right answer in green,
"Answered 7 of 9 · 0:14 left", then **Next question** (off while a question is open; **Finish quiz** after the last)
and **End quiz** (asks first). After a reveal it shows the right count and the top 3.

### Debate panel (`DebatePanel`)

Top right. One control with two views:

- **Student view:** the motion, "For 2 · Against 2 · Undecided 1", "Floor: Kael 1:12 left", **For**, **Against** and
  **Undecided** (the chosen one lit), **Raise hand** / **Lower hand**, and "Only Kael and the Teacher can speak."
  while someone holds the floor.
- **Teacher view:** the counts with each side's change since the opening vote, the floor line with **Take back**, the
  raised hands in order with **Give floor** on each, **Final vote** and **End debate**. In `Result` with the bonus on,
  it lists the speakers with tick boxes (at most 3) and **Confirm**.
- **The opening and final votes** show as a centred card, like the quiz card. It has a timer, the side buttons and,
  in the final vote, the opening counts and who would win now.
- The result line reads "For wins (+2)", "Against wins (+1)" or "A draw."

### Side markers

A small label over each head in the room: "For" (green), "Against" (red) or "?" (grey, for Undecided), with a ★ on
the floor holder. It's drawn with the viewport overlays in step 7, beside health bars and chant text. The labels come
from `DebateMarks` by entity id, and are cleared on map change and on `ToolClosed`. A student with no side has no
label.

### Placement

The quiz Teacher panel and the debate panel sit where the poll box sits. If a world poll is open at the same time, the
class panel moves below it.

The client needs no chat change for the floor rule; the server refuses the line.

## Content

### Registrar (`CollegeRegistrarScript`)

- **My quizzes**, shown to anyone for whom `IsTeacherAnywhere` holds, opens the editor (sends `QuizList`).

### Lectern (`CollegeLecternScript`)

During the speaker's own Lecture or Discussion class:

- **Start a quiz** and **Start a debate**, each until that tool has been used in this class, and only while no tool
  runs;
- **My quizzes**;
- while a tool runs, **Show quiz panel** or **Show debate panel**, which resends the state.

Dialog limits apply: option text 35 characters, dialog text 360, no em dashes.

### Messages (orange bar, 45 characters at most)

| When | Text |
|---|---|
| Talking while someone else holds the floor | `{Name} has the floor.` (name cut to fit) |
| Floor given | `You have the floor for {N} minute(s).` |
| Floor ended | `Your turn is over.` |
| Quiz attendance check failed | `No roll: too few quiz answers.` |
| Debate attendance check failed | `No roll: you missed a debate vote.` |
| Quiz bonus | `Bonus roll for your quiz score!` |
| Debate bonus | `Bonus roll for your debate speech!` |
| Quiz started (room) | `A quiz begins: {title}` (cut to fit) |
| Debate started (room) | `A debate begins! Pick your side.` |
| Quiz not ready | from `QuizRules`, e.g. `Question 3 needs a right answer.` |
| Quiz limit | `You can keep 20 quizzes.` |

## Testing

**Server unit tests** in `Tests/Chaos.Tests/College/`:

- `QuizRulesTests`: every limit at its edge for `CheckSave` and `CheckReady`; a missing right answer; 1 and 5 answers.
- `LiveQuizTests`:
  - an answer before the deadline replaces an earlier one; an answer after it is refused;
  - the reveal at the deadline; scores and answered counts;
  - `Next` refused while open and after the last question;
  - the Teacher's answers refused;
  - a late joiner's counts.
- `LiveDebateTests`:
  - the phase order and timers;
  - hands keep their order; giving the floor drops that hand;
  - a turn ends at its deadline; giving the floor to someone else ends the current turn;
  - leaving the room drops the hand and the floor and keeps the side;
  - gains, the winner, a draw, and Undecided never winning;
  - best speakers limited to the speakers list and to 3.
- `CollegeServiceTests` additions:
  - guards at 4 and 5 questions, and at 2 and 3 speakers;
  - the half-answered rule at its boundary;
  - the tie-group cut (5, 5, 4 and 5, 5, 5, 5) and the one-right-answer minimum;
  - at most one bonus roll when both tools give one;
  - a failed tool check leaves the 3-student rule and the Teacher's mark unchanged;
  - `MayTalk` for a student, the floor holder, the Teacher, an admin and another map;
  - tools refused in Activity classes and refused a second time in one class;
  - quizzes survive a save and reload; the 20-quiz limit; owner checks; copying.
- Packet round-trip tests for every new sub-type, and the `CLIENT_VERSION` 775 pin.

**Client unit tests** in `Tests/Chaos.Client.Tests`:

- `QuizDocument`: add, move, remove, the right-answer mark following its answer, and dropping trailing blank answers.
- The countdown and phase state behind the quiz card and the debate panel.

The full server suite stays at its known baseline (`OnItemDroppedOn` stackable, and `GiveAbility` if it still fails
on master).

**In-game checklist** for the user:

1. As a Teacher, open **My quizzes** at the Registrar. Write a quiz, save it, copy it and delete the copy. Try to start
   a quiz with a question missing its right answer.
2. Start a Lecture class. Start the quiz with both switches on and 20 seconds per question. Answer from 3 students on
   3 computers, changing one answer before the deadline. Check the reveal, the Teacher panel and the top 3.
3. Leave one student answering fewer than half the questions. At class end, check that student gets no roll and the
   top scorers get a second roll.
4. In another class, start a debate with both switches on. Vote, raise hands, give and take back the floor, and try to
   talk while someone else holds it. Check the side markers and the ★.
5. Run the final vote, check the result and pick best speakers. At class end, check the bonus rolls and that a student
   who missed a vote gets none.
6. Open a world poll during a class tool and check that the panels don't overlap.

## Shipping

Server + client 775. The client goes out in a launcher patch before the server restart. No .dat files or maps. Unora
ships only if the plan finds a dialog text file to change. Like parts 1-4, nothing is pushed until the user says so.

## Open follow-ups carried from parts 1-4

- The file-server `AislingRenamer` must update `CollegeState.json` before a rename. Part 5 adds `Quizzes[].Owner`, and
  the running class's tool state holds names (answers, scores, sides, hands, speakers).
