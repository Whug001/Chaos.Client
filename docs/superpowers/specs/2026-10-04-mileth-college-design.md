# Mileth College — overall design

Date: 2026-10-04
Status: approved in brainstorming; each build part gets its own spec before work starts.

## Goal

Mileth College is a gameplay system where players move from Student to Teacher. It is based on
retail's Mileth College (https://vorlof.com/general/milethcollege.html). Unora changes how judging,
rewards and creation tools work.

1. Students attend classes run by Teachers and earn Educated Marks.
2. A student spends 10 marks to enter a piece of their own work in a subject.
3. Judges vote over two weeks; the Director sets the final award tier.
4. A Village award or higher makes the student a Teacher of that subject.

Pieces are made inside the custom client: a pixel canvas, a note grid and a writing window.

## Subjects and creation tools

| Subject | Tool used for entries and activity classes |
|---|---|
| Art | Pixel canvas |
| Music | Note grid composer |
| Literature | Writing window |
| History | Writing window |
| Lore | Writing window |
| Philosophy | Writing window |

## Award tiers

From lowest to highest: **Clave**, **Village**, **Kingdom**, **Aisling**. The Director can also give
**No award**.

Claiming any award gives:

- a legend mark naming the subject and tier, such as "Village Award in Art". It replaces any lower
  award mark in that subject;
- the subject's title and emblem. Exact titles and emblem art are chosen in the part 2 spec;
- for Clave only, one free-entry token.

Awards give no gold or experience.

A player may enter a subject again to climb tiers. Their highest award in each subject is the one
that counts.

## Roles

| Role | How it is held | What it allows |
|---|---|---|
| Student | Anyone | Attend classes, earn marks, enter pieces, use the shop |
| Clave holder | Clave award in a subject | Award rewards only. Cannot teach or judge. |
| Teacher | Village, Kingdom or Aisling award in a subject | Teach that subject only. Judge entries in any subject. Post on the College board. |
| Director | One player, appointed by an admin command | All Teacher rights in every subject. Set final awards. Delete any board post. |
| Knight | Existing `Aisling.IsKnight` | Teach a subject while that subject has no Teacher. Judge while there are fewer than 3 Teachers in total. |
| Admin | Existing admin flag | All Director rights at any time |

Rules the server checks every time someone acts:

- Teacher status is never stored. It comes from the character's awards. If an award is removed,
  teaching rights in that subject go with it.
- No one votes on their own entry. The Director cannot set the verdict on their own entry; an
  admin does that.
- The Knight teaching check runs when a class starts. A class a Knight has already started is
  allowed to finish.
- The Director is stored in College state, so admin changes take effect immediately.

The College board works like the Theatre board. Anyone can read it. Teachers, the Director and
admins can post. A post's author, the Director and admins can delete it.

## Classes

The College has one classroom. Only one class runs at a time.

Teachers choose a format for each class:

- **Lecture:** the Teacher talks in chat. A quiz tool comes in part 5.
- **Discussion:** open talk. A debate tool comes in part 5.
- **Activity:** students use the subject's creation tool. Each activity becomes available as its
  tool is built in parts 2–4.

### Timetable

- A Teacher books at the Registrar. They pick a subject they may teach, a format, a start time up
  to 14 days ahead, and a length from 20 to 120 minutes.
- Bookings may not overlap.
- A Teacher can cancel their own booking. The Director and admins can cancel any booking.
- Anyone can view the next 7 days of bookings at the Registrar.
- **Walk-in class:** a Teacher may start a class at once if the classroom is empty. The class must
  end before the next booking starts. Walk-ins last at most 120 minutes.
- **No-show:** the Teacher has 10 minutes from the booked start to begin the class. After that,
  the slot is freed.
- When a class starts, the server sends a world message naming the subject and Teacher.

### Running a class

- The Teacher starts the class at the lectern.
- The server records each person's minutes on the classroom map.
- The Teacher can end the class only after it has run 20 minutes.
- The Teacher can remove a disruptive student. That student is put outside and cannot come back
  into that class.
- The class ends by itself when the booked length runs out, or after 120 minutes for a walk-in.
- If the Teacher is gone for more than 10 minutes, the class ends.
- If the class ends before 20 minutes for any reason, it is called off with no rewards.

### End-of-class check

All three rules must pass:

1. The class ran for at least 20 minutes.
2. At least 3 students qualify. A student qualifies by being on the classroom map for 80% of the
   class. The 3 students must be on 3 different computers, judged by the two login machine IDs.
   No qualifying student may share the Teacher's computer.
3. The Teacher was present for 80% of the class.

The Teacher of a class is never counted as one of its students. Teachers, Knights, the Director
and admins may attend someone else's class as ordinary students.

If all pass, each qualifying student gets one roll at an Educated Mark and the Teacher gets
1 mark. If any rule fails, no one gets anything, and the server tells the class which rule failed.

## Educated Marks

- **Roll chance:** the first roll is 20%. Each miss adds 10 points, up to 100%, so the 9th try
  always succeeds. A success resets the chance to 20%.
- Marks and roll progress belong to each character and are shared across subjects.
- Marks cannot be traded.
- A legend mark shows the current total, for example "Educated (14)". It updates in place whenever
  marks change.
- **Entry cost:** 10 marks. A Clave free-entry token is used first if the player has one. Taking the
  payment and creating the entry happen as one step. If either fails, neither happens.
- **Shop:** a College vendor sells untradable items for marks. Stock and prices are chosen in the
  part 1 spec. The existing "College" outfits are the starting candidates: artist, prep uniform,
  scholar dress, lorekeeper and Mileth College skirt.
- **Admin command:** an admin can give, take or show a character's marks.

## Entries, judging and the verdict

- A player starts an entry at the Registrar by picking a subject. The subject's tool opens.
- Work is saved on the server as a draft, one draft per subject per character.
- **Submit** takes the payment and starts two weeks of voting.
- A player can have one open entry per subject at a time.
- A piece must be about the game world.
- **Judging window:** judges see open entries with their subject and days left. Opening an entry
  shows the piece itself. The author's name is hidden from judges until the verdict.
- **Votes:** each judge picks No award, Clave, Village, Kingdom or Aisling, and can add a short
  comment. A judge may change their vote until voting closes.
- **Verdict:** after two weeks the entry is "awaiting verdict". The Director or an admin sees all
  votes and comments, sets the final tier, and may add a note to the author.
- The author gets in-game mail when the verdict is posted.
- If an entry waits more than 7 days for a verdict, the Director and admins get a reminder.
- **No award:** the marks stay spent.
- **Removal:** the Director or an admin may remove an entry that breaks the rules. Its marks are not
  refunded.
- **Claiming:** the author claims the award from the Registrar.

## Gallery

A gallery room in the College has display stands. Using a stand opens a gallery window. The window
lists awarded pieces by subject and tier, with the author and award shown. Paintings show full
size, tunes play, and writing can be read. An author can hide their own piece.

## Location

The College is a door inside Mileth town. It leads to:

- an **entrance hall** with the Registrar, the College board and the vendor;
- a **classroom** with a lectern;
- a **gallery**.

Map art and exact placement are chosen in the part 1 spec. The gallery room may be added in part 2.

## Architecture

### Server: `Chaos\Services\College\`

This follows the town government service in `Chaos\Services\Towns\`.

| Unit | Responsibility |
|---|---|
| `CollegeState` | All saved College data in `LocalStorage\CollegeState.json`: Director, bookings, running class, mark balances, roll chances, free-entry tokens, awards, entries, votes, verdicts |
| Piece storage | Drafts and submitted pieces, one file per piece, through the named-instance feature of `IStorage<T>` |
| `CollegeRoles` | Who may teach which subject, judge, or set verdicts. Covers Knight cutoffs and self-judging. |
| `Timetable` | Booking checks: overlaps, 14-day limit, 20–120 minute length, walk-in fit |
| `ClassSession` | Minutes per person in the classroom; the end-of-class check |
| `MarkLedger` | Rolls, spending, the "Educated (N)" legend mark. The random source is passed in so tests can fix outcomes. |
| `EntryBook` | Entries, votes, voting deadlines, verdicts, claims. Payment and entry creation as one step. |
| `CollegeService` | The single entry point for scripts. Calls the units above and saves state. |
| `CollegeTickService` | `BackgroundService` on a 30-second timer. Auto-ends classes, frees no-show bookings, closes voting, sends verdict reminders. |

The rule units (`CollegeRoles`, `Timetable`, `ClassSession`, `MarkLedger`, `EntryBook`) do not touch
the game world. They take plain data and the current time, so they can be unit tested.

Scripts:

- Registrar dialog script: timetable, booking, entries, judging, claiming.
- Lectern dialog script: start and end class, remove a student.
- Classroom map script: entry and exit events for attendance.
- College vendor: sells items for marks.
- `CollegeBoardScript`: board permissions.
- `/college` admin command: set or clear the Director; give, take or show marks; grant or remove
  awards.

### Client

Part 1 needs no client changes; booking and classes use dialogs.

From part 2 on, all College windows share one new message pair: one server-to-client message type
and one client-to-server message type, each with a sub-type byte. New opcode numbers follow the
current highest (client to server 143, server to client 151 as of this date). `CLIENT_VERSION`
goes up with each part that adds client code.

## Build parts

Each part gets its own spec, plan and in-game check.

1. **Core:** College map in Mileth, roles, `/college` command, College board, timetable, walk-in
   classes, attendance, the end-of-class check, Educated Marks, the shop. Lecture and discussion
   classes work in this part.
2. **Entries and judging:** the writing window, drafts, submit, the judging window, verdicts, mail,
   claiming, titles, emblems, the gallery. This covers Literature, History, Lore and Philosophy.
3. **Art:** the pixel canvas, for both entries and activity classes.
4. **Music:** the note-grid composer, for both entries and activity classes.
5. **Lecture tools:** quizzes and debate tools for History, Lore and Philosophy classes.

## Testing

Unit tests in `Chaos.Tests` for the rule units:

- roll chances in order (20, 30 … 100) and the reset after a success;
- booking overlaps, the 14-day limit and walk-in fit;
- the 80% presence rule and the 3-computer rule;
- the Knight teaching cutoff per subject and the 3-Teacher judging cutoff;
- no self-judging, including the Director's own entry;
- a failed entry submission leaving marks unchanged;
- the "end class" action being refused before 20 minutes.

In-game checks cover dialogs, the map, the board and the world message.
