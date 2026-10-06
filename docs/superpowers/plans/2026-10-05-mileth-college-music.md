# Mileth College Part 4 (Music Composer) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Players compose Music entries and Music hand-ins on a three-layer note grid inside the client. The client makes the sound itself. Judges, the reader, the gallery's Music tab and Show to class all play tunes.

**Architecture:**
- **Server:** a Music piece holds one tune block: scale, speed, melody instrument and up to 512 four-byte notes (layer, row, start, length). Pure checks in `CollegeProtocol` and `PieceRules` validate it. The server never makes sound.
- **Messages:** the protocol gains a `Tune` block kind and a `TuneFetch` / `Tune` message pair, used by the gallery's Play buttons. `CLIENT_VERSION` 774.
- **Client model:** a pure `TuneDocument` does every edit; `TuneScales` maps rows to pitches.
- **Client sound:** a pure `TuneSynth` renders a whole tune into one 48 kHz stereo buffer with modelled instruments. `TunePlayer` plays it on a channel kept for tunes, fading the map music out and back.
- **Client windows:** `MusicComposerControl` (Draft and HandIn modes) draws the three stacked grids with `TuneGrid`, which the reader reuses read-only.

**Tech Stack:** C# 14 / .NET 10, TUnit + FluentAssertions + Moq (server and client tests), MonoGame + SDL2_mixer (client).

**Spec:** `Chaos.Client/docs/superpowers/specs/2026-10-05-mileth-college-music-design.md`. Also read the parent design `2026-10-04-mileth-college-design.md`, the part 2 spec `2026-10-05-mileth-college-entries-design.md` and the part 3 spec `2026-10-05-mileth-college-art-design.md` in the same folder.

## Global Constraints

- **Branches and folders.** Every path in a task is relative to the repo that task names:
  - server: `C:\Users\Michael\Documents\GitHub\worktrees\college-music-server` (branch `feat/college-music` from server `master` 79b4c6511);
  - client: `C:\Users\Michael\Documents\GitHub\worktrees\college-music-client` (branch `feat/college-music` from client `main`);
  - Unora: no changes (checked at plan time; no dialog text says "write" or "draw" where Music now applies), so no Unora worktree.

  Task 0 creates them.
- **Shared checkouts.** Other Claude sessions use the main checkouts.
  - Never run `git stash`, `git reset`, `git add -A` or `git add .` anywhere. Stage by explicit path.
  - Never delete files with `rm`; the Boost safety check refuses it. Leave deletions to the user.
- **Builds.**
  - A running `Chaos.exe` or game client locks `bin/`. If a build fails with a file lock, ask the user to stop it.
  - Never build the server and the client at the same time.
  - The client worktree builds `Chaos.Client/Chaos.Client.csproj`, not the `.slnx`, with `-p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\college-music-server`.
  - Client tasks compile only after server Task 1 (the tune enums and `CollegeBlockInfo` fields live in the server repo).
- **Commits:** one commit per repo at the end (Task 11). Implementers must NOT commit. Leave all changes in the working tree.
- **Tests:** `dotnet test` does not work. Run TUnit through `dotnet run`:
  - server: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/<Class>/*"`;
  - client: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\college-music-server -- --treenode-filter "/*/*/<Class>/*"`.
- **Known server baseline:** the full server suite fails only `OnItemDroppedOn` stackable, plus `GiveAbility` if it fails on master. Any other failure is a regression.
- **Code style:**
  - Match the surrounding code. Use `Lock` with `EnterScope()`, never `lock`.
  - Comments only for a non-obvious "why". No explanatory comments in tests.
  - Test methods use the `Snake_case_sentence` names the existing College tests use.
  - Use Serena's symbolic tools for code reads and edits where they fit (user CLAUDE.md).
- **Tune format (both sides agree through `CollegeProtocol`):**
  - enums `TuneScale` (Major, Minor, FiveNote, Dorian, Mixolydian, Desert), `TuneSpeed` (Slow 80, Walking 100, Steady 120, Lively 140, Quick 160 bpm), `TuneInstrument` (Lute, Harp, Flute, Bells), `TuneLayer` (Melody, Bass, Drums), all `: byte` from 0;
  - 64 columns, one column = half a beat; 15 rows for Melody and Bass, 3 for Drums;
  - a note is 4 bytes: layer, row, start, length; at most 512 notes; drum notes 1 long; no overlap within a layer and row;
  - pitch of a row = `base + 12 * (row / n) + steps[row % n]`, Melody base 60, Bass base 36;
  - composer's note 300 characters at most;
  - Submit needs a title and 16 melody notes on 3 distinct rows; a hand-in needs 8 notes in any layer.
- **Messages:** `CollegeBlockKind.Tune = 4`, `CollegeActionType.TuneFetch = 13`, `CollegeDisplayType.Tune = 9`. `CLIENT_VERSION` 773 -> 774. No new opcodes.
- **Sound:** 48,000 Hz, 16-bit, stereo. Same tune -> same samples on every PC (fixed seeds). Tunes use the sound-effects volume; map music fades out while a tune plays.
- **Text limits:** dialog option text 35 characters, dialog text 360, orange bar 45, no em dashes in Unora JSON.

**User decisions (already made):**
- Three layers (melody, bass, drums), held notes allowed; 64 columns.
- Sound made by the client from code with modelled instruments; no sound files.
- The melody picks Lute, Harp, Flute or Bells; bass and drums are fixed.
- Six scales: Major, Minor, Five-note, Dorian, Mixolydian, Desert. Rows follow the scale.
- Five named speeds: Slow, Walking, Steady, Lively, Quick.
- Composer layout B: all three layers stacked in one window.
- A Music entry is a title, one tune and a composer's note of up to 300 characters; minimums 16 melody notes on 3 rows to enter, 8 notes to hand in.
- Gallery Music tab: a Play button on each row. Show to class plays the tune for everyone at once. No tunes in writing pieces.
- Render the whole tune, then play it as one sound (approach 1).
- Tunes follow the sound-effects volume.
- Copy bars cuts a note that crosses the middle; Loop starts each pass at column 64; a gallery fetch with no reply in 5 seconds can be retried.

**Plan-time adjustments to the spec (Task 10 records them in the spec):**
- Unora needs no changes.
- Extra player texts: "Give your tune a title.", "Music pieces hold one tune and a note.", and the generic wrong-block text "That piece holds a part it can't."
- An Art piece holding a tune block is refused (`WrongBlock`).
- The server's save throttle lets each subject's draft through once per burst, so three College windows open at logout all save.
- The per-minute fetch limit moves into `CollegeFetchLimit`, shared by `DrawingFetch` and `TuneFetch`.
- A new tune starts at Major, Steady, Lute.
- The layer is picked with **Melody**, **Bass** and **Drums** toolbar buttons (or by clicking a cell), not by clicking a name beside the grid.
- Gallery columns on every tab move to 0 / 236 / 356 for the Music rows' **Play** button.
- Note previews use the tune channel without fading the map music, and are skipped while a tune plays or renders.
- Loop asks the composer for each next pass (`Func<TuneData?>`); a null ends playback after the current pass.

**Shared names (all tasks):**
- `TuneDocument` exposes `Version`, `IsDirty`, `MarkClean()` and `event Action? Changed` (raised whenever `Version` goes up, including `Load`, `Undo`, `Redo`).
- `ChaosGame` owns `TunePlayer` (`Game.TunePlayer`), updates it each frame after `SoundSystem.Update()`, and stops it on unload. `TunePlayer` talks to the mixer through `ITuneOutput`, which `SoundSystem` implements.
- `CollegeCodec.ReadTune` / `WriteTune` handle the fields after the kind byte; the `Tune` display is Id then `WriteTune`.

## File structure

**Server (`worktrees/college-music-server`)**

| File | Change |
|---|---|
| `Chaos.DarkAges/Definitions/Enums.cs` | `TuneScale`, `TuneSpeed`, `TuneInstrument`, `TuneLayer`; `CollegeBlockKind.Tune`, `CollegeActionType.TuneFetch`, `CollegeDisplayType.Tune` |
| `Chaos.DarkAges/Definitions/CollegeProtocol.cs` | Tune constants, `TuneBpm`, `TuneRows`, `TuneIsValid`, `TuneNoteCount`, `TuneMelodyNotes`, `TuneMelodyRows` |
| `Chaos.DarkAges/Definitions/CONSTANTS.cs` | `CLIENT_VERSION` 774 |
| `Chaos.Networking/Entities/Server/CollegeInfos.cs` | `CollegeBlockInfo.Scale`, `.Speed`, `.Instrument`, `.Notes` |
| `Chaos.Networking/Entities/Client/CollegeActionArgs.cs` | `Id` doc covers TuneFetch |
| `Chaos.Networking/Entities/Server/CollegeDisplayArgs.cs` | `Tune` |
| `Chaos.Networking/Converters/CollegeCodec.cs` | Tune block, `ReadTune`, `WriteTune` |
| `Chaos.Networking/Converters/Client/CollegeActionConverter.cs` | TuneFetch |
| `Chaos.Networking/Converters/Server/CollegeDisplayConverter.cs` | Tune |
| `Chaos/Services/College/CollegePiece.cs` | `PieceBlockType.Tune`, block scale/speed/instrument/notes, `PieceCheck.BadTune`, `KindOf` Music |
| `Chaos/Services/College/TuneRules.cs` (new) | Tune-block validity and note counts (pure) |
| `Chaos/Services/College/PieceRules.cs` | Normalize tunes, `CheckMusic`, Music entry and hand-in minimums, no tunes elsewhere |
| `Chaos/Services/College/CollegeText.cs` | Music texts, `PieceProblem(check, subject)`, "(music class)" |
| `Chaos/Services/College/CollegeOptions.cs` | Music open, `HasActivity` true for all |
| `Chaos/Services/College/CollegeService.Entries.cs` | `PieceProblem` callers, Music hand-in text, `GalleryTune` |
| `Chaos/Services/College/CollegeFetchLimit.cs` (new) | Per-name fetches per minute |
| `Chaos/Services/College/CollegePanel.cs` | Tune mapping, `ToBlockInfo`, `TuneFetch`/`SendTune`, fetch limits, logout drafts for three windows |
| `Chaos/Scripting/DialogScripts/Temuair/College/CollegeRegistrarScript.cs` | "opens soon" text no longer names Music |
| `Chaos/Scripting/DialogScripts/Temuair/College/CollegeLecternScript.cs` | "Compose a hand-in" |

**Client (`worktrees/college-music-client`)**

| File | Change |
|---|---|
| `Chaos.Client/ViewModel/College/TuneNote.cs` (new) | One note: layer, row, start, length |
| `Chaos.Client/ViewModel/College/TuneData.cs` (new) | An immutable tune, to and from `CollegeBlockInfo` |
| `Chaos.Client/ViewModel/College/TuneScales.cs` (new) | Row to MIDI note, row names, home rows, column length |
| `Chaos.Client/ViewModel/College/TuneDocument.cs` (new) | The editable tune: place, drag, remove, copy, clear, undo/redo |
| `Chaos.Client/Systems/College/TuneSynth.cs` (new) | Tune to 16-bit stereo samples (pure) |
| `Chaos.Client/Systems/College/TuneWav.cs` (new) | Samples to WAV bytes (pure) |
| `Chaos.Client/Systems/College/ITuneOutput.cs` (new) | What `TunePlayer` needs from the mixer |
| `Chaos.Client/Systems/College/TunePlayer.cs` (new) | Renders off the game thread, plays, playhead, loop, owner, previews |
| `Chaos.Client/Systems/MusicDuck.cs` (new) | Map-music fade level (pure) |
| `Chaos.Client/Systems/SoundSystem.cs` | Reserved tune channel, `ITuneOutput`, music fade while a tune plays |
| `Chaos.Client/ChaosGame.cs` | Owns `TunePlayer`, updates it each frame, stops it on unload |
| `Tests/Chaos.Client.Tests/College/TuneScalesTests.cs` (new) | |
| `Tests/Chaos.Client.Tests/College/TuneDataTests.cs` (new) | |
| `Tests/Chaos.Client.Tests/College/TuneDocumentTests.cs` (new) | |
| `Tests/Chaos.Client.Tests/College/TuneSynthTests.cs` (new) | |
| `Tests/Chaos.Client.Tests/College/TuneWavTests.cs` (new) | |
| `Tests/Chaos.Client.Tests/College/TunePlayerTests.cs` (new) | |
| `Tests/Chaos.Client.Tests/MusicDuckTests.cs` (new) | |
| `Chaos.Client/Controls/World/Popups/College/TuneGridLayout.cs` (new) | Pure grid maths: sizes, row tops, cell and bar hit tests (Task 8) |
| `Chaos.Client/ViewModel/College/TuneLabels.cs` (new) | Display names of scales, speeds and instruments, the reader's summary line, enum cycling (Task 8) |
| `Chaos.Client/Controls/World/Popups/College/TuneGrid.cs` (new) | The stacked Melody / Bass / Drums grids: drawing, the beat bar, playhead, editing mouse input (Task 8) |
| `Chaos.Client/Controls/World/Popups/College/MusicComposerControl.cs` (new) | The 560 x 464 composer window, Draft and HandIn modes (Task 8) |
| `Chaos.Client/Controls/World/Popups/College/TuneBlockView.cs` (new) | The reader's tune block: summary, Play/Stop, read-only grid (Task 9) |
| `Chaos.Client/Controls/World/Popups/College/TuneRowButton.cs` (new) | The gallery Music row's small Play button (Task 9) |
| `Chaos.Client/Systems/College/CollegeTunes.cs` (new) | Gallery tunes by entry id, 5 s fetch time-out (Task 9) |
| `Chaos.Client/Systems/College/GalleryTunePlay.cs` (new) | Pure: what a row's Play press does and which row waits for its tune (Task 9) |
| `Chaos.Client/Controls/World/Popups/College/PieceView.cs` | Tune block; takes the `TunePlayer` (Task 9) |
| `Chaos.Client/Controls/World/Popups/College/CollegeReaderControl.cs` | Passes the player on; Shown auto-plays; Hide stops (Task 9) |
| `Chaos.Client/Controls/World/Popups/College/CollegeListWindow.cs` | `RowsTop` for subclasses (Task 9) |
| `Chaos.Client/Controls/World/Popups/College/CollegeGalleryControl.cs` | Music tab Play buttons; narrower columns (Task 9) |
| `Chaos.Client/Screens/WorldScreen.College.cs` | Composer, tunes cache, Music routing, `Tune` replies, logout save (Task 9) |
| `Chaos.Client/Screens/WorldScreen.cs` | Logout: clear tunes, stop the player (Task 9) |
| `Chaos.Client/Screens/WorldScreen.Map.cs` | A new map stops the tune (Task 9) |
| `CLAUDE.md` | College control list (Task 9) |
| `Tests/Chaos.Client.Tests/College/TuneGridLayoutTests.cs`, `TuneLabelsTests.cs` (new) | Task 8 |
| `Tests/Chaos.Client.Tests/College/CollegeTunesTests.cs`, `GalleryTunePlayTests.cs` (new) | Task 9 |

---

### Task 0: Create the worktrees

**Goal:** Two worktrees on branch `feat/college-music`, building on the local mains.

**Files:**
- Create: `C:\Users\Michael\Documents\GitHub\worktrees\college-music-server`, `...\college-music-client`

**Acceptance Criteria:**
- [ ] `git -C <worktree> branch --show-current` prints `feat/college-music` in both
- [ ] The server worktree's HEAD is 79b4c6511; the client's is client `main`'s tip
- [ ] The server worktree builds

**Verify:** `dotnet build C:\Users\Michael\Documents\GitHub\worktrees\college-music-server\Chaos.slnx` -> Build succeeded

**Steps:**

- [ ] **Step 1: Check the mains.**

```bash
git -C C:/Users/Michael/Documents/GitHub/Chaos.Client/Chaos-Server log --oneline -1 master
git -C C:/Users/Michael/Documents/GitHub/Chaos.Client log --oneline -1 main
```

Expected: `79b4c6511 ...` and client main's tip (e771b637 or later; docs-only commits on top are fine). If server master moved past 79b4c6511, stop and ask the user which commit to build on.

- [ ] **Step 2: Create the worktrees.**

```bash
git -C C:/Users/Michael/Documents/GitHub/Chaos.Client/Chaos-Server worktree add C:/Users/Michael/Documents/GitHub/worktrees/college-music-server -b feat/college-music master
git -C C:/Users/Michael/Documents/GitHub/Chaos.Client worktree add C:/Users/Michael/Documents/GitHub/worktrees/college-music-client -b feat/college-music main
```

- [ ] **Step 3: Build the server worktree.** Run `dotnet build Chaos.slnx` in the server worktree. Expected: Build succeeded.

```json:metadata
{"files": [], "verifyCommand": "dotnet build C:\\Users\\Michael\\Documents\\GitHub\\worktrees\\college-music-server\\Chaos.slnx", "acceptanceCriteria": ["two worktrees on feat/college-music", "server at 79b4c6511, client at main's tip", "server builds"], "modelTier": "mechanical"}
```

---

### Task 1: Protocol: tune block, TuneFetch, Tune, CLIENT_VERSION 774 (server repo)

**Goal:** Both message converters carry tune blocks and the new sub-types. The shared tune helpers in `CollegeProtocol` check a tune's notes. `CLIENT_VERSION` is 774.

**Files:**
- Modify: `Chaos.DarkAges/Definitions/Enums.cs` (the `#region Mileth College` enums)
- Modify: `Chaos.DarkAges/Definitions/CollegeProtocol.cs`
- Modify: `Chaos.DarkAges/Definitions/CONSTANTS.cs:23`
- Modify: `Chaos.Networking/Entities/Server/CollegeInfos.cs`
- Modify: `Chaos.Networking/Entities/Client/CollegeActionArgs.cs`
- Modify: `Chaos.Networking/Entities/Server/CollegeDisplayArgs.cs`
- Modify: `Chaos.Networking/Converters/CollegeCodec.cs`
- Modify: `Chaos.Networking/Converters/Client/CollegeActionConverter.cs`
- Modify: `Chaos.Networking/Converters/Server/CollegeDisplayConverter.cs`
- Test: `Tests/Chaos.Tests/Networking/CollegePacketConverterTests.cs`, `Tests/Chaos.Tests/Networking/GuildEmblemPacketConverterTests.cs`, new `Tests/Chaos.Tests/College/TuneNotesTests.cs`

**Acceptance Criteria:**
- [ ] Every action and display sub-type round-trips, including a Music piece with a tune block, `TuneFetch` and `Tune`
- [ ] A tune with no notes round-trips; a tune with more than 512 notes (or a ragged byte count) is written as 512 whole notes
- [ ] Reading a tune whose count is over 512 throws `ArgumentOutOfRangeException`
- [ ] `CollegeProtocol.TuneIsValid` accepts a note covering all 64 columns and 512 notes. It refuses 513 notes, unknown enums, rows and lengths outside the grid, drum notes longer than 1, and overlaps in one row. It accepts chords.
- [ ] `TuneBpm` gives 80, 100, 120, 140 and 160; `TuneMelodyNotes`, `TuneMelodyRows` and `TuneNoteCount` count correctly
- [ ] `CLIENT_VERSION` is 774

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/CollegePacketConverterTests/*"` -> all pass; same for `GuildEmblemPacketConverterTests` and `TuneNotesTests`

**Steps:**

- [ ] **Step 1: Write the failing tests.** In `CollegePacketConverterTests.cs`, add after `ArtPiece()`:

```csharp
    private static CollegePieceInfo MusicPiece()
        => new()
        {
            Subject = CollegeSubjectCode.Music,
            Title = "Song of the Mileth Well",
            Blocks =
            [
                new CollegeBlockInfo
                {
                    Kind = CollegeBlockKind.Tune,
                    Scale = TuneScale.Dorian,
                    Speed = TuneSpeed.Lively,
                    Instrument = TuneInstrument.Harp,
                    Notes = [0, 4, 0, 2, 1, 7, 0, 6, 2, 0, 0, 1]
                },
                new CollegeBlockInfo { Kind = CollegeBlockKind.Text, Text = "For the well." }
            ]
        };
```

In `Actions()`, add:

```csharp
        yield return new CollegeActionArgs { Type = CollegeActionType.Submit, Subject = CollegeSubjectCode.Music, Piece = MusicPiece() };
        yield return new CollegeActionArgs { Type = CollegeActionType.TuneFetch, Id = 41 };
```

In `Displays()`, add:

```csharp
        yield return new CollegeDisplayArgs { Type = CollegeDisplayType.Tune, Id = 41, Tune = MusicPiece().Blocks[0] };
```

Add these tests at the end of the class:

```csharp
    [Test]
    public void A_tune_with_no_notes_round_trips()
    {
        var original = new CollegeActionArgs
        {
            Type = CollegeActionType.HandIn,
            Piece = new CollegePieceInfo
            {
                Subject = CollegeSubjectCode.Music,
                Blocks = [new CollegeBlockInfo { Kind = CollegeBlockKind.Tune, Scale = TuneScale.Desert, Speed = TuneSpeed.Quick, Instrument = TuneInstrument.Bells }]
            }
        };

        RoundTrip(new CollegeActionConverter(), original).Should().BeEquivalentTo(original);
    }

    [Test]
    public void A_long_or_ragged_tune_is_written_as_512_whole_notes()
    {
        var notes = new byte[(CollegeProtocol.MAX_TUNE_NOTES + 1) * CollegeProtocol.TUNE_NOTE_BYTES + 3];
        notes[^4] = 9;

        var original = new CollegeActionArgs
        {
            Type = CollegeActionType.HandIn,
            Piece = new CollegePieceInfo
            {
                Subject = CollegeSubjectCode.Music,
                Blocks = [new CollegeBlockInfo { Kind = CollegeBlockKind.Tune, Notes = notes }]
            }
        };

        var block = RoundTrip(new CollegeActionConverter(), original).Piece!.Blocks.Single();

        block.Notes.Should().HaveCount(CollegeProtocol.MAX_TUNE_NOTES * CollegeProtocol.TUNE_NOTE_BYTES);
        block.Notes.Should().OnlyContain(b => b == 0);
    }

    [Test]
    public void A_tune_counting_more_than_512_notes_is_refused()
    {
        var act = () =>
        {
            var local = new SpanReader(Enc, [(byte)CollegeDisplayType.Tune, 0, 0, 0, 41, 0, 0, 0, 0x02, 0x01, 0, 0, 0, 0]);
            new CollegeDisplayConverter().Deserialize(ref local);
        };

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
```

(The last test's bytes are the type, then the Id 41 as an Int32, then scale, speed and instrument, then the count 0x0201 = 513, then a few spare bytes. `SpanReader` reads big-endian by default.)

In `GuildEmblemPacketConverterTests.cs`, change the pin:

```csharp
    [Test]
    public void Client_version_is_774()
        => CONSTANTS.CLIENT_VERSION.Should().Be(774);
```

Create `Tests/Chaos.Tests/College/TuneNotesTests.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Tests.College;

public sealed class TuneNotesTests
{
    private static byte[] Notes(params (TuneLayer Layer, int Row, int Start, int Length)[] notes)
        => notes.SelectMany(n => new[] { (byte)n.Layer, (byte)n.Row, (byte)n.Start, (byte)n.Length }).ToArray();

    private static bool Valid(byte[] notes) => CollegeProtocol.TuneIsValid(TuneScale.Major, TuneSpeed.Steady, TuneInstrument.Lute, notes);

    [Test]
    public void Speeds_have_their_beats_a_minute()
    {
        Enum.GetValues<TuneSpeed>().Select(CollegeProtocol.TuneBpm).Should().Equal(80, 100, 120, 140, 160);
        CollegeProtocol.TuneRows(TuneLayer.Melody).Should().Be(15);
        CollegeProtocol.TuneRows(TuneLayer.Bass).Should().Be(15);
        CollegeProtocol.TuneRows(TuneLayer.Drums).Should().Be(3);
    }

    [Test]
    public void Notes_must_fit_the_grid()
    {
        Valid([]).Should().BeTrue();
        Valid(Notes((TuneLayer.Melody, 14, 0, 64))).Should().BeTrue();
        Valid(Notes((TuneLayer.Bass, 14, 60, 4))).Should().BeTrue();
        Valid(Notes((TuneLayer.Drums, 2, 63, 1))).Should().BeTrue();

        Valid(Notes((TuneLayer.Melody, 15, 0, 1))).Should().BeFalse();
        Valid(Notes((TuneLayer.Drums, 3, 0, 1))).Should().BeFalse();
        Valid(Notes((TuneLayer.Drums, 0, 0, 2))).Should().BeFalse();
        Valid(Notes((TuneLayer.Melody, 0, 0, 0))).Should().BeFalse();
        Valid(Notes((TuneLayer.Melody, 0, 60, 5))).Should().BeFalse();
        Valid(Notes(((TuneLayer)3, 0, 0, 1))).Should().BeFalse();
        Valid([0, 0, 0, 1, 0]).Should().BeFalse();
    }

    [Test]
    public void Unknown_scale_speed_or_instrument_is_refused()
    {
        CollegeProtocol.TuneIsValid((TuneScale)6, TuneSpeed.Steady, TuneInstrument.Lute, []).Should().BeFalse();
        CollegeProtocol.TuneIsValid(TuneScale.Major, (TuneSpeed)5, TuneInstrument.Lute, []).Should().BeFalse();
        CollegeProtocol.TuneIsValid(TuneScale.Major, TuneSpeed.Steady, (TuneInstrument)4, []).Should().BeFalse();
    }

    [Test]
    public void Notes_in_one_row_may_not_overlap_but_chords_may()
    {
        Valid(Notes((TuneLayer.Melody, 0, 0, 4), (TuneLayer.Melody, 0, 3, 1))).Should().BeFalse();
        Valid(Notes((TuneLayer.Melody, 0, 0, 4), (TuneLayer.Melody, 0, 4, 1))).Should().BeTrue();
        Valid(Notes((TuneLayer.Melody, 0, 0, 4), (TuneLayer.Melody, 1, 0, 4))).Should().BeTrue();
        Valid(Notes((TuneLayer.Melody, 0, 0, 4), (TuneLayer.Bass, 0, 0, 4))).Should().BeTrue();
    }

    [Test]
    public void A_tune_holds_512_notes()
    {
        var many = Enumerable.Range(0, CollegeProtocol.MAX_TUNE_NOTES + 1)
                             .Select(i => (TuneLayer.Melody, i / 64, i % 64, 1))
                             .ToArray();

        Valid(Notes(many[..CollegeProtocol.MAX_TUNE_NOTES])).Should().BeTrue();
        Valid(Notes(many)).Should().BeFalse();
    }

    [Test]
    public void Melody_notes_and_rows_are_counted()
    {
        var notes = Notes(
            (TuneLayer.Melody, 2, 0, 1),
            (TuneLayer.Melody, 2, 1, 1),
            (TuneLayer.Melody, 5, 2, 3),
            (TuneLayer.Bass, 7, 0, 8),
            (TuneLayer.Drums, 0, 0, 1));

        CollegeProtocol.TuneNoteCount(notes).Should().Be(5);
        CollegeProtocol.TuneMelodyNotes(notes).Should().Be(3);
        CollegeProtocol.TuneMelodyRows(notes).Should().Be(2);
    }
}
```

- [ ] **Step 2: Run to see them fail.** Run the `CollegePacketConverterTests` filter. Expected: build errors for `TuneScale`, `CollegeBlockKind.Tune`, `TuneFetch`, `Notes` and the others.

- [ ] **Step 3: Enums.** In `Enums.cs`, in the College region:
  - add `TuneFetch = 13` after `DrawingFetch = 12` in `CollegeActionType`;
  - add `Tune = 9` after `Drawing = 8` in `CollegeDisplayType`;
  - add `Tune = 4` after `Drawing = 3` in `CollegeBlockKind`;
  - add these before `#endregion`:

```csharp

/// <summary>A tune's scale. The Melody and Bass rows are steps of this scale, so changing it moves every note.</summary>
public enum TuneScale : byte
{
    Major = 0,
    Minor = 1,
    FiveNote = 2,
    Dorian = 3,
    Mixolydian = 4,
    Desert = 5
}

/// <summary>A tune's speed; <c>CollegeProtocol.TuneBpm</c> gives its beats a minute.</summary>
public enum TuneSpeed : byte
{
    Slow = 0,
    Walking = 1,
    Steady = 2,
    Lively = 3,
    Quick = 4
}

/// <summary>The instrument that plays a tune's melody. Bass and drums are fixed.</summary>
public enum TuneInstrument : byte
{
    Lute = 0,
    Harp = 1,
    Flute = 2,
    Bells = 3
}

public enum TuneLayer : byte
{
    Melody = 0,
    Bass = 1,
    Drums = 2
}
```

  In `CONSTANTS.cs`, change `CLIENT_VERSION = 773` to `CLIENT_VERSION = 774`.

- [ ] **Step 4: Protocol helpers.** In `CollegeProtocol`, add after `MIN_HAND_IN_DRAWN_PIXELS`:

```csharp

    public const int TUNE_STEPS = 64;
    public const int TUNE_PITCH_ROWS = 15;
    public const int TUNE_DRUM_ROWS = 3;

    /// <summary>Each note is four bytes: layer, row, start column, length in columns.</summary>
    public const int TUNE_NOTE_BYTES = 4;

    public const int MAX_TUNE_NOTES = 512;
    public const int MAX_MUSIC_NOTE_CHARS = 300;
    public const int MIN_ENTRY_MELODY_NOTES = 16;
    public const int MIN_ENTRY_MELODY_ROWS = 3;
    public const int MIN_HAND_IN_NOTES = 8;
```

and after `DrawnPixels`:

```csharp

    public static int TuneBpm(TuneSpeed speed)
        => speed switch
        {
            TuneSpeed.Slow    => 80,
            TuneSpeed.Walking => 100,
            TuneSpeed.Steady  => 120,
            TuneSpeed.Lively  => 140,
            TuneSpeed.Quick   => 160,
            _                 => 120
        };

    public static int TuneRows(TuneLayer layer) => layer == TuneLayer.Drums ? TUNE_DRUM_ROWS : TUNE_PITCH_ROWS;

    /// <summary>
    ///     Known scale, speed and instrument; whole notes, at most <see cref="MAX_TUNE_NOTES" />; every note inside the grid,
    ///     drum notes one column long; no two notes overlapping in the same layer and row (notes on different rows may sound together).
    /// </summary>
    public static bool TuneIsValid(TuneScale scale, TuneSpeed speed, TuneInstrument instrument, ReadOnlySpan<byte> notes)
    {
        if (!Enum.IsDefined(scale) || !Enum.IsDefined(speed) || !Enum.IsDefined(instrument))
            return false;

        if ((notes.Length % TUNE_NOTE_BYTES != 0) || (notes.Length / TUNE_NOTE_BYTES > MAX_TUNE_NOTES))
            return false;

        //one bit per column, for every row of every layer
        Span<ulong> used = stackalloc ulong[3 * TUNE_PITCH_ROWS];
        used.Clear();

        for (var i = 0; i < notes.Length; i += TUNE_NOTE_BYTES)
        {
            var layer = (TuneLayer)notes[i];
            int row = notes[i + 1], start = notes[i + 2], length = notes[i + 3];

            if (!Enum.IsDefined(layer) || (row >= TuneRows(layer)) || (length < 1) || (start + length > TUNE_STEPS))
                return false;

            if ((layer == TuneLayer.Drums) && (length != 1))
                return false;

            //a shift by 64 is a shift by 0 in C#, so a full-width note needs its own mask
            var mask = (length == TUNE_STEPS ? ulong.MaxValue : (1UL << length) - 1) << start;
            ref var cells = ref used[((int)layer * TUNE_PITCH_ROWS) + row];

            if ((cells & mask) != 0)
                return false;

            cells |= mask;
        }

        return true;
    }

    public static int TuneNoteCount(ReadOnlySpan<byte> notes) => notes.Length / TUNE_NOTE_BYTES;

    public static int TuneMelodyNotes(ReadOnlySpan<byte> notes)
    {
        var count = 0;

        for (var i = 0; i + TUNE_NOTE_BYTES <= notes.Length; i += TUNE_NOTE_BYTES)
            if (notes[i] == (byte)TuneLayer.Melody)
                count++;

        return count;
    }

    public static int TuneMelodyRows(ReadOnlySpan<byte> notes)
    {
        var rows = 0;

        for (var i = 0; i + TUNE_NOTE_BYTES <= notes.Length; i += TUNE_NOTE_BYTES)
            if ((notes[i] == (byte)TuneLayer.Melody) && (notes[i + 1] < 32))
                rows |= 1 << notes[i + 1];

        return int.PopCount(rows);
    }
```

- [ ] **Step 5: Fields.** Replace `CollegeBlockInfo` in `CollegeInfos.cs` with:

```csharp
/// <summary>
///     One block of a piece: text (may hold line breaks), a one-line heading, a picture named by its hash, a drawing
///     (palette R, G, B per swatch, then one swatch number per pixel), or a tune (scale, speed, melody instrument, then
///     four bytes per note: layer, row, start, length).
/// </summary>
public sealed record CollegeBlockInfo
{
    public CollegeBlockKind Kind { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public byte[] Palette { get; set; } = [];
    public byte[] Pixels { get; set; } = [];
    public TuneScale Scale { get; set; }
    public TuneSpeed Speed { get; set; }
    public TuneInstrument Instrument { get; set; }
    public byte[] Notes { get; set; } = [];
}
```

In `CollegeActionArgs.cs`, change the `Id` summary to `/// <summary>OpenPiece, Vote, Verdict, RemoveEntry, ShowToClass, DrawingFetch, TuneFetch.</summary>`.

In `CollegeDisplayArgs.cs`, change the `Id` summary to `/// <summary>Piece: the entry or hand-in id. Drawing, Tune: the entry id.</summary>`. Then add after the `Pixels` property:

```csharp

    //Tune
    public CollegeBlockInfo Tune { get; set; } = new() { Kind = CollegeBlockKind.Tune };
```

- [ ] **Step 6: Codec.** In `CollegeCodec.ReadPiece`, add an arm to the `kind switch`, before the `_` arm:

```csharp
                    CollegeBlockKind.Tune => ReadTune(ref reader),
```

In `WritePiece`, add before `default:`:

```csharp
                case CollegeBlockKind.Tune:
                    WriteTune(ref writer, block);

                    break;
```

Add after `WriteFixed`:

```csharp
    /// <summary>A tune block after its kind byte: scale, speed, instrument, a two-byte note count, then four bytes per note.</summary>
    public static CollegeBlockInfo ReadTune(ref SpanReader reader)
    {
        var block = new CollegeBlockInfo
        {
            Kind = CollegeBlockKind.Tune,
            Scale = (TuneScale)reader.ReadByte(),
            Speed = (TuneSpeed)reader.ReadByte(),
            Instrument = (TuneInstrument)reader.ReadByte()
        };

        var count = reader.ReadUInt16();

        if (count > CollegeProtocol.MAX_TUNE_NOTES)
            throw new ArgumentOutOfRangeException(nameof(reader), count, "Too many College tune notes");

        block.Notes = reader.ReadBytes(count * CollegeProtocol.TUNE_NOTE_BYTES);

        return block;
    }

    /// <summary>Writes whole notes only, and at most <see cref="CollegeProtocol.MAX_TUNE_NOTES" />.</summary>
    public static void WriteTune(ref SpanWriter writer, CollegeBlockInfo block)
    {
        writer.WriteByte((byte)block.Scale);
        writer.WriteByte((byte)block.Speed);
        writer.WriteByte((byte)block.Instrument);

        var count = Math.Min(block.Notes.Length / CollegeProtocol.TUNE_NOTE_BYTES, CollegeProtocol.MAX_TUNE_NOTES);
        writer.WriteUInt16((ushort)count);
        writer.WriteBytes(block.Notes.AsSpan(0, count * CollegeProtocol.TUNE_NOTE_BYTES));
    }
```

- [ ] **Step 7: Converters.** In `CollegeActionConverter`, add `case CollegeActionType.TuneFetch:` under `case CollegeActionType.DrawingFetch:` in both `Deserialize` and `Serialize`, so it reads and writes only `Id`.

In `CollegeDisplayConverter.Deserialize`, add after the `Drawing` case:

```csharp
            case CollegeDisplayType.Tune:
                args.Id = reader.ReadInt32();
                args.Tune = CollegeCodec.ReadTune(ref reader);

                break;
```

and in `Serialize`:

```csharp
            case CollegeDisplayType.Tune:
                writer.WriteInt32(args.Id);
                CollegeCodec.WriteTune(ref writer, args.Tune);

                break;
```

- [ ] **Step 8: Run the tests.** Run the `CollegePacketConverterTests`, `GuildEmblemPacketConverterTests` and `TuneNotesTests` filters. Expected: all pass.

```json:metadata
{"files": ["Chaos.DarkAges/Definitions/Enums.cs", "Chaos.DarkAges/Definitions/CollegeProtocol.cs", "Chaos.DarkAges/Definitions/CONSTANTS.cs", "Chaos.Networking/Entities/Server/CollegeInfos.cs", "Chaos.Networking/Entities/Client/CollegeActionArgs.cs", "Chaos.Networking/Entities/Server/CollegeDisplayArgs.cs", "Chaos.Networking/Converters/CollegeCodec.cs", "Chaos.Networking/Converters/Client/CollegeActionConverter.cs", "Chaos.Networking/Converters/Server/CollegeDisplayConverter.cs", "Tests/Chaos.Tests/Networking/CollegePacketConverterTests.cs", "Tests/Chaos.Tests/Networking/GuildEmblemPacketConverterTests.cs", "Tests/Chaos.Tests/College/TuneNotesTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/CollegePacketConverterTests/*\"", "acceptanceCriteria": ["every sub-type round-trips incl. tune block, TuneFetch, Tune", "empty tune round-trips; long/ragged tune written as 512 whole notes", "count over 512 refused on read", "TuneIsValid grid, overlap, 512 limit, enums", "TuneBpm and note counts", "CLIENT_VERSION 774"], "modelTier": "mechanical"}
```

---

### Task 2: Server Music pieces: model, rules, mapping, storage (server repo)

**Goal:** The server stores and checks Music pieces: one valid tune, an optional note of up to 300 characters, and the entry and hand-in minimums. Tunes map to and from messages without loss, and other subjects refuse tune blocks.

**Files:**
- Modify: `Chaos/Services/College/CollegePiece.cs`
- Create: `Chaos/Services/College/TuneRules.cs`
- Modify: `Chaos/Services/College/PieceRules.cs`
- Modify: `Chaos/Services/College/CollegeText.cs` (Music texts, `PieceProblem`)
- Modify: `Chaos/Services/College/CollegeService.Entries.cs` (the three `PieceProblem` callers and the hand-in refusal text)
- Modify: `Chaos/Services/College/CollegePanel.cs` (`ToPiece`, `ToInfo`, new `ToBlockInfo`)
- Test: `Tests/Chaos.Tests/College/PieceRulesTests.cs`, `CollegePanelMappingTests.cs`, `CollegePieceStoreTests.cs`, `CollegeEntryServiceTests.cs` (one call site)

**Acceptance Criteria:**
- [ ] A Music piece needs exactly one tune block whose scale, speed, instrument and notes pass `CollegeProtocol.TuneIsValid`. Otherwise it gets `BadTune`.
- [ ] Headings, pictures, drawings or a second text block in Music give `WrongBlock`. So does a tune in a writing or Art piece.
- [ ] A Music note over 300 characters gives `TooMuchText`
- [ ] Music Submit needs a title and at least 16 melody notes on at least 3 rows. A Music hand-in needs at least 8 notes in any layer. Below that is `TooShort`.
- [ ] `PieceProblem(check, CollegeSubject.Music)` gives the Music texts. `TooShort` gives `Entries need 16 melody notes on 3 rows.` and `BadTune` gives `That tune could not be read.`
- [ ] `ToPiece` then `ToInfo` round-trips a tune exactly. A tune piece file round-trips with the scale written by name, and a writing piece file has no `Scale` or `Notes` keys.

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/PieceRulesTests/*"` -> all pass; same for `CollegePanelMappingTests`, `CollegePieceStoreTests`

**Steps:**

- [ ] **Step 1: Write the failing tests.** Add to `PieceRulesTests` (the file already has `using Chaos.DarkAges.Definitions;` and the Art helpers `Drawing(...)` and `Art(...)`):

```csharp
    private static byte[] Notes(params (TuneLayer Layer, int Row, int Start, int Length)[] notes)
        => notes.SelectMany(n => new[] { (byte)n.Layer, (byte)n.Row, (byte)n.Start, (byte)n.Length }).ToArray();

    private static byte[] Melody(int count, int rows = 3)
        => Notes(Enumerable.Range(0, count).Select(i => (TuneLayer.Melody, i % rows, i, 1)).ToArray());

    private static PieceBlock Tune(byte[] notes, TuneScale scale = TuneScale.Major)
        => new() { Type = PieceBlockType.Tune, Scale = scale, Speed = TuneSpeed.Steady, Instrument = TuneInstrument.Lute, Notes = notes };

    private static CollegePiece MusicPiece(string title, params PieceBlock[] blocks)
        => new() { Id = CollegePiece.NewId(), Subject = CollegeSubject.Music, Title = title, Blocks = blocks.ToList() };

    [Test]
    public void A_music_piece_needs_one_valid_tune()
    {
        PieceRules.Check(MusicPiece("t"), Exists).Should().Be(PieceCheck.BadTune);
        PieceRules.Check(MusicPiece("t", Tune(Melody(3)), Tune(Melody(3))), Exists).Should().Be(PieceCheck.BadTune);
        PieceRules.Check(MusicPiece("t", Tune(Melody(3), (TuneScale)9)), Exists).Should().Be(PieceCheck.BadTune);
        PieceRules.Check(MusicPiece("t", Tune(Notes((TuneLayer.Melody, 15, 0, 1)))), Exists).Should().Be(PieceCheck.BadTune);
        PieceRules.Check(MusicPiece("t", Tune(Notes((TuneLayer.Melody, 0, 0, 4), (TuneLayer.Melody, 0, 2, 1)))), Exists).Should().Be(PieceCheck.BadTune);
        PieceRules.Check(MusicPiece("t", new PieceBlock { Type = PieceBlockType.Tune, Notes = Melody(3) }), Exists).Should().Be(PieceCheck.BadTune);
        PieceRules.Check(MusicPiece("t", Tune(Melody(3))), Exists).Should().Be(PieceCheck.Ok);
        PieceRules.Check(MusicPiece("t", Tune([])), Exists).Should().Be(PieceCheck.Ok);
    }

    [Test]
    public void A_music_note_holds_three_hundred_characters()
    {
        PieceRules.Check(MusicPiece("t", Tune(Melody(3)), Text(new string('n', 300))), Exists).Should().Be(PieceCheck.Ok);
        PieceRules.Check(MusicPiece("t", Tune(Melody(3)), Text(new string('n', 301))), Exists).Should().Be(PieceCheck.TooMuchText);
    }

    [Test]
    public void Music_holds_only_a_tune_and_a_note_and_no_other_subject_holds_a_tune()
    {
        PieceRules.Check(MusicPiece("t", Tune(Melody(3)), Heading("h")), Exists).Should().Be(PieceCheck.WrongBlock);
        PieceRules.Check(MusicPiece("t", Tune(Melody(3)), Picture(HashA)), Exists).Should().Be(PieceCheck.WrongBlock);
        PieceRules.Check(MusicPiece("t", Tune(Melody(3)), Drawing()), Exists).Should().Be(PieceCheck.WrongBlock);
        PieceRules.Check(MusicPiece("t", Tune(Melody(3)), Text("a"), Text("b")), Exists).Should().Be(PieceCheck.WrongBlock);
        PieceRules.Check(Piece("t", Text("x"), Tune(Melody(3))), Exists).Should().Be(PieceCheck.WrongBlock);
        PieceRules.Check(Art("t", Drawing(), Tune(Melody(3))), Exists).Should().Be(PieceCheck.WrongBlock);
    }

    [Test]
    public void A_music_entry_needs_a_title_and_16_melody_notes_on_3_rows()
    {
        PieceRules.CheckEntry(MusicPiece("", Tune(Melody(16))), Exists).Should().Be(PieceCheck.NoTitle);
        PieceRules.CheckEntry(MusicPiece("t", Tune(Melody(15))), Exists).Should().Be(PieceCheck.TooShort);
        PieceRules.CheckEntry(MusicPiece("t", Tune(Melody(16, rows: 2))), Exists).Should().Be(PieceCheck.TooShort);
        PieceRules.CheckEntry(MusicPiece("t", Tune(Melody(16))), Exists).Should().Be(PieceCheck.Ok);

        var bassAndDrums = Melody(15).Concat(Notes((TuneLayer.Bass, 0, 0, 1), (TuneLayer.Drums, 0, 0, 1))).ToArray();
        PieceRules.CheckEntry(MusicPiece("t", Tune(bassAndDrums)), Exists).Should().Be(PieceCheck.TooShort);
    }

    [Test]
    public void A_music_hand_in_needs_8_notes_in_any_layer()
    {
        var drums = Enumerable.Range(0, 8).Select(i => (TuneLayer.Drums, 0, i, 1)).ToArray();

        PieceRules.CheckHandIn(MusicPiece("", Tune(Notes(drums[..7]))), Exists).Should().Be(PieceCheck.TooShort);
        PieceRules.CheckHandIn(MusicPiece("", Tune(Notes(drums))), Exists).Should().Be(PieceCheck.Ok);
        PieceRules.CheckHandIn(MusicPiece("", Tune(Melody(4).Concat(Notes(drums[..4])).ToArray())), Exists).Should().Be(PieceCheck.Ok);
    }

    [Test]
    public void Normalize_keeps_a_tune()
    {
        var tune = Tune(Melody(5), TuneScale.Desert);

        var clean = PieceRules.Normalize(MusicPiece("t", tune));

        var kept = clean.Blocks.Single();
        kept.Type.Should().Be(PieceBlockType.Tune);
        kept.Scale.Should().Be(TuneScale.Desert);
        kept.Speed.Should().Be(TuneSpeed.Steady);
        kept.Instrument.Should().Be(TuneInstrument.Lute);
        kept.Notes.Should().Equal(tune.Notes).And.NotBeSameAs(tune.Notes);
    }

    [Test]
    public void Music_problems_have_music_texts()
    {
        CollegeText.PieceProblem(PieceCheck.TooShort, CollegeSubject.Music).Should().Be("Entries need 16 melody notes on 3 rows.");
        CollegeText.PieceProblem(PieceCheck.BadTune, CollegeSubject.Music).Should().Be("That tune could not be read.");
        CollegeText.PieceProblem(PieceCheck.NoTitle, CollegeSubject.Music).Should().Be("Give your tune a title.");
        CollegeText.PieceProblem(PieceCheck.TooShort, CollegeSubject.History).Should().Be(CollegeText.ENTRY_TOO_SHORT);

        foreach (var subject in Enum.GetValues<CollegeSubject>())
            foreach (var check in Enum.GetValues<PieceCheck>())
                CollegeText.PieceProblem(check, subject).Length.Should().BeLessThanOrEqualTo(45);
    }
```

Add to `CollegePanelMappingTests`:

```csharp
    [Test]
    public void Tunes_map_both_ways()
    {
        var info = new CollegePieceInfo
        {
            Subject = CollegeSubjectCode.Music,
            Title = "Song",
            Blocks =
            [
                new CollegeBlockInfo
                {
                    Kind = CollegeBlockKind.Tune,
                    Scale = TuneScale.Mixolydian,
                    Speed = TuneSpeed.Walking,
                    Instrument = TuneInstrument.Flute,
                    Notes = [0, 4, 0, 2, 2, 1, 3, 1]
                },
                new CollegeBlockInfo { Kind = CollegeBlockKind.Text, Text = "A note" }
            ]
        };

        var piece = CollegePanel.ToPiece(info, CollegeSubject.Music);

        piece.Kind.Should().Be(PieceKind.Tune);
        piece.Blocks[0].Scale.Should().Be(TuneScale.Mixolydian);
        piece.Blocks[1].Notes.Should().BeNull();
        CollegePanel.ToInfo(piece).Should().BeEquivalentTo(info);
    }
```

Add to `CollegePieceStoreTests` (add `using Chaos.DarkAges.Definitions;` at the top):

```csharp
    [Test]
    public void A_tune_round_trips_and_writing_has_no_tune_keys()
    {
        var store = Create(out var dir);
        var tune = new CollegePiece
        {
            Id = CollegePiece.NewId(),
            Subject = CollegeSubject.Music,
            Kind = PieceKind.Tune,
            Title = "Song",
            Blocks =
            [
                new PieceBlock
                {
                    Type = PieceBlockType.Tune,
                    Scale = TuneScale.Dorian,
                    Speed = TuneSpeed.Lively,
                    Instrument = TuneInstrument.Harp,
                    Notes = [0, 4, 0, 2, 1, 7, 0, 6]
                }
            ]
        };
        var writing = new CollegePiece { Id = CollegePiece.NewId(), Subject = CollegeSubject.Lore, Title = "Words", Blocks = [new PieceBlock { Type = PieceBlockType.Text, Text = "x" }] };

        store.Save(tune);
        store.Save(writing);

        var loaded = store.Load(tune.Id)!.Blocks.Single();
        loaded.Notes.Should().Equal(tune.Blocks[0].Notes);
        loaded.Scale.Should().Be(TuneScale.Dorian);
        loaded.Speed.Should().Be(TuneSpeed.Lively);
        loaded.Instrument.Should().Be(TuneInstrument.Harp);
        File.ReadAllText(Path.Combine(dir, tune.Id + ".json")).Should().Contain("\"Dorian\"");
        var text = File.ReadAllText(Path.Combine(dir, writing.Id + ".json"));
        text.Should().NotContain("Scale").And.NotContain("Notes");
    }
```

In `CollegeEntryServiceTests.Saving_refuses_closed_subjects_and_broken_pieces`, change `CollegeText.PieceProblem(PieceCheck.TitleTooLong)` to `CollegeText.PieceProblem(PieceCheck.TitleTooLong, CollegeSubject.Lore)`.

- [ ] **Step 2: Run to see them fail.** Run the `PieceRulesTests` filter. Expected: build errors (`PieceBlockType.Tune`, `Scale`, `BadTune`, the `PieceProblem` overload and the others).

- [ ] **Step 3: Model.** In `CollegePiece.cs`:
  - add `using Chaos.DarkAges.Definitions;` under `using System.Text.Json.Serialization;`;
  - add `Tune` after `Drawing` in `PieceBlockType`;
  - add `BadTune` after `TooLittleDrawn` in `PieceCheck`;
  - add these properties to `PieceBlock` after `Pixels`:

```csharp

    /// <summary>Tune blocks: the scale the Melody and Bass rows follow.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TuneScale? Scale { get; set; }

    /// <summary>Tune blocks.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TuneSpeed? Speed { get; set; }

    /// <summary>Tune blocks: the melody's instrument.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TuneInstrument? Instrument { get; set; }

    /// <summary>Tune blocks: four bytes per note, layer, row, start, length (saved as base64).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte[]? Notes { get; set; }
```

  - replace `KindOf` with:

```csharp
    public static PieceKind KindOf(CollegeSubject subject)
        => subject switch
        {
            CollegeSubject.Art   => PieceKind.Drawing,
            CollegeSubject.Music => PieceKind.Tune,
            _                    => PieceKind.Writing
        };
```

- [ ] **Step 4: TuneRules.** Create `Chaos/Services/College/TuneRules.cs`:

```csharp
using Chaos.DarkAges.Definitions;

namespace Chaos.Services.College;

/// <summary>Tune-block checks and counts, through the rules both sides share in <see cref="CollegeProtocol" />. No game-world access.</summary>
public static class TuneRules
{
    public static bool IsValid(PieceBlock block)
        => block is { Type: PieceBlockType.Tune, Scale: { } scale, Speed: { } speed, Instrument: { } instrument, Notes: { } notes }
           && CollegeProtocol.TuneIsValid(scale, speed, instrument, notes);

    private static byte[] NotesOf(CollegePiece piece) => piece.Blocks.FirstOrDefault(b => b.Type == PieceBlockType.Tune)?.Notes ?? [];

    public static int NoteCount(CollegePiece piece) => CollegeProtocol.TuneNoteCount(NotesOf(piece));

    public static int MelodyNotes(CollegePiece piece) => CollegeProtocol.TuneMelodyNotes(NotesOf(piece));

    public static int MelodyRows(CollegePiece piece) => CollegeProtocol.TuneMelodyRows(NotesOf(piece));
}
```

- [ ] **Step 5: PieceRules.** In `PieceRules.cs`:

  1. In `Normalize`, add a case after the `Drawing` case:

```csharp
                case PieceBlockType.Tune:
                    blocks.Add(
                        new PieceBlock
                        {
                            Type = PieceBlockType.Tune,
                            Scale = block.Scale,
                            Speed = block.Speed,
                            Instrument = block.Instrument,
                            Notes = block.Notes?.ToArray()
                        });

                    break;
```

  2. In `Check`, replace the start of the method body (the Art line and the drawing line) with:

```csharp
        if (piece.Subject == CollegeSubject.Art)
            return CheckArt(piece);

        if (piece.Subject == CollegeSubject.Music)
            return CheckMusic(piece);

        if (piece.Blocks.Any(b => b.Type is PieceBlockType.Drawing or PieceBlockType.Tune))
            return PieceCheck.WrongBlock;
```

  3. In `CheckEntry`, add after the Art line:

```csharp
        if (piece.Subject == CollegeSubject.Music)
            return (TuneRules.MelodyNotes(piece) < CollegeProtocol.MIN_ENTRY_MELODY_NOTES)
                   || (TuneRules.MelodyRows(piece) < CollegeProtocol.MIN_ENTRY_MELODY_ROWS)
                ? PieceCheck.TooShort
                : PieceCheck.Ok;
```

  4. In `CheckHandIn`, add after the Art line:

```csharp
        if (piece.Subject == CollegeSubject.Music)
            return TuneRules.NoteCount(piece) < CollegeProtocol.MIN_HAND_IN_NOTES ? PieceCheck.TooShort : PieceCheck.Ok;
```

  5. In `CheckArt`, change `b.Type is PieceBlockType.Heading or PieceBlockType.Picture` to `b.Type is PieceBlockType.Heading or PieceBlockType.Picture or PieceBlockType.Tune`.

  6. Add after `CheckArt`:

```csharp

    //a Music piece is a title, one tune and at most one text block, the composer's note
    private static PieceCheck CheckMusic(CollegePiece piece)
    {
        if (piece.Title.Length > CollegeProtocol.MAX_TITLE_CHARS)
            return PieceCheck.TitleTooLong;

        if (piece.Blocks.Any(b => b.Type is PieceBlockType.Heading or PieceBlockType.Picture or PieceBlockType.Drawing)
            || (piece.Blocks.Count(b => b.Type == PieceBlockType.Text) > 1))
            return PieceCheck.WrongBlock;

        var tunes = piece.Blocks.Where(b => b.Type == PieceBlockType.Tune).ToList();

        if ((tunes.Count != 1) || !TuneRules.IsValid(tunes[0]))
            return PieceCheck.BadTune;

        return TextLength(piece) > CollegeProtocol.MAX_MUSIC_NOTE_CHARS ? PieceCheck.TooMuchText : PieceCheck.Ok;
    }
```

- [ ] **Step 6: Texts.** In `CollegeText.cs`, add after `DRAW_MORE_HAND_IN`:

```csharp
    public const string TUNE_TOO_SHORT = "Entries need 16 melody notes on 3 rows.";
    public const string TUNE_TOO_SHORT_HAND_IN = "Hand-ins need at least 8 notes.";
    public const string BAD_TUNE = "That tune could not be read.";
```

and replace `PieceProblem` with:

```csharp
    public static string PieceProblem(PieceCheck check, CollegeSubject subject)
    {
        var art = subject == CollegeSubject.Art;
        var music = subject == CollegeSubject.Music;

        return check switch
        {
            PieceCheck.TooMuchText when art || music => "Notes can be 300 characters at most.",
            PieceCheck.NoTitle when art              => "Give your drawing a title.",
            PieceCheck.NoTitle when music            => "Give your tune a title.",
            PieceCheck.WrongBlock when art           => "Art pieces hold one drawing and a note.",
            PieceCheck.WrongBlock when music         => "Music pieces hold one tune and a note.",
            PieceCheck.TooShort when music           => TUNE_TOO_SHORT,
            PieceCheck.TooManyBlocks                 => "That piece has too many parts.",
            PieceCheck.TitleTooLong                  => "Titles can be 40 characters at most.",
            PieceCheck.HeadingTooLong                => "Headings can be 60 characters at most.",
            PieceCheck.TooMuchText                   => "Pieces can be 10,000 characters at most.",
            PieceCheck.TooManyPictures               => "Pieces can hold 5 pictures at most.",
            PieceCheck.BadHash                       => "A picture is damaged. Insert it again.",
            PieceCheck.DuplicatePicture              => "A picture appears twice.",
            PieceCheck.MissingPicture                => "A picture didn't upload. Insert it again.",
            PieceCheck.NoTitle                       => ENTRY_TOO_SHORT,
            PieceCheck.TooShort                      => ENTRY_TOO_SHORT,
            PieceCheck.BadDrawing                    => "That drawing is damaged. Save it again.",
            PieceCheck.WrongBlock                    => "That piece holds a part it can't.",
            PieceCheck.TooLittleDrawn                => DRAW_MORE,
            PieceCheck.BadTune                       => BAD_TUNE,
            _                                        => string.Empty
        };
    }
```

- [ ] **Step 7: Callers.** In `CollegeService.Entries.cs`:
  - in `SaveDraft` and `Submit`, change `CollegeText.PieceProblem(check, subject == CollegeSubject.Art)` to `CollegeText.PieceProblem(check, subject)`;
  - in `HandIn`, replace the refusal `check switch` with:

```csharp
                check switch
                {
                    PieceCheck.TooShort when running.Subject == CollegeSubject.Music => CollegeText.TUNE_TOO_SHORT_HAND_IN,
                    PieceCheck.TooShort                                               => "Hand-ins need at least 20 characters.",
                    PieceCheck.TooLittleDrawn                                         => CollegeText.DRAW_MORE_HAND_IN,
                    _                                                                 => CollegeText.PieceProblem(check, running.Subject)
                });
```

  Run `grep -rn "PieceProblem(" Chaos Tests --include=*.cs`. Expected: no call still passes a `bool`.

- [ ] **Step 8: Mapping.** In `CollegePanel.cs`, replace `ToPiece` and `ToInfo` with:

```csharp
    public static CollegePiece ToPiece(CollegePieceInfo info, CollegeSubject subject)
        => new()
        {
            Subject = subject,
            Kind = CollegePiece.KindOf(subject),
            Title = info.Title,
            Blocks = info.Blocks
                         .Select(
                             b => new PieceBlock
                             {
                                 Type = b.Kind switch
                                 {
                                     CollegeBlockKind.Heading => PieceBlockType.Heading,
                                     CollegeBlockKind.Picture => PieceBlockType.Picture,
                                     CollegeBlockKind.Drawing => PieceBlockType.Drawing,
                                     CollegeBlockKind.Tune    => PieceBlockType.Tune,
                                     _                        => PieceBlockType.Text
                                 },
                                 Text = b.Kind is CollegeBlockKind.Text or CollegeBlockKind.Heading ? b.Text : "",
                                 Hash = b.Kind == CollegeBlockKind.Picture ? b.Hash : "",
                                 Palette = b.Kind == CollegeBlockKind.Drawing ? DrawingRules.PaletteToHex(b.Palette) : null,
                                 Pixels = b.Kind == CollegeBlockKind.Drawing ? b.Pixels.ToArray() : null,
                                 Scale = b.Kind == CollegeBlockKind.Tune ? b.Scale : null,
                                 Speed = b.Kind == CollegeBlockKind.Tune ? b.Speed : null,
                                 Instrument = b.Kind == CollegeBlockKind.Tune ? b.Instrument : null,
                                 Notes = b.Kind == CollegeBlockKind.Tune ? b.Notes.ToArray() : null
                             })
                         .ToList()
        };

    public static CollegePieceInfo ToInfo(CollegePiece piece)
        => new()
        {
            Subject = (CollegeSubjectCode)piece.Subject,
            Title = piece.Title,
            Blocks = piece.Blocks.Select(ToBlockInfo).ToList()
        };

    public static CollegeBlockInfo ToBlockInfo(PieceBlock b)
        => new()
        {
            Kind = b.Type switch
            {
                PieceBlockType.Heading => CollegeBlockKind.Heading,
                PieceBlockType.Picture => CollegeBlockKind.Picture,
                PieceBlockType.Drawing => CollegeBlockKind.Drawing,
                PieceBlockType.Tune    => CollegeBlockKind.Tune,
                _                      => CollegeBlockKind.Text
            },
            Text = b.Text,
            Hash = b.Hash,
            Palette = b.Type == PieceBlockType.Drawing ? DrawingRules.PaletteToBytes(b.Palette) : [],
            Pixels = b.Type == PieceBlockType.Drawing ? b.Pixels ?? [] : [],
            Scale = b.Scale ?? default,
            Speed = b.Speed ?? default,
            Instrument = b.Instrument ?? default,
            Notes = b.Type == PieceBlockType.Tune ? b.Notes ?? [] : []
        };
```

- [ ] **Step 9: Run the tests.** Run `PieceRulesTests`, `CollegePanelMappingTests` and `CollegePieceStoreTests`. Expected: all pass. Then run all College tests (`--treenode-filter "/*/Chaos.Tests.College/*/*"`). Expected: all pass. Music is still closed until Task 3, so `Saving_refuses_closed_subjects_and_broken_pieces` and `EntryBookTests.Submit_checks_subject_open_entry_draft_and_marks` still pass.

```json:metadata
{"files": ["Chaos/Services/College/CollegePiece.cs", "Chaos/Services/College/TuneRules.cs", "Chaos/Services/College/PieceRules.cs", "Chaos/Services/College/CollegeText.cs", "Chaos/Services/College/CollegeService.Entries.cs", "Chaos/Services/College/CollegePanel.cs", "Tests/Chaos.Tests/College/PieceRulesTests.cs", "Tests/Chaos.Tests/College/CollegePanelMappingTests.cs", "Tests/Chaos.Tests/College/CollegePieceStoreTests.cs", "Tests/Chaos.Tests/College/CollegeEntryServiceTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/Chaos.Tests.College/*/*\"", "acceptanceCriteria": ["Music piece needs one valid tune (BadTune)", "WrongBlock for other blocks in Music and tunes elsewhere", "note 300", "entry 16 melody notes on 3 rows; hand-in 8 notes", "Music texts from PieceProblem(check, subject)", "tune mapping and piece-file round trip"], "modelTier": "standard"}
```

---

### Task 3: Server service: open Music, Music activities and hand-ins, TuneFetch (server repo)

**Goal:** Music entries and Music activity classes work end to end on the server. The gallery can fetch one claimed tune by entry id, sharing the Art thumbnails' fetch limit.

**Files:**
- Modify: `Chaos/Services/College/CollegeOptions.cs`
- Modify: `Chaos/Services/College/CollegeText.cs` (`ClassBegun`)
- Modify: `Chaos/Services/College/CollegeService.Entries.cs` (new `GalleryTune`)
- Create: `Chaos/Services/College/CollegeFetchLimit.cs`
- Modify: `Chaos/Services/College/CollegePanel.cs` (`Handle`, fetch limits, `SendPicture`, `SendDrawing`, new `SendTune`, the logout-draft throttle)
- Test: `Tests/Chaos.Tests/College/CollegeEntryServiceTests.cs`, `EntryBookTests.cs`, `TimetableTests.cs`, `CollegeServiceTests.cs`, new `CollegeFetchLimitTests.cs`

**Acceptance Criteria:**
- [ ] Music drafts save and submit, with the Music message for each refusal
- [ ] Activity classes can be booked and started in every subject, Music included
- [ ] Music hand-ins need a running Music Activity class, in its room. A tune handed in to an Art class is refused, and so is a drawing handed in to a Music class.
- [ ] `GalleryTune(id)` returns the tune only for a `Claimed`, not hidden, Music entry
- [ ] `TuneFetch` is answered outside the 250 ms throttle, sharing one limit of 60 a minute per player with `DrawingFetch`
- [ ] A Music activity class is announced with " (music class)"
- [ ] Logging out with three College windows open saves all three drafts (one per subject); a subject can't skip the throttle twice in one burst

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.College/*/*"` -> all pass

**Steps:**

- [ ] **Step 1: Update the tests that expected Music to be closed or without activities.**
  - `CollegeEntryServiceTests.Saving_refuses_closed_subjects_and_broken_pieces`: change `var rig = Create();` to `var rig = Create(new CollegeOptions { OpenSubjects = [CollegeSubject.History, CollegeSubject.Lore] });`. Music stays the closed subject in that test.
  - `EntryBookTests.Submit_checks_subject_open_entry_draft_and_marks`: replace its first line after `var state = new CollegeState();` with:

```csharp
        EntryBook.CanSubmit(state, new CollegeOptions { OpenSubjects = [CollegeSubject.History] }, "Aroha", CollegeSubject.Music)
                 .Should().Be(SubmitResult.NotOpen);
        EntryBook.CanSubmit(state, Options, "Aroha", CollegeSubject.Music).Should().Be(SubmitResult.NoDraft);
```

  - `TimetableTests.Bad_bookings_are_refused`: delete the line `Book(state, OneOClock, 30, ClassFormat.Activity, CollegeSubject.Music).Should().Be(BookResult.FormatNotAvailable);`.
  - `TimetableTests.Activity_can_be_booked_for_every_subject_but_music`: rename it to `Activity_can_be_booked_for_every_subject` and replace its body with:

```csharp
        var state = new CollegeState();

        Book(state, OneOClock, 30, ClassFormat.Activity, CollegeSubject.Music).Should().Be(BookResult.Booked);
        Book(state, OneOClock.AddHours(1), 30, ClassFormat.Activity, CollegeSubject.Art).Should().Be(BookResult.Booked);
        Book(state, OneOClock.AddHours(2), 30, ClassFormat.Activity, CollegeSubject.History).Should().Be(BookResult.Booked);
```

  Then run `grep -rn "FormatNotAvailable\|CollegeSubject.Music" Tests/Chaos.Tests/College/`. Any other test that expects Music to be closed, or expects `FormatNotAvailable`, changes the same way.

- [ ] **Step 2: Write the failing tests.** Add to `CollegeEntryServiceTests` (the file already has `using Chaos.DarkAges.Definitions;`):

```csharp
    private static CollegePiece MusicPiece(string title, int melodyNotes, int rows = 3, int drumNotes = 0, string note = "")
    {
        var notes = new List<byte>();

        for (var i = 0; i < melodyNotes; i++)
            notes.AddRange([(byte)TuneLayer.Melody, (byte)(i % rows), (byte)i, 1]);

        for (var i = 0; i < drumNotes; i++)
            notes.AddRange([(byte)TuneLayer.Drums, 0, (byte)i, 1]);

        var blocks = new List<PieceBlock>
        {
            new()
            {
                Type = PieceBlockType.Tune,
                Scale = TuneScale.Dorian,
                Speed = TuneSpeed.Lively,
                Instrument = TuneInstrument.Harp,
                Notes = notes.ToArray()
            }
        };

        if (note.Length > 0)
            blocks.Add(new PieceBlock { Type = PieceBlockType.Text, Text = note });

        return new CollegePiece { Title = title, Blocks = blocks };
    }

    [Test]
    public void A_music_draft_saves_and_submits()
    {
        var rig = Create();
        rig.State.StudentFor("Aroha").Marks += 3;

        rig.Service.SaveDraft("Aroha", CollegeSubject.Music, MusicPiece("Song", 15)).Result.Should().Be(CollegeWriterResult.Saved);
        rig.Service.Submit("Aroha", CollegeSubject.Music).Message.Should().Be(CollegeText.TUNE_TOO_SHORT);
        rig.Service.SaveDraft("Aroha", CollegeSubject.Music, MusicPiece("Song", 16, rows: 2));
        rig.Service.Submit("Aroha", CollegeSubject.Music).Message.Should().Be(CollegeText.TUNE_TOO_SHORT);
        rig.Service.SaveDraft("Aroha", CollegeSubject.Music, MusicPiece("", 16));
        rig.Service.Submit("Aroha", CollegeSubject.Music).Message.Should().Be("Give your tune a title.");

        rig.Service.SaveDraft("Aroha", CollegeSubject.Music, MusicPiece("Song", 16, note: "For the well.")).Result.Should().Be(CollegeWriterResult.Saved);
        rig.Service.Submit("Aroha", CollegeSubject.Music).Result.Should().Be(CollegeWriterResult.Submitted);

        var piece = rig.Pieces.Load(rig.State.Entries.Single().PieceId)!;
        piece.Kind.Should().Be(PieceKind.Tune);
        piece.Blocks.Select(b => b.Type).Should().Equal(PieceBlockType.Tune, PieceBlockType.Text);
        piece.Blocks[0].Scale.Should().Be(TuneScale.Dorian);
        piece.Blocks[0].Notes.Should().Equal(MusicPiece("Song", 16).Blocks[0].Notes);
    }

    [Test]
    public void Music_and_other_drafts_refuse_each_others_pieces()
    {
        var rig = Create();

        rig.Service.SaveDraft("Aroha", CollegeSubject.Music, Writing("Words", 300)).Result.Should().Be(CollegeWriterResult.Refused);
        rig.Service.SaveDraft("Aroha", CollegeSubject.Music, ArtPiece("Dusk", 400)).Result.Should().Be(CollegeWriterResult.Refused);
        rig.Service.SaveDraft("Aroha", CollegeSubject.History, MusicPiece("Song", 16)).Result.Should().Be(CollegeWriterResult.Refused);
        rig.Service.SaveDraft("Aroha", CollegeSubject.Art, MusicPiece("Song", 16)).Result.Should().Be(CollegeWriterResult.Refused);
    }

    [Test]
    public void Music_hand_ins_need_a_music_activity_class()
    {
        var rig = Create();
        rig.Service.StartWalkIn("Sir", "m0", CollegeSubject.Music, ClassFormat.Activity, "college_creation", true, false)
           .Result.Should().Be(StartResult.Started);

        rig.Service.HandIn("Aroha", "college_creation", MusicPiece("", 0, drumNotes: 7)).Message.Should().Be(CollegeText.TUNE_TOO_SHORT_HAND_IN);
        rig.Service.HandIn("Aroha", "college_creation", ArtPiece("", 400)).Result.Should().Be(CollegeWriterResult.Refused);
        rig.Service.HandIn("Aroha", "college_creation", MusicPiece("", 0, drumNotes: 8)).Result.Should().Be(CollegeWriterResult.HandedIn);

        var handIn = rig.State.HandIns.Single();
        handIn.Subject.Should().Be(CollegeSubject.Music);
        handIn.Title.Should().Be("Untitled");
        rig.Pieces.Load(handIn.PieceId)!.Kind.Should().Be(PieceKind.Tune);
    }

    [Test]
    public void Art_classes_refuse_tunes()
    {
        var rig = Create();
        rig.Service.StartWalkIn("Sir", "m0", CollegeSubject.Art, ClassFormat.Activity, "college_creation", true, false)
           .Result.Should().Be(StartResult.Started);

        rig.Service.HandIn("Aroha", "college_creation", MusicPiece("", 16)).Result.Should().Be(CollegeWriterResult.Refused);
    }

    [Test]
    public void Gallery_tunes_come_only_from_visible_claimed_music()
    {
        var rig = Create();
        rig.Service.SetDirector("Dir");
        rig.State.StudentFor("Aroha").Marks += 3;
        rig.Service.SaveDraft("Aroha", CollegeSubject.Music, MusicPiece("Song", 16));
        rig.Service.Submit("Aroha", CollegeSubject.Music).Result.Should().Be(CollegeWriterResult.Submitted);
        var id = rig.State.Entries.Single().Id;

        rig.Service.GalleryTune(id).Should().BeNull();

        rig.Service.CloseVoting(id);
        rig.Service.PostVerdict("Dir", false, id, AwardTier.Village, "").Should().Be(VerdictResult.Posted);
        rig.Service.Claim("Aroha", id, out _, out _).Should().Be(ClaimResult.Claimed);

        var tune = rig.Service.GalleryTune(id)!;
        tune.Scale.Should().Be(TuneScale.Dorian);
        tune.Notes.Should().HaveCount(16 * CollegeProtocol.TUNE_NOTE_BYTES);

        rig.Service.SetHidden("Aroha", id, true);
        rig.Service.GalleryTune(id).Should().BeNull();
        rig.Service.GalleryTune(999).Should().BeNull();

        var history = SubmitOne(rig, "Brannoc");
        rig.Service.CloseVoting(history);
        rig.Service.PostVerdict("Dir", false, history, AwardTier.Village, "");
        rig.Service.Claim("Brannoc", history, out _, out _).Should().Be(ClaimResult.Claimed);
        rig.Service.GalleryTune(history).Should().BeNull();
    }
```

(The helper calls match `Gallery_drawings_come_only_from_visible_claimed_art`, already in this file: `SetDirector`, `CloseVoting`, `PostVerdict`, `Claim` and `SetHidden`. If a signature differs, copy it from that test.)

Add to `CollegeServiceTests`:

```csharp
    [Test]
    public void A_music_activity_is_announced_as_a_music_class()
        => CollegeText.ClassBegun(CollegeSubject.Music, "Sir", ClassFormat.Activity).Should().Be("A Music class has begun! (music class)");

    [Test]
    public void Every_subject_is_open_and_has_activities()
    {
        var options = new CollegeOptions();

        foreach (var subject in Enum.GetValues<CollegeSubject>())
        {
            options.IsOpen(subject).Should().BeTrue(subject.ToString());
            CollegeOptions.HasActivity(subject).Should().BeTrue(subject.ToString());
        }
    }
```

Create `Tests/Chaos.Tests/College/CollegeFetchLimitTests.cs`:

```csharp
using Chaos.Services.College;
using FluentAssertions;

namespace Chaos.Tests.College;

public sealed class CollegeFetchLimitTests
{
    private static readonly DateTime Noon = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void A_name_gets_its_limit_per_minute()
    {
        var limit = new CollegeFetchLimit(60);

        for (var i = 0; i < 60; i++)
            limit.TryTake("Aroha", Noon.AddSeconds(i * 0.5)).Should().BeTrue();

        limit.TryTake("AROHA", Noon.AddSeconds(31)).Should().BeFalse();
        limit.TryTake("Brannoc", Noon.AddSeconds(31)).Should().BeTrue();
        limit.TryTake("Aroha", Noon.AddSeconds(60)).Should().BeTrue();
    }
}
```

- [ ] **Step 3: Run to see them fail.** Run the `CollegeEntryServiceTests` filter. Expected: build errors (`GalleryTune`, `CollegeFetchLimit` missing).

- [ ] **Step 4: Options.** In `CollegeOptions.cs`, replace the `OpenSubjects` summary and default with:

```csharp
    /// <summary>Subjects whose entries are open. Every subject is open since the composer shipped (part 4).</summary>
    public List<CollegeSubject> OpenSubjects { get; set; } =
    [
        CollegeSubject.Art,
        CollegeSubject.Music,
        CollegeSubject.History,
        CollegeSubject.Literature,
        CollegeSubject.Lore,
        CollegeSubject.Philosophy
    ];
```

and replace `HasActivity` and its summary with:

```csharp
    /// <summary>Subjects whose classes can be Activities. Every subject has its tool since part 4: writing, the canvas, the composer.</summary>
    public static bool HasActivity(CollegeSubject subject) => true;
```

- [ ] **Step 5: Class text.** In `CollegeText.cs`:
  - add `private const string MUSIC_CLASS = " (music class)";` after `DRAWING_CLASS`;
  - in `ClassBegun`, replace the `var label = ...` line with:

```csharp
        var label = subject switch
        {
            CollegeSubject.Art   => DRAWING_CLASS,
            CollegeSubject.Music => MUSIC_CLASS,
            _                    => WRITING_CLASS
        };
```

  - change the summary's second sentence to: `A writing class ends with " (writing class)", a drawing class with " (drawing class)" and a music class with " (music class)" when that fits; the prompt is never shown here.`

- [ ] **Step 6: GalleryTune.** In `CollegeService.Entries.cs`, add after `GalleryDrawing`:

```csharp

    /// <summary>A gallery row's tune: the tune of a claimed, shown Music entry, or null.</summary>
    public PieceBlock? GalleryTune(int id)
    {
        using var scope = Sync.EnterScope();

        if (EntryBook.Find(State, id) is not { Subject: CollegeSubject.Music, Status: EntryStatus.Claimed, Hidden: false } entry)
            return null;

        return Pieces.Load(entry.PieceId)?.Blocks.FirstOrDefault(b => (b.Type == PieceBlockType.Tune) && TuneRules.IsValid(b));
    }
```

- [ ] **Step 7: Fetch limit.** Create `Chaos/Services/College/CollegeFetchLimit.cs`:

```csharp
namespace Chaos.Services.College;

/// <summary>At most <c>perMinute</c> fetches per character name in any one minute. Names ignore case.</summary>
public sealed class CollegeFetchLimit(int perMinute)
{
    private readonly Dictionary<string, List<DateTime>> Log = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock Sync = new();

    public bool TryTake(string name, DateTime now)
    {
        using var scope = Sync.EnterScope();

        if (!Log.TryGetValue(name, out var times))
            Log[name] = times = [];

        times.RemoveAll(t => now - t >= TimeSpan.FromMinutes(1));

        if (times.Count >= perMinute)
            return false;

        times.Add(now);

        return true;
    }
}
```

- [ ] **Step 8: Panel.** In `CollegePanel.cs`:
  1. Replace the two constants and the three fetch fields (`Fetches`, `DrawingFetches`, `FetchSync`) with:

```csharp
    private const int FETCHES_PER_MINUTE = 30;

    //the gallery's Art thumbnails and Music Play buttons share one limit
    private const int GALLERY_FETCHES_PER_MINUTE = 60;

    private readonly CollegeFetchLimit PictureFetches = new(FETCHES_PER_MINUTE);
    private readonly CollegeFetchLimit GalleryFetches = new(GALLERY_FETCHES_PER_MINUTE);
```

  2. Delete the private `TakeFetch` method.
  3. In `SendPicture`, change `if (!TakeFetch(Fetches, viewer.Name, FETCHES_PER_MINUTE))` to `if (!PictureFetches.TryTake(viewer.Name, Now))`.
  4. In `SendDrawing`, change `!TakeFetch(DrawingFetches, viewer.Name, DRAWING_FETCHES_PER_MINUTE)` to `!GalleryFetches.TryTake(viewer.Name, Now)`.
  5. In `Handle`, add to the first switch, after the `DrawingFetch` case:

```csharp
            case CollegeActionType.TuneFetch:
                SendTune(viewer, args.Id);

                return;
```

  6. Add after `SendDrawing`:

```csharp

    //a gallery row's Play button; like the thumbnails it skips the 250 ms throttle
    private void SendTune(Aisling viewer, int id)
    {
        if (!GalleryFetches.TryTake(viewer.Name, Now) || college.GalleryTune(id) is not { } tune)
            return;

        viewer.Client.SendCollegeDisplay(
            new CollegeDisplayArgs
            {
                Type = CollegeDisplayType.Tune,
                Id = id,
                Tune = ToBlockInfo(tune)
            });
    }
```

  7. **Logout drafts.** Today one extra SaveDraft of a different subject may skip the throttle, for the writing window and the Art canvas closing together. With the composer, three windows can close together, and the third draft would be dropped. Replace the `LastDraftSubjects` field with:

```csharp
    private readonly Dictionary<string, HashSet<CollegeSubject>> BurstDraftSubjects = new(StringComparer.OrdinalIgnoreCase);
```

  and replace `IsSecondWindowDraft` and `NoteDraftSubject` (keep their call sites, renaming the first) with:

```csharp
    //logging out with the writing window, the Art canvas and the composer open sends one SaveDraft per subject back to back
    private bool IsAnotherWindowDraft(string name, CollegeActionType type, CollegeSubject subject)
    {
        if (type != CollegeActionType.SaveDraft)
            return false;

        using var scope = DraftSync.EnterScope();

        return BurstDraftSubjects.TryGetValue(name, out var seen) && !seen.Contains(subject);
    }

    //a throttle-free draft starts a burst; each let-through draft adds its subject, so no subject skips the throttle twice
    private void NoteDraftSubject(string name, CollegeActionType type, CollegeSubject subject, bool throttled)
    {
        using var scope = DraftSync.EnterScope();

        if (type != CollegeActionType.SaveDraft)
            BurstDraftSubjects.Remove(name);
        else if (!throttled)
            BurstDraftSubjects[name] = [subject];
        else if (BurstDraftSubjects.TryGetValue(name, out var seen))
            seen.Add(subject);
    }
```

  In `Handle`, change `IsSecondWindowDraft(` to `IsAnotherWindowDraft(`.

- [ ] **Step 9: Run the tests.** Run `--treenode-filter "/*/Chaos.Tests.College/*/*"`. Expected: all pass. Then run `dotnet build Chaos.slnx`. Expected: Build succeeded.

```json:metadata
{"files": ["Chaos/Services/College/CollegeOptions.cs", "Chaos/Services/College/CollegeText.cs", "Chaos/Services/College/CollegeService.Entries.cs", "Chaos/Services/College/CollegeFetchLimit.cs", "Chaos/Services/College/CollegePanel.cs", "Tests/Chaos.Tests/College/CollegeEntryServiceTests.cs", "Tests/Chaos.Tests/College/EntryBookTests.cs", "Tests/Chaos.Tests/College/TimetableTests.cs", "Tests/Chaos.Tests/College/CollegeServiceTests.cs", "Tests/Chaos.Tests/College/CollegeFetchLimitTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/Chaos.Tests.College/*/*\"", "acceptanceCriteria": ["Music drafts save and submit with Music messages", "Activity bookable/startable in every subject", "hand-ins checked as the class subject both ways for Music", "GalleryTune only for visible claimed Music", "TuneFetch shares the 60/min gallery limit", "music class announcement", "three logout drafts all saved"], "modelTier": "standard"}
```

---

### Task 4: Registrar and lectern (server repo; no Unora changes)

**Goal:** Players reach the composer from the Registrar and the lectern, and no dialog still says Music opens soon.

**Files:**
- Modify (server): `Chaos/Scripting/DialogScripts/Temuair/College/CollegeRegistrarScript.cs`
- Modify (server): `Chaos/Scripting/DialogScripts/Temuair/College/CollegeLecternScript.cs`

Unora needs no edits, so the Unora worktree is not created. Its College dialogs (`Data/Configuration/Templates/Dialogs/Temauir/mileth/college/`) were checked: they already say "enter", "make" and "work" (part 3) and never name Music.

**Acceptance Criteria:**
- [ ] The Registrar's subject list shows Music without "(opens soon)", and picking it opens the composer through `Panel.OpenWriter`. Booking offers Activity for Music.
- [ ] The "opens soon" text no longer names Music
- [ ] During a Music activity, students see "Compose a hand-in"; Art keeps "Draw a hand-in" and writing subjects keep "Write a hand-in"
- [ ] The server builds

**Verify:** `dotnet build Chaos.slnx` in the server worktree -> Build succeeded

**Steps:**

- [ ] **Step 1: Registrar.** In `CollegeRegistrarScript.cs`, in `college_registrar_write_open`, change the else text to `Subject.Text = "Entries in that subject open soon.";`. Nothing else changes. Music is open (Task 3), so the subject list and the Activity option follow `IsOpen` and `HasActivity` on their own.

- [ ] **Step 2: Lectern.** In `CollegeLecternScript.cs`:
  - add `private const string COMPOSE_HAND_IN = "Compose a hand-in";` after `DRAW_HAND_IN`;
  - in `ShowMain`, replace `Subject.AddOption(CollegeOptions.IsWritingSubject(running.Subject) ? WRITE_HAND_IN : DRAW_HAND_IN, "college_lectern_handin");` with `Subject.AddOption(HandInOption(running.Subject), "college_lectern_handin");`;
  - add this method at the end of the class:

```csharp

    private static string HandInOption(CollegeSubject subject)
        => subject switch
        {
            CollegeSubject.Art   => DRAW_HAND_IN,
            CollegeSubject.Music => COMPOSE_HAND_IN,
            _                    => WRITE_HAND_IN
        };
```

  `IsWritingSubject` then has no caller. Leave it in place: it is public and part 2's spec names it.

- [ ] **Step 3: Build.** Run `dotnet build Chaos.slnx` in the server worktree. Expected: Build succeeded.

```json:metadata
{"files": ["Chaos/Scripting/DialogScripts/Temuair/College/CollegeRegistrarScript.cs", "Chaos/Scripting/DialogScripts/Temuair/College/CollegeLecternScript.cs"], "verifyCommand": "dotnet build Chaos.slnx", "acceptanceCriteria": ["Music listed and opens the composer; Activity offered", "opens-soon text no longer names Music", "Compose a hand-in for Music; Draw/Write kept", "server builds"], "modelTier": "mechanical"}
```

---

### Task 5: Client tune model: TuneNote, TuneData, TuneScales, TuneDocument (client repo)

**Goal:** A pure tune model with no graphics or sound. It maps rows to pitches and names, and does every edit the
composer needs, with undo.

**Files:**
- Create: `Chaos.Client/ViewModel/College/TuneNote.cs`
- Create: `Chaos.Client/ViewModel/College/TuneData.cs`
- Create: `Chaos.Client/ViewModel/College/TuneScales.cs`
- Create: `Chaos.Client/ViewModel/College/TuneDocument.cs`
- Test: `Tests/Chaos.Client.Tests/College/TuneScalesTests.cs`, `TuneDataTests.cs`, `TuneDocumentTests.cs`

**Acceptance Criteria:**
- [ ] `TuneScales.Midi` gives melody base 60 and bass base 36. It handles every scale, including Five-note across three
      octaves, and gives -1 for drums.
- [ ] `TuneScales.RowName` gives names such as "C4", "Eb4" and "B2", and "Drum", "Tamb", "Wood" for the drum rows.
- [ ] `TuneData` round-trips through `CollegeBlockInfo`. `From` drops notes outside the grid and ignores trailing bytes.
- [ ] A drag stops at the next note in the row and at column 64. Drum notes stay 1 long.
- [ ] Placing on a filled cell, or a 513th note, is refused.
- [ ] Copy bars 1-4 to 5-8 cuts a note that crosses the middle, replaces bars 5-8, and leaves other layers alone.
- [ ] Undo keeps 50 steps, a new edit clears Redo, and a scale change undoes.
- [ ] `MelodyNotes`, `MelodyRows` and `NoteCount` are right.
- [ ] `Changed` is raised each time `Version` goes up, including on undo and load.

**Verify:** `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\college-music-server -- --treenode-filter "/*/*/TuneDocumentTests/*"` -> all pass; same for `TuneScalesTests` and `TuneDataTests`

**Steps:**

> **Run order:** this task compiles only after server Task 1. Task 1 adds `TuneScale`, `TuneSpeed`, `TuneInstrument`,
> `TuneLayer`, `CollegeBlockKind.Tune`, the `CollegeBlockInfo` tune fields and the `CollegeProtocol` tune helpers.
> `UnoraServerPath` points the client build at the server worktree.

- [ ] **Step 1: Write the failing tests.** Create `Tests/Chaos.Client.Tests/College/TuneScalesTests.cs`:

```csharp
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class TuneScalesTests
{
    [Test]
    public void Melody_starts_at_middle_C_and_bass_two_octaves_lower()
    {
        TuneScales.Midi(TuneScale.Major, TuneLayer.Melody, 0).Should().Be(60);
        TuneScales.Midi(TuneScale.Major, TuneLayer.Bass, 0).Should().Be(36);
    }

    [Test]
    public void A_seven_note_scale_spans_two_octaves_and_one_note()
    {
        TuneScales.Midi(TuneScale.Major, TuneLayer.Melody, 7).Should().Be(72);
        TuneScales.Midi(TuneScale.Major, TuneLayer.Melody, 14).Should().Be(84);
        TuneScales.Midi(TuneScale.Minor, TuneLayer.Melody, 2).Should().Be(63);
        TuneScales.Midi(TuneScale.Dorian, TuneLayer.Melody, 5).Should().Be(69);
        TuneScales.Midi(TuneScale.Mixolydian, TuneLayer.Melody, 6).Should().Be(70);
        TuneScales.Midi(TuneScale.Desert, TuneLayer.Bass, 6).Should().Be(47);
    }

    [Test]
    public void Five_note_spans_three_octaves()
    {
        TuneScales.Midi(TuneScale.FiveNote, TuneLayer.Melody, 5).Should().Be(72);
        TuneScales.Midi(TuneScale.FiveNote, TuneLayer.Melody, 14).Should().Be(93);
    }

    [Test]
    public void Drums_have_no_pitch()
        => TuneScales.Midi(TuneScale.Major, TuneLayer.Drums, 0).Should().Be(-1);

    [Test]
    public void Row_names_use_the_note_and_octave()
    {
        TuneScales.RowName(TuneScale.Major, TuneLayer.Melody, 0).Should().Be("C4");
        TuneScales.RowName(TuneScale.Minor, TuneLayer.Melody, 2).Should().Be("Eb4");
        TuneScales.RowName(TuneScale.Desert, TuneLayer.Bass, 6).Should().Be("B2");
        TuneScales.RowName(TuneScale.Major, TuneLayer.Drums, 0).Should().Be("Drum");
        TuneScales.RowName(TuneScale.Major, TuneLayer.Drums, 1).Should().Be("Tamb");
        TuneScales.RowName(TuneScale.Major, TuneLayer.Drums, 2).Should().Be("Wood");
    }

    [Test]
    public void Home_rows_repeat_every_scale_length()
    {
        TuneScales.IsHome(TuneScale.Major, 0).Should().BeTrue();
        TuneScales.IsHome(TuneScale.Major, 7).Should().BeTrue();
        TuneScales.IsHome(TuneScale.FiveNote, 5).Should().BeTrue();
        TuneScales.IsHome(TuneScale.FiveNote, 7).Should().BeFalse();
    }

    [Test]
    public void A_column_is_half_a_beat()
    {
        TuneScales.StepSeconds(TuneSpeed.Steady).Should().BeApproximately(0.25, 1e-9);
        TuneScales.StepSeconds(TuneSpeed.Slow).Should().BeApproximately(0.375, 1e-9);
        TuneScales.StepSeconds(TuneSpeed.Quick).Should().BeApproximately(0.1875, 1e-9);
    }
}
```

Create `Tests/Chaos.Client.Tests/College/TuneDataTests.cs`:

```csharp
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class TuneDataTests
{
    [Test]
    public void A_tune_round_trips_through_a_block()
    {
        var tune = new TuneData(
            TuneScale.Dorian,
            TuneSpeed.Lively,
            TuneInstrument.Harp,
            [new TuneNote(TuneLayer.Melody, 4, 0, 3), new TuneNote(TuneLayer.Drums, 2, 63, 1)]);

        var block = tune.ToBlock();
        var back = TuneData.From(block);

        block.Kind.Should().Be(CollegeBlockKind.Tune);
        block.Notes.Should().Equal(0, 4, 0, 3, 2, 2, 63, 1);
        back.Scale.Should().Be(TuneScale.Dorian);
        back.Speed.Should().Be(TuneSpeed.Lively);
        back.Instrument.Should().Be(TuneInstrument.Harp);
        back.Notes.Should().Equal(tune.Notes);
    }

    [Test]
    public void From_drops_notes_outside_the_grid_and_trailing_bytes()
    {
        var block = new CollegeBlockInfo
        {
            Kind = CollegeBlockKind.Tune,
            Notes =
            [
                0, 3, 10, 2,
                0, 15, 0, 1,
                2, 3, 0, 1,
                1, 0, 0, 0,
                1, 0, 60, 5,
                2, 0, 4, 2,
                9
            ]
        };

        TuneData.From(block).Notes.Should().Equal(new TuneNote(TuneLayer.Melody, 3, 10, 2));
    }

    [Test]
    public void The_empty_tune_is_major_steady_lute()
    {
        TuneData.Empty.Scale.Should().Be(TuneScale.Major);
        TuneData.Empty.Speed.Should().Be(TuneSpeed.Steady);
        TuneData.Empty.Instrument.Should().Be(TuneInstrument.Lute);
        TuneData.Empty.Notes.Should().BeEmpty();
    }
}
```

Create `Tests/Chaos.Client.Tests/College/TuneDocumentTests.cs`:

```csharp
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class TuneDocumentTests
{
    private static TuneDocument Document(params TuneNote[] notes)
    {
        var document = new TuneDocument();
        document.Load(TuneData.Empty with { Notes = notes });

        return document;
    }

    private static void Place(TuneDocument document, TuneLayer layer, int row, int column, int dragTo = -1)
    {
        document.BeginPlace(layer, row, column).Should().BeTrue();

        if (dragTo >= 0)
            document.DragTo(dragTo);

        document.EndPlace();
    }

    [Test]
    public void A_placed_note_is_one_column_and_undoes_as_one_step()
    {
        var document = new TuneDocument();

        document.BeginPlace(TuneLayer.Melody, 3, 5).Should().BeTrue();
        document.CanUndo.Should().BeFalse();
        document.DragTo(8);
        document.EndPlace();

        document.Notes.Should().Equal(new TuneNote(TuneLayer.Melody, 3, 5, 4));
        document.IsDirty.Should().BeTrue();
        document.Undo().Should().BeTrue();
        document.Notes.Should().BeEmpty();
        document.CanUndo.Should().BeFalse();
    }

    [Test]
    public void Changed_is_raised_each_time_the_version_goes_up()
    {
        var document = new TuneDocument();
        var raised = 0;
        document.Changed += () => raised++;

        Place(document, TuneLayer.Melody, 3, 5, dragTo: 7);
        document.Undo();
        document.Load(TuneData.Empty);

        raised.Should().Be(document.Version);
        raised.Should().BeGreaterThan(3);
    }

    [Test]
    public void A_drag_stops_before_the_next_note_in_the_row()
    {
        var document = Document(new TuneNote(TuneLayer.Melody, 3, 10, 2));

        Place(document, TuneLayer.Melody, 3, 4, 20);

        document.NoteAt(TuneLayer.Melody, 3, 4).Should().Be(new TuneNote(TuneLayer.Melody, 3, 4, 6));
    }

    [Test]
    public void A_drag_stops_at_the_end_of_the_grid()
    {
        var document = new TuneDocument();

        Place(document, TuneLayer.Bass, 0, 60, 80);

        document.Notes.Should().Equal(new TuneNote(TuneLayer.Bass, 0, 60, 4));
    }

    [Test]
    public void A_drag_to_the_left_keeps_one_column()
    {
        var document = new TuneDocument();

        Place(document, TuneLayer.Melody, 0, 20, 5);

        document.Notes.Should().Equal(new TuneNote(TuneLayer.Melody, 0, 20, 1));
    }

    [Test]
    public void Drum_notes_stay_one_column()
    {
        var document = new TuneDocument();

        Place(document, TuneLayer.Drums, 1, 0, 10);

        document.Notes.Should().Equal(new TuneNote(TuneLayer.Drums, 1, 0, 1));
    }

    [Test]
    public void A_filled_cell_or_a_cell_outside_the_grid_is_refused()
    {
        var document = Document(new TuneNote(TuneLayer.Melody, 2, 8, 4));

        document.BeginPlace(TuneLayer.Melody, 2, 10).Should().BeFalse();
        document.BeginPlace(TuneLayer.Melody, 15, 0).Should().BeFalse();
        document.BeginPlace(TuneLayer.Drums, 3, 0).Should().BeFalse();
        document.BeginPlace(TuneLayer.Melody, 0, 64).Should().BeFalse();
        document.Notes.Should().HaveCount(1);
    }

    [Test]
    public void The_513th_note_is_refused()
    {
        var notes = new List<TuneNote>();

        for (var row = 0; row < 15; row++)
            for (var start = 0; start < 64; start++)
                if (notes.Count < CollegeProtocol.MAX_TUNE_NOTES)
                    notes.Add(new TuneNote(row < 8 ? TuneLayer.Melody : TuneLayer.Bass, row, start, 1));

        var document = Document(notes.ToArray());

        document.NoteCount.Should().Be(512);
        document.BeginPlace(TuneLayer.Drums, 0, 0).Should().BeFalse();
    }

    [Test]
    public void Remove_takes_the_note_under_a_cell()
    {
        var document = Document(new TuneNote(TuneLayer.Bass, 4, 10, 6));

        document.Remove(TuneLayer.Bass, 4, 3).Should().BeFalse();
        document.Remove(TuneLayer.Bass, 4, 15).Should().BeTrue();

        document.Notes.Should().BeEmpty();
        document.Undo();
        document.Notes.Should().HaveCount(1);
    }

    [Test]
    public void Copy_first_half_cuts_at_the_middle_and_replaces_bars_5_to_8()
    {
        var document = Document(
            new TuneNote(TuneLayer.Melody, 1, 0, 4),
            new TuneNote(TuneLayer.Melody, 2, 30, 6),
            new TuneNote(TuneLayer.Melody, 5, 40, 2),
            new TuneNote(TuneLayer.Bass, 0, 40, 2));

        document.CopyFirstHalf(TuneLayer.Melody);

        document.Notes.Should().BeEquivalentTo(
        [
            new TuneNote(TuneLayer.Melody, 1, 0, 4),
            new TuneNote(TuneLayer.Melody, 2, 30, 2),
            new TuneNote(TuneLayer.Melody, 1, 32, 4),
            new TuneNote(TuneLayer.Melody, 2, 62, 2),
            new TuneNote(TuneLayer.Bass, 0, 40, 2)
        ]);
        document.Undo().Should().BeTrue();
        document.Notes.Should().HaveCount(4);
    }

    [Test]
    public void Clear_empties_one_layer()
    {
        var document = Document(new TuneNote(TuneLayer.Melody, 1, 0, 4), new TuneNote(TuneLayer.Drums, 0, 0, 1));

        document.Clear(TuneLayer.Melody);

        document.Notes.Should().Equal(new TuneNote(TuneLayer.Drums, 0, 0, 1));
    }

    [Test]
    public void Undo_keeps_fifty_steps()
    {
        var document = new TuneDocument();

        for (var column = 0; column < 51; column++)
            Place(document, TuneLayer.Melody, 0, column);

        var undone = 0;

        while (document.Undo())
            undone++;

        undone.Should().Be(TuneDocument.UNDO_LIMIT);
        document.Notes.Should().HaveCount(1);
    }

    [Test]
    public void A_new_edit_clears_redo()
    {
        var document = new TuneDocument();
        Place(document, TuneLayer.Melody, 0, 0);
        document.Undo();
        document.CanRedo.Should().BeTrue();

        Place(document, TuneLayer.Melody, 1, 0);

        document.CanRedo.Should().BeFalse();
    }

    [Test]
    public void A_scale_change_undoes_and_redoes()
    {
        var document = new TuneDocument();

        document.SetScale(TuneScale.Desert);
        document.SetSpeed(TuneSpeed.Quick);
        document.SetInstrument(TuneInstrument.Bells);

        document.Undo();
        document.Undo();
        document.Scale.Should().Be(TuneScale.Desert);
        document.Speed.Should().Be(TuneSpeed.Steady);
        document.Undo();
        document.Scale.Should().Be(TuneScale.Major);
        document.Redo().Should().BeTrue();
        document.Scale.Should().Be(TuneScale.Desert);
    }

    [Test]
    public void Setting_the_same_value_records_nothing()
    {
        var document = new TuneDocument();

        document.SetScale(TuneScale.Major);

        document.CanUndo.Should().BeFalse();
        document.IsDirty.Should().BeFalse();
    }

    [Test]
    public void Counts_cover_melody_notes_rows_and_all_notes()
    {
        var document = Document(
            new TuneNote(TuneLayer.Melody, 1, 0, 1),
            new TuneNote(TuneLayer.Melody, 1, 4, 1),
            new TuneNote(TuneLayer.Melody, 6, 8, 1),
            new TuneNote(TuneLayer.Bass, 6, 8, 1),
            new TuneNote(TuneLayer.Drums, 0, 8, 1));

        document.MelodyNotes.Should().Be(3);
        document.MelodyRows.Should().Be(2);
        document.NoteCount.Should().Be(5);
    }

    [Test]
    public void Load_clears_undo_and_the_dirty_flag()
    {
        var document = new TuneDocument();
        Place(document, TuneLayer.Melody, 0, 0);

        document.Load(TuneData.Empty);

        document.CanUndo.Should().BeFalse();
        document.IsDirty.Should().BeFalse();
        document.Notes.Should().BeEmpty();
    }

    [Test]
    public void Edits_wait_while_a_note_is_being_placed()
    {
        var document = new TuneDocument();
        document.BeginPlace(TuneLayer.Melody, 0, 0);

        document.Undo().Should().BeFalse();
        document.Remove(TuneLayer.Melody, 0, 0).Should().BeFalse();
        document.BeginPlace(TuneLayer.Melody, 1, 0).Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run to see them fail.** Run the `TuneScalesTests` filter.
  Expected: build errors, because `TuneScales`, `TuneData`, `TuneNote` and `TuneDocument` don't exist.

- [ ] **Step 3: Write `TuneNote` and `TuneData`.** Create `Chaos.Client/ViewModel/College/TuneNote.cs`:

```csharp
using Chaos.DarkAges.Definitions;

namespace Chaos.Client.ViewModel.College;

/// <summary>One note of a College tune: a row of a layer, from a start column for a number of columns.</summary>
public readonly record struct TuneNote(TuneLayer Layer, int Row, int Start, int Length)
{
    public int End => Start + Length;

    public bool Covers(TuneLayer layer, int row, int column) => (Layer == layer) && (Row == row) && (column >= Start) && (column < End);

    /// <summary>Inside the grid: a known layer and row, within the 64 columns, and drum notes one column long.</summary>
    public bool IsInGrid
        => Enum.IsDefined(Layer)
           && (Row >= 0)
           && (Row < CollegeProtocol.TuneRows(Layer))
           && (Start >= 0)
           && (Length >= 1)
           && (End <= CollegeProtocol.TUNE_STEPS)
           && ((Layer != TuneLayer.Drums) || (Length == 1));
}
```

Create `Chaos.Client/ViewModel/College/TuneData.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;

namespace Chaos.Client.ViewModel.College;

/// <summary>A finished tune that never changes: what is saved, sent, drawn read-only and played.</summary>
public sealed record TuneData(TuneScale Scale, TuneSpeed Speed, TuneInstrument Instrument, IReadOnlyList<TuneNote> Notes)
{
    public static readonly TuneData Empty = new(TuneScale.Major, TuneSpeed.Steady, TuneInstrument.Lute, []);

    /// <summary>Notes outside the grid are dropped, and bytes after the last whole note are ignored.</summary>
    public static TuneData From(CollegeBlockInfo block)
    {
        var notes = new List<TuneNote>();
        var bytes = block.Notes;

        for (var at = 0; at + CollegeProtocol.TUNE_NOTE_BYTES <= bytes.Length; at += CollegeProtocol.TUNE_NOTE_BYTES)
        {
            var note = new TuneNote((TuneLayer)bytes[at], bytes[at + 1], bytes[at + 2], bytes[at + 3]);

            if (note.IsInGrid)
                notes.Add(note);
        }

        return new TuneData(block.Scale, block.Speed, block.Instrument, notes);
    }

    public CollegeBlockInfo ToBlock()
    {
        var bytes = new byte[Notes.Count * CollegeProtocol.TUNE_NOTE_BYTES];

        for (var i = 0; i < Notes.Count; i++)
        {
            var at = i * CollegeProtocol.TUNE_NOTE_BYTES;
            bytes[at] = (byte)Notes[i].Layer;
            bytes[at + 1] = (byte)Notes[i].Row;
            bytes[at + 2] = (byte)Notes[i].Start;
            bytes[at + 3] = (byte)Notes[i].Length;
        }

        return new CollegeBlockInfo
        {
            Kind = CollegeBlockKind.Tune,
            Scale = Scale,
            Speed = Speed,
            Instrument = Instrument,
            Notes = bytes
        };
    }
}
```

- [ ] **Step 4: Write `TuneScales`.** Create `Chaos.Client/ViewModel/College/TuneScales.cs`:

```csharp
using Chaos.DarkAges.Definitions;

namespace Chaos.Client.ViewModel.College;

/// <summary>
///     Where a tune row sits in pitch. Rows are steps of the tune's scale, so changing the scale moves every note into
///     the new scale.
/// </summary>
public static class TuneScales
{
    public const int MELODY_BASE = 60;
    public const int BASS_BASE = 36;

    private static readonly int[] Major = [0, 2, 4, 5, 7, 9, 11];
    private static readonly int[] Minor = [0, 2, 3, 5, 7, 8, 10];
    private static readonly int[] FiveNote = [0, 2, 4, 7, 9];
    private static readonly int[] Dorian = [0, 2, 3, 5, 7, 9, 10];
    private static readonly int[] Mixolydian = [0, 2, 4, 5, 7, 9, 10];
    private static readonly int[] Desert = [0, 2, 3, 5, 7, 8, 11];

    private static readonly string[] NoteNames = ["C", "C#", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"];
    private static readonly string[] DrumNames = ["Drum", "Tamb", "Wood"];

    public static IReadOnlyList<int> StepsOf(TuneScale scale)
        => scale switch
        {
            TuneScale.Minor      => Minor,
            TuneScale.FiveNote   => FiveNote,
            TuneScale.Dorian     => Dorian,
            TuneScale.Mixolydian => Mixolydian,
            TuneScale.Desert     => Desert,
            _                    => Major
        };

    /// <summary>The row's MIDI note number; drums have no pitch and give -1.</summary>
    public static int Midi(TuneScale scale, TuneLayer layer, int row)
    {
        if (layer == TuneLayer.Drums)
            return -1;

        var steps = StepsOf(scale);
        var root = layer == TuneLayer.Melody ? MELODY_BASE : BASS_BASE;

        return root + (12 * (row / steps.Count)) + steps[row % steps.Count];
    }

    public static double Frequency(int midi) => 440.0 * Math.Pow(2, (midi - 69) / 12.0);

    public static string RowName(TuneScale scale, TuneLayer layer, int row)
    {
        if (layer == TuneLayer.Drums)
            return (row >= 0) && (row < DrumNames.Length) ? DrumNames[row] : string.Empty;

        var midi = Midi(scale, layer, row);

        return NoteNames[midi % 12] + ((midi / 12) - 1);
    }

    /// <summary>The scale's home note (C) on each octave.</summary>
    public static bool IsHome(TuneScale scale, int row) => row % StepsOf(scale).Count == 0;

    /// <summary>One column is half a beat.</summary>
    public static double StepSeconds(TuneSpeed speed) => 60.0 / CollegeProtocol.TuneBpm(speed) / 2;
}
```

- [ ] **Step 5: Write `TuneDocument`.** Create `Chaos.Client/ViewModel/College/TuneDocument.cs`:

```csharp
using Chaos.DarkAges.Definitions;

namespace Chaos.Client.ViewModel.College;

/// <summary>
///     The composer's editable tune. Every edit keeps the notes inside the grid with no overlaps in a row, and undoes
///     as one step; a placed note and its drag are one step. Up to <see cref="UNDO_LIMIT" /> steps are kept.
/// </summary>
public sealed class TuneDocument
{
    public const int UNDO_LIMIT = 50;
    private const int HALF = CollegeProtocol.TUNE_STEPS / 2;

    private readonly List<TuneNote> NoteList = [];
    private readonly List<TuneData> RedoSteps = [];
    private readonly List<TuneData> UndoSteps = [];
    private int PlacingIndex = -1;
    private TuneData? PlaceStart;

    public TuneScale Scale { get; private set; } = TuneData.Empty.Scale;
    public TuneSpeed Speed { get; private set; } = TuneData.Empty.Speed;
    public TuneInstrument Instrument { get; private set; } = TuneData.Empty.Instrument;
    public IReadOnlyList<TuneNote> Notes => NoteList;

    /// <summary>Goes up on every change, including each drag step, so a grid knows when to redraw.</summary>
    public int Version { get; private set; }

    /// <summary>Raised each time <see cref="Version" /> goes up.</summary>
    public event Action? Changed;

    /// <summary>Changed since it was loaded or last saved.</summary>
    public bool IsDirty { get; private set; }

    public bool CanUndo => UndoSteps.Count > 0;
    public bool CanRedo => RedoSteps.Count > 0;
    public bool IsPlacing => PlacingIndex >= 0;

    public int NoteCount => NoteList.Count;
    public int MelodyNotes => NoteList.Count(n => n.Layer == TuneLayer.Melody);
    public int MelodyRows => NoteList.Where(n => n.Layer == TuneLayer.Melody).Select(n => n.Row).Distinct().Count();

    public TuneNote? NoteAt(TuneLayer layer, int row, int column)
    {
        var index = IndexAt(layer, row, column);

        return index < 0 ? null : NoteList[index];
    }

    /// <summary>Starts a one-column note on an empty cell. False if the cell is filled, outside the grid, or the tune is full.</summary>
    public bool BeginPlace(TuneLayer layer, int row, int column)
    {
        var note = new TuneNote(layer, row, column, 1);

        if (IsPlacing || !note.IsInGrid || (IndexAt(layer, row, column) >= 0) || (NoteList.Count >= CollegeProtocol.MAX_TUNE_NOTES))
            return false;

        PlaceStart = Snapshot();
        NoteList.Add(note);
        PlacingIndex = NoteList.Count - 1;
        Bump();

        return true;
    }

    /// <summary>Lengthens the note being placed to reach a column, up to the next note in its row or the grid's end.</summary>
    public void DragTo(int column)
    {
        if (!IsPlacing)
            return;

        var note = NoteList[PlacingIndex];

        if (note.Layer == TuneLayer.Drums)
            return;

        var limit = NoteList.Where(n => (n.Layer == note.Layer) && (n.Row == note.Row) && (n.Start > note.Start))
                            .Select(n => n.Start)
                            .DefaultIfEmpty(CollegeProtocol.TUNE_STEPS)
                            .Min();

        var length = Math.Clamp(column - note.Start + 1, 1, limit - note.Start);

        if (length == note.Length)
            return;

        NoteList[PlacingIndex] = note with { Length = length };
        Bump();
    }

    public void EndPlace()
    {
        if (!IsPlacing)
            return;

        PlacingIndex = -1;
        Commit(PlaceStart!);
        PlaceStart = null;
    }

    public bool Remove(TuneLayer layer, int row, int column)
    {
        var index = IndexAt(layer, row, column);

        if (IsPlacing || (index < 0))
            return false;

        var before = Snapshot();
        NoteList.RemoveAt(index);
        Commit(before);

        return true;
    }

    /// <summary>Replaces bars 5-8 of one layer with a copy of bars 1-4. A note crossing the middle is cut there first.</summary>
    public void CopyFirstHalf(TuneLayer layer)
    {
        if (IsPlacing)
            return;

        var before = Snapshot();
        NoteList.RemoveAll(n => (n.Layer == layer) && (n.Start >= HALF));

        for (var i = 0; i < NoteList.Count; i++)
            if ((NoteList[i].Layer == layer) && (NoteList[i].End > HALF))
                NoteList[i] = NoteList[i] with { Length = HALF - NoteList[i].Start };

        var copies = NoteList.Where(n => n.Layer == layer).Select(n => n with { Start = n.Start + HALF }).ToList();

        foreach (var copy in copies)
        {
            if (NoteList.Count >= CollegeProtocol.MAX_TUNE_NOTES)
                break;

            NoteList.Add(copy);
        }

        Commit(before);
    }

    public void Clear(TuneLayer layer)
    {
        if (IsPlacing)
            return;

        var before = Snapshot();
        NoteList.RemoveAll(n => n.Layer == layer);
        Commit(before);
    }

    public void SetScale(TuneScale scale) => Change(() => Scale = scale);

    public void SetSpeed(TuneSpeed speed) => Change(() => Speed = speed);

    public void SetInstrument(TuneInstrument instrument) => Change(() => Instrument = instrument);

    public bool Undo()
    {
        if (IsPlacing || !CanUndo)
            return false;

        RedoSteps.Add(Snapshot());
        Restore(UndoSteps[^1]);
        UndoSteps.RemoveAt(UndoSteps.Count - 1);
        IsDirty = true;

        return true;
    }

    public bool Redo()
    {
        if (IsPlacing || !CanRedo)
            return false;

        UndoSteps.Add(Snapshot());
        Restore(RedoSteps[^1]);
        RedoSteps.RemoveAt(RedoSteps.Count - 1);
        IsDirty = true;

        return true;
    }

    public TuneData Snapshot() => new(Scale, Speed, Instrument, NoteList.ToArray());

    /// <summary>Replaces the whole tune, clearing undo and the dirty flag.</summary>
    public void Load(TuneData data)
    {
        PlacingIndex = -1;
        PlaceStart = null;
        Restore(data);
        UndoSteps.Clear();
        RedoSteps.Clear();
        IsDirty = false;
    }

    public void MarkClean() => IsDirty = false;

    private int IndexAt(TuneLayer layer, int row, int column) => NoteList.FindIndex(n => n.Covers(layer, row, column));

    private void Bump()
    {
        Version++;
        Changed?.Invoke();
    }

    private void Change(Action apply)
    {
        if (IsPlacing)
            return;

        var before = Snapshot();
        apply();
        Commit(before);
    }

    private void Commit(TuneData before)
    {
        if ((before.Scale == Scale) && (before.Speed == Speed) && (before.Instrument == Instrument) && before.Notes.SequenceEqual(NoteList))
            return;

        UndoSteps.Add(before);

        if (UndoSteps.Count > UNDO_LIMIT)
            UndoSteps.RemoveAt(0);

        RedoSteps.Clear();
        IsDirty = true;
        Bump();
    }

    private void Restore(TuneData data)
    {
        Scale = data.Scale;
        Speed = data.Speed;
        Instrument = data.Instrument;
        NoteList.Clear();
        NoteList.AddRange(data.Notes);
        Bump();
    }
}
```

- [ ] **Step 6: Run the tests.** Run the `TuneScalesTests`, `TuneDataTests` and `TuneDocumentTests` filters. Expected: all pass.

```json:metadata
{"files": ["Chaos.Client/ViewModel/College/TuneNote.cs", "Chaos.Client/ViewModel/College/TuneData.cs", "Chaos.Client/ViewModel/College/TuneScales.cs", "Chaos.Client/ViewModel/College/TuneDocument.cs", "Tests/Chaos.Client.Tests/College/TuneScalesTests.cs", "Tests/Chaos.Client.Tests/College/TuneDataTests.cs", "Tests/Chaos.Client.Tests/College/TuneDocumentTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\\Users\\Michael\\Documents\\GitHub\\worktrees\\college-music-server -- --treenode-filter \"/*/*/TuneDocumentTests/*\"", "acceptanceCriteria": ["Midi: melody base 60, bass 36, every scale, Five-note 3 octaves, drums -1", "RowName C4/Eb4/B2 and Drum/Tamb/Wood", "TuneData round-trips; From drops off-grid notes and trailing bytes", "drag stops at next note and column 64; drums stay 1 long", "filled cell and 513th note refused", "Copy bars 1-4 to 5-8 cuts at the middle, replaces bars 5-8, other layers untouched", "50-step undo, redo cleared by new edit, scale change undoes", "MelodyNotes, MelodyRows, NoteCount correct", "Changed raised on every Version bump"], "modelTier": "mechanical"}
```

---

### Task 6: Client sound maker: TuneSynth and TuneWav (client repo)

**Goal:** A tune becomes 16-bit stereo samples at 48 kHz, made in code from modelled instruments. The same tune always
gives the same samples. The samples can be wrapped as WAV bytes for the mixer.

**Files:**
- Create: `Chaos.Client/Systems/College/TuneSynth.cs`
- Create: `Chaos.Client/Systems/College/TuneWav.cs`
- Test: `Tests/Chaos.Client.Tests/College/TuneSynthTests.cs`, `Tests/Chaos.Client.Tests/College/TuneWavTests.cs`

**Acceptance Criteria:**
- [ ] For every speed, the buffer lasts 64 columns plus 1.5 seconds: `LengthInFrames(Quick)` is 648,000 and `LengthInFrames(Slow)` is 1,224,000.
- [ ] An empty tune renders silence.
- [ ] The same tune renders the same samples twice.
- [ ] With all 30 melody and bass rows sounding at once, no sample goes past 90% of the 16-bit range, and the tune is not silent.
- [ ] A flute note an octave higher crosses zero about twice as often.
- [ ] `RenderNote` gives a short buffer that isn't silent, for every instrument, the bass and each drum.
- [ ] `TuneWav.Wrap` writes a 44-byte header (PCM, 2 channels, 48,000 Hz, 16 bits) followed by the samples.

**Verify:** `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\college-music-server -- --treenode-filter "/*/*/TuneSynthTests/*"` -> all pass; same for `TuneWavTests`

**Steps:**

- [ ] **Step 1: Write the failing tests.** Create `Tests/Chaos.Client.Tests/College/TuneSynthTests.cs`:

```csharp
using Chaos.Client.Systems.College;
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class TuneSynthTests
{
    private static TuneData Tune(TuneSpeed speed, TuneInstrument instrument, params TuneNote[] notes)
        => new(TuneScale.Major, speed, instrument, notes);

    private static int ZeroCrossings(short[] stereo, double fromSeconds, double toSeconds)
    {
        var from = (int)(fromSeconds * TuneSynth.SAMPLE_RATE);
        var to = (int)(toSeconds * TuneSynth.SAMPLE_RATE);
        var crossings = 0;

        for (var frame = from + 1; frame < to; frame++)
            if ((stereo[(frame - 1) * 2] < 0) != (stereo[frame * 2] < 0))
                crossings++;

        return crossings;
    }

    [Test]
    public void A_tune_lasts_64_columns_plus_the_ring_out()
    {
        TuneSynth.LengthInFrames(TuneSpeed.Quick).Should().Be(648_000);
        TuneSynth.LengthInFrames(TuneSpeed.Slow).Should().Be(1_224_000);

        foreach (var speed in Enum.GetValues<TuneSpeed>())
            TuneSynth.Render(Tune(speed, TuneInstrument.Lute)).Should().HaveCount(TuneSynth.LengthInFrames(speed) * 2);
    }

    [Test]
    public void An_empty_tune_is_silent()
        => TuneSynth.Render(Tune(TuneSpeed.Quick, TuneInstrument.Lute)).Should().OnlyContain(s => s == 0);

    [Test]
    public void The_same_tune_gives_the_same_samples()
    {
        var tune = Tune(
            TuneSpeed.Quick,
            TuneInstrument.Harp,
            new TuneNote(TuneLayer.Melody, 4, 0, 3),
            new TuneNote(TuneLayer.Bass, 0, 0, 8),
            new TuneNote(TuneLayer.Drums, 0, 0, 1),
            new TuneNote(TuneLayer.Drums, 1, 4, 1),
            new TuneNote(TuneLayer.Drums, 2, 6, 1));

        TuneSynth.Render(tune).Should().Equal(TuneSynth.Render(tune));
    }

    [Test]
    public void Every_row_at_once_stays_inside_the_16_bit_range()
    {
        var notes = new List<TuneNote>();

        for (var row = 0; row < CollegeProtocol.TUNE_PITCH_ROWS; row++)
        {
            notes.Add(new TuneNote(TuneLayer.Melody, row, 0, 64));
            notes.Add(new TuneNote(TuneLayer.Bass, row, 0, 64));
        }

        var samples = TuneSynth.Render(Tune(TuneSpeed.Quick, TuneInstrument.Bells, notes.ToArray()));
        var peak = samples.Max(s => Math.Abs((int)s));

        peak.Should().BeLessThanOrEqualTo((int)Math.Ceiling(0.9 * short.MaxValue));
        peak.Should().BeGreaterThan(10_000);
    }

    [Test]
    public void A_note_an_octave_higher_crosses_zero_twice_as_often()
    {
        var low = TuneSynth.Render(Tune(TuneSpeed.Slow, TuneInstrument.Flute, new TuneNote(TuneLayer.Melody, 0, 0, 4)));
        var high = TuneSynth.Render(Tune(TuneSpeed.Slow, TuneInstrument.Flute, new TuneNote(TuneLayer.Melody, 7, 0, 4)));

        var ratio = ZeroCrossings(high, 0.2, 1.2) / (double)ZeroCrossings(low, 0.2, 1.2);

        ratio.Should().BeInRange(1.9, 2.1);
    }

    [Test]
    public void A_single_note_renders_a_short_sound()
    {
        foreach (var instrument in Enum.GetValues<TuneInstrument>())
        {
            var note = TuneSynth.RenderNote(TuneScale.Major, instrument, TuneLayer.Melody, 7);
            note.Length.Should().BeInRange(2, 3 * TuneSynth.SAMPLE_RATE * 2);
            note.Should().Contain(s => s != 0);
        }

        TuneSynth.RenderNote(TuneScale.Minor, TuneInstrument.Lute, TuneLayer.Bass, 3).Should().Contain(s => s != 0);

        for (var drum = 0; drum < CollegeProtocol.TUNE_DRUM_ROWS; drum++)
            TuneSynth.RenderNote(TuneScale.Major, TuneInstrument.Lute, TuneLayer.Drums, drum).Should().Contain(s => s != 0);
    }

    [Test]
    public void Both_channels_carry_the_same_sound()
    {
        var samples = TuneSynth.RenderNote(TuneScale.Major, TuneInstrument.Lute, TuneLayer.Melody, 2);

        for (var frame = 0; frame < samples.Length / 2; frame++)
            samples[frame * 2].Should().Be(samples[(frame * 2) + 1]);
    }
}
```

Create `Tests/Chaos.Client.Tests/College/TuneWavTests.cs`:

```csharp
using System.Buffers.Binary;
using System.Text;
using Chaos.Client.Systems.College;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class TuneWavTests
{
    [Test]
    public void The_header_describes_16_bit_stereo_at_48_kHz()
    {
        var wav = TuneWav.Wrap([1, -1, 300, -300]);
        var span = wav.AsSpan();

        wav.Should().HaveCount(44 + 8);
        Encoding.ASCII.GetString(wav, 0, 4).Should().Be("RIFF");
        BinaryPrimitives.ReadInt32LittleEndian(span[4..]).Should().Be(36 + 8);
        Encoding.ASCII.GetString(wav, 8, 8).Should().Be("WAVEfmt ");
        BinaryPrimitives.ReadInt32LittleEndian(span[16..]).Should().Be(16);
        BinaryPrimitives.ReadInt16LittleEndian(span[20..]).Should().Be(1);
        BinaryPrimitives.ReadInt16LittleEndian(span[22..]).Should().Be(2);
        BinaryPrimitives.ReadInt32LittleEndian(span[24..]).Should().Be(TuneSynth.SAMPLE_RATE);
        BinaryPrimitives.ReadInt32LittleEndian(span[28..]).Should().Be(TuneSynth.SAMPLE_RATE * 4);
        BinaryPrimitives.ReadInt16LittleEndian(span[32..]).Should().Be(4);
        BinaryPrimitives.ReadInt16LittleEndian(span[34..]).Should().Be(16);
        Encoding.ASCII.GetString(wav, 36, 4).Should().Be("data");
        BinaryPrimitives.ReadInt32LittleEndian(span[40..]).Should().Be(8);
    }

    [Test]
    public void The_samples_follow_the_header_little_endian()
    {
        var wav = TuneWav.Wrap([1, -1, 300, -300]).AsSpan(44);

        BinaryPrimitives.ReadInt16LittleEndian(wav).Should().Be(1);
        BinaryPrimitives.ReadInt16LittleEndian(wav[2..]).Should().Be(-1);
        BinaryPrimitives.ReadInt16LittleEndian(wav[4..]).Should().Be(300);
        BinaryPrimitives.ReadInt16LittleEndian(wav[6..]).Should().Be(-300);
    }
}
```

- [ ] **Step 2: Run to see them fail.** Run the `TuneSynthTests` filter. Expected: build errors, because `TuneSynth` and `TuneWav` don't exist.

- [ ] **Step 3: Write `TuneWav`.** Create `Chaos.Client/Systems/College/TuneWav.cs`:

```csharp
using System.Buffers.Binary;

namespace Chaos.Client.Systems.College;

/// <summary>Wraps rendered tune samples in a WAV header, so the mixer loads them like any sound effect.</summary>
public static class TuneWav
{
    private const int HEADER_BYTES = 44;
    private const short CHANNELS = 2;
    private const short BITS = 16;

    public static byte[] Wrap(short[] stereoSamples)
    {
        var dataBytes = stereoSamples.Length * 2;
        var bytes = new byte[HEADER_BYTES + dataBytes];
        var span = bytes.AsSpan();

        "RIFF"u8.CopyTo(span);
        BinaryPrimitives.WriteInt32LittleEndian(span[4..], 36 + dataBytes);
        "WAVE"u8.CopyTo(span[8..]);
        "fmt "u8.CopyTo(span[12..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[16..], 16);
        BinaryPrimitives.WriteInt16LittleEndian(span[20..], 1);
        BinaryPrimitives.WriteInt16LittleEndian(span[22..], CHANNELS);
        BinaryPrimitives.WriteInt32LittleEndian(span[24..], TuneSynth.SAMPLE_RATE);
        BinaryPrimitives.WriteInt32LittleEndian(span[28..], TuneSynth.SAMPLE_RATE * CHANNELS * (BITS / 8));
        BinaryPrimitives.WriteInt16LittleEndian(span[32..], CHANNELS * (BITS / 8));
        BinaryPrimitives.WriteInt16LittleEndian(span[34..], BITS);
        "data"u8.CopyTo(span[36..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[40..], dataBytes);

        for (var i = 0; i < stereoSamples.Length; i++)
            BinaryPrimitives.WriteInt16LittleEndian(span[(HEADER_BYTES + (i * 2))..], stereoSamples[i]);

        return bytes;
    }
}
```

- [ ] **Step 4: Write `TuneSynth`.** The instruments port the browser mockups in
  `.superpowers/brainstorm/1236794-1791241737/content/window-layout.html` (in client `main`). The mockup's volumes,
  decays and timings are kept. Create `Chaos.Client/Systems/College/TuneSynth.cs`:

```csharp
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;

namespace Chaos.Client.Systems.College;

/// <summary>
///     Turns a College tune into 16-bit stereo samples at the mixer's rate. The instruments are modelled in code, not
///     recorded: plucked strings use the Karplus-Strong method (a burst of noise fed through a short delay line that
///     softens it on each pass). Every random part is seeded from the note's layer, row and start, so a tune always
///     gives exactly the same samples on every PC.
/// </summary>
public static class TuneSynth
{
    public const int SAMPLE_RATE = 48000;
    public const double TAIL_SECONDS = 1.5;

    private const float PEAK = 0.9f;
    private const double PREVIEW_SECONDS = 0.25;
    private const double TAU = Math.PI * 2;

    public static int LengthInFrames(TuneSpeed speed)
        => (int)Math.Round(((CollegeProtocol.TUNE_STEPS * TuneScales.StepSeconds(speed)) + TAIL_SECONDS) * SAMPLE_RATE);

    public static short[] Render(TuneData tune)
    {
        var step = TuneScales.StepSeconds(tune.Speed);
        var mix = new float[LengthInFrames(tune.Speed)];

        foreach (var note in tune.Notes)
        {
            var at = (int)Math.Round(note.Start * step * SAMPLE_RATE);
            var rng = new Noise(Seed(note));
            AddNote(mix, at, tune.Scale, tune.Instrument, note.Layer, note.Row, note.Length * step, ref rng);
        }

        return ToStereo(mix);
    }

    /// <summary>One short note on its own, for the preview when a note is placed.</summary>
    public static short[] RenderNote(TuneScale scale, TuneInstrument instrument, TuneLayer layer, int row)
    {
        var mix = new float[(int)Math.Ceiling(VoiceSeconds(instrument, layer, row, PREVIEW_SECONDS) * SAMPLE_RATE)];
        var rng = new Noise(Seed(new TuneNote(layer, row, 0, 1)));
        AddNote(mix, 0, scale, instrument, layer, row, PREVIEW_SECONDS, ref rng);

        return ToStereo(mix);
    }

    private static double VoiceSeconds(TuneInstrument instrument, TuneLayer layer, int row, double held)
        => layer switch
        {
            TuneLayer.Drums => row switch
            {
                0 => 0.35,
                1 => 0.2,
                _ => 0.07
            },
            TuneLayer.Bass => held + 0.4,
            _ => instrument switch
            {
                TuneInstrument.Harp  => Math.Max(held, 0.9) + 0.4,
                TuneInstrument.Flute => held + 0.3,
                TuneInstrument.Bells => 2.0,
                _                    => held + 0.4
            }
        };

    private static uint Seed(TuneNote note)
        => ((uint)((((int)note.Layer * 64) + note.Row) * 64 + note.Start) * 2654435761u) ^ 0x5BD1E995u;

    private static void AddNote(
        float[] mix,
        int at,
        TuneScale scale,
        TuneInstrument instrument,
        TuneLayer layer,
        int row,
        double held,
        ref Noise rng)
    {
        if (layer == TuneLayer.Drums)
        {
            AddDrum(mix, at, row, ref rng);

            return;
        }

        var frequency = TuneScales.Frequency(TuneScales.Midi(scale, layer, row));

        if (layer == TuneLayer.Bass)
        {
            AddPluck(mix, at, frequency, held, 0.7, 0.997, 0.25, 3, ref rng);

            return;
        }

        switch (instrument)
        {
            case TuneInstrument.Harp:
                AddPluck(mix, at, frequency, Math.Max(held, 0.9), 0.4, 0.9985, 0.35, 3, ref rng);

                break;
            case TuneInstrument.Flute:
                AddFlute(mix, at, frequency, held, ref rng);

                break;
            case TuneInstrument.Bells:
                AddBells(mix, at, frequency);

                break;
            default:
                AddPluck(mix, at, frequency, held, 0.5, 0.996, 0.55, 2, ref rng);

                break;
        }
    }

    private static void AddPluck(
        float[] mix,
        int at,
        double frequency,
        double held,
        double volume,
        double decay,
        double brightness,
        double ringSeconds,
        ref Noise rng)
    {
        var length = (int)(Math.Min(ringSeconds, held + 0.4) * SAMPLE_RATE);
        var period = Math.Max(2, (int)Math.Round(SAMPLE_RATE / frequency));
        var line = new float[length];
        var previous = 0f;

        for (var i = 0; (i < period) && (i < length); i++)
        {
            previous += (float)(brightness * (rng.NextSigned() - previous));
            line[i] = previous;
        }

        if (period < length)
            line[period] = (float)(line[0] * decay);

        for (var i = period + 1; i < length; i++)
            line[i] = (float)(decay * 0.5 * (line[i - period] + line[i - period - 1]));

        var envelope = new Envelope(volume, 0.002, Math.Max(0, held - 0.05), 0.25);

        for (var i = 0; (i < length) && (at + i < mix.Length); i++)
            mix[at + i] += line[i] * envelope.Next();
    }

    private static void AddFlute(float[] mix, int at, double frequency, double held, ref Noise rng)
    {
        var hold = Math.Max(0, held - 0.12);
        var tone = new Envelope(0.16, 0.07, hold, 0.12);
        var overtone = new Envelope(0.025, 0.07, hold, 0.12);
        var length = (int)((held + 0.3) * SAMPLE_RATE);
        double phase = 0, overtonePhase = 0;

        for (var i = 0; (i < length) && (at + i < mix.Length); i++)
        {
            var seconds = i / (double)SAMPLE_RATE;
            var vibrato = seconds >= 0.15 ? frequency * 0.006 * Math.Sin(TAU * 5.2 * (seconds - 0.15)) : 0;
            phase += TAU * (frequency + vibrato) / SAMPLE_RATE;
            overtonePhase += TAU * frequency * 2 / SAMPLE_RATE;
            mix[at + i] += (float)((Math.Sin(phase) * tone.Next()) + (Math.Sin(overtonePhase) * overtone.Next()));
        }

        AddNoise(mix, at, Biquad.BandPass(frequency * 2, 2), 0.03, Math.Min(0.12, held), ref rng);
    }

    private static void AddBells(float[] mix, int at, double frequency)
    {
        ReadOnlySpan<(double Ratio, double Volume)> partials = [(1, 0.14), (2.76, 0.05), (5.4, 0.025)];
        var length = 2 * SAMPLE_RATE;

        foreach (var (ratio, volume) in partials)
        {
            var envelope = new Envelope(volume, 0.002, 0, 1.6 / ratio);
            var step = TAU * frequency * ratio / SAMPLE_RATE;

            for (var i = 0; (i < length) && (at + i < mix.Length); i++)
                mix[at + i] += (float)(Math.Sin(step * i) * envelope.Next());
        }
    }

    private static void AddDrum(float[] mix, int at, int row, ref Noise rng)
    {
        switch (row)
        {
            case 0:
                AddSweep(mix, at, 95, 55, 0.25, new Envelope(0.55, 0.003, 0, 0.3), 0.35);
                AddNoise(mix, at, Biquad.LowPass(400, 0.707), 0.15, 0.05, ref rng);

                break;
            case 1:
                for (var burst = 0; burst < 3; burst++)
                    AddNoise(mix, at + (int)(burst * 0.012 * SAMPLE_RATE), Biquad.BandPass(6500, 3), 0.12, 0.09, ref rng);

                break;
            default:
                AddSweep(mix, at, 1500, 1500, 0.06, new Envelope(0.12, 0.001, 0, 0.04), 0.06);
                AddNoise(mix, at, Biquad.BandPass(2500, 4), 0.04, 0.02, ref rng);

                break;
        }
    }

    /// <summary>A sine whose pitch falls from one frequency to another along an exponential curve.</summary>
    private static void AddSweep(float[] mix, int at, double from, double to, double sweepSeconds, Envelope envelope, double seconds)
    {
        var length = (int)(seconds * SAMPLE_RATE);
        double phase = 0;

        for (var i = 0; (i < length) && (at + i < mix.Length); i++)
        {
            var progress = Math.Min(1, i / (sweepSeconds * SAMPLE_RATE));
            phase += TAU * from * Math.Pow(to / from, progress) / SAMPLE_RATE;
            mix[at + i] += (float)(Math.Sin(phase) * envelope.Next());
        }
    }

    private static void AddNoise(float[] mix, int at, Biquad filter, double volume, double seconds, ref Noise rng)
    {
        var envelope = new Envelope(volume, 0.001, 0, seconds);
        var length = (int)((seconds + 0.05) * SAMPLE_RATE);

        for (var i = 0; (i < length) && (at + i < mix.Length); i++)
            mix[at + i] += filter.Process(rng.NextSigned()) * envelope.Next();
    }

    private static short[] ToStereo(float[] mix)
    {
        var peak = 0f;

        foreach (var sample in mix)
            peak = Math.Max(peak, Math.Abs(sample));

        var scale = (peak > PEAK ? PEAK / peak : 1f) * short.MaxValue;
        var stereo = new short[mix.Length * 2];

        for (var i = 0; i < mix.Length; i++)
        {
            var value = (short)Math.Clamp(Math.Round(mix[i] * scale), short.MinValue, short.MaxValue);
            stereo[i * 2] = value;
            stereo[(i * 2) + 1] = value;
        }

        return stereo;
    }

    /// <summary>
    ///     Gain over time, as Web Audio ramps it in the mockups: a straight rise from near silence to the peak, a hold,
    ///     then an exponential fall back to near silence, then nothing.
    /// </summary>
    private struct Envelope
    {
        private const double FLOOR = 0.0001;

        private readonly int AttackEnd;
        private readonly double AttackStep;
        private readonly int HoldEnd;
        private readonly double Peak;
        private readonly int ReleaseEnd;
        private readonly double ReleaseFactor;
        private double Gain;
        private int Index;

        public Envelope(double peak, double attack, double hold, double release)
        {
            Peak = peak;
            AttackEnd = Math.Max(1, (int)(attack * SAMPLE_RATE));
            HoldEnd = AttackEnd + (int)(hold * SAMPLE_RATE);
            ReleaseEnd = HoldEnd + Math.Max(1, (int)(release * SAMPLE_RATE));
            AttackStep = (peak - FLOOR) / AttackEnd;
            ReleaseFactor = Math.Pow(FLOOR / peak, 1.0 / (ReleaseEnd - HoldEnd));
            Gain = FLOOR;
            Index = 0;
        }

        public float Next()
        {
            if (Index < AttackEnd)
                Gain = FLOOR + (AttackStep * (Index + 1));
            else if (Index < HoldEnd)
                Gain = Peak;
            else if (Index < ReleaseEnd)
                Gain *= ReleaseFactor;
            else
                Gain = 0;

            Index++;

            return (float)Gain;
        }
    }

    /// <summary>A two-pole filter (the RBJ audio-cookbook forms) for shaping noise.</summary>
    private struct Biquad
    {
        private readonly float A1, A2, B0, B1, B2;
        private float X1, X2, Y1, Y2;

        private Biquad(double b0, double b1, double b2, double a0, double a1, double a2)
        {
            B0 = (float)(b0 / a0);
            B1 = (float)(b1 / a0);
            B2 = (float)(b2 / a0);
            A1 = (float)(a1 / a0);
            A2 = (float)(a2 / a0);
            X1 = X2 = Y1 = Y2 = 0;
        }

        public static Biquad LowPass(double frequency, double q)
        {
            var (cos, alpha) = Shape(frequency, q);

            return new Biquad((1 - cos) / 2, 1 - cos, (1 - cos) / 2, 1 + alpha, -2 * cos, 1 - alpha);
        }

        public static Biquad BandPass(double frequency, double q)
        {
            var (cos, alpha) = Shape(frequency, q);

            return new Biquad(alpha, 0, -alpha, 1 + alpha, -2 * cos, 1 - alpha);
        }

        public float Process(float x)
        {
            var y = (B0 * x) + (B1 * X1) + (B2 * X2) - (A1 * Y1) - (A2 * Y2);
            X2 = X1;
            X1 = x;
            Y2 = Y1;
            Y1 = y;

            return y;
        }

        private static (double Cos, double Alpha) Shape(double frequency, double q)
        {
            var w0 = TAU * Math.Min(frequency, SAMPLE_RATE * 0.45) / SAMPLE_RATE;

            return (Math.Cos(w0), Math.Sin(w0) / (2 * q));
        }
    }

    /// <summary>A small fixed random generator (xorshift), so noise is the same on every PC and .NET version.</summary>
    private struct Noise(uint seed)
    {
        private uint State = seed == 0 ? 0x9E3779B9u : seed;

        public float NextSigned()
        {
            State ^= State << 13;
            State ^= State >> 17;
            State ^= State << 5;

            return ((State / (float)uint.MaxValue) * 2f) - 1f;
        }
    }
}
```

- [ ] **Step 5: Run the tests.** Run the `TuneSynthTests` and `TuneWavTests` filters. Expected: all pass.
  - If `Every_row_at_once_stays_inside_the_16_bit_range` reports a peak of 10,000 or less, the scaling is wrong. Check
    `ToStereo`; don't loosen the test.
  - If the zero-crossing ratio falls outside 1.9-2.1, check that the flute's vibrato starts at 0.15 s and has a depth
    of `frequency * 0.006`.

- [ ] **Step 6: Listen once.** This is a manual check, not a test. Write a scratch WAV and play it in Windows:

```csharp
File.WriteAllBytes(
    Path.Combine(Path.GetTempPath(), "tune-check.wav"),
    TuneWav.Wrap(TuneSynth.Render(new TuneData(TuneScale.Dorian, TuneSpeed.Lively, TuneInstrument.Lute,
        [new TuneNote(TuneLayer.Melody, 4, 0, 2), new TuneNote(TuneLayer.Melody, 5, 2, 1), new TuneNote(TuneLayer.Melody, 6, 3, 1),
         new TuneNote(TuneLayer.Bass, 0, 0, 8), new TuneNote(TuneLayer.Drums, 0, 0, 1), new TuneNote(TuneLayer.Drums, 1, 4, 1),
         new TuneNote(TuneLayer.Drums, 2, 2, 1), new TuneNote(TuneLayer.Drums, 2, 6, 1)]))));
```

Run it from a throwaway test, and delete that test before moving on. The sound should be close to the mockup: a
plucked lute over a low string, a frame drum, a tambourine and a wood block. Report anything harsh or clicking to the
coordinator instead of retuning the numbers.

```json:metadata
{"files": ["Chaos.Client/Systems/College/TuneSynth.cs", "Chaos.Client/Systems/College/TuneWav.cs", "Tests/Chaos.Client.Tests/College/TuneSynthTests.cs", "Tests/Chaos.Client.Tests/College/TuneWavTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\\Users\\Michael\\Documents\\GitHub\\worktrees\\college-music-server -- --treenode-filter \"/*/*/TuneSynthTests/*\"", "acceptanceCriteria": ["buffer = 64 columns + 1.5 s for each speed (Quick 648000 frames, Slow 1224000)", "empty tune is silent", "same tune gives same samples", "every row at once peaks at most 90% of 16-bit and above 10000", "octave-higher flute crosses zero 1.9-2.1x as often", "RenderNote short and not silent for every instrument, bass and drum", "TuneWav header PCM 2ch 48000 Hz 16-bit + little-endian samples"], "modelTier": "standard"}
```

---

### Task 7: Client tune channel and player: MusicDuck, SoundSystem, TunePlayer, ChaosGame (client repo)

**Goal:** One tune at a time plays on its own mixer channel, at the sound effects volume. The map music fades out
while a tune plays. `TunePlayer` renders off the game thread and reports the playhead, loops, owners and note
previews.

**Files:**
- Create: `Chaos.Client/Systems/MusicDuck.cs`
- Create: `Chaos.Client/Systems/College/ITuneOutput.cs`
- Create: `Chaos.Client/Systems/College/TunePlayer.cs`
- Modify: `Chaos.Client/Systems/SoundSystem.cs`
- Modify: `Chaos.Client/ChaosGame.cs`: the `SoundSystem` property (~line 112), the constructor, `Update` (~line 895) and `UnloadContent` (~line 861)
- Test: `Tests/Chaos.Client.Tests/MusicDuckTests.cs`, `Tests/Chaos.Client.Tests/College/TunePlayerTests.cs`

**Acceptance Criteria:**
- [ ] `MusicDuck` falls from 1 to 0 over 0.3 s while ducked, rises back when released, and scales the music volume.
- [ ] `TunePlayer.Play` renders through the runner, plays once the render is done, and reports `IsRendering` then `IsPlaying`.
- [ ] `Column` follows the clock from the start column, up to 64.
- [ ] Without a loop, the tune stops when the output stops. `Stopped` is raised with the owner.
- [ ] Playing another tune stops the first and raises `Stopped` for the first owner. `StopIfOwner` with another owner does nothing.
- [ ] With a loop, the next pass is rendered while the current one plays, and starts when `Column` reaches 64.
- [ ] A loop source that returns null ends playback after the current pass.
- [ ] A failed render stops the player.
- [ ] Previews are rendered once per scale, instrument, layer and row, and are skipped while a tune plays. A bass preview ignores the instrument.
- [ ] `SoundSystem` keeps channel 31 out of the effect pool, plays tunes at the sound effects volume, and frees the tune chunk when it ends.
- [ ] The full client suite passes.

**Verify:** `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\college-music-server -- --treenode-filter "/*/*/TunePlayerTests/*"` -> all pass; same for `MusicDuckTests`. Then the full client suite -> all pass.

**Steps:**

- [ ] **Step 1: Write the failing tests.** Create `Tests/Chaos.Client.Tests/MusicDuckTests.cs`:

```csharp
using Chaos.Client.Systems;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class MusicDuckTests
{
    [Test]
    public void Ducking_fades_the_music_out_over_the_fade_time()
    {
        var duck = new MusicDuck { Ducked = true };

        duck.Step(MusicDuck.FADE_SECONDS / 2).Should().BeTrue();
        duck.Level.Should().BeApproximately(0.5, 1e-9);
        duck.Apply(120).Should().Be(60);

        duck.Step(1).Should().BeTrue();
        duck.Level.Should().Be(0);
        duck.Step(1).Should().BeFalse();
    }

    [Test]
    public void Releasing_brings_the_music_back()
    {
        var duck = new MusicDuck { Ducked = true };
        duck.Step(1);

        duck.Ducked = false;
        duck.Step(MusicDuck.FADE_SECONDS);

        duck.Level.Should().Be(1);
        duck.Apply(84).Should().Be(84);
    }

    [Test]
    public void At_rest_nothing_changes()
    {
        var duck = new MusicDuck();

        duck.Step(0.5).Should().BeFalse();
        duck.Apply(96).Should().Be(96);
    }
}
```

Create `Tests/Chaos.Client.Tests/College/TunePlayerTests.cs`:

```csharp
using Chaos.Client.Systems.College;
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class TunePlayerTests
{
    private readonly FakeOutput Output = new();
    private readonly List<object> Stopped = [];
    private double Now;
    private int NoteRenders;
    private int TuneRenders;

    private static readonly TuneData Steady = TuneData.Empty with { Speed = TuneSpeed.Steady };

    private TunePlayer Player(Func<TuneData, short[]>? render = null)
    {
        var player = new TunePlayer(
            Output,
            () => Now,
            work => Task.FromResult(work()),
            render
            ?? (_ =>
            {
                TuneRenders++;

                return new short[4];
            }),
            (_, _, _, _) =>
            {
                NoteRenders++;

                return new short[4];
            });

        player.Stopped += Stopped.Add;

        return player;
    }

    [Test]
    public void A_tune_renders_then_plays()
    {
        var player = Player();
        var owner = new object();

        player.Play(Steady, owner);

        player.IsRendering.Should().BeTrue();
        player.IsPlaying.Should().BeFalse();
        player.Owner.Should().BeSameAs(owner);

        player.Update();

        player.IsRendering.Should().BeFalse();
        player.IsPlaying.Should().BeTrue();
        Output.Calls.Should().Equal("tune");
    }

    [Test]
    public void The_playhead_follows_the_clock_from_the_start_column()
    {
        var player = Player();
        player.Play(Steady, new object(), 8);
        player.Update();

        Now = 1;

        player.Column.Should().BeApproximately(12, 1e-9);

        Now = 100;

        player.Column.Should().Be(CollegeProtocol.TUNE_STEPS);
    }

    [Test]
    public void Without_a_loop_the_tune_stops_when_the_sound_ends()
    {
        var player = Player();
        var owner = new object();
        player.Play(Steady, owner);
        player.Update();

        Output.IsTunePlaying = false;
        player.Update();

        player.Owner.Should().BeNull();
        player.Column.Should().BeNull();
        Stopped.Should().Equal(owner);
    }

    [Test]
    public void Playing_another_tune_stops_the_first()
    {
        var player = Player();
        var first = new object();
        var second = new object();
        player.Play(Steady, first);
        player.Update();

        player.Play(Steady, second);

        Stopped.Should().Equal(first);
        Output.Calls.Should().Equal("tune", "stop");
        player.Owner.Should().BeSameAs(second);
    }

    [Test]
    public void Stop_if_owner_ignores_other_owners()
    {
        var player = Player();
        var owner = new object();
        player.Play(Steady, owner);
        player.Update();

        player.StopIfOwner(new object());
        player.IsPlaying.Should().BeTrue();

        player.StopIfOwner(owner);
        player.IsPlaying.Should().BeFalse();
        Stopped.Should().Equal(owner);
    }

    [Test]
    public void A_loop_renders_the_next_pass_and_starts_it_at_column_64()
    {
        var player = Player();
        var passes = 0;

        player.Play(
            Steady,
            new object(),
            loop: () =>
            {
                passes++;

                return Steady;
            });

        player.Update();

        passes.Should().Be(1);
        TuneRenders.Should().Be(2);

        Now = CollegeProtocol.TUNE_STEPS * TuneScales.StepSeconds(TuneSpeed.Steady);
        player.Update();

        Output.Calls.Should().Equal("tune", "tune");
        player.Column.Should().BeApproximately(0, 1e-9);
        passes.Should().Be(2);
    }

    [Test]
    public void A_loop_that_returns_null_ends_after_the_current_pass()
    {
        var player = Player();
        var looping = true;

        player.Play(Steady, new object(), loop: () => looping ? Steady : null);
        player.Update();

        looping = false;
        Now = CollegeProtocol.TUNE_STEPS * TuneScales.StepSeconds(TuneSpeed.Steady);
        player.Update();

        Output.Calls.Should().Equal("tune");
        player.IsPlaying.Should().BeTrue();

        Output.IsTunePlaying = false;
        player.Update();

        player.IsPlaying.Should().BeFalse();
    }

    [Test]
    public void A_failed_render_stops_the_player()
    {
        var owner = new object();
        var player = new TunePlayer(
            Output,
            () => Now,
            _ => Task.FromException<short[]>(new InvalidOperationException()),
            _ => [],
            (_, _, _, _) => []);
        player.Stopped += Stopped.Add;

        player.Play(Steady, owner);
        player.Update();

        player.Owner.Should().BeNull();
        Stopped.Should().Equal(owner);
        Output.Calls.Should().Equal("stop");
    }

    [Test]
    public void Previews_are_rendered_once_per_note()
    {
        var player = Player();

        player.Preview(TuneScale.Major, TuneInstrument.Lute, TuneLayer.Melody, 3);
        player.Preview(TuneScale.Major, TuneInstrument.Lute, TuneLayer.Melody, 3);
        player.Preview(TuneScale.Major, TuneInstrument.Lute, TuneLayer.Bass, 3);
        player.Preview(TuneScale.Major, TuneInstrument.Bells, TuneLayer.Bass, 3);

        NoteRenders.Should().Be(2);
        Output.Calls.Should().Equal("preview", "preview", "preview", "preview");
    }

    [Test]
    public void Previews_wait_while_a_tune_plays()
    {
        var player = Player();
        player.Play(Steady, new object());
        player.Update();

        player.Preview(TuneScale.Major, TuneInstrument.Lute, TuneLayer.Melody, 3);

        Output.Calls.Should().Equal("tune");
    }

    private sealed class FakeOutput : ITuneOutput
    {
        public List<string> Calls { get; } = [];
        public bool IsTunePlaying { get; set; }

        public bool PlayTune(byte[] wav)
        {
            Calls.Add("tune");
            IsTunePlaying = true;

            return true;
        }

        public bool PlayPreview(byte[] wav)
        {
            Calls.Add("preview");

            return true;
        }

        public void StopTune()
        {
            Calls.Add("stop");
            IsTunePlaying = false;
        }
    }
}
```

- [ ] **Step 2: Run to see them fail.** Run the `TunePlayerTests` filter. Expected: build errors, because `MusicDuck`,
  `ITuneOutput` and `TunePlayer` don't exist.

- [ ] **Step 3: Write `MusicDuck` and `ITuneOutput`.** Create `Chaos.Client/Systems/MusicDuck.cs`:

```csharp
namespace Chaos.Client.Systems;

/// <summary>How far the map music is turned down while a College tune plays: 1 is full volume, 0 is silent.</summary>
public sealed class MusicDuck
{
    public const double FADE_SECONDS = 0.3;

    public double Level { get; private set; } = 1;
    public bool Ducked { get; set; }

    /// <summary>Moves the level toward its target. True when it changed, so the caller applies it.</summary>
    public bool Step(double elapsedSeconds)
    {
        var target = Ducked ? 0 : 1;

        if (Level == target)
            return false;

        var delta = elapsedSeconds / FADE_SECONDS;
        Level = Ducked ? Math.Max(0, Level - delta) : Math.Min(1, Level + delta);

        return true;
    }

    public int Apply(int musicVolume) => (int)Math.Round(musicVolume * Level);
}
```

Create `Chaos.Client/Systems/College/ITuneOutput.cs`:

```csharp
namespace Chaos.Client.Systems.College;

/// <summary>The one mixer channel College tunes play on. <see cref="SoundSystem" /> is the real one.</summary>
public interface ITuneOutput
{
    bool IsTunePlaying { get; }

    /// <summary>Plays a whole tune and fades the map music out until it stops.</summary>
    bool PlayTune(byte[] wav);

    /// <summary>Plays one note's preview on the same channel, leaving the map music as it is.</summary>
    bool PlayPreview(byte[] wav);

    void StopTune();
}
```

- [ ] **Step 4: Write `TunePlayer`.** Create `Chaos.Client/Systems/College/TunePlayer.cs`:

```csharp
using System.Diagnostics;
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;

namespace Chaos.Client.Systems.College;

/// <summary>
///     Plays one College tune at a time. A tune is rendered whole on a background thread, then played as one sound, so
///     its timing is exact. Each play names an owner (the window or row that started it); starting another tune stops
///     the first and raises <see cref="Stopped" /> with the old owner. Call <see cref="Update" /> once per frame.
/// </summary>
public sealed class TunePlayer
{
    private const int MAX_PREVIEWS = 64;

    private readonly Func<double> Clock;
    private readonly ITuneOutput Output;
    private readonly Dictionary<(TuneScale, TuneInstrument, TuneLayer, int), byte[]> Previews = [];
    private readonly Func<TuneScale, TuneInstrument, TuneLayer, int, short[]> RenderNote;
    private readonly Func<TuneData, short[]> RenderTune;
    private readonly Func<Func<short[]>, Task<short[]>> Run;

    private TuneData? Current;
    private int From;
    private Func<TuneData?>? Loop;
    private Task<short[]>? NextPass;
    private TuneData? NextTune;
    private bool Playing;
    private Task<short[]>? Rendering;
    private double StartedAt;

    public TunePlayer(ITuneOutput output)
        : this(
            output,
            static () => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency,
            static work => Task.Run(work),
            TuneSynth.Render,
            TuneSynth.RenderNote) { }

    internal TunePlayer(
        ITuneOutput output,
        Func<double> clock,
        Func<Func<short[]>, Task<short[]>> run,
        Func<TuneData, short[]> renderTune,
        Func<TuneScale, TuneInstrument, TuneLayer, int, short[]> renderNote)
    {
        Output = output;
        Clock = clock;
        Run = run;
        RenderTune = renderTune;
        RenderNote = renderNote;
    }

    public object? Owner { get; private set; }
    public bool IsRendering => (Owner is not null) && (Rendering is not null);
    public bool IsPlaying => (Owner is not null) && Playing;

    /// <summary>The playhead in columns while a tune plays, at most 64; null otherwise.</summary>
    public double? Column
        => IsPlaying && Current is not null
            ? Math.Min(CollegeProtocol.TUNE_STEPS, From + ((Clock() - StartedAt) / TuneScales.StepSeconds(Current.Speed)))
            : null;

    public event Action<object>? Stopped;

    /// <summary>
    ///     Starts a tune from a column. With <paramref name="loop" />, each next pass is taken from it while the current
    ///     pass plays, so edits are heard on the next pass. A null from it ends the loop after the current pass.
    /// </summary>
    public void Play(TuneData tune, object owner, int fromColumn = 0, Func<TuneData?>? loop = null)
    {
        Stop();

        Owner = owner;
        Current = tune;
        From = Math.Clamp(fromColumn, 0, CollegeProtocol.TUNE_STEPS - 1);
        Loop = loop;

        var from = From;
        Rendering = Run(() => Slice(RenderTune(tune), tune.Speed, from));
    }

    public void Stop()
    {
        if (Owner is null)
            return;

        var old = Owner;
        Owner = null;
        Current = null;
        Loop = null;
        NextPass = null;
        NextTune = null;
        Rendering = null;
        Playing = false;
        Output.StopTune();
        Stopped?.Invoke(old);
    }

    public void StopIfOwner(object owner)
    {
        if (ReferenceEquals(Owner, owner))
            Stop();
    }

    /// <summary>Plays one note on its own; skipped while a tune plays or renders, since both use the same channel.</summary>
    public void Preview(TuneScale scale, TuneInstrument instrument, TuneLayer layer, int row)
    {
        if (Owner is not null)
            return;

        var key = layer switch
        {
            TuneLayer.Drums => (TuneScale.Major, TuneInstrument.Lute, layer, row),
            TuneLayer.Bass  => (scale, TuneInstrument.Lute, layer, row),
            _               => (scale, instrument, layer, row)
        };

        if (!Previews.TryGetValue(key, out var wav))
        {
            if (Previews.Count >= MAX_PREVIEWS)
                Previews.Clear();

            wav = TuneWav.Wrap(RenderNote(key.Item1, key.Item2, key.Item3, key.Item4));
            Previews[key] = wav;
        }

        Output.PlayPreview(wav);
    }

    public void Update()
    {
        if (Owner is null)
            return;

        if (Rendering is { IsCompleted: true } rendered)
        {
            Rendering = null;

            if (!rendered.IsCompletedSuccessfully || !Output.PlayTune(TuneWav.Wrap(rendered.Result)))
            {
                Stop();

                return;
            }

            Playing = true;
            StartedAt = Clock();
            StartNextPass();
        }

        if (!Playing)
            return;

        if (Loop is not null)
        {
            if ((Column < CollegeProtocol.TUNE_STEPS) || NextPass is not { IsCompleted: true } next)
                return;

            var following = Loop();

            if (following is null)
            {
                Loop = null;
                NextPass = null;
                NextTune = null;

                return;
            }

            if (!next.IsCompletedSuccessfully || !Output.PlayTune(TuneWav.Wrap(next.Result)))
            {
                Stop();

                return;
            }

            Current = NextTune;
            From = 0;
            StartedAt = Clock();
            StartNextPass(following);

            return;
        }

        if (!Output.IsTunePlaying)
            Stop();
    }

    private void StartNextPass(TuneData? known = null)
    {
        if (Loop is null)
            return;

        if ((known ?? Loop()) is not { } tune)
        {
            Loop = null;

            return;
        }

        NextTune = tune;
        NextPass = Run(() => RenderTune(tune));
    }

    private static short[] Slice(short[] samples, TuneSpeed speed, int fromColumn)
    {
        if (fromColumn == 0)
            return samples;

        var skip = (int)Math.Round(fromColumn * TuneScales.StepSeconds(speed) * TuneSynth.SAMPLE_RATE) * 2;

        return skip >= samples.Length ? [] : samples[skip..];
    }
}
```

- [ ] **Step 5: Run the player tests.** Run the `TunePlayerTests` and `MusicDuckTests` filters. Expected: all pass.

- [ ] **Step 6: Give `SoundSystem` the tune channel.** In `Chaos.Client/Systems/SoundSystem.cs`:

1. Change the class line to `public sealed class SoundSystem : IDisposable, ITuneOutput`.
   Add `using System.Diagnostics;` and `using Chaos.Client.Systems.College;` to the `#region` usings.
2. After `private const int FADE_OUT_MS = 200;` add:

```csharp
    //College tunes play on the last channel, which PlayChunk never hands to a sound effect, so a crowd of effects can't
    //cut a tune off and a tune never steals an effect's voice
    private const int TUNE_CHANNEL = CHANNEL_COUNT - 1;
```

3. After `private long SoundCacheTimestamp;` add:

```csharp
    private readonly MusicDuck Duck = new();
    private long LastUpdateTimestamp;
    private nint TuneChunk;
```

4. In `PlayChunk`, change the free-channel loop from `for (var i = 0; i < CHANNEL_COUNT; i++)` to
   `for (var i = 0; i < TUNE_CHANNEL; i++)`.
5. Replace `SetMusicVolume` and `SetSoundVolume` with:

```csharp
    /// <summary>
    ///     Sets the music volume. Range: 0 (mute) to 10 (max). Applies immediately to the currently playing track; while
    ///     a College tune holds the music down, the new volume is reached when the music fades back in.
    /// </summary>
    public void SetMusicVolume(int volume)
    {
        MusicVolumeValue = Math.Clamp(volume, 0, VOLUME_STEPS) * VOLUME_SCALE;

        if (Initialized)
            SdlMixer.Mix_VolumeMusic(Duck.Apply(MusicVolumeValue));
    }

    /// <summary>
    ///     Sets the sound effect volume. Range: 0 (mute) to 10 (max). Future plays use the new volume; sounds already in
    ///     flight keep their current channel volume, except a College tune, which follows the slider at once.
    /// </summary>
    public void SetSoundVolume(int volume)
    {
        SfxVolume = Math.Clamp(volume, 0, VOLUME_STEPS) * VOLUME_SCALE;

        if (Initialized && (TuneChunk != nint.Zero))
            SdlMixer.Mix_Volume(TUNE_CHANNEL, SfxVolume);
    }

    /// <inheritdoc />
    public bool IsTunePlaying => (TuneChunk != nint.Zero) && (SdlMixer.Mix_Playing(TUNE_CHANNEL) != 0);

    /// <inheritdoc />
    public bool PlayTune(byte[] wav) => StartTuneChunk(wav, true);

    /// <inheritdoc />
    public bool PlayPreview(byte[] wav) => StartTuneChunk(wav, false);

    /// <inheritdoc />
    public void StopTune()
    {
        ReleaseTuneChunk();
        Duck.Ducked = false;
    }

    private bool StartTuneChunk(byte[] wav, bool holdMusicDown)
    {
        if (IsDisposed || !Initialized)
            return false;

        ReleaseTuneChunk();

        var chunk = LoadChunkFromBytes(wav);

        if (chunk == nint.Zero)
        {
            Duck.Ducked = false;

            return false;
        }

        SdlMixer.Mix_Volume(TUNE_CHANNEL, SfxVolume);

        if (SdlMixer.Mix_PlayChannel(TUNE_CHANNEL, chunk, 0) < 0)
        {
            SdlMixer.Mix_FreeChunk(chunk);
            Duck.Ducked = false;

            return false;
        }

        TuneChunk = chunk;
        Duck.Ducked = holdMusicDown;

        return true;
    }

    private void ReleaseTuneChunk()
    {
        if (TuneChunk == nint.Zero)
            return;

        //Mix_HaltChannel stops the channel under the audio lock, so the chunk is no longer read when it is freed
        SdlMixer.Mix_HaltChannel(TUNE_CHANNEL);
        SdlMixer.Mix_FreeChunk(TuneChunk);
        TuneChunk = nint.Zero;
    }
```

6. In `Update`, after the `FinishedChannels` drain loop and before `//detect fade-out completion`, add:

```csharp
        //a tune that played to its end: free its chunk and let the map music come back
        if ((TuneChunk != nint.Zero) && (SdlMixer.Mix_Playing(TUNE_CHANNEL) == 0))
        {
            SdlMixer.Mix_FreeChunk(TuneChunk);
            TuneChunk = nint.Zero;
            Duck.Ducked = false;
        }

        var now = Stopwatch.GetTimestamp();
        var elapsed = LastUpdateTimestamp == 0 ? 0 : (now - LastUpdateTimestamp) / (double)Stopwatch.Frequency;
        LastUpdateTimestamp = now;

        if (Duck.Step(elapsed))
            SdlMixer.Mix_VolumeMusic(Duck.Apply(MusicVolumeValue));
```

7. In `Dispose`, right after `SdlMixer.Mix_HaltMusic();`, add:

```csharp
        if (TuneChunk != nint.Zero)
        {
            SdlMixer.Mix_FreeChunk(TuneChunk);
            TuneChunk = nint.Zero;
        }
```

The drain in `Update` already resets a finished channel's volume to `SfxVolume`. On the tune channel that is the right
level, and `ChannelToSoundId` never holds the tune channel, so the drain leaves the tune alone.

- [ ] **Step 7: Own the player in `ChaosGame`.** In `Chaos.Client/ChaosGame.cs`:

1. After the `SoundSystem` property, add:

```csharp
    /// <summary>
    ///     Plays College tunes, one at a time, on the sound system's reserved tune channel.
    /// </summary>
    public TunePlayer TunePlayer { get; }
```

2. Make this the first statement of the `ChaosGame()` constructor body. Property initializers have already run, so
   `SoundSystem` exists:

```csharp
        TunePlayer = new TunePlayer(SoundSystem);
```

3. In `Update`, right after `SoundSystem.Update();`, add:

```csharp
        //after the sound system has reaped a finished tune, so the player sees that it ended this frame
        TunePlayer.Update();
```

4. In `UnloadContent`, right before `SoundSystem.Dispose();`, add `TunePlayer.Stop();`.
5. Add `using Chaos.Client.Systems.College;` if the file doesn't already import it.

- [ ] **Step 8: Build and run the full client suite.**

```bash
dotnet build Chaos.Client/Chaos.Client.csproj -p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\college-music-server
dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\college-music-server -- --no-ansi
```

Expected: build succeeded, and every test passes.

**Checked only in game (Task 10's checklist):** the SDL side of `SoundSystem` (the reserved channel, the music fade and
the volume slider following a playing tune) needs the real mixer. The unit tests cover the fade maths (`MusicDuck`)
and every player rule (`TunePlayer` with a fake output).

```json:metadata
{"files": ["Chaos.Client/Systems/MusicDuck.cs", "Chaos.Client/Systems/College/ITuneOutput.cs", "Chaos.Client/Systems/College/TunePlayer.cs", "Chaos.Client/Systems/SoundSystem.cs", "Chaos.Client/ChaosGame.cs", "Tests/Chaos.Client.Tests/MusicDuckTests.cs", "Tests/Chaos.Client.Tests/College/TunePlayerTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\\Users\\Michael\\Documents\\GitHub\\worktrees\\college-music-server -- --treenode-filter \"/*/*/TunePlayerTests/*\"", "acceptanceCriteria": ["MusicDuck fades 1->0 over 0.3 s, back when released, scales volume", "Play renders via runner then plays; IsRendering then IsPlaying", "Column follows the clock from the start column, capped at 64", "without loop, stops when output stops; Stopped raised with owner", "another Play stops the first; StopIfOwner ignores other owners", "loop renders next pass during current and starts it at column 64", "null from loop source ends after current pass", "failed render stops the player", "previews cached per scale/instrument/layer/row, skipped while playing; bass ignores instrument", "SoundSystem: channel 31 out of effect pool, tunes at SFX volume, chunk freed at end", "full client suite passes"], "modelTier": "standard"}
```

---

### Task 8: Client composer window: TuneGridLayout, TuneLabels, TuneGrid, MusicComposerControl (client repo)

**Goal:** The 560 x 464 Music composer window has the stacked layout (option B, cells 8 x 7), the toolbar, playback through `TunePlayer`, and Draft and HandIn modes.

**Files:**
- Create: `Chaos.Client/Controls/World/Popups/College/TuneGridLayout.cs`
- Create: `Chaos.Client/ViewModel/College/TuneLabels.cs`
- Create: `Chaos.Client/Controls/World/Popups/College/TuneGrid.cs`
- Create: `Chaos.Client/Controls/World/Popups/College/MusicComposerControl.cs`
- Test: `Tests/Chaos.Client.Tests/College/TuneGridLayoutTests.cs`, `Tests/Chaos.Client.Tests/College/TuneLabelsTests.cs`

**Acceptance Criteria:**
- [ ] The composer grid is 530 x 251 and the reader grid is 384 x 138. Hit tests map the top-left melody cell to row 14, column 0, and the bottom-right drum cell to row 0, column 63. The label column and the gaps between layers are not cells. The beat bar gives bars 0-7.
- [ ] Every part fits 560 x 464 without overlap:
  - caption;
  - the Title box with the **Scale**, **Speed** and **Melody** cycle buttons on one row;
  - beat bar and grids at y 52-303;
  - toolbar at y 306: **Melody**, **Bass**, **Drums** | **Play**, **Loop**, **Undo**, **Redo** | **Copy bars 1-4 to 5-8**, **Clear layer**;
  - 3-line note box at y 331-377;
  - footer at y 391.
- [ ] Mouse:
  - The left button on an empty cell places a note and plays its preview; dragging right lengthens it. The left button on a note removes it. The right button always removes.
  - Clicking a cell selects its layer.
  - Clicking a bar number plays from that bar.
- [ ] Keys: Space plays or stops, Ctrl+Z undoes and Ctrl+Y redoes, all only while neither text box has focus. Escape closes.
- [ ] **Play** reads `Wait...` while the tune renders and `Stop` while it plays. The playhead line crosses all three grids. **Loop** keeps replaying, and turning it off ends playback after the current pass.
- [ ] Copy and Clear act on the selected layer. Undo and Redo are enabled only when they apply.
- [ ] **Draft** mode: Save; Submit with confirmation; closing with changes saves first. **HandIn** mode: the prompt shows in the caption; "Hand in"; "Close without handing in?".
- [ ] Before sending, the window refuses with the server's text: no title, fewer than 16 melody notes or 3 melody rows, or fewer than 8 notes for a hand-in.
- [ ] Hiding the window stops a tune it started.

**Verify:** `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\college-music-server -- --treenode-filter "/*/*/TuneGridLayoutTests/*"` -> all pass; same for `TuneLabelsTests`; and `dotnet build Chaos.Client/Chaos.Client.csproj -p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\college-music-server` -> Build succeeded. The window is checked in game (Task 10 checklist).

**Steps:**

- [ ] **Step 1: Write the failing tests.** Create `Tests/Chaos.Client.Tests/College/TuneGridLayoutTests.cs`:

```csharp
using Chaos.Client.Controls.World.Popups.College;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class TuneGridLayoutTests
{
    private static readonly TuneGridLayout Composer = TuneGridLayout.Composer;

    [Test]
    public void The_composer_grid_is_530_by_251()
    {
        Composer.Width.Should().Be(530);
        Composer.Height.Should().Be(251);
        Composer.LayerTop(TuneLayer.Melody).Should().Be(14);
        Composer.LayerTop(TuneLayer.Bass).Should().Be(122);
        Composer.LayerTop(TuneLayer.Drums).Should().Be(230);
    }

    [Test]
    public void The_reader_grid_is_384_by_138()
    {
        TuneGridLayout.Reader.Width.Should().Be(384);
        TuneGridLayout.Reader.Height.Should().Be(138);
        TuneGridLayout.Reader.LayerTop(TuneLayer.Melody).Should().Be(0);
    }

    [Test]
    public void The_top_left_melody_cell_is_its_highest_row()
    {
        Composer.TryHitCell(18, 14, out var layer, out var row, out var column).Should().BeTrue();

        layer.Should().Be(TuneLayer.Melody);
        row.Should().Be(14);
        column.Should().Be(0);
    }

    [Test]
    public void The_bottom_right_drum_cell_is_row_0_column_63()
    {
        Composer.TryHitCell(529, 250, out var layer, out var row, out var column).Should().BeTrue();

        layer.Should().Be(TuneLayer.Drums);
        row.Should().Be(0);
        column.Should().Be(63);
    }

    [Test]
    public void The_label_column_the_beat_bar_and_the_gaps_are_not_cells()
    {
        Composer.TryHitCell(17, 20, out _, out _, out _).Should().BeFalse();
        Composer.TryHitCell(100, 5, out _, out _, out _).Should().BeFalse();
        Composer.TryHitCell(100, 120, out _, out _, out _).Should().BeFalse();
        Composer.TryHitCell(100, 228, out _, out _, out _).Should().BeFalse();
        Composer.TryHitCell(530, 20, out _, out _, out _).Should().BeFalse();
    }

    [Test]
    public void Row_tops_count_down_from_the_top_row()
    {
        Composer.RowTop(TuneLayer.Melody, 14).Should().Be(14);
        Composer.RowTop(TuneLayer.Bass, 0).Should().Be(122 + (14 * 7));
        Composer.RowTop(TuneLayer.Drums, 2).Should().Be(230);
    }

    [Test]
    public void The_beat_bar_names_bars_0_to_7()
    {
        Composer.BarAt(18, 0).Should().Be(0);
        Composer.BarAt(18 + (7 * 64), 11).Should().Be(7);
        Composer.BarAt(529, 5).Should().Be(7);
        Composer.BarAt(18, 12).Should().BeNull();
        Composer.BarAt(10, 5).Should().BeNull();
        TuneGridLayout.Reader.BarAt(0, 0).Should().BeNull();
    }

    [Test]
    public void Columns_for_a_drag_stay_inside_the_grid()
    {
        Composer.ColumnAt(0).Should().Be(0);
        Composer.ColumnAt(18 + 8 * 10 + 7).Should().Be(10);
        Composer.ColumnAt(10_000).Should().Be(63);
    }
}
```

Create `Tests/Chaos.Client.Tests/College/TuneLabelsTests.cs`:

```csharp
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class TuneLabelsTests
{
    [Test]
    public void Scales_speeds_and_instruments_have_display_names()
    {
        TuneLabels.Scale(TuneScale.FiveNote).Should().Be("Five-note");
        TuneLabels.Scale(TuneScale.Desert).Should().Be("Desert");
        TuneLabels.Speed(TuneSpeed.Walking).Should().Be("Walking");
        TuneLabels.Instrument(TuneInstrument.Bells).Should().Be("Bells");
    }

    [Test]
    public void The_summary_names_scale_speed_and_instrument()
        => TuneLabels.Summary(new TuneData(TuneScale.Dorian, TuneSpeed.Lively, TuneInstrument.Harp, []))
                     .Should()
                     .Be("Dorian, Lively, Harp");

    [Test]
    public void Next_cycles_and_wraps()
    {
        TuneLabels.Next(TuneScale.Major).Should().Be(TuneScale.Minor);
        TuneLabels.Next(TuneScale.Desert).Should().Be(TuneScale.Major);
        TuneLabels.Next(TuneSpeed.Quick).Should().Be(TuneSpeed.Slow);
        TuneLabels.Next(TuneInstrument.Bells).Should().Be(TuneInstrument.Lute);
    }
}
```

- [ ] **Step 2: Run to see them fail.** Run the `TuneGridLayoutTests` filter. Expected: build errors for `TuneGridLayout` and `TuneLabels`.

- [ ] **Step 3: TuneGridLayout.** Create `Chaos.Client/Controls/World/Popups/College/TuneGridLayout.cs`:

```csharp
using Chaos.DarkAges.Definitions;

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>
///     Where things sit in a <see cref="TuneGrid" />, in pixels from its top left: an optional beat bar, then Melody, Bass
///     and Drums stacked with a gap between them, each with an optional row-name column at the left. Row 0 is a layer's
///     bottom row.
/// </summary>
public sealed record TuneGridLayout(int CellWidth, int CellHeight, int LabelWidth, int BeatBarHeight, int LayerGap)
{
    public const int BEAT_BAR_GAP = 2;
    public const int COLUMNS_PER_BAR = 8;

    /// <summary>The composer: 8 x 7 cells, row names, the bar numbers above.</summary>
    public static readonly TuneGridLayout Composer = new(8, 7, 18, 12, 3);

    /// <summary>The reader: 6 x 4 cells (384 wide, like a drawing), no names, no beat bar.</summary>
    public static readonly TuneGridLayout Reader = new(6, 4, 0, 0, 3);

    public static readonly TuneLayer[] Layers = [TuneLayer.Melody, TuneLayer.Bass, TuneLayer.Drums];

    public int GridX => LabelWidth;
    public int GridWidth => CollegeProtocol.TUNE_STEPS * CellWidth;
    public int Width => LabelWidth + GridWidth;
    public int Height => LayerTop(TuneLayer.Drums) + LayerHeight(TuneLayer.Drums);

    public int LayerHeight(TuneLayer layer) => CollegeProtocol.TuneRows(layer) * CellHeight;

    public int LayerTop(TuneLayer layer)
        => layer switch
        {
            TuneLayer.Melody => BeatBarHeight > 0 ? BeatBarHeight + BEAT_BAR_GAP : 0,
            TuneLayer.Bass   => LayerTop(TuneLayer.Melody) + LayerHeight(TuneLayer.Melody) + LayerGap,
            _                => LayerTop(TuneLayer.Bass) + LayerHeight(TuneLayer.Bass) + LayerGap
        };

    public int RowTop(TuneLayer layer, int row) => LayerTop(layer) + ((CollegeProtocol.TuneRows(layer) - 1 - row) * CellHeight);

    /// <summary>The column under <paramref name="x" />, kept inside the grid (for a drag that leaves it).</summary>
    public int ColumnAt(int x) => Math.Clamp((int)Math.Floor((x - GridX) / (double)CellWidth), 0, CollegeProtocol.TUNE_STEPS - 1);

    public bool TryHitCell(int x, int y, out TuneLayer layer, out int row, out int column)
    {
        layer = TuneLayer.Melody;
        row = 0;
        column = 0;

        if ((x < GridX) || (x >= Width))
            return false;

        foreach (var candidate in Layers)
        {
            var top = LayerTop(candidate);

            if ((y < top) || (y >= (top + LayerHeight(candidate))))
                continue;

            layer = candidate;
            row = CollegeProtocol.TuneRows(candidate) - 1 - ((y - top) / CellHeight);
            column = (x - GridX) / CellWidth;

            return true;
        }

        return false;
    }

    /// <summary>The bar (0-7) whose number is under the point, or null outside the beat bar.</summary>
    public int? BarAt(int x, int y)
        => (BeatBarHeight > 0) && (y >= 0) && (y < BeatBarHeight) && (x >= GridX) && (x < Width)
            ? (x - GridX) / (COLUMNS_PER_BAR * CellWidth)
            : null;
}
```

- [ ] **Step 4: TuneLabels.** Create `Chaos.Client/ViewModel/College/TuneLabels.cs`:

```csharp
using Chaos.DarkAges.Definitions;

namespace Chaos.Client.ViewModel.College;

/// <summary>What the composer's buttons and the reader's summary line call a tune's scale, speed and instrument.</summary>
public static class TuneLabels
{
    public static string Scale(TuneScale scale)
        => scale switch
        {
            TuneScale.FiveNote => "Five-note",
            _                  => scale.ToString()
        };

    public static string Speed(TuneSpeed speed) => speed.ToString();

    public static string Instrument(TuneInstrument instrument) => instrument.ToString();

    /// <summary>"Dorian, Lively, Harp".</summary>
    public static string Summary(TuneData tune) => $"{Scale(tune.Scale)}, {Speed(tune.Speed)}, {Instrument(tune.Instrument)}";

    /// <summary>The value after <paramref name="value" />, wrapping to the first: one click of a cycle button.</summary>
    public static T Next<T>(T value) where T : struct, Enum
    {
        var values = Enum.GetValues<T>();

        return values[(Array.IndexOf(values, value) + 1) % values.Length];
    }
}
```

- [ ] **Step 5: Run the tests.** Run the `TuneGridLayoutTests` and `TuneLabelsTests` filters. Expected: all pass. (They need Task 5's `TuneData` and Task 1's enums.)

- [ ] **Step 6: TuneGrid.** Create `Chaos.Client/Controls/World/Popups/College/TuneGrid.cs`:

```csharp
#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>
///     A tune's Melody, Bass and Drums grids stacked, with the bar numbers above (composer) and a playhead line while it
///     plays. It shows <see cref="Document" /> when set, otherwise <see cref="Data" /> (the reader). While
///     <see cref="Editable" />, the left button places a note (dragging lengthens it) or removes the note under it, and the
///     right button removes.
/// </summary>
public sealed class TuneGrid : UIElement
{
    private const int SMALL_TEXT_HEIGHT = 6;

    private static readonly Color MelodyColour = new(232, 180, 90);
    private static readonly Color BassColour = new(111, 168, 220);
    private static readonly Color DrumsColour = new(215, 112, 112);
    private static readonly Color RowDark = new(29, 26, 22);
    private static readonly Color RowLight = new(33, 30, 25);
    private static readonly Color HomeRow = new(48, 43, 33);
    private static readonly Color BarLine = new(106, 88, 56);
    private static readonly Color BeatLine = new(52, 46, 37);
    private static readonly Color BeatBarFill = new(20, 18, 16);
    private static readonly Color PlayheadColour = new(255, 255, 255, 210);
    private static readonly string[] BarNumbers = ["1", "2", "3", "4", "5", "6", "7", "8"];

    private readonly Dictionary<string, Texture2D?> SmallText = new(StringComparer.Ordinal);
    private bool Placing;

    public TuneGrid(TuneGridLayout layout)
    {
        Layout = layout;
        Width = layout.Width;
        Height = layout.Height;
    }

    public TuneGridLayout Layout { get; }

    /// <summary>The tune being edited (the composer).</summary>
    public TuneDocument? Document { get; set; }

    /// <summary>A tune to show when there is no <see cref="Document" /> (the reader).</summary>
    public TuneData? Data { get; set; }

    public bool Editable { get; set; }

    /// <summary>The layer Copy and Clear act on; outlined in its colour while editable.</summary>
    public TuneLayer SelectedLayer { get; set; } = TuneLayer.Melody;

    /// <summary>The playhead in columns (fractions allowed), or null when nothing plays.</summary>
    public double? Playhead { get; set; }

    /// <summary>Raised with a new note's layer and row, for its preview.</summary>
    public event Action<TuneLayer, int>? NotePlaced;

    /// <summary>Raised after any press, drag or release that may have changed the tune or the selected layer.</summary>
    public event Action? Edited;

    /// <summary>Raised with a bar (0-7) when its number is clicked.</summary>
    public event Action<int>? BarClicked;

    private TuneScale Scale => Document?.Scale ?? Data?.Scale ?? TuneScale.Major;

    private IReadOnlyList<TuneNote> Notes => Document?.Notes ?? Data?.Notes ?? Array.Empty<TuneNote>();

    public static Color ColourOf(TuneLayer layer)
        => layer switch
        {
            TuneLayer.Melody => MelodyColour,
            TuneLayer.Bass   => BassColour,
            _                => DrumsColour
        };

    public override void Dispose()
    {
        foreach (var texture in SmallText.Values)
            texture?.Dispose();

        SmallText.Clear();
        base.Dispose();
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        base.Draw(spriteBatch);

        if (Layout.BeatBarHeight > 0)
            DrawBeatBar(spriteBatch);

        var scale = Scale;

        foreach (var layer in TuneGridLayout.Layers)
            DrawLayer(spriteBatch, layer, scale);

        foreach (var note in Notes)
            DrawRectClipped(
                spriteBatch,
                new Rectangle(
                    ScreenX + Layout.GridX + (note.Start * Layout.CellWidth) + 1,
                    ScreenY + Layout.RowTop(note.Layer, note.Row) + 1,
                    (note.Length * Layout.CellWidth) - 1,
                    Layout.CellHeight - 1),
                ColourOf(note.Layer));

        if (Playhead is { } column)
            DrawRectClipped(
                spriteBatch,
                new Rectangle(ScreenX + Layout.GridX + (int)(column * Layout.CellWidth), ScreenY, 1, Height),
                PlayheadColour);
    }

    public override void OnMouseDown(MouseDownEvent e)
    {
        if (!Editable || Document is null || Placing || ((e.Button != MouseButton.Left) && (e.Button != MouseButton.Right)))
            return;

        e.Handled = true;
        var x = e.ScreenX - ScreenX;
        var y = e.ScreenY - ScreenY;

        if (Layout.BarAt(x, y) is { } bar)
        {
            if (e.Button == MouseButton.Left)
                BarClicked?.Invoke(bar);

            return;
        }

        if (!Layout.TryHitCell(x, y, out var layer, out var row, out var column))
            return;

        SelectedLayer = layer;

        if ((e.Button == MouseButton.Right) || Document.NoteAt(layer, row, column) is not null)
            Document.Remove(layer, row, column);
        else if (Document.BeginPlace(layer, row, column))
        {
            Placing = true;
            NotePlaced?.Invoke(layer, row);
        }

        Edited?.Invoke();
    }

    public override void OnMouseMove(MouseMoveEvent e)
    {
        if (!Placing)
            return;

        e.Handled = true;

        //the button came up where no mouse-up reached this grid (e.g. the window lost focus mid-drag)
        if (!InputBuffer.IsLeftButtonHeld)
        {
            Finish();

            return;
        }

        Document!.DragTo(Layout.ColumnAt(e.ScreenX - ScreenX));
        Edited?.Invoke();
    }

    public override void OnMouseUp(MouseUpEvent e)
    {
        if (!Placing)
            return;

        e.Handled = true;
        Finish();
    }

    public override void ResetInteractionState()
    {
        base.ResetInteractionState();

        if (Placing)
            Finish();
    }

    private void Finish()
    {
        Placing = false;
        Document?.EndPlace();
        Edited?.Invoke();
    }

    private void DrawBeatBar(SpriteBatch spriteBatch)
    {
        DrawRectClipped(
            spriteBatch,
            new Rectangle(ScreenX + Layout.GridX, ScreenY, Layout.GridWidth, Layout.BeatBarHeight),
            BeatBarFill);

        for (var bar = 0; bar < BarNumbers.Length; bar++)
            DrawTextClipped(
                spriteBatch,
                new Vector2(ScreenX + Layout.GridX + (bar * TuneGridLayout.COLUMNS_PER_BAR * Layout.CellWidth) + 3, ScreenY),
                BarNumbers[bar],
                LegendColors.Gray);
    }

    private void DrawLayer(SpriteBatch spriteBatch, TuneLayer layer, TuneScale scale)
    {
        var rows = CollegeProtocol.TuneRows(layer);
        var layerTop = ScreenY + Layout.LayerTop(layer);
        var layerHeight = Layout.LayerHeight(layer);
        var gridLeft = ScreenX + Layout.GridX;

        for (var row = 0; row < rows; row++)
        {
            var top = ScreenY + Layout.RowTop(layer, row);
            var home = (layer != TuneLayer.Drums) && TuneScales.IsHome(scale, row);
            var fill = home ? HomeRow : row % 2 == 0 ? RowLight : RowDark;
            DrawRectClipped(spriteBatch, new Rectangle(gridLeft, top, Layout.GridWidth, Layout.CellHeight), fill);

            if (Layout.LabelWidth > 0)
                DrawRowName(
                    spriteBatch,
                    TuneScales.RowName(scale, layer, row),
                    top + ((Layout.CellHeight - SMALL_TEXT_HEIGHT) / 2),
                    home || (layer == TuneLayer.Drums) ? ColourOf(layer) : LegendColors.Gray);
        }

        for (var column = 0; column <= CollegeProtocol.TUNE_STEPS; column += 2)
            DrawRectClipped(
                spriteBatch,
                new Rectangle(gridLeft + (column * Layout.CellWidth), layerTop, 1, layerHeight),
                column % TuneGridLayout.COLUMNS_PER_BAR == 0 ? BarLine : BeatLine);

        if (Editable && (layer == SelectedLayer))
        {
            var colour = ColourOf(layer);
            var outline = new Rectangle(gridLeft - 1, layerTop - 1, Layout.GridWidth + 2, layerHeight + 2);
            DrawRectClipped(spriteBatch, new Rectangle(outline.X, outline.Y, outline.Width, 1), colour);
            DrawRectClipped(spriteBatch, new Rectangle(outline.X, outline.Bottom - 1, outline.Width, 1), colour);
            DrawRectClipped(spriteBatch, new Rectangle(outline.X, outline.Y, 1, outline.Height), colour);
            DrawRectClipped(spriteBatch, new Rectangle(outline.Right - 1, outline.Y, 1, outline.Height), colour);
        }
    }

    //row names are 6 px tall (the 12 px font does not fit a 7 px row), right-aligned in the label column
    private void DrawRowName(SpriteBatch spriteBatch, string name, int y, Color colour)
    {
        if (!SmallText.TryGetValue(name, out var texture))
            SmallText[name] = texture = TextRenderer.BuildSmallText(TextureConverter.Device, name, SMALL_TEXT_HEIGHT);

        if (texture is not null)
            DrawTexture(spriteBatch, texture, new Vector2(ScreenX + Layout.LabelWidth - texture.Width - 2, y), colour);
    }
}
```

  Notes for the implementer:
  - `InputBuffer` lives in the `Chaos.Client` namespace, as `ArtCanvas` uses it. Add `using Chaos.Client;` only if the build asks for it.
  - If `LegendColors` is not under `Chaos.Client.Rendering.Definitions`, copy the `using` from `ArtCanvasControl.cs`.

- [ ] **Step 7: MusicComposerControl.** Create `Chaos.Client/Controls/World/Popups/College/MusicComposerControl.cs`:

```csharp
#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Systems.College;
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.College;

public enum MusicComposerMode
{
    Draft,
    HandIn
}

/// <summary>
///     The Music composer (spec: docs/superpowers/specs/2026-10-05-mileth-college-music-design.md): title, scale, speed
///     and melody instrument, the stacked Melody, Bass and Drums grids, the toolbar, the composer's note and the footer.
///     Draft and HandIn send the piece to the server; tunes play through the shared <see cref="TunePlayer" />.
/// </summary>
public sealed class MusicComposerControl : GuildCloakDialogBase
{
    private const int WIDTH = 560;
    private const int HEIGHT = 464;
    private const int LEFT = 16;
    private const int INNER_WIDTH = WIDTH - (2 * LEFT);
    private const int CAPTION_TOP = 10;
    private const int TOP_ROW = 26;
    private const int GAP = 4;
    private const int SCALE_WIDTH = 114;
    private const int SPEED_WIDTH = 96;
    private const int INSTRUMENT_WIDTH = 90;
    private const int TITLE_WIDTH = INNER_WIDTH - SCALE_WIDTH - SPEED_WIDTH - INSTRUMENT_WIDTH - (3 * GAP);
    private const int GRID_X = LEFT - 2;
    private const int GRID_Y = 52;
    private const int TOOL_GAP = 2;
    private const int GROUP_GAP = 6;
    private const int PLAY_WIDTH = 54;
    private const int SMALL_TOOL_WIDTH = 40;
    private const int COPY_WIDTH = 130;
    private const int NOTE_HEIGHT = (3 * TextRenderer.CHAR_HEIGHT) + 10;
    private const int FOOTER_BOTTOM = HEIGHT - BORDER_BOTTOM_HEIGHT - 4;
    private const int BUTTONS_TOP = FOOTER_BOTTOM - CustomButton.HEIGHT;
    private const int SAVE_WIDTH = 60;
    private const int SUBMIT_X = LEFT + SAVE_WIDTH + 6;
    private const int SUBMIT_WIDTH = 130;
    private const int STATUS_X = SUBMIT_X + SUBMIT_WIDTH + 8;
    private const int COUNTER_WIDTH = 60;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;
    private const string CONFIRM_SUBMIT_CAPTION = "Yes, submit";
    private const string MORE_MELODY = "Entries need 16 melody notes on 3 rows.";
    private const string MORE_NOTES = "Hand-ins need at least 8 notes.";
    private const string SAVING = "Saving...";

    private static readonly TuneGridLayout GridLayout = TuneGridLayout.Composer;
    private static readonly int ToolsY = GRID_Y + GridLayout.Height + 3;
    private static readonly int NoteTop = ToolsY + CustomButton.HEIGHT + 3;

    private readonly UILabel CaptionLabel;
    private readonly CustomButton ClearButton;
    private readonly CustomButton CopyButton;
    private readonly TuneDocument Document = new();
    private readonly TuneGrid Grid;
    private readonly CustomButton InstrumentButton;
    private readonly Dictionary<TuneLayer, CustomButton> LayerButtons = new();
    private readonly CustomButton LoopButton;
    private readonly CustomTextBox NoteBox;
    private readonly UILabel NoteCounter;
    private readonly CustomButton PlayButton;
    private readonly TunePlayer Player;
    private readonly CustomButton RedoButton;
    private readonly CustomButton SaveButton;
    private readonly CustomButton ScaleButton;
    private readonly CustomButton SpeedButton;
    private readonly UILabel Status;
    private readonly CustomButton SubmitButton;
    private readonly CustomTextBox TitleBox;
    private readonly CustomButton UndoButton;

    private string? CaptionTitle;
    private bool CloseArmed;

    //Close sent a save and waits for its Saved before hiding
    private bool ClosingOnSave;
    private bool Loop;
    private string SavedNote = string.Empty;
    private string SavedTitle = string.Empty;
    private CollegeDisplayArgs? Session;
    private bool SubmitArmed;
    private bool TuneDirty;

    public MusicComposerControl(TunePlayer player)
        : base("_nsett", false)
    {
        Name = "MusicComposer";
        Visible = false;
        UsesControlStack = true;
        Width = WIDTH;
        Height = HEIGHT;
        this.CenterOnScreen();
        Player = player;

        OkButton = CreateCloseButton(OnCloseClicked, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);
        CaptionLabel = Caption(string.Empty, LEFT, CAPTION_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gold);

        TitleBox = new CustomTextBox
        {
            X = LEFT,
            Y = TOP_ROW,
            Width = TITLE_WIDTH,
            Height = CustomButton.HEIGHT,
            MaxLength = CollegeProtocol.MAX_TITLE_CHARS,
            IsSelectable = true,
            IsTabStop = true,
            HintText = "Title"
        };

        AddChild(TitleBox);

        var x = LEFT + TITLE_WIDTH + GAP;
        ScaleButton = Tool(string.Empty, SCALE_WIDTH, x, TOP_ROW, () => Document.SetScale(TuneLabels.Next(Document.Scale)));
        x += SCALE_WIDTH + GAP;
        SpeedButton = Tool(string.Empty, SPEED_WIDTH, x, TOP_ROW, () => Document.SetSpeed(TuneLabels.Next(Document.Speed)));
        x += SPEED_WIDTH + GAP;

        InstrumentButton = Tool(
            string.Empty,
            INSTRUMENT_WIDTH,
            x,
            TOP_ROW,
            () => Document.SetInstrument(TuneLabels.Next(Document.Instrument)));

        Grid = new TuneGrid(GridLayout)
        {
            X = GRID_X,
            Y = GRID_Y,
            Document = Document,
            Editable = true
        };

        Grid.NotePlaced += (layer, row) => Player.Preview(Document.Scale, Document.Instrument, layer, row);

        Grid.Edited += () =>
        {
            DisarmSubmit();
            CloseArmed = false;
            RefreshControls();
        };

        Grid.BarClicked += bar => PlayFrom(bar * TuneGridLayout.COLUMNS_PER_BAR);
        AddChild(Grid);

        Document.Changed += () => TuneDirty = true;

        x = LEFT;

        foreach (var (layer, caption, width) in new[]
                 {
                     (TuneLayer.Melody, "Melody", 48),
                     (TuneLayer.Bass, "Bass", 36),
                     (TuneLayer.Drums, "Drums", 42)
                 })
        {
            LayerButtons[layer] = Tool(caption, width, x, ToolsY, () => Grid.SelectedLayer = layer);
            x += width + TOOL_GAP;
        }

        x += GROUP_GAP - TOOL_GAP;
        PlayButton = AddButton("Play", PLAY_WIDTH, x, ToolsY, OnPlayClicked);
        x += PLAY_WIDTH + TOOL_GAP;
        LoopButton = Tool("Loop", SMALL_TOOL_WIDTH, x, ToolsY, () => Loop = !Loop);
        x += SMALL_TOOL_WIDTH + TOOL_GAP;
        UndoButton = Tool("Undo", SMALL_TOOL_WIDTH, x, ToolsY, () => Document.Undo());
        x += SMALL_TOOL_WIDTH + TOOL_GAP;
        RedoButton = Tool("Redo", SMALL_TOOL_WIDTH, x, ToolsY, () => Document.Redo());
        x += SMALL_TOOL_WIDTH + GROUP_GAP;
        CopyButton = Tool("Copy bars 1-4 to 5-8", COPY_WIDTH, x, ToolsY, () => Document.CopyFirstHalf(Grid.SelectedLayer));
        x += COPY_WIDTH + TOOL_GAP;
        ClearButton = Tool("Clear layer", LEFT + INNER_WIDTH - x, x, ToolsY, () => Document.Clear(Grid.SelectedLayer));

        NoteBox = new CustomTextBox
        {
            X = LEFT,
            Y = NoteTop,
            Width = INNER_WIDTH,
            Height = NOTE_HEIGHT,
            IsMultiLine = true,
            IsSelectable = true,
            IsTabStop = true,
            MaxLength = CollegeProtocol.MAX_MUSIC_NOTE_CHARS,
            HintText = "Composer's note"
        };

        AddChild(NoteBox);

        var footerTextY = BUTTONS_TOP + ((CustomButton.HEIGHT - TextRenderer.CHAR_HEIGHT) / 2);
        SaveButton = AddButton("Save", SAVE_WIDTH, LEFT, BUTTONS_TOP, OnSaveClicked);
        SubmitButton = AddButton("Submit", SUBMIT_WIDTH, SUBMIT_X, BUTTONS_TOP, OnSubmitClicked);
        Status = Caption(string.Empty, STATUS_X, footerTextY, LEFT + INNER_WIDTH - COUNTER_WIDTH - 4 - STATUS_X);
        NoteCounter = Caption(string.Empty, LEFT + INNER_WIDTH - COUNTER_WIDTH, footerTextY, COUNTER_WIDTH, HorizontalAlignment.Right, LegendColors.Gray);
    }

    /// <summary>Raised with SaveDraft, Submit and HandIn.</summary>
    public event Action<CollegeActionArgs>? ActionRequested;

    public MusicComposerMode Mode { get; private set; }

    private bool IsDirty => TuneDirty || (TitleBox.Text != SavedTitle) || (NoteBox.Text != SavedNote);

    private bool IsMine => ReferenceEquals(Player.Owner, this);

    private string SubmitCaption
        => Mode == MusicComposerMode.HandIn ? "Hand in"
            : Session?.FreeEntries > 0 ? "Submit (free entry)"
            : $"Submit ({Session?.EntryCost} {Marks(Session?.EntryCost ?? 0)})";

    /// <summary>Opens a Music draft or hand-in (a null piece is a blank tune).</summary>
    public void Open(CollegeDisplayArgs args)
    {
        Session = args;
        Mode = args.Mode == CollegeWriterMode.HandIn ? MusicComposerMode.HandIn : MusicComposerMode.Draft;

        Document.Load(
            args.Piece?.Blocks.FirstOrDefault(b => b.Kind == CollegeBlockKind.Tune) is { } block
                ? TuneData.From(block)
                : TuneData.Empty);

        TitleBox.Text = args.Piece?.Title ?? string.Empty;
        NoteBox.Text = args.Piece?.Blocks.FirstOrDefault(b => b.Kind == CollegeBlockKind.Text)?.Text ?? string.Empty;
        Begin();
    }

    /// <summary>Applies a WriterResult for this window's session. True when a hidden window was shown again (a refused save).</summary>
    public bool ShowResult(CollegeDisplayArgs args)
    {
        if (Session is null || (args.Mode != Session.Mode) || (args.Subject != Session.Subject))
            return false;

        Status.Text = args.Message;

        switch (args.Result)
        {
            case CollegeWriterResult.Submitted or CollegeWriterResult.HandedIn:
                ClosingOnSave = false;
                MarkSaved();
                Hide();

                break;
            case CollegeWriterResult.Saved when ClosingOnSave:
                ClosingOnSave = false;

                //composing done while the save was on its way keeps the window open
                if (!IsDirty)
                    Hide();

                break;
            case CollegeWriterResult.Refused:
                ClosingOnSave = false;
                TuneDirty = true;

                if (!Visible)
                {
                    Show();

                    return true;
                }

                break;
        }

        return false;
    }

    public void SaveIfDirty()
    {
        if (Visible && Session is not null && (Mode == MusicComposerMode.Draft) && IsDirty)
            Send(CollegeActionType.SaveDraft);
    }

    public override void Hide()
    {
        if (!Visible)
            return;

        Player.StopIfOwner(this);
        TitleBox.IsFocused = false;
        NoteBox.IsFocused = false;
        base.Hide();
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            OnCloseClicked();
            e.Handled = true;

            return;
        }

        var typing = TitleBox.IsFocused || NoteBox.IsFocused;

        if (!typing && e.Accelerator && e.Keycode is Keycode.Z or Keycode.Y)
        {
            if (e.Keycode == Keycode.Z)
                Document.Undo();
            else
                Document.Redo();

            DisarmSubmit();
            RefreshControls();
            e.Handled = true;

            return;
        }

        if (!typing && !e.Accelerator && (e.Keycode == Keycode.Space))
        {
            OnPlayClicked();
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    public override void Update(GameTime gameTime)
    {
        if (Visible)
        {
            var counter = $"{NoteBox.Text.Length}/{CollegeProtocol.MAX_MUSIC_NOTE_CHARS}";

            if (NoteCounter.Text != counter)
                NoteCounter.Text = counter;

            if ((Mode == MusicComposerMode.Draft) && (TitleBox.Text != CaptionTitle))
            {
                CaptionTitle = TitleBox.Text;
                CaptionLabel.Text = DraftCaption(CaptionTitle);
            }

            var mine = IsMine;
            var play = !mine ? "Play" : Player.IsRendering ? "Wait..." : Player.IsPlaying ? "Stop" : "Play";

            if (PlayButton.Caption != play)
                PlayButton.Caption = play;

            Grid.Playhead = mine && Player.IsPlaying ? Player.Column : null;
        }

        base.Update(gameTime);
    }

    private void Begin()
    {
        Player.StopIfOwner(this);
        SavedTitle = TitleBox.Text;
        SavedNote = NoteBox.Text;
        TuneDirty = false;
        CloseArmed = false;
        ClosingOnSave = false;
        Loop = false;
        Grid.SelectedLayer = TuneLayer.Melody;
        TitleBox.IsFocused = false;
        NoteBox.IsFocused = false;
        SaveButton.Visible = Mode == MusicComposerMode.Draft;
        SubmitButton.X = Mode == MusicComposerMode.HandIn ? LEFT : SUBMIT_X;
        CaptionTitle = TitleBox.Text;

        CaptionLabel.Text = Mode == MusicComposerMode.HandIn
            ? OneLine(Session is { Prompt.Length: > 0 } session ? $"Hand-in: {session.Prompt}" : "Music: class hand-in")
            : DraftCaption(TitleBox.Text);

        Status.Text = string.Empty;
        DisarmSubmit();
        RefreshControls();
        Show();
    }

    private void RefreshControls()
    {
        ScaleButton.Caption = $"Scale: {TuneLabels.Scale(Document.Scale)}";
        SpeedButton.Caption = $"Speed: {TuneLabels.Speed(Document.Speed)}";
        InstrumentButton.Caption = $"Melody: {TuneLabels.Instrument(Document.Instrument)}";

        foreach (var (layer, button) in LayerButtons)
            button.Selected = Grid.SelectedLayer == layer;

        LoopButton.Selected = Loop;
        UndoButton.Enabled = Document.CanUndo;
        RedoButton.Enabled = Document.CanRedo;
    }

    private CustomButton Tool(string caption, int width, int x, int y, Action onClick)
        => AddButton(
            caption,
            width,
            x,
            y,
            () =>
            {
                onClick();
                DisarmSubmit();
                CloseArmed = false;
                RefreshControls();
            });

    private void OnPlayClicked()
    {
        if (IsMine && (Player.IsPlaying || Player.IsRendering))
        {
            Player.Stop();

            return;
        }

        PlayFrom(0);
    }

    private void PlayFrom(int column) => Player.Play(Document.Snapshot(), this, column, LoopSource);

    //asked at the end of each pass: the tune as it is now while Loop is on, so edits are heard on the next pass
    private TuneData? LoopSource() => Loop && Visible ? Document.Snapshot() : null;

    private void OnSaveClicked()
    {
        DisarmSubmit();
        CloseArmed = false;

        if ((Mode != MusicComposerMode.Draft) || Session is null)
            return;

        Status.Text = SAVING;
        Send(CollegeActionType.SaveDraft);
    }

    private void OnSubmitClicked()
    {
        CloseArmed = false;

        if (Session is null)
            return;

        if (Mode == MusicComposerMode.HandIn)
        {
            if (Document.NoteCount < CollegeProtocol.MIN_HAND_IN_NOTES)
            {
                Status.Text = MORE_NOTES;

                return;
            }

            Status.Text = "Handing in...";
            Send(CollegeActionType.HandIn);

            return;
        }

        if (TitleBox.Text.Trim().Length == 0)
        {
            DisarmSubmit();
            Status.Text = "Give your tune a title.";

            return;
        }

        if ((Document.MelodyNotes < CollegeProtocol.MIN_ENTRY_MELODY_NOTES)
            || (Document.MelodyRows < CollegeProtocol.MIN_ENTRY_MELODY_ROWS))
        {
            DisarmSubmit();
            Status.Text = MORE_MELODY;

            return;
        }

        if (!SubmitArmed)
        {
            SubmitArmed = true;
            SubmitButton.Caption = CONFIRM_SUBMIT_CAPTION;

            Status.Text = Session.FreeEntries > 0
                ? "Use your free entry? Click Yes, submit."
                : $"Enter it for {Session.EntryCost} {Marks(Session.EntryCost)}? Click Yes, submit.";

            return;
        }

        DisarmSubmit();
        Status.Text = "Submitting...";
        Send(CollegeActionType.Submit);
    }

    private void OnCloseClicked()
    {
        DisarmSubmit();

        //a second Close while the save is on its way hides anyway; a refusal still brings the piece back
        if ((Mode == MusicComposerMode.Draft) && Session is not null && !ClosingOnSave && IsDirty)
        {
            ClosingOnSave = true;
            Status.Text = SAVING;
            Send(CollegeActionType.SaveDraft);

            return;
        }

        if ((Mode == MusicComposerMode.HandIn) && IsDirty && !CloseArmed)
        {
            CloseArmed = true;
            Status.Text = "Close without handing in? Click Close again.";

            return;
        }

        ClosingOnSave = false;
        CloseArmed = false;
        Hide();
    }

    private void Send(CollegeActionType type)
    {
        var blocks = new List<CollegeBlockInfo>
        {
            Document.Snapshot()
                    .ToBlock()
        };

        if (NoteBox.Text.Trim().Length > 0)
            blocks.Add(new CollegeBlockInfo { Kind = CollegeBlockKind.Text, Text = NoteBox.Text });

        ActionRequested?.Invoke(
            new CollegeActionArgs
            {
                Type = type,
                Subject = CollegeSubjectCode.Music,
                Piece = new CollegePieceInfo
                {
                    Subject = CollegeSubjectCode.Music,
                    Title = TitleBox.Text.Trim(),
                    Blocks = blocks
                }
            });

        //marked saved as it is sent, so composing done while the reply is on its way still counts as a change
        MarkSaved();
    }

    private void MarkSaved()
    {
        SavedTitle = TitleBox.Text;
        SavedNote = NoteBox.Text;
        TuneDirty = false;
    }

    private void DisarmSubmit()
    {
        SubmitArmed = false;
        SubmitButton.Caption = SubmitCaption;
    }

    private static string DraftCaption(string title)
        => title.Trim().Length > 0 ? OneLine($"Music: \"{title.Trim()}\"") : "Music: draft";

    private static string OneLine(string text)
    {
        var lines = TextRenderer.WrapText(text, INNER_WIDTH - 8);

        return lines.Count <= 1 ? text : lines[0].TrimEnd() + "...";
    }

    private static string Marks(int count) => count == 1 ? "Mark" : "Marks";
}
```

  Notes for the implementer:
  - `Tool` captures `layer` from a `foreach` deconstruction; C# gives each iteration its own variable, so each lambda is correct.
  - `() => Document.Undo()` throws away the `bool`; this is allowed for an `Action`.
  - The **Clear layer** button takes the width that is left, which is 74 px. Check that **Clear layer** ends at x 544 (`LEFT + INNER_WIDTH`).

- [ ] **Step 8: Build and test.** Run the Verify commands. Expected: the tests pass and the build succeeds with no new warnings in these files.

```json:metadata
{"files": ["Chaos.Client/Controls/World/Popups/College/TuneGridLayout.cs", "Chaos.Client/ViewModel/College/TuneLabels.cs", "Chaos.Client/Controls/World/Popups/College/TuneGrid.cs", "Chaos.Client/Controls/World/Popups/College/MusicComposerControl.cs", "Tests/Chaos.Client.Tests/College/TuneGridLayoutTests.cs", "Tests/Chaos.Client.Tests/College/TuneLabelsTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\\Users\\Michael\\Documents\\GitHub\\worktrees\\college-music-server -- --treenode-filter \"/*/*/TuneGridLayoutTests/*\"", "acceptanceCriteria": ["composer grid 530x251, reader 384x138; hit tests, bars, gaps", "layout fits 560x464: top row, grids 52-303, toolbar 306, note 331-377, footer 391", "left places/drags/removes, right removes, cell click selects layer, bar click plays from bar", "Space/Ctrl+Z/Ctrl+Y outside text boxes; Escape closes", "Play/Wait.../Stop, playhead, Loop incl. turning off mid-play", "Copy/Clear on selected layer; Undo/Redo enabled state", "Draft and HandIn behaviours as the Art canvas", "client pre-checks with server texts", "Hide stops an owned tune"], "modelTier": "standard"}
```

---

### Task 9: Client reader, routing, tune cache and gallery Play (client repo)

**Goal:** The reader shows and plays tunes, and Shown tunes play at once. Music `OpenWriter` and `WriterResult` reach the composer. `Tune` replies fill a cache. The gallery's Music rows get **Play** buttons. A tune stops on map change and logout.

**Files:**
- Create: `Chaos.Client/Controls/World/Popups/College/TuneBlockView.cs`
- Create: `Chaos.Client/Controls/World/Popups/College/TuneRowButton.cs`
- Create: `Chaos.Client/Systems/College/CollegeTunes.cs`
- Create: `Chaos.Client/Systems/College/GalleryTunePlay.cs`
- Modify: `Chaos.Client/Controls/World/Popups/College/PieceView.cs`
- Modify: `Chaos.Client/Controls/World/Popups/College/CollegeReaderControl.cs`
- Modify: `Chaos.Client/Controls/World/Popups/College/CollegeListWindow.cs`
- Modify: `Chaos.Client/Controls/World/Popups/College/CollegeGalleryControl.cs`
- Modify: `Chaos.Client/Screens/WorldScreen.College.cs`
- Modify: `Chaos.Client/Screens/WorldScreen.cs` (`UnloadContent`, after `College?.Drawings.Clear();`)
- Modify: `Chaos.Client/Screens/WorldScreen.Map.cs` (`HandleMapInfo`, after `PumpkinWindow.Hide();`)
- Modify: `CLAUDE.md` (College control list)
- Test: `Tests/Chaos.Client.Tests/College/CollegeTunesTests.cs`, `Tests/Chaos.Client.Tests/College/GalleryTunePlayTests.cs`

**Acceptance Criteria:**
- [ ] `CollegeTunes`:
  - fetches a tune once while its reply is due, and again after 5 seconds with no reply;
  - keeps and announces an arriving tune;
  - `IsWaiting` is true only while a fetch is due;
  - `Clear` forgets everything.
- [ ] `GalleryTunePlay`:
  - a cached tune plays at once;
  - an uncached tune is fetched and its row waits;
  - pressing again while the fetch is due sends nothing;
  - a playing row stops;
  - only the waiting row plays when its tune arrives;
  - waiting ends after 5 seconds;
  - captions read Play, Wait and Stop.
- [ ] Reader:
  - The tune block shows "Dorian, Lively, Harp" with **Play**/**Stop** (`Wait...` while rendering) and the 384 x 138 grid, with a playhead while playing.
  - The composer's note follows as text.
  - The `Shown` context starts the tune at once.
  - Closing the reader, or opening another piece, stops it.
- [ ] Routing:
  - Music `OpenWriter` opens the composer, and Music `WriterResult` goes to it.
  - `Tune` replies go to `CollegeTunes`.
  - Logging out saves an open Music draft, clears the tune cache and stops the player.
  - A new map stops the player; a same-map refresh does not.
- [ ] Gallery Music tab:
  - Each of the 10 rows has a 40 x 14 **Play** button at the right.
  - Pressing it sends `TuneFetch` once if the tune is not cached, then plays the tune when it arrives.
  - The button reads **Wait** while waiting and **Stop** while its tune plays.
  - Another row's **Play** stops the first.
  - Closing the gallery stops a gallery tune.
  - Other tabs show no buttons, and every tab uses the columns 0 / 236 / 356.

**Verify:** `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\college-music-server -- --no-ansi` -> the whole client suite passes; `dotnet build Chaos.Client/Chaos.Client.csproj -p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\college-music-server` -> Build succeeded.

**Steps:**

- [ ] **Step 1: Write the failing tests.** Create `Tests/Chaos.Client.Tests/College/CollegeTunesTests.cs`:

```csharp
using Chaos.Client.Systems.College;
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class CollegeTunesTests
{
    private static readonly DateTime Start = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    private static CollegeDisplayArgs Tune(int id)
        => new()
        {
            Type = CollegeDisplayType.Tune,
            Id = id,
            Tune = new CollegeBlockInfo
            {
                Kind = CollegeBlockKind.Tune,
                Scale = TuneScale.Dorian,
                Speed = TuneSpeed.Lively,
                Instrument = TuneInstrument.Harp,
                Notes = [0, 3, 0, 2]
            }
        };

    [Test]
    public void A_tune_is_fetched_once_while_its_reply_is_due()
    {
        var tunes = new CollegeTunes();

        tunes.NeedsFetch(4, Start).Should().BeTrue();
        tunes.NeedsFetch(4, Start.AddSeconds(4.9)).Should().BeFalse();
    }

    [Test]
    public void A_tune_with_no_reply_is_fetched_again_after_five_seconds()
    {
        var tunes = new CollegeTunes();
        tunes.NeedsFetch(4, Start);

        tunes.NeedsFetch(4, Start.AddSeconds(5)).Should().BeTrue();
    }

    [Test]
    public void An_arriving_tune_is_kept_and_announced()
    {
        var tunes = new CollegeTunes();
        var ready = new List<int>();
        tunes.TuneReady += ready.Add;
        tunes.NeedsFetch(4, Start);

        tunes.OnTune(Tune(4));

        tunes.TryGet(4, out var tune).Should().BeTrue();
        tune!.Scale.Should().Be(TuneScale.Dorian);
        tune.Speed.Should().Be(TuneSpeed.Lively);
        tune.Instrument.Should().Be(TuneInstrument.Harp);
        tune.Notes.Should().Equal(new TuneNote(TuneLayer.Melody, 3, 0, 2));
        tunes.NeedsFetch(4, Start.AddSeconds(10)).Should().BeFalse();
        ready.Should().Equal(4);
    }

    [Test]
    public void A_tune_is_waiting_only_while_its_fetch_is_due()
    {
        var tunes = new CollegeTunes();

        tunes.IsWaiting(4, Start).Should().BeFalse();
        tunes.NeedsFetch(4, Start);
        tunes.IsWaiting(4, Start.AddSeconds(1)).Should().BeTrue();
        tunes.IsWaiting(4, Start.AddSeconds(5)).Should().BeFalse();

        tunes.OnTune(Tune(4));
        tunes.IsWaiting(4, Start.AddSeconds(1)).Should().BeFalse();
    }

    [Test]
    public void Clearing_forgets_tunes_and_requests()
    {
        var tunes = new CollegeTunes();
        tunes.NeedsFetch(4, Start);
        tunes.OnTune(Tune(5));

        tunes.Clear();

        tunes.TryGet(5, out _).Should().BeFalse();
        tunes.NeedsFetch(4, Start.AddSeconds(1)).Should().BeTrue();
    }
}
```

Create `Tests/Chaos.Client.Tests/College/GalleryTunePlayTests.cs`:

```csharp
using Chaos.Client.Systems.College;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class GalleryTunePlayTests
{
    private static readonly DateTime Start = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void A_cached_tune_plays_at_once()
    {
        var play = new GalleryTunePlay();

        play.Press(4, false, true, false, Start).Should().Be(TuneRowAction.Play);
        play.Pending.Should().Be(0);
    }

    [Test]
    public void An_uncached_tune_is_fetched_and_its_row_waits()
    {
        var play = new GalleryTunePlay();

        play.Press(4, false, false, true, Start).Should().Be(TuneRowAction.Fetch);
        play.Pending.Should().Be(4);
    }

    [Test]
    public void Pressing_again_while_the_fetch_is_due_sends_nothing()
    {
        var play = new GalleryTunePlay();
        play.Press(4, false, false, true, Start);

        play.Press(4, false, false, false, Start.AddSeconds(1)).Should().Be(TuneRowAction.None);
        play.Pending.Should().Be(4);
    }

    [Test]
    public void Pressing_a_playing_row_stops_it()
    {
        var play = new GalleryTunePlay();

        play.Press(4, true, true, false, Start).Should().Be(TuneRowAction.Stop);
    }

    [Test]
    public void Only_the_waiting_row_plays_when_its_tune_arrives()
    {
        var play = new GalleryTunePlay();
        play.Press(4, false, false, true, Start);

        play.Arrived(5).Should().BeFalse();
        play.Arrived(4).Should().BeTrue();
        play.Arrived(4).Should().BeFalse();
    }

    [Test]
    public void A_row_stops_waiting_after_five_seconds()
    {
        var play = new GalleryTunePlay();
        play.Press(4, false, false, true, Start);

        play.Tick(Start.AddSeconds(4.9));
        play.Pending.Should().Be(4);

        play.Tick(Start.AddSeconds(5));
        play.Pending.Should().Be(0);
        play.Arrived(4).Should().BeFalse();
    }

    [Test]
    public void Captions_read_play_wait_and_stop()
    {
        GalleryTunePlay.Caption(false, false).Should().Be("Play");
        GalleryTunePlay.Caption(false, true).Should().Be("Wait");
        GalleryTunePlay.Caption(true, false).Should().Be("Stop");
    }
}
```

- [ ] **Step 2: Run to see them fail.** Run the `CollegeTunesTests` filter. Expected: build errors for `CollegeTunes`, `GalleryTunePlay` and `TuneRowAction`.

- [ ] **Step 3: CollegeTunes.** Create `Chaos.Client/Systems/College/CollegeTunes.cs`:

```csharp
using System.Diagnostics.CodeAnalysis;
using Chaos.Client.ViewModel.College;
using Chaos.Networking.Entities.Server;

namespace Chaos.Client.Systems.College;

/// <summary>
///     Gallery tunes by entry id, kept until logout. A tune is fetched when someone presses its Play. A fetch with no
///     reply after <see cref="FETCH_TIMEOUT" /> (the server's fetch limit drops it silently) may be sent again.
/// </summary>
public sealed class CollegeTunes
{
    public static readonly TimeSpan FETCH_TIMEOUT = TimeSpan.FromSeconds(5);

    private readonly Dictionary<int, DateTime> Requested = new();
    private readonly Dictionary<int, TuneData> Tunes = new();

    public event Action<int>? TuneReady;

    /// <summary>True when the caller should send TuneFetch now; the request is then counted as sent at <paramref name="now" />.</summary>
    public bool NeedsFetch(int id, DateTime now)
    {
        if (Tunes.ContainsKey(id) || IsWaiting(id, now))
            return false;

        Requested[id] = now;

        return true;
    }

    /// <summary>A fetch for <paramref name="id" /> is on its way and not yet timed out.</summary>
    public bool IsWaiting(int id, DateTime now)
        => !Tunes.ContainsKey(id) && Requested.TryGetValue(id, out var sent) && ((now - sent) < FETCH_TIMEOUT);

    public void OnTune(CollegeDisplayArgs args)
    {
        Tunes[args.Id] = TuneData.From(args.Tune);
        Requested.Remove(args.Id);
        TuneReady?.Invoke(args.Id);
    }

    public bool TryGet(int id, [MaybeNullWhen(false)] out TuneData tune) => Tunes.TryGetValue(id, out tune);

    public void Clear()
    {
        Tunes.Clear();
        Requested.Clear();
    }
}
```

- [ ] **Step 4: GalleryTunePlay.** Create `Chaos.Client/Systems/College/GalleryTunePlay.cs`:

```csharp
namespace Chaos.Client.Systems.College;

public enum TuneRowAction
{
    None,
    Fetch,
    Play,
    Stop
}

/// <summary>
///     The gallery's Music rows: what a Play press does, and which row waits for its tune to arrive. A row waits at most
///     <see cref="CollegeTunes.FETCH_TIMEOUT" />, so a fetch the server dropped leaves its button reading Play again.
/// </summary>
public sealed class GalleryTunePlay
{
    private DateTime PendingSince;

    /// <summary>The entry whose tune is awaited (0 for none).</summary>
    public int Pending { get; private set; }

    /// <param name="playingThis">The row's own tune is playing or rendering.</param>
    /// <param name="cached">The tune is already in <see cref="CollegeTunes" />.</param>
    /// <param name="fetchDue">The cache said to send TuneFetch now.</param>
    public TuneRowAction Press(int id, bool playingThis, bool cached, bool fetchDue, DateTime now)
    {
        if (playingThis)
        {
            Pending = 0;

            return TuneRowAction.Stop;
        }

        if (cached)
        {
            Pending = 0;

            return TuneRowAction.Play;
        }

        Pending = id;
        PendingSince = now;

        return fetchDue ? TuneRowAction.Fetch : TuneRowAction.None;
    }

    /// <summary>True when the arriving tune is the awaited one; the caller then plays it.</summary>
    public bool Arrived(int id)
    {
        if ((id == 0) || (id != Pending))
            return false;

        Pending = 0;

        return true;
    }

    public void Tick(DateTime now)
    {
        if ((Pending != 0) && ((now - PendingSince) >= CollegeTunes.FETCH_TIMEOUT))
            Pending = 0;
    }

    public void Cancel() => Pending = 0;

    public static string Caption(bool playingThis, bool pending) => playingThis ? "Stop" : pending ? "Wait" : "Play";
}
```

- [ ] **Step 5: Run the tests.** Run the `CollegeTunesTests` and `GalleryTunePlayTests` filters. Expected: all pass.

- [ ] **Step 6: TuneBlockView.** Create `Chaos.Client/Controls/World/Popups/College/TuneBlockView.cs`:

```csharp
#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Systems.College;
using Chaos.Client.ViewModel.College;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>
///     A tune inside the reader: Play/Stop and the "Dorian, Lively, Harp" line, then the three grids read-only at 6 x 4
///     cells with the playhead while this block's tune plays.
/// </summary>
public sealed class TuneBlockView : UIPanel
{
    private const int BUTTON_WIDTH = 54;
    private const int SUMMARY_GAP = 6;
    private const int GRID_TOP = CustomButton.HEIGHT + 4;

    private readonly TuneGrid Grid;
    private readonly CustomButton PlayButton;
    private readonly TunePlayer Player;
    private readonly TuneData Tune;

    public TuneBlockView(TuneData tune, TunePlayer player, int width)
    {
        Tune = tune;
        Player = player;
        Width = width;
        Height = GRID_TOP + TuneGridLayout.Reader.Height;
        IsPassThrough = true;

        PlayButton = new CustomButton("Play", BUTTON_WIDTH);
        PlayButton.Clicked += Toggle;
        AddChild(PlayButton);

        AddChild(
            new UILabel
            {
                X = BUTTON_WIDTH + SUMMARY_GAP,
                Y = (CustomButton.HEIGHT - TextRenderer.CHAR_HEIGHT) / 2,
                Width = width - BUTTON_WIDTH - SUMMARY_GAP,
                Height = TextRenderer.CHAR_HEIGHT,
                ForegroundColor = LegendColors.Gray,
                IsHitTestVisible = false,
                Text = TuneLabels.Summary(tune)
            });

        Grid = new TuneGrid(TuneGridLayout.Reader)
        {
            X = Math.Max(0, (width - TuneGridLayout.Reader.Width) / 2),
            Y = GRID_TOP,
            Data = tune,
            IsHitTestVisible = false
        };

        AddChild(Grid);
    }

    private bool IsMine => ReferenceEquals(Player.Owner, this);

    public void Play() => Player.Play(Tune, this);

    public void Stop() => Player.StopIfOwner(this);

    public override void Update(GameTime gameTime)
    {
        var mine = IsMine;
        var caption = !mine ? "Play" : Player.IsRendering ? "Wait..." : Player.IsPlaying ? "Stop" : "Play";

        if (PlayButton.Caption != caption)
            PlayButton.Caption = caption;

        Grid.Playhead = mine && Player.IsPlaying ? Player.Column : null;
        base.Update(gameTime);
    }

    private void Toggle()
    {
        if (IsMine && (Player.IsPlaying || Player.IsRendering))
            Player.Stop();
        else
            Play();
    }
}
```

- [ ] **Step 7: PieceView.** In `PieceView.cs`:
  - Change the constructor to `public PieceView(CollegePictureTransfers transfers, TunePlayer player, int width, int height)`. Add the field `private readonly TunePlayer Player;` and set `Player = player;` after `Transfers = transfers;`.
  - Add the field `private TuneBlockView? TuneView;`.
  - In `Layout`'s switch, add after the `Drawing` case:

```csharp
                case CollegeBlockKind.Tune:
                    y += AddTune(block, y);

                    break;
```

  - Add the methods after `AddDrawing`:

```csharp
    private int AddTune(CollegeBlockInfo block, int y)
    {
        var view = new TuneBlockView(TuneData.From(block), Player, Content.Width)
        {
            X = 0,
            Y = y
        };

        Content.AddChild(view);
        TuneView = view;

        return view.Height;
    }

    /// <summary>Plays the piece's tune, if it has one (Show to class).</summary>
    public void PlayTune() => TuneView?.Play();

    /// <summary>Stops the piece's tune if it is the one playing.</summary>
    public void StopTune() => TuneView?.Stop();
```

  - At the top of `ClearContent`, before the `foreach`, add:

```csharp
        //a piece being replaced or closed takes its tune with it
        TuneView?.Stop();
        TuneView = null;
```

  - Change the class summary's first sentence to: `A read-only piece: headings in gold, word-wrapped text, pictures at their real size, drawings and tunes, top to bottom, scrolled by the bar or the mouse wheel.`

- [ ] **Step 8: CollegeReaderControl.** In `CollegeReaderControl.cs`:
  - Change the constructor to `public CollegeReaderControl(CollegePictureTransfers transfers, TextPopupControl votesPopup, TunePlayer player)` and the `PieceView` line to `View = new PieceView(transfers, player, INNER_WIDTH, ACTIONS_TOP - GAP - BODY_TOP)`.
  - In `Hide()`, after `CommentBox.IsFocused = false;`, add `View.StopTune();`.
  - In `Open`, after `Show();`, add:

```csharp

        //a Teacher's "Show to class" plays the tune for the whole room as the piece arrives
        if (args.Context == CollegePieceContext.Shown)
            View.PlayTune();
```

- [ ] **Step 9: TuneRowButton.** Create `Chaos.Client/Controls/World/Popups/College/TuneRowButton.cs`:

```csharp
#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>
///     A gallery Music row's small Play / Wait / Stop button. It fits inside a list row, which is shorter than a
///     <c>CustomButton</c>. It sits over the row, so its clicks never select or open the row.
/// </summary>
public sealed class TuneRowButton : UIPanel
{
    public const int WIDTH = 40;
    public const int HEIGHT = CollegeListRow.HEIGHT - 2;

    private readonly UILabel Label;

    public TuneRowButton()
    {
        Width = WIDTH;
        Height = HEIGHT;
        BackgroundColor = new Color(10, 8, 5);
        BorderColor = new Color(138, 116, 72);

        Label = new UILabel
        {
            X = 0,
            Y = (HEIGHT - TextRenderer.CHAR_HEIGHT) / 2,
            Width = WIDTH,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false,
            Text = "Play"
        };

        AddChild(Label);
    }

    public int Id { get; set; }

    public string Caption { get => Label.Text; set => Label.Text = value; }

    public event Action<int>? Pressed;

    public override void OnClick(ClickEvent e)
    {
        if (e.Button != MouseButton.Left)
            return;

        Pressed?.Invoke(Id);
        e.Handled = true;
    }

    public override void OnDoubleClick(DoubleClickEvent e) => e.Handled = true;
}
```

- [ ] **Step 10: CollegeListWindow.** In `CollegeListWindow.cs`:
  - add after `protected UILabel Empty { get; }`:

```csharp
    /// <summary>The y of the first row, for controls laid over the rows.</summary>
    protected int RowsTop { get; }
```

  - in the constructor, after `var rowsTop = headerTop + TextRenderer.CHAR_HEIGHT + 4;`, add `RowsTop = rowsTop;`.

- [ ] **Step 11: CollegeGalleryControl.** In `CollegeGalleryControl.cs`:
  - Add the usings `using Chaos.Client.Rendering;` (only if needed) and `using Chaos.Client.ViewModel.College;`.
  - Add the constant `private const int MUSIC_ROWS = 10;` and the fields:

```csharp
    private readonly TuneRowButton[] PlayButtons;
    private readonly TunePlayer Player;
    private readonly GalleryTunePlay TunePlay = new();
    private readonly Dictionary<int, object> TuneOwners = new();
    private readonly CollegeTunes Tunes;
```

  - Change the constructor signature and the base call's columns:

```csharp
    public CollegeGalleryControl(CollegeDrawings drawings, CollegeTunes tunes, TunePlayer player)
        : base(
            "CollegeGallery",
            TABBED_HEADER_TOP,
            MUSIC_ROWS,
            [("Title", 0), ("Author", 236), ("Award", 356)],
            "No pieces yet.")
```

  - At the end of the constructor, add:

```csharp
        Tunes = tunes;
        Player = player;
        Tunes.TuneReady += OnTuneReady;
        PlayButtons = new TuneRowButton[MUSIC_ROWS];

        for (var i = 0; i < MUSIC_ROWS; i++)
        {
            var button = new TuneRowButton
            {
                X = LEFT + INNER_WIDTH - TuneRowButton.WIDTH - 2,
                Y = RowsTop + (i * CollegeListRow.HEIGHT) + 1,
                Visible = false
            };

            button.Pressed += OnPlayPressed;
            AddChild(button);
            PlayButtons[i] = button;
        }
```

  - Add `private bool IsMusic => Subject == CollegeSubjectCode.Music;` after `IsArt`.
  - In `Dispose`, after `Drawings.DrawingReady -= OnDrawingReady;`, add `Tunes.TuneReady -= OnTuneReady;`.
  - Replace `PageShown` with:

```csharp
    protected override void PageShown(IReadOnlyList<CollegeGalleryRowInfo> shown)
    {
        if (Cells is not null)
            for (var i = 0; i < Cells.Length; i++)
            {
                var cell = Cells[i];
                cell.Visible = IsArt && (i < shown.Count);

                if (!cell.Visible)
                    continue;

                var row = shown[i];
                cell.Set(row.Id, row.Title, row.Author, CollegeTiers.Badge(row.Tier));
                cell.Picture = Drawings.TryGetTexture(row.Id, out var texture) ? texture : null;

                if ((cell.Picture is null) && Drawings.NeedsFetch(row.Id))
                    Raise(
                        new CollegeActionArgs
                        {
                            Type = CollegeActionType.DrawingFetch,
                            Id = row.Id
                        });
            }

        if (PlayButtons is null)
            return;

        for (var i = 0; i < PlayButtons.Length; i++)
        {
            var button = PlayButtons[i];
            button.Visible = IsMusic && (i < shown.Count);

            if (button.Visible)
                button.Id = shown[i].Id;
        }

        RefreshPlayButtons();
    }
```

  - Add the members:

```csharp
    public override void Hide()
    {
        if (!Visible)
            return;

        if (Player.Owner is { } owner && TuneOwners.ContainsValue(owner))
            Player.Stop();

        TunePlay.Cancel();
        base.Hide();
    }

    public override void Update(GameTime gameTime)
    {
        if (Visible && IsMusic)
        {
            TunePlay.Tick(DateTime.UtcNow);
            RefreshPlayButtons();
        }

        base.Update(gameTime);
    }

    //one owner object per entry, so a tune keeps its row's Stop across page turns
    private object TuneOwner(int id)
    {
        if (!TuneOwners.TryGetValue(id, out var owner))
            TuneOwners[id] = owner = new object();

        return owner;
    }

    private bool IsPlaying(int id)
        => ReferenceEquals(Player.Owner, TuneOwner(id)) && (Player.IsPlaying || Player.IsRendering);

    private void OnPlayPressed(int id)
    {
        if (id == 0)
            return;

        var now = DateTime.UtcNow;
        var cached = Tunes.TryGet(id, out var tune);

        switch (TunePlay.Press(id, IsPlaying(id), cached, !cached && Tunes.NeedsFetch(id, now), now))
        {
            case TuneRowAction.Stop:
                Player.Stop();

                break;
            case TuneRowAction.Play:
                Player.Play(tune!, TuneOwner(id));

                break;
            case TuneRowAction.Fetch:
                Raise(
                    new CollegeActionArgs
                    {
                        Type = CollegeActionType.TuneFetch,
                        Id = id
                    });

                break;
        }

        RefreshPlayButtons();
    }

    private void OnTuneReady(int id)
    {
        if (Visible && TunePlay.Arrived(id) && Tunes.TryGet(id, out var tune))
            Player.Play(tune, TuneOwner(id));

        RefreshPlayButtons();
    }

    private void RefreshPlayButtons()
    {
        foreach (var button in PlayButtons)
        {
            if (!button.Visible)
                continue;

            var caption = GalleryTunePlay.Caption(IsPlaying(button.Id), TunePlay.Pending == button.Id);

            if (button.Caption != caption)
                button.Caption = caption;
        }
    }
```

  - Change the class summary to: `The gallery: one subject's awarded pieces, in the order the server sent them (tier, then newest). The Art tab shows a picture grid of 8; the other tabs show 10 rows, and on the Music tab each row has a Play button. The subject tabs ask the server for that subject's list.`

- [ ] **Step 12: Routing.** In `WorldScreen.College.cs`:
  - In `SaveCollegeDraft`, add `College?.Composer.SaveIfDirty();`.
  - In `BuildCollege`, after `var drawings = new CollegeDrawings();`, add `var tunes = new CollegeTunes();`. In the initializer:
    - add `Tunes = tunes,` and the composer:

```csharp
            Composer = new MusicComposerControl(Game.TunePlayer)
            {
                ZIndex = 2
            },
```

    - change the gallery to `Gallery = new CollegeGalleryControl(drawings, tunes, Game.TunePlayer)`;
    - change the reader to `Reader = new CollegeReaderControl(transfers, votesPopup, Game.TunePlayer)`.
  - After `windows.Canvas.ActionRequested += SendCollegeAction;`, add `windows.Composer.ActionRequested += SendCollegeAction;`. After `Root.AddChild(windows.PictureCanvas);`, add `Root.AddChild(windows.Composer);`.
  - In `HandleCollegeDisplay`'s switch, add before `case CollegeDisplayType.OpenWriter:` (the general one):

```csharp
            case CollegeDisplayType.OpenWriter when args.Subject == CollegeSubjectCode.Music:
                BringToFront(College.Composer);
                College.Composer.Open(args);

                break;
```

    and before `case CollegeDisplayType.WriterResult:` (the general one):

```csharp
            case CollegeDisplayType.WriterResult when args.Subject == CollegeSubjectCode.Music:
                if (College.Composer.ShowResult(args))
                    BringToFront(College.Composer);

                break;
```

    and after the `Drawing` case:

```csharp
            case CollegeDisplayType.Tune:
                College.Tunes.OnTune(args);

                break;
```

  - In `CollegeWindows`, add `public required MusicComposerControl Composer { get; init; }` and `public required CollegeTunes Tunes { get; init; }` (keep the alphabetical order of the properties).
  - Change the class summary to `/// <summary>The Mileth College windows: writer, Art canvas, Music composer, reader, judging list, gallery and hand-in list.</summary>`.

- [ ] **Step 13: Logout and map change.**
  - In `WorldScreen.cs` `UnloadContent`, after `College?.Drawings.Clear();`, add:

```csharp
        College?.Tunes.Clear();
        Game.TunePlayer.Stop();
```

  - In `WorldScreen.Map.cs` `HandleMapInfo`, after `PumpkinWindow.Hide();` (the new-map branch, not the same-map refresh above it), add:

```csharp

        //a tune from the old map (the composer, a reader, the gallery or a class showing) ends with it
        Game.TunePlayer.Stop();
```

- [ ] **Step 14: CLAUDE.md.** In the `College/` entry of the **Popups** paragraph, replace `and `CollegeDrawings` keeps gallery thumbnails).` with:

```
and `CollegeDrawings` keeps gallery thumbnails; MusicComposerControl — the Music composer (Draft and HandIn modes; TuneGrid draws the stacked Melody, Bass and Drums grids from TuneGridLayout's maths, and the reader's TuneBlockView shows it read-only); TuneRowButton — a gallery Music row's Play button; the tune model is `ViewModel/College/TuneDocument` (with TuneData, TuneScales and TuneLabels), sound comes from `Systems/College/TuneSynth` through `TunePlayer`, and `CollegeTunes` keeps gallery tunes with `GalleryTunePlay` deciding what a row's Play does).
```

- [ ] **Step 15: Build and test.** Run the Verify commands. Expected: Build succeeded; the whole client suite passes.

```json:metadata
{"files": ["Chaos.Client/Controls/World/Popups/College/TuneBlockView.cs", "Chaos.Client/Controls/World/Popups/College/TuneRowButton.cs", "Chaos.Client/Systems/College/CollegeTunes.cs", "Chaos.Client/Systems/College/GalleryTunePlay.cs", "Chaos.Client/Controls/World/Popups/College/PieceView.cs", "Chaos.Client/Controls/World/Popups/College/CollegeReaderControl.cs", "Chaos.Client/Controls/World/Popups/College/CollegeListWindow.cs", "Chaos.Client/Controls/World/Popups/College/CollegeGalleryControl.cs", "Chaos.Client/Screens/WorldScreen.College.cs", "Chaos.Client/Screens/WorldScreen.cs", "Chaos.Client/Screens/WorldScreen.Map.cs", "CLAUDE.md", "Tests/Chaos.Client.Tests/College/CollegeTunesTests.cs", "Tests/Chaos.Client.Tests/College/GalleryTunePlayTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\\Users\\Michael\\Documents\\GitHub\\worktrees\\college-music-server -- --no-ansi", "acceptanceCriteria": ["CollegeTunes: once per due fetch, again after 5 s, keeps/announces, IsWaiting, Clear", "GalleryTunePlay: play/fetch/none/stop, only waiting row plays on arrival, 5 s wait, captions", "reader tune block with summary, Play/Stop/Wait..., grid and playhead; Shown auto-plays; close or replace stops", "Music OpenWriter/WriterResult to composer; Tune to cache; logout saves draft, clears cache, stops player; new map stops, same-map refresh does not", "gallery Music rows: Play button, one TuneFetch, play on arrival, Wait/Stop captions, another row stops the first, closing stops; columns 0/236/356"], "modelTier": "standard"}
```

---

### Task 10: Full checks, docs and the spec record (both repos)

**Goal:** Both suites are at their baselines, both builds are clean, the client docs list the new parts, and the spec records the plan-time adjustments.

**Files:**
- Modify (client worktree): `CLAUDE.md` (College line under Popups; SoundSystem line under Game Systems)
- Modify (client main checkout): `docs/superpowers/specs/2026-10-05-mileth-college-music-design.md`

**Acceptance Criteria:**
- [ ] Server: `dotnet build Chaos.slnx` succeeds. The full suite fails only the known baseline tests.
- [ ] Client: the `Chaos.Client.csproj` build succeeds and the full client suite passes
- [ ] `CLAUDE.md` names `MusicComposerControl`, `TuneGrid`, `TuneDocument`, `TuneScales`, `TuneSynth`, `TunePlayer` and `CollegeTunes`; its SoundSystem bullet mentions the tune channel and the music fade
- [ ] The spec lists the plan-time adjustments below

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj` in the server worktree -> only baseline failures. `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\college-music-server -- --no-ansi` in the client worktree -> all pass.

**Steps:**

- [ ] **Step 1: Server.** In the server worktree, run `dotnet build Chaos.slnx`, then the full suite. Record the pass and fail counts. Any failure outside the baseline is a regression: fix it before going on.

- [ ] **Step 2: Client.** In the client worktree, build `Chaos.Client/Chaos.Client.csproj` with `UnoraServerPath`, then run the full client suite. Record the counts.

- [ ] **Step 3: Client CLAUDE.md.** Task 9 already extended the `College/` entry. Check that it names
  `MusicComposerControl`, `TuneGrid`, `TuneDocument`, `TuneScales`, `TuneSynth`, `TunePlayer` and `CollegeTunes`, and
  add any that are missing. Then add to the `SoundSystem` bullet under Game Systems: "The last mixer channel is kept for
  College tunes (`PlayTune`/`PlayPreview`/`StopTune`, through `ITuneOutput`); map music fades out while a tune plays
  (`MusicDuck`) and back after."

- [ ] **Step 4: Spec record.** In the main client checkout (`C:\Users\Michael\Documents\GitHub\Chaos.Client`), add a section before "## Testing" in `docs/superpowers/specs/2026-10-05-mileth-college-music-design.md`:

```markdown
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
```

  Leave the spec uncommitted; Task 11 commits it with the plan.

```json:metadata
{"files": ["CLAUDE.md", "docs/superpowers/specs/2026-10-05-mileth-college-music-design.md"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj", "acceptanceCriteria": ["server build + suite at baseline", "client build + suite pass", "CLAUDE.md lists the new controls, model and systems", "spec records the plan-time adjustments"], "modelTier": "standard"}
```

---

### Task 11: Commit the full implementation

**Goal:** One commit per repo on `feat/college-music`. The client's `Chaos-Server` gitlink points at the server commit. The spec update and plan status are committed on client main.

**Files:**
- All changes from Tasks 1-10

**Acceptance Criteria:**
- [ ] Server and client each have exactly one new commit on `feat/college-music`, with explicit-path staging only
- [ ] The client commit's `Chaos-Server` gitlink equals the server commit
- [ ] Nothing is pushed

**Verify:** `git -C <each worktree> log --oneline -2` shows the new commit on top of its base. `git -C C:\Users\Michael\Documents\GitHub\worktrees\college-music-client ls-tree HEAD Chaos-Server` shows the server commit's SHA.

**Steps:**

- [ ] **Step 1: Server commit.** In the server worktree, run `git status --short` and stage each changed and new file by its path (from Tasks 1-4). Write the message to a file with UTF-8 without BOM, then run `git commit -F <file>`:

```
Mileth College part 4: Music pieces, tune blocks and TuneFetch

Music opens: a Music piece is a title, one tune (scale, speed, melody instrument and up to 512 notes on a 64-column
grid of melody, bass and drums) and a 300-character note. Submit needs 16 melody notes on 3 rows, a hand-in 8 notes.
Every subject now has activity classes. TuneFetch sends one claimed tune for the gallery's Play buttons and shares
the fetch limit with DrawingFetch. CLIENT_VERSION 774.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01FtxoAKkUAaqcCKBWNB2Gmz
```

- [ ] **Step 2: Client commit.** In the client worktree, point the submodule at the server commit with `git update-index --cacheinfo 160000,<server sha>,Chaos-Server`. Stage the client files by path (Tasks 5-10, including `CLAUDE.md`), then commit:

```
Mileth College part 4: the Music composer

MusicComposerControl (Draft and HandIn modes) on a pure TuneDocument model: three stacked grids (melody, bass,
drums), held notes, six scales, five speeds, four melody instruments, copy bars, loop, 50-step undo. TuneSynth renders
a tune with modelled instruments (plucked strings, flute, bells, hand drums) into one 48 kHz buffer; TunePlayer plays
it on a reserved mixer channel and fades the map music. The reader plays tunes, Show to class plays for the room, and
the gallery's Music rows get Play buttons fed by TuneFetch. Chaos-Server points at the part 4 server commit.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01FtxoAKkUAaqcCKBWNB2Gmz
```

- [ ] **Step 3: Spec commit.** In the main client checkout, run `git add -f docs/superpowers/specs/2026-10-05-mileth-college-music-design.md docs/superpowers/plans/2026-10-05-mileth-college-music.md docs/superpowers/plans/2026-10-05-mileth-college-music.md.tasks.json`. Commit with message `Mileth College part 4: spec record and plan status`, plus the two attribution lines.

- [ ] **Step 4: Report.** Tell the user:
  - the three commit SHAs;
  - that nothing is pushed;
  - the merge order: server, then client;
  - the in-game checklist from the spec's Testing section.

```json:metadata
{"files": [], "verifyCommand": "git -C C:\\Users\\Michael\\Documents\\GitHub\\worktrees\\college-music-client ls-tree HEAD Chaos-Server", "acceptanceCriteria": ["one commit per repo on feat/college-music", "client gitlink = server commit", "spec/plan committed on client main", "nothing pushed"], "modelTier": "mechanical"}
```
