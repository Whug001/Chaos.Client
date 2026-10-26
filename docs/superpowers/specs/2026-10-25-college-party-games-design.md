# Mileth College: Party Games (Quip Duel, Bluff, Sketch Guess)

Date: 2026-10-25. Status: approved in brainstorming, awaiting spec review.

Three new class tools for College Teachers, modelled on Jackbox games: Quip Duel (like Quiplash),
Bluff (like Fibbage) and Sketch Guess (like Drawful). They sit next to Quiz, Debate, Hangman and
Scramble (see `2026-10-05-mileth-college-tools-design.md` and
`2026-10-12-mileth-college-word-games-design.md`).

All three share one engine: players write (or draw) in secret, vote on anonymous answers, then see
who wrote what. Each game adds only its own rules for handing out prompts, voting and scoring.

Mockups: `Chaos.Client/.superpowers/brainstorm/83119-1792949014/content/`
(`party-player-cards.html`, `party-teacher-sketch.html`). The user approved both as shown, including
the simple Sketch Guess drawing tools.

## Rules shared with the other tools

- One tool runs at a time. Each tool runs at most once per class. The three party games are three
  separate tools, so a class may run all three.
- Party games are allowed in every class type (Lecture, Discussion and Activity).
- The Teacher hosts, never plays and never scores.
- Everyone in the room except the Teacher plays. A game needs at least 3 players in the room to
  start (`PARTY_MIN_PLAYERS = 3`).
- The Teacher picks "Counts for attendance" and "Bonus" when starting, as for Hangman.
  - Both take effect only once at least 3 votes have been held (`PARTY_GUARD_VOTES = 3`). A vote is
    one Quip Duel duel, one Bluff question or one Sketch Guess drawing that reached its vote step.
    A duel shown with a single answer and no vote does not count.
  - Attendance: a player passes if they answered at least half of the prompts they were given
    (answers × 2 ≥ prompts given). A Sketch Guess drawing counts as one prompt, and so does each
    fake title they were asked for. A player given no prompts passes. A failed check goes through
    `FailedToolCheck`.
  - Bonus: one extra roll for the top scorers among students who qualified for the class. The best
    score must be at least 1, and at most 3 students (`MAX_BONUS_STUDENTS`) may share it. At most
    one bonus per student per class, from any tool (`BonusFor`, `TopScorers`).

## The shared engine

### Starting

1. At the lectern: Start Quip Duel, Start Bluff or Start Sketch Guess.
2. Pick the prompt source: Built-in, one of my lists (of that game's kind), or my list then built-in.
3. Pick the game's length (rounds, questions or drawing time; see each game).
4. Pick the timer for writing steps from `QuizSeconds` (15, 20, 30, 45, 60). Default 45 for Quip
   Duel, 30 for Bluff and Sketch Guess titles.
5. Pick "Counts for attendance", then "Bonus".
6. The game starts at once.

The game takes a copy of its prompts at start. Editing or deleting a list mid-game changes nothing.

### Steps of a round

1. **Write.** Each player answers their prompts in secret before the timer runs out. A player who
   has answered everything sees "Waiting for others". The step ends early once every player in the
   room has answered.
2. **Vote.** Answers appear in random order with no names. Each voter picks one. A player may not
   vote for their own answer. The vote ends when the timer runs out (15 s,
   `PARTY_VOTE_SECONDS`) or every eligible voter in the room has voted.
3. **Reveal.** Everyone sees the authors and points. See each game for what comes next.

### Text answers

- Answers are trimmed, and runs of spaces are collapsed. Empty answers are refused.
- An answer that trips the in-game chat word filter is refused with "Please reword your answer."
  The player may try again while the timer runs.
- An answer cannot be changed after it is sent.
- Two answers are "the same" if they match after trimming, collapsing spaces and ignoring case.

### Teacher controls

The Teacher panel sits at the right, like the Hangman Teacher panel.

- While players write: the player list with answered counts ("2/2 done", "1/2", "0/2", "away"),
  plus **Stop writing now** (ends the write step at once), Next round (lit only between rounds) and
  End game.
- While players vote: every answer with its author's name, so a strike is an informed choice;
  **Strike selected answer**; **Skip this** (skips the current duel, question or drawing with no
  points); End game; and the running scores.
- A struck answer disappears from every voter's card at once and scores nothing. Votes already cast
  for it are dropped, and those voters may vote again. A struck Sketch Guess drawing skips that
  drawing with no points for anyone.
- Each strike is written to the server log with the Teacher, the author and the text.

### Leaving and joining

- A player who leaves the room keeps their sent answers and their score. While away, they get no
  new prompts and can't vote, and the step does not wait for them.
- A player who comes back rejoins at the next step. If they were in the game before, they keep
  their score.
- A player who first enters the room mid-game joins at the next round with a score of 0. In Sketch
  Guess they only write titles and vote; the drawing step is over.

### Ending

After the last round, or when the Teacher presses End game, every card shows final scores for
10 seconds (`PARTY_FINAL_SECONDS`), best score first, top score in gold. Then the tool closes like
the quiz.

## Quip Duel

- **Length:** 1 to 3 rounds; default 2.
- **Handing out prompts:** each round, players are shuffled into a ring. With n players the round
  uses n prompts, and prompt i goes to player i and player i+1 (wrapping round). Every player gets
  exactly 2 prompts, and every prompt goes to exactly 2 players.
- **Answers:** up to 60 characters (`QUIP_MAX_CHARS`). The write card shows one prompt at a time
  ("Prompt 1 of 2") with a character count.
- **Duels:** after the write step, the round's duels are voted on one at a time, in random order.
  Each shows the prompt and its two answers. Everyone except the two authors votes. The two authors
  see "Your duel! Others are voting."
- **Points:** 1 point per vote. An answer that gets every vote, with at least 2 votes cast, earns
  2 more (a "clean sweep").
- **Missing answers:** if one author didn't answer, the other answer is shown alone with no vote
  and earns 1 point. If neither answered, the duel is skipped.
- **Reveal:** the duel's reveal shows for 5 seconds (`QUIP_REVEAL_SECONDS`), then the next duel
  starts on its own. After the round's last duel, the Teacher presses Next round.
- If two answers to one duel are the same, both are still shown and both authors score the votes.

## Bluff

- **Length:** 3 to 10 questions; default 5. Each question is one round.
- **Question:** up to 120 characters, a hidden real answer of up to 40 characters, and up to 3
  decoys of up to 40 characters.
- **Write:** each player writes one fake answer, up to 40 characters (`BLUFF_MAX_CHARS`).
  - The real answer, typed by a player, is refused with "That's the real answer! Write a fake one."
  - A fake that matches a decoy is allowed; it is shown once as that player's fake, not as a decoy.
  - Two players' fakes that are the same are shown as one choice. Both authors score for it.
- **Vote:** the real answer and every fake in random order. If fewer than 4 choices would show, the
  question's decoys are added until there are 4 or the decoys run out. Each player picks what they
  think is true. A player's own fake is greyed out and cannot be picked.
- **Points:** 2 for picking the real answer. 1 for each other player who picks your fake.
- **Reveal:** the real answer in green with the names of those who found it; each fake with its
  author and how many it fooled; decoys marked "decoy". The Teacher presses Next question.

## Sketch Guess

- **Length:** the Teacher picks the drawing time: 60, 90 or 120 seconds; default 90.
- **Drawing step:** every player gets a different secret prompt and draws it. If more than 10
  players are in the room (`SKETCH_MAX_ARTISTS = 10`), 10 are picked at random as artists and the
  rest see "You'll be guessing this game."
- **Drawing card:** the same 96 x 72 canvas as Art classes, shown at 2x. 8 fixed colours (black,
  white, red, orange, yellow, green, blue, brown), plus Pen, Big pen, Fill, Erase, Undo and Clear.
  **Done** sends the drawing early. When the time runs out, whatever is on the canvas is sent. A
  blank canvas counts as no drawing.
- **Then, for each drawing** that was sent, in random order (one drawing per round):
  1. Everyone except the artist writes a fake title, up to 40 characters, with the drawing shown
     above the box. Typing the real prompt is refused, as in Bluff.
  2. Vote: the real prompt and the fake titles, as in Bluff, with the drawing above the list. The
     artist doesn't vote and sees "Your drawing! Others are guessing."
  3. Reveal, as in Bluff. The Teacher presses Next drawing.
- **Points:** 2 for picking the real prompt. 1 for each player your fake title fools. The artist
  gets 1 for each player who picks the real prompt.
- Sketch Guess has no decoys; if only the real prompt and one fake show, the vote still runs.
- Drawings are not saved to the gallery or anywhere else after the game.
- Drawings travel as the same palette-plus-pixel data the Art canvas uses, with a fixed 8-colour
  palette. They are checked against `DrawingRules` sizes on the server.

## Prompt lists

### Kinds

| Kind            | Entry                                                              |
|-----------------|--------------------------------------------------------------------|
| Quip prompts    | One line, up to 80 characters. May contain `____` as the blank.    |
| Bluff questions | Question (≤120), real answer (≤40), 0 to 3 decoys (≤40 each).      |
| Sketch prompts  | A thing to draw, up to 30 characters.                              |

Entries in one list may not repeat (same-text rule above). Text goes through the chat word filter
on save.

### Built-in set

- Ships as data in Unora: one JSON file per kind under the College data folder.
- About 200 Quip prompts, 120 Bluff questions and 200 Sketch prompts. Quip and Sketch prompts are
  written with a Temuair flavour.
- Bluff questions are mostly odd real-world facts. A Temuair question is included only when its
  answer can be checked against Unora's own data (maps, NPCs, items, lore texts).
- The server keeps the last time each built-in entry was used (in `CollegeState`), and picks the
  least recently used entries first, breaking ties at random. Entries repeat only once a kind's
  set is used up.

### Teachers' lists

These follow the word-list rules:

- A list has an id, a kind, a title (up to 40 characters) and 5 to 50 entries (Bluff: 3 to 30).
- Each Teacher keeps up to 20 party lists across all three kinds. Who may keep lists follows the
  quiz rule (`CollegeRoles.CanPost`).
- Stored like word lists: one JSON file per list in `WorksDirectory/PartyLists`, and an index
  `CollegeState.PartyLists` so list screens never open the files.
- A Party bank lists every Teacher's lists by kind, with author and entry count, 10 per page.
  Preview is read-only; Copy makes "Copy of " + title (cut to 40) and counts against the 20.
  Lists in a running class game are hidden from non-owners, as word lists are.
- Only the owner may edit or delete a list.
- AislingRenamer in Chaos.FileServer must rewrite owner names in `CollegeState.PartyLists`.

### Prompt source at start

- **Built-in:** entries come from the built-in set.
- **One of my lists:** entries come from that list only. If it holds too few, the game says so at
  start and refuses (Quip Duel needs players × rounds prompts; Bluff needs the question count;
  Sketch Guess needs one per artist).
- **My list, then built-in:** the list's entries first, in random order, then built-in entries for
  any gap.

There is no "type a quick list" option in this batch.

## Client windows

Approved mockups as listed above.

- **PartyCardControl:** the player's card, in the quiz card's place and frame. It shows the write,
  vote, reveal and final-scores views for all three games. Timer bar and countdown as on the quiz
  card (`ToolCountdown`).
- **PartyTeacherPanel:** the Teacher's side panel described under Teacher controls.
- **SketchDrawCard:** the Sketch Guess drawing card. It reuses `PixelDrawing` and `ArtEditor` with
  the 8-colour palette and the reduced tool set; it does not open the full `ArtCanvasControl`.
- **PartyListEditorControl:** the list editor with Mine and Party bank tabs and a kind picker,
  built like `WordListEditorControl`. The Bluff entry editor has fields for the question, answer
  and decoys.
- New Action and Display values for `CollegeActionType` and `CollegeDisplayType` take the next free
  numbers, chosen at plan time. The change bumps `CLIENT_VERSION` (now 806), so it needs a client
  release.

## Server shape

- `LiveParty` (on the class session, beside `LiveHangman`) holds the game: kind, prompts, round,
  step, deadline, players and names, answers, votes, scores, prompts given and answered per player,
  strikes and votes held.
- `PartySession` holds the shared rules: steps, timers, voting, strikes, leaving and joining,
  attendance and the guard.
- `QuipRules`, `BluffRules` and `SketchRules` each give the prompt hand-out, the choices to show
  in a vote, and the points.
- `PartyListStore` + `PartyListRules` mirror `CollegeWordListStore` + `WordListRules`.
- `PartyPromptBank` loads the built-in set and picks least-recently-used entries.
- Timers advance from `CollegeTickService`, as Hangman's do.
- Lectern and registrar dialog templates in Unora gain the three Start options and the list
  editor entry.

## Error handling

- A server restart mid-game resumes the game where it was, like the other tools (decided 2026-10-25).
- If the Teacher leaves the room, the game pauses timers until they return or the class ends; on
  class end the game ends with no final scores card.
- If fewer than 2 eligible players remain in the room, the current step runs on its timer; the
  game does not end on its own. The Teacher may End game.
- Packets from a player not in the game, for the wrong step, or for a prompt not theirs are
  ignored. Oversize text or drawings are refused.
- Answers and votes are rate-limited like Hangman picks.

## Testing

- Server unit tests for `PartySession` and each rules class: prompt hand-out for 3 to 12 players,
  the ring rule, clean sweep, missing answers, Bluff real-answer refusal, duplicate merging,
  decoy fill, own-vote refusal, strike with votes re-opened, artist cap at 10, attendance and the
  3-vote guard, bonus through `TopScorers`, leave/rejoin scores, late joiners.
- Server tests for `PartyListRules`, the store, the bank's hiding rule, and least-recently-used
  picking in `PartyPromptBank`.
- A data test that the built-in files load, meet the length rules and have no repeats.
- Client tests for the card's view switching, countdown text, and the Sketch card's palette and
  tools.
- File server test for the AislingRenamer change.
- In-game check list written at the end of the plan (one run of each game with 3 players, plus the
  Teacher controls, leaving and the list editor).

## Out of scope

- A quick typed list.
- Saving Sketch Guess drawings.
- Audience mode, teams, and playing from a phone.
- More Jackbox games; the engine is meant to make one cheap to add later.
