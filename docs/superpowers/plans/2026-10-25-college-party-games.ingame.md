# College party games: in-game check list

Setup: one Teacher and **4 students** in a College class room, with a class running. Use 4, not 3: when a student walks out, 3 would drop the room below the 3-player minimum (see "Leaving").

Start each game from the lectern. The steps are: Start <game> → source → (for a list: mix) → length → timer → attendance → bonus. The game starts when you answer the bonus step.

## Lectern and Registrar

- [ ] The lectern shows "Start Quip Duel", "Start Bluff" and "Start Sketch Guess" next to "Start Scramble". Each one disappears after that game has run in this class.
- [ ] While a game runs, the lectern offers "Show Quip Duel panel" (or "Show Bluff panel", "Show Sketch Guess panel"), not "Show debate panel". Picking it reopens the Teacher panel.
- [ ] "My party lists" shows next to "My word lists" at the lectern and at the Registrar, and opens the party list editor.
- [ ] The timer step lists the default first, marked "(default)": 45 for Quip Duel, 30 for the others.
- [ ] Starting with only 2 students in the room is refused with "A party game needs at least 3 players here."

## Quip Duel (2 rounds, built-in prompts)

- [ ] Every student gets 2 prompts to answer.
- [ ] The duels come up for a vote one at a time.
- [ ] The two authors of a duel see "Your duel! Others are voting." and cannot vote on it.
- [ ] A clean sweep (every vote to one answer) shows its line.
- [ ] The reveal moves on by itself after 5 seconds.
- [ ] "Next round" on the Teacher panel starts round 2.
- [ ] The final card shows for 10 seconds, then closes.

## Bluff (3 questions, from a Teacher list)

- [ ] Typing the real answer as your fake is refused.
- [ ] Two students typing the same fake: it shows once, crediting both.
- [ ] Decoys fill the choices up to 4.
- [ ] You cannot vote for your own fake.
- [ ] The reveal marks the real answer, each fake with its authors, and the decoys.
- [ ] "Next question" works.

## Sketch Guess (60 seconds)

- [ ] Every tool works: pen, big pen, fill, erase, the 8 colours, Undo and Clear.
- [ ] Done sends the drawing and shows "Sent. You can still change it and press Done again."
- [ ] After Done, the canvas stays up. Change the drawing and press Done again: the newer drawing is the one others see.
- [ ] Close the drawing card with Esc and let the clock run out: the drawing is still sent at the deadline.
- [ ] A blank canvas is not sent.
- [ ] A student's title card never shows the real title before the reveal.
- [ ] Students who are not drawing see "You'll be guessing this game."
- [ ] During voting on a drawing, its artist sees "Your drawing! Others are guessing."
- [ ] The artist gets a point for each correct guess.

## Teacher controls

- [ ] "Stop writing now" ends the writing step at once.
- [ ] Strike an answer that already has a vote: the voter can vote again.
- [ ] "Strike drawing" removes a drawing.
- [ ] "Skip this" moves past the current item.
- [ ] "End game" in the middle of a vote ends the game.
- [ ] Each strike writes one line in the server log, "College strike: <Teacher> struck <authors>: <text>".

## Leaving

- [ ] A student walks out mid-write: the step does not wait for them.
- [ ] They come back and still have their score.
- [ ] A new student who enters mid-game sees "You'll join at the next round." and joins the next round at 0.
- [ ] The Teacher walks out: the timer stops. It goes on when they return.
- [ ] Leave only 2 students in the room with the Teacher present: the game holds for 60 seconds, then ends with a message to the room. If a third student comes back within 60 seconds, the game goes on.

## Rude text

- [ ] An answer containing a word from the chat filter list is refused with "Please reword your answer." on the orange bar.
- [ ] A party list title or entry with such a word is refused and not saved.

## Party list editor

- [ ] Make one list of each kind (Quip Duel, Bluff, Sketch Guess).
- [ ] Saving with too few entries is refused. Saving a repeated entry is refused.
- [ ] The Party bank tab shows another Teacher's list. "Copy to mine" works.
- [ ] A list in use by a running game is hidden from other Teachers' bank.
- [ ] "New list" after opening a list makes a new list; it does not overwrite the opened one.

## Rewards

- [ ] With "Counts for attendance" on: after 3 votes are held, a student who answered nothing fails; a student who answered at least half of their prompts passes.
- [ ] With "Bonus" on: the top scorer gets the bonus roll.

## Restart

- [ ] Restart the server in the middle of a game: when the Teacher and students are back in the room, the game resumes at the same step.
