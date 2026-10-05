# Mileth College part 2: Entries, judging, writing and the gallery

Date: 2026-10-05
Parent design: `2026-10-04-mileth-college-design.md`. Part 1 spec: `2026-10-05-mileth-college-core-design.md`
(both in this folder). Read them first; this spec only adds what part 2 needs.
Status: approved section by section in brainstorming on 2026-10-05.

## Scope

Part 2 delivers:

- the piece format and its storage, with uploaded pictures;
- the writing window, for the four writing subjects;
- drafts, Submit, Withdraw, voting, the verdict, mail, claiming;
- titles, emblems and legend marks for awards;
- the judging list and the shared reader window;
- the gallery stands and the gallery window;
- writing activity classes with hand-ins.

The entry system covers all six subjects. Art and Music entries open in parts 3 and 4, when the pixel
canvas and the note-grid composer exist. Until then the Registrar shows "Opens soon" for them, and
their gallery stands show "No pieces yet".

## Repositories and branches

Build on top of part 1, on the same branches:

| Repo | Worktree | Branch | Part 1 commit |
|---|---|---|---|
| `Chaos-Server` | `worktrees/college-server` | `feat/college-core` | 69f8fbe6b |
| `Unora` | `worktrees/college-unora` | `feat/college-core` | a460b5a7d |
| `Chaos.Client` | new worktree `worktrees/college-client`, branch `feat/college-core` from client `main` | | |

The client worktree builds `Chaos.Client/Chaos.Client.csproj` with `UnoraServerPath` pointing at the
server worktree, as for the emblems work. The `.slnx` fails in a worktree.

Part 2 ships as server + Unora + client + a setoa.dat launcher patch. The client and setoa.dat go out
in one launcher patch before the server restart.

## Decisions made in brainstorming

| Topic | Decision |
|---|---|
| Piece length | Up to 10,000 characters of text |
| Pictures | Up to 5 per piece, uploaded from the player's PC. Art drawings from part 3 can be inserted once part 3 exists. |
| Picture size | Shrunk by the client to fit 400 x 300 |
| Layout | One long scroll. Pictures sit on their own lines between text blocks. |
| Formatting | Plain text with line breaks, plus one heading style |
| Titles | Tier plus noun, e.g. "Village Historian" |
| Emblems | One design per subject in four tier colours (24 emblems) |
| Writing activity class | The Teacher sets a prompt. Students write and hand in. The Teacher reads hand-ins and can show one to the class. |
| Gallery stands | One stand per subject, opening the gallery on that subject |
| Feedback to the author | Only the Director's note. Judges' comments stay private to the Director and admins. |
| Picture transport | Through the game connection in 32 KB parts, cached by content hash on both sides |
| Art and Music entries | Same entry system, opened by parts 3 and 4 |

Subject nouns for titles:

| Subject | Noun | Emblem design |
|---|---|---|
| Art | Artist | brush |
| Music | Musician | lute |
| Literature | Wordsmith | quill |
| History | Historian | scroll |
| Lore | Lorekeeper | tome |
| Philosophy | Philosopher | eye |

## An entry's life

### Drafts

- Each character has at most one draft per subject.
- The Registrar's **Write an entry** opens the writing window on that subject's draft, or a blank one.
- **Save** sends the whole piece to the server. Closing the window also saves if anything changed.

### Submit

1. The writer presses **Submit** and confirms ("Enter this piece for 3 Educated Marks?" or "...for
   your free entry?").
2. The server refuses if any of these hold:
   - the player already has an open entry (voting or awaiting verdict) in that subject;
   - the piece has no title;
   - the piece has fewer than 200 characters of text;
   - the player has no free-entry token and fewer than `EntryCost` marks.
3. Otherwise the server, inside the College lock:
   1. writes the piece file (a new piece id, so the draft file is untouched if this fails);
   2. takes one free-entry token if the player has one, otherwise `EntryCost` marks;
   3. adds the entry with status `Voting` and `VotingEndsUtc = now + 14 days`;
   4. clears the draft slot and deletes the draft's piece file.
4. If step 1 throws, nothing else happens and the player sees "Your entry could not be saved."

### Withdraw

The author may withdraw an entry while it's in `Voting`. The dialog warns that marks are not
refunded. The entry and its piece file are deleted.

### Voting

- Judges are everyone `CollegeRoles.CanJudge` allows (part 1), except the entry's author.
- A judge sees entries in `Voting` without the author's name.
- A vote is a tier (`None`, `Clave`, `Village`, `Kingdom`, `Aisling`) plus an optional comment of up to
  300 characters. Comments are private to the Director and admins.
- A judge may change their vote until voting closes.

### Verdict

- When `VotingEndsUtc` passes, the tick moves the entry to `AwaitingVerdict`.
- The Director or an admin sees the author, every vote and every comment.
- They set the tier and may write a note of up to 500 characters. The Director can't decide their own
  entry; an admin must.
- The status becomes `Decided`. The author gets mail (see **Mail**).
- **Reminders:** once an entry has waited 7 days, the tick sends one reminder per day. It goes to the
  Director by mail, and to the names in `College.VerdictReminderNames` by mail. An admin who logs in
  while any entry has waited 7 days also gets an orange bar.

### Claim

- The author claims a `Decided` entry from the Registrar's **My entries**.
- A `None` verdict can't be claimed. It only shows "No award" and can be dismissed, which deletes the
  entry and its piece.
- Claiming writes the award to `CollegeState.Awards`, if it's higher than the one held. Teaching
  rights start here, not at the verdict.
- Then the rewards in **Rewards** are given. The status becomes `Claimed`.
- A claimed award lower than or equal to the one already held is recorded, and the piece enters the
  gallery, but no reward changes.

### Removal

The Director or an admin may remove an entry in any status, including a `Claimed` gallery piece. The
piece file is deleted and the author gets mail. Marks are not refunded. Removing a claimed piece does
not remove the award; `/college award` does that.

### Gallery visibility

- Every `Claimed` entry is in its subject's gallery unless the author hid it.
- The author hides or shows it from **My entries**.

### Moderation

Nobody reviews pictures before judges see them. Removal is the safeguard.

## Rewards

Given at claim, through `ICollegeNotifier`, while the player stands at the Registrar.

| Reward | Detail |
|---|---|
| Legend mark | Key `collegeAward{Subject}`, e.g. `collegeAwardHistory`. Text "Village Award in History". Updated in place when the tier rises. Icon and colour: follow the part 1 Educated mark (`MarkIcon.Wizard` or the closest scholarly icon). |
| Title | "{Tier} {Noun}", e.g. "Village Historian". Removes any other College title for that subject. If the removed title was active, the new one becomes active. |
| Emblem | Key `college_{subject}_{tier}` in lower case, e.g. `college_history_village`. Removes any other College emblem for that subject. |
| Free entry | Clave only: +1 `FreeEntries` |

**Login sync:** the College login hook in `DefaultAislingScript` (where part 1 applies the Educated
mark) also makes the award marks, titles and emblems match `CollegeState.Awards`. An admin's
`/college award` on an offline player takes effect at that player's next login. The same sync runs at
once when the player is online.

Emblem templates use the `Staff` source kind (`EmblemSourceKind.Staff`), the kind `EmblemService.Give` grants.
The sync grants and takes them directly.

## Mail

Sent with `IStore<MailBox>` `Load` / `Post` / `Save`, author "Mileth College", so it reaches offline
players.

| When | To | Subject | Body |
|---|---|---|---|
| Verdict posted | Author | `College verdict` | `Your {Subject} entry "{Title}" received: {Tier or No award}.` plus the Director's note, plus "Claim it from the Registrar." when there's an award |
| Entry removed | Author | `Entry removed` | `Your {Subject} entry "{Title}" was removed by the College.` |
| Reminder | Director, `VerdictReminderNames` | `Verdicts waiting` | `{N} College entries have waited over 7 days for a verdict.` |

## Storage

### Pieces

- One file per piece: `LocalStorage/College/Pieces/{pieceId}.json`. The id is a GUID in "N" format.
- Written to a temp file, then moved into place.
- Pieces hold no character names, so renames only touch `CollegeState.json`.

```json
{
  "id": "6f1c...",
  "subject": "History",
  "kind": "Writing",
  "title": "The Fall of Old Mileth",
  "blocks": [
    { "type": "Heading", "text": "The Burning" },
    { "type": "Text", "text": "When the raiders came...\nThe well ran dry." },
    { "type": "Picture", "hash": "9a0b..." }
  ],
  "updatedUtc": "2026-10-05T12:00:00Z"
}
```

`kind` is `Writing` in part 2. `Drawing` and `Tune` are reserved for parts 3 and 4, which add their
own block types.

### `PieceRules` (pure, unit tested)

| Rule | Limit |
|---|---|
| Title | 1-40 characters for Submit; may be empty in a draft |
| Heading block | 1-60 characters, one line |
| Text block | Any length; line breaks allowed |
| Text in all | Headings plus text blocks, at most 10,000 characters |
| Pictures | At most 5 picture blocks; every hash must exist in the picture store |
| Blocks | At most 200 blocks; no two picture blocks may share a hash |
| Submit minimum | Title present and at least 200 characters of text |
| Hand-in minimum | At least 20 characters of text; title optional |

Control characters other than `\n` are stripped on save.

### Pictures

- Stored once each: `LocalStorage/College/Pictures/{hash}.{png|jpg}`. The hash is SHA-256 of the
  file bytes, in lower-case hex.
- The server accepts PNG or JPEG, at most 400 x 300 pixels and 200 KB. It reads the size from the
  file header and refuses anything else.
- **Upload caps per character:** 10 new pictures per hour and 5 MB per day. Pictures the server
  already has don't count.
- An upload in progress is kept in memory per character. It's dropped after 2 minutes without a part,
  which also covers a player who disconnects mid-upload.

### Cleanup (tick, at most once an hour)

- Delete pictures that no piece has referenced for 24 hours. The server tracks "last referenced" in
  memory, rebuilt at startup by scanning pieces.
- Delete hand-ins older than 7 days, with their piece files.

### `CollegeState` additions

- `Drafts`: name -> subject -> piece id.
- `Entries`: list of `CollegeEntry`:
  - `Id` (int, from `NextEntryId`), `Author`, `Subject`, `PieceId`, `Title`;
  - `SubmittedUtc`, `VotingEndsUtc`;
  - `Status`: `Voting`, `AwaitingVerdict`, `Decided`, `Claimed`;
  - `Votes`: judge -> `{ Tier, Comment, Utc }`;
  - `Verdict`: `{ Tier, Note, By, Utc }` or null;
  - `Hidden` (bool), `LastReminderUtc`.
- `HandIns`: list of `{ Id, Teacher, Subject, ClassStartedUtc, Student, PieceId, Prompt, Utc }`.
- `RunningClass` gains `Prompt` (string?).

Withdrawn, dismissed and removed entries are deleted, not kept with a status.

### `CollegeOptions` additions

| Option | Default |
|---|---|
| `VotingDays` | 14 |
| `VerdictReminderDays` | 7 |
| `VerdictReminderNames` | empty list |
| `HandInDays` | 7 |
| `MaxPictureBytes` | 204800 |
| `PictureUploadsPerHour` | 10 |
| `PictureBytesPerDay` | 5242880 |

## Server units

| Unit | Kind | Responsibility |
|---|---|---|
| `PieceRules` | pure | The limits above |
| `EntryBook` | pure | Submit checks, Withdraw, Vote, CloseVoting, SetVerdict, Claim, Dismiss, Remove, SetHidden, RemindersDue. Takes `CollegeState`, plain values and `now`; returns result values. |
| `CollegePieceStore` | file I/O | Load, save, delete pieces |
| `CollegePictureStore` | file I/O | Has, add (with checks), open, delete; the reference scan |
| `PictureUploads` | memory | Part assembly per connection, caps per character |
| `CollegeService` | existing | New members for each action, inside the lock, saving after each change |
| `CollegeRewards` | world | Gives and syncs marks, titles and emblems; called by the notifier |
| `CollegePanel` | world | Builds and sends `CollegeDisplay` messages, like `TownBallotPanel` |

Tick additions in `CollegeService.Tick`: close voting, send reminders, hourly cleanup.

`/college` additions:

| Usage | Effect |
|---|---|
| `/college entries` | Open and waiting entries: id, subject, author, status, days |
| `/college entry <id>` | One entry's status, votes and comments |
| `/college removeentry <id>` | Removes an entry (as **Removal**) |
| `/college closevoting <id>` | Ends voting on an entry now, for testing and fixing mistakes |

## Messages

One message each way, each starting with a sub-type byte, following `TownBallot`.

- `ClientOpCode.CollegeAction = 144`
- `ServerOpCode.CollegeDisplay = 152`
- `CLIENT_VERSION` 771 -> 772. Update the test that pins it
  (`GuildEmblemPacketConverterTests`).
- Limits shared by both sides go in `CollegeProtocol` in `Chaos.DarkAges`, like
  `TownBallotProtocol`.

**`CollegeAction` (client to server)**

| Sub-type | Fields |
|---|---|
| `SaveDraft` | subject, piece |
| `Submit` | subject, piece (the server saves it as the draft, then submits it) |
| `HandIn` | piece |
| `PictureCheck` | hash, byte length |
| `PicturePart` | hash, part index, part count, bytes (at most 32 KB) |
| `PictureFetch` | hash |
| `OpenPiece` | source (`Entry`, `Gallery`, `HandIn`), id |
| `Vote` | entry id, tier, comment |
| `Verdict` | entry id, tier, note |
| `RemoveEntry` | entry id |
| `GalleryPage` | subject |
| `ShowToClass` | hand-in id |

**`CollegeDisplay` (server to client)**

| Sub-type | Fields |
|---|---|
| `OpenWriter` | mode (`Draft`, `HandIn`), subject, prompt, piece |
| `WriterResult` | result code, message |
| `PictureReply` | hash, reply (`Send`, `Have`, `Accepted`, `Refused`), message |
| `PicturePart` | hash, part index, part count, bytes |
| `JudgingList` | rows: entry id, subject, title, days left, my vote; plus waiting rows for the Director and admins |
| `Piece` | context (`Judge`, `Verdict`, `Gallery`, `HandIn`, `Shown`), entry or hand-in id, piece, header text, my vote and comment, votes (Verdict only), author (Verdict, Gallery, HandIn, Shown) |
| `GalleryList` | subject, rows: entry id, title, author, tier, claimed date |
| `HandInList` | rows: hand-in id, student, title, time |

The server checks every action:

- drafts belong to the sender;
- votes need `CanJudge`, never on your own entry;
- verdicts need the Director or an admin, never on your own entry;
- authors are never sent in the `Judge` context;
- removal needs the Director or an admin;
- hand-ins need a running Activity class in a writing subject, with the sender present in its room;
- hand-in lists and Show to class need the class's Teacher (or the Director or an admin).

Opening a piece also needs the right: the author for their own entries, `CanJudge` for `Voting`, the
Director or admins for `AwaitingVerdict` and `Decided`, anyone for a visible `Claimed` piece.

A piece's text is at most about 40 KB even in the worst case, so it fits in one message. Pictures
always travel separately.

## Client

New folder `Controls/World/Popups/College/`, a new `Screens/WorldScreen.College.cs` partial, and a
`CollegeViewState` view model if the windows need one.

### Writing window (`CollegeWriterControl`)

- **Layout:** title box at the top, a scrolling stack of blocks, a footer with **Heading**, **Insert
  picture**, **Save**, **Submit (3 marks)** or **Submit (free entry)**, and a counter
  "2,140 / 10,000 &nbsp; 1 / 5". In hand-in mode, the prompt shows above the title and the buttons are
  **Hand in** and **Close**.
- **Blocks:** a text block is a multi-line `UITextBox` that grows with its text. A heading block is a
  one-line box in a larger, darker style. A picture block shows the picture with a small remove button.
- **Editing rules** live in a pure `WritingDocument` model (unit tested):
  - **Heading** turns the line at the cursor into a heading block, splitting the text block around it.
    Pressing it on a heading turns it back into text.
  - **Insert picture** splits the text block at the cursor and puts the picture between.
  - Enter at the end of a heading moves to the text block after it, creating one if needed.
  - Backspace at the start of a block merges it into the block above. Two text blocks next to each
    other always merge.
  - Typing stops at 10,000 characters.
- **Closing** a draft with changes saves it. Closing a hand-in that hasn't been handed in asks
  "Close without handing in?".

### Pictures in the client

- **Insert picture** opens the Windows open-file dialog (`GetOpenFileNameW` through P/Invoke), filtered
  to PNG and JPG. This is new code; the client has no file picker today.
- The client decodes the file with SkiaSharp and shrinks it to fit 400 x 300, keeping its shape.
- Pictures with any transparent pixel are saved as PNG. Others are saved as JPEG at quality 85.
- If the result is still over 200 KB, the client lowers JPEG quality in steps down to 60, then refuses
  with "That picture is too large."
- **Upload:** `PictureCheck`, then the parts on `Send`. The block shows "Uploading..." until
  `Accepted`, or turns into an error line on `Refused`.
- **Cache:** downloaded and uploaded pictures are kept in a `CollegePictures` folder beside the client
  settings, named by hash. It's capped at 50 MB; the oldest files are deleted first.

### Reader window (`CollegeReaderControl`)

One window opens every piece. It scrolls; headings and pictures show in place. Its footer depends on
the `Piece` context:

| Context | Footer |
|---|---|
| `Judge` | Tier buttons, a private comment box (300), **Save vote**, **Prev**, **Next** |
| `Verdict` | Vote summary ("Village x3, Kingdom x1"), **Votes and comments** (a list popup), tier buttons, a note box (500), **Post verdict**, **Remove entry** |
| `Gallery` | Title, author, award. Directors and admins also get **Remove**. |
| `HandIn` | Student and time, **Show to class** |
| `Shown` | "Shown by {Teacher}" |

### Judging list (`CollegeJudgingControl`)

Tabs **Voting (N)** and, for the Director and admins, **Awaiting verdict (N)**. Rows: subject, title,
days left, your vote. Double-click or **Read** opens the reader.

### Gallery (`CollegeGalleryControl`)

Six subject tabs. It opens on the stand's subject. Rows: title, author, tier badge (Clave bronze,
Village silver, Kingdom gold, Aisling pale blue), sorted by tier then newest. 10 rows per page. Empty
subjects show "No pieces yet".

### Hand-in list (`CollegeHandInsControl`)

Rows: student, title, time. **Read** opens the reader in `HandIn` context.

Mockups from brainstorming: `.superpowers/brainstorm/1113311-1791209310/content/writing-layout.html`
and `windows.html` in this repo.

## Content (Unora)

### Registrar (`CollegeRegistrarScript`)

New main-menu options:

- **Write an entry:** pick a subject. Art and Music show "Opens soon" and do nothing. Then the
  server sends `OpenWriter`.
- **My entries:** one line per entry with subject, title and status. Picking one offers what applies:
  - `Voting`: Withdraw (with the no-refund warning);
  - `Decided`: Claim, or Dismiss for No award;
  - `Claimed`: Hide from gallery or Show in gallery.
- **Judge entries:** shown when `CanJudge`. Opens the judging list.
- **Class hand-ins:** shown to anyone with hand-ins from their classes in the last 7 days. Opens the
  hand-in list.

### Lectern (`CollegeLecternScript`)

- In the four writing rooms, Activity can be booked and started. The part 1 rule that hides
  Activity stays for Art and Music.
- Starting an Activity class asks the Teacher for a prompt of up to 120 characters (a text-entry
  dialog). It is stored on `RunningClass.Prompt` and shown in the class-start world message only as
  "(writing class)".
- During an Activity class, students get **Write a hand-in** (opens the writer in hand-in mode), and the
  Teacher gets **Hand-ins** (the list for this class).
- One hand-in per student per class. Handing in again replaces the earlier one.

### Gallery stands

- `college_maps.py` places one stand on the centre walkable tile of each carpet-framed marble square in
  the Lyceum hall (map 762).
- The four north squares, west to east: History, Literature, Lore, Philosophy. The two south squares,
  west to east: Art, Music.
- Stand art: an existing furniture piece per subject (a book stand for the writing subjects, an easel for
  Art, an instrument for Music). The tool's preview shows the candidates to the user before the
  choice is fixed.
- Each stand tile has a new reactor `collegeGalleryStand` with `scriptVars.collegeGalleryStand.subject`.
  `OnClicked` sends `GalleryList` for that subject, like `TowerLeaderboardBoardScript`.

### Emblems

- 24 templates in `Templates/Emblems/`: `college_{subject}_{tier}.json`, names such as
  "Village Historian", descriptions such as "Village Award in History, Mileth College".
- Art numbers 259-282, in subject order Art, Music, Literature, History, Lore, Philosophy, and tier order
  Clave, Village, Kingdom, Aisling. So Art is 259-262 and Philosophy is 279-282. These were free on
  every branch on 2026-10-05.
- Six base designs are drawn with PixelLab and shown to the user. A script recolours each into bronze,
  silver, gold and pale blue.
- The art goes in `Tools/Emblems/art/custom/embl259.png` to `embl282.png`. A new setoa.dat is built on
  the live set (see the "Dats live 2026-10-04" note) and ships with client 772.

### Messages (orange bar, 45 characters at most)

| When | Text |
|---|---|
| Draft saved | `Draft saved.` |
| Submitted | `Your entry is in! Voting ends in 14 days.` |
| Not enough marks | `You need 3 Educated Marks to enter.` |
| Already entered | `You already have an entry in {Subject}.` |
| Too short | `Entries need a title and 200 characters.` |
| Vote saved | `Your vote is saved.` |
| Verdict posted | `Verdict posted.` |
| Claimed | `You claimed the {Tier} Award in {Subject}!` |
| Handed in | `Your hand-in was received.` |
| Picture refused | the server's reason, e.g. `That picture is too large.` |

Dialog limits apply: option text 35 characters, dialog text 360, no em dashes.

## Testing

**Server unit tests** in `Tests/Chaos.Tests/College/`:

- `PieceRulesTests`: each limit at its edge, the Submit and hand-in minimums, duplicate picture hashes,
  stripped control characters.
- `EntryBookTests`:
  - one open entry per subject;
  - no self-vote;
  - the Director can't decide their own entry, and an admin can;
  - votes change until voting closes and are refused after;
  - CloseVoting on time;
  - reminders on day 7 and once a day after;
  - Claim sets the award only when higher;
  - Dismiss, Withdraw and Remove delete the entry.
- `CollegePictureStoreTests`: PNG and JPEG accepted; other data, over-size pictures, too-large files
  and wrong hashes refused; the hourly and daily caps; cleanup after 24 hours.
- `CollegeServiceTests` additions:
  - a failed piece write leaves marks unchanged;
  - a free token is used before marks;
  - verdict and removal mail are posted;
  - entries and pieces survive a save and reload;
  - hand-ins are deleted after 7 days.
- `CollegeRewardsTests` (or a notifier test): a claim replaces the lower title and emblem, and the login
  sync removes rewards for a removed award.
- Packet round-trip tests for every `CollegeAction` and `CollegeDisplay` sub-type.

**Client unit tests** in `Tests/Chaos.Client.Tests`:

- `WritingDocument`: split, merge, heading toggle, picture insert, the character cap;
- the shrink rule (fit within 400 x 300, keeping shape) and the PNG-or-JPEG choice;
- the cache cap.

**Map tool:** `test_college_maps.py` checks each stand is on a walkable centre tile of its square, has
the reactor, and that the rest of map 762 is unchanged.

The full server suite stays at its known baseline (`OnItemDroppedOn` stackable, and `GiveAbility` if it
still fails on master).

**In-game checklist** for the user:

1. Write a History draft with a heading and two pictures. Save, close, reopen.
2. Submit it. Check the marks drop and the Educated mark updates.
3. As a second character with `/college award ... village`, judge it: read, vote, change the vote.
4. End voting with `/college closevoting <id>`, then post a verdict as the Director. Check the mail.
5. Claim it. Check the legend mark, the title and the emblem. Relog.
6. Open the History stand and read the piece. Hide it, then show it.
7. Run a writing activity class: set a prompt, hand in from two characters, show one to the class.
8. Withdraw an entry, and remove one as an admin.

## Open follow-ups carried from part 1

- The file-server `AislingRenamer` must update `CollegeState.json` before a rename. Part 2 adds
  entries, votes, verdicts, drafts and hand-ins to what it must rename.
