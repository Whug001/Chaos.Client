# Mileth College part 1: Core

Date: 2026-10-05
Parent design: `2026-10-04-mileth-college-design.md` (same folder). Read it first; this spec only adds
the detail part 1 needs.
Status: written overnight on the user's instruction ("get the core done with recommendations if
needed"). Every judgement call made without the user is listed in **Recommendations made without
the user** at the end.

## Scope

Part 1 delivers, with no client change:

- the way there: a path from Mileth Village Way to a new College Grounds map with the Tavaly temple;
- the Lyceum hall and five subject rooms, moved out of Tagor;
- roles: Director, Teacher (from awards), Knight bootstrap, admin;
- the `/college` admin command;
- the College board;
- the timetable, bookings, walk-in classes, attendance and the end-of-class check;
- Educated Marks: rolls, the Teacher's mark, the "Educated (N)" legend mark;
- the College shop.

Part 1 has no entries, judging, gallery or creation windows. Awards exist in part 1 only through the
admin command, so a server can bootstrap Teachers and test the honorary shop items.

## Repositories

| Repo | Work |
|---|---|
| `Chaos-Server` (server) | `Chaos/Services/College/`, scripts, command, DI, tests |
| `Unora` (content) | map tool, maps, map instances, merchants, dialogs, board template |
| `Chaos.Client` | none (no packets, no `CLIENT_VERSION` change) |

The server sends map files to players, so new maps ship with a server restart, as the Town Hall did.

## World

### Map numbers

| Map | Template | Size | Instance id | Notes |
|---|---|---|---|---|
| Mileth Village Way | 402 (existing) | 16 x 32 | `mileth_village_way` | new path and edge warp |
| College Grounds | **774** (new) | 30 x 30 | `mileth_college_grounds` | Tavaly temple |
| Lyceum hall | 762 (existing) | 45 x 25 | `mileth_college` | moved from Tagor, renamed |
| Chamber of Creation | **775** (new, copy of 763) | 20 x 20 | `college_creation` | Art and Music |
| Chronicle Hall | **776** (new, copy of 763) | 20 x 20 | `college_chronicle` | History |
| Chamber of Quills | 763 (existing) | 20 x 20 | `college_quills` | Literature; moved, renamed |
| Codex Room | **777** (new, copy of 763) | 20 x 20 | `college_codex` | Lore |
| Philosophers' Thought | **778** (new, copy of 763) | 20 x 20 | `college_philosophers` | Philosophy |

Templates 774–778 were free in `Unora/Data/Configuration/Templates/Maps` and in every worktree on
2026-10-05.

Instance folders move from `MapInstances/Temuair/Towns/Tagor/{Lyceum_of_Tagor,Chamber_of_Quills}` to
`MapInstances/Temuair/Towns/Mileth/College/<Name>/`. Nothing links to the old instance ids today:
Tagor's long hall at `tagor:(73, 69)` has no warp into the Lyceum. The Lyceum's exit warp to Tagor
is replaced.

### Map tool

`Unora/Tools/College/college_maps.py` is the single source of truth for every map file above. It
follows `Tools/TownHall/town_hall.py`:

- It keeps base copies beside itself: `village_way_402_base.map` (today's `lod402.map`),
  `lyceum_762_base.map`, `chamber_763_base.map` (today's files). It always builds from the bases,
  so two runs give the same bytes.
- It reads Tavaly from `Unora/UnusedMapData/lod11500.map` (100 x 100).
- Walkability comes only from foreground ids flagged `0x0F` in the server's `sotp.dat`, as in
  `town_hall.py`.
- `--check` runs self-checks and writes nothing.
- It writes a preview PNG of every map it builds into a folder given by `--preview <dir>`, using the
  DALib render approach in `Chaos.Client/Tools/MirrorBackdrops/Program.cs`. If running DALib from
  Python is impractical, a small C# render tool beside it is fine.
- It has unit tests in `Tools/College/test_college_maps.py` (pytest), like `test_town_hall.py`.

### Mileth Village Way (402)

- A dirt path runs from the existing dirt road near (13, 15) south-east to the map's x = 15 edge at
  y = 24–26. It uses the same dirt background ids as the existing patch.
- Warps at (15, 24), (15, 25), (15, 26) lead to `mileth_college_grounds:(1, 14)`, `(1, 15)` and
  `(1, 16)`.
- Nothing else on the map changes. Existing warps, boards and the shrine stay where they are.

### College Grounds (774)

- 30 x 30. Ground and decoration come from Tavaly: its stone paving, grass, standing columns and
  some trees. The jungle trees are optional and must not block the paths.
- **The temple:** the colonnade temple from Tavaly. Copy foreground ids 19701–19761 and the filler
  pieces 20075/20076 from Tavaly's rectangle x 45–58, y 27–47. Do not copy other foreground ids in
  that rectangle (the trees are 16370–16379). Place it centred at the north (low y) side of the map.
- A paved path runs from the west edge arrival tiles (1, 14..16) to the temple's door.
- **Exit:** the west edge tiles (0, 14), (0, 15), (0, 16) warp to `mileth_village_way:(14, 24)`,
  `(14, 25)`, `(14, 26)`.
- **Door:** the temple's door tiles warp to `mileth_college:(43, 10)`. The tool finds the door
  tiles from the pasted pieces, and the preview must show the warp on the doorway.
- Map flags and music: copy Mileth Village Way's instance settings (music 11, day/night on).

### Lyceum hall (762, `mileth_college`)

The map art is kept as the user built it. Instance changes:

- The name becomes "Mileth College". Keep music 16.
- The exit warp at (44, 10) leads to the College Grounds tile in front of the temple door.
- The five subject doors keep their signs at (6,1), (13,1), (20,1), (27,1) and (34,1). The tool
  finds each alcove's door tile, the way (22, 0) is today's Literature door. Each warps to its room's
  arrival tile. The Art sign text becomes "(Art and Music) Chamber of Creation".
- The sixth alcove (far east) stays closed and gets no sign.
- **NPCs:**
  - Veyrin becomes the **Registrar**, standing in the study corner by the round table (about (10, 3);
    the tool picks a walkable tile). Her greeting changes to a College welcome.
  - The Curator of Regalia becomes the **College vendor**. Her old stock (trenchcoat, cloak,
    earring, beard) is removed.
  - The four Lyceum Guards keep patrolling.
- **College board:** a `bulletinboard` reactor on a wall tile near the Registrar, opening the
  `milethcollege` board.

### Subject rooms (763, 775–778)

- Each is a copy of the Chamber of Quills.
- **Lectern:** each room gets a lectern at the front, in the open floor between the banners and the
  benches (about (9, 4)). Recommended art: the marble podium pieces around stc 20161–20166 from the
  Lyceum's own set. The tool renders the room so the choice can be checked; any lectern-like piece
  from that set is fine.
- The lectern tile has a `showDialogOnClick` reactor that opens `college_lectern_initial` (merchant
  `college_lectern`).
- **Exit:** each room's door at the bottom-left (around (8, 18)) warps back to the Lyceum tile in
  front of that room's alcove.
- **Instance:** `scriptKeys: ["collegeRoom"]` (see below). Music 16. No day/night cycle.

## Server

### Configuration: `CollegeOptions`

Bound from `appsettings.json` section `College` (follow how `TownGovernmentOptions` is bound). These
are the defaults:

| Option | Default |
|---|---|
| `TickSeconds` | 30 |
| `MinimumClassMinutes` | 20 |
| `MaximumClassMinutes` | 120 |
| `MinimumStudents` | 3 |
| `PresenceShare` | 0.8 |
| `StartGraceMinutes` | 10 |
| `TeacherAbsenceMinutes` | 10 |
| `BookingDaysAhead` | 14 |
| `TimetableDaysShown` | 7 |
| `FirstRollChance` | 0.20 |
| `RollChanceStep` | 0.10 |
| `EntryCost` | 3 (stored for part 2) |
| `KnightJudgingTeacherLimit` | 3 (stored for part 2) |
| `Rooms` | subject → room instance id (table below) |

| Subject | Room instance id |
|---|---|
| Art | `college_creation` |
| Music | `college_creation` |
| History | `college_chronicle` |
| Literature | `college_quills` |
| Lore | `college_codex` |
| Philosophy | `college_philosophers` |

`CollegeSubject` is an enum: `Art, Music, History, Literature, Lore, Philosophy`.
`AwardTier` is an enum: `None, Clave, Village, Kingdom, Aisling`.

### State: `CollegeState`

Saved through `IStorage<CollegeState>` to `LocalStorage/CollegeState.json`, like
`TownGovernmentState`. Character names are keys and compare case-insensitively.

- `Director` (string?)
- `Bookings`: list of `{ Id, Teacher, Subject, Format, StartUtc, Minutes }`
- `RunningClass`: `{ BookingId?, Teacher, Subject, Format, Room, StartedUtc, PlannedMinutes,
  Presence: name → { Minutes, MachineKey }, TeacherLastSeenUtc, Removed: set of names }` or null
- `Students`: name → `{ Marks, RollChance, FreeEntries, LifetimeMarks }`
- `Awards`: name → subject → tier (the highest award in each subject)
- `History`: the last 50 finished classes `{ Teacher, Subject, StartedUtc, Minutes, Qualified,
  Rolled, Won, Outcome }`, for the admin status view and debugging

`Format` is an enum: `Lecture, Discussion, Activity`. In part 1, `Activity` can't be booked, because
no activity tool exists yet.

### Rule units (no game-world access, unit tested)

Each takes plain data plus `now` (UTC) and returns a result value.

**`CollegeRoles`**
- `CanTeach(name, subject, isKnight, isAdmin, state)`:
  - true for admins and the Director;
  - true for anyone whose award in that subject is Village or higher;
  - true for a Knight when no character holds Village or higher in that subject.
- `IsTeacherAnywhere(...)`: true if `CanTeach` holds for any subject. This is used for the board.
- `CanJudge(...)` and the 3-Teacher cutoff: implemented and tested now for part 2. Nothing in part
  1 calls it.

**`Timetable`**
- `TryBook(teacher, subject, format, startUtc, minutes, now, state)`. It refuses:
  - a start in the past;
  - a start more than 14 days ahead;
  - a length that isn't one of 20, 30, 45, 60, 90 or 120;
  - an overlap with another booking or with the running class.
- Start times are on the hour or half hour.
- `TryCancel(id, by, isAdminOrDirector)`: owners cancel their own bookings; the Director and
  admins cancel any.
- `WalkInLimit(now, state)`: the minutes a walk-in may run, which is the time until the next
  booking, capped at 120. A walk-in needs at least 20.
- `Upcoming(now, days)`: the bookings to show on the timetable.

**`ClassSession`**
- `Start(...)`, `RecordPresence(sample, now)`, `CanEndEarly(now)` (true after 20 minutes) and
  `Finish(now, reason)` → `ClassResult`.
- The end-of-class check, from the parent design:
  1. The class ran at least 20 minutes.
  2. At least 3 students qualify. A student qualifies with presence of at least 80% of the class's
     actual length, rounded down to whole sampling intervals.
  3. The Teacher was present for 80% of the class.
- **One character per computer:** students are grouped by machine key, and only the character with
  the most minutes in each group qualifies. A student sharing the Teacher's machine key never
  qualifies.
- **Machine key:** `Client.LoginId1` as text. If it equals the client's default `0xFF00FF00` (the
  value `MachineIdentity` sends when it can't read the machine), use `ip:` plus `Client.RemoteIp`
  instead.
- A removed student is never counted.
- `ClassResult` holds the outcome, the qualifying names, the failed rule (if any) and the minutes run.

**`MarkLedger`**
- `Roll(name, random)`: succeeds when `random.NextDouble() < chance`. The chance starts at 0.20,
  gains 0.10 per miss, is capped at 1.0, and resets to 0.20 on success. A success adds 1 mark.
- `Give(name, n)` and `TryTake(name, n)` for the shop and admins. Taking never goes below 0.
- The random source is passed in.

### `CollegeService` (singleton)

The only entry point for scripts. It holds a `Lock`, mutates `CollegeState` only inside it, and saves
after each change. Its main members:

- `Book`, `Cancel`, `Timetable`, `StartClass(teacher, subject, format, now)` (booked or walk-in),
  `EndClass(by, now)`, `RemoveStudent(teacher, name)`, `RecordPresence(roomInstanceId, samples, now)`,
  `Tick(now)`, `Balance(name)`, `TryBuy(name, cost)`, `SetDirector`, `SetAward`, `GiveMarks`,
  `TakeMarks`, `Status`.
- **Starting a booked class:** only the booking's Teacher can start it. They have from 10 minutes
  before the slot to `StartGraceMinutes` after it. Starting it removes the booking.
- **Starting a walk-in:** this needs no running class, `CanTeach`, and `WalkInLimit` of at least 20.
- **Where the Teacher must be:** a class can be started only from its subject's room, at the lectern.
  The script checks the room.

### `CollegeTickService` (`BackgroundService`)

Copy `TownGovernmentTickService`. Every `TickSeconds` it calls `CollegeService.Tick(now)`. `Tick`
does four things:

- **Planned end:** it finishes a running class whose planned length has passed.
- **Teacher absence:** it finishes the class when the Teacher has not been seen for 10 minutes. If
  that happens before 20 minutes, the class is called off.
- **No-shows:** it drops bookings whose start plus `StartGraceMinutes` has passed. The Teacher gets
  the message "Your class booking expired."
- **Saving:** it saves state.

### Presence: the `collegeRoom` map script

`CollegeRoomScript : MapScriptBase` with key `collegeRoom`, on all five rooms. In `Update`, every
`TickSeconds` it sends `CollegeService.RecordPresence(Subject.InstanceId, samples, now)`. The samples
are every aisling on the map: name plus machine key. Sampling happens on the map's own update, so
reading the map is thread-safe. The service ignores samples from rooms other than the running
class's room. Each sample counts as `TickSeconds` of presence. A sample that includes the Teacher
sets `TeacherLastSeenUtc`.

### Notifier: `ICollegeNotifier` / `CollegeNotifier`

This copies `ITownNotifier` / `TownNotifier`. It covers:

- `Broadcast(message)`: OrangeBar2 to everyone.
- `Tell(name, message)`: an orange bar if the player is online.
- `RefreshEducatedMark(name, marks)`: sets the legend mark if the player is online.
- `MoveToHall(name, roomInstanceId)`: used to remove a student.

It's built the same way `TownNotifier` gives legend marks. When a player is offline, the legend
mark is refreshed at their next login (see **The "Educated (N)" legend mark**). Moving a player between maps
must use the same thread-safe approach the codebase uses elsewhere for warping players from
services.

### Messages (orange bar text is 45 characters at most)

| When | Who | Text |
|---|---|---|
| Class starts | Everyone | `A {Subject} class with {Teacher} has begun!` (shorten if needed) |
| Roll won | Student | `You earned an Educated Mark!` |
| Roll missed | Student | `No mark this time. Better luck next class.` |
| Teacher paid | Teacher | `You earned an Educated Mark for teaching.` |
| Class failed a rule | Everyone in the room | e.g. `Class ended: fewer than 3 students stayed.` |
| Called off (<20 min) | Everyone in the room | `Class called off before 20 minutes.` |

### The "Educated (N)" legend mark

- **Key:** `collegeEducated`.
- **Text:** "Educated", with the count shown through the existing `LegendMark.Count` display (`ToString()`
  already shows `"{Text} ({Count})"` when the count is above 1). Use `MarkIcon.Wizard` (or the
  closest scholarly icon) in white.
- **Count:** the current balance. It updates in place through `Legend.TryGetValue` and isn't
  removed and re-added. When the balance is 0, the mark is removed.
- **At login:** the College's login hook (on the existing login path, the way
  `DefaultAislingScript` handles queued position removals) refreshes it from `CollegeState`.

### Scripts

**Registrar dialog** (`CollegeRegistrarScript`, merchant `veyrin`). The main menu:

- **Timetable:** the next 7 days, one line per booking: `Tue 14:00 UTC (in 5h) History - Aroha, 45m`.
  It's split across dialog pages so each stays within 360 characters. "Nothing booked" when empty.
- **Book a class** (shown only to someone who can teach), in five steps:
  1. Subject (only subjects they can teach).
  2. Format: Lecture or Discussion.
  3. Day: the next 14 days.
  4. Start time: half-hour slots for that day, with taken slots left out.
  5. Length: 20, 30, 45, 60, 90 or 120 minutes, then confirm.
- **My bookings:** list and cancel. The Director and admins see every booking and can cancel any.
- **My marks:** the balance, plus "Free entries: N" if any.
- **About the College:** two pages that explain classes, marks and Teachers.

**Lectern dialog** (`CollegeLecternScript`, merchant `college_lectern`). What it shows depends on
the state:

- **No class running, speaker can teach this room's subject:**
  - **Begin booked class:** offered if they have a booking that can start now.
  - **Begin a class now:** a walk-in; pick a format and see the allowed length.
- **Speaker's class running here:**
  - **End class:** refused before 20 minutes with the minutes left.
  - **Remove a student:** a list of the people in the room.
  - **Class status:** elapsed and planned minutes, and who has enough presence so far.
- **Anything else:** a short line saying who is teaching and how long is left, or that the lectern
  is free.

**College shop** (`CollegeShopScript`, merchant `regalia`):

- Copy the `TowerMarksShopScript` dialog chain: initial, amount request, confirmation, accepted.
- Stock and prices come from the merchant template's `scriptVars.collegeShop.items`
  (`{ itemTemplateKey: price }`), as the Tower shop does.
- Two more `scriptVars` lists, `collegeShop.artAward` and `collegeShop.loreAward`, name the
  honorary items. Each needs an award of Clave or higher in Art or Lore respectively.
- **Paying:** `CollegeService.TryBuy`, then the item, with the same bag-full refund and gender check
  as the Tower shop. Bought items get `NoNpcSale = true`. If items support a per-instance no-trade
  flag, set it too.

| Item key | Price | Needs |
|---|---|---|
| `male_artist`, `female_artist` | 2 | none |
| `male_lorekeeper`, `female_lorekeeper` | 2 | none |
| `male_PrepUniform`, `female_prepuniform` | 1 | none |
| `male_scholaroutfit`, `female_scholardress` | 1 | none |
| `female_milethcollegeskirt` | 1 | none |
| `honor_male_artist`, `honor_female_artist`, `honor_male_artisthat`, `honor_female_artisthat`, `artist_boots` | 3 | Art award |
| `honor_male_lorekeeper`, `honor_female_lorekeeper`, `honor_male_lorekeeperhat`, `honor_female_lorekeeperhat`, `lorekeeper_boots` | 3 | Lore award |

**College board** (`CollegeBoardScript`, script key `CollegeBoard`, template `milethcollege`). Copy
`TheatreBoardScript`:

- Anyone can view.
- Posting: `IsTeacherAnywhere`, the Director or admins.
- Deleting: the author, the Director or admins.
- Template `scriptVars.collegeBoard.postRetentionHours: -1`.

**`/college` admin command** (`CollegeCommand`, admin only):

| Usage | Effect |
|---|---|
| `/college status` | the Director, running class, next 3 bookings, Teacher counts per subject |
| `/college director <name>` / `/college director none` | sets or clears the Director |
| `/college marks <name>` | shows the balance, roll chance and free entries |
| `/college marks <name> give <n>` / `take <n>` | changes the balance and refreshes the legend mark |
| `/college award <name> <subject> <tier>` | sets the highest award (`none` removes it) |
| `/college endclass` | finishes the running class now (normal end-of-class check) |

### DI and registration

Add `AddCollege()` in `ServiceCollectionExtensions` beside `AddTowns()`, and call it from
`Program.cs`. It registers:

- the options;
- `CollegeService` (singleton);
- `ICollegeNotifier`;
- `CollegeTickService`.

`IStorage<CollegeState>` is already covered by the open generic. Script keys come from the class
names, as for every other script.

## Content (Unora)

- **Map instances and reactors** as described under **World**, written by `college_maps.py`, except
  the hand-written JSON listed here.
- **Merchants:**
  - Edit `veyrin.json`: the script `collegeRegistrar`, name "Veyrin, College Registrar".
  - Edit `regalia.json`: script keys `["collegeShop"]`, stock as in the table above, and name
    "Curator of Regalia".
  - Add `college_lectern.json`: an invisible or blank-sprite merchant, as for the Town Hall stations.
- **Dialogs** under `Templates/Dialogs/Temauir/mileth/college/`: the Registrar, lectern and shop
  chains. Follow the dialog limits: option text 35 characters, dialog text 360 characters, no em
  dashes.
- **Board:** `Templates/BulletinBoards/milethcollege.json`.

## Testing

Unit tests in `Tests/Chaos.Tests/College/` (TUnit, FluentAssertions):

- **`MarkLedgerTests`:**
  - chances run 0.2, 0.3 … 1.0 with a fixed random;
  - a success resets the chance and adds 1;
  - `TryTake` refuses more than the balance.
- **`TimetableTests`:**
  - overlaps;
  - the 14-day limit;
  - the allowed lengths;
  - half-hour starts;
  - a booking overlapping the running class;
  - `WalkInLimit`;
  - cancel rights.
- **`ClassSessionTests`:**
  - the 80% rule at the boundary;
  - 3 machines;
  - one character per machine (the most minutes wins);
  - the Teacher's machine is excluded;
  - the default machine ID falls back to IP;
  - the Teacher below 80%;
  - removed students;
  - ending early before 20 minutes;
  - called off before 20 minutes.
- **`CollegeRolesTests`:**
  - Village and higher teaches only its subject;
  - Clave does not teach;
  - a Knight teaches until a subject has a Teacher;
  - Art and Music are separate subjects even though they share a room;
  - admins and the Director;
  - the judging cutoff at 3 Teachers.
- **`CollegeServiceTests`:**
  - booked start window;
  - walk-in;
  - no-show expiry on `Tick`;
  - auto-end;
  - Teacher absence;
  - rewards paid once;
  - a `TryBuy` failure leaves the balance unchanged;
  - state survives a save and reload.
- **Map tool:** `test_college_maps.py`:
  - the bases rebuild identically;
  - the warps exist and are walkable;
  - the temple copy includes only the allowed ids;
  - the Village Way bytes outside the path are unchanged.
- The full server suite stays at its known baseline: the 2 known failures, GiveAbility and
  OnItemDroppedOn stackable.

**In-game checklist** for the user (not automated):

1. Walk Mileth → Village Way → the new path → the College Grounds → the temple door → the Lyceum.
2. Talk to the Registrar and the Curator. Read the board.
3. As a Knight, book a class, then start it at the lectern.
4. Hold a 20-minute class with 3 students on 3 computers. Check the rolls, the Teacher's mark and
   the legend mark.
5. Try to end early, remove a student, and let a booking expire.
6. Buy an overcoat. Try an honorary piece without and with an `/college award`.

## Recommendations made without the user

These were decided overnight. Each is easy to change.

1. **Machine key:** `LoginId1`, falling back to the IP address when the client sends its default ID,
   so a failed machine read doesn't lump strangers together.
2. **One character per computer per class:** only the character with the most minutes rolls. This
   stops alts on one PC from all rolling.
3. **Times in UTC** with "in N hours", because players span time zones and the server's timed
   systems already use UTC.
4. **Fixed choices for booking:** half-hour start times and lengths of 20/30/45/60/90/120 minutes,
   so booking works through dialog menus without typed input.
5. **Presence by sampling:** every 30 seconds on each room's own update, instead of enter and exit
   events. This is simpler and survives reconnects.
6. **Marks in `CollegeState`,** not character counters, so admins can edit them and offline Teachers
   still get paid.
7. **Classes start at the subject room's lectern,** so the Teacher is in the room when it starts.
8. **Honorary shop pieces need Clave or higher** in Art or Lore. Any award in the subject counts.
9. **Activity format hidden in part 1,** until a creation tool exists.
10. **The sixth Lyceum alcove stays closed,** because Art and Music share the Chamber of Creation.
11. **The Lyceum's study corner** holds the Registrar, and the board sits on the wall beside it.
