# Mileth College: Hangman and Word Scramble

Date: 2026-10-12. Status: approved in brainstorming, awaiting spec review.

Two new class tools for College Teachers, next to Quiz and Debate (see
`2026-10-05-mileth-college-tools-design.md`). Hangman is one shared board the class plays in turn.
Word Scramble is a race where every student solves the same scrambled word on their own card.
Both play from Teacher-written word lists.

Hangman's rules come partly from a College Teacher's request: a turn order the Teacher sets, a
buzz-in button so the game decides who answered first, Casual and Survival modes, and words typed in
or picked from a word bank. Teams were asked for too and are left for later (see Out of scope).

Mockups: `Chaos.Client/.superpowers/brainstorm/139309-1791855981/content/` (`hangman-board-v2.html`
option A, `scramble-card.html`, `hangman-teacher.html`).

## Rules shared with the other tools

- One tool runs at a time. Each tool runs at most once per class. A class may run Quiz, Debate,
  Hangman and Scramble one after another.
- Tools are allowed in Lecture and Discussion classes and refused in Activity classes.
- The Teacher picks "Counts for attendance" and "Bonus" when starting, as for a quiz.
  - Both take effect only once at least 5 words have ended (`WORD_GAME_GUARD_WORDS = 5`). A word
    ends when it is solved, lost, or revealed.
  - A student who fails a game's attendance check loses the class reward roll, through
    `FailedToolCheck`.
  - The bonus is one extra roll for the top scorers among the students who qualified for the class.
    The best score must be at least 1, and at most 3 students (`MAX_BONUS_STUDENTS`) may share it.
    A student gets at most one bonus per class, from any tool. This goes through `BonusFor` and
    `TopScorers`.
- The Teacher never plays and never scores.

## Word lists

- A word list has an id, a title (up to 40 characters, `MAX_QUIZ_TITLE_CHARS`) and 5 to 30 words.
- A word is 3 to 20 characters long and may hold the letters A to Z and single spaces between
  letters. Saving trims the word, collapses runs of spaces and stores it upper-case. A word may not
  start or end with a space. Two words in one list may not be the same.
- Each word has an optional hint of up to 60 characters.
- Each Teacher keeps up to 20 lists. Who may keep lists follows the quiz rule (`CollegeRoles.CanPost`).
- Lists are stored like quizzes: one JSON file per list in `WorksDirectory/WordLists`, and an index
  `CollegeState.WordLists` so list screens never open the files.
- The Word bank lists every Teacher's lists with the author's name and word count, 10 per page.
  Anyone who may keep lists may preview a bank list (read-only) and copy it to their own lists
  ("Copy of " + title, cut to 40 characters). Copying counts against their 20.
- Copy on the Mine tab copies the Teacher's own list, as quiz Copy does.
- A list may be deleted only by its owner. A running game keeps its own copy of the words, so a
  delete or edit mid-game changes nothing in that game.
- AislingRenamer in Chaos.FileServer must rewrite the owner name in `CollegeState.WordLists`, the
  same as for `Quizzes`.

### Quick list

At the lectern a Teacher may choose "Type a quick list" in place of a saved list. The word-list
editor opens in a "not saved" mode: one page, 1 to 10 words, the same word and hint rules, and a
Start button in place of Save. The quick list is used for that game only. With fewer than 5 words,
attendance and bonus can never take effect; the editor says so under the word count.

## Hangman

### Starting

1. At the lectern: Start Hangman.
2. Pick a list (own lists or "Type a quick list").
3. Pick the mode: Casual or Survival.
4. Pick "Counts for attendance" (yes/no), then "Bonus" (yes/no).
5. The quick-list editor opens if chosen. Start there continues.
6. The turn-order window opens for the Teacher. It lists everyone in the room except the Teacher,
   each with a tick box, all ticked at first. The Teacher drags names to reorder,
   or uses Shuffle or Tick all. Start begins the game with at least 1 ticked player; Cancel abandons
   the start, and the tool is not counted as used.

Only players on the turn list play. Other students in the room watch the board, cannot pick or
buzz, and are not judged by Hangman's attendance check.

### A word

- The board shows blanks for letters and gaps for spaces, the hint, the gallows (Survival) or a miss
  count (Casual), the A to Z buttons, the turn strip and a line of recent events ("Aroha found L.
  Kael missed S.").
- Turns go down the list. The player whose turn it is has 20 seconds (`HANGMAN_TURN_SECONDS`) to
  pick a letter that has not been tried on this word.
  - Right letter: every copy of it is revealed and the picker scores 1 point.
  - Wrong letter: it counts as a miss.
  - No pick in time, or the player has left the room: the turn is skipped. A player who is not in the
    room is skipped at once instead of after 20 seconds.
- Buzz: any player on the list, including the one whose turn it is, may press Buzz at any time while
  the word is open and nobody else is answering.
  - The server takes the first buzz to arrive. That player gets 10 seconds
    (`HANGMAN_SOLVE_SECONDS`) to type the whole word. The turn clock pauses meanwhile, and other
    buzzes are refused with "Someone is answering."
  - Right answer: the word is solved, the player scores 3 points, and the word ends.
  - Wrong answer or no answer in time: it counts as a miss, and that player may not buzz again on
    this word. The turn clock resumes where it stopped.
  - Answers are compared after the same trimming and upper-casing as saved words.
- Revealing the last letter by picking also solves the word. The picker gets their 1 point for the
  letter, and no solve points are awarded.
- Survival: the word is lost at 6 misses (`HANGMAN_MAX_MISSES`). Nobody gets solve points; letter
  points already scored stay.
- Casual: there is no miss limit. The word goes on until it is solved.
- When a word ends, everyone sees the full word. The Teacher presses Next word for the next one.
  The next word starts with the player after whoever made the last move of the word before (the
  solver, the last picker, or the player whose miss lost it).
- After the last word, or when the Teacher presses End game, the board shows final scores and
  closes like the quiz card.

### Teacher panel

A side panel like the quiz Teacher panel. It shows the word, mode, misses and the answer in green,
plus the turn list with points and the current turn. Buttons:

- Add player: picks from students in the room who are not on the list. They join at the end.
- Remove: select a name, then press Remove. The player stays in the scores but gets no more turns.
- Skip turn: ends the current turn as a skip.
- Next word: lit once the current word has ended.
- End game.

### Attendance

For each player the server counts turns given (every turn that came to them, skips included) and
turns taken (turns where they picked a letter). A player passes if turns taken × 2 ≥ turns given.
A player who was given no turns passes. Players removed from the list keep the counts they had.

## Word Scramble

### Starting

1. At the lectern: Start Scramble.
2. Pick a list (own lists or "Type a quick list").
3. Pick the time per word from `QuizSeconds` (15, 20, 30, 45, 60).
4. Pick "Counts for attendance", then "Bonus".
5. The quick-list editor opens if chosen. Start there begins the game; otherwise it begins at once.

Everyone in the room except the Teacher plays.

### A word

- The card shows the scrambled letter tiles, the hint, a time bar, a typing box and a Guess button.
  It sits where the quiz card sits.
- The scramble is made once per word on the server, and everyone sees the same one. Each
  space-separated part is shuffled on its own and spaces stay in place. The whole result must differ
  from the word. A word whose letters cannot be rearranged into anything different (for example
  "AAA") is shown as it is.
- A student may guess as often as they like until the deadline, at most 2 guesses a second
  (extra guesses are dropped). The card shows only "Not quite. Keep trying." or the student's own
  solve. It also shows how many classmates have solved it, but never who.
- Points by finish order: first correct 3, second 2, every later correct 1.
- At the deadline, or as soon as every player in the room has solved it, the word is revealed:
  tiles turn green, and the card shows the student's points, who solved it in order, and the top
  scores so far. The Teacher presses Next word.
- After the last word or End game, the card shows final scores and closes.

### Teacher panel

Like the quiz Teacher panel: the scrambled tiles, the answer, "Solved 4 of 9 · 0:12 left", after the
reveal the solve order and the top scores, and Next word and End game.

### Attendance

A student passes if they made at least one guess on at least half of the words that ended.

## No spoilers in chat

While a Hangman or Scramble word is open, a public chat message in the class room that contains the
answer (compared upper-case, ignoring spaces) is not sent. The sender sees "No spoilers while the
word is open." This uses the hook the debate uses to block chat (`FloorBlock`). Whispers and group
chat are not checked.

## Server

In `Chaos/Services/College/`:

- `HangmanSession` and `ScrambleSession`: pure rule classes like `QuizSession`, holding no locks and
  doing no I/O. They take the current time as an argument so tests control it.
- `LiveHangman` and `LiveScramble` in `CollegeTools.cs`: the running game on the class
  (`CollegeClass.Hangman`, `CollegeClass.Scramble`), next to `LiveQuiz`.
- `CollegeService.Tools.cs`:
  - `CollegeTool` gains `Hangman = 2` and `Scramble = 3`. `ToolStatus` gains `HangmanUsed` and
    `ScrambleUsed`.
  - Start, pick, buzz, solve, guess and control methods, following `StartQuiz` and `ControlQuiz`.
  - `ToolTick` drives the turn clock, the solve window and the Scramble deadline.
  - `ToolView` gains `HangmanView?` and `ScrambleView?`, built per viewer. A student's view never
    holds the Hangman word, or the Scramble answer before the reveal.
  - `FailedToolCheck`, `BonusFor` and `TopScorers` learn both games.
- `CollegeService.WordLists.cs`, `WordListRules.cs` and a word-list store: copies of the quiz
  versions, plus a bank page listing and a copy that works on any list.
- `CollegeLecternScript`: the Start Hangman and Start Scramble menus and steps, carrying a
  `HangmanSetup` or `ScrambleSetup` record like `QuizSetup`. There are also "My word lists" entries
  at the lectern and the Registrar.
- The chat check sits next to the debate's floor check in the WorldServer public-message handler.

## Packets

- `CollegeActionType` gains, from 24 upward: WordListOpen, WordListSave, WordListDelete,
  WordListCopy, WordBankPage, WordBankCopy, QuickListStart, HangmanOrder (the turn-order Start),
  HangmanPick, HangmanBuzz, HangmanSolve, HangmanControl, ScrambleGuess and ScrambleControl.
- `HangmanControlType` holds Next, Skip, AddPlayer, RemovePlayer and End. `ScrambleControlType`
  holds Next and End.
- `CollegeDisplayType` gains, from 19 upward: WordLists, WordListEdit (with saved, read-only preview
  and quick modes), WordBank, WordListResult, HangmanOrder, HangmanBoard and ScrambleCard.
  - The board and card carry an `IsTeacher` flag and Teacher-only fields, like `CollegeDebateInfo`.
  - `ToolClosed` closes them, as it closes the quiz card.
- New info records go in `CollegeInfos.cs`. Converters and `CollegeCodec` are extended, and every
  new type gets a round-trip test.
- `CLIENT_VERSION` goes from 781 to 782, along with the test that pins it
  (`GuildEmblemPacketConverterTests`).

## Client

In `Controls/World/Popups/College/`:

- `WordListEditorControl`: two pages like `QuizEditorControl`. Page 1 has Mine and Word bank tabs;
  page 2 edits one list. It also has the quick mode. Its model is `ViewModel/College/WordListDocument`.
- `HangmanTurnOrderControl`: the Teacher's tick-and-order window.
- `HangmanBoardControl`: the centred board (mockup option A). In Casual mode the gallows is replaced
  by a miss count.
- `HangmanTeacherPanel` and `ScrambleTeacherPanel`: side panels.
- `ScrambleCardControl`: the centred card. Tiles shrink to fit a 20-character word on one line.
- Texts and countdowns are added to `ClassToolText` and `ToolCountdown`. Board state that can be
  tested without graphics goes in `ViewModel/College/HangmanBoardState`.
- `WorldScreen.College.cs` routes the new display types.

## Unora

About 10 new lectern dialog templates in `Data/Configuration/Templates/Dialogs/Temauir/mileth/college/`
(Hangman list, mode, attend and bonus steps; Scramble list, time, attend and bonus steps; word-list
entry). The `college_lectern` and registrar menus gain the new options. Dialog text keeps to the
limits in CLAUDE.md: 35-character options, 360-character text and no em dashes.

## Testing

- Server unit tests:
  - `HangmanSessionTests`: turns, skips, absent players, letter scoring, buzz first-arrival, solve
    window and pause, lockout, Survival loss, Casual, last-letter solve, next-word starter, and
    attendance counts.
  - `ScrambleSessionTests`: scramble differs, spaces kept, "AAA" case, guess rate limit, finish-order
    points, early reveal, deadline reveal and attendance.
  - `WordListRulesTests`: word and hint limits, normalisation and duplicates.
  - Word-list service tests: save, copy, bank copy, the 20-list limit and ownership.
  - `CollegeToolServiceTests`: start refusals (one at a time, once per class, Activity class), the
    attendance and bonus guards, and the one-bonus cap.
  - Packet round-trip tests for every new action and display.
  - A chat spoiler test.
- Client tests: `WordListDocumentTests`, `HangmanBoardStateTests`, and the `ClassToolText` and
  `ToolCountdown` additions.
- File server: an AislingRenamer test for `WordLists`.
- In-game checklist (for the user, in a real class with at least 2 students):
  1. Write a list, copy one from the bank, and type a quick list.
  2. Hangman Survival: take turns, let a turn time out, buzz right and wrong, lose a word, use Next,
     Add and Remove.
  3. Hangman Casual.
  4. Scramble with two students racing, including an early reveal.
  5. A chat spoiler attempt.
  6. Attendance and bonus results at class end.
  7. Check that the windows sit right at 640x480.

## Shipping

Server, Unora data and client together, with a server restart and a client release (CLIENT_VERSION
782). File-server AislingRenamer change ships with the next file-server deploy.

## Out of scope

- Teams. Left for a follow-up once classes have tried free-for-all.
- Built-in word lists shipped with the game.
- Hangman or Scramble in Activity classes.
- Spoiler checks on whispers and group chat.
