# Mileth College part 4: The Music composer

Date: 2026-10-05
Parent design: `2026-10-04-mileth-college-design.md`. Part 1 spec: `2026-10-05-mileth-college-core-design.md`.
Part 2 spec: `2026-10-05-mileth-college-entries-design.md`. Part 3 spec: `2026-10-05-mileth-college-art-design.md`
(all in this folder). Read them first; this spec only adds what part 4 needs.
Status: approved section by section in brainstorming on 2026-10-05.

## Scope

Part 4 delivers:

- a note-grid composer window in the client, for Music entries and Music hand-ins;
- a sound maker in the client that turns a tune into audio, and a player for it;
- Music pieces on the server: a tune block, its rules and storage;
- Music entries at the Registrar, and Music activity classes at the lectern;
- tunes in the reader, a Play button on each row of the gallery's Music tab, and tunes that play for the whole class
  on **Show to class**.

After this part, every subject is open.

## Decisions made in brainstorming

| Topic | Decision |
|---|---|
| Layers | Three: Melody, Bass, Drums. Notes can be held for several beats. |
| Sound | Made by the client from code (modelled instruments). No sound files. Every player hears the same tune. |
| Instruments | The melody picks Lute, Harp, Flute or Bells for the whole tune. Bass (a low plucked string) and drums (frame drum, tambourine, wood block) are fixed. |
| Scales | Major, Minor, Five-note, Dorian, Mixolydian, Desert (harmonic minor). Rows always follow the scale. |
| Speed | Five named speeds: Slow 80, Walking 100, Steady 120, Lively 140, Quick 160 beats a minute |
| Length | 64 columns (8 bars), 12 to 24 seconds depending on speed |
| Window layout | All three layers stacked in one window (mockup B) |
| Music entry | A title, one tune and a composer's note of up to 300 characters |
| Gallery Music tab | Text rows with a **Play** button on each row |
| Show to class | The tune plays at once for everyone in the room |
| Tunes in writing pieces | No |
| How tunes play | The whole tune is rendered to one buffer, then played as one sound |
| Volume | Tunes follow the sound effects volume. Map music fades out while a tune plays. |

Mockups from brainstorming: `.superpowers/brainstorm/1236794-1791241737/content/` in this repo (`tune-size.html`,
`sound-style.html`, `scales.html`, `window-layout.html`). They play sound in the browser; the instrument code there is
the starting point for `TuneSynth`.

## Repositories and branches

| Repo | Worktree | Branch | Starts from |
|---|---|---|---|
| `Chaos-Server` | `worktrees/college-music-server` | `feat/college-music` | server master 79b4c6511 (parts 1-3, not pushed) |
| `Unora` | `worktrees/college-music-unora`, only if a change turns out to be needed | `feat/college-music` | Unora main 48baa8d18 |
| `Chaos.Client` | `worktrees/college-music-client` | `feat/college-music` | client main e12d396f |

The client worktree builds `Chaos.Client/Chaos.Client.csproj` with `UnoraServerPath` pointing at the server worktree,
as in parts 2 and 3. The `.slnx` fails in a worktree.

Registrar and lectern dialogs are server scripts, so Unora is expected to need no changes. The plan confirms this and
edits any dialog text that still says "write" or "draw" where Music now applies.

## The tune

### What a tune holds

| Field | Values |
|---|---|
| Scale | `Major`, `Minor`, `FiveNote`, `Dorian`, `Mixolydian`, `Desert` |
| Speed | `Slow` (80), `Walking` (100), `Steady` (120), `Lively` (140), `Quick` (160) beats a minute |
| Instrument (melody) | `Lute`, `Harp`, `Flute`, `Bells` |
| Notes | Up to 512 notes, each `{ Layer, Row, Start, Length }` |

- **Layers:** `Melody` = 0, `Bass` = 1, `Drums` = 2.
- **Columns:** 64. One column is half a beat, so a column lasts `60 / bpm / 2` seconds. 64 columns take 24 seconds at
  Slow and 12 seconds at Quick.
- **Rows:** Melody and Bass have 15 rows each; row 0 is the lowest. A row is a step of the tune's scale, not a fixed
  pitch, so changing the scale moves every note into the new scale.
  - Pitch of a row (as a MIDI note number): `base + 12 * (row / n) + scale[row % n]`, where `n` is the number of notes
    in the scale. Melody's base is 60 (middle C); Bass's base is 36, two octaves lower.
  - Scale steps, in semitones from C:

    | Scale | Steps |
    |---|---|
    | Major | 0 2 4 5 7 9 11 |
    | Minor | 0 2 3 5 7 8 10 |
    | FiveNote | 0 2 4 7 9 |
    | Dorian | 0 2 3 5 7 9 10 |
    | Mixolydian | 0 2 4 5 7 9 10 |
    | Desert | 0 2 3 5 7 8 11 |

  - So a 7-note scale spans two octaves and one note in 15 rows; Five-note spans three octaves.
  - Drums have 3 rows: 0 frame drum, 1 tambourine, 2 wood block.
- **Note rules:**
  - `Start` is 0-63 and `Length` is at least 1, with `Start + Length` at most 64.
  - Drum notes are always 1 long.
  - Two notes in the same layer and row may not overlap. Notes on different rows may sound together (chords).

### Music piece shape

A piece with subject Music has kind `Tune` (`CollegePiece.KindOf`; the `Tune` value already exists, unused). It holds:

- a title (rules as in part 2: 1-40 characters for Submit, may be empty in a draft);
- exactly one `Tune` block;
- at most one `Text` block: the composer's note, up to 300 characters, line breaks allowed.

Heading, picture and drawing blocks are refused in Music pieces. Tune blocks are refused in every other subject.

### Storage

`PieceBlockType` gains `Tune = 4`. On disk, inside the existing piece file:

```json
{ "type": "Tune", "scale": "Dorian", "speed": "Lively", "instrument": "Harp", "notes": "<base64, 4 bytes per note>" }
```

Each note is 4 bytes: layer, row, start, length. Scale, speed and instrument are written by name through the existing
`JsonStringEnumConverter`. Fields that don't apply to a block are skipped when null, as for drawings.

### Messages

`CollegeBlockKind` gains `Tune = 4`. In `CollegeCodec`, a tune block is:

| Field | Size |
|---|---|
| kind (`Tune`) | 1 byte |
| scale, speed, instrument | 1 byte each |
| note count | 2 bytes |
| notes | 4 bytes each (layer, row, start, length) |

A whole Music piece is under 3 KB, so it fits in one message.

New constants in `CollegeProtocol`:

| Constant | Value |
|---|---|
| `TUNE_STEPS` | 64 |
| `TUNE_PITCH_ROWS` | 15 |
| `TUNE_DRUM_ROWS` | 3 |
| `MAX_TUNE_NOTES` | 512 |
| `MAX_MUSIC_NOTE_CHARS` | 300 |
| `MIN_ENTRY_MELODY_NOTES` | 16 |
| `MIN_ENTRY_MELODY_ROWS` | 3 |
| `MIN_HAND_IN_NOTES` | 8 |

The scale, speed, instrument and layer enums live in `Chaos.DarkAges` so both sides share them. Speed's beats per minute
come from one shared helper.

Other message changes:

- **`OpenWriter`** is reused. When its subject is Music, the client opens the composer, in `Draft` or `HandIn` mode.
- **`SaveDraft`, `Submit`, `HandIn`** carry Music pieces through the same codec, with the new tune block.
- **New `CollegeActionType.TuneFetch = 13`**: entry id.
- **New `CollegeDisplayType.Tune = 9`**: entry id, tune block.
- `CLIENT_VERSION` 773 -> 774. Update the test that pins it (`GuildEmblemPacketConverterTests`).

The server answers `TuneFetch` only for an entry that is `Claimed`, has subject Music and is not hidden: the same right
as opening it from the gallery. Anything else gets no reply. It shares the 60-per-minute fetch limit with
`DrawingFetch` (`TakeFetch` in `CollegePanel`). The fetch reads the piece file; it changes no state.

### Rules (`PieceRules`, pure, unit tested)

A new private `CheckMusic` sits beside `CheckArt` in `Check`. `CheckEntry` and `CheckHandIn` gain Music branches.

| Rule | Check result when broken |
|---|---|
| Scale, speed and instrument are known values | new `BadTune` |
| Every note in the grid: layer known; row below 15 (melody, bass) or 3 (drums); length at least 1; start + length at most 64 | `BadTune` |
| Drum notes are 1 long | `BadTune` |
| No two notes overlap in the same layer and row | `BadTune` |
| At most 512 notes | `BadTune` |
| Music piece has exactly one tune block | `BadTune` |
| Composer's note at most 300 characters | `TooMuchText` |
| Heading, picture or drawing block in a Music piece | `WrongBlock` |
| Tune block outside Music | `WrongBlock` |
| Submit: title present, and at least 16 melody notes on at least 3 different rows | `NoTitle` / `TooShort` |
| Hand-in: at least 8 notes in any layer | `TooShort` |

The Submit minimum stops a drum loop or one repeated note being entered for marks. The server never makes sound. A
modified client could still send a tune it did not compose in the window; removal by the Director or an admin is the
safeguard, as for pictures and drawings.

`CollegeText.PieceProblem` takes the subject instead of `bool art`, and gains the Music texts in **Messages** below.

### Opening Music

- `CollegeOptions.OpenSubjects` gains Music. The Registrar's "(opens soon)" code stays but no subject uses it.
- `CollegeOptions.HasActivity` becomes true for every subject. `IsWritingSubject` is unchanged.
- `CollegeService.HandIn` already checks that the piece's subject matches the running Activity class; Music needs no
  new check there.
- `CollegeText.ClassBegun` ends a Music activity class's world message with " (music class)", as Art ends with
  " (drawing class)".

## The client

### Tune model (`ViewModel/College/TuneDocument`, pure, unit tested)

Holds the notes, scale, speed and instrument, and performs every edit:

- **Place:** a note of length 1 on an empty cell. While the button is held, dragging right lengthens it, up to the next
  note in that row or the end of the grid. Drum notes stay 1 long.
- **Remove:** the note covering a cell.
- **Copy bars 1-4 to 5-8:** in one layer, replaces columns 32-63 with a copy of columns 0-31. A note that crosses
  column 32 is first cut to end at column 32, then copied.
- **Clear layer:** removes every note in one layer.
- **Scale, speed, instrument:** each change is one edit.
- **Undo / Redo:** one step per place (including its drag), remove, copy, clear, or scale, speed or instrument change.
  Up to 50 steps; the oldest is dropped after that. Any new edit clears Redo.
- **Counts:** melody notes, distinct melody rows and total notes, so the window can warn before Submit or Hand in.
- Edits never create an overlap or a note outside the grid, and refuse a 513th note.

A small shared helper, `TuneScales`, gives a row's MIDI note for each scale and layer, and a row's name ("C4", "F#3").
It is used by the window, the reader and the sound maker.

### Sound maker (`Systems/College/TuneSynth`, pure, unit tested)

Turns a tune into one buffer of 16-bit stereo samples at 48,000 Hz, the mixer's own format.

- **Lute, Harp, Bass:** plucked strings (the Karplus-Strong method: a burst of noise fed through a short delay line
  that softens it each pass). Lute is bright with a 2-second ring, Harp is softer with a 3-second ring, and Bass is dark.
- **Flute:** a soft sine tone with a quiet second harmonic, a gentle 5 Hz wobble in pitch, and a short breath of noise
  at the start.
- **Bells:** three sine tones at 1, 2.76 and 5.4 times the pitch, each fading over time; the higher ones fade faster.
- **Drums:** frame drum (a low sine that drops in pitch, plus a soft thump), tambourine (three quick bursts of
  high-pass noise), wood block (a short high click).
- A held note sounds for its length, then fades over 0.25 seconds. Plucked notes may die away before their length ends.
- All noise comes from a random generator with a fixed seed per note (from its layer, row and start). The same tune
  always gives exactly the same samples, on every PC.
- After mixing, if the loudest sample would distort, the whole buffer is scaled down to fit.
- The buffer lasts the tune's 64 columns plus 1.5 seconds of ring-out.
- A single note can be rendered on its own, for the preview when it is placed.

The browser mockups hold working versions of every instrument. Their settings are the starting point.

### Player (`Systems/College/TunePlayer`)

Plays one tune at a time on top of `SoundSystem`.

- **Play:** renders the tune on a background thread, then plays it. While it renders, the window's Play button reads
  "Wait...". Rendering is expected to take tens of milliseconds.
- **Through the mixer:** the buffer gets a WAV header in memory and loads through the existing `Mix_LoadWAV_RW` path.
  `SoundSystem` gains a method to play such a chunk on one channel kept for tunes (the last of the 32 channels, left
  out of the sound-effect pool). The chunk is freed when the tune stops or ends.
- **Volume:** tunes use the sound effects volume (`ClientSettings.SoundVolume`).
- **Map music:** fades out over about 0.3 seconds when a tune starts, by stepping `Mix_VolumeMusic` in
  `SoundSystem.Update`, and fades back to the music volume when the tune stops or ends. A music volume change while a
  tune plays is saved and applied after the fade back.
- **Playhead:** the current column, from the time since playback began. Controls ask the player each frame.
- **Owner:** each Play names the control that started it. Starting another tune stops the first, and its owner's
  button goes back to **Play**.
- **Preview:** placing a note plays just that note. Rendered notes are kept by layer, row, scale and instrument, so
  the same note is not rendered twice.
- **Stop:** closing the window that owns the tune, changing map, or logging out stops it.

### Composer window (`Controls/World/Popups/College/MusicComposerControl`)

560 x 464, built like `ArtCanvasControl` (`GuildCloakDialogBase`, hand-placed buttons). It follows the
`window-layout.html` mockup, option B (stacked), with cells grown to 8 x 7:

- **Top:** caption `Music: "{title}"`, then the **Title** box and three buttons that cycle on each click: **Scale**,
  **Speed** and **Melody** (the instrument).
- **Beat bar:** bar numbers 1 to 8 over the grids. Clicking a number plays from that bar.
- **Grids:** Melody (15 rows), Bass (15 rows) and Drums (3 rows), each with its name and row names at the left. The
  scale's home note is lit on each octave. Bar lines every 8 columns, fainter lines every 2.
- **Layer choice:** clicking a layer's name selects it (lit). **Copy bars 1-4 to 5-8** and **Clear layer** act on the
  selected layer. Melody starts selected.
- **Toolbar:** **Play** / **Stop**, **Loop** (a toggle), **Undo**, **Redo**, **Copy bars 1-4 to 5-8**,
  **Clear layer**.
- **Playhead:** a light line across all three grids and the beat bar while the tune plays.
- **Below:** the **Note** box (3 lines, 300 characters) and a counter "72/300".
- **Footer:** **Save**, **Submit (3 marks)** or **Submit (free entry)**, a status line and **OK**, as on the Art canvas.

Mouse and keys:

- The left button on an empty cell places a note and plays its preview; dragging right holds it longer. The left
  button on a note removes it. The right button always removes.
- Space plays or stops. Ctrl+Z undoes and Ctrl+Y redoes. These work while no text box has focus.
- An edit during playback does not change what is playing; the next **Play** uses it.
- With **Loop** on, the next pass is rendered in the background during the current one, from the tune as it is then,
  so edits are heard on the next pass. The next pass starts when the playhead reaches column 64, cutting off the
  current pass's ring-out.

Modes:

| Mode | Opened by | Differences |
|---|---|---|
| `Draft` | `OpenWriter` (Draft) for Music | As above. Closing with changes saves the draft, as the other windows do. |
| `HandIn` | `OpenWriter` (HandIn) for Music | The caption shows the class prompt. Buttons are **Hand in** and **Close**. Closing before handing in asks "Close without handing in?". |

There is no Picture mode. Before sending Submit or Hand in, the window checks the counts against the minimums and shows
the same message the server would, so the player isn't charged a round trip for an obvious refusal. The server still
checks.

### Showing tunes

- **Routing (`WorldScreen.College.cs`):** `OpenWriter` and `WriterResult` for Music go to the composer.
- **`PieceView`** gains a tune block. It shows a line such as "Dorian, Lively, Harp" with a **Play** / **Stop**
  button, then the three grids read-only at 6 x 4 cells (384 wide, like a drawing), with the playhead while playing.
  The composer's note follows as a text block. This covers every reader context: Judge, Verdict, Gallery, HandIn,
  Shown and Own.
- **Show to class:** when a `Piece` arrives in the `Shown` context and holds a tune, the reader opens and the tune
  starts at once. Everyone in the room, the Teacher included, hears it within a fraction of a second of each other.
- **Gallery Music tab (`CollegeGalleryControl`):** keeps its text rows (title, author, award) and adds a small
  **Play** button at the right of each row. The title column narrows to make room. Pressing **Play** sends `TuneFetch`
  if the tune isn't cached, then plays it when the `Tune` reply arrives. The button reads **Stop** while its tune plays.
  **Read** still opens the reader.
- **`CollegeTunes`** (`Systems/College/`) keeps fetched tunes by entry id until logout, like `CollegeDrawings`.
  A tune is fetched only when someone presses its **Play**. If no reply comes within 5 seconds (for example, the fetch
  limit was hit), the button goes back to **Play** and a later press fetches again.

## Content

### Registrar (`CollegeRegistrarScript`)

- **Make an entry** -> Music sends `OpenWriter` for Music, which opens the composer.
- The Activity format is offered for Music bookings.
- **My entries**, **Judge entries** and **Class hand-ins** need no change; the reader shows tunes.

### Lectern (`CollegeLecternScript`)

- In the Chamber of Creation, Music Activity classes can be booked and started, as Art ones can.
- Starting a Music Activity class asks for a prompt of up to 120 characters.
- During a Music Activity class, students get **Compose a hand-in** (opens the composer in HandIn mode), and the Teacher
  gets **Hand-ins**. Art keeps **Draw a hand-in** and the writing subjects keep **Write a hand-in**.
- One hand-in per student per class; handing in again replaces it.

### No map or art changes

The Music gallery stand, the Music emblems (263-266) and the shop are already in place. No .dat files ship with this
part.

### Messages (orange bar, 45 characters at most)

| When | Text |
|---|---|
| Too few melody notes to enter | `Entries need 16 melody notes on 3 rows.` |
| Too few notes to hand in | `Hand-ins need at least 8 notes.` |
| Bad tune data | `That tune could not be read.` |
| Draft saved, submitted, handed in | Same texts as part 2 |

Dialog limits apply: option text 35 characters, dialog text 360, no em dashes.

## Plan-time adjustments

- Unora needs no changes; no dialog text says "write" or "draw" where Music now applies.
- Extra player texts: "Give your tune a title.", "Music pieces hold one tune and a note.", and the wrong-block text
  "That piece holds a part it can't."
- An Art piece holding a tune block is refused (`WrongBlock`).
- The server's save throttle lets each subject's draft through once per burst, so three College windows open at
  logout all save.
- The per-minute fetch limit moved into `CollegeFetchLimit`, shared by `DrawingFetch` and `TuneFetch`.
- A new tune starts at Major, Steady, Lute.
- The layer is picked with **Melody**, **Bass** and **Drums** buttons at the left of the toolbar (or by clicking a
  cell), not by clicking a name beside the grid: a name row above each grid does not fit with the 3-line note.
- Gallery columns on every tab move to 0 / 236 / 356 to make room for the Music rows' **Play** button.
- Note previews use the tune channel without fading the map music, and are skipped while a tune plays or renders.
- Loop asks the composer for the next pass; turning Loop off mid-pass ends playback after that pass.
- With Loop on, the next pass is asked for and rendered about 0.5 s before the current pass ends, so edits are heard on
  the next pass.
- A plucked note held longer than its ring fades out over 20 ms at the end of its sound, so it doesn't click.
- Changing the gallery's tab stops a gallery tune and forgets a Play still waiting for its tune.

## Testing

**Server unit tests** in `Tests/Chaos.Tests/College/`:

- `PieceRulesTests` additions:
  - unknown scale, speed, instrument or layer refused;
  - notes outside the grid refused: row 15 in melody or bass, row 3 in drums, length 0, start + length 65;
  - a drum note of length 2 refused;
  - two notes overlapping in one row refused; two notes in the same column on different rows accepted;
  - 512 notes accepted and 513 refused;
  - the composer's note at 300 and 301 characters;
  - heading, picture and drawing blocks refused in Music; tune blocks refused outside Music;
  - Submit with 15 and 16 melody notes, and with 2 and 3 distinct melody rows; bass and drum notes don't count;
  - hand-in with 7 and 8 notes, counted across all layers.
- `CollegeServiceTests` additions:
  - a Music piece survives a save and reload, every field unchanged;
  - a Music draft can be submitted end to end;
  - Music hand-ins are accepted only in a running Music Activity class, in its room;
  - a Music piece handed in to an Art class is refused, and the reverse.
- `Timetable` / booking: a Music Activity class can be booked and started.
- `TuneFetch`: answered for a visible `Claimed` Music entry; no reply for hidden, `Voting`, non-Music or unknown
  entries; counted against the shared fetch limit.
- Packet round-trip tests: tune blocks with 0 and 512 notes, `TuneFetch`, `Tune`, and the `CLIENT_VERSION` 774 pin.

**Client unit tests** in `Tests/Chaos.Client.Tests`:

- `TuneScales`: row to MIDI note for every scale and both pitched layers, including Five-note across three octaves,
  and row names.
- `TuneDocument`:
  - a drag stops at the next note in the row and at the grid's end;
  - drum notes stay 1 long;
  - Copy bars 1-4 to 5-8, including a note that crosses the middle and notes already in bars 5-8;
  - Clear layer;
  - the 50-step undo limit, Redo cleared by a new edit, and undoing a scale change;
  - the 512-note limit;
  - the melody-note, melody-row and total counts.
- `TuneSynth`:
  - the buffer length for each speed (64 columns plus 1.5 seconds);
  - an empty tune renders silence;
  - the same tune renders the same samples twice;
  - a tune with every melody and bass row sounding at once never goes past the 16-bit range;
  - a single note renders to a short buffer.
- `CollegeTunes`: a tune is fetched once per id and fetched again after the 5-second time-out.

The full server suite stays at its known baseline (`OnItemDroppedOn` stackable, and `GiveAbility` if it still fails
on master).

**In-game checklist** for the user:

1. Compose a Music draft: notes in all three layers, held notes and a chord. Try every scale, speed and instrument.
   Use Copy bars, Clear layer, Undo, Redo, Loop and play from bar 5. Save, close and reopen.
2. While it plays, check the map music fades out and comes back after. Change the sound effects volume: the tune
   follows it.
3. Try to submit with too few melody notes: the message shows. Then submit; check that the marks drop.
4. As a judge, open it, play it and vote.
5. `/college closevoting <id>`, post the verdict, claim it. At the Music stand, press **Play** on its row, then
   **Read** and play it there. Start another row's tune: the first one stops.
6. Run a Music Activity class: set a prompt, hand in from two characters, show one to the class. It plays for
   everyone in the room.
7. Close the composer while a tune plays, and change map while one plays: both stop it.

## Shipping

Server + client 774. The client goes out in a launcher patch before the server restart. Unora ships only if the plan
finds a dialog text to change. Like parts 1-3, nothing is pushed until the user says so.

## Open follow-ups carried from parts 1-3

- The file-server `AislingRenamer` must update `CollegeState.json` before a rename. Part 4 adds nothing new to rename:
  tunes live in piece files, which hold no names.
