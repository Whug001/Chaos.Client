# Mileth College Part 2 (Entries, Judging, Writing, Gallery) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Players write College entries with headings and uploaded pictures, judges vote, the Director posts verdicts, authors claim titles, emblems and legend marks, and awarded pieces show in a gallery; writing classes get prompts and hand-ins.

**Architecture:** The server keeps entry bookkeeping in the existing `CollegeState.json` and each piece in its own JSON file, with pictures stored once by SHA-256 hash. Pure rule classes (`PieceRules`, `EntryBook`, `PictureFormat`, `CollegeRewardPlan`) hold every rule and are unit tested. `CollegeService` applies them under its lock. One new message pair (`CollegeAction` 144 / `CollegeDisplay` 152, each with a sub-type byte) carries everything to the client. The client adds a writing window, one shared reader window, and the judging, gallery and hand-in lists.

**Tech Stack:** C# 14 / .NET 10, TUnit + FluentAssertions + Moq (server and client tests), MonoGame + SkiaSharp 3.116 (client), Python 3 + pytest (Unora map tool), PixelLab MCP (emblem art).

**Spec:** `Chaos.Client/docs/superpowers/specs/2026-10-05-mileth-college-entries-design.md`. Also read the parent design `2026-10-04-mileth-college-design.md` and the part 1 spec `2026-10-05-mileth-college-core-design.md` in the same folder.

## Global Constraints

- **Branches and folders.** All paths below are relative to the repo named in each task:
  - server: `C:\Users\Michael\Documents\GitHub\worktrees\college-server` (branch `feat/college-core`, on top of 69f8fbe6b);
  - Unora: `C:\Users\Michael\Documents\GitHub\worktrees\college-unora` (branch `feat/college-core`, on top of a460b5a7d);
  - client: `C:\Users\Michael\Documents\GitHub\worktrees\college-client` (created in Task 12, branch `feat/college-core` from client `main`).
- **Shared checkouts.** Other Claude sessions use the main checkouts. Never run `git stash`, `git reset`, `git add -A` or `git add .` anywhere. Stage by explicit path.
- **Builds.** A running `Chaos.exe` or game client locks `bin/`. If a build fails with a file lock, ask the user to stop it. Never build the server and client solutions at the same time.
- **Client builds in a worktree:** build `Chaos.Client/Chaos.Client.csproj` (not the `.slnx`) with `-p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\college-server`.
- **Commits:** this plan uses one commit per repo at the end (Task 18). Implementers must NOT commit. Leave all changes in the working tree.
- **Tests:** server tests run with `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/<Class>/*"`. Client tests run with `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=<server worktree> -- --treenode-filter "/*/*/<Class>/*"`. `dotnet test` does not work.
- **Known server baseline:** the full server suite fails only `OnItemDroppedOn` stackable (and `GiveAbility` if it fails on master). Anything else failing is a regression.
- **Code style:** match the surrounding code. Use `Lock` with `EnterScope()`, never `lock`. Comments only for a non-obvious "why". No explanatory comments in tests. Test methods use the `Snake_case_sentence` names that `Tests/Chaos.Tests/College/*` use.
- **Limits (both sides agree through `CollegeProtocol`):** title 40 characters; heading 60; text 10,000 in all; 5 pictures; 200 blocks; pictures at most 400 x 300 and 204,800 bytes, PNG or JPEG; parts of 32,768 bytes; Submit needs a title and 200 characters; a hand-in needs 20 characters; judge comment 300; verdict note 500; class prompt 120.
- **Opcodes:** `ClientOpCode.CollegeAction = 144`, `ServerOpCode.CollegeDisplay = 152`. `CLIENT_VERSION` 771 -> 772.
- **Emblem art numbers:** 259-282, in subject order Art, Music, Literature, History, Lore, Philosophy and tier order Clave, Village, Kingdom, Aisling.
- **Dialog text limits (Unora JSON):** option text 35 characters, dialog text 360, no em dashes. Orange bar text 45 characters at most.

**User decisions (already made):**
- Entries hold up to 10,000 characters and up to 5 pictures uploaded from the player's PC, shrunk to fit 400 x 300.
- One long scroll; pictures sit on their own lines between text blocks.
- Plain text with line breaks plus one heading style.
- Titles are "{Tier} {Noun}": Artist, Musician, Wordsmith (Literature), Historian, Lorekeeper, Philosopher.
- Emblems: six designs (brush, lute, quill, scroll, tome, eye) in four tier colours (bronze, silver, gold, pale blue): 24 emblems.
- Writing activity class: the Teacher sets a prompt, students write and hand in, the Teacher reads hand-ins and can show one to the class.
- One gallery stand per subject, opening the gallery on that subject.
- The author sees only the Director's note. Judges' comments stay private to the Director and admins.
- Pictures travel through the game connection in 32 KB parts, cached by content hash on both sides.
- Art and Music use the same entry system but open in parts 3 and 4; until then the Registrar says "Opens soon".
- Withdrawn entries, and entries that got No award, keep their marks spent. Removing a gallery piece does not remove the award.
- The award (and teaching rights) is recorded at claim, not at the verdict.

## Deferred decision (asked during execution)

- **Gallery stand art (Task 10).** The spec says the user sees the stand art in the map tool's preview before it's fixed. The plan starts every stand on the Town Hall book lectern (foreground 11017/11018). Task 10 renders the hall and sends the preview to the user. Ask: "Keep the book lectern on all six stands, or pick different furniture for Art and Music?" Keeping it changes nothing else. Changing it only changes the `STAND_ART` constants in `college_maps.py`.

## File structure

**Server (`worktrees/college-server`)**

| File | Responsibility |
|---|---|
| `Chaos.DarkAges/Definitions/CollegeProtocol.cs` (new) | Shared limits and `PartCount` |
| `Chaos.DarkAges/Definitions/Enums.cs` | College sub-type and code enums (new region) |
| `Chaos.Networking.Abstractions/Definitions/Enums.cs` | Opcodes 144 / 152 |
| `Chaos.Networking/Entities/Server/CollegeInfos.cs` (new) | Piece, block and row info types shared by both messages |
| `Chaos.Networking/Entities/Client/CollegeActionArgs.cs` (new) | Client-to-server message |
| `Chaos.Networking/Entities/Server/CollegeDisplayArgs.cs` (new) | Server-to-client message |
| `Chaos.Networking/Converters/CollegeCodec.cs` (new) | Reads and writes pieces and rows |
| `Chaos.Networking/Converters/Client/CollegeActionConverter.cs` (new) | Converter |
| `Chaos.Networking/Converters/Server/CollegeDisplayConverter.cs` (new) | Converter |
| `Chaos/Services/College/CollegePiece.cs` (new) | Server piece model |
| `Chaos/Services/College/PieceRules.cs` (new) | Piece limits and cleanup (pure) |
| `Chaos/Services/College/PictureFormat.cs` (new) | PNG/JPEG header reading and hashing (pure) |
| `Chaos/Services/College/CollegePictureStore.cs` (new) | Picture files and the unused-picture sweep |
| `Chaos/Services/College/PictureUploads.cs` (new) | Part assembly and upload caps |
| `Chaos/Services/College/CollegePieceStore.cs` (new) | Piece files |
| `Chaos/Services/College/EntryBook.cs` (new) | Entry rules (pure) |
| `Chaos/Services/College/CollegeEntryModels.cs` (new) | Entry, vote, verdict, hand-in and view records |
| `Chaos/Services/College/CollegeService.Entries.cs` (new) | Service members for drafts, entries, hand-ins, galleries |
| `Chaos/Services/College/ICollegeMail.cs`, `CollegeMail.cs` (new) | Mail to offline players |
| `Chaos/Services/College/CollegeRewardPlan.cs` (new) | Award -> mark, title, emblem (pure) |
| `Chaos/Services/College/CollegeRewards.cs` (new) | Applies the plan to an online `Aisling` |
| `Chaos/Services/College/CollegePanel.cs` (new) | Builds and sends `CollegeDisplay`; routes `CollegeAction` |
| `Chaos/Services/College/CollegeState.cs`, `CollegeOptions.cs`, `CollegeText.cs`, `CollegeService.cs` | Additions |
| `Chaos/Scripting/ReactorTileScripts/Temauir/College/CollegeGalleryStandScript.cs` (new) | Gallery stand |
| `Chaos/Scripting/DialogScripts/Temuair/College/CollegeRegistrarScript.cs`, `CollegeLecternScript.cs` | Menu additions |
| `Chaos/Messaging/Admin/CollegeCommand.cs` | `entries`, `entry`, `removeentry`, `closevoting`; award sync |
| `Chaos/Scripting/AislingScripts/DefaultAislingScript.cs` | Login reward sync |
| `Chaos/Services/Servers/WorldServer.cs`, `Chaos/Networking/...ChaosWorldClient.cs`, `Chaos/Extensions/ServiceCollectionExtensions.cs` | Wiring |
| `Tests/Chaos.Tests/College/*Tests.cs`, `Tests/Chaos.Tests/Networking/CollegePacketConverterTests.cs` | Tests |

**Unora (`worktrees/college-unora`)**: `Tools/College/college_maps.py` and its tests (stands), College dialog JSON, `Templates/Emblems/college_*.json`, `Tools/Emblems/art/custom/embl259-282.png`, `Tools/College/emblem_tiers.py` (new recolour script).

**Client (`worktrees/college-client`)**

| File | Responsibility |
|---|---|
| `Chaos.Client.Networking/ConnectionManager.cs`, `Definitions/Delegates.cs` | Handler, event, send |
| `Chaos.Client/ViewModel/College/WritingDocument.cs` (new) | Editor block model (pure) |
| `Chaos.Client/Systems/College/PicturePrep.cs` (new) | Shrink and encode rules |
| `Chaos.Client/Systems/College/CollegePictureCache.cs` (new) | On-disk cache with a 50 MB cap |
| `Chaos.Client/Systems/College/PictureFilePicker.cs` (new) | Windows open-file dialog on its own STA thread |
| `Chaos.Client/Systems/College/CollegePictureTransfers.cs` (new) | Upload and download bookkeeping, textures by hash |
| `Chaos.Client/Controls/World/Popups/College/*.cs` (new) | Writer, reader, judging, gallery, hand-in windows, shared `PieceView` |
| `Chaos.Client/Screens/WorldScreen.College.cs` (new) | Wiring |
| `Tests/Chaos.Client.Tests/College/*Tests.cs` (new) | Tests |

---

### Task 1: College message protocol (server worktree)

**Goal:** Add the `CollegeAction` / `CollegeDisplay` message pair, its shared limits and enums, and round-trip tests, and raise `CLIENT_VERSION` to 772.

**Files:**
- Create: `Chaos.DarkAges/Definitions/CollegeProtocol.cs`
- Modify: `Chaos.DarkAges/Definitions/Enums.cs` (append a `#region Mileth College`)
- Modify: `Chaos.Networking.Abstractions/Definitions/Enums.cs` (after `TowerLeaderboardRequest = 143` and after `TowerLeaderboard = 151`)
- Modify: `Chaos.DarkAges/Definitions/CONSTANTS.cs` (`CLIENT_VERSION = 772`)
- Create: `Chaos.Networking/Entities/Server/CollegeInfos.cs`, `Chaos.Networking/Entities/Client/CollegeActionArgs.cs`, `Chaos.Networking/Entities/Server/CollegeDisplayArgs.cs`
- Create: `Chaos.Networking/Converters/CollegeCodec.cs`, `Chaos.Networking/Converters/Client/CollegeActionConverter.cs`, `Chaos.Networking/Converters/Server/CollegeDisplayConverter.cs`
- Modify: `Chaos/Networking/Abstractions/IChaosWorldClient.cs` and `Chaos/Networking/ChaosWorldClient.cs` (`SendCollegeDisplay`)
- Test: `Tests/Chaos.Tests/Networking/CollegePacketConverterTests.cs` (new); `Tests/Chaos.Tests/Networking/GuildEmblemPacketConverterTests.cs` (version test)

**Acceptance Criteria:**
- [ ] Every `CollegeActionType` and `CollegeDisplayType` value round-trips through its converter with all of its fields.
- [ ] An undefined sub-type byte throws `ArgumentOutOfRangeException` on read.
- [ ] `CONSTANTS.CLIENT_VERSION` is 772 and the version test says so.
- [ ] The existing opcode-uniqueness test still passes with 144 and 152 added.

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/CollegePacketConverterTests/*"` -> all pass; then `--treenode-filter "/*/*/GuildEmblemPacketConverterTests/*"` -> all pass.

**Steps:**

- [ ] **Step 1: Add the shared limits.** Create `Chaos.DarkAges/Definitions/CollegeProtocol.cs`:

```csharp
namespace Chaos.DarkAges.Definitions;

/// <summary>
///     Limits for Mileth College pieces and pictures that the client and the server must agree on. The server's
///     <c>CollegeOptions</c> defaults come from here.
/// </summary>
public static class CollegeProtocol
{
    /// <summary>The largest picture part, in bytes. One game message holds at most 64 KB.</summary>
    public const int PART_SIZE = 32_768;

    public const int MAX_PICTURE_BYTES = 204_800;
    public const int MAX_PICTURE_WIDTH = 400;
    public const int MAX_PICTURE_HEIGHT = 300;
    public const int MAX_PICTURES = 5;
    public const int MAX_TEXT_CHARS = 10_000;
    public const int MAX_TITLE_CHARS = 40;
    public const int MAX_HEADING_CHARS = 60;
    public const int MAX_BLOCKS = 200;
    public const int MIN_ENTRY_CHARS = 200;
    public const int MIN_HAND_IN_CHARS = 20;
    public const int MAX_COMMENT_CHARS = 300;
    public const int MAX_NOTE_CHARS = 500;
    public const int MAX_PROMPT_CHARS = 120;

    /// <summary>A SHA-256 hash in lower-case hex.</summary>
    public const int HASH_CHARS = 64;

    /// <summary>The tier byte for "no vote yet".</summary>
    public const byte NO_TIER = byte.MaxValue;

    /// <summary>How many parts a picture of <paramref name="length" /> bytes is sent in.</summary>
    public static int PartCount(int length) => (length + PART_SIZE - 1) / PART_SIZE;
}
```

- [ ] **Step 2: Add the enums.** Append to `Chaos.DarkAges/Definitions/Enums.cs` (after the last `#endregion`):

```csharp
#region Mileth College
/// <summary>A College subject on the wire. Same order as the server's <c>CollegeSubject</c>.</summary>
public enum CollegeSubjectCode : byte
{
    Art = 0,
    Music = 1,
    History = 2,
    Literature = 3,
    Lore = 4,
    Philosophy = 5
}

/// <summary>An award tier on the wire. Same order as the server's <c>AwardTier</c>.</summary>
public enum CollegeTierCode : byte
{
    None = 0,
    Clave = 1,
    Village = 2,
    Kingdom = 3,
    Aisling = 4
}

/// <summary>What a <c>CollegeAction</c> message asks for.</summary>
public enum CollegeActionType : byte
{
    SaveDraft = 0,
    Submit = 1,
    HandIn = 2,
    PictureCheck = 3,
    PicturePart = 4,
    PictureFetch = 5,
    OpenPiece = 6,
    Vote = 7,
    Verdict = 8,
    RemoveEntry = 9,
    GalleryPage = 10,
    ShowToClass = 11
}

/// <summary>What a <c>CollegeDisplay</c> message does.</summary>
public enum CollegeDisplayType : byte
{
    OpenWriter = 0,
    WriterResult = 1,
    PictureReply = 2,
    PicturePart = 3,
    JudgingList = 4,
    Piece = 5,
    GalleryList = 6,
    HandInList = 7
}

public enum CollegeWriterMode : byte
{
    Draft = 0,
    HandIn = 1
}

public enum CollegeWriterResult : byte
{
    Saved = 0,
    Submitted = 1,
    HandedIn = 2,
    Refused = 3
}

public enum CollegePictureReply : byte
{
    /// <summary>The server doesn't have the picture; send its parts.</summary>
    Send = 0,

    /// <summary>The server already has the picture; nothing to send.</summary>
    Have = 1,
    Accepted = 2,
    Refused = 3
}

/// <summary>Where a piece the client asks for comes from.</summary>
public enum CollegePieceSource : byte
{
    Entry = 0,
    Gallery = 1,
    HandIn = 2
}

/// <summary>Which footer the reader window shows.</summary>
public enum CollegePieceContext : byte
{
    Judge = 0,
    Verdict = 1,
    Gallery = 2,
    HandIn = 3,
    Shown = 4,
    Own = 5
}

public enum CollegeBlockKind : byte
{
    Text = 0,
    Heading = 1,
    Picture = 2
}
#endregion
```

- [ ] **Step 3: Add the opcodes.** In `Chaos.Networking.Abstractions/Definitions/Enums.cs`, after `TowerLeaderboardRequest = 143,` add:

```csharp

    /// <summary>
    ///     A Mileth College action: save or submit a draft, hand in, upload or fetch a picture, open a piece, vote, post a
    ///     verdict, remove an entry, turn a gallery page or show a hand-in. The first byte is a <c>CollegeActionType</c>.
    ///     <br />
    ///     Hex value: 0x90
    /// </summary>
    CollegeAction = 144,
```

After `TowerLeaderboard = 151,` add:

```csharp

    /// <summary>
    ///     A Mileth College window update: open the writer, a save result, a picture reply or part, the judging list, one
    ///     piece, the gallery or the hand-in list. The first byte is a <c>CollegeDisplayType</c>.
    ///     <br />
    ///     Hex value: 0x98
    /// </summary>
    CollegeDisplay = 152,
```

In `Chaos.DarkAges/Definitions/CONSTANTS.cs` change `CLIENT_VERSION = 771` to `CLIENT_VERSION = 772`. In `Tests/Chaos.Tests/Networking/GuildEmblemPacketConverterTests.cs` rename `Client_version_is_771` to `Client_version_is_772` and expect `772`.

- [ ] **Step 4: Add the info types.** Create `Chaos.Networking/Entities/Server/CollegeInfos.cs`:

```csharp
using Chaos.DarkAges.Definitions;

namespace Chaos.Networking.Entities.Server;

/// <summary>One block of a piece: text (may hold line breaks), a one-line heading, or a picture named by its hash.</summary>
public sealed record CollegeBlockInfo
{
    public CollegeBlockKind Kind { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
}

/// <summary>A piece of College work as both sides send it.</summary>
public sealed record CollegePieceInfo
{
    public CollegeSubjectCode Subject { get; set; }
    public string Title { get; set; } = string.Empty;
    public List<CollegeBlockInfo> Blocks { get; set; } = [];
}

/// <summary>One judging-list row. The author is never sent.</summary>
public sealed record CollegeJudgingRowInfo
{
    public int Id { get; set; }
    public CollegeSubjectCode Subject { get; set; }
    public string Title { get; set; } = string.Empty;
    public byte DaysLeft { get; set; }

    /// <summary>The viewer's vote, or <see cref="CollegeProtocol.NO_TIER" />.</summary>
    public byte MyTier { get; set; } = CollegeProtocol.NO_TIER;

    /// <summary>True for an entry awaiting a verdict (Director and admins only).</summary>
    public bool Waiting { get; set; }
}

public sealed record CollegeVoteInfo
{
    public string Judge { get; set; } = string.Empty;
    public CollegeTierCode Tier { get; set; }
    public string Comment { get; set; } = string.Empty;
}

public sealed record CollegeGalleryRowInfo
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public CollegeTierCode Tier { get; set; }
    public uint ClaimedUnix { get; set; }
}

public sealed record CollegeHandInRowInfo
{
    public int Id { get; set; }
    public string Student { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public uint HandedInUnix { get; set; }
}
```

- [ ] **Step 5: Add the args.** Create `Chaos.Networking/Entities/Client/CollegeActionArgs.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Entities.Client;

/// <summary>
///     Represents the serialization of the <see cref="ClientOpCode.CollegeAction" /> packet. Which fields are on the wire
///     depends on <see cref="Type" />; see <c>CollegeActionConverter</c>.
/// </summary>
public sealed record CollegeActionArgs : IPacketSerializable
{
    public required CollegeActionType Type { get; set; }

    /// <summary>SaveDraft, Submit, GalleryPage.</summary>
    public CollegeSubjectCode Subject { get; set; }

    /// <summary>SaveDraft, Submit (the piece is saved, then submitted), HandIn.</summary>
    public CollegePieceInfo? Piece { get; set; }

    /// <summary>PictureCheck, PicturePart, PictureFetch.</summary>
    public string Hash { get; set; } = string.Empty;

    /// <summary>PictureCheck: the picture's size in bytes.</summary>
    public uint Length { get; set; }

    /// <summary>PicturePart.</summary>
    public byte PartIndex { get; set; }

    public byte PartCount { get; set; }
    public byte[] Data { get; set; } = [];

    /// <summary>OpenPiece.</summary>
    public CollegePieceSource Source { get; set; }

    /// <summary>OpenPiece, Vote, Verdict, RemoveEntry, ShowToClass.</summary>
    public int Id { get; set; }

    /// <summary>Vote, Verdict.</summary>
    public CollegeTierCode Tier { get; set; }

    /// <summary>Vote: the comment. Verdict: the note to the author.</summary>
    public string Text { get; set; } = string.Empty;
}
```

Create `Chaos.Networking/Entities/Server/CollegeDisplayArgs.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Entities.Server;

/// <summary>
///     Represents the serialization of the <see cref="ServerOpCode.CollegeDisplay" /> packet. Which fields are on the wire
///     depends on <see cref="Type" />; see <c>CollegeDisplayConverter</c>.
/// </summary>
public sealed record CollegeDisplayArgs : IPacketSerializable
{
    public required CollegeDisplayType Type { get; set; }

    //OpenWriter
    public CollegeWriterMode Mode { get; set; }

    /// <summary>OpenWriter, GalleryList.</summary>
    public CollegeSubjectCode Subject { get; set; }

    public string Prompt { get; set; } = string.Empty;
    public byte EntryCost { get; set; }
    public byte FreeEntries { get; set; }

    /// <summary>OpenWriter (null for a blank piece), Piece.</summary>
    public CollegePieceInfo? Piece { get; set; }

    //WriterResult
    public CollegeWriterResult Result { get; set; }

    /// <summary>WriterResult, PictureReply.</summary>
    public string Message { get; set; } = string.Empty;

    //pictures
    public string Hash { get; set; } = string.Empty;
    public CollegePictureReply Reply { get; set; }
    public byte PartIndex { get; set; }
    public byte PartCount { get; set; }
    public byte[] Data { get; set; } = [];

    /// <summary>JudgingList, Piece: the viewer is the Director or an admin.</summary>
    public bool CanModerate { get; set; }

    public List<CollegeJudgingRowInfo> JudgingRows { get; set; } = [];

    //Piece
    public CollegePieceContext Context { get; set; }

    /// <summary>Piece: the entry or hand-in id.</summary>
    public int Id { get; set; }

    public string Header { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public byte MyTier { get; set; } = CollegeProtocol.NO_TIER;
    public string MyComment { get; set; } = string.Empty;
    public List<CollegeVoteInfo> Votes { get; set; } = [];

    public List<CollegeGalleryRowInfo> GalleryRows { get; set; } = [];
    public List<CollegeHandInRowInfo> HandInRows { get; set; } = [];
}
```

- [ ] **Step 6: Write the failing tests.** Create `Tests/Chaos.Tests/Networking/CollegePacketConverterTests.cs`:

```csharp
using System.Text;
using Chaos.DarkAges.Definitions;
using Chaos.IO.Memory;
using Chaos.Networking.Converters.Client;
using Chaos.Networking.Converters.Server;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Chaos.Packets.Abstractions;
using FluentAssertions;

namespace Chaos.Tests.Networking;

public sealed class CollegePacketConverterTests
{
    private static readonly Encoding Enc = Encoding.GetEncoding(949);
    private static readonly string Hash = new('a', 64);

    private static T RoundTrip<T>(PacketConverterBase<T> converter, T original) where T: class, IPacketSerializable
    {
        var writer = new SpanWriter(Enc, usePooling: false);
        converter.Serialize(ref writer, original);
        var bytes = writer.ToSpan().ToArray();
        var reader = new SpanReader(Enc, bytes);

        return converter.Deserialize(ref reader);
    }

    private static CollegePieceInfo Piece()
        => new()
        {
            Subject = CollegeSubjectCode.History,
            Title = "The Fall of Old Mileth",
            Blocks =
            [
                new CollegeBlockInfo { Kind = CollegeBlockKind.Heading, Text = "The Burning" },
                new CollegeBlockInfo { Kind = CollegeBlockKind.Text, Text = "When the raiders came...\nThe well ran dry." },
                new CollegeBlockInfo { Kind = CollegeBlockKind.Picture, Hash = Hash }
            ]
        };

    private static IEnumerable<CollegeActionArgs> Actions()
    {
        yield return new CollegeActionArgs { Type = CollegeActionType.SaveDraft, Subject = CollegeSubjectCode.Lore, Piece = Piece() };
        yield return new CollegeActionArgs { Type = CollegeActionType.Submit, Subject = CollegeSubjectCode.Philosophy, Piece = Piece() };
        yield return new CollegeActionArgs { Type = CollegeActionType.HandIn, Piece = Piece() };
        yield return new CollegeActionArgs { Type = CollegeActionType.PictureCheck, Hash = Hash, Length = 150_000 };

        yield return new CollegeActionArgs
        {
            Type = CollegeActionType.PicturePart, Hash = Hash, PartIndex = 2, PartCount = 5, Data = [1, 2, 3, 250]
        };
        yield return new CollegeActionArgs { Type = CollegeActionType.PictureFetch, Hash = Hash };
        yield return new CollegeActionArgs { Type = CollegeActionType.OpenPiece, Source = CollegePieceSource.HandIn, Id = 77 };

        yield return new CollegeActionArgs
        {
            Type = CollegeActionType.Vote, Id = 12, Tier = CollegeTierCode.Kingdom, Text = "Strong ending.\nWeak start."
        };
        yield return new CollegeActionArgs { Type = CollegeActionType.Verdict, Id = 12, Tier = CollegeTierCode.Village, Text = "Well done." };
        yield return new CollegeActionArgs { Type = CollegeActionType.RemoveEntry, Id = 9 };
        yield return new CollegeActionArgs { Type = CollegeActionType.GalleryPage, Subject = CollegeSubjectCode.Art };
        yield return new CollegeActionArgs { Type = CollegeActionType.ShowToClass, Id = 4 };
    }

    private static IEnumerable<CollegeDisplayArgs> Displays()
    {
        yield return new CollegeDisplayArgs
        {
            Type = CollegeDisplayType.OpenWriter,
            Mode = CollegeWriterMode.HandIn,
            Subject = CollegeSubjectCode.Literature,
            Prompt = "Describe a Mileth festival",
            EntryCost = 3,
            FreeEntries = 1,
            Piece = Piece()
        };
        yield return new CollegeDisplayArgs { Type = CollegeDisplayType.OpenWriter, Subject = CollegeSubjectCode.Lore, EntryCost = 3 };
        yield return new CollegeDisplayArgs { Type = CollegeDisplayType.WriterResult, Result = CollegeWriterResult.Refused, Message = "Too short." };
        yield return new CollegeDisplayArgs { Type = CollegeDisplayType.PictureReply, Hash = Hash, Reply = CollegePictureReply.Refused, Message = "No." };

        yield return new CollegeDisplayArgs
        {
            Type = CollegeDisplayType.PicturePart, Hash = Hash, PartIndex = 0, PartCount = 1, Data = [9, 8, 7]
        };

        yield return new CollegeDisplayArgs
        {
            Type = CollegeDisplayType.JudgingList,
            CanModerate = true,
            JudgingRows =
            [
                new CollegeJudgingRowInfo { Id = 1, Subject = CollegeSubjectCode.History, Title = "A", DaysLeft = 12, MyTier = 2 },
                new CollegeJudgingRowInfo { Id = 2, Subject = CollegeSubjectCode.Lore, Title = "B", Waiting = true }
            ]
        };

        yield return new CollegeDisplayArgs
        {
            Type = CollegeDisplayType.Piece,
            Context = CollegePieceContext.Verdict,
            Id = 5,
            Header = "awaiting verdict",
            Author = "Aroha",
            MyTier = 3,
            MyComment = "Lovely.",
            CanModerate = true,
            Votes = [new CollegeVoteInfo { Judge = "Brannoc", Tier = CollegeTierCode.Village, Comment = "Good." }],
            Piece = Piece()
        };

        yield return new CollegeDisplayArgs
        {
            Type = CollegeDisplayType.GalleryList,
            Subject = CollegeSubjectCode.History,
            GalleryRows = [new CollegeGalleryRowInfo { Id = 3, Title = "T", Author = "Elowen", Tier = CollegeTierCode.Kingdom, ClaimedUnix = 1_790_000_000 }]
        };

        yield return new CollegeDisplayArgs
        {
            Type = CollegeDisplayType.HandInList,
            HandInRows = [new CollegeHandInRowInfo { Id = 8, Student = "Tamsin", Title = "Festival", HandedInUnix = 1_790_000_100 }]
        };
    }

    [Test]
    public void Every_action_round_trips()
    {
        foreach (var original in Actions())
            RoundTrip(new CollegeActionConverter(), original).Should().BeEquivalentTo(original, original.Type.ToString());

        Actions().Select(a => a.Type).Distinct().Should().HaveCount(Enum.GetValues<CollegeActionType>().Length);
    }

    [Test]
    public void Every_display_round_trips()
    {
        foreach (var original in Displays())
            RoundTrip(new CollegeDisplayConverter(), original).Should().BeEquivalentTo(original, original.Type.ToString());

        Displays().Select(d => d.Type).Distinct().Should().HaveCount(Enum.GetValues<CollegeDisplayType>().Length);
    }

    [Test]
    public void An_unknown_action_type_is_refused()
    {
        var act = () =>
        {
            var local = new SpanReader(Enc, [200]);
            new CollegeActionConverter().Deserialize(ref local);
        };

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void An_unknown_block_kind_is_refused()
    {
        var act = () =>
        {
            var local = new SpanReader(Enc, [(byte)CollegeActionType.HandIn, 0, 0, 1, 9]);
            new CollegeActionConverter().Deserialize(ref local);
        };

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void Opcodes_are_144_and_152()
    {
        ((byte)Chaos.Networking.Abstractions.Definitions.ClientOpCode.CollegeAction).Should().Be(144);
        ((byte)Chaos.Networking.Abstractions.Definitions.ServerOpCode.CollegeDisplay).Should().Be(152);
    }
}
```

The bytes in `An_unknown_block_kind_is_refused` are: type HandIn, subject 0, title length 0, block count 1, block kind 9.

- [ ] **Step 7: Run to see them fail.** Run `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/CollegePacketConverterTests/*"`. Expected: build errors for the missing converters.

- [ ] **Step 8: Write the codec.** Create `Chaos.Networking/Converters/CollegeCodec.cs`:

```csharp
#pragma warning disable CS1591
using Chaos.DarkAges.Definitions;
using Chaos.IO.Memory;
using Chaos.Networking.Entities.Server;

namespace Chaos.Networking.Converters;

/// <summary>Reads and writes College pieces and list rows for both College messages.</summary>
public static class CollegeCodec
{
    public static CollegePieceInfo ReadPiece(ref SpanReader reader)
    {
        var piece = new CollegePieceInfo
        {
            Subject = ReadSubject(ref reader),
            Title = reader.ReadString8()
        };

        var count = reader.ReadByte();

        for (var i = 0; i < count; i++)
        {
            var kind = (CollegeBlockKind)reader.ReadByte();

            if (!Enum.IsDefined(kind))
                throw new ArgumentOutOfRangeException(nameof(reader), kind, "Unknown College block kind");

            piece.Blocks.Add(
                kind switch
                {
                    CollegeBlockKind.Text    => new CollegeBlockInfo { Kind = kind, Text = reader.ReadString16() },
                    CollegeBlockKind.Heading => new CollegeBlockInfo { Kind = kind, Text = reader.ReadString8() },
                    _                        => new CollegeBlockInfo { Kind = kind, Hash = reader.ReadString8() }
                });
        }

        return piece;
    }

    public static void WritePiece(ref SpanWriter writer, CollegePieceInfo piece)
    {
        writer.WriteByte((byte)piece.Subject);
        writer.WriteString8(piece.Title);

        var count = Math.Min(piece.Blocks.Count, byte.MaxValue);
        writer.WriteByte((byte)count);

        foreach (var block in piece.Blocks.Take(count))
        {
            writer.WriteByte((byte)block.Kind);

            switch (block.Kind)
            {
                case CollegeBlockKind.Text:
                    writer.WriteString16(block.Text);

                    break;
                case CollegeBlockKind.Heading:
                    writer.WriteString8(block.Text);

                    break;
                default:
                    writer.WriteString8(block.Hash);

                    break;
            }
        }
    }

    public static CollegePieceInfo? ReadOptionalPiece(ref SpanReader reader) => reader.ReadBoolean() ? ReadPiece(ref reader) : null;

    public static void WriteOptionalPiece(ref SpanWriter writer, CollegePieceInfo? piece)
    {
        writer.WriteBoolean(piece is not null);

        if (piece is not null)
            WritePiece(ref writer, piece);
    }

    public static CollegeSubjectCode ReadSubject(ref SpanReader reader)
    {
        var subject = (CollegeSubjectCode)reader.ReadByte();

        if (!Enum.IsDefined(subject))
            throw new ArgumentOutOfRangeException(nameof(reader), subject, "Unknown College subject");

        return subject;
    }

    public static CollegeTierCode ReadTier(ref SpanReader reader)
    {
        var tier = (CollegeTierCode)reader.ReadByte();

        if (!Enum.IsDefined(tier))
            throw new ArgumentOutOfRangeException(nameof(reader), tier, "Unknown College tier");

        return tier;
    }

    public static List<CollegeJudgingRowInfo> ReadJudgingRows(ref SpanReader reader)
    {
        var count = reader.ReadUInt16();
        var rows = new List<CollegeJudgingRowInfo>(count);

        for (var i = 0; i < count; i++)
            rows.Add(
                new CollegeJudgingRowInfo
                {
                    Id = reader.ReadInt32(),
                    Subject = ReadSubject(ref reader),
                    Title = reader.ReadString8(),
                    DaysLeft = reader.ReadByte(),
                    MyTier = reader.ReadByte(),
                    Waiting = reader.ReadBoolean()
                });

        return rows;
    }

    public static void WriteJudgingRows(ref SpanWriter writer, List<CollegeJudgingRowInfo> rows)
    {
        writer.WriteUInt16((ushort)Math.Min(rows.Count, ushort.MaxValue));

        foreach (var row in rows.Take(ushort.MaxValue))
        {
            writer.WriteInt32(row.Id);
            writer.WriteByte((byte)row.Subject);
            writer.WriteString8(row.Title);
            writer.WriteByte(row.DaysLeft);
            writer.WriteByte(row.MyTier);
            writer.WriteBoolean(row.Waiting);
        }
    }

    public static List<CollegeVoteInfo> ReadVotes(ref SpanReader reader)
    {
        var count = reader.ReadByte();
        var votes = new List<CollegeVoteInfo>(count);

        for (var i = 0; i < count; i++)
            votes.Add(
                new CollegeVoteInfo
                {
                    Judge = reader.ReadString8(),
                    Tier = ReadTier(ref reader),
                    Comment = reader.ReadString16()
                });

        return votes;
    }

    public static void WriteVotes(ref SpanWriter writer, List<CollegeVoteInfo> votes)
    {
        var count = Math.Min(votes.Count, byte.MaxValue);
        writer.WriteByte((byte)count);

        foreach (var vote in votes.Take(count))
        {
            writer.WriteString8(vote.Judge);
            writer.WriteByte((byte)vote.Tier);
            writer.WriteString16(vote.Comment);
        }
    }

    public static List<CollegeGalleryRowInfo> ReadGalleryRows(ref SpanReader reader)
    {
        var count = reader.ReadUInt16();
        var rows = new List<CollegeGalleryRowInfo>(count);

        for (var i = 0; i < count; i++)
            rows.Add(
                new CollegeGalleryRowInfo
                {
                    Id = reader.ReadInt32(),
                    Title = reader.ReadString8(),
                    Author = reader.ReadString8(),
                    Tier = ReadTier(ref reader),
                    ClaimedUnix = reader.ReadUInt32()
                });

        return rows;
    }

    public static void WriteGalleryRows(ref SpanWriter writer, List<CollegeGalleryRowInfo> rows)
    {
        writer.WriteUInt16((ushort)Math.Min(rows.Count, ushort.MaxValue));

        foreach (var row in rows.Take(ushort.MaxValue))
        {
            writer.WriteInt32(row.Id);
            writer.WriteString8(row.Title);
            writer.WriteString8(row.Author);
            writer.WriteByte((byte)row.Tier);
            writer.WriteUInt32(row.ClaimedUnix);
        }
    }

    public static List<CollegeHandInRowInfo> ReadHandInRows(ref SpanReader reader)
    {
        var count = reader.ReadUInt16();
        var rows = new List<CollegeHandInRowInfo>(count);

        for (var i = 0; i < count; i++)
            rows.Add(
                new CollegeHandInRowInfo
                {
                    Id = reader.ReadInt32(),
                    Student = reader.ReadString8(),
                    Title = reader.ReadString8(),
                    HandedInUnix = reader.ReadUInt32()
                });

        return rows;
    }

    public static void WriteHandInRows(ref SpanWriter writer, List<CollegeHandInRowInfo> rows)
    {
        writer.WriteUInt16((ushort)Math.Min(rows.Count, ushort.MaxValue));

        foreach (var row in rows.Take(ushort.MaxValue))
        {
            writer.WriteInt32(row.Id);
            writer.WriteString8(row.Student);
            writer.WriteString8(row.Title);
            writer.WriteUInt32(row.HandedInUnix);
        }
    }
}
```

If `SpanReader` has no `ReadInt32`, use `(int)reader.ReadUInt32()` and `writer.WriteUInt32((uint)row.Id)` instead. Check with `grep -n "public int ReadInt32" Chaos.IO/Memory/SpanReader.cs`.

- [ ] **Step 9: Write the converters.** Create `Chaos.Networking/Converters/Client/CollegeActionConverter.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.IO.Memory;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Converters.Client;

/// <summary>Provides packet serialization and deserialization logic for <see cref="CollegeActionArgs" /></summary>
public sealed class CollegeActionConverter : PacketConverterBase<CollegeActionArgs>
{
    /// <inheritdoc />
    public override byte OpCode => (byte)ClientOpCode.CollegeAction;

    /// <inheritdoc />
    public override CollegeActionArgs Deserialize(ref SpanReader reader)
    {
        var type = (CollegeActionType)reader.ReadByte();

        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(reader), type, "Unknown College action type");

        var args = new CollegeActionArgs { Type = type };

        switch (type)
        {
            case CollegeActionType.SaveDraft:
            case CollegeActionType.Submit:
                args.Subject = CollegeCodec.ReadSubject(ref reader);
                args.Piece = CollegeCodec.ReadPiece(ref reader);

                break;
            case CollegeActionType.GalleryPage:
                args.Subject = CollegeCodec.ReadSubject(ref reader);

                break;
            case CollegeActionType.HandIn:
                args.Piece = CollegeCodec.ReadPiece(ref reader);

                break;
            case CollegeActionType.PictureCheck:
                args.Hash = reader.ReadString8();
                args.Length = reader.ReadUInt32();

                break;
            case CollegeActionType.PicturePart:
                args.Hash = reader.ReadString8();
                args.PartIndex = reader.ReadByte();
                args.PartCount = reader.ReadByte();
                args.Data = reader.ReadData16();

                break;
            case CollegeActionType.PictureFetch:
                args.Hash = reader.ReadString8();

                break;
            case CollegeActionType.OpenPiece:
                args.Source = (CollegePieceSource)reader.ReadByte();
                args.Id = reader.ReadInt32();

                break;
            case CollegeActionType.Vote:
            case CollegeActionType.Verdict:
                args.Id = reader.ReadInt32();
                args.Tier = CollegeCodec.ReadTier(ref reader);
                args.Text = reader.ReadString16();

                break;
            case CollegeActionType.RemoveEntry:
            case CollegeActionType.ShowToClass:
                args.Id = reader.ReadInt32();

                break;
        }

        return args;
    }

    /// <inheritdoc />
    public override void Serialize(ref SpanWriter writer, CollegeActionArgs args)
    {
        writer.WriteByte((byte)args.Type);

        switch (args.Type)
        {
            case CollegeActionType.SaveDraft:
            case CollegeActionType.Submit:
                writer.WriteByte((byte)args.Subject);
                CollegeCodec.WritePiece(ref writer, args.Piece!);

                break;
            case CollegeActionType.GalleryPage:
                writer.WriteByte((byte)args.Subject);

                break;
            case CollegeActionType.HandIn:
                CollegeCodec.WritePiece(ref writer, args.Piece!);

                break;
            case CollegeActionType.PictureCheck:
                writer.WriteString8(args.Hash);
                writer.WriteUInt32(args.Length);

                break;
            case CollegeActionType.PicturePart:
                writer.WriteString8(args.Hash);
                writer.WriteByte(args.PartIndex);
                writer.WriteByte(args.PartCount);
                writer.WriteData16(args.Data);

                break;
            case CollegeActionType.PictureFetch:
                writer.WriteString8(args.Hash);

                break;
            case CollegeActionType.OpenPiece:
                writer.WriteByte((byte)args.Source);
                writer.WriteInt32(args.Id);

                break;
            case CollegeActionType.Vote:
            case CollegeActionType.Verdict:
                writer.WriteInt32(args.Id);
                writer.WriteByte((byte)args.Tier);
                writer.WriteString16(args.Text);

                break;
            case CollegeActionType.RemoveEntry:
            case CollegeActionType.ShowToClass:
                writer.WriteInt32(args.Id);

                break;
        }
    }
}
```

Create `Chaos.Networking/Converters/Server/CollegeDisplayConverter.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.IO.Memory;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
using Chaos.Packets.Abstractions;

namespace Chaos.Networking.Converters.Server;

/// <summary>Provides packet serialization and deserialization logic for <see cref="CollegeDisplayArgs" /></summary>
public sealed class CollegeDisplayConverter : PacketConverterBase<CollegeDisplayArgs>
{
    /// <inheritdoc />
    public override byte OpCode => (byte)ServerOpCode.CollegeDisplay;

    /// <inheritdoc />
    public override CollegeDisplayArgs Deserialize(ref SpanReader reader)
    {
        var type = (CollegeDisplayType)reader.ReadByte();

        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(reader), type, "Unknown College display type");

        var args = new CollegeDisplayArgs { Type = type };

        switch (type)
        {
            case CollegeDisplayType.OpenWriter:
                args.Mode = (CollegeWriterMode)reader.ReadByte();
                args.Subject = CollegeCodec.ReadSubject(ref reader);
                args.Prompt = reader.ReadString8();
                args.EntryCost = reader.ReadByte();
                args.FreeEntries = reader.ReadByte();
                args.Piece = CollegeCodec.ReadOptionalPiece(ref reader);

                break;
            case CollegeDisplayType.WriterResult:
                args.Result = (CollegeWriterResult)reader.ReadByte();
                args.Message = reader.ReadString8();

                break;
            case CollegeDisplayType.PictureReply:
                args.Hash = reader.ReadString8();
                args.Reply = (CollegePictureReply)reader.ReadByte();
                args.Message = reader.ReadString8();

                break;
            case CollegeDisplayType.PicturePart:
                args.Hash = reader.ReadString8();
                args.PartIndex = reader.ReadByte();
                args.PartCount = reader.ReadByte();
                args.Data = reader.ReadData16();

                break;
            case CollegeDisplayType.JudgingList:
                args.CanModerate = reader.ReadBoolean();
                args.JudgingRows = CollegeCodec.ReadJudgingRows(ref reader);

                break;
            case CollegeDisplayType.Piece:
                args.Context = (CollegePieceContext)reader.ReadByte();
                args.Id = reader.ReadInt32();
                args.Header = reader.ReadString8();
                args.Author = reader.ReadString8();
                args.MyTier = reader.ReadByte();
                args.MyComment = reader.ReadString16();
                args.CanModerate = reader.ReadBoolean();
                args.Votes = CollegeCodec.ReadVotes(ref reader);
                args.Piece = CollegeCodec.ReadPiece(ref reader);

                break;
            case CollegeDisplayType.GalleryList:
                args.Subject = CollegeCodec.ReadSubject(ref reader);
                args.GalleryRows = CollegeCodec.ReadGalleryRows(ref reader);

                break;
            case CollegeDisplayType.HandInList:
                args.HandInRows = CollegeCodec.ReadHandInRows(ref reader);

                break;
        }

        return args;
    }

    /// <inheritdoc />
    public override void Serialize(ref SpanWriter writer, CollegeDisplayArgs args)
    {
        writer.WriteByte((byte)args.Type);

        switch (args.Type)
        {
            case CollegeDisplayType.OpenWriter:
                writer.WriteByte((byte)args.Mode);
                writer.WriteByte((byte)args.Subject);
                writer.WriteString8(args.Prompt);
                writer.WriteByte(args.EntryCost);
                writer.WriteByte(args.FreeEntries);
                CollegeCodec.WriteOptionalPiece(ref writer, args.Piece);

                break;
            case CollegeDisplayType.WriterResult:
                writer.WriteByte((byte)args.Result);
                writer.WriteString8(args.Message);

                break;
            case CollegeDisplayType.PictureReply:
                writer.WriteString8(args.Hash);
                writer.WriteByte((byte)args.Reply);
                writer.WriteString8(args.Message);

                break;
            case CollegeDisplayType.PicturePart:
                writer.WriteString8(args.Hash);
                writer.WriteByte(args.PartIndex);
                writer.WriteByte(args.PartCount);
                writer.WriteData16(args.Data);

                break;
            case CollegeDisplayType.JudgingList:
                writer.WriteBoolean(args.CanModerate);
                CollegeCodec.WriteJudgingRows(ref writer, args.JudgingRows);

                break;
            case CollegeDisplayType.Piece:
                writer.WriteByte((byte)args.Context);
                writer.WriteInt32(args.Id);
                writer.WriteString8(args.Header);
                writer.WriteString8(args.Author);
                writer.WriteByte(args.MyTier);
                writer.WriteString16(args.MyComment);
                writer.WriteBoolean(args.CanModerate);
                CollegeCodec.WriteVotes(ref writer, args.Votes);
                CollegeCodec.WritePiece(ref writer, args.Piece!);

                break;
            case CollegeDisplayType.GalleryList:
                writer.WriteByte((byte)args.Subject);
                CollegeCodec.WriteGalleryRows(ref writer, args.GalleryRows);

                break;
            case CollegeDisplayType.HandInList:
                CollegeCodec.WriteHandInRows(ref writer, args.HandInRows);

                break;
        }
    }
}
```

- [ ] **Step 10: Add the send method.** In `Chaos/Networking/Abstractions/IChaosWorldClient.cs`, beside `void SendTownBallot(TownBallotArgs args);` add `void SendCollegeDisplay(CollegeDisplayArgs args);`. In `Chaos/Networking/ChaosWorldClient.cs`, beside `SendTownBallot`, add `public void SendCollegeDisplay(CollegeDisplayArgs args) => Send(args);`. If a test project has a fake `IChaosWorldClient` that fails to compile, add the same member there (`grep -rln ": IChaosWorldClient" Tests`).

- [ ] **Step 11: Run the tests.** Run `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/CollegePacketConverterTests/*"`. Expected: 5 passed. Then `--treenode-filter "/*/*/GuildEmblemPacketConverterTests/*"`: all pass.

```json:metadata
{"files": ["Chaos.DarkAges/Definitions/CollegeProtocol.cs", "Chaos.DarkAges/Definitions/Enums.cs", "Chaos.Networking.Abstractions/Definitions/Enums.cs", "Chaos.DarkAges/Definitions/CONSTANTS.cs", "Chaos.Networking/Entities/Server/CollegeInfos.cs", "Chaos.Networking/Entities/Client/CollegeActionArgs.cs", "Chaos.Networking/Entities/Server/CollegeDisplayArgs.cs", "Chaos.Networking/Converters/CollegeCodec.cs", "Chaos.Networking/Converters/Client/CollegeActionConverter.cs", "Chaos.Networking/Converters/Server/CollegeDisplayConverter.cs", "Chaos/Networking/Abstractions/IChaosWorldClient.cs", "Chaos/Networking/ChaosWorldClient.cs", "Tests/Chaos.Tests/Networking/CollegePacketConverterTests.cs", "Tests/Chaos.Tests/Networking/GuildEmblemPacketConverterTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/CollegePacketConverterTests/*\"", "acceptanceCriteria": ["every action and display sub-type round-trips", "unknown sub-type throws ArgumentOutOfRangeException", "CLIENT_VERSION is 772", "opcode uniqueness test passes"], "modelTier": "mechanical"}
```

---

### Task 2: Piece model, piece rules and new options (server worktree)

**Goal:** Add the server's piece model, the pure `PieceRules` (cleanup and every limit), the option additions, and the refusal texts.

**Files:**
- Create: `Chaos/Services/College/CollegePiece.cs`, `Chaos/Services/College/PieceRules.cs`
- Modify: `Chaos/Services/College/CollegeOptions.cs`, `Chaos/Services/College/CollegeText.cs`
- Test: `Tests/Chaos.Tests/College/PieceRulesTests.cs` (new)

**Acceptance Criteria:**
- [ ] `Normalize` strips control characters except `\n`, turns `\r\n` and `\r` into `\n`, makes headings and the title one trimmed line, drops empty text and heading blocks, merges neighbouring text blocks, and lower-cases picture hashes.
- [ ] `Check` returns the first broken limit in this order: TooManyBlocks, TitleTooLong, HeadingTooLong, TooMuchText, TooManyPictures, BadHash, DuplicatePicture, MissingPicture; otherwise Ok. Each limit passes at its maximum and fails one above.
- [ ] `CheckEntry` also returns NoTitle for an empty title and TooShort under 200 characters; `CheckHandIn` returns TooShort under 20.
- [ ] `CollegeOptions` implements `IDirectoryBound` and has the spec's new options with the spec's defaults.

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/PieceRulesTests/*"` -> all pass.

**Steps:**

- [ ] **Step 1: Add the model.** Create `Chaos/Services/College/CollegePiece.cs`:

```csharp
namespace Chaos.Services.College;

public enum PieceKind
{
    Writing,
    Drawing,
    Tune
}

public enum PieceBlockType
{
    Text,
    Heading,
    Picture
}

public sealed class PieceBlock
{
    public PieceBlockType Type { get; set; }
    public string Text { get; set; } = "";
    public string Hash { get; set; } = "";
}

/// <summary>One piece of College work: a draft, an entry or a hand-in. Saved as its own file; holds no character names.</summary>
public sealed class CollegePiece
{
    public string Id { get; set; } = "";
    public CollegeSubject Subject { get; set; }
    public PieceKind Kind { get; set; } = PieceKind.Writing;
    public string Title { get; set; } = "";
    public List<PieceBlock> Blocks { get; set; } = [];
    public DateTime UpdatedUtc { get; set; }

    public static string NewId() => Guid.NewGuid().ToString("N");

    public IEnumerable<string> PictureHashes => Blocks.Where(b => b.Type == PieceBlockType.Picture).Select(b => b.Hash);
}

public enum PieceCheck
{
    Ok,
    TooManyBlocks,
    TitleTooLong,
    HeadingTooLong,
    TooMuchText,
    TooManyPictures,
    BadHash,
    DuplicatePicture,
    MissingPicture,
    NoTitle,
    TooShort
}
```

- [ ] **Step 2: Add the options.** In `Chaos/Services/College/CollegeOptions.cs`, add `using Chaos.Common.Abstractions;` and `using Chaos.DarkAges.Definitions;`, change the declaration to `public sealed record CollegeOptions : IDirectoryBound`, and add after `HallInstanceId`:

```csharp
    public int VotingDays { get; set; } = 14;
    public int VerdictReminderDays { get; set; } = 7;
    public List<string> VerdictReminderNames { get; set; } = [];
    public int HandInDays { get; set; } = 7;
    public int MaxPictureBytes { get; set; } = CollegeProtocol.MAX_PICTURE_BYTES;
    public int PictureUploadsPerHour { get; set; } = 10;
    public int PictureBytesPerDay { get; set; } = 5 * 1024 * 1024;
    public int UnusedPictureHours { get; set; } = 24;

    /// <summary>Subjects whose entries are open. Art and Music join when their tools ship (parts 3 and 4).</summary>
    public List<CollegeSubject> OpenSubjects { get; set; } =
        [CollegeSubject.History, CollegeSubject.Literature, CollegeSubject.Lore, CollegeSubject.Philosophy];

    /// <summary>Where pieces and pictures are saved, under the staging directory.</summary>
    public string WorksDirectory { get; set; } = Path.Combine("LocalStorage", "College");

    public bool IsOpen(CollegeSubject subject) => OpenSubjects.Contains(subject);

    public static bool IsWritingSubject(CollegeSubject subject) => subject is not (CollegeSubject.Art or CollegeSubject.Music);

    /// <inheritdoc />
    public void UseBaseDirectory(string baseDirectory) => WorksDirectory = Path.Combine(baseDirectory, WorksDirectory);
```

Check that `CollegeState.json` lives in `<staging>/LocalStorage/` (`grep -rn "LocalStorage" Chaos/appsettings.json Chaos.Storage/*.cs | head`). If local storage uses another folder name, use that name instead of `"LocalStorage"`.

- [ ] **Step 3: Write the failing tests.** Create `Tests/Chaos.Tests/College/PieceRulesTests.cs`:

```csharp
using Chaos.Services.College;
using FluentAssertions;

namespace Chaos.Tests.College;

public sealed class PieceRulesTests
{
    private static readonly string HashA = new('a', 64);
    private static readonly string HashB = new('b', 64);

    private static CollegePiece Piece(string title, params PieceBlock[] blocks)
        => new() { Id = CollegePiece.NewId(), Subject = CollegeSubject.History, Title = title, Blocks = blocks.ToList() };

    private static PieceBlock Text(string text) => new() { Type = PieceBlockType.Text, Text = text };
    private static PieceBlock Heading(string text) => new() { Type = PieceBlockType.Heading, Text = text };
    private static PieceBlock Picture(string hash) => new() { Type = PieceBlockType.Picture, Hash = hash };

    private static bool Exists(string _) => true;

    [Test]
    public void Normalize_cleans_text_and_merges_neighbouring_text_blocks()
    {
        var piece = Piece(
            "  The\r\nTitle \u0007",
            Heading(" Part\nOne "),
            Text("a\r\nb\u0001"),
            Text("c\rd"),
            Text("   "),
            Heading("  "),
            Picture(HashA.ToUpperInvariant()));

        var clean = PieceRules.Normalize(piece);

        clean.Title.Should().Be("The Title");
        clean.Blocks.Should().HaveCount(3);
        clean.Blocks[0].Should().BeEquivalentTo(Heading("Part One"));
        clean.Blocks[1].Should().BeEquivalentTo(Text("a\nb\nc\nd"));
        clean.Blocks[2].Should().BeEquivalentTo(Picture(HashA));
        clean.Id.Should().Be(piece.Id);
    }

    [Test]
    public void Text_length_counts_text_and_headings_only()
        => PieceRules.TextLength(Piece("title", Heading("abc"), Text("de"), Picture(HashA))).Should().Be(5);

    [Test]
    public void A_piece_at_every_limit_is_ok()
    {
        var blocks = new List<PieceBlock> { Heading(new string('h', 60)) };
        blocks.AddRange(Enumerable.Range(0, 5).Select(i => Picture(new string((char)('a' + i), 64))));
        blocks.Add(Text(new string('t', 10_000 - 60)));

        PieceRules.Check(Piece(new string('x', 40), blocks.ToArray()), Exists).Should().Be(PieceCheck.Ok);
    }

    [Test]
    public void Each_limit_fails_one_above()
    {
        PieceRules.Check(Piece(new string('x', 41)), Exists).Should().Be(PieceCheck.TitleTooLong);
        PieceRules.Check(Piece("t", Heading(new string('h', 61))), Exists).Should().Be(PieceCheck.HeadingTooLong);
        PieceRules.Check(Piece("t", Text(new string('t', 10_001))), Exists).Should().Be(PieceCheck.TooMuchText);

        var six = Enumerable.Range(0, 6).Select(i => Picture(new string((char)('a' + i), 64))).ToArray();
        PieceRules.Check(Piece("t", six), Exists).Should().Be(PieceCheck.TooManyPictures);

        var many = Enumerable.Range(0, 201).Select(_ => Heading("h")).ToArray();
        PieceRules.Check(Piece("t", many), Exists).Should().Be(PieceCheck.TooManyBlocks);
    }

    [Test]
    public void Pictures_must_be_well_formed_unique_and_stored()
    {
        PieceRules.Check(Piece("t", Picture("xyz")), Exists).Should().Be(PieceCheck.BadHash);
        PieceRules.Check(Piece("t", Picture(HashA), Picture(HashA)), Exists).Should().Be(PieceCheck.DuplicatePicture);
        PieceRules.Check(Piece("t", Picture(HashA), Picture(HashB)), h => h == HashA).Should().Be(PieceCheck.MissingPicture);
    }

    [Test]
    public void An_entry_needs_a_title_and_two_hundred_characters()
    {
        PieceRules.CheckEntry(Piece("", Text(new string('t', 200))), Exists).Should().Be(PieceCheck.NoTitle);
        PieceRules.CheckEntry(Piece("t", Text(new string('t', 199))), Exists).Should().Be(PieceCheck.TooShort);
        PieceRules.CheckEntry(Piece("t", Text(new string('t', 200))), Exists).Should().Be(PieceCheck.Ok);
    }

    [Test]
    public void A_hand_in_needs_twenty_characters_and_no_title()
    {
        PieceRules.CheckHandIn(Piece("", Text(new string('t', 19))), Exists).Should().Be(PieceCheck.TooShort);
        PieceRules.CheckHandIn(Piece("", Text(new string('t', 20))), Exists).Should().Be(PieceCheck.Ok);
    }
}
```

- [ ] **Step 4: Run to see them fail.** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/PieceRulesTests/*"`. Expected: build error, `PieceRules` does not exist.

- [ ] **Step 5: Write `PieceRules`.** Create `Chaos/Services/College/PieceRules.cs`:

```csharp
using System.Text;
using Chaos.DarkAges.Definitions;

namespace Chaos.Services.College;

/// <summary>Cleans a piece and checks it against the College limits. No game-world access.</summary>
public static class PieceRules
{
    public static string Clean(string text)
    {
        var builder = new StringBuilder(text.Length);
        var unified = text.Replace("\r\n", "\n").Replace('\r', '\n');

        foreach (var c in unified)
            if ((c == '\n') || !char.IsControl(c))
                builder.Append(c);

        return builder.ToString();
    }

    private static string OneLine(string text) => Clean(text).Replace('\n', ' ').Trim();

    public static CollegePiece Normalize(CollegePiece piece)
    {
        var blocks = new List<PieceBlock>();

        foreach (var block in piece.Blocks)
            switch (block.Type)
            {
                case PieceBlockType.Heading:
                    var heading = OneLine(block.Text);

                    if (heading.Length > 0)
                        blocks.Add(new PieceBlock { Type = PieceBlockType.Heading, Text = heading });

                    break;
                case PieceBlockType.Text:
                    var text = Clean(block.Text);

                    if (text.Trim().Length == 0)
                        break;

                    if ((blocks.Count > 0) && (blocks[^1].Type == PieceBlockType.Text))
                        blocks[^1].Text += "\n" + text;
                    else
                        blocks.Add(new PieceBlock { Type = PieceBlockType.Text, Text = text });

                    break;
                case PieceBlockType.Picture:
                    blocks.Add(new PieceBlock { Type = PieceBlockType.Picture, Hash = block.Hash.Trim().ToLowerInvariant() });

                    break;
            }

        return new CollegePiece
        {
            Id = piece.Id,
            Subject = piece.Subject,
            Kind = piece.Kind,
            Title = OneLine(piece.Title),
            Blocks = blocks,
            UpdatedUtc = piece.UpdatedUtc
        };
    }

    public static int TextLength(CollegePiece piece) => piece.Blocks.Where(b => b.Type != PieceBlockType.Picture).Sum(b => b.Text.Length);

    public static bool IsHash(string hash)
        => (hash.Length == CollegeProtocol.HASH_CHARS) && hash.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    public static PieceCheck Check(CollegePiece piece, Func<string, bool> pictureExists)
    {
        if (piece.Blocks.Count > CollegeProtocol.MAX_BLOCKS)
            return PieceCheck.TooManyBlocks;

        if (piece.Title.Length > CollegeProtocol.MAX_TITLE_CHARS)
            return PieceCheck.TitleTooLong;

        if (piece.Blocks.Any(b => (b.Type == PieceBlockType.Heading) && (b.Text.Length > CollegeProtocol.MAX_HEADING_CHARS)))
            return PieceCheck.HeadingTooLong;

        if (TextLength(piece) > CollegeProtocol.MAX_TEXT_CHARS)
            return PieceCheck.TooMuchText;

        var hashes = piece.PictureHashes.ToList();

        if (hashes.Count > CollegeProtocol.MAX_PICTURES)
            return PieceCheck.TooManyPictures;

        if (!hashes.All(IsHash))
            return PieceCheck.BadHash;

        if (hashes.Distinct(StringComparer.Ordinal).Count() != hashes.Count)
            return PieceCheck.DuplicatePicture;

        return hashes.All(pictureExists) ? PieceCheck.Ok : PieceCheck.MissingPicture;
    }

    public static PieceCheck CheckEntry(CollegePiece piece, Func<string, bool> pictureExists)
    {
        var check = Check(piece, pictureExists);

        if (check != PieceCheck.Ok)
            return check;

        if (piece.Title.Length == 0)
            return PieceCheck.NoTitle;

        return TextLength(piece) < CollegeProtocol.MIN_ENTRY_CHARS ? PieceCheck.TooShort : PieceCheck.Ok;
    }

    public static PieceCheck CheckHandIn(CollegePiece piece, Func<string, bool> pictureExists)
    {
        var check = Check(piece, pictureExists);

        if (check != PieceCheck.Ok)
            return check;

        return TextLength(piece) < CollegeProtocol.MIN_HAND_IN_CHARS ? PieceCheck.TooShort : PieceCheck.Ok;
    }
}
```

- [ ] **Step 6: Add the refusal texts.** In `Chaos/Services/College/CollegeText.cs` add (all at most 45 characters):

```csharp
    public const string DRAFT_SAVED = "Draft saved.";
    public const string SUBMITTED = "Your entry is in! Voting ends in 14 days.";
    public const string NOT_ENOUGH_MARKS = "You need 3 Educated Marks to enter.";
    public const string HANDED_IN = "Your hand-in was received.";
    public const string VOTE_SAVED = "Your vote is saved.";
    public const string VERDICT_POSTED = "Verdict posted.";
    public const string SAVE_FAILED = "Your entry could not be saved.";
    public const string NOT_OPEN = "That subject opens soon.";

    public static string AlreadyEntered(CollegeSubject subject) => $"You already have an entry in {SubjectName(subject)}.";

    public static string Claimed(AwardTier tier, CollegeSubject subject) => $"You claimed the {tier} Award in {SubjectName(subject)}!";

    public static string PieceProblem(PieceCheck check)
        => check switch
        {
            PieceCheck.TooManyBlocks    => "That piece has too many parts.",
            PieceCheck.TitleTooLong     => "Titles can be 40 characters at most.",
            PieceCheck.HeadingTooLong   => "Headings can be 60 characters at most.",
            PieceCheck.TooMuchText      => "Pieces can be 10,000 characters at most.",
            PieceCheck.TooManyPictures  => "Pieces can hold 5 pictures at most.",
            PieceCheck.BadHash          => "A picture is damaged. Insert it again.",
            PieceCheck.DuplicatePicture => "A picture appears twice.",
            PieceCheck.MissingPicture   => "A picture didn't upload. Insert it again.",
            PieceCheck.NoTitle          => "Entries need a title and 200 characters.",
            PieceCheck.TooShort         => "That piece is too short.",
            _                           => string.Empty
        };
```

- [ ] **Step 7: Run the tests.** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/PieceRulesTests/*"`. Expected: 7 passed.

```json:metadata
{"files": ["Chaos/Services/College/CollegePiece.cs", "Chaos/Services/College/PieceRules.cs", "Chaos/Services/College/CollegeOptions.cs", "Chaos/Services/College/CollegeText.cs", "Tests/Chaos.Tests/College/PieceRulesTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/PieceRulesTests/*\"", "acceptanceCriteria": ["Normalize cleans and merges as specified", "Check returns the first broken limit in order", "CheckEntry/CheckHandIn minimums", "CollegeOptions implements IDirectoryBound with the new defaults"], "modelTier": "mechanical"}
```

---

### Task 3: Picture format, picture store and uploads (server worktree)

**Goal:** Read PNG and JPEG headers, store each picture once by hash, sweep unused pictures, and assemble uploads in parts with per-character caps.

**Files:**
- Create: `Chaos/Services/College/PictureFormat.cs`, `Chaos/Services/College/CollegePictureStore.cs`, `Chaos/Services/College/PictureUploads.cs`
- Test: `Tests/Chaos.Tests/College/PictureFormatTests.cs`, `Tests/Chaos.Tests/College/CollegePictureStoreTests.cs`, `Tests/Chaos.Tests/College/PictureUploadsTests.cs` (new)

**Acceptance Criteria:**
- [ ] `PictureFormat.TryRead` returns the extension and size of a PNG (IHDR) and a baseline or progressive JPEG (SOF0/SOF2), and false for anything else.
- [ ] `CollegePictureStore.Add` refuses data over `MaxPictureBytes` (TooLarge), a hash that doesn't match (WrongHash), unreadable data (NotAPicture) and pictures over 400 x 300 (TooBig); it accepts PNG and JPEG and writes `<hash>.png` / `<hash>.jpg`.
- [ ] `Sweep` deletes a picture only after it has been unreferenced for `UnusedPictureHours` across sweeps.
- [ ] `PictureUploads.Begin` replies Have for a stored picture, Send for a new one, and Refused past 10 new pictures an hour or 5 MB a day; `AddPart` replies only on the last part (Accepted or Refused); open uploads expire after 2 minutes without a part.

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/Picture*Tests/*"` and `--treenode-filter "/*/*/CollegePictureStoreTests/*"` -> all pass.

**Steps:**

- [ ] **Step 1: Write the failing format tests.** Create `Tests/Chaos.Tests/College/PictureFormatTests.cs`. The helpers build minimal headers by hand; they are reused by the other picture tests through `PictureFormatTests.Png(...)` and `Jpeg(...)`:

```csharp
using Chaos.Services.College;
using FluentAssertions;

namespace Chaos.Tests.College;

public sealed class PictureFormatTests
{
    public static byte[] Png(int width, int height, int padTo = 64)
    {
        var bytes = new List<byte> { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R' };
        bytes.AddRange(BigEndian(width));
        bytes.AddRange(BigEndian(height));

        while (bytes.Count < padTo)
            bytes.Add(0);

        return bytes.ToArray();
    }

    public static byte[] Jpeg(int width, int height, byte sof = 0xC0, int padTo = 64)
    {
        var bytes = new List<byte> { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x04, 0x00, 0x00 };
        bytes.AddRange([0xFF, sof, 0x00, 0x11, 0x08, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width]);

        while (bytes.Count < padTo)
            bytes.Add(0);

        return bytes.ToArray();
    }

    private static byte[] BigEndian(int value) => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

    [Test]
    public void A_png_reports_its_size()
    {
        PictureFormat.TryRead(Png(400, 300), out var ext, out var width, out var height).Should().BeTrue();
        (ext, width, height).Should().Be(("png", 400, 300));
    }

    [Test]
    public void A_jpeg_reports_its_size_past_other_segments()
    {
        PictureFormat.TryRead(Jpeg(320, 240), out var ext, out var width, out var height).Should().BeTrue();
        (ext, width, height).Should().Be(("jpg", 320, 240));

        PictureFormat.TryRead(Jpeg(10, 20, 0xC2), out _, out var w2, out var h2).Should().BeTrue();
        (w2, h2).Should().Be((10, 20));
    }

    [Test]
    public void Other_data_is_not_a_picture()
    {
        PictureFormat.TryRead([1, 2, 3], out _, out _, out _).Should().BeFalse();
        PictureFormat.TryRead("GIF89a................"u8.ToArray(), out _, out _, out _).Should().BeFalse();
        PictureFormat.TryRead([0xFF, 0xD8, 0xFF], out _, out _, out _).Should().BeFalse();
    }

    [Test]
    public void Hash_is_lower_case_sha256_hex()
        => PictureFormat.Hash("abc"u8.ToArray()).Should().Be("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
}
```

- [ ] **Step 2: Write `PictureFormat`.** Create `Chaos/Services/College/PictureFormat.cs`:

```csharp
using System.Security.Cryptography;

namespace Chaos.Services.College;

/// <summary>Reads a PNG or JPEG's size from its header, and hashes picture bytes. No decoding.</summary>
public static class PictureFormat
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    public static bool TryRead(ReadOnlySpan<byte> bytes, out string extension, out int width, out int height)
    {
        extension = "";
        width = height = 0;

        if ((bytes.Length >= 24) && bytes[..8].SequenceEqual(PngSignature) && bytes[12..16].SequenceEqual("IHDR"u8))
        {
            width = ReadInt32(bytes[16..20]);
            height = ReadInt32(bytes[20..24]);
            extension = "png";

            return (width > 0) && (height > 0);
        }

        if ((bytes.Length < 4) || (bytes[0] != 0xFF) || (bytes[1] != 0xD8))
            return false;

        var at = 2;

        while (at + 4 <= bytes.Length)
        {
            if (bytes[at] != 0xFF)
                return false;

            var marker = bytes[at + 1];
            var length = (bytes[at + 2] << 8) | bytes[at + 3];

            if (IsStartOfFrame(marker))
            {
                if (at + 9 > bytes.Length)
                    return false;

                height = (bytes[at + 5] << 8) | bytes[at + 6];
                width = (bytes[at + 7] << 8) | bytes[at + 8];
                extension = "jpg";

                return (width > 0) && (height > 0);
            }

            if (length < 2)
                return false;

            at += 2 + length;
        }

        return false;
    }

    private static bool IsStartOfFrame(byte marker) => marker is >= 0xC0 and <= 0xCF and not (0xC4 or 0xC8 or 0xCC);

    private static int ReadInt32(ReadOnlySpan<byte> b) => (b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3];
}
```

If `Convert.ToHexStringLower` doesn't exist in this target, use `Convert.ToHexString(...).ToLowerInvariant()`.

- [ ] **Step 3: Write the failing store tests.** Create `Tests/Chaos.Tests/College/CollegePictureStoreTests.cs`:

```csharp
using Chaos.Services.College;
using FluentAssertions;

namespace Chaos.Tests.College;

public sealed class CollegePictureStoreTests
{
    private static readonly DateTime Noon = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    private static CollegePictureStore Create(out string dir)
    {
        dir = Path.Combine(Path.GetTempPath(), "college-pictures-" + Guid.NewGuid().ToString("N"));

        return new CollegePictureStore(dir, new CollegeOptions());
    }

    [Test]
    public void A_png_and_a_jpeg_are_stored_by_hash()
    {
        var store = Create(out var dir);
        var png = PictureFormatTests.Png(400, 300);
        var jpg = PictureFormatTests.Jpeg(100, 100);

        store.Add(PictureFormat.Hash(png), png).Should().Be(PictureProblem.None);
        store.Add(PictureFormat.Hash(jpg), jpg).Should().Be(PictureProblem.None);

        File.Exists(Path.Combine(dir, PictureFormat.Hash(png) + ".png")).Should().BeTrue();
        File.Exists(Path.Combine(dir, PictureFormat.Hash(jpg) + ".jpg")).Should().BeTrue();
        store.Has(PictureFormat.Hash(png)).Should().BeTrue();
        store.Read(PictureFormat.Hash(jpg)).Should().Equal(jpg);
    }

    [Test]
    public void Bad_pictures_are_refused()
    {
        var store = Create(out _);
        var big = PictureFormatTests.Png(401, 300);
        var huge = PictureFormatTests.Png(10, 10, 204_801);
        var junk = new byte[] { 1, 2, 3, 4 };
        var good = PictureFormatTests.Png(10, 10);

        store.Add(PictureFormat.Hash(big), big).Should().Be(PictureProblem.TooBig);
        store.Add(PictureFormat.Hash(huge), huge).Should().Be(PictureProblem.TooLarge);
        store.Add(PictureFormat.Hash(junk), junk).Should().Be(PictureProblem.NotAPicture);
        store.Add(new string('0', 64), good).Should().Be(PictureProblem.WrongHash);
        store.Has(PictureFormat.Hash(good)).Should().BeFalse();
    }

    [Test]
    public void Unused_pictures_go_after_a_day_of_sweeps()
    {
        var store = Create(out _);
        var kept = PictureFormatTests.Png(10, 10);
        var unused = PictureFormatTests.Png(20, 20);
        store.Add(PictureFormat.Hash(kept), kept);
        store.Add(PictureFormat.Hash(unused), unused);
        var referenced = new HashSet<string> { PictureFormat.Hash(kept) };

        store.Sweep(referenced, Noon).Should().Be(0);
        store.Sweep(referenced, Noon.AddHours(23)).Should().Be(0);
        store.Sweep(referenced, Noon.AddHours(24)).Should().Be(1);

        store.Has(PictureFormat.Hash(unused)).Should().BeFalse();
        store.Has(PictureFormat.Hash(kept)).Should().BeTrue();
    }

    [Test]
    public void A_picture_used_again_starts_its_clock_over()
    {
        var store = Create(out _);
        var png = PictureFormatTests.Png(10, 10);
        var hash = PictureFormat.Hash(png);
        store.Add(hash, png);

        store.Sweep(new HashSet<string>(), Noon);
        store.Sweep(new HashSet<string> { hash }, Noon.AddHours(12));
        store.Sweep(new HashSet<string>(), Noon.AddHours(13));

        store.Sweep(new HashSet<string>(), Noon.AddHours(30)).Should().Be(0);
        store.Has(hash).Should().BeTrue();
    }
}
```

- [ ] **Step 4: Write the store.** Create `Chaos/Services/College/CollegePictureStore.cs`:

```csharp
using Microsoft.Extensions.Options;

namespace Chaos.Services.College;

public enum PictureProblem
{
    None,
    TooLarge,
    WrongHash,
    NotAPicture,
    TooBig
}

public interface ICollegePictureStore
{
    bool Has(string hash);

    /// <summary>Checks and stores a picture. An already-stored picture returns <see cref="PictureProblem.None" />.</summary>
    PictureProblem Add(string hash, byte[] bytes);

    byte[]? Read(string hash);

    /// <summary>Deletes pictures not in <paramref name="referenced" /> for the configured hours. Returns how many went.</summary>
    int Sweep(IReadOnlySet<string> referenced, DateTime now);
}

/// <summary>One file per picture, named by its SHA-256 hash, in <c>WorksDirectory/Pictures</c>.</summary>
public sealed class CollegePictureStore : ICollegePictureStore
{
    private readonly string Directory;
    private readonly CollegeOptions Options;
    private readonly Lock Sync = new();
    private readonly Dictionary<string, DateTime> UnusedSince = new(StringComparer.Ordinal);

    public CollegePictureStore(IOptions<CollegeOptions> options)
        : this(Path.Combine(options.Value.WorksDirectory, "Pictures"), options.Value) { }

    public CollegePictureStore(string directory, CollegeOptions options)
    {
        Directory = directory;
        Options = options;
        System.IO.Directory.CreateDirectory(directory);
    }

    private string? PathOf(string hash)
    {
        if (!PieceRules.IsHash(hash))
            return null;

        foreach (var ext in (string[])["png", "jpg"])
        {
            var path = Path.Combine(Directory, $"{hash}.{ext}");

            if (File.Exists(path))
                return path;
        }

        return null;
    }

    public bool Has(string hash)
    {
        using var scope = Sync.EnterScope();

        return PathOf(hash) is not null;
    }

    public PictureProblem Add(string hash, byte[] bytes)
    {
        if (bytes.Length > Options.MaxPictureBytes)
            return PictureProblem.TooLarge;

        if (!PieceRules.IsHash(hash) || (PictureFormat.Hash(bytes) != hash))
            return PictureProblem.WrongHash;

        if (!PictureFormat.TryRead(bytes, out var ext, out var width, out var height))
            return PictureProblem.NotAPicture;

        if ((width > Chaos.DarkAges.Definitions.CollegeProtocol.MAX_PICTURE_WIDTH)
            || (height > Chaos.DarkAges.Definitions.CollegeProtocol.MAX_PICTURE_HEIGHT))
            return PictureProblem.TooBig;

        using var scope = Sync.EnterScope();

        if (PathOf(hash) is not null)
            return PictureProblem.None;

        var final = Path.Combine(Directory, $"{hash}.{ext}");
        var temp = final + ".tmp";
        File.WriteAllBytes(temp, bytes);
        File.Move(temp, final, true);

        return PictureProblem.None;
    }

    public byte[]? Read(string hash)
    {
        using var scope = Sync.EnterScope();

        return PathOf(hash) is { } path ? File.ReadAllBytes(path) : null;
    }

    public int Sweep(IReadOnlySet<string> referenced, DateTime now)
    {
        using var scope = Sync.EnterScope();

        var deleted = 0;

        foreach (var path in System.IO.Directory.EnumerateFiles(Directory))
        {
            if (path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                continue;

            var hash = Path.GetFileNameWithoutExtension(path);

            if (referenced.Contains(hash))
            {
                UnusedSince.Remove(hash);

                continue;
            }

            if (!UnusedSince.TryGetValue(hash, out var since))
            {
                UnusedSince[hash] = now;

                continue;
            }

            if (now - since < TimeSpan.FromHours(Options.UnusedPictureHours))
                continue;

            File.Delete(path);
            UnusedSince.Remove(hash);
            deleted++;
        }

        return deleted;
    }
}
```

Add `using Chaos.DarkAges.Definitions;` at the top and shorten the two `CollegeProtocol` references if that compiles cleanly.

- [ ] **Step 5: Write the failing upload tests.** Create `Tests/Chaos.Tests/College/PictureUploadsTests.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Services.College;
using FluentAssertions;

namespace Chaos.Tests.College;

public sealed class PictureUploadsTests
{
    private static readonly DateTime Noon = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    private static PictureUploads Create(out CollegePictureStore store)
    {
        var options = new CollegeOptions();
        store = new CollegePictureStore(Path.Combine(Path.GetTempPath(), "college-up-" + Guid.NewGuid().ToString("N")), options);

        return new PictureUploads(store, options);
    }

    private static UploadReply? SendAll(PictureUploads uploads, string name, byte[] bytes, DateTime now)
    {
        var hash = PictureFormat.Hash(bytes);
        var parts = bytes.Chunk(CollegeProtocol.PART_SIZE).ToList();
        UploadReply? last = null;

        for (var i = 0; i < parts.Count; i++)
            last = uploads.AddPart(name, hash, i, parts.Count, parts[i], now);

        return last;
    }

    [Test]
    public void A_new_picture_is_sent_in_parts_and_accepted_on_the_last()
    {
        var uploads = Create(out var store);
        var png = PictureFormatTests.Png(300, 200, 70_000);
        var hash = PictureFormat.Hash(png);

        uploads.Begin("Aroha", hash, png.Length, Noon).Reply.Should().Be(CollegePictureReply.Send);

        var parts = png.Chunk(CollegeProtocol.PART_SIZE).ToList();
        uploads.AddPart("Aroha", hash, 0, parts.Count, parts[0], Noon).Should().BeNull();
        uploads.AddPart("Aroha", hash, 1, parts.Count, parts[1], Noon).Should().BeNull();
        uploads.AddPart("Aroha", hash, 2, parts.Count, parts[2], Noon)!.Reply.Should().Be(CollegePictureReply.Accepted);

        store.Has(hash).Should().BeTrue();
        uploads.Begin("Brannoc", hash, png.Length, Noon).Reply.Should().Be(CollegePictureReply.Have);
    }

    [Test]
    public void A_bad_picture_is_refused_on_the_last_part()
    {
        var uploads = Create(out var store);
        var big = PictureFormatTests.Png(800, 600);
        uploads.Begin("Aroha", PictureFormat.Hash(big), big.Length, Noon);

        SendAll(uploads, "Aroha", big, Noon)!.Reply.Should().Be(CollegePictureReply.Refused);
        store.Has(PictureFormat.Hash(big)).Should().BeFalse();
    }

    [Test]
    public void Begin_refuses_a_bad_length_or_hash()
    {
        var uploads = Create(out _);

        uploads.Begin("Aroha", new string('a', 64), 0, Noon).Reply.Should().Be(CollegePictureReply.Refused);
        uploads.Begin("Aroha", new string('a', 64), 204_801, Noon).Reply.Should().Be(CollegePictureReply.Refused);
        uploads.Begin("Aroha", "nothex", 100, Noon).Reply.Should().Be(CollegePictureReply.Refused);
    }

    [Test]
    public void Ten_new_pictures_an_hour_is_the_cap()
    {
        var uploads = Create(out _);

        for (var i = 1; i <= 10; i++)
        {
            var png = PictureFormatTests.Png(i, i);
            uploads.Begin("Aroha", PictureFormat.Hash(png), png.Length, Noon).Reply.Should().Be(CollegePictureReply.Send);
            SendAll(uploads, "Aroha", png, Noon)!.Reply.Should().Be(CollegePictureReply.Accepted);
        }

        var eleventh = PictureFormatTests.Png(11, 11);
        uploads.Begin("Aroha", PictureFormat.Hash(eleventh), eleventh.Length, Noon).Reply.Should().Be(CollegePictureReply.Refused);
        uploads.Begin("Aroha", PictureFormat.Hash(eleventh), eleventh.Length, Noon.AddHours(1)).Reply.Should().Be(CollegePictureReply.Send);
        uploads.Begin("Brannoc", PictureFormat.Hash(eleventh), eleventh.Length, Noon).Reply.Should().Be(CollegePictureReply.Send);
    }

    [Test]
    public void Five_megabytes_a_day_is_the_cap()
    {
        var uploads = Create(out _);

        for (var i = 0; i < 26; i++)
        {
            var png = PictureFormatTests.Png(i + 1, 1, 200_000);
            var at = Noon.AddHours(i * 0.5);
            uploads.Begin("Aroha", PictureFormat.Hash(png), png.Length, at).Reply.Should().Be(CollegePictureReply.Send);
            SendAll(uploads, "Aroha", png, at);
        }

        var next = PictureFormatTests.Png(99, 1, 200_000);
        uploads.Begin("Aroha", PictureFormat.Hash(next), next.Length, Noon.AddHours(13)).Reply.Should().Be(CollegePictureReply.Refused);
    }

    [Test]
    public void An_upload_with_no_parts_for_two_minutes_expires()
    {
        var uploads = Create(out _);
        var png = PictureFormatTests.Png(10, 10);
        var hash = PictureFormat.Hash(png);
        uploads.Begin("Aroha", hash, png.Length, Noon);

        uploads.Expire(Noon.AddMinutes(2));

        uploads.AddPart("Aroha", hash, 0, 1, png, Noon.AddMinutes(2))!.Reply.Should().Be(CollegePictureReply.Refused);
    }
}
```

The 5 MB test uploads 26 pictures of 200,000 bytes (5.2 MB). The first 26 begins pass because each check counts only bytes already accepted. Every 2 hours stays under 10 an hour, because they are 30 minutes apart.

- [ ] **Step 6: Write `PictureUploads`.** Create `Chaos/Services/College/PictureUploads.cs`:

```csharp
using Chaos.DarkAges.Definitions;

namespace Chaos.Services.College;

public sealed record UploadReply(CollegePictureReply Reply, string Message = "");

/// <summary>
///     Puts uploaded pictures together from their parts, then hands them to the store. Each character may have a few uploads
///     open, and is capped per hour and per day on pictures the server didn't already have.
/// </summary>
public sealed class PictureUploads(ICollegePictureStore store, CollegeOptions options)
{
    private const int MAX_OPEN_PER_CHARACTER = 5;
    private static readonly TimeSpan IdleLimit = TimeSpan.FromMinutes(2);

    public const string UPLOAD_FAILED = "That picture didn't upload. Try again.";
    public const string TOO_MANY = "You've uploaded a lot today. Try later.";

    private sealed class Pending
    {
        public required int Length { get; init; }
        public required byte[]?[] Parts { get; init; }
        public DateTime LastUtc { get; set; }
    }

    private readonly Dictionary<(string Name, string Hash), Pending> Open = new();
    private readonly Lock Sync = new();
    private readonly Dictionary<string, List<(DateTime Utc, int Bytes)>> Usage = new(StringComparer.OrdinalIgnoreCase);

    public UploadReply Begin(string name, string hash, int length, DateTime now)
    {
        if (!PieceRules.IsHash(hash) || (length <= 0) || (length > options.MaxPictureBytes))
            return new UploadReply(CollegePictureReply.Refused, UPLOAD_FAILED);

        if (store.Has(hash))
            return new UploadReply(CollegePictureReply.Have);

        using var scope = Sync.EnterScope();

        if (!WithinCaps(name, length, now) || (Open.Keys.Count(k => k.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) >= MAX_OPEN_PER_CHARACTER))
            return new UploadReply(CollegePictureReply.Refused, TOO_MANY);

        Open[(name.ToLowerInvariant(), hash)] = new Pending
        {
            Length = length,
            Parts = new byte[]?[CollegeProtocol.PartCount(length)],
            LastUtc = now
        };

        return new UploadReply(CollegePictureReply.Send);
    }

    /// <summary>Null until the last part arrives; then Accepted or Refused. A part for no open upload is Refused.</summary>
    public UploadReply? AddPart(string name, string hash, int index, int count, byte[] data, DateTime now)
    {
        byte[] bytes;

        using (Sync.EnterScope())
        {
            var key = (name.ToLowerInvariant(), hash);

            if (!Open.TryGetValue(key, out var pending)
                || (count != pending.Parts.Length)
                || (index < 0)
                || (index >= count)
                || (data.Length > CollegeProtocol.PART_SIZE))
            {
                Open.Remove(key);

                return new UploadReply(CollegePictureReply.Refused, UPLOAD_FAILED);
            }

            pending.Parts[index] = data;
            pending.LastUtc = now;

            if (pending.Parts.Any(p => p is null))
                return null;

            Open.Remove(key);
            bytes = pending.Parts.SelectMany(p => p!).ToArray();

            if (bytes.Length != pending.Length)
                return new UploadReply(CollegePictureReply.Refused, UPLOAD_FAILED);
        }

        var problem = store.Add(hash, bytes);

        if (problem != PictureProblem.None)
            return new UploadReply(CollegePictureReply.Refused, Problem(problem));

        using (Sync.EnterScope())
            UsageOf(name).Add((now, bytes.Length));

        return new UploadReply(CollegePictureReply.Accepted);
    }

    public void Expire(DateTime now)
    {
        using var scope = Sync.EnterScope();

        foreach (var key in Open.Where(kv => now - kv.Value.LastUtc >= IdleLimit).Select(kv => kv.Key).ToList())
            Open.Remove(key);
    }

    public static string Problem(PictureProblem problem)
        => problem switch
        {
            PictureProblem.TooLarge    => "That picture is too large.",
            PictureProblem.TooBig      => "Pictures can be 400 x 300 at most.",
            PictureProblem.NotAPicture => "Only PNG and JPG pictures work.",
            _                          => UPLOAD_FAILED
        };

    private List<(DateTime Utc, int Bytes)> UsageOf(string name)
    {
        if (!Usage.TryGetValue(name, out var list))
            Usage[name] = list = [];

        return list;
    }

    private bool WithinCaps(string name, int length, DateTime now)
    {
        var list = UsageOf(name);
        list.RemoveAll(u => now - u.Utc >= TimeSpan.FromDays(1));

        var lastHour = list.Count(u => now - u.Utc < TimeSpan.FromHours(1));
        var lastDay = list.Sum(u => u.Bytes);

        return (lastHour < options.PictureUploadsPerHour) && (lastDay + length <= options.PictureBytesPerDay);
    }
}
```

- [ ] **Step 7: Run the tests.** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/PictureFormatTests/*"`, then `PictureUploadsTests`, then `CollegePictureStoreTests`. Expected: all pass (4 + 6 + 4).

```json:metadata
{"files": ["Chaos/Services/College/PictureFormat.cs", "Chaos/Services/College/CollegePictureStore.cs", "Chaos/Services/College/PictureUploads.cs", "Tests/Chaos.Tests/College/PictureFormatTests.cs", "Tests/Chaos.Tests/College/CollegePictureStoreTests.cs", "Tests/Chaos.Tests/College/PictureUploadsTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/PictureUploadsTests/*\"", "acceptanceCriteria": ["PNG and JPEG sizes read from headers", "store refuses TooLarge/WrongHash/NotAPicture/TooBig", "sweep deletes after 24 unreferenced hours", "upload caps 10/hour and 5 MB/day; expiry after 2 minutes"], "modelTier": "standard"}
```

---

### Task 4: Piece store (server worktree)

**Goal:** Save, load and delete pieces as one JSON file each, and list every picture hash pieces use.

**Files:**
- Create: `Chaos/Services/College/CollegePieceStore.cs`
- Test: `Tests/Chaos.Tests/College/CollegePieceStoreTests.cs` (new)

**Acceptance Criteria:**
- [ ] A saved piece loads back equal, with enums written as names.
- [ ] Saving writes `<id>.json` through a temp file; `Delete` removes it; loading a missing or malformed id returns null.
- [ ] Ids that aren't 32 lower-case hex characters are refused (no path tricks).
- [ ] `AllPictureHashes` returns every picture hash in every piece file.

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/CollegePieceStoreTests/*"` -> all pass.

**Steps:**

- [ ] **Step 1: Write the failing tests.** Create `Tests/Chaos.Tests/College/CollegePieceStoreTests.cs`:

```csharp
using Chaos.Services.College;
using FluentAssertions;

namespace Chaos.Tests.College;

public sealed class CollegePieceStoreTests
{
    private static CollegePieceStore Create(out string dir)
    {
        dir = Path.Combine(Path.GetTempPath(), "college-pieces-" + Guid.NewGuid().ToString("N"));

        return new CollegePieceStore(dir);
    }

    private static CollegePiece Piece(string hash)
        => new()
        {
            Id = CollegePiece.NewId(),
            Subject = CollegeSubject.Lore,
            Title = "Of Sgrios",
            UpdatedUtc = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc),
            Blocks =
            [
                new PieceBlock { Type = PieceBlockType.Heading, Text = "I" },
                new PieceBlock { Type = PieceBlockType.Text, Text = "line one\nline two" },
                new PieceBlock { Type = PieceBlockType.Picture, Hash = hash }
            ]
        };

    [Test]
    public void A_piece_round_trips_with_enum_names()
    {
        var store = Create(out var dir);
        var piece = Piece(new string('a', 64));

        store.Save(piece);

        store.Load(piece.Id).Should().BeEquivalentTo(piece);
        File.ReadAllText(Path.Combine(dir, piece.Id + ".json")).Should().Contain("\"Heading\"").And.Contain("\"Lore\"");
    }

    [Test]
    public void Delete_removes_the_file_and_missing_or_bad_ids_load_as_null()
    {
        var store = Create(out var dir);
        var piece = Piece(new string('a', 64));
        store.Save(piece);

        store.Delete(piece.Id);

        store.Load(piece.Id).Should().BeNull();
        store.Load("../../etc").Should().BeNull();
        File.WriteAllText(Path.Combine(dir, new string('c', 32) + ".json"), "{ not json");
        store.Load(new string('c', 32)).Should().BeNull();
    }

    [Test]
    public void Saving_a_bad_id_throws()
    {
        var store = Create(out _);
        var piece = Piece(new string('a', 64));
        piece.Id = "..\\evil";

        var act = () => store.Save(piece);

        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void All_picture_hashes_covers_every_piece()
    {
        var store = Create(out _);
        store.Save(Piece(new string('a', 64)));
        store.Save(Piece(new string('b', 64)));

        store.AllPictureHashes().Should().BeEquivalentTo([new string('a', 64), new string('b', 64)]);
    }
}
```

- [ ] **Step 2: Write the store.** Create `Chaos/Services/College/CollegePieceStore.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Chaos.Services.College;

public interface ICollegePieceStore
{
    CollegePiece? Load(string id);
    void Save(CollegePiece piece);
    void Delete(string id);
    IReadOnlySet<string> AllPictureHashes();
}

/// <summary>One JSON file per piece in <c>WorksDirectory/Pieces</c>, written through a temp file.</summary>
public sealed class CollegePieceStore : ICollegePieceStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string Directory;
    private readonly Lock Sync = new();

    public CollegePieceStore(IOptions<CollegeOptions> options)
        : this(Path.Combine(options.Value.WorksDirectory, "Pieces")) { }

    public CollegePieceStore(string directory)
    {
        Directory = directory;
        System.IO.Directory.CreateDirectory(directory);
    }

    public static bool IsId(string id) => (id.Length == 32) && id.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    private string PathOf(string id) => Path.Combine(Directory, id + ".json");

    public CollegePiece? Load(string id)
    {
        if (!IsId(id))
            return null;

        using var scope = Sync.EnterScope();

        try
        {
            return File.Exists(PathOf(id)) ? JsonSerializer.Deserialize<CollegePiece>(File.ReadAllText(PathOf(id)), Json) : null;
        } catch (JsonException)
        {
            return null;
        }
    }

    public void Save(CollegePiece piece)
    {
        if (!IsId(piece.Id))
            throw new ArgumentException($"Bad piece id '{piece.Id}'", nameof(piece));

        using var scope = Sync.EnterScope();

        var path = PathOf(piece.Id);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(piece, Json));
        File.Move(temp, path, true);
    }

    public void Delete(string id)
    {
        if (!IsId(id))
            return;

        using var scope = Sync.EnterScope();

        File.Delete(PathOf(id));
    }

    public IReadOnlySet<string> AllPictureHashes()
    {
        var hashes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var path in System.IO.Directory.EnumerateFiles(Directory, "*.json"))
            if (Load(Path.GetFileNameWithoutExtension(path)) is { } piece)
                hashes.UnionWith(piece.PictureHashes);

        return hashes;
    }
}
```

- [ ] **Step 3: Run the tests.** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/CollegePieceStoreTests/*"`. Expected: 4 passed.

```json:metadata
{"files": ["Chaos/Services/College/CollegePieceStore.cs", "Tests/Chaos.Tests/College/CollegePieceStoreTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/CollegePieceStoreTests/*\"", "acceptanceCriteria": ["round trip with enum names", "temp-file write, delete, null for missing/malformed", "bad ids refused", "AllPictureHashes covers every piece"], "modelTier": "mechanical"}
```

---
### Task 5: Entry state and `EntryBook` rules (server worktree)

**Goal:** Add the entry, vote, verdict and hand-in records to `CollegeState`, and the pure `EntryBook` that holds every entry rule.

**Files:**
- Create: `Chaos/Services/College/CollegeEntryModels.cs`, `Chaos/Services/College/EntryBook.cs`
- Modify: `Chaos/Services/College/CollegeState.cs` (new collections, draft helpers, `CollegeClass.Prompt`)
- Test: `Tests/Chaos.Tests/College/EntryBookTests.cs` (new)

**Acceptance Criteria:**
- [ ] `CanSubmit` returns NotOpen for Art/Music, AlreadyEntered with an entry in Voting or AwaitingVerdict, NoDraft without a draft, NotEnoughMarks with no free entry and fewer than `EntryCost` marks; otherwise Submitted.
- [ ] `Pay` uses a free entry first, otherwise takes `EntryCost` marks.
- [ ] `Vote` refuses NoSuchEntry, Closed (not Voting, or past `VotingEndsUtc`), OwnEntry, NotAllowed; a judge's second vote replaces the first; comments are cut to 300 characters.
- [ ] `CloseVoting` moves Voting entries at or past their end to AwaitingVerdict.
- [ ] `SetVerdict` refuses NotAllowed (not a moderator), OwnEntry (the author, even an admin), NotWaiting; otherwise sets the verdict (note cut to 500) and status Decided.
- [ ] `Claim` refuses NotYours, NotDecided, NoAward; on success sets Claimed; raises the award only when higher; adds a free entry only for a raised Clave.
- [ ] `Withdraw` works only in Voting; `Dismiss` only for a Decided None verdict; `Remove` works in any status; `SetHidden` only for Claimed; all only for the author except `Remove`.
- [ ] `RemindersDue` returns AwaitingVerdict entries whose voting ended 7 or more days ago and that had no reminder in the last day.

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/EntryBookTests/*"` -> all pass.

**Steps:**

- [ ] **Step 1: Add the records.** Create `Chaos/Services/College/CollegeEntryModels.cs`:

```csharp
namespace Chaos.Services.College;

public enum EntryStatus
{
    Voting,
    AwaitingVerdict,
    Decided,
    Claimed
}

public sealed class CollegeEntry
{
    public int Id { get; set; }
    public string Author { get; set; } = "";
    public CollegeSubject Subject { get; set; }
    public string PieceId { get; set; } = "";
    public string Title { get; set; } = "";
    public DateTime SubmittedUtc { get; set; }
    public DateTime VotingEndsUtc { get; set; }
    public EntryStatus Status { get; set; }

    /// <summary>Judge (lower case) to vote.</summary>
    public Dictionary<string, CollegeVote> Votes { get; set; } = [];

    public CollegeVerdict? Verdict { get; set; }
    public bool Hidden { get; set; }
    public DateTime? LastReminderUtc { get; set; }
    public DateTime? ClaimedUtc { get; set; }

    public bool IsAuthor(string name) => Author.Equals(name, StringComparison.OrdinalIgnoreCase);
}

public sealed class CollegeVote
{
    public string Judge { get; set; } = "";
    public AwardTier Tier { get; set; }
    public string Comment { get; set; } = "";
    public DateTime Utc { get; set; }
}

public sealed class CollegeVerdict
{
    public AwardTier Tier { get; set; }
    public string Note { get; set; } = "";
    public string By { get; set; } = "";
    public DateTime Utc { get; set; }
}

public sealed class CollegeHandIn
{
    public int Id { get; set; }
    public string Teacher { get; set; } = "";
    public CollegeSubject Subject { get; set; }
    public DateTime ClassStartedUtc { get; set; }
    public string Student { get; set; } = "";
    public string PieceId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Prompt { get; set; } = "";
    public DateTime Utc { get; set; }
}

public enum SubmitResult
{
    Submitted,
    NotOpen,
    AlreadyEntered,
    NoDraft,
    NotEnoughMarks
}

public enum VoteResult
{
    Saved,
    NoSuchEntry,
    Closed,
    OwnEntry,
    NotAllowed
}

public enum VerdictResult
{
    Posted,
    NoSuchEntry,
    NotWaiting,
    OwnEntry,
    NotAllowed
}

public enum ClaimResult
{
    Claimed,
    NoSuchEntry,
    NotYours,
    NotDecided,
    NoAward
}
```

- [ ] **Step 2: Extend the state.** In `Chaos/Services/College/CollegeState.cs`, add to `CollegeState` after `History`:

```csharp
    public int NextEntryId { get; set; } = 1;
    public int NextHandInId { get; set; } = 1;

    /// <summary>Character (lower case) to subject name to the draft's piece id.</summary>
    public Dictionary<string, Dictionary<string, string>> Drafts { get; set; } = [];

    public List<CollegeEntry> Entries { get; set; } = [];
    public List<CollegeHandIn> HandIns { get; set; } = [];

    public string? DraftOf(string name, CollegeSubject subject)
        => Drafts.TryGetValue(Key(name), out var drafts) && drafts.TryGetValue(subject.ToString(), out var id) ? id : null;

    public void SetDraft(string name, CollegeSubject subject, string? pieceId)
    {
        var key = Key(name);

        if (!Drafts.TryGetValue(key, out var drafts))
            Drafts[key] = drafts = [];

        if (pieceId is null)
            drafts.Remove(subject.ToString());
        else
            drafts[subject.ToString()] = pieceId;

        if (drafts.Count == 0)
            Drafts.Remove(key);
    }
```

Add `public string Prompt { get; set; } = "";` to `CollegeClass` after `Removed`.

- [ ] **Step 3: Write the failing tests.** Create `Tests/Chaos.Tests/College/EntryBookTests.cs`:

```csharp
using Chaos.Services.College;
using FluentAssertions;

namespace Chaos.Tests.College;

public sealed class EntryBookTests
{
    private static readonly DateTime Noon = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);
    private static readonly CollegeOptions Options = new();

    private static CollegeState WithEntry(out CollegeEntry entry, EntryStatus status = EntryStatus.Voting)
    {
        var state = new CollegeState();
        entry = EntryBook.Add(state, Options, "Aroha", CollegeSubject.History, CollegePiece.NewId(), "The Fall", Noon);
        entry.Status = status;

        return state;
    }

    [Test]
    public void Submit_checks_subject_open_entry_draft_and_marks()
    {
        var state = new CollegeState();

        EntryBook.CanSubmit(state, Options, "Aroha", CollegeSubject.Art).Should().Be(SubmitResult.NotOpen);
        EntryBook.CanSubmit(state, Options, "Aroha", CollegeSubject.History).Should().Be(SubmitResult.NoDraft);

        state.SetDraft("Aroha", CollegeSubject.History, CollegePiece.NewId());
        EntryBook.CanSubmit(state, Options, "Aroha", CollegeSubject.History).Should().Be(SubmitResult.NotEnoughMarks);

        state.StudentFor("Aroha").Marks = 3;
        EntryBook.CanSubmit(state, Options, "aroha", CollegeSubject.History).Should().Be(SubmitResult.Submitted);

        EntryBook.Add(state, Options, "Aroha", CollegeSubject.History, CollegePiece.NewId(), "T", Noon);
        EntryBook.CanSubmit(state, Options, "Aroha", CollegeSubject.History).Should().Be(SubmitResult.AlreadyEntered);
    }

    [Test]
    public void A_waiting_entry_still_blocks_a_new_one_but_a_decided_one_does_not()
    {
        var state = WithEntry(out var entry, EntryStatus.AwaitingVerdict);
        state.SetDraft("Aroha", CollegeSubject.History, CollegePiece.NewId());
        state.StudentFor("Aroha").Marks = 3;

        EntryBook.CanSubmit(state, Options, "Aroha", CollegeSubject.History).Should().Be(SubmitResult.AlreadyEntered);

        entry.Status = EntryStatus.Decided;
        EntryBook.CanSubmit(state, Options, "Aroha", CollegeSubject.History).Should().Be(SubmitResult.Submitted);
    }

    [Test]
    public void Pay_uses_a_free_entry_before_marks()
    {
        var state = new CollegeState();
        var student = state.StudentFor("Aroha");
        student.Marks = 5;
        student.FreeEntries = 1;

        EntryBook.Pay(state, Options, "Aroha").Should().BeTrue();
        (student.Marks, student.FreeEntries).Should().Be((5, 0));

        EntryBook.Pay(state, Options, "Aroha").Should().BeFalse();
        student.Marks.Should().Be(2);
    }

    [Test]
    public void Add_numbers_entries_and_sets_fourteen_days_of_voting()
    {
        var state = new CollegeState();

        var first = EntryBook.Add(state, Options, "Aroha", CollegeSubject.Lore, "p1", "T", Noon);
        var second = EntryBook.Add(state, Options, "Brannoc", CollegeSubject.Lore, "p2", "U", Noon);

        (first.Id, second.Id).Should().Be((1, 2));
        first.Status.Should().Be(EntryStatus.Voting);
        first.VotingEndsUtc.Should().Be(Noon.AddDays(14));
    }

    [Test]
    public void Votes_follow_the_rules_and_can_change()
    {
        var state = WithEntry(out var entry);

        EntryBook.Vote(state, 99, "Judge", true, AwardTier.Clave, "", Noon).Should().Be(VoteResult.NoSuchEntry);
        EntryBook.Vote(state, entry.Id, "aroha", true, AwardTier.Clave, "", Noon).Should().Be(VoteResult.OwnEntry);
        EntryBook.Vote(state, entry.Id, "Judge", false, AwardTier.Clave, "", Noon).Should().Be(VoteResult.NotAllowed);

        EntryBook.Vote(state, entry.Id, "Judge", true, AwardTier.Clave, "ok", Noon).Should().Be(VoteResult.Saved);
        EntryBook.Vote(state, entry.Id, "judge", true, AwardTier.Kingdom, new string('c', 400), Noon).Should().Be(VoteResult.Saved);

        entry.Votes.Should().ContainSingle();
        entry.Votes["judge"].Tier.Should().Be(AwardTier.Kingdom);
        entry.Votes["judge"].Comment.Should().HaveLength(300);

        EntryBook.Vote(state, entry.Id, "Judge", true, AwardTier.Clave, "", entry.VotingEndsUtc).Should().Be(VoteResult.Closed);
    }

    [Test]
    public void Close_voting_moves_finished_entries_to_awaiting_verdict()
    {
        var state = WithEntry(out var entry);

        EntryBook.CloseVoting(state, Noon.AddDays(13)).Should().BeEmpty();
        EntryBook.CloseVoting(state, Noon.AddDays(14)).Should().ContainSingle();

        entry.Status.Should().Be(EntryStatus.AwaitingVerdict);
    }

    [Test]
    public void Verdicts_need_a_moderator_who_is_not_the_author_and_a_waiting_entry()
    {
        var state = WithEntry(out var entry);

        EntryBook.SetVerdict(state, entry.Id, "Dir", true, AwardTier.Village, "", Noon).Should().Be(VerdictResult.NotWaiting);

        entry.Status = EntryStatus.AwaitingVerdict;
        EntryBook.SetVerdict(state, entry.Id, "Judge", false, AwardTier.Village, "", Noon).Should().Be(VerdictResult.NotAllowed);
        EntryBook.SetVerdict(state, entry.Id, "Aroha", true, AwardTier.Village, "", Noon).Should().Be(VerdictResult.OwnEntry);

        EntryBook.SetVerdict(state, entry.Id, "Dir", true, AwardTier.Village, new string('n', 600), Noon).Should().Be(VerdictResult.Posted);
        entry.Status.Should().Be(EntryStatus.Decided);
        entry.Verdict!.Tier.Should().Be(AwardTier.Village);
        entry.Verdict.Note.Should().HaveLength(500);
        entry.Verdict.By.Should().Be("Dir");
    }

    [Test]
    public void Claim_raises_the_award_only_when_higher()
    {
        var state = WithEntry(out var entry, EntryStatus.Decided);
        entry.Verdict = new CollegeVerdict { Tier = AwardTier.Village };

        EntryBook.Claim(state, entry.Id, "Brannoc", Noon, out _).Should().Be(ClaimResult.NotYours);
        EntryBook.Claim(state, entry.Id, "Aroha", Noon, out var raised).Should().Be(ClaimResult.Claimed);

        raised.Should().BeTrue();
        entry.Status.Should().Be(EntryStatus.Claimed);
        entry.ClaimedUtc.Should().Be(Noon);
        state.AwardOf("Aroha", CollegeSubject.History).Should().Be(AwardTier.Village);

        var lower = EntryBook.Add(state, Options, "Aroha", CollegeSubject.History, "p", "T", Noon);
        lower.Status = EntryStatus.Decided;
        lower.Verdict = new CollegeVerdict { Tier = AwardTier.Clave };

        EntryBook.Claim(state, lower.Id, "Aroha", Noon, out var raisedAgain).Should().Be(ClaimResult.Claimed);
        raisedAgain.Should().BeFalse();
        state.AwardOf("Aroha", CollegeSubject.History).Should().Be(AwardTier.Village);
        state.StudentFor("Aroha").FreeEntries.Should().Be(0);
    }

    [Test]
    public void A_raised_clave_gives_a_free_entry()
    {
        var state = WithEntry(out var entry, EntryStatus.Decided);
        entry.Verdict = new CollegeVerdict { Tier = AwardTier.Clave };

        EntryBook.Claim(state, entry.Id, "Aroha", Noon, out _).Should().Be(ClaimResult.Claimed);

        state.StudentFor("Aroha").FreeEntries.Should().Be(1);
    }

    [Test]
    public void Claim_refuses_undecided_and_no_award_entries()
    {
        var state = WithEntry(out var entry);

        EntryBook.Claim(state, entry.Id, "Aroha", Noon, out _).Should().Be(ClaimResult.NotDecided);

        entry.Status = EntryStatus.Decided;
        entry.Verdict = new CollegeVerdict { Tier = AwardTier.None };
        EntryBook.Claim(state, entry.Id, "Aroha", Noon, out _).Should().Be(ClaimResult.NoAward);
    }

    [Test]
    public void Withdraw_dismiss_remove_and_hide_follow_the_status()
    {
        var state = WithEntry(out var entry);

        EntryBook.Withdraw(state, entry.Id, "Brannoc").Should().BeNull();
        EntryBook.Withdraw(state, entry.Id, "Aroha").Should().BeSameAs(entry);
        state.Entries.Should().BeEmpty();

        state = WithEntry(out entry, EntryStatus.Decided);
        entry.Verdict = new CollegeVerdict { Tier = AwardTier.Clave };
        EntryBook.Dismiss(state, entry.Id, "Aroha").Should().BeNull();
        entry.Verdict.Tier = AwardTier.None;
        EntryBook.Dismiss(state, entry.Id, "Aroha").Should().BeSameAs(entry);

        state = WithEntry(out entry, EntryStatus.Decided);
        EntryBook.SetHidden(state, entry.Id, "Aroha", true).Should().BeFalse();
        entry.Status = EntryStatus.Claimed;
        EntryBook.SetHidden(state, entry.Id, "Brannoc", true).Should().BeFalse();
        EntryBook.SetHidden(state, entry.Id, "Aroha", true).Should().BeTrue();
        entry.Hidden.Should().BeTrue();

        EntryBook.Remove(state, entry.Id).Should().BeSameAs(entry);
        state.Entries.Should().BeEmpty();
    }

    [Test]
    public void Reminders_start_after_seven_days_and_repeat_daily()
    {
        var state = WithEntry(out var entry, EntryStatus.AwaitingVerdict);
        var ended = entry.VotingEndsUtc;

        EntryBook.RemindersDue(state, Options, ended.AddDays(6)).Should().BeEmpty();
        EntryBook.RemindersDue(state, Options, ended.AddDays(7)).Should().ContainSingle();

        entry.LastReminderUtc = ended.AddDays(7);
        EntryBook.RemindersDue(state, Options, ended.AddDays(7).AddHours(23)).Should().BeEmpty();
        EntryBook.RemindersDue(state, Options, ended.AddDays(8)).Should().ContainSingle();
    }

    [Test]
    public void Days_left_rounds_up_and_stops_at_zero()
    {
        WithEntry(out var entry);

        EntryBook.DaysLeft(entry, Noon).Should().Be(14);
        EntryBook.DaysLeft(entry, Noon.AddDays(13).AddHours(1)).Should().Be(1);
        EntryBook.DaysLeft(entry, Noon.AddDays(20)).Should().Be(0);
    }
}
```

- [ ] **Step 4: Run to see them fail.** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/EntryBookTests/*"`. Expected: build error, `EntryBook` does not exist.

- [ ] **Step 5: Write `EntryBook`.** Create `Chaos/Services/College/EntryBook.cs`:

```csharp
using Chaos.DarkAges.Definitions;

namespace Chaos.Services.College;

/// <summary>Every entry rule: submitting, voting, verdicts, claiming, withdrawing and removing. No game-world access.</summary>
public static class EntryBook
{
    public static CollegeEntry? Find(CollegeState state, int id) => state.Entries.FirstOrDefault(e => e.Id == id);

    public static CollegeEntry? OpenEntry(CollegeState state, string author, CollegeSubject subject)
        => state.Entries.FirstOrDefault(
            e => e.IsAuthor(author) && (e.Subject == subject) && e.Status is EntryStatus.Voting or EntryStatus.AwaitingVerdict);

    public static SubmitResult CanSubmit(CollegeState state, CollegeOptions options, string author, CollegeSubject subject)
    {
        if (!options.IsOpen(subject))
            return SubmitResult.NotOpen;

        if (OpenEntry(state, author, subject) is not null)
            return SubmitResult.AlreadyEntered;

        if (state.DraftOf(author, subject) is null)
            return SubmitResult.NoDraft;

        var student = state.Students.GetValueOrDefault(CollegeState.Key(author));

        return (student is { FreeEntries: > 0 }) || ((student?.Marks ?? 0) >= options.EntryCost)
            ? SubmitResult.Submitted
            : SubmitResult.NotEnoughMarks;
    }

    /// <summary>Takes a free entry if there is one, otherwise the entry cost. Call only after <see cref="CanSubmit" />. True when a free entry was used.</summary>
    public static bool Pay(CollegeState state, CollegeOptions options, string author)
    {
        var student = state.StudentFor(author);

        if (student.FreeEntries > 0)
        {
            student.FreeEntries--;

            return true;
        }

        student.Marks -= options.EntryCost;

        return false;
    }

    public static CollegeEntry Add(
        CollegeState state,
        CollegeOptions options,
        string author,
        CollegeSubject subject,
        string pieceId,
        string title,
        DateTime now)
    {
        var entry = new CollegeEntry
        {
            Id = state.NextEntryId++,
            Author = author,
            Subject = subject,
            PieceId = pieceId,
            Title = title,
            SubmittedUtc = now,
            VotingEndsUtc = now.AddDays(options.VotingDays),
            Status = EntryStatus.Voting
        };

        state.Entries.Add(entry);

        return entry;
    }

    public static VoteResult Vote(CollegeState state, int id, string judge, bool canJudge, AwardTier tier, string comment, DateTime now)
    {
        if (Find(state, id) is not { } entry)
            return VoteResult.NoSuchEntry;

        if ((entry.Status != EntryStatus.Voting) || (now >= entry.VotingEndsUtc))
            return VoteResult.Closed;

        if (entry.IsAuthor(judge))
            return VoteResult.OwnEntry;

        if (!canJudge || !Enum.IsDefined(tier))
            return VoteResult.NotAllowed;

        entry.Votes[CollegeState.Key(judge)] = new CollegeVote
        {
            Judge = judge,
            Tier = tier,
            Comment = Cut(comment, CollegeProtocol.MAX_COMMENT_CHARS),
            Utc = now
        };

        return VoteResult.Saved;
    }

    public static IReadOnlyList<CollegeEntry> CloseVoting(CollegeState state, DateTime now)
    {
        var closed = state.Entries.Where(e => (e.Status == EntryStatus.Voting) && (now >= e.VotingEndsUtc)).ToList();

        foreach (var entry in closed)
            entry.Status = EntryStatus.AwaitingVerdict;

        return closed;
    }

    public static VerdictResult SetVerdict(CollegeState state, int id, string by, bool canModerate, AwardTier tier, string note, DateTime now)
    {
        if (Find(state, id) is not { } entry)
            return VerdictResult.NoSuchEntry;

        if (!canModerate || !Enum.IsDefined(tier))
            return VerdictResult.NotAllowed;

        if (entry.IsAuthor(by))
            return VerdictResult.OwnEntry;

        if (entry.Status != EntryStatus.AwaitingVerdict)
            return VerdictResult.NotWaiting;

        entry.Verdict = new CollegeVerdict
        {
            Tier = tier,
            Note = Cut(note, CollegeProtocol.MAX_NOTE_CHARS),
            By = by,
            Utc = now
        };
        entry.Status = EntryStatus.Decided;

        return VerdictResult.Posted;
    }

    public static ClaimResult Claim(CollegeState state, int id, string author, DateTime now, out bool raised)
    {
        raised = false;

        if (Find(state, id) is not { } entry)
            return ClaimResult.NoSuchEntry;

        if (!entry.IsAuthor(author))
            return ClaimResult.NotYours;

        if ((entry.Status != EntryStatus.Decided) || entry.Verdict is null)
            return ClaimResult.NotDecided;

        var tier = entry.Verdict.Tier;

        if (tier == AwardTier.None)
            return ClaimResult.NoAward;

        entry.Status = EntryStatus.Claimed;
        entry.ClaimedUtc = now;

        if (tier <= state.AwardOf(author, entry.Subject))
            return ClaimResult.Claimed;

        state.SetAward(author, entry.Subject, tier);
        raised = true;

        if (tier == AwardTier.Clave)
            state.StudentFor(author).FreeEntries++;

        return ClaimResult.Claimed;
    }

    public static CollegeEntry? Withdraw(CollegeState state, int id, string author)
        => Take(state, id, e => e.IsAuthor(author) && (e.Status == EntryStatus.Voting));

    public static CollegeEntry? Dismiss(CollegeState state, int id, string author)
        => Take(state, id, e => e.IsAuthor(author) && (e.Status == EntryStatus.Decided) && (e.Verdict?.Tier == AwardTier.None));

    public static CollegeEntry? Remove(CollegeState state, int id) => Take(state, id, _ => true);

    public static bool SetHidden(CollegeState state, int id, string author, bool hidden)
    {
        if (Find(state, id) is not { } entry || !entry.IsAuthor(author) || (entry.Status != EntryStatus.Claimed))
            return false;

        entry.Hidden = hidden;

        return true;
    }

    public static IReadOnlyList<CollegeEntry> RemindersDue(CollegeState state, CollegeOptions options, DateTime now)
        => state.Entries
                .Where(
                    e => (e.Status == EntryStatus.AwaitingVerdict)
                         && (now - e.VotingEndsUtc >= TimeSpan.FromDays(options.VerdictReminderDays))
                         && (e.LastReminderUtc is null || (now - e.LastReminderUtc.Value >= TimeSpan.FromDays(1))))
                .ToList();

    public static int DaysLeft(CollegeEntry entry, DateTime now) => Math.Max(0, (int)Math.Ceiling((entry.VotingEndsUtc - now).TotalDays));

    public static string Cut(string text, int max)
    {
        var trimmed = PieceRules.Clean(text).Trim();

        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }

    private static CollegeEntry? Take(CollegeState state, int id, Func<CollegeEntry, bool> allowed)
    {
        if (Find(state, id) is not { } entry || !allowed(entry))
            return null;

        state.Entries.Remove(entry);

        return entry;
    }
}
```

- [ ] **Step 6: Run the tests.** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/EntryBookTests/*"`. Expected: 13 passed. Then run all College tests (`--treenode-filter "/*/*/*/*"` is the whole suite; use `"/*/Chaos.Tests.College/*/*"`) to check part 1 still passes.

```json:metadata
{"files": ["Chaos/Services/College/CollegeEntryModels.cs", "Chaos/Services/College/EntryBook.cs", "Chaos/Services/College/CollegeState.cs", "Tests/Chaos.Tests/College/EntryBookTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/EntryBookTests/*\"", "acceptanceCriteria": ["CanSubmit results", "Pay uses free entry first", "Vote rules and replacement", "CloseVoting", "SetVerdict rules incl. own entry", "Claim raises only when higher; Clave token only when raised", "Withdraw/Dismiss/Remove/SetHidden status rules", "RemindersDue after 7 days, daily"], "modelTier": "mechanical"}
```

---

### Task 6: Entry flows in `CollegeService`, mail and the tick (server worktree)

**Goal:** Give `CollegeService` the draft, submit, hand-in, vote, verdict, claim, withdraw, dismiss, remove, hide and query members, send mail through a new `ICollegeMail`, and extend `Tick` with voting closure, reminders and the hourly cleanup.

**Files:**
- Create: `Chaos/Services/College/ICollegeMail.cs`, `Chaos/Services/College/CollegeMail.cs`, `Chaos/Services/College/CollegeService.Entries.cs`
- Modify: `Chaos/Services/College/CollegeService.cs` (make `partial`; constructor; `Tick`; Activity rule in `StartWalkIn`; `Copy` keeps `Prompt`)
- Modify: `Chaos/Services/College/Timetable.cs` (Activity bookable for writing subjects)
- Modify: `Chaos/Services/College/CollegeTickService.cs` (also expires uploads)
- Modify: `Tests/Chaos.Tests/College/CollegeServiceTests.cs` (`Create` passes the new dependencies), `Tests/Chaos.Tests/College/TimetableTests.cs` (Activity rule)
- Test: `Tests/Chaos.Tests/College/CollegeEntryServiceTests.cs` (new)

**Acceptance Criteria:**
- [ ] `SaveDraft` normalizes and checks the piece, keeps one piece id per character and subject, and refuses closed subjects and broken pieces with the matching `CollegeText` message.
- [ ] `Submit` writes the entry's piece file first; if that throws, marks and free entries are unchanged and the draft stays. On success it pays, adds the entry, clears and deletes the draft, and refreshes the Educated mark.
- [ ] `HandIn` needs a running Activity class in a writing subject, the sender in that class's room, not the Teacher, not removed; a second hand-in from the same student replaces the first and deletes its piece.
- [ ] `PostVerdict` mails the author `College verdict` with the tier, the note, and "Claim it from the Registrar." for an award. `RemoveEntry` mails `Entry removed`.
- [ ] `Tick` closes voting, mails one `Verdicts waiting` reminder to the Director and every `VerdictReminderNames` name when any entry is due (and stamps `LastReminderUtc`), and at most once an hour deletes expired hand-ins with their pieces and sweeps pictures.
- [ ] `OpenPiece` returns the right context (Own, Judge, Verdict, Gallery, HandIn) or null when the viewer may not read it; the `Judge` context never carries the author.
- [ ] Activity can be booked and started in History, Literature, Lore and Philosophy, and still not in Art or Music.
- [ ] Part 1's College tests still pass.

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.College/*/*"` -> all pass.

**Steps:**

- [ ] **Step 1: Add the mail sender.** Create `Chaos/Services/College/ICollegeMail.cs`:

```csharp
namespace Chaos.Services.College;

/// <summary>In-game mail from the College. Reaches offline players.</summary>
public interface ICollegeMail
{
    void Send(string to, string subject, string body);
}
```

Create `Chaos/Services/College/CollegeMail.cs`:

```csharp
using Chaos.Collections;
using Chaos.Storage.Abstractions;

namespace Chaos.Services.College;

public sealed class CollegeMail(IStore<MailBox> mailStore) : ICollegeMail
{
    public const string AUTHOR = "Mileth College";

    public void Send(string to, string subject, string body)
    {
        //the mailbox's own Post takes its lock and id, as MarketService does
        var mailbox = mailStore.Load(to);
        mailbox.Post(AUTHOR, subject, body, true);
        mailStore.Save(mailbox);
    }
}
```

- [ ] **Step 2: Widen the constructor.** In `Chaos/Services/College/CollegeService.cs` change `public sealed class CollegeService` to `public sealed partial class CollegeService`, add fields `private readonly ICollegePieceStore Pieces;`, `private readonly ICollegePictureStore Pictures;`, `private readonly ICollegeMail Mail;`, and change the constructor to:

```csharp
    public CollegeService(
        IStorage<CollegeState> storage,
        IOptions<CollegeOptions> options,
        ICollegeNotifier notifier,
        ICollegePieceStore pieces,
        ICollegePictureStore pictures,
        ICollegeMail mail,
        TimeProvider time,
        ILogger<CollegeService> logger)
    {
        Storage = storage;
        State = storage.Value;
        Options = options.Value;
        Ledger = new MarkLedger(Options);
        Notifier = notifier;
        Pieces = pieces;
        Pictures = pictures;
        Mail = mail;
        Time = time;
        Logger = logger;
    }
```

In `CollegeServiceTests.Create`, pass `new Mock<ICollegePieceStore>().Object, new Mock<ICollegePictureStore>().Object, new Mock<ICollegeMail>().Object` in the same positions.

- [ ] **Step 3: Allow Activity for writing subjects.** In `StartWalkIn`, replace `if (format == ClassFormat.Activity)` with `if ((format == ClassFormat.Activity) && !CollegeOptions.IsWritingSubject(subject))`. In `Timetable.TryBook` make the same change (`subject` is a parameter there). In `Copy(CollegeClass c)` add `Prompt = c.Prompt`. In `TimetableTests`, change any test that expects Activity to be refused for every subject so it expects `FormatNotAvailable` for Art and `Booked` for History, and add that pair if no test covers it.

- [ ] **Step 4: Extend `Tick`.** Add a field `private DateTime LastCleanupUtc;` and, in `Tick`, before the final `SaveLocked();`, add:

```csharp
        EntryBook.CloseVoting(State, now);
        SendRemindersLocked(now);

        if (now - LastCleanupUtc >= TimeSpan.FromHours(1))
        {
            LastCleanupUtc = now;
            CleanupLocked(now);
        }
```

`SendRemindersLocked` and `CleanupLocked` live in the new partial file (Step 6).

- [ ] **Step 5: Write the failing tests.** Create `Tests/Chaos.Tests/College/CollegeEntryServiceTests.cs`. It uses the real file stores in a temp folder and a mocked mail sender:

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

public sealed class CollegeEntryServiceTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    private sealed record Rig(
        CollegeService Service,
        CollegeState State,
        FixedTime Time,
        Mock<ICollegeMail> Mail,
        Mock<ICollegeNotifier> Notifier,
        ICollegePieceStore Pieces,
        CollegePictureStore Pictures);

    private static Rig Create(CollegeOptions? options = null, ICollegePieceStore? pieces = null)
    {
        options ??= new CollegeOptions();
        var dir = Path.Combine(Path.GetTempPath(), "college-svc-" + Guid.NewGuid().ToString("N"));
        var state = new CollegeState();
        var storage = new Mock<IStorage<CollegeState>>();
        storage.SetupGet(s => s.Value).Returns(state);
        var pictures = new CollegePictureStore(Path.Combine(dir, "Pictures"), options);
        pieces ??= new CollegePieceStore(Path.Combine(dir, "Pieces"));
        var mail = new Mock<ICollegeMail>();
        var notifier = new Mock<ICollegeNotifier>();
        var time = new FixedTime(Noon);

        var service = new CollegeService(
            storage.Object,
            Microsoft.Extensions.Options.Options.Create(options),
            notifier.Object,
            pieces,
            pictures,
            mail.Object,
            time,
            NullLogger<CollegeService>.Instance);

        return new Rig(service, state, time, mail, notifier, pieces, pictures);
    }

    private static CollegePiece Writing(string title, int chars)
        => new()
        {
            Title = title,
            Blocks = [new PieceBlock { Type = PieceBlockType.Text, Text = new string('w', chars) }]
        };

    private static int SubmitOne(Rig rig, string author = "Aroha", CollegeSubject subject = CollegeSubject.History)
    {
        rig.State.StudentFor(author).Marks += 3;
        rig.Service.SaveDraft(author, subject, Writing("The Fall", 250)).Result.Should().Be(CollegeWriterResult.Saved);
        rig.Service.Submit(author, subject).Result.Should().Be(CollegeWriterResult.Submitted);

        return rig.State.Entries.Single(e => e.IsAuthor(author) && (e.Subject == subject)).Id;
    }

    [Test]
    public void Saving_a_draft_keeps_one_piece_per_subject()
    {
        var rig = Create();

        rig.Service.SaveDraft("Aroha", CollegeSubject.Lore, Writing("One", 10)).Result.Should().Be(CollegeWriterResult.Saved);
        var id = rig.State.DraftOf("Aroha", CollegeSubject.Lore);
        rig.Service.SaveDraft("Aroha", CollegeSubject.Lore, Writing("Two", 10));

        rig.State.DraftOf("Aroha", CollegeSubject.Lore).Should().Be(id);
        rig.Service.LoadDraft("aroha", CollegeSubject.Lore)!.Title.Should().Be("Two");
    }

    [Test]
    public void Saving_refuses_closed_subjects_and_broken_pieces()
    {
        var rig = Create();

        rig.Service.SaveDraft("Aroha", CollegeSubject.Art, Writing("A", 10)).Message.Should().Be(CollegeText.NOT_OPEN);

        var outcome = rig.Service.SaveDraft("Aroha", CollegeSubject.Lore, Writing(new string('t', 41), 10));
        outcome.Result.Should().Be(CollegeWriterResult.Refused);
        outcome.Message.Should().Be(CollegeText.PieceProblem(PieceCheck.TitleTooLong));
    }

    [Test]
    public void Submit_pays_and_turns_the_draft_into_an_entry()
    {
        var rig = Create();
        var id = SubmitOne(rig);

        var entry = rig.State.Entries.Single();
        entry.Id.Should().Be(id);
        rig.State.StudentFor("Aroha").Marks.Should().Be(0);
        rig.State.DraftOf("Aroha", CollegeSubject.History).Should().BeNull();
        rig.Pieces.Load(entry.PieceId)!.Title.Should().Be("The Fall");
        rig.Notifier.Verify(n => n.RefreshEducatedMark("Aroha", 0), Times.Once);
    }

    [Test]
    public void A_failed_piece_write_leaves_marks_and_draft_alone()
    {
        var real = new CollegePieceStore(Path.Combine(Path.GetTempPath(), "college-fail-" + Guid.NewGuid().ToString("N")));
        var pieces = new Mock<ICollegePieceStore>();
        pieces.Setup(p => p.Load(It.IsAny<string>())).Returns<string>(real.Load);
        pieces.Setup(p => p.Save(It.IsAny<CollegePiece>())).Callback<CollegePiece>(real.Save);
        var rig = Create(pieces: pieces.Object);
        rig.State.StudentFor("Aroha").Marks = 3;
        rig.Service.SaveDraft("Aroha", CollegeSubject.History, Writing("The Fall", 250));
        var draft = rig.State.DraftOf("Aroha", CollegeSubject.History);
        pieces.Setup(p => p.Save(It.IsAny<CollegePiece>())).Throws(new IOException("disk full"));

        var outcome = rig.Service.Submit("Aroha", CollegeSubject.History);

        outcome.Message.Should().Be(CollegeText.SAVE_FAILED);
        rig.State.StudentFor("Aroha").Marks.Should().Be(3);
        rig.State.Entries.Should().BeEmpty();
        rig.State.DraftOf("Aroha", CollegeSubject.History).Should().Be(draft);
    }

    [Test]
    public void A_free_entry_is_used_before_marks()
    {
        var rig = Create();
        rig.State.StudentFor("Aroha").FreeEntries = 1;
        rig.Service.SaveDraft("Aroha", CollegeSubject.History, Writing("The Fall", 250));

        rig.Service.Submit("Aroha", CollegeSubject.History).Result.Should().Be(CollegeWriterResult.Submitted);

        rig.State.StudentFor("Aroha").FreeEntries.Should().Be(0);
        rig.State.StudentFor("Aroha").Marks.Should().Be(0);
    }

    [Test]
    public void A_verdict_mails_the_author_with_the_note()
    {
        var rig = Create();
        var id = SubmitOne(rig);
        rig.Service.SetDirector("Dir");
        rig.Service.CloseVoting(id).Should().BeTrue();

        rig.Service.PostVerdict("Dir", false, id, AwardTier.Village, "Lovely work.").Should().Be(VerdictResult.Posted);

        rig.Mail.Verify(
            m => m.Send(
                "Aroha",
                "College verdict",
                It.Is<string>(b => b.Contains("Village") && b.Contains("Lovely work.") && b.Contains("Claim it from the Registrar."))),
            Times.Once);
    }

    [Test]
    public void Removing_an_entry_mails_the_author_and_deletes_the_piece()
    {
        var rig = Create();
        var id = SubmitOne(rig);
        var pieceId = rig.State.Entries.Single().PieceId;

        rig.Service.RemoveEntry("Admin", true, id).Should().BeTrue();

        rig.State.Entries.Should().BeEmpty();
        rig.Pieces.Load(pieceId).Should().BeNull();
        rig.Mail.Verify(m => m.Send("Aroha", "Entry removed", It.IsAny<string>()), Times.Once);
    }

    [Test]
    public void Tick_closes_voting_and_sends_one_reminder_a_day()
    {
        var options = new CollegeOptions { VerdictReminderNames = ["AdminOne"] };
        var rig = Create(options);
        SubmitOne(rig);
        rig.Service.SetDirector("Dir");

        rig.Time.Later(TimeSpan.FromDays(14));
        rig.Service.Tick();
        rig.State.Entries.Single().Status.Should().Be(EntryStatus.AwaitingVerdict);

        rig.Time.Later(TimeSpan.FromDays(7));
        rig.Service.Tick();
        rig.Service.Tick();

        rig.Mail.Verify(m => m.Send("Dir", "Verdicts waiting", It.IsAny<string>()), Times.Once);
        rig.Mail.Verify(m => m.Send("AdminOne", "Verdicts waiting", It.IsAny<string>()), Times.Once);
        rig.Service.VerdictsOverdue().Should().BeTrue();
    }

    [Test]
    public void Hand_ins_need_a_writing_activity_and_replace_each_other()
    {
        var rig = Create();
        rig.Service.StartWalkIn("Sir", "m0", CollegeSubject.Lore, ClassFormat.Activity, "college_codex", true, false)
           .Result.Should().Be(StartResult.Started);
        rig.Service.SetPrompt("Sir", false, "Describe a festival").Should().BeTrue();

        rig.Service.HandIn("Aroha", "college_quills", Writing("", 30)).Result.Should().Be(CollegeWriterResult.Refused);
        rig.Service.HandIn("Sir", "college_codex", Writing("", 30)).Result.Should().Be(CollegeWriterResult.Refused);
        rig.Service.HandIn("Aroha", "college_codex", Writing("", 5)).Result.Should().Be(CollegeWriterResult.Refused);

        rig.Service.HandIn("Aroha", "college_codex", Writing("First", 30)).Result.Should().Be(CollegeWriterResult.HandedIn);
        var firstPiece = rig.State.HandIns.Single().PieceId;
        rig.Service.HandIn("Aroha", "college_codex", Writing("Second", 30)).Result.Should().Be(CollegeWriterResult.HandedIn);

        rig.State.HandIns.Should().ContainSingle().Which.Title.Should().Be("Second");
        rig.State.HandIns.Single().Prompt.Should().Be("Describe a festival");
        rig.Pieces.Load(firstPiece).Should().BeNull();
        rig.Service.HandIns("Sir", false, true).Should().ContainSingle();
    }

    [Test]
    public void Hand_ins_are_deleted_after_seven_days()
    {
        var rig = Create();
        rig.Service.StartWalkIn("Sir", "m0", CollegeSubject.Lore, ClassFormat.Activity, "college_codex", true, false);
        rig.Service.HandIn("Aroha", "college_codex", Writing("First", 30));
        var piece = rig.State.HandIns.Single().PieceId;

        rig.Time.Later(TimeSpan.FromDays(7));
        rig.Service.Tick();

        rig.State.HandIns.Should().BeEmpty();
        rig.Pieces.Load(piece).Should().BeNull();
    }

    [Test]
    public void Open_piece_gives_each_reader_the_right_context()
    {
        var rig = Create();
        var id = SubmitOne(rig);
        rig.Service.SetAward("Judge", CollegeSubject.Lore, AwardTier.Village);
        rig.Service.SetDirector("Dir");

        rig.Service.OpenPiece("Aroha", false, false, CollegePieceSource.Entry, id)!.Context.Should().Be(CollegePieceContext.Own);

        var judged = rig.Service.OpenPiece("Judge", false, false, CollegePieceSource.Entry, id)!;
        judged.Context.Should().Be(CollegePieceContext.Judge);
        judged.Author.Should().BeEmpty();

        rig.Service.OpenPiece("Stranger", false, false, CollegePieceSource.Entry, id).Should().BeNull();

        rig.Service.CloseVoting(id);
        var verdict = rig.Service.OpenPiece("Dir", false, false, CollegePieceSource.Entry, id)!;
        verdict.Context.Should().Be(CollegePieceContext.Verdict);
        verdict.Author.Should().Be("Aroha");

        rig.Service.PostVerdict("Dir", false, id, AwardTier.Village, "");
        rig.Service.Claim("Aroha", id, out _, out _).Should().Be(ClaimResult.Claimed);
        rig.Service.OpenPiece("Stranger", false, false, CollegePieceSource.Gallery, id)!.Context.Should().Be(CollegePieceContext.Gallery);

        rig.Service.SetHidden("Aroha", id, true);
        rig.Service.OpenPiece("Stranger", false, false, CollegePieceSource.Gallery, id).Should().BeNull();
        rig.Service.Gallery(CollegeSubject.History).Should().BeEmpty();
    }

    [Test]
    public void Judging_list_hides_own_entries_and_shows_waiting_ones_to_moderators()
    {
        var rig = Create();
        var id = SubmitOne(rig);
        SubmitOne(rig, "Brannoc", CollegeSubject.Lore);
        rig.Service.SetDirector("Aroha");

        rig.Service.JudgingList("Aroha", false, false).Should().ContainSingle().Which.Subject.Should().Be(CollegeSubject.Lore);

        rig.Service.CloseVoting(id);
        rig.Service.JudgingList("Brannoc", false, true).Should().Contain(r => r.Waiting && (r.Id == id));
        rig.Service.JudgingList("Aroha", false, false).Should().NotContain(r => r.Id == id);
    }
}
```

- [ ] **Step 6: Write the service members.** Create `Chaos/Services/College/CollegeService.Entries.cs`:

```csharp
using System.Globalization;
using Chaos.DarkAges.Definitions;
using Microsoft.Extensions.Logging;

namespace Chaos.Services.College;

public sealed record SaveOutcome(CollegeWriterResult Result, string Message);

public sealed record EntryView(int Id, CollegeSubject Subject, string Title, EntryStatus Status, int DaysLeft, AwardTier? Verdict, bool Hidden);

public sealed record JudgingRow(int Id, CollegeSubject Subject, string Title, int DaysLeft, AwardTier? MyTier, bool Waiting);

public sealed record GalleryRow(int Id, string Title, string Author, AwardTier Tier, DateTime ClaimedUtc);

public sealed record HandInRow(int Id, string Student, string Title, DateTime Utc);

public sealed record PieceView(
    CollegePiece Piece,
    CollegePieceContext Context,
    int Id,
    string Header,
    string Author,
    AwardTier? MyTier,
    string MyComment,
    IReadOnlyList<CollegeVote> Votes,
    bool CanModerate);

public sealed partial class CollegeService
{
    private static SaveOutcome Refused(string message) => new(CollegeWriterResult.Refused, message);

    // ---- drafts and entries ----

    public CollegePiece? LoadDraft(string name, CollegeSubject subject)
    {
        using var scope = Sync.EnterScope();

        return State.DraftOf(name, subject) is { } id ? Pieces.Load(id) : null;
    }

    public int FreeEntriesOf(string name)
    {
        using var scope = Sync.EnterScope();

        return State.Students.GetValueOrDefault(CollegeState.Key(name))?.FreeEntries ?? 0;
    }

    public SaveOutcome SaveDraft(string name, CollegeSubject subject, CollegePiece piece)
    {
        using var scope = Sync.EnterScope();

        if (!Options.IsOpen(subject))
            return Refused(CollegeText.NOT_OPEN);

        var clean = PieceRules.Normalize(piece);
        var check = PieceRules.Check(clean, Pictures.Has);

        if (check != PieceCheck.Ok)
            return Refused(CollegeText.PieceProblem(check));

        clean.Id = State.DraftOf(name, subject) ?? CollegePiece.NewId();
        clean.Subject = subject;
        clean.UpdatedUtc = Now;

        try
        {
            Pieces.Save(clean);
        } catch (IOException e)
        {
            Logger.LogError(e, "College draft save failed for {Name}", name);

            return Refused(CollegeText.SAVE_FAILED);
        }

        State.SetDraft(name, subject, clean.Id);
        SaveLocked();

        return new SaveOutcome(CollegeWriterResult.Saved, CollegeText.DRAFT_SAVED);
    }

    public SaveOutcome Submit(string name, CollegeSubject subject)
    {
        using var scope = Sync.EnterScope();

        switch (EntryBook.CanSubmit(State, Options, name, subject))
        {
            case SubmitResult.NotOpen:
                return Refused(CollegeText.NOT_OPEN);
            case SubmitResult.AlreadyEntered:
                return Refused(CollegeText.AlreadyEntered(subject));
            case SubmitResult.NoDraft:
                return Refused("Save your draft before you submit it.");
            case SubmitResult.NotEnoughMarks:
                return Refused(CollegeText.NOT_ENOUGH_MARKS);
        }

        var draftId = State.DraftOf(name, subject)!;

        if (Pieces.Load(draftId) is not { } draft)
            return Refused("Save your draft before you submit it.");

        var check = PieceRules.CheckEntry(draft, Pictures.Has);

        if (check != PieceCheck.Ok)
            return Refused(CollegeText.PieceProblem(check));

        draft.Id = CollegePiece.NewId();
        draft.UpdatedUtc = Now;

        //the entry's own file is written before anything is paid, so a failed write costs nothing
        try
        {
            Pieces.Save(draft);
        } catch (IOException e)
        {
            Logger.LogError(e, "College entry save failed for {Name}", name);

            return Refused(CollegeText.SAVE_FAILED);
        }

        EntryBook.Pay(State, Options, name);
        EntryBook.Add(State, Options, name, subject, draft.Id, draft.Title, Now);
        State.SetDraft(name, subject, null);
        SaveLocked();
        Pieces.Delete(draftId);
        Notifier.RefreshEducatedMark(name, State.StudentFor(name).Marks);

        return new SaveOutcome(CollegeWriterResult.Submitted, CollegeText.SUBMITTED);
    }

    public IReadOnlyList<EntryView> MyEntries(string name)
    {
        using var scope = Sync.EnterScope();

        return State.Entries
                    .Where(e => e.IsAuthor(name))
                    .OrderBy(e => e.Id)
                    .Select(e => new EntryView(e.Id, e.Subject, e.Title, e.Status, EntryBook.DaysLeft(e, Now), e.Verdict?.Tier, e.Hidden))
                    .ToList();
    }

    public bool Withdraw(string name, int id)
    {
        using var scope = Sync.EnterScope();

        return DeleteLocked(EntryBook.Withdraw(State, id, name));
    }

    public bool Dismiss(string name, int id)
    {
        using var scope = Sync.EnterScope();

        return DeleteLocked(EntryBook.Dismiss(State, id, name));
    }

    private bool DeleteLocked(CollegeEntry? removed)
    {
        if (removed is null)
            return false;

        SaveLocked();
        Pieces.Delete(removed.PieceId);

        return true;
    }

    public bool RemoveEntry(string by, bool isAdmin, int id)
    {
        using var scope = Sync.EnterScope();

        if (!CollegeRoles.CanModerate(State, by, isAdmin) || EntryBook.Remove(State, id) is not { } removed)
            return false;

        SaveLocked();
        Pieces.Delete(removed.PieceId);
        TryMail(removed.Author, "Entry removed", $"Your {removed.Subject} entry \"{removed.Title}\" was removed by the College.");

        return true;
    }

    public bool SetHidden(string name, int id, bool hidden)
    {
        using var scope = Sync.EnterScope();

        if (!EntryBook.SetHidden(State, id, name, hidden))
            return false;

        SaveLocked();

        return true;
    }

    // ---- judging ----

    public bool CanJudge(string name, bool isKnight, bool isAdmin)
    {
        using var scope = Sync.EnterScope();

        return CollegeRoles.CanJudge(State, name, isKnight, isAdmin, Options.KnightJudgingTeacherLimit);
    }

    public IReadOnlyList<JudgingRow> JudgingList(string name, bool isKnight, bool isAdmin)
    {
        using var scope = Sync.EnterScope();

        if (!CollegeRoles.CanJudge(State, name, isKnight, isAdmin, Options.KnightJudgingTeacherLimit))
            return [];

        var moderator = CollegeRoles.CanModerate(State, name, isAdmin);

        return State.Entries
                    .Where(e => !e.IsAuthor(name))
                    .Where(e => (e.Status == EntryStatus.Voting) || (moderator && (e.Status == EntryStatus.AwaitingVerdict)))
                    .OrderBy(e => e.Status)
                    .ThenBy(e => e.VotingEndsUtc)
                    .Select(
                        e => new JudgingRow(
                            e.Id,
                            e.Subject,
                            e.Title,
                            EntryBook.DaysLeft(e, Now),
                            e.Votes.TryGetValue(CollegeState.Key(name), out var vote) ? vote.Tier : null,
                            e.Status == EntryStatus.AwaitingVerdict))
                    .ToList();
    }

    public VoteResult Vote(string name, bool isKnight, bool isAdmin, int id, AwardTier tier, string comment)
    {
        using var scope = Sync.EnterScope();

        var canJudge = CollegeRoles.CanJudge(State, name, isKnight, isAdmin, Options.KnightJudgingTeacherLimit);
        var result = EntryBook.Vote(State, id, name, canJudge, tier, comment, Now);

        if (result == VoteResult.Saved)
            SaveLocked();

        return result;
    }

    public VerdictResult PostVerdict(string name, bool isAdmin, int id, AwardTier tier, string note)
    {
        using var scope = Sync.EnterScope();

        var result = EntryBook.SetVerdict(State, id, name, CollegeRoles.CanModerate(State, name, isAdmin), tier, note, Now);

        if (result != VerdictResult.Posted)
            return result;

        SaveLocked();

        var entry = EntryBook.Find(State, id)!;
        var verdict = entry.Verdict!;
        var award = verdict.Tier == AwardTier.None ? "No award" : $"the {verdict.Tier} Award";
        var body = $"Your {entry.Subject} entry \"{entry.Title}\" received {award}.";

        if (verdict.Note.Length > 0)
            body += $"\n\n{verdict.Note}";

        if (verdict.Tier != AwardTier.None)
            body += "\n\nClaim it from the Registrar.";

        TryMail(entry.Author, "College verdict", body);

        return result;
    }

    /// <summary>Ends voting on an entry now: the admin command, for tests and mistakes.</summary>
    public bool CloseVoting(int id)
    {
        using var scope = Sync.EnterScope();

        if (EntryBook.Find(State, id) is not { Status: EntryStatus.Voting } entry)
            return false;

        entry.VotingEndsUtc = Now;
        EntryBook.CloseVoting(State, Now);
        SaveLocked();

        return true;
    }

    public ClaimResult Claim(string name, int id, out AwardTier tier, out CollegeSubject subject)
    {
        using var scope = Sync.EnterScope();

        var entry = EntryBook.Find(State, id);
        tier = entry?.Verdict?.Tier ?? AwardTier.None;
        subject = entry?.Subject ?? default;

        var result = EntryBook.Claim(State, id, name, Now, out _);

        if (result == ClaimResult.Claimed)
            SaveLocked();

        return result;
    }

    public bool VerdictsOverdue()
    {
        using var scope = Sync.EnterScope();

        return State.Entries.Any(
            e => (e.Status == EntryStatus.AwaitingVerdict) && (Now - e.VotingEndsUtc >= TimeSpan.FromDays(Options.VerdictReminderDays)));
    }

    /// <summary>Every subject's award for a character, for the reward sync.</summary>
    public IReadOnlyDictionary<CollegeSubject, AwardTier> AwardsOf(string name)
    {
        using var scope = Sync.EnterScope();

        return Enum.GetValues<CollegeSubject>().ToDictionary(s => s, s => State.AwardOf(name, s));
    }

    // ---- gallery ----

    public IReadOnlyList<GalleryRow> Gallery(CollegeSubject subject)
    {
        using var scope = Sync.EnterScope();

        return State.Entries
                    .Where(e => (e.Subject == subject) && (e.Status == EntryStatus.Claimed) && !e.Hidden)
                    .OrderByDescending(e => e.Verdict!.Tier)
                    .ThenByDescending(e => e.ClaimedUtc)
                    .Select(e => new GalleryRow(e.Id, e.Title, e.Author, e.Verdict!.Tier, e.ClaimedUtc ?? e.SubmittedUtc))
                    .ToList();
    }

    // ---- hand-ins ----

    public bool SetPrompt(string teacher, bool isAdmin, string prompt)
    {
        using var scope = Sync.EnterScope();

        if (State.RunningClass is not { } running
            || (!running.Teacher.Equals(teacher, StringComparison.OrdinalIgnoreCase) && !CollegeRoles.CanModerate(State, teacher, isAdmin)))
            return false;

        running.Prompt = EntryBook.Cut(prompt.Replace('\n', ' '), CollegeProtocol.MAX_PROMPT_CHARS);
        SaveLocked();

        return true;
    }

    public SaveOutcome HandIn(string name, string roomInstanceId, CollegePiece piece)
    {
        using var scope = Sync.EnterScope();

        if (State.RunningClass is not { Format: ClassFormat.Activity } running
            || !CollegeOptions.IsWritingSubject(running.Subject)
            || !running.Room.Equals(roomInstanceId, StringComparison.OrdinalIgnoreCase))
            return Refused("You can hand in only during a writing class.");

        if (running.Teacher.Equals(name, StringComparison.OrdinalIgnoreCase) || running.Removed.Contains(CollegeState.Key(name)))
            return Refused("You can't hand in to this class.");

        var clean = PieceRules.Normalize(piece);
        var check = PieceRules.CheckHandIn(clean, Pictures.Has);

        if (check != PieceCheck.Ok)
            return Refused(check == PieceCheck.TooShort ? "Hand-ins need at least 20 characters." : CollegeText.PieceProblem(check));

        clean.Id = CollegePiece.NewId();
        clean.Subject = running.Subject;
        clean.UpdatedUtc = Now;

        try
        {
            Pieces.Save(clean);
        } catch (IOException e)
        {
            Logger.LogError(e, "College hand-in save failed for {Name}", name);

            return Refused(CollegeText.SAVE_FAILED);
        }

        var earlier = State.HandIns.FirstOrDefault(
            h => h.Student.Equals(name, StringComparison.OrdinalIgnoreCase)
                 && h.Teacher.Equals(running.Teacher, StringComparison.OrdinalIgnoreCase)
                 && (h.ClassStartedUtc == running.StartedUtc));

        if (earlier is not null)
        {
            State.HandIns.Remove(earlier);
            Pieces.Delete(earlier.PieceId);
        }

        State.HandIns.Add(
            new CollegeHandIn
            {
                Id = State.NextHandInId++,
                Teacher = running.Teacher,
                Subject = running.Subject,
                ClassStartedUtc = running.StartedUtc,
                Student = name,
                PieceId = clean.Id,
                Title = clean.Title.Length > 0 ? clean.Title : "Untitled",
                Prompt = running.Prompt,
                Utc = Now
            });

        SaveLocked();

        return new SaveOutcome(CollegeWriterResult.HandedIn, CollegeText.HANDED_IN);
    }

    /// <summary>The running class's hand-ins (for its Teacher or a moderator), or a Teacher's hand-ins from recent classes.</summary>
    public IReadOnlyList<HandInRow> HandIns(string name, bool isAdmin, bool currentClassOnly)
    {
        using var scope = Sync.EnterScope();

        IEnumerable<CollegeHandIn> rows;

        if (currentClassOnly)
        {
            if (State.RunningClass is not { } running
                || (!running.Teacher.Equals(name, StringComparison.OrdinalIgnoreCase) && !CollegeRoles.CanModerate(State, name, isAdmin)))
                return [];

            rows = State.HandIns.Where(
                h => h.Teacher.Equals(running.Teacher, StringComparison.OrdinalIgnoreCase) && (h.ClassStartedUtc == running.StartedUtc));
        } else
            rows = State.HandIns.Where(h => h.Teacher.Equals(name, StringComparison.OrdinalIgnoreCase));

        return rows.OrderByDescending(h => h.Utc).Select(h => new HandInRow(h.Id, h.Student, h.Title, h.Utc)).ToList();
    }

    public bool HasHandIns(string name)
    {
        using var scope = Sync.EnterScope();

        return State.HandIns.Any(h => h.Teacher.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    // ---- reading ----

    public PieceView? OpenPiece(string name, bool isKnight, bool isAdmin, CollegePieceSource source, int id)
    {
        using var scope = Sync.EnterScope();

        var moderator = CollegeRoles.CanModerate(State, name, isAdmin);

        if (source == CollegePieceSource.HandIn)
        {
            if (State.HandIns.FirstOrDefault(h => h.Id == id) is not { } handIn
                || (!handIn.Teacher.Equals(name, StringComparison.OrdinalIgnoreCase)
                    && !handIn.Student.Equals(name, StringComparison.OrdinalIgnoreCase)
                    && !moderator))
                return null;

            return ViewOf(handIn.PieceId, CollegePieceContext.HandIn, id, $"{handIn.Student}, {handIn.Utc.ToString("ddd HH:mm", CultureInfo.InvariantCulture)} UTC", handIn.Student, null, "", [], moderator);
        }

        if (EntryBook.Find(State, id) is not { } entry)
            return null;

        var award = entry.Verdict is { } v ? $"{v.Tier} Award in {entry.Subject}" : "";

        if (source == CollegePieceSource.Gallery)
            return (entry.Status == EntryStatus.Claimed) && (!entry.Hidden || entry.IsAuthor(name) || moderator)
                ? ViewOf(entry.PieceId, CollegePieceContext.Gallery, id, award, entry.Author, null, "", [], moderator)
                : null;

        if (entry.IsAuthor(name))
            return ViewOf(entry.PieceId, CollegePieceContext.Own, id, StatusText(entry), entry.Author, null, "", [], false);

        var canJudge = CollegeRoles.CanJudge(State, name, isKnight, isAdmin, Options.KnightJudgingTeacherLimit);

        if ((entry.Status == EntryStatus.Voting) && canJudge)
        {
            var mine = entry.Votes.GetValueOrDefault(CollegeState.Key(name));

            return ViewOf(
                entry.PieceId,
                CollegePieceContext.Judge,
                id,
                $"{entry.Subject}, {EntryBook.DaysLeft(entry, Now)} days left",
                "",
                mine?.Tier,
                mine?.Comment ?? "",
                [],
                moderator);
        }

        if (moderator && entry.Status is EntryStatus.AwaitingVerdict or EntryStatus.Decided)
            return ViewOf(
                entry.PieceId,
                CollegePieceContext.Verdict,
                id,
                StatusText(entry),
                entry.Author,
                entry.Verdict?.Tier,
                entry.Verdict?.Note ?? "",
                entry.Votes.Values.OrderBy(v => v.Utc).ToList(),
                true);

        return (entry.Status == EntryStatus.Claimed) && !entry.Hidden
            ? ViewOf(entry.PieceId, CollegePieceContext.Gallery, id, award, entry.Author, null, "", [], moderator)
            : null;
    }

    /// <summary>A hand-in for "Show to class": only the running class's Teacher or a moderator.</summary>
    public PieceView? HandInForShow(string name, bool isAdmin, int id)
    {
        using var scope = Sync.EnterScope();

        if (State.RunningClass is not { } running
            || (!running.Teacher.Equals(name, StringComparison.OrdinalIgnoreCase) && !CollegeRoles.CanModerate(State, name, isAdmin))
            || State.HandIns.FirstOrDefault(h => (h.Id == id) && (h.ClassStartedUtc == running.StartedUtc)) is not { } handIn)
            return null;

        return ViewOf(handIn.PieceId, CollegePieceContext.Shown, id, $"Shown by {running.Teacher}", handIn.Student, null, "", [], false);
    }

    private PieceView? ViewOf(
        string pieceId,
        CollegePieceContext context,
        int id,
        string header,
        string author,
        AwardTier? myTier,
        string myComment,
        IReadOnlyList<CollegeVote> votes,
        bool canModerate)
        => Pieces.Load(pieceId) is { } piece
            ? new PieceView(piece, context, id, header, author, myTier, myComment, votes, canModerate)
            : null;

    private string StatusText(CollegeEntry entry)
        => entry.Status switch
        {
            EntryStatus.Voting          => $"by {entry.Author}, {EntryBook.DaysLeft(entry, Now)} days of voting left",
            EntryStatus.AwaitingVerdict => $"by {entry.Author}, awaiting verdict",
            EntryStatus.Decided         => $"by {entry.Author}, decided: {entry.Verdict?.Tier.ToString() ?? "No award"}",
            _                           => $"by {entry.Author}, {entry.Verdict?.Tier} Award"
        };

    // ---- tick helpers (called from Tick, inside the lock) ----

    private void SendRemindersLocked(DateTime now)
    {
        var due = EntryBook.RemindersDue(State, Options, now);

        if (due.Count == 0)
            return;

        foreach (var entry in due)
            entry.LastReminderUtc = now;

        var waiting = State.Entries.Count(e => e.Status == EntryStatus.AwaitingVerdict);
        var body = $"{waiting} College entries have waited over {Options.VerdictReminderDays} days for a verdict.";
        var recipients = Options.VerdictReminderNames.Append(State.Director).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var name in recipients)
            TryMail(name, "Verdicts waiting", body);
    }

    private void CleanupLocked(DateTime now)
    {
        foreach (var handIn in State.HandIns.Where(h => now - h.Utc >= TimeSpan.FromDays(Options.HandInDays)).ToList())
        {
            State.HandIns.Remove(handIn);
            Pieces.Delete(handIn.PieceId);
        }

        try
        {
            Pictures.Sweep(Pieces.AllPictureHashes(), now);
        } catch (IOException e)
        {
            Logger.LogError(e, "College picture sweep failed");
        }
    }

    private void TryMail(string to, string subject, string body)
    {
        try
        {
            Mail.Send(to, subject, body);
        } catch (Exception e)
        {
            Logger.LogError(e, "College mail to {Name} failed", to);
        }
    }
}
```

`EntryView` and `HandInRow` use the `Status` and `Utc` names the scripts in Task 9 read.

- [ ] **Step 7: Expire uploads on the tick.** Change `CollegeTickService` to take `PictureUploads uploads` and `TimeProvider time` too, and after `service.Tick();` call `uploads.Expire(time.GetUtcNow().UtcDateTime);`.

- [ ] **Step 8: Run the tests.** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/CollegeEntryServiceTests/*"`. Expected: 12 passed. Then `--treenode-filter "/*/Chaos.Tests.College/*/*"`: every College test passes.

```json:metadata
{"files": ["Chaos/Services/College/ICollegeMail.cs", "Chaos/Services/College/CollegeMail.cs", "Chaos/Services/College/CollegeService.Entries.cs", "Chaos/Services/College/CollegeService.cs", "Chaos/Services/College/Timetable.cs", "Chaos/Services/College/CollegeTickService.cs", "Tests/Chaos.Tests/College/CollegeServiceTests.cs", "Tests/Chaos.Tests/College/TimetableTests.cs", "Tests/Chaos.Tests/College/CollegeEntryServiceTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/Chaos.Tests.College/*/*\"", "acceptanceCriteria": ["SaveDraft one piece per subject, refusals", "Submit writes piece first; failure leaves marks and draft", "HandIn rules and replacement", "verdict and removal mail", "Tick closes voting, daily reminders, hourly cleanup", "OpenPiece contexts; Judge carries no author", "Activity allowed for writing subjects only", "part 1 tests still pass"], "modelTier": "standard"}
```

---

### Task 7: Award rewards: plan, apply, login sync (server worktree)

**Goal:** Turn a character's awards into the matching legend marks, titles and emblems, apply them to an online player at claim and at login, and refresh them when `/college award` changes an online player.

**Files:**
- Create: `Chaos/Services/College/CollegeRewardPlan.cs`, `Chaos/Services/College/CollegeRewards.cs`
- Modify: `Chaos/Scripting/AislingScripts/DefaultAislingScript.cs` (constructor and the College login lines near `CollegeNotifier.ApplyEducatedMark`)
- Modify: `Chaos/Messaging/Admin/CollegeCommand.cs` (`Award` syncs an online target)
- Test: `Tests/Chaos.Tests/College/CollegeRewardPlanTests.cs` (new)

**Acceptance Criteria:**
- [ ] `CollegeRewardPlan.Title` gives "Village Historian", "Aisling Wordsmith" (Literature), "Clave Artist" and so on; `MarkKey` gives `collegeAwardHistory`; `MarkText` gives "Village Award in History"; `EmblemKey` gives `college_history_village`.
- [ ] `PlanTitles` removes every other College title for that subject, adds the wanted one, and makes it active when a removed one was active; with tier None it removes them all.
- [ ] `PlanEmblems` takes every other owned College emblem for that subject and gives the wanted one if not owned.
- [ ] At login, `DefaultAislingScript` calls `CollegeRewards.Sync` after the Educated mark, and an admin gets the orange bar "College entries await a verdict." when `VerdictsOverdue()`.
- [ ] `/college award <name> <subject> <tier>` calls `CollegeRewards.Sync` when the target is online.

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/CollegeRewardPlanTests/*"` -> all pass; the server builds.

**Steps:**

- [ ] **Step 1: Write the failing tests.** Create `Tests/Chaos.Tests/College/CollegeRewardPlanTests.cs`:

```csharp
using Chaos.Services.College;
using FluentAssertions;

namespace Chaos.Tests.College;

public sealed class CollegeRewardPlanTests
{
    [Test]
    public void Names_and_keys_follow_the_pattern()
    {
        CollegeRewardPlan.Title(CollegeSubject.History, AwardTier.Village).Should().Be("Village Historian");
        CollegeRewardPlan.Title(CollegeSubject.Literature, AwardTier.Aisling).Should().Be("Aisling Wordsmith");
        CollegeRewardPlan.Title(CollegeSubject.Art, AwardTier.Clave).Should().Be("Clave Artist");
        CollegeRewardPlan.Title(CollegeSubject.Music, AwardTier.Kingdom).Should().Be("Kingdom Musician");
        CollegeRewardPlan.Title(CollegeSubject.Lore, AwardTier.Clave).Should().Be("Clave Lorekeeper");
        CollegeRewardPlan.Title(CollegeSubject.Philosophy, AwardTier.Clave).Should().Be("Clave Philosopher");
        CollegeRewardPlan.MarkKey(CollegeSubject.History).Should().Be("collegeAwardHistory");
        CollegeRewardPlan.MarkText(CollegeSubject.History, AwardTier.Village).Should().Be("Village Award in History");
        CollegeRewardPlan.EmblemKey(CollegeSubject.History, AwardTier.Village).Should().Be("college_history_village");
    }

    [Test]
    public void A_higher_title_replaces_the_lower_and_keeps_it_active()
    {
        var plan = CollegeRewardPlan.PlanTitles(["Clave Historian", "Arena Host"], "Clave Historian", CollegeSubject.History, AwardTier.Village);

        plan.Remove.Should().Equal("Clave Historian");
        plan.Add.Should().Be("Village Historian");
        plan.Active.Should().Be("Village Historian");
    }

    [Test]
    public void Another_active_title_stays_active()
    {
        var plan = CollegeRewardPlan.PlanTitles(["Clave Historian", "Arena Host"], "Arena Host", CollegeSubject.History, AwardTier.Village);

        plan.Active.Should().Be("Arena Host");
    }

    [Test]
    public void No_award_removes_every_title_for_the_subject_only()
    {
        var plan = CollegeRewardPlan.PlanTitles(["Village Historian", "Clave Lorekeeper"], null, CollegeSubject.History, AwardTier.None);

        plan.Remove.Should().Equal("Village Historian");
        plan.Add.Should().BeNull();
    }

    [Test]
    public void A_held_title_is_not_added_again()
        => CollegeRewardPlan.PlanTitles(["Village Historian"], null, CollegeSubject.History, AwardTier.Village).Add.Should().BeNull();

    [Test]
    public void Emblems_swap_to_the_new_tier()
    {
        var owned = new HashSet<string> { "college_history_clave", "college_lore_clave" };

        var plan = CollegeRewardPlan.PlanEmblems(owned.Contains, CollegeSubject.History, AwardTier.Kingdom);

        plan.Take.Should().Equal("college_history_clave");
        plan.Give.Should().Be("college_history_kingdom");
    }
}
```

- [ ] **Step 2: Write the plan.** Create `Chaos/Services/College/CollegeRewardPlan.cs`:

```csharp
namespace Chaos.Services.College;

public sealed record TitlePlan(IReadOnlyList<string> Remove, string? Add, string? Active);

public sealed record EmblemPlan(IReadOnlyList<string> Take, string? Give);

/// <summary>What a character's award in one subject should look like: legend mark, title and emblem. No game-world access.</summary>
public static class CollegeRewardPlan
{
    private static readonly AwardTier[] Tiers = [AwardTier.Clave, AwardTier.Village, AwardTier.Kingdom, AwardTier.Aisling];

    public static string Noun(CollegeSubject subject)
        => subject switch
        {
            CollegeSubject.Art        => "Artist",
            CollegeSubject.Music      => "Musician",
            CollegeSubject.Literature => "Wordsmith",
            CollegeSubject.History    => "Historian",
            CollegeSubject.Lore       => "Lorekeeper",
            _                         => "Philosopher"
        };

    public static string Title(CollegeSubject subject, AwardTier tier) => $"{tier} {Noun(subject)}";

    public static string MarkKey(CollegeSubject subject) => $"collegeAward{subject}";

    public static string MarkText(CollegeSubject subject, AwardTier tier) => $"{tier} Award in {subject}";

    public static string EmblemKey(CollegeSubject subject, AwardTier tier)
        => $"college_{subject.ToString().ToLowerInvariant()}_{tier.ToString().ToLowerInvariant()}";

    public static TitlePlan PlanTitles(IReadOnlyList<string> titles, string? active, CollegeSubject subject, AwardTier tier)
    {
        var wanted = tier == AwardTier.None ? null : Title(subject, tier);
        var college = Tiers.Select(t => Title(subject, t)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var remove = titles.Where(t => college.Contains(t) && !t.Equals(wanted, StringComparison.OrdinalIgnoreCase)).ToList();
        var add = (wanted is not null) && !titles.Contains(wanted, StringComparer.OrdinalIgnoreCase) ? wanted : null;
        var activeRemoved = active is not null && remove.Contains(active, StringComparer.OrdinalIgnoreCase);

        return new TitlePlan(remove, add, activeRemoved ? wanted : active);
    }

    public static EmblemPlan PlanEmblems(Func<string, bool> owns, CollegeSubject subject, AwardTier tier)
    {
        var wanted = tier == AwardTier.None ? null : EmblemKey(subject, tier);
        var take = Tiers.Select(t => EmblemKey(subject, t)).Where(k => (k != wanted) && owns(k)).ToList();

        return new EmblemPlan(take, (wanted is not null) && !owns(wanted) ? wanted : null);
    }
}
```

- [ ] **Step 3: Write the applier.** Create `Chaos/Services/College/CollegeRewards.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Models.Legend;
using Chaos.Models.World;
using Chaos.Services.Emblems;
using Chaos.Time;
using Microsoft.Extensions.Logging;

namespace Chaos.Services.College;

/// <summary>Makes an online player's College legend marks, titles and emblems match their awards.</summary>
public sealed class CollegeRewards(CollegeService college, EmblemService emblems, ILogger<CollegeRewards> logger)
{
    public void Sync(Aisling aisling)
    {
        var awards = college.AwardsOf(aisling.Name);
        var titlesChanged = false;

        foreach (var (subject, tier) in awards)
        {
            SyncMark(aisling, subject, tier);
            titlesChanged |= SyncTitles(aisling, subject, tier);
            SyncEmblems(aisling, subject, tier);
        }

        if (titlesChanged)
            aisling.Client.SendSelfProfile();
    }

    private static void SyncMark(Aisling aisling, CollegeSubject subject, AwardTier tier)
    {
        var key = CollegeRewardPlan.MarkKey(subject);

        if (tier == AwardTier.None)
        {
            aisling.Legend.Remove(key, out _);

            return;
        }

        var text = CollegeRewardPlan.MarkText(subject, tier);

        if (aisling.Legend.TryGetValue(key, out var mark))
            mark.Text = text;
        else
            aisling.Legend.AddUnique(new LegendMark(text, key, MarkIcon.Wizard, MarkColor.White, 1, GameTime.Now));
    }

    private static bool SyncTitles(Aisling aisling, CollegeSubject subject, AwardTier tier)
    {
        var plan = CollegeRewardPlan.PlanTitles(aisling.Titles.ToList(), aisling.ActiveTitle, subject, tier);

        if ((plan.Remove.Count == 0) && plan.Add is null)
            return false;

        foreach (var title in plan.Remove)
            aisling.Titles.Remove(title);

        if (plan.Add is not null)
            aisling.Titles.Add(plan.Add);

        //removing a title shifts the active index, so the active one is set again by name
        if (plan.Active is not null)
            aisling.SetActiveTitle(plan.Active);

        return true;
    }

    private void SyncEmblems(Aisling aisling, CollegeSubject subject, AwardTier tier)
    {
        var plan = CollegeRewardPlan.PlanEmblems(key => aisling.Emblems.TryGetOwned(key, out _), subject, tier);

        foreach (var key in plan.Take)
            emblems.Take(aisling, key, out _);

        if (plan.Give is not null && !emblems.Give(aisling, plan.Give, out var reply))
            logger.LogWarning("College emblem {Key} not given to {Name}: {Reply}", plan.Give, aisling.Name, reply);
    }
}
```

If `MarkColor.White` doesn't exist, use the colour part 1 uses in `CollegeNotifier.ApplyEducatedMark`. If `aisling.Titles.Remove` returns `bool` and warns, discard it with `_ =`.

- [ ] **Step 4: Login sync.** In `DefaultAislingScript`, add a constructor parameter `CollegeRewards collegeRewards` stored in a field `CollegeRewards`, following how `CollegeService college` is added at its constructor (around line 129). Right after `CollegeNotifier.ApplyEducatedMark(Subject, College.MarksOf(Subject.Name));` add:

```csharp
        CollegeRewards.Sync(Subject);

        if (Subject.IsAdmin && College.VerdictsOverdue())
            Subject.SendOrangeBarMessage("College entries await a verdict.");
```

- [ ] **Step 5: Admin award sync.** In `CollegeCommand`, inject `CollegeRewards rewards` and `IClientRegistry<IChaosWorldClient> clients` in the constructor. In the `Award` method, after the call to `College.SetAward(...)` succeeds, find the target online and sync:

```csharp
        var online = clients.Select(c => c.Aisling).FirstOrDefault(a => a?.Name.Equals(name, StringComparison.OrdinalIgnoreCase) == true);

        if (online is not null)
            rewards.Sync(online);
```

Use the local variable that holds the target name in `Award`. Add `using Chaos.Networking.Abstractions;` if needed.

- [ ] **Step 6: Run the tests and build.** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/CollegeRewardPlanTests/*"`. Expected: 6 passed. `dotnet build Chaos/Chaos.csproj` succeeds (DI registration comes in Task 8; the build doesn't need it).

```json:metadata
{"files": ["Chaos/Services/College/CollegeRewardPlan.cs", "Chaos/Services/College/CollegeRewards.cs", "Chaos/Scripting/AislingScripts/DefaultAislingScript.cs", "Chaos/Messaging/Admin/CollegeCommand.cs", "Tests/Chaos.Tests/College/CollegeRewardPlanTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/CollegeRewardPlanTests/*\"", "acceptanceCriteria": ["title, mark and emblem names", "PlanTitles replace/active rules", "PlanEmblems swap", "login sync and admin overdue bar", "/college award syncs online target"], "modelTier": "standard"}
```

---

### Task 8: `CollegePanel`, the packet handler and DI (server worktree)

**Goal:** Route every `CollegeAction` to the service, build and send every `CollegeDisplay`, serve pictures in parts, and register the new services.

**Files:**
- Create: `Chaos/Services/College/CollegePanel.cs`
- Modify: `Chaos/Services/Servers/WorldServer.cs` (field, constructor parameter, `OnCollegeAction`, handler registration next to `TowerLeaderboardRequest`)
- Modify: `Chaos/Extensions/ServiceCollectionExtensions.cs` (`AddCollege`)
- Test: `Tests/Chaos.Tests/College/CollegePanelMappingTests.cs` (new)

**Acceptance Criteria:**
- [ ] `CollegePanel.ToPiece` and `ToInfo` map pieces both ways without loss (subject, title, every block).
- [ ] `JudgingRows`, `GalleryRows` and `HandInRows` map service rows to info rows (tier `None` vote maps to tier byte 0; no vote maps to `NO_TIER`; days left clamp to 255).
- [ ] `Handle` answers each action as the spec's message table says, and drops non-picture actions faster than one per 250 ms per character.
- [ ] `PictureFetch` sends the picture in parts of at most `PART_SIZE`, at most 30 fetches a minute per character.
- [ ] The server starts with `AddCollege` registering `ICollegePieceStore`, `ICollegePictureStore`, `PictureUploads`, `ICollegeMail`, `CollegeRewards`, `CollegePanel` and the directory-bound options.

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/CollegePanelMappingTests/*"` -> all pass; `dotnet build Chaos.slnx` succeeds; starting the server locally logs no DI error (stop it afterwards).

**Steps:**

- [ ] **Step 1: Write the failing mapping tests.** Create `Tests/Chaos.Tests/College/CollegePanelMappingTests.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using Chaos.Services.College;
using FluentAssertions;

namespace Chaos.Tests.College;

public sealed class CollegePanelMappingTests
{
    private static readonly DateTime Noon = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void Pieces_map_both_ways()
    {
        var info = new CollegePieceInfo
        {
            Subject = CollegeSubjectCode.Lore,
            Title = "Of Sgrios",
            Blocks =
            [
                new CollegeBlockInfo { Kind = CollegeBlockKind.Heading, Text = "I" },
                new CollegeBlockInfo { Kind = CollegeBlockKind.Text, Text = "a\nb" },
                new CollegeBlockInfo { Kind = CollegeBlockKind.Picture, Hash = new string('a', 64) }
            ]
        };

        var piece = CollegePanel.ToPiece(info, CollegeSubject.Lore);

        piece.Subject.Should().Be(CollegeSubject.Lore);
        piece.Blocks.Select(b => b.Type).Should().Equal(PieceBlockType.Heading, PieceBlockType.Text, PieceBlockType.Picture);
        CollegePanel.ToInfo(piece).Should().BeEquivalentTo(info);
    }

    [Test]
    public void Subject_and_tier_codes_match_the_server_enums()
    {
        Enum.GetNames<CollegeSubjectCode>().Should().Equal(Enum.GetNames<CollegeSubject>());
        Enum.GetNames<CollegeTierCode>().Should().Equal(Enum.GetNames<AwardTier>());
    }

    [Test]
    public void Rows_map_votes_and_days()
    {
        var rows = CollegePanel.JudgingRows(
        [
            new JudgingRow(1, CollegeSubject.History, "A", 400, null, false),
            new JudgingRow(2, CollegeSubject.Lore, "B", 3, AwardTier.None, true)
        ]);

        rows[0].DaysLeft.Should().Be(255);
        rows[0].MyTier.Should().Be(CollegeProtocol.NO_TIER);
        rows[1].MyTier.Should().Be(0);
        rows[1].Waiting.Should().BeTrue();

        var gallery = CollegePanel.GalleryRows([new GalleryRow(3, "T", "Elowen", AwardTier.Kingdom, Noon)]);
        gallery[0].Tier.Should().Be(CollegeTierCode.Kingdom);
        gallery[0].ClaimedUnix.Should().Be((uint)new DateTimeOffset(Noon).ToUnixTimeSeconds());

        CollegePanel.HandInRows([new HandInRow(8, "Tamsin", "Festival", Noon)])[0].Student.Should().Be("Tamsin");
    }
}
```

- [ ] **Step 2: Write the panel.** Create `Chaos/Services/College/CollegePanel.cs`. Use `TownBallotPanel` as the model for structure, and `TownRequestThrottle` (namespace `Chaos.Services.Towns`) for the 250 ms limit:

```csharp
#region
using Chaos.DarkAges.Definitions;
using Chaos.Models.World;
using Chaos.Networking.Abstractions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Chaos.Services.Towns;
using Microsoft.Extensions.Logging;
#endregion

namespace Chaos.Services.College;

/// <summary>
///     The College windows: builds every <c>CollegeDisplay</c> message from <see cref="CollegeService" /> and routes every
///     <c>CollegeAction</c> back to it. The service checks every rule; this class maps, throttles and sends.
/// </summary>
public sealed class CollegePanel(
    CollegeService college,
    PictureUploads uploads,
    ICollegePictureStore pictures,
    TimeProvider time,
    ILogger<CollegePanel> logger)
{
    private const int FETCHES_PER_MINUTE = 30;

    private readonly Dictionary<string, List<DateTime>> Fetches = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock FetchSync = new();
    private readonly TownRequestThrottle Throttle = new(time, TimeSpan.FromMilliseconds(250));

    private DateTime Now => time.GetUtcNow().UtcDateTime;

    // ---- opening windows (called by scripts) ----

    public void OpenWriter(Aisling viewer, CollegeSubject subject)
    {
        var draft = college.LoadDraft(viewer.Name, subject);

        viewer.Client.SendCollegeDisplay(
            new CollegeDisplayArgs
            {
                Type = CollegeDisplayType.OpenWriter,
                Mode = CollegeWriterMode.Draft,
                Subject = (CollegeSubjectCode)subject,
                EntryCost = (byte)college.Options.EntryCost,
                FreeEntries = (byte)Math.Min(college.FreeEntriesOf(viewer.Name), byte.MaxValue),
                Piece = draft is null ? null : ToInfo(draft)
            });
    }

    public void OpenHandInWriter(Aisling viewer)
    {
        if (college.Running() is not { Format: ClassFormat.Activity } running)
            return;

        viewer.Client.SendCollegeDisplay(
            new CollegeDisplayArgs
            {
                Type = CollegeDisplayType.OpenWriter,
                Mode = CollegeWriterMode.HandIn,
                Subject = (CollegeSubjectCode)running.Subject,
                Prompt = running.Prompt
            });
    }

    public void OpenJudging(Aisling viewer)
        => viewer.Client.SendCollegeDisplay(
            new CollegeDisplayArgs
            {
                Type = CollegeDisplayType.JudgingList,
                CanModerate = college.CanModerate(viewer.Name, viewer.IsAdmin),
                JudgingRows = JudgingRows(college.JudgingList(viewer.Name, viewer.IsKnight, viewer.IsAdmin))
            });

    public void OpenGallery(Aisling viewer, CollegeSubject subject)
        => viewer.Client.SendCollegeDisplay(
            new CollegeDisplayArgs
            {
                Type = CollegeDisplayType.GalleryList,
                Subject = (CollegeSubjectCode)subject,
                GalleryRows = GalleryRows(college.Gallery(subject))
            });

    public void OpenHandIns(Aisling viewer, bool currentClassOnly)
        => viewer.Client.SendCollegeDisplay(
            new CollegeDisplayArgs
            {
                Type = CollegeDisplayType.HandInList,
                HandInRows = HandInRows(college.HandIns(viewer.Name, viewer.IsAdmin, currentClassOnly))
            });

    public void OpenPiece(Aisling viewer, CollegePieceSource source, int id)
    {
        if (college.OpenPiece(viewer.Name, viewer.IsKnight, viewer.IsAdmin, source, id) is { } view)
            viewer.Client.SendCollegeDisplay(PieceArgs(view));
        else
            viewer.SendOrangeBarMessage("That piece is no longer available.");
    }

    // ---- actions from the client ----

    public void Handle(Aisling viewer, CollegeActionArgs args)
    {
        switch (args.Type)
        {
            case CollegeActionType.PictureCheck:
                var begin = uploads.Begin(viewer.Name, args.Hash, (int)Math.Min(args.Length, int.MaxValue), Now);
                SendPictureReply(viewer, args.Hash, begin);

                return;
            case CollegeActionType.PicturePart:
                if (uploads.AddPart(viewer.Name, args.Hash, args.PartIndex, args.PartCount, args.Data, Now) is { } reply)
                    SendPictureReply(viewer, args.Hash, reply);

                return;
            case CollegeActionType.PictureFetch:
                SendPicture(viewer, args.Hash);

                return;
        }

        //every other action can write a file, so a burst is dropped
        if (!Throttle.TryStart(viewer.Name))
            return;

        switch (args.Type)
        {
            case CollegeActionType.SaveDraft when args.Piece is not null:
                SendResult(viewer, college.SaveDraft(viewer.Name, (CollegeSubject)args.Subject, ToPiece(args.Piece, (CollegeSubject)args.Subject)));

                break;
            case CollegeActionType.Submit when args.Piece is not null:
                //the window sends its latest text with Submit, so it is saved first and nothing typed since the last Save is lost
                var subject = (CollegeSubject)args.Subject;
                var saved = college.SaveDraft(viewer.Name, subject, ToPiece(args.Piece, subject));
                var submitted = saved.Result == CollegeWriterResult.Saved ? college.Submit(viewer.Name, subject) : saved;
                SendResult(viewer, submitted);
                viewer.SendOrangeBarMessage(submitted.Message);

                break;
            case CollegeActionType.HandIn when args.Piece is not null:
                var handedIn = college.HandIn(viewer.Name, viewer.MapInstance.InstanceId, ToPiece(args.Piece, default));
                SendResult(viewer, handedIn);
                viewer.SendOrangeBarMessage(handedIn.Message);

                break;
            case CollegeActionType.OpenPiece:
                OpenPiece(viewer, args.Source, args.Id);

                break;
            case CollegeActionType.Vote:
                var vote = college.Vote(viewer.Name, viewer.IsKnight, viewer.IsAdmin, args.Id, (AwardTier)args.Tier, args.Text);
                viewer.SendOrangeBarMessage(VoteText(vote));
                OpenJudging(viewer);

                break;
            case CollegeActionType.Verdict:
                var verdict = college.PostVerdict(viewer.Name, viewer.IsAdmin, args.Id, (AwardTier)args.Tier, args.Text);
                viewer.SendOrangeBarMessage(VerdictText(verdict));
                OpenJudging(viewer);

                break;
            case CollegeActionType.RemoveEntry:
                viewer.SendOrangeBarMessage(college.RemoveEntry(viewer.Name, viewer.IsAdmin, args.Id) ? "Entry removed." : "You can't remove that.");
                OpenJudging(viewer);

                break;
            case CollegeActionType.GalleryPage:
                OpenGallery(viewer, (CollegeSubject)args.Subject);

                break;
            case CollegeActionType.ShowToClass:
                ShowToClass(viewer, args.Id);

                break;
        }
    }

    private void ShowToClass(Aisling teacher, int handInId)
    {
        if (college.HandInForShow(teacher.Name, teacher.IsAdmin, handInId) is not { } view)
        {
            teacher.SendOrangeBarMessage("You can only show your own class's work.");

            return;
        }

        var args = PieceArgs(view);

        //the packet handler runs under the sender's map, so its aislings can be read here
        foreach (var aisling in teacher.MapInstance.GetEntities<Aisling>())
            aisling.Client.SendCollegeDisplay(args);
    }

    private void SendPicture(Aisling viewer, string hash)
    {
        using (FetchSync.EnterScope())
        {
            if (!Fetches.TryGetValue(viewer.Name, out var times))
                Fetches[viewer.Name] = times = [];

            times.RemoveAll(t => Now - t >= TimeSpan.FromMinutes(1));

            if (times.Count >= FETCHES_PER_MINUTE)
                return;

            times.Add(Now);
        }

        if (pictures.Read(hash) is not { } bytes)
            return;

        var parts = bytes.Chunk(CollegeProtocol.PART_SIZE).ToList();

        for (var i = 0; i < parts.Count; i++)
            viewer.Client.SendCollegeDisplay(
                new CollegeDisplayArgs
                {
                    Type = CollegeDisplayType.PicturePart,
                    Hash = hash,
                    PartIndex = (byte)i,
                    PartCount = (byte)parts.Count,
                    Data = parts[i]
                });
    }

    private static void SendPictureReply(Aisling viewer, string hash, UploadReply reply)
        => viewer.Client.SendCollegeDisplay(
            new CollegeDisplayArgs
            {
                Type = CollegeDisplayType.PictureReply,
                Hash = hash,
                Reply = reply.Reply,
                Message = reply.Message
            });

    private static void SendResult(Aisling viewer, SaveOutcome outcome)
        => viewer.Client.SendCollegeDisplay(
            new CollegeDisplayArgs
            {
                Type = CollegeDisplayType.WriterResult,
                Result = outcome.Result,
                Message = outcome.Message
            });

    public static string VoteText(VoteResult result)
        => result switch
        {
            VoteResult.Saved    => CollegeText.VOTE_SAVED,
            VoteResult.Closed   => "Voting on that entry has closed.",
            VoteResult.OwnEntry => "You can't vote on your own entry.",
            _                   => "You can't vote on that entry."
        };

    public static string VerdictText(VerdictResult result)
        => result switch
        {
            VerdictResult.Posted     => CollegeText.VERDICT_POSTED,
            VerdictResult.OwnEntry   => "An admin must decide your own entry.",
            VerdictResult.NotWaiting => "That entry isn't awaiting a verdict.",
            _                        => "You can't post that verdict."
        };

    // ---- mapping ----

    public static CollegePiece ToPiece(CollegePieceInfo info, CollegeSubject subject)
        => new()
        {
            Subject = subject,
            Title = info.Title,
            Blocks = info.Blocks
                         .Select(
                             b => new PieceBlock
                             {
                                 Type = b.Kind switch
                                 {
                                     CollegeBlockKind.Heading => PieceBlockType.Heading,
                                     CollegeBlockKind.Picture => PieceBlockType.Picture,
                                     _                        => PieceBlockType.Text
                                 },
                                 Text = b.Kind == CollegeBlockKind.Picture ? "" : b.Text,
                                 Hash = b.Kind == CollegeBlockKind.Picture ? b.Hash : ""
                             })
                         .ToList()
        };

    public static CollegePieceInfo ToInfo(CollegePiece piece)
        => new()
        {
            Subject = (CollegeSubjectCode)piece.Subject,
            Title = piece.Title,
            Blocks = piece.Blocks
                          .Select(
                              b => new CollegeBlockInfo
                              {
                                  Kind = b.Type switch
                                  {
                                      PieceBlockType.Heading => CollegeBlockKind.Heading,
                                      PieceBlockType.Picture => CollegeBlockKind.Picture,
                                      _                      => CollegeBlockKind.Text
                                  },
                                  Text = b.Text,
                                  Hash = b.Hash
                              })
                          .ToList()
        };

    public static CollegeDisplayArgs PieceArgs(PieceView view)
        => new()
        {
            Type = CollegeDisplayType.Piece,
            Context = view.Context,
            Id = view.Id,
            Header = view.Header,
            Author = view.Author,
            MyTier = view.MyTier is { } tier ? (byte)tier : CollegeProtocol.NO_TIER,
            MyComment = view.MyComment,
            CanModerate = view.CanModerate,
            Votes = view.Votes
                        .Select(v => new CollegeVoteInfo { Judge = v.Judge, Tier = (CollegeTierCode)v.Tier, Comment = v.Comment })
                        .ToList(),
            Piece = ToInfo(view.Piece)
        };

    public static List<CollegeJudgingRowInfo> JudgingRows(IReadOnlyList<JudgingRow> rows)
        => rows.Select(
                   r => new CollegeJudgingRowInfo
                   {
                       Id = r.Id,
                       Subject = (CollegeSubjectCode)r.Subject,
                       Title = r.Title,
                       DaysLeft = (byte)Math.Clamp(r.DaysLeft, 0, byte.MaxValue),
                       MyTier = r.MyTier is { } tier ? (byte)tier : CollegeProtocol.NO_TIER,
                       Waiting = r.Waiting
                   })
               .ToList();

    public static List<CollegeGalleryRowInfo> GalleryRows(IReadOnlyList<GalleryRow> rows)
        => rows.Select(
                   r => new CollegeGalleryRowInfo
                   {
                       Id = r.Id,
                       Title = r.Title,
                       Author = r.Author,
                       Tier = (CollegeTierCode)r.Tier,
                       ClaimedUnix = (uint)new DateTimeOffset(r.ClaimedUtc).ToUnixTimeSeconds()
                   })
               .ToList();

    public static List<CollegeHandInRowInfo> HandInRows(IReadOnlyList<HandInRow> rows)
        => rows.Select(
                   r => new CollegeHandInRowInfo
                   {
                       Id = r.Id,
                       Student = r.Student,
                       Title = r.Title,
                       HandedInUnix = (uint)new DateTimeOffset(r.Utc).ToUnixTimeSeconds()
                   })
               .ToList();
}
```

Notes for the implementer:
- `logger` is kept for future use; if the build warns about an unused parameter, log one `LogDebug` line in `ShowToClass`.
- Before relying on the comment in `ShowToClass`, read `WorldServer.ExecuteHandler` and confirm it runs the handler inside the sender's map synchronization. If it doesn't, collect the aislings the way `CollegeLecternScript.RemoveStudent` does from a dialog, or schedule the send on the map (follow whatever pattern `ExecuteHandler` documents).
- `TownRequestThrottle` must accept `(TimeProvider, TimeSpan)`; check its constructor in `Chaos/Services/Towns/`. If it is `internal` to another assembly, copy its 15 lines into a private nested class here.

- [ ] **Step 3: Handle the packet.** In `WorldServer.cs` add a field `private readonly CollegePanel CollegePanel;`, a constructor parameter `CollegePanel collegePanel` (assign it like `TownBallotPanel`), and after `OnTowerLeaderboardRequest`:

```csharp
    /// <summary>Routes a College window action to <see cref="CollegePanel" />, which checks every rule through the College service.</summary>
    public ValueTask OnCollegeAction(IChaosWorldClient client, in Packet packet)
    {
        var args = PacketSerializer.Deserialize<CollegeActionArgs>(in packet);

        return ExecuteHandler(client, args, InnerOnCollegeAction);

        ValueTask InnerOnCollegeAction(IChaosWorldClient localClient, CollegeActionArgs localArgs)
        {
            if (localClient.Connected && localClient.Aisling is { } aisling)
                CollegePanel.Handle(aisling, localArgs);

            return default;
        }
    }
```

Register it after the `TowerLeaderboardRequest` line: `ClientHandlers[(byte)ClientOpCode.CollegeAction] = OnCollegeAction;`. If any test constructs `WorldServer` directly (`grep -rn "new WorldServer(" Tests`), add the new argument there.

- [ ] **Step 4: Register the services.** Replace `AddCollege` in `ServiceCollectionExtensions.cs` with:

```csharp
    /// <summary>
    ///     Mileth College: the service that owns classes, marks and entries; the piece and picture stores; uploads; mail;
    ///     rewards; the window panel; the notifier; and the background tick.
    /// </summary>
    public static void AddCollege(this IServiceCollection services)
    {
        services.AddOptionsFromConfig<CollegeOptions>(ConfigKeys.Options.Key);
        services.ConfigureOptions<DirectoryBoundOptionsConfigurer<CollegeOptions>>();
        services.AddSingleton<ICollegeNotifier, CollegeNotifier>();
        services.AddSingleton<ICollegePieceStore, CollegePieceStore>();
        services.AddSingleton<ICollegePictureStore, CollegePictureStore>();
        services.AddSingleton(sp => new PictureUploads(sp.GetRequiredService<ICollegePictureStore>(), sp.GetRequiredService<IOptions<CollegeOptions>>().Value));
        services.AddSingleton<ICollegeMail, CollegeMail>();
        services.AddSingleton<CollegeService>();
        services.AddSingleton<CollegeRewards>();
        services.AddSingleton<CollegePanel>();
        services.AddHostedService<CollegeTickService>();
    }
```

`CollegePieceStore` and `CollegePictureStore` each have two constructors; if DI complains about ambiguity, register them with factories (`sp => new CollegePieceStore(sp.GetRequiredService<IOptions<CollegeOptions>>())`). Check that `DirectoryBoundOptionsConfigurer<T>` binds to the staging directory the same way `TowerOptions` does, so pieces land in `<staging>/LocalStorage/College/`.

- [ ] **Step 5: Run the tests and build.** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/CollegePanelMappingTests/*"` -> 3 passed. `dotnet build Chaos.slnx` -> succeeds. Start the server (`dotnet run --project Chaos/Chaos.csproj`), wait for the World listener line, check the log has no DI or College errors, then stop it.

```json:metadata
{"files": ["Chaos/Services/College/CollegePanel.cs", "Chaos/Services/Servers/WorldServer.cs", "Chaos/Extensions/ServiceCollectionExtensions.cs", "Tests/Chaos.Tests/College/CollegePanelMappingTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/CollegePanelMappingTests/*\"", "acceptanceCriteria": ["ToPiece/ToInfo lossless", "row mapping incl. NO_TIER and day clamp", "Handle routes every action; 250 ms throttle on non-picture actions", "PictureFetch parts and 30/min cap", "AddCollege registers everything; server starts"], "modelTier": "standard"}
```

---

### Task 9: Registrar, lectern, gallery stand and admin command (server worktree)

**Goal:** Add the Registrar's Write an entry, My entries, Judge entries and Class hand-ins menus; the lectern's Activity, prompt and hand-in options; the `collegeGalleryStand` reactor; and the new `/college` subcommands.

**Files:**
- Modify: `Chaos/Scripting/DialogScripts/Temuair/College/CollegeRegistrarScript.cs`
- Modify: `Chaos/Scripting/DialogScripts/Temuair/College/CollegeLecternScript.cs`
- Create: `Chaos/Scripting/ReactorTileScripts/Temauir/College/CollegeGalleryStandScript.cs`
- Modify: `Chaos/Messaging/Admin/CollegeCommand.cs`

**Acceptance Criteria:**
- [ ] The Registrar shows "Write an entry" and "My entries" to everyone, "Judge entries" to anyone `CanJudge` allows, and "Class hand-ins" to anyone with hand-ins.
- [ ] Write an entry lists all six subjects; Art and Music read "Art (opens soon)" / "Music (opens soon)" and show "Art and Music entries open soon." instead of a window.
- [ ] My entries lists entries as "History: The Fall of Old" (cut to 35 characters); picking one offers exactly the actions its status allows (Read, Withdraw, Claim, Dismiss, Hide from gallery, Show in gallery) and runs them through the service; Claim also calls `CollegeRewards.Sync` and sends `CollegeText.Claimed`.
- [ ] Activity appears as a format at the Registrar and the lectern only for the four writing subjects; after starting an Activity class the lectern asks for a prompt; during it, students see "Write a hand-in" and the Teacher sees "Hand-ins" and "Change the prompt".
- [ ] Clicking a gallery stand opens the gallery on the stand's subject.
- [ ] `/college entries`, `/college entry <id>`, `/college removeentry <id>` and `/college closevoting <id>` work as the spec's table says, and the help text lists them.

**Verify:** `dotnet build Chaos.slnx` succeeds and `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.College/*/*"` passes. Dialog behaviour is checked in game (Task 18 checklist).

**Steps:**

- [ ] **Step 1: Gallery stand.** Create `Chaos/Scripting/ReactorTileScripts/Temauir/College/CollegeGalleryStandScript.cs` (copy the `BulletinBoardScript` pattern for script vars):

```csharp
using Chaos.Models.World;
using Chaos.Scripting.ReactorTileScripts.Abstractions;
using Chaos.Services.College;

namespace Chaos.Scripting.ReactorTileScripts.Temauir.College;

/// <summary>A gallery stand in the Lyceum hall. Opens the gallery on its subject (<c>scriptVars.collegeGalleryStand.subject</c>).</summary>
public sealed class CollegeGalleryStandScript : ConfigurableReactorTileScriptBase
{
    private readonly CollegePanel Panel;
    public CollegeSubject Subject { get; init; }

    /// <inheritdoc />
    public CollegeGalleryStandScript(ReactorTile subject, CollegePanel panel)
        : base(subject)
        => Panel = panel;

    /// <inheritdoc />
    public override void OnClicked(Aisling source) => Panel.OpenGallery(source, Subject);
}
```

If `ConfigurableReactorTileScriptBase` names its subject constructor parameter differently or binds enums from strings differently, match `BulletinBoardScript` exactly; if enums don't bind, make `Subject` a `string` and parse it with `Enum.TryParse` in `OnClicked`.

- [ ] **Step 2: Registrar constants and dependencies.** In `CollegeRegistrarScript`, inject `CollegePanel panel` and `CollegeRewards rewards` in the constructor (fields `Panel`, `Rewards`). Add constants:

```csharp
    private const string WRITE = "Write an entry";
    private const string MY_ENTRIES = "My entries";
    private const string JUDGE = "Judge entries";
    private const string HAND_INS = "Class hand-ins";
    private const string READ = "Read it";
    private const string WITHDRAW = "Withdraw (no refund)";
    private const string CLAIM = "Claim the award";
    private const string DISMISS = "Dismiss";
    private const string HIDE = "Hide from gallery";
    private const string SHOW = "Show in gallery";
    private const string OPENS_SOON = " (opens soon)";
```

Add a private record for the picked entry: `private sealed record EntryPick(int Id);`.

- [ ] **Step 3: Registrar main menu.** At the start of `ShowMain`, before the teaching options, insert (positions count from 1 because option 0 is "Timetable"):

```csharp
        Subject.InsertOption(position++, WRITE, "college_registrar_write_subject");
        Subject.InsertOption(position++, MY_ENTRIES, "college_registrar_entries");

        if (College.CanJudge(source.Name, source.IsKnight, source.IsAdmin))
            Subject.InsertOption(position++, JUDGE, "college_registrar_judge");

        if (College.HasHandIns(source.Name))
            Subject.InsertOption(position++, HAND_INS, "college_registrar_handins");
```

- [ ] **Step 4: Registrar displaying.** Add these cases to `OnDisplaying`'s switch:

```csharp
            case "college_registrar_write_subject":
                Subject.Options.Clear();

                foreach (var subject in Enum.GetValues<CollegeSubject>())
                    Subject.AddOption(
                        College.Options.IsOpen(subject) ? subject.ToString() : subject + OPENS_SOON,
                        "college_registrar_write_open");

                break;
            case "college_registrar_write_open":
                if (Subject.Context is CollegeSubject chosen && College.Options.IsOpen(chosen))
                {
                    Panel.OpenWriter(source, chosen);
                    Subject.Close(source);
                } else
                    Subject.Text = "Art and Music entries open soon. Their tools are still being made.";

                break;
            case "college_registrar_judge":
                Panel.OpenJudging(source);
                Subject.Close(source);

                break;
            case "college_registrar_handins":
                Panel.OpenHandIns(source, false);
                Subject.Close(source);

                break;
            case "college_registrar_entries":
                ShowEntries(source);

                break;
            case "college_registrar_entry":
                ShowEntry(source);

                break;
            case "college_registrar_entry_done":
                Subject.Text = Subject.Context as string ?? "Done.";

                break;
            case "college_registrar_book_format":
                if (Subject.Context is BookDraft draftForFormat && !CollegeOptions.IsWritingSubject((CollegeSubject)draftForFormat.Subject))
                    RemoveOption("Activity");

                break;
```

Add these helpers to the Menus region:

```csharp
    private void ShowEntries(Aisling source)
    {
        var entries = College.MyEntries(source.Name);

        if (entries.Count == 0)
        {
            Subject.Reply(source, "You have no entries. Write one first.");

            return;
        }

        Subject.Options.Clear();

        foreach (var entry in entries)
            Subject.AddOption(EntryLabel(entry), "college_registrar_entry");
    }

    private void ShowEntry(Aisling source)
    {
        if (Subject.Context is not EntryPick pick || College.MyEntries(source.Name).FirstOrDefault(e => e.Id == pick.Id) is not { } entry)
        {
            Subject.Reply(source, "That entry has changed. Please look again.");

            return;
        }

        Subject.Options.Clear();
        Subject.AddOption(READ, "college_registrar_entry_done");

        switch (entry.Status)
        {
            case EntryStatus.Voting:
                Subject.Text = $"\"{entry.Title}\" is being judged. Voting ends in {entry.DaysLeft} days.";
                Subject.AddOption(WITHDRAW, "college_registrar_entry_done");

                break;
            case EntryStatus.AwaitingVerdict:
                Subject.Text = $"\"{entry.Title}\" is awaiting the Director's verdict.";

                break;
            case EntryStatus.Decided when entry.Verdict is AwardTier.None:
                Subject.Text = $"\"{entry.Title}\" received no award this time.";
                Subject.AddOption(DISMISS, "college_registrar_entry_done");

                break;
            case EntryStatus.Decided:
                Subject.Text = $"\"{entry.Title}\" received the {entry.Verdict} Award!";
                Subject.AddOption(CLAIM, "college_registrar_entry_done");

                break;
            case EntryStatus.Claimed:
                Subject.Text = $"\"{entry.Title}\" holds the {entry.Verdict} Award.";
                Subject.AddOption(entry.Hidden ? SHOW : HIDE, "college_registrar_entry_done");

                break;
        }
    }

    private static string EntryLabel(EntryView entry)
    {
        var label = $"{entry.Subject}: {entry.Title}";

        return label.Length <= 35 ? label : label[..35];
    }

    private void RemoveOption(string text)
    {
        var option = Subject.Options.FirstOrDefault(o => o.OptionText == text);

        if (option is not null)
            Subject.Options.Remove(option);
    }
```

If `Subject.Options` items expose the text under another name than `OptionText`, use that name (check `DialogOption`).

- [ ] **Step 5: Registrar actions.** Add these cases to `OnNext`'s switch:

```csharp
            case "college_registrar_write_subject" when option is not null:
                var name = option.Replace(OPENS_SOON, string.Empty);

                if (Enum.TryParse<CollegeSubject>(name, out var writeSubject))
                    Subject.Context = writeSubject;

                break;
            case "college_registrar_entries" when optionIndex is { } entryIndex:
                var listed = College.MyEntries(source.Name).ElementAtOrDefault(entryIndex - 1);
                Subject.Context = listed is not null && (EntryLabel(listed) == option) ? new EntryPick(listed.Id) : null;

                break;
            case "college_registrar_entry" when Subject.Context is EntryPick pick:
                Subject.Context = EntryAction(source, pick.Id, option);

                break;
```

And the action helper in the Actions region:

```csharp
    private string? EntryAction(Aisling source, int id, string? option)
    {
        switch (option)
        {
            case READ:
                Panel.OpenPiece(source, CollegePieceSource.Entry, id);

                return null;
            case WITHDRAW:
                return College.Withdraw(source.Name, id) ? "Your entry was withdrawn. Its marks stay spent." : "That entry can't be withdrawn now.";
            case DISMISS:
                return College.Dismiss(source.Name, id) ? "The entry is cleared. Better luck next time." : "That entry can't be cleared.";
            case HIDE:
            case SHOW:
                return College.SetHidden(source.Name, id, option == HIDE)
                    ? option == HIDE ? "Your piece is hidden from the gallery." : "Your piece is back in the gallery."
                    : "That piece isn't in the gallery.";
            case CLAIM:
                var result = College.Claim(source.Name, id, out var tier, out var subject);

                if (result != ClaimResult.Claimed)
                    return "That award can't be claimed.";

                Rewards.Sync(source);
                source.SendOrangeBarMessage(CollegeText.Claimed(tier, subject));

                return $"Congratulations! The {tier} Award in {subject} is yours.";
            default:
                return null;
        }
    }
```

`college_registrar_entry_done` shows the returned text; for Read (null) it shows "Done.". Change that case to close the dialog when `Subject.Context` is null: `if (Subject.Context is string text) Subject.Text = text; else Subject.Close(source);`.

`CollegePieceSource` is in `Chaos.DarkAges.Definitions`; add the using.

- [ ] **Step 6: Lectern.** In `CollegeLecternScript`, inject `CollegePanel panel` (field `Panel`). Add constants `WRITE_HAND_IN = "Write a hand-in"`, `HAND_INS = "Hand-ins"`, `CHANGE_PROMPT = "Change the prompt"`.

In `ShowMain`, in the running-class branch:
- for someone who doesn't own the class but is in its room during an Activity class: after the "is teaching" text, `if (running.Format == ClassFormat.Activity && running.Room.Equals(here, StringComparison.OrdinalIgnoreCase)) Subject.AddOption(WRITE_HAND_IN, "college_lectern_handin");`;
- for the owner during an Activity class: add `Subject.AddOption(HAND_INS, "college_lectern_handins");` and `Subject.AddOption(CHANGE_PROMPT, "college_lectern_prompt");` after "Class status".

Add `OnDisplaying` cases:

```csharp
            case "college_lectern_handin":
                Panel.OpenHandInWriter(source);
                Subject.Close(source);

                break;
            case "college_lectern_handins":
                Panel.OpenHandIns(source, true);
                Subject.Close(source);

                break;
            case "college_lectern_begin_now_format":
                var walkIn = Subject.Context is string s && Enum.TryParse<CollegeSubject>(s, out var subjectHere) ? subjectHere : (CollegeSubject?)null;

                if (walkIn is { } subjectNow && CollegeOptions.IsWritingSubject(subjectNow))
                    Subject.Text = $"Lecture, Discussion or Activity? A class now may run up to {College.WalkInLimit()} minutes.";
                else
                {
                    Subject.Text = $"Lecture or Discussion? A class now may run up to {College.WalkInLimit()} minutes.";
                    var activity = Subject.Options.FirstOrDefault(o => o.OptionText == "Activity");

                    if (activity is not null)
                        Subject.Options.Remove(activity);
                }

                break;
            case "college_lectern_prompt_done":
                Subject.Text = Subject.Context as string ?? "Done.";

                break;
```

This replaces the existing `college_lectern_begin_now_format` case. Replace the shared `college_lectern_begin_done` / `college_lectern_remove_done` case with two cases, so a Teacher who just started an Activity class is asked for the prompt (Task 10 turns `college_lectern_begin_done.json` into a `DialogMenu` with no fixed options):

```csharp
            case "college_lectern_begin_done":
                Subject.Text = Subject.Context as string ?? "Done.";

                if (College.Running() is { Format: ClassFormat.Activity } started
                    && started.Teacher.Equals(source.Name, StringComparison.OrdinalIgnoreCase))
                {
                    Subject.Text += " Set the prompt your students will write about.";
                    Subject.AddOption(CHANGE_PROMPT, "college_lectern_prompt");
                }

                break;
            case "college_lectern_remove_done":
                Subject.Text = Subject.Context as string ?? "Done.";

                break;
```

Add the prompt text entry in `OnNext`:

```csharp
            case "college_lectern_prompt":
                Subject.Context = TryFetchArgs<string>(out var prompt) && !string.IsNullOrWhiteSpace(prompt)
                                  && College.SetPrompt(source.Name, source.IsAdmin, prompt)
                    ? "The prompt is set. Students can now write a hand-in here."
                    : "Only the class's Teacher can set its prompt.";

                break;
```

- [ ] **Step 7: Admin command.** In `CollegeCommand`, extend the help text and `USAGE` with `| entries | entry <id> | removeentry <id> | closevoting <id>`, add these cases to the switch, and add the methods:

```csharp
            case "entries":
                Entries(source);

                break;
            case "entry":
                Entry(source, args);

                break;
            case "removeentry":
                IdAction(source, args, id => College.RemoveEntry(source.Name, true, id), "Entry {0} removed.", "No entry {0}.");

                break;
            case "closevoting":
                IdAction(source, args, College.CloseVoting, "Voting on entry {0} is closed.", "Entry {0} isn't in voting.");

                break;
```

```csharp
    private void Entries(Aisling source)
    {
        var rows = College.AdminEntries();

        if (rows.Count == 0)
        {
            source.SendServerMessage(ServerMessageType.ActiveMessage, "No open or waiting entries.");

            return;
        }

        foreach (var row in rows)
            source.SendServerMessage(ServerMessageType.ActiveMessage, row);
    }

    private void Entry(Aisling source, ArgumentCollection args)
    {
        if (!args.TryGetNext<int>(out var id) || College.AdminEntry(id) is not { } lines)
        {
            source.SendServerMessage(ServerMessageType.ActiveMessage, "Usage: /college entry <id>");

            return;
        }

        foreach (var line in lines)
            source.SendServerMessage(ServerMessageType.ActiveMessage, line);
    }

    private void IdAction(Aisling source, ArgumentCollection args, Func<int, bool> action, string done, string failed)
    {
        if (!args.TryGetNext<int>(out var id))
        {
            source.SendServerMessage(ServerMessageType.ActiveMessage, USAGE);

            return;
        }

        var ok = action(id);
        source.SendServerMessage(ServerMessageType.ActiveMessage, string.Format(CultureInfo.InvariantCulture, ok ? done : failed, id));

        if (ok)
            Logger.LogInformation("{Admin} ran /college on entry {Id}", source.Name, id);
    }
```

Match how the existing `Status` method sends lines (if it uses a helper or another `ServerMessageType`, use the same). Add to `CollegeService.Entries.cs`:

```csharp
    public IReadOnlyList<string> AdminEntries()
    {
        using var scope = Sync.EnterScope();

        return State.Entries
                    .Where(e => e.Status is EntryStatus.Voting or EntryStatus.AwaitingVerdict)
                    .Select(e => $"#{e.Id} {e.Subject} \"{e.Title}\" by {e.Author}: {e.Status}, {EntryBook.DaysLeft(e, Now)}d left, {e.Votes.Count} votes")
                    .ToList();
    }

    public IReadOnlyList<string>? AdminEntry(int id)
    {
        using var scope = Sync.EnterScope();

        if (EntryBook.Find(State, id) is not { } e)
            return null;

        var lines = new List<string> { $"#{e.Id} {e.Subject} \"{e.Title}\" by {e.Author}: {e.Status}" };
        lines.AddRange(e.Votes.Values.Select(v => $"{v.Judge}: {v.Tier}{(v.Comment.Length > 0 ? " - " + v.Comment : "")}"));

        if (e.Verdict is { } verdict)
            lines.Add($"Verdict {verdict.Tier} by {verdict.By}{(verdict.Note.Length > 0 ? ": " + verdict.Note : "")}");

        return lines;
    }
```

- [ ] **Step 8: Build and run the College tests.** `dotnet build Chaos.slnx` -> succeeds. `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.College/*/*"` -> all pass.

```json:metadata
{"files": ["Chaos/Scripting/DialogScripts/Temuair/College/CollegeRegistrarScript.cs", "Chaos/Scripting/DialogScripts/Temuair/College/CollegeLecternScript.cs", "Chaos/Scripting/ReactorTileScripts/Temauir/College/CollegeGalleryStandScript.cs", "Chaos/Messaging/Admin/CollegeCommand.cs", "Chaos/Services/College/CollegeService.Entries.cs"], "verifyCommand": "dotnet build Chaos.slnx", "acceptanceCriteria": ["Registrar menu options by role", "Write an entry lists six subjects with opens-soon for Art/Music", "My entries actions by status; Claim syncs rewards", "Activity format only for writing subjects; prompt; hand-in options", "gallery stand opens its subject", "/college entries, entry, removeentry, closevoting"], "modelTier": "standard"}
```

---
### Task 10: Dialogs and gallery stands (Unora worktree)

**Goal:** Add the College dialog templates part 2 needs, the Activity format options, and six gallery stands in the Lyceum hall through `college_maps.py`.

**Files:**
- Create under `Data/Configuration/Templates/Dialogs/Temauir/mileth/college/`: `college_registrar_write_subject.json`, `college_registrar_write_open.json`, `college_registrar_judge.json`, `college_registrar_handins.json`, `college_registrar_entries.json`, `college_registrar_entry.json`, `college_registrar_entry_done.json`, `college_lectern_handin.json`, `college_lectern_handins.json`, `college_lectern_prompt.json`, `college_lectern_prompt_done.json`
- Modify there: `college_registrar_book_format.json`, `college_lectern_begin_now_format.json` (add Activity), `college_lectern_begin_done.json` (DialogMenu), `college_registrar_about2.json` (one line about entries)
- Modify: `Tools/College/college_maps.py` (stands), `Tools/College/test_college_maps.py`
- Regenerated by the tool: `Data/Configuration/MapData/lod762.map`, `Data/Configuration/MapInstances/Temuair/Towns/Mileth/College/Mileth_College/reactors.json`

**Acceptance Criteria:**
- [ ] `python Tools/College/check_dialogs.py` prints `ok`.
- [ ] `python -m unittest Tools.College.test_college_maps` passes, including the new stand tests: six stands, one per subject, each on the centre of its carpet-framed marble square, walkable in the base map, with a `collegeGalleryStand` reactor naming its subject, and every other byte of map 762 unchanged apart from the board and the stands.
- [ ] `python Tools/College/college_maps.py --preview <dir>` renders the hall with the six stands; the preview is sent to the user and their stand-art choice is applied (Deferred decision).

**Verify:** `python Tools/College/check_dialogs.py` -> `ok`; `python -m unittest Tools.College.test_college_maps` -> OK.

**Steps:**

- [ ] **Step 1: Add the dialogs.** Create each file below. All use `"scriptKeys": ["collegeRegistrar"]` (or `["collegeLectern"]` for lectern ones) and `"scriptVars": {}`.

`college_registrar_write_subject.json`:

```json
{
  "contextual": true,
  "options": [],
  "scriptKeys": [
    "collegeRegistrar"
  ],
  "scriptVars": {},
  "templateKey": "college_registrar_write_subject",
  "text": "Which subject will you write for? Your draft is kept until you submit it.",
  "type": "DialogMenu"
}
```

`college_registrar_write_open.json`, `college_registrar_judge.json`, `college_registrar_handins.json`, `college_registrar_entry_done.json` all have this shape (change `templateKey`; the script sets the text or closes the dialog):

```json
{
  "contextual": true,
  "nextDialogKey": "close",
  "options": [],
  "scriptKeys": [
    "collegeRegistrar"
  ],
  "scriptVars": {},
  "templateKey": "college_registrar_write_open",
  "text": "",
  "type": "Normal"
}
```

`college_registrar_entries.json`: `DialogMenu`, contextual, no options, text `"Your entries. Choose one to see what you can do."`. `college_registrar_entry.json`: `DialogMenu`, contextual, no options, text `""`.

Lectern: `college_lectern_handin.json`, `college_lectern_handins.json` and `college_lectern_prompt_done.json` use the `Normal` shape above with `"scriptKeys": ["collegeLectern"]`. `college_lectern_prompt.json`:

```json
{
  "contextual": true,
  "nextDialogKey": "college_lectern_prompt_done",
  "options": [],
  "scriptKeys": [
    "collegeLectern"
  ],
  "scriptVars": {},
  "templateKey": "college_lectern_prompt",
  "text": "What should your students write about? Keep it to one line.",
  "textBoxLength": 120,
  "type": "DialogTextEntry"
}
```

- [ ] **Step 2: Activity options and the begin dialog.** In `college_registrar_book_format.json` add a third option `{"dialogKey": "college_registrar_book_day", "optionText": "Activity"}` and change the text to `"Will it be a lecture, a discussion or a writing activity?"`. In `college_lectern_begin_now_format.json` add `{"dialogKey": "college_lectern_begin_done", "optionText": "Activity"}`. In `college_lectern_begin_done.json` set `"type": "DialogMenu"` and remove `nextDialogKey` if present (the script adds the prompt option). In `college_registrar_about2.json` add a sentence to the text, keeping it within 360 characters: `"Students may also enter their own writing for 3 marks."` (shorten the existing text if needed).

- [ ] **Step 3: Run the dialog check.** `python Tools/College/check_dialogs.py`. Expected: `ok`. Fix any option over 35 characters or text over 360.

- [ ] **Step 4: Add the stand tests.** In `Tools/College/test_college_maps.py` add:

```python
    def test_six_gallery_stands_one_per_subject(self):
        _, reactors = cm.all_entities(self.maps, self.sotp)[cm.HALL_INSTANCE]
        stands = {r["scriptVars"]["collegeGalleryStand"]["subject"]: cm.point(r["source"])
                  for r in reactors if r["scriptKeys"] == ["collegeGalleryStand"]}
        self.assertEqual(set(stands), {"Art", "Music", "History", "Literature", "Lore", "Philosophy"})
        self.assertEqual(stands, cm.STANDS)
        hall = self.maps[cm.HALL_ID]
        for subject, at in stands.items():
            self.assertEqual(hall.fg(*at), cm.STAND_ART[subject], subject)

    def test_stands_sit_on_walkable_marble_inside_their_squares(self):
        base = cm.Grid.load(cm.HERE / "lyceum_762_base.map", cm.HALL_W, cm.HALL_H)
        for subject, (x0, y0, x1, y1) in cm.STAND_SQUARES.items():
            x, y = cm.STANDS[subject]
            self.assertTrue(x0 <= x <= x1 and y0 <= y <= y1, subject)
            self.assertIn(base.bg(x, y), cm.MARBLE, subject)
            self.assertTrue(cm.walkable(base, x, y, self.sotp), subject)
            self.assertIn(base.bg(x0 - 1, y), cm.CARPET, f"{subject} west edge")
            self.assertIn(base.bg(x1 + 1, y), cm.CARPET, f"{subject} east edge")

    def test_hall_changes_only_on_the_board_and_stands(self):
        base = cm.Grid.load(cm.HERE / "lyceum_762_base.map", cm.HALL_W, cm.HALL_H)
        built = self.maps[cm.HALL_ID]
        changed = {(x, y) for y in range(cm.HALL_H) for x in range(cm.HALL_W) if base.t[y][x] != built.t[y][x]}
        self.assertEqual(changed, {cm.BOARD} | set(cm.STANDS.values()))
```

Run `python -m unittest Tools.College.test_college_maps`. Expected: the three new tests fail with `AttributeError: ... has no attribute 'STANDS'`.

- [ ] **Step 5: Place the stands.** In `Tools/College/college_maps.py`, after the `BOARD_KEY` line, add:

```python
# Gallery stands: one per subject, on the centre of each marble square that the hall's carpet frames. The four north
# squares sit between the alcove carpets (x 11-13, 18-20, 25-27, 32-34, y 2-9); the two south squares are x 11-14 and
# x 20-31, y 16-24. Background ids: marble 19366-19372, carpet 7918-7941.
MARBLE = set(range(19366, 19373))
CARPET = set(range(7918, 7942))
STAND_SQUARES = {
    "History": (11, 2, 13, 9),
    "Literature": (18, 2, 20, 9),
    "Lore": (25, 2, 27, 9),
    "Philosophy": (32, 2, 34, 9),
    "Art": (11, 16, 14, 24),
    "Music": (20, 16, 31, 24),
}
STANDS = {
    "History": (12, 6),
    "Literature": (19, 6),
    "Lore": (26, 6),
    "Philosophy": (33, 6),
    "Art": (12, 20),
    "Music": (25, 20),
}
BOOK_LECTERN = (11017, 11018)  # the Mileth Town Hall's book lectern
STAND_ART = {subject: BOOK_LECTERN for subject in STANDS}
```

Change `build_hall` to place them:

```python
def build_hall() -> Grid:
    g = Grid.load(HERE / "lyceum_762_base.map", HALL_W, HALL_H)
    g.set(*BOARD, l=BOARD_ART[0], r=BOARD_ART[1])
    for subject, at in STANDS.items():
        g.set(*at, l=STAND_ART[subject][0], r=STAND_ART[subject][1])
    return g
```

In `hall_entities`, after the bulletin board reactor:

```python
    reactors += [reactor("collegeGalleryStand", at, {"subject": subject}, True) for subject, at in STANDS.items()]
```

In `self_check`, after the existing hall checks, add a check that each stand tile is marble and walkable in the base map (raise `LayoutError` with the subject otherwise), mirroring `test_stands_sit_on_walkable_marble_inside_their_squares`.

Verify the north squares' y range before running: the guards stand at `(12, 2)`, `(19, 2)`, `(26, 2)` and `(33, 2)`, so the stands at y 6 leave them room. If `y 2-9` isn't marble on all three columns of a square, print the hall's background grid (`python -c "..."` with `Grid.bg`) and correct `STAND_SQUARES` and `STANDS` to the real centre.

- [ ] **Step 6: Run the tests and the tool.** `python -m unittest Tools.College.test_college_maps` -> OK. Then write the files and a preview: `python Tools/College/college_maps.py --preview C:\Users\Michael\AppData\Local\Temp\college-preview`. Check that `lod762.map` and the hall's `reactors.json` changed and nothing outside the College folders did (`git status --short`).

- [ ] **Step 7: Ask the user about the stand art (Deferred decision).** Send `college-preview/762.png` to the user (SendUserFile, display render) and ask: "These six stands use the Town Hall's book lectern. Keep it on all six, or pick different furniture for Art and Music?" If they pick other art, change `STAND_ART` for those subjects (foreground pair ids from a tile they name or a preview you render for them), re-run Step 6, and send the new preview.

```json:metadata
{"files": ["Data/Configuration/Templates/Dialogs/Temauir/mileth/college/", "Tools/College/college_maps.py", "Tools/College/test_college_maps.py", "Data/Configuration/MapData/lod762.map", "Data/Configuration/MapInstances/Temuair/Towns/Mileth/College/Mileth_College/reactors.json"], "verifyCommand": "python -m unittest Tools.College.test_college_maps", "acceptanceCriteria": ["check_dialogs prints ok", "six stands, one per subject, walkable marble centres, reactor with subject", "hall changes only on board and stands", "preview sent and stand art confirmed by the user"], "modelTier": "standard"}
```

---

### Task 11: College emblems (Unora worktree)

**Goal:** Draw six 11 x 11 emblem designs, recolour each into the four tier metals as art 259-282, write the 24 emblem templates, and build the setoa.dat batch.

**Files:**
- Create: `Tools/College/emblems/base_art.png` ... `base_philosophy.png` (six 11 x 11 RGBA PNGs)
- Create: `Tools/College/emblem_tiers.py`, `Tools/College/test_emblem_tiers.py`
- Create (generated): `Tools/Emblems/art/custom/embl259.png` ... `embl282.png`, `Data/Configuration/Templates/Emblems/college_<subject>_<tier>.json` (24 files)

**Acceptance Criteria:**
- [ ] The six base designs (brush, lute, quill, scroll, tome, eye) are 11 x 11 RGBA with a transparent background, drawn in greys, and the user approved them.
- [ ] `emblem_tiers.py` writes 24 PNGs numbered 259-282 in the order Art, Music, Literature, History, Lore, Philosophy x Clave, Village, Kingdom, Aisling, recoloured bronze, silver, gold and pale blue.
- [ ] It writes 24 emblem JSON files with key `college_<subject>_<tier>`, the art number, name `"<Tier> <Noun>"`, description `"<Tier> Award in <Subject>, Mileth College."` and `"source": {"staff": true}`.
- [ ] `python -m unittest Tools.College.test_emblem_tiers` passes; `python -m unittest Tools/Emblems/test_emblems.py` still passes.
- [ ] `python Tools/Emblems/build.py --no-apply` produces a Desktop batch folder whose setoa.dat holds embl259-282, built on the live setoa.dat (see the "Dats live 2026-10-04" note: build on those dats).

**Verify:** `python -m unittest Tools.College.test_emblem_tiers` -> OK; the build prints its batch folder and `review/emblems.png` shows 259-282.

**Steps:**

- [ ] **Step 1: Draw the bases.** With PixelLab's `pixelart_workbench` (load it with ToolSearch; start with `pixelart_workbench(argv=['describe', 'start'], mode='high')`), draw six 11 x 11 icons in 4-5 greys plus a dark outline on a transparent background: brush (Art), lute (Music), quill (Literature), scroll (History), closed tome (Lore), open eye (Philosophy). Keep the outline near-black (not pure black; the client draws pure black as transparent). Save them as `Tools/College/emblems/base_<subject>.png` (lower-case subject). Render a 10x-scaled sheet of all six side by side and send it to the user. Wait for approval; redraw any they reject.

- [ ] **Step 2: Write the failing test.** Create `Tools/College/test_emblem_tiers.py`:

```python
"""Checks for the College emblem recolour. Run from the Unora root: python -m unittest Tools.College.test_emblem_tiers"""
import json
import tempfile
import unittest
from pathlib import Path

from PIL import Image

from Tools.College import emblem_tiers as et


class EmblemTiersTests(unittest.TestCase):
    def test_numbering_follows_subject_then_tier(self):
        self.assertEqual(et.art_number("Art", "Clave"), 259)
        self.assertEqual(et.art_number("Music", "Clave"), 263)
        self.assertEqual(et.art_number("Literature", "Clave"), 267)
        self.assertEqual(et.art_number("History", "Village"), 272)
        self.assertEqual(et.art_number("Philosophy", "Aisling"), 282)

    def test_recolour_keeps_shape_and_uses_the_tier_ramp(self):
        base = Image.new("RGBA", (11, 11), (0, 0, 0, 0))
        base.putpixel((5, 5), (200, 200, 200, 255))
        base.putpixel((6, 5), (40, 40, 40, 255))
        gold = et.recolour(base, "Kingdom")
        self.assertEqual(gold.getpixel((0, 0))[3], 0)
        self.assertEqual(gold.getpixel((5, 5))[3], 255)
        r, g, b, _ = gold.getpixel((5, 5))
        self.assertGreater(r, b)
        self.assertNotEqual(gold.getpixel((6, 5))[:3], (0, 0, 0))

    def test_build_writes_24_pictures_and_templates(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            bases = root / "bases"
            bases.mkdir()
            for subject in et.SUBJECTS:
                Image.new("RGBA", (11, 11), (128, 128, 128, 255)).save(bases / f"base_{subject.lower()}.png")
            et.build(bases, root / "art", root / "emblems")
            self.assertEqual(len(list((root / "art").glob("embl*.png"))), 24)
            data = json.loads((root / "emblems" / "college_history_village.json").read_text(encoding="utf-8"))
            self.assertEqual(data, {
                "key": "college_history_village",
                "art": 272,
                "name": "Village Historian",
                "description": "Village Award in History, Mileth College.",
                "source": {"staff": True},
            })


if __name__ == "__main__":
    unittest.main()
```

Run `python -m unittest Tools.College.test_emblem_tiers`. Expected: `ModuleNotFoundError: emblem_tiers`.

- [ ] **Step 3: Write the recolour tool.** Create `Tools/College/emblem_tiers.py`:

```python
"""Recolours the six College emblem designs into the four award tiers and writes their emblem templates.

Run from the Unora root:
    python Tools/College/emblem_tiers.py
Reads Tools/College/emblems/base_<subject>.png (11x11 greys on transparent), writes
Tools/Emblems/art/custom/embl259.png ... embl282.png and Data/Configuration/Templates/Emblems/college_<subject>_<tier>.json.
Numbering: Art, Music, Literature, History, Lore, Philosophy, each Clave, Village, Kingdom, Aisling, from 259.
Design: Chaos.Client docs/superpowers/specs/2026-10-05-mileth-college-entries-design.md ("Emblems").
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

from PIL import Image

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
FIRST_ART = 259
SUBJECTS = ["Art", "Music", "Literature", "History", "Lore", "Philosophy"]
TIERS = ["Clave", "Village", "Kingdom", "Aisling"]
NOUNS = {"Art": "Artist", "Music": "Musician", "Literature": "Wordsmith", "History": "Historian",
         "Lore": "Lorekeeper", "Philosophy": "Philosopher"}
# dark to light; the darkest stays above pure black, which the client draws as transparent
RAMPS = {
    "Clave": [(52, 30, 16), (110, 62, 30), (160, 98, 50), (205, 140, 82), (238, 190, 140)],
    "Village": [(40, 44, 52), (96, 102, 112), (150, 156, 166), (200, 204, 212), (240, 242, 246)],
    "Kingdom": [(58, 40, 8), (128, 92, 20), (196, 150, 40), (236, 200, 80), (255, 238, 160)],
    "Aisling": [(20, 44, 70), (54, 104, 150), (102, 160, 210), (156, 204, 240), (214, 238, 255)],
}


def art_number(subject: str, tier: str) -> int:
    return FIRST_ART + SUBJECTS.index(subject) * len(TIERS) + TIERS.index(tier)


def recolour(base: Image.Image, tier: str) -> Image.Image:
    ramp = RAMPS[tier]
    out = Image.new("RGBA", base.size, (0, 0, 0, 0))
    for y in range(base.height):
        for x in range(base.width):
            r, g, b, a = base.getpixel((x, y))
            if a == 0:
                continue
            light = (r * 299 + g * 587 + b * 114) / 1000 / 255
            out.putpixel((x, y), (*ramp[min(len(ramp) - 1, int(light * len(ramp)))], a))
    return out


def template(subject: str, tier: str) -> dict:
    return {
        "key": f"college_{subject.lower()}_{tier.lower()}",
        "art": art_number(subject, tier),
        "name": f"{tier} {NOUNS[subject]}",
        "description": f"{tier} Award in {subject}, Mileth College.",
        "source": {"staff": True},
    }


def build(bases: Path, art_dir: Path, emblem_dir: Path) -> None:
    art_dir.mkdir(parents=True, exist_ok=True)
    emblem_dir.mkdir(parents=True, exist_ok=True)
    for subject in SUBJECTS:
        base = Image.open(bases / f"base_{subject.lower()}.png").convert("RGBA")
        if base.size != (11, 11):
            raise ValueError(f"base_{subject.lower()}.png is {base.size}, not 11x11")
        for tier in TIERS:
            recolour(base, tier).save(art_dir / f"embl{art_number(subject, tier)}.png")
            data = template(subject, tier)
            (emblem_dir / f"{data['key']}.json").write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")


def main() -> int:
    build(HERE / "emblems", ROOT / "Tools/Emblems/art/custom", ROOT / "Data/Configuration/Templates/Emblems")
    print("wrote embl259-282 and 24 emblem templates")
    return 0


if __name__ == "__main__":
    sys.exit(main())
```

Check the format of an existing emblem JSON (`bardofunora.json`): two-space indent, the same key order. If the existing files have no trailing newline, drop the `+ "\n"`.

- [ ] **Step 4: Run the tests, then the tool.** `python -m unittest Tools.College.test_emblem_tiers` -> OK. `python Tools/College/emblem_tiers.py`. Then `python -m unittest Tools/Emblems/test_emblems.py` -> OK (it may check that every template's art exists).

- [ ] **Step 5: Build the setoa batch.** Read `Tools/Emblems/README.md` and the "Dats live 2026-10-04" memory note before building: the batch must start from the live setoa.dat set, not an older one. Run `python Tools/Emblems/build.py --no-apply`. Open `Tools/Emblems/review/emblems.png`, check 259-282 show the six designs in four colours, and send it to the user. Record the batch folder path for Task 18.

```json:metadata
{"files": ["Tools/College/emblems/", "Tools/College/emblem_tiers.py", "Tools/College/test_emblem_tiers.py", "Tools/Emblems/art/custom/", "Data/Configuration/Templates/Emblems/"], "verifyCommand": "python -m unittest Tools.College.test_emblem_tiers", "acceptanceCriteria": ["six 11x11 grey bases approved by the user", "24 PNGs 259-282 in subject/tier order and tier colours", "24 templates with key, art, name, description, staff source", "emblem tests pass", "setoa batch built on the live set"], "modelTier": "standard"}
```

---
### Task 12: Client worktree and College networking (client worktree)

**Goal:** Create the client worktree on `feat/college-core` and add the `CollegeDisplay` handler, event and the `CollegeAction` send method.

**Files:**
- Create worktree: `C:\Users\Michael\Documents\GitHub\worktrees\college-client`
- Modify: `Chaos.Client.Networking/Definitions/Delegates.cs`, `Chaos.Client.Networking/ConnectionManager.cs`

**Acceptance Criteria:**
- [ ] The worktree exists on branch `feat/college-core` from client `main`.
- [ ] `ConnectionManager` raises `OnCollegeDisplay` for every `ServerOpCode.CollegeDisplay` packet and exposes `SendCollegeAction`.
- [ ] The client builds against the server worktree, and `CONSTANTS.CLIENT_VERSION` resolves to 772 in the client.

**Verify:** `dotnet build Chaos.Client/Chaos.Client.csproj -p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\college-server` -> Build succeeded.

**Steps:**

- [ ] **Step 1: Create the worktree.** Run from anywhere:

```bash
git -C C:/Users/Michael/Documents/GitHub/Chaos.Client worktree add C:/Users/Michael/Documents/GitHub/worktrees/college-client -b feat/college-core main
```

Expected: `Preparing worktree (new branch 'feat/college-core')`. The `Chaos-Server` submodule folder stays empty in the worktree; builds use `UnoraServerPath` instead. All later client steps run inside this worktree.

- [ ] **Step 2: Add the delegate.** In `Chaos.Client.Networking/Definitions/Delegates.cs`, beside `TownBallotHandler`:

```csharp
public delegate void CollegeDisplayHandler(CollegeDisplayArgs args);
```

- [ ] **Step 3: Add the handler, event and send.** In `ConnectionManager.cs`:
- beside `public event TownBallotHandler? OnTownBallot;` add `public event CollegeDisplayHandler? OnCollegeDisplay;`
- beside `SendTownBallotInteraction` add `public void SendCollegeAction(CollegeActionArgs args) => SendIfWorld(args);`
- in `IndexHandlers()` after the `TowerLeaderboard` line add `PacketHandlers[(byte)ServerOpCode.CollegeDisplay] = HandleCollegeDisplay;`
- beside `HandleTownBallot` add:

```csharp
    private void HandleCollegeDisplay(ServerPacket pkt)
    {
        var args = Client.Deserialize<CollegeDisplayArgs>(in pkt);
        OnCollegeDisplay?.Invoke(args);
    }
```

- [ ] **Step 4: Build.** Run the Verify command. Expected: Build succeeded. Then confirm the version: `grep -n "CLIENT_VERSION = " C:/Users/Michael/Documents/GitHub/worktrees/college-server/Chaos.DarkAges/Definitions/CONSTANTS.cs` shows 772 (the client reads it through `GlobalSettings.ClientVersion`).

```json:metadata
{"files": ["Chaos.Client.Networking/Definitions/Delegates.cs", "Chaos.Client.Networking/ConnectionManager.cs"], "verifyCommand": "dotnet build Chaos.Client/Chaos.Client.csproj -p:UnoraServerPath=C:\\Users\\Michael\\Documents\\GitHub\\worktrees\\college-server", "acceptanceCriteria": ["worktree on feat/college-core", "OnCollegeDisplay raised; SendCollegeAction exists", "client builds against server worktree with version 772"], "modelTier": "mechanical"}
```

---

### Task 13: `WritingDocument` editor model (client worktree)

**Goal:** A pure model of the writing window's blocks with every editing rule: heading toggle, picture insert and remove, backspace merge, Enter after a heading, the character budget, and conversion to and from `CollegePieceInfo`.

**Files:**
- Create: `Chaos.Client/ViewModel/College/WritingDocument.cs`
- Test: `Tests/Chaos.Client.Tests/College/WritingDocumentTests.cs`

**Acceptance Criteria:**
- [ ] A new document has one empty text block; the last block is always text.
- [ ] `ToggleHeading` on a text block turns the caret's line into a heading (cut to 60), splitting the text around it; on a heading it turns it back into text and merges with neighbouring text.
- [ ] `InsertPicture` splits a text block at the caret with the picture between and a text block after; it refuses a sixth picture or a repeated hash.
- [ ] `BackspaceAtStart` removes a picture above the block (merging the texts around it), or merges the block into the one above.
- [ ] `EnterAfterHeading` moves to the next text block, creating one if needed.
- [ ] `SetText` keeps the total at 10,000 characters and headings to one line of 60.
- [ ] `ToInfo` drops empty blocks and trims the title; `From` round-trips a piece.

**Verify:** `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\college-server -- --treenode-filter "/*/*/WritingDocumentTests/*"` -> all pass.

**Steps:**

- [ ] **Step 1: Write the failing tests.** Create `Tests/Chaos.Client.Tests/College/WritingDocumentTests.cs`:

```csharp
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class WritingDocumentTests
{
    private static readonly string HashA = new('a', 64);

    private static WritingDocument WithText(string text)
    {
        var doc = new WritingDocument();
        doc.SetText(0, text);

        return doc;
    }

    private static string Shape(WritingDocument doc)
        => string.Join(" | ", doc.Blocks.Select(b => b.Kind switch
        {
            WritingBlockKind.Heading => "H:" + b.Text,
            WritingBlockKind.Picture => "P",
            _                        => "T:" + b.Text
        }));

    [Test]
    public void A_new_document_has_one_empty_text_block()
        => Shape(new WritingDocument()).Should().Be("T:");

    [Test]
    public void Heading_takes_the_caret_line_and_splits_the_text()
    {
        var doc = WithText("one\ntwo\nthree");

        var (index, caret) = doc.ToggleHeading(0, 5);

        Shape(doc).Should().Be("T:one | H:two | T:three");
        (index, caret).Should().Be((1, 1));
    }

    [Test]
    public void Heading_on_the_only_line_keeps_a_text_block_after_it()
    {
        var doc = WithText("Title line");

        doc.ToggleHeading(0, 0);

        Shape(doc).Should().Be("H:Title line | T:");
    }

    [Test]
    public void Heading_is_cut_to_sixty_and_the_rest_moves_down()
    {
        var doc = WithText(new string('h', 70));

        doc.ToggleHeading(0, 0);

        Shape(doc).Should().Be($"H:{new string('h', 60)} | T:{new string('h', 10)}");
    }

    [Test]
    public void Toggling_a_heading_back_merges_with_its_neighbours()
    {
        var doc = WithText("one\ntwo\nthree");
        doc.ToggleHeading(0, 5);

        var (index, caret) = doc.ToggleHeading(1, 2);

        Shape(doc).Should().Be("T:one\ntwo\nthree");
        (index, caret).Should().Be((0, 6));
    }

    [Test]
    public void A_picture_splits_text_at_the_caret()
    {
        var doc = WithText("before\nafter");

        var (index, caret) = doc.InsertPicture(0, 7, HashA)!.Value;

        Shape(doc).Should().Be("T:before | P | T:after");
        (index, caret).Should().Be((2, 0));
        doc.PictureCount.Should().Be(1);
    }

    [Test]
    public void A_sixth_or_repeated_picture_is_refused()
    {
        var doc = new WritingDocument();

        for (var i = 0; i < 5; i++)
            doc.InsertPicture(doc.Blocks.Count - 1, 0, new string((char)('a' + i), 64)).Should().NotBeNull();

        doc.InsertPicture(doc.Blocks.Count - 1, 0, new string('f', 64)).Should().BeNull();

        var other = new WritingDocument();
        other.InsertPicture(0, 0, HashA);
        other.InsertPicture(other.Blocks.Count - 1, 0, HashA).Should().BeNull();
    }

    [Test]
    public void Backspace_after_a_picture_removes_it_and_joins_the_text()
    {
        var doc = WithText("before\nafter");
        doc.InsertPicture(0, 7, HashA);

        var (index, caret) = doc.BackspaceAtStart(2);

        Shape(doc).Should().Be("T:before\nafter");
        (index, caret).Should().Be((0, 6));
    }

    [Test]
    public void Backspace_merges_text_into_a_heading_above()
    {
        var doc = WithText("Title\nbody line\nmore");
        doc.ToggleHeading(0, 0);

        var (index, caret) = doc.BackspaceAtStart(1);

        Shape(doc).Should().Be("H:Titlebody line | T:more");
        (index, caret).Should().Be((0, 5));
    }

    [Test]
    public void Backspace_in_the_first_block_does_nothing()
    {
        var doc = WithText("abc");

        doc.BackspaceAtStart(0).Should().Be((0, 0));
        Shape(doc).Should().Be("T:abc");
    }

    [Test]
    public void Enter_after_a_heading_goes_to_the_text_below()
    {
        var doc = WithText("Title\nbody");
        doc.ToggleHeading(0, 0);

        doc.EnterAfterHeading(0).Should().Be((1, 0));

        var lone = new WritingDocument();
        lone.SetText(0, "x");
        lone.InsertPicture(0, 1, HashA);
        lone.ToggleHeading(0, 0);
        var (index, _) = lone.EnterAfterHeading(0);
        lone.Blocks[index].Kind.Should().Be(WritingBlockKind.Text);
    }

    [Test]
    public void Set_text_keeps_the_budget_and_one_line_headings()
    {
        var doc = WithText(new string('a', 9_990));
        doc.InsertPicture(0, 9_990, HashA);

        doc.SetText(2, new string('b', 50));

        doc.Blocks[2].Text.Should().HaveLength(10);
        doc.TextLength.Should().Be(10_000);

        var heading = WithText("x");
        heading.ToggleHeading(0, 0);
        heading.SetText(0, "a\nb" + new string('c', 80));
        heading.Blocks[0].Text.Should().HaveLength(60).And.StartWith("a b");
    }

    [Test]
    public void To_info_drops_empty_blocks_and_from_round_trips()
    {
        var doc = WithText("Title\nbody");
        doc.Title = "  My Piece  ";
        doc.ToggleHeading(0, 0);
        doc.InsertPicture(1, 4, HashA);

        var info = doc.ToInfo(CollegeSubjectCode.Lore);

        info.Title.Should().Be("My Piece");
        info.Blocks.Select(b => b.Kind).Should().Equal(CollegeBlockKind.Heading, CollegeBlockKind.Text, CollegeBlockKind.Picture);

        var back = WritingDocument.From(info);
        Shape(back).Should().Be("H:Title | T:body | P | T:");
        back.IsDirty.Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run to see them fail.** Run the Verify command. Expected: build error, `WritingDocument` does not exist.

- [ ] **Step 3: Write the model.** Create `Chaos.Client/ViewModel/College/WritingDocument.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;

namespace Chaos.Client.ViewModel.College;

public enum WritingBlockKind
{
    Text,
    Heading,
    Picture
}

public sealed class WritingBlock
{
    public WritingBlockKind Kind { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;

    public static WritingBlock OfText(string text) => new() { Kind = WritingBlockKind.Text, Text = text };
}

/// <summary>
///     The writing window's piece as editable blocks: text (with line breaks), one-line headings and pictures. Every editing
///     rule lives here; the window only shows blocks and forwards keys. The last block is always text, so there is always
///     somewhere to type.
/// </summary>
public sealed class WritingDocument
{
    public string Title
    {
        get;
        set
        {
            field = value.Length <= CollegeProtocol.MAX_TITLE_CHARS ? value : value[..CollegeProtocol.MAX_TITLE_CHARS];
            IsDirty = true;
        }
    } = string.Empty;

    public List<WritingBlock> Blocks { get; } = [WritingBlock.OfText(string.Empty)];
    public bool IsDirty { get; private set; }
    public int TextLength => Blocks.Where(b => b.Kind != WritingBlockKind.Picture).Sum(b => b.Text.Length);
    public int PictureCount => Blocks.Count(b => b.Kind == WritingBlockKind.Picture);

    public void MarkClean() => IsDirty = false;

    public static WritingDocument From(CollegePieceInfo? piece)
    {
        var doc = new WritingDocument();

        if (piece is null)
            return doc;

        doc.Title = piece.Title;
        doc.Blocks.Clear();

        foreach (var block in piece.Blocks)
            doc.Blocks.Add(
                new WritingBlock
                {
                    Kind = block.Kind switch
                    {
                        CollegeBlockKind.Heading => WritingBlockKind.Heading,
                        CollegeBlockKind.Picture => WritingBlockKind.Picture,
                        _                        => WritingBlockKind.Text
                    },
                    Text = block.Text,
                    Hash = block.Hash
                });

        doc.EnsureTrailingText();
        doc.MarkClean();

        return doc;
    }

    public CollegePieceInfo ToInfo(CollegeSubjectCode subject)
        => new()
        {
            Subject = subject,
            Title = Title.Trim(),
            Blocks = Blocks.Where(b => (b.Kind == WritingBlockKind.Picture) || (b.Text.Trim().Length > 0))
                           .Select(
                               b => new CollegeBlockInfo
                               {
                                   Kind = b.Kind switch
                                   {
                                       WritingBlockKind.Heading => CollegeBlockKind.Heading,
                                       WritingBlockKind.Picture => CollegeBlockKind.Picture,
                                       _                        => CollegeBlockKind.Text
                                   },
                                   Text = b.Kind == WritingBlockKind.Picture ? string.Empty : b.Text,
                                   Hash = b.Hash
                               })
                           .ToList()
        };

    /// <summary>Sets a block's text, keeping headings to one line of 60 and the whole piece within 10,000 characters.</summary>
    public void SetText(int index, string text)
    {
        var block = Blocks[index];

        if (block.Kind == WritingBlockKind.Picture)
            return;

        if (block.Kind == WritingBlockKind.Heading)
            text = text.Replace("\r", string.Empty).Replace('\n', ' ');

        var limit = block.Kind == WritingBlockKind.Heading ? CollegeProtocol.MAX_HEADING_CHARS : int.MaxValue;
        var budget = CollegeProtocol.MAX_TEXT_CHARS - (TextLength - block.Text.Length);
        var max = Math.Max(0, Math.Min(limit, budget));
        var next = text.Length <= max ? text : text[..max];

        if (next == block.Text)
            return;

        block.Text = next;
        IsDirty = true;
    }

    public (int Index, int Caret) ToggleHeading(int index, int caret)
    {
        var block = Blocks[index];
        IsDirty = true;

        if (block.Kind == WritingBlockKind.Picture)
            return (index, 0);

        if (block.Kind == WritingBlockKind.Heading)
        {
            block.Kind = WritingBlockKind.Text;

            return MergeTextAround(index, caret);
        }

        var text = block.Text;
        caret = Math.Clamp(caret, 0, text.Length);
        var lineStart = caret == 0 ? 0 : text.LastIndexOf('\n', caret - 1) + 1;
        var lineEnd = text.IndexOf('\n', caret);

        if (lineEnd < 0)
            lineEnd = text.Length;

        var before = lineStart > 0 ? text[..(lineStart - 1)] : null;
        var line = text[lineStart..lineEnd];
        var after = lineEnd < text.Length ? text[(lineEnd + 1)..] : null;

        if (line.Length > CollegeProtocol.MAX_HEADING_CHARS)
        {
            after = line[CollegeProtocol.MAX_HEADING_CHARS..] + (after is null ? string.Empty : "\n" + after);
            line = line[..CollegeProtocol.MAX_HEADING_CHARS];
        }

        var replacement = new List<WritingBlock>();

        if (before is not null)
            replacement.Add(WritingBlock.OfText(before));

        var headingIndex = index + replacement.Count;
        replacement.Add(new WritingBlock { Kind = WritingBlockKind.Heading, Text = line });

        if (after is not null)
            replacement.Add(WritingBlock.OfText(after));

        Blocks.RemoveAt(index);
        Blocks.InsertRange(index, replacement);
        EnsureTrailingText();

        return (headingIndex, Math.Min(caret - lineStart, line.Length));
    }

    /// <summary>Puts a picture at the caret. Null when the piece already has 5 pictures or this one.</summary>
    public (int Index, int Caret)? InsertPicture(int index, int caret, string hash)
    {
        if ((PictureCount >= CollegeProtocol.MAX_PICTURES) || Blocks.Any(b => (b.Kind == WritingBlockKind.Picture) && (b.Hash == hash)))
            return null;

        IsDirty = true;
        var picture = new WritingBlock { Kind = WritingBlockKind.Picture, Hash = hash };
        var block = Blocks[index];

        if (block.Kind != WritingBlockKind.Text)
        {
            Blocks.Insert(index + 1, picture);

            if ((index + 2 >= Blocks.Count) || (Blocks[index + 2].Kind != WritingBlockKind.Text))
                Blocks.Insert(index + 2, WritingBlock.OfText(string.Empty));

            return (index + 2, 0);
        }

        caret = Math.Clamp(caret, 0, block.Text.Length);
        var before = block.Text[..caret];
        var after = block.Text[caret..];

        if (before.EndsWith('\n'))
            before = before[..^1];

        if (after.StartsWith('\n'))
            after = after[1..];

        block.Text = before;
        Blocks.Insert(index + 1, picture);
        Blocks.Insert(index + 2, WritingBlock.OfText(after));

        return (index + 2, 0);
    }

    /// <summary>Backspace with the caret at a block's start: removes a picture above it, or joins the block to the one above.</summary>
    public (int Index, int Caret) BackspaceAtStart(int index)
    {
        if (index <= 0)
            return (0, 0);

        IsDirty = true;
        var previous = Blocks[index - 1];
        var current = Blocks[index];

        if (previous.Kind == WritingBlockKind.Picture)
        {
            Blocks.RemoveAt(index - 1);

            if ((index - 2 >= 0) && (Blocks[index - 2].Kind == WritingBlockKind.Text) && (current.Kind == WritingBlockKind.Text))
            {
                var above = Blocks[index - 2];
                var caret = above.Text.Length;
                above.Text = JoinParagraphs(above.Text, current.Text);
                Blocks.RemoveAt(index - 1);
                EnsureTrailingText();

                return (index - 2, caret);
            }

            EnsureTrailingText();

            return (index - 1, 0);
        }

        if (current.Kind == WritingBlockKind.Picture)
            return (index, 0);

        var at = previous.Text.Length;

        if (previous.Kind == WritingBlockKind.Heading)
        {
            var newline = current.Text.IndexOf('\n');
            var firstLine = newline < 0 ? current.Text : current.Text[..newline];
            var rest = newline < 0 ? string.Empty : current.Text[(newline + 1)..];
            var room = CollegeProtocol.MAX_HEADING_CHARS - previous.Text.Length;
            var moved = firstLine.Length <= room ? firstLine : firstLine[..Math.Max(0, room)];
            previous.Text += moved;
            current.Text = firstLine[moved.Length..] + (rest.Length > 0 && firstLine.Length > moved.Length ? "\n" : string.Empty) + rest;

            if ((current.Text.Length == 0) && (current.Kind == WritingBlockKind.Text) && (index != Blocks.Count - 1))
                Blocks.RemoveAt(index);
        } else
        {
            previous.Text += current.Text;
            Blocks.RemoveAt(index);
        }

        EnsureTrailingText();

        return (index - 1, at);
    }

    /// <summary>Enter at the end of a heading: the caret moves to the text below it, which is created if missing.</summary>
    public (int Index, int Caret) EnterAfterHeading(int index)
    {
        if ((index + 1 < Blocks.Count) && (Blocks[index + 1].Kind == WritingBlockKind.Text))
            return (index + 1, 0);

        Blocks.Insert(index + 1, WritingBlock.OfText(string.Empty));
        IsDirty = true;

        return (index + 1, 0);
    }

    /// <summary>Removes a picture by its remove button, joining the text around it.</summary>
    public (int Index, int Caret) RemovePicture(int index)
        => (index + 1 < Blocks.Count) && (Blocks[index].Kind == WritingBlockKind.Picture) ? BackspaceAtStart(index + 1) : (index, 0);

    private (int Index, int Caret) MergeTextAround(int index, int caret)
    {
        if ((index > 0) && (Blocks[index - 1].Kind == WritingBlockKind.Text))
        {
            var previous = Blocks[index - 1];
            caret += previous.Text.Length + (previous.Text.Length > 0 ? 1 : 0);
            previous.Text = JoinParagraphs(previous.Text, Blocks[index].Text);
            Blocks.RemoveAt(index);
            index--;
        }

        if ((index + 1 < Blocks.Count) && (Blocks[index + 1].Kind == WritingBlockKind.Text))
        {
            Blocks[index].Text = JoinParagraphs(Blocks[index].Text, Blocks[index + 1].Text);
            Blocks.RemoveAt(index + 1);
        }

        EnsureTrailingText();

        return (index, caret);
    }

    private static string JoinParagraphs(string first, string second)
        => first.Length == 0 ? second : second.Length == 0 ? first : first + "\n" + second;

    private void EnsureTrailingText()
    {
        if ((Blocks.Count == 0) || (Blocks[^1].Kind != WritingBlockKind.Text))
            Blocks.Add(WritingBlock.OfText(string.Empty));
    }
}
```

- [ ] **Step 4: Run the tests.** Run the Verify command. Expected: 13 passed. If a test and the model disagree, the test states the intended behaviour from the spec; fix the model, not the test, unless the test contradicts the spec.

```json:metadata
{"files": ["Chaos.Client/ViewModel/College/WritingDocument.cs", "Tests/Chaos.Client.Tests/College/WritingDocumentTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\\Users\\Michael\\Documents\\GitHub\\worktrees\\college-server -- --treenode-filter \"/*/*/WritingDocumentTests/*\"", "acceptanceCriteria": ["one empty text block; last block always text", "ToggleHeading split/merge", "InsertPicture split; refuses sixth/repeat", "BackspaceAtStart removes picture or merges", "EnterAfterHeading", "SetText budget and heading line", "ToInfo/From round trip"], "modelTier": "standard"}
```

---

### Task 14: Picture preparation, cache, transfers and file picker (client worktree)

**Goal:** Shrink and encode chosen pictures, hash them, keep downloaded pictures on disk with a 50 MB cap, track uploads and downloads by hash, and open the Windows file picker without freezing the game.

**Files:**
- Create: `Chaos.Client/Systems/College/PicturePrep.cs`, `Chaos.Client/Systems/College/CollegePictureCache.cs`, `Chaos.Client/Systems/College/PictureAssembly.cs`, `Chaos.Client/Systems/College/CollegePictureTransfers.cs`, `Chaos.Client/Systems/College/PictureFilePicker.cs`
- Test: `Tests/Chaos.Client.Tests/College/PicturePrepTests.cs`, `Tests/Chaos.Client.Tests/College/CollegePictureCacheTests.cs`, `Tests/Chaos.Client.Tests/College/PictureAssemblyTests.cs`

**Acceptance Criteria:**
- [ ] `PicturePrep.FitSize` keeps pictures within 400 x 300 and keeps their shape; small pictures stay as they are.
- [ ] `Prepare` returns PNG for a picture with any transparent pixel and JPEG (quality 85, stepping down by 5 to 60) otherwise, always at most 204,800 bytes, with the SHA-256 hash in lower-case hex; it refuses non-pictures and files over 20 MB with a player-facing message.
- [ ] `CollegePictureCache` stores and reads pictures by hash and deletes the oldest files once the folder passes 50 MB.
- [ ] `PictureAssembly` returns the bytes only when every part has arrived, and refuses a part with a wrong count or index.
- [ ] `PictureFilePicker.PickAsync` shows the Windows open dialog (PNG/JPG filter) on its own STA thread and returns the path or null.

**Verify:** `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\college-server -- --treenode-filter "/*/*/Picture*Tests/*"` and `--treenode-filter "/*/*/CollegePictureCacheTests/*"` -> all pass.

**Steps:**

- [ ] **Step 1: Write the failing tests.** Create `Tests/Chaos.Client.Tests/College/PicturePrepTests.cs`:

```csharp
using Chaos.Client.Systems.College;
using Chaos.DarkAges.Definitions;
using FluentAssertions;
using SkiaSharp;

namespace Chaos.Client.Tests.College;

public class PicturePrepTests
{
    private static byte[] Encode(int width, int height, bool transparent, SKEncodedImageFormat format = SKEncodedImageFormat.Png)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        var random = new Random(7);

        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                bitmap.SetPixel(x, y, new SKColor((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256), transparent && (x == 0) ? (byte)0 : (byte)255));

        using var image = SKImage.FromBitmap(bitmap);

        return image.Encode(format, 95).ToArray();
    }

    [Test]
    public void Fit_size_keeps_shape_within_the_box()
    {
        PicturePrep.FitSize(800, 600).Should().Be((400, 300));
        PicturePrep.FitSize(1000, 200).Should().Be((400, 80));
        PicturePrep.FitSize(300, 900).Should().Be((100, 300));
        PicturePrep.FitSize(120, 90).Should().Be((120, 90));
    }

    [Test]
    public void An_opaque_photo_becomes_a_small_jpeg()
    {
        var picture = PicturePrep.Prepare(Encode(1200, 900, false, SKEncodedImageFormat.Jpeg), out var error);

        error.Should().BeEmpty();
        picture!.Width.Should().Be(400);
        picture.Height.Should().Be(300);
        picture.Bytes.Length.Should().BeLessThanOrEqualTo(CollegeProtocol.MAX_PICTURE_BYTES);
        picture.Bytes[..2].Should().Equal(0xFF, 0xD8);
        picture.Hash.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Test]
    public void A_picture_with_transparency_stays_png()
    {
        var picture = PicturePrep.Prepare(Encode(64, 64, true), out _);

        picture!.Bytes[..4].Should().Equal(0x89, 0x50, 0x4E, 0x47);
        (picture.Width, picture.Height).Should().Be((64, 64));
    }

    [Test]
    public void Other_files_are_refused_with_a_message()
    {
        PicturePrep.Prepare([1, 2, 3, 4], out var error).Should().BeNull();
        error.Should().Be(PicturePrep.NOT_A_PICTURE);
    }
}
```

Create `Tests/Chaos.Client.Tests/College/CollegePictureCacheTests.cs`:

```csharp
using Chaos.Client.Systems.College;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class CollegePictureCacheTests
{
    private static CollegePictureCache Create(long cap)
        => new(Path.Combine(Path.GetTempPath(), "college-cache-" + Guid.NewGuid().ToString("N")), cap);

    [Test]
    public void Pictures_are_stored_and_read_by_hash()
    {
        var cache = Create(1_000);
        var hash = new string('a', 64);

        cache.Write(hash, [1, 2, 3]);

        cache.TryRead(hash).Should().Equal(1, 2, 3);
        cache.TryRead(new string('b', 64)).Should().BeNull();
        cache.TryRead("../x").Should().BeNull();
    }

    [Test]
    public void The_oldest_pictures_go_past_the_cap()
    {
        var cache = Create(250);

        for (var i = 0; i < 3; i++)
        {
            cache.Write(new string((char)('a' + i), 64), new byte[100]);
            Thread.Sleep(20);
        }

        cache.TryRead(new string('a', 64)).Should().BeNull();
        cache.TryRead(new string('b', 64)).Should().NotBeNull();
        cache.TryRead(new string('c', 64)).Should().NotBeNull();
    }
}
```

Create `Tests/Chaos.Client.Tests/College/PictureAssemblyTests.cs`:

```csharp
using Chaos.Client.Systems.College;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class PictureAssemblyTests
{
    [Test]
    public void Bytes_come_back_only_when_every_part_is_in()
    {
        var assembly = new PictureAssembly(3);

        assembly.Add(2, 3, [5]).Should().BeNull();
        assembly.Add(0, 3, [1, 2]).Should().BeNull();
        assembly.Add(1, 3, [3, 4])!.Should().Equal(1, 2, 3, 4, 5);
    }

    [Test]
    public void A_mismatched_part_is_refused()
    {
        var assembly = new PictureAssembly(2);

        assembly.Add(0, 3, [1]).Should().BeNull();
        assembly.Failed.Should().BeTrue();
        new PictureAssembly(2).Add(5, 2, [1]).Should().BeNull();
    }
}
```

- [ ] **Step 2: Run to see them fail.** Expected: build errors for the missing classes.

- [ ] **Step 3: Write `PicturePrep`.** Create `Chaos.Client/Systems/College/PicturePrep.cs`:

```csharp
using System.Security.Cryptography;
using Chaos.DarkAges.Definitions;
using SkiaSharp;

namespace Chaos.Client.Systems.College;

public sealed record PreparedPicture(byte[] Bytes, string Hash, int Width, int Height);

/// <summary>Turns a picture file into what the College accepts: at most 400 x 300 and 200 KB, PNG if it has transparency, JPEG otherwise.</summary>
public static class PicturePrep
{
    public const string NOT_A_PICTURE = "That file isn't a PNG or JPG picture.";
    public const string TOO_LARGE = "That picture is too large.";
    public const string FILE_TOO_BIG = "That file is too big to open.";
    private const int MAX_FILE_BYTES = 20 * 1024 * 1024;

    public static (int Width, int Height) FitSize(int width, int height)
    {
        if ((width <= CollegeProtocol.MAX_PICTURE_WIDTH) && (height <= CollegeProtocol.MAX_PICTURE_HEIGHT))
            return (width, height);

        var scale = Math.Min((double)CollegeProtocol.MAX_PICTURE_WIDTH / width, (double)CollegeProtocol.MAX_PICTURE_HEIGHT / height);

        return (Math.Max(1, (int)Math.Floor(width * scale)), Math.Max(1, (int)Math.Floor(height * scale)));
    }

    public static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    public static PreparedPicture? Prepare(byte[] file, out string error)
    {
        error = string.Empty;

        if (file.Length > MAX_FILE_BYTES)
        {
            error = FILE_TOO_BIG;

            return null;
        }

        using var source = SKBitmap.Decode(file);

        if (source is null)
        {
            error = NOT_A_PICTURE;

            return null;
        }

        var (width, height) = FitSize(source.Width, source.Height);
        using var sized = (width == source.Width) && (height == source.Height)
            ? source.Copy()
            : source.Resize(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul), new SKSamplingOptions(SKCubicResampler.Mitchell));
        using var image = SKImage.FromBitmap(sized);

        byte[]? bytes;

        if (HasTransparency(sized))
            bytes = image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
        else
        {
            bytes = null;

            for (var quality = 85; quality >= 60; quality -= 5)
            {
                bytes = image.Encode(SKEncodedImageFormat.Jpeg, quality).ToArray();

                if (bytes.Length <= CollegeProtocol.MAX_PICTURE_BYTES)
                    break;
            }
        }

        if (bytes is null || (bytes.Length > CollegeProtocol.MAX_PICTURE_BYTES))
        {
            error = TOO_LARGE;

            return null;
        }

        return new PreparedPicture(bytes, Hash(bytes), width, height);
    }

    private static bool HasTransparency(SKBitmap bitmap)
    {
        foreach (var pixel in bitmap.Pixels)
            if (pixel.Alpha < 255)
                return true;

        return false;
    }
}
```

- [ ] **Step 4: Write the cache and the assembly.** Create `Chaos.Client/Systems/College/CollegePictureCache.cs`:

```csharp
namespace Chaos.Client.Systems.College;

/// <summary>College pictures on disk, named by hash, in a folder capped at a size; the least recently used go first.</summary>
public sealed class CollegePictureCache
{
    public const long DEFAULT_CAP = 50L * 1024 * 1024;

    private readonly long Cap;
    private readonly string Directory;

    public CollegePictureCache()
        : this(Path.Combine(GlobalSettings.DataPath, "CollegePictures"), DEFAULT_CAP) { }

    public CollegePictureCache(string directory, long cap)
    {
        Directory = directory;
        Cap = cap;
        System.IO.Directory.CreateDirectory(directory);
    }

    private static bool IsHash(string hash) => (hash.Length == 64) && hash.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    private string PathOf(string hash) => Path.Combine(Directory, hash + ".pic");

    public byte[]? TryRead(string hash)
    {
        if (!IsHash(hash) || !File.Exists(PathOf(hash)))
            return null;

        File.SetLastWriteTimeUtc(PathOf(hash), DateTime.UtcNow);

        return File.ReadAllBytes(PathOf(hash));
    }

    public void Write(string hash, byte[] bytes)
    {
        if (!IsHash(hash))
            return;

        File.WriteAllBytes(PathOf(hash), bytes);
        Trim();
    }

    private void Trim()
    {
        var files = new DirectoryInfo(Directory).GetFiles("*.pic").OrderBy(f => f.LastWriteTimeUtc).ToList();
        var total = files.Sum(f => f.Length);

        foreach (var file in files)
        {
            if (total <= Cap)
                break;

            total -= file.Length;
            file.Delete();
        }
    }
}
```

Create `Chaos.Client/Systems/College/PictureAssembly.cs`:

```csharp
using Chaos.DarkAges.Definitions;

namespace Chaos.Client.Systems.College;

/// <summary>Puts one downloaded picture back together from its parts.</summary>
public sealed class PictureAssembly(int count)
{
    private readonly byte[]?[] Parts = new byte[]?[count];

    public bool Failed { get; private set; }

    /// <summary>The whole picture once the last part arrives; null before that or after a bad part.</summary>
    public byte[]? Add(int index, int partCount, byte[] data)
    {
        if (Failed || (partCount != Parts.Length) || (index < 0) || (index >= Parts.Length) || (data.Length > CollegeProtocol.PART_SIZE))
        {
            Failed = true;

            return null;
        }

        Parts[index] = data;

        return Parts.Any(p => p is null) ? null : Parts.SelectMany(p => p!).ToArray();
    }
}
```

- [ ] **Step 5: Write the file picker.** Create `Chaos.Client/Systems/College/PictureFilePicker.cs`:

```csharp
using System.Runtime.InteropServices;

namespace Chaos.Client.Systems.College;

/// <summary>
///     The Windows open-file dialog for pictures. It runs on its own STA thread, because the dialog is modal and the game
///     loop must keep answering the server while the player browses.
/// </summary>
public static class PictureFilePicker
{
    private const int OFN_NOCHANGEDIR = 0x00000008;
    private const int OFN_PATHMUSTEXIST = 0x00000800;
    private const int OFN_FILEMUSTEXIST = 0x00001000;
    private const int OFN_EXPLORER = 0x00080000;
    private const int MAX_PATH_CHARS = 1024;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenFileName
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public string lpstrFilter;
        public string? lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public IntPtr lpstrFile;
        public int nMaxFile;
        public string? lpstrFileTitle;
        public int nMaxFileTitle;
        public string? lpstrInitialDir;
        public string? lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public string? lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public string? lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetOpenFileNameW(ref OpenFileName ofn);

    public static Task<string?> PickAsync()
    {
        if (!OperatingSystem.IsWindows())
            return Task.FromResult<string?>(null);

        var result = new TaskCompletionSource<string?>();

        var thread = new Thread(
            () =>
            {
                try
                {
                    result.SetResult(Show());
                } catch (Exception e)
                {
                    result.SetException(e);
                }
            })
        {
            IsBackground = true
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return result.Task;
    }

    private static string? Show()
    {
        var buffer = Marshal.AllocHGlobal(MAX_PATH_CHARS * 2);

        try
        {
            Marshal.WriteInt16(buffer, 0);

            var dialog = new OpenFileName
            {
                lStructSize = Marshal.SizeOf<OpenFileName>(),
                lpstrFilter = "Pictures (*.png, *.jpg)\0*.png;*.jpg;*.jpeg\0\0",
                lpstrFile = buffer,
                nMaxFile = MAX_PATH_CHARS,
                lpstrTitle = "Insert picture",
                Flags = OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR
            };

            return GetOpenFileNameW(ref dialog) ? Marshal.PtrToStringUni(buffer) : null;
        } finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
```

`OFN_NOCHANGEDIR` matters: without it the dialog changes the process's working directory, which the client may rely on.

- [ ] **Step 6: Write the transfers.** Create `Chaos.Client/Systems/College/CollegePictureTransfers.cs`. It owns the textures and talks to the server through a send callback, so the controls only ask "do you have hash X?":

```csharp
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Systems.College;

/// <summary>
///     College pictures by hash: uploads (check, then parts), downloads (fetch, then parts, then the disk cache) and the
///     textures made from them. Used on the game thread only.
/// </summary>
public sealed class CollegePictureTransfers(Action<CollegeActionArgs> send, CollegePictureCache cache)
{
    private readonly Dictionary<string, PictureAssembly> Downloads = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Texture2D> Textures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, byte[]> Uploads = new(StringComparer.Ordinal);

    /// <summary>A picture's texture is ready.</summary>
    public event Action<string>? PictureReady;

    /// <summary>An upload finished: the hash, whether it worked, and the server's message.</summary>
    public event Action<string, bool, string>? UploadFinished;

    public bool TryGetTexture(string hash, out Texture2D texture)
    {
        if (Textures.TryGetValue(hash, out texture!))
            return true;

        if (cache.TryRead(hash) is { } bytes && MakeTexture(hash, bytes))
            return Textures.TryGetValue(hash, out texture!);

        if (!Downloads.ContainsKey(hash))
        {
            Downloads[hash] = new PictureAssembly(1);
            send(new CollegeActionArgs { Type = CollegeActionType.PictureFetch, Hash = hash });
        }

        texture = null!;

        return false;
    }

    public void Upload(PreparedPicture picture)
    {
        cache.Write(picture.Hash, picture.Bytes);
        MakeTexture(picture.Hash, picture.Bytes);
        Uploads[picture.Hash] = picture.Bytes;
        send(new CollegeActionArgs { Type = CollegeActionType.PictureCheck, Hash = picture.Hash, Length = (uint)picture.Bytes.Length });
    }

    public void OnReply(CollegeDisplayArgs args)
    {
        switch (args.Reply)
        {
            case CollegePictureReply.Send when Uploads.TryGetValue(args.Hash, out var bytes):
                var parts = bytes.Chunk(CollegeProtocol.PART_SIZE).ToList();

                for (var i = 0; i < parts.Count; i++)
                    send(
                        new CollegeActionArgs
                        {
                            Type = CollegeActionType.PicturePart,
                            Hash = args.Hash,
                            PartIndex = (byte)i,
                            PartCount = (byte)parts.Count,
                            Data = parts[i]
                        });

                break;
            case CollegePictureReply.Have:
            case CollegePictureReply.Accepted:
                Uploads.Remove(args.Hash);
                UploadFinished?.Invoke(args.Hash, true, args.Message);

                break;
            case CollegePictureReply.Refused:
                Uploads.Remove(args.Hash);
                UploadFinished?.Invoke(args.Hash, false, args.Message);

                break;
        }
    }

    public void OnPart(CollegeDisplayArgs args)
    {
        if (!Downloads.TryGetValue(args.Hash, out var assembly))
            return;

        //the first part tells how many parts there are
        if (args.PartIndex == 0 || assembly.Failed)
            Downloads[args.Hash] = assembly = new PictureAssembly(args.PartCount);

        if (assembly.Add(args.PartIndex, args.PartCount, args.Data) is not { } bytes)
            return;

        Downloads.Remove(args.Hash);

        if (PicturePrep.Hash(bytes) != args.Hash)
            return;

        cache.Write(args.Hash, bytes);

        if (MakeTexture(args.Hash, bytes))
            PictureReady?.Invoke(args.Hash);
    }

    public void Clear()
    {
        foreach (var texture in Textures.Values)
            texture.Dispose();

        Textures.Clear();
        Downloads.Clear();
        Uploads.Clear();
    }

    private bool MakeTexture(string hash, byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes);
            Textures[hash] = Texture2D.FromStream(ChaosGame.Device, stream);

            return true;
        } catch (InvalidOperationException)
        {
            return false;
        }
    }
}
```

The server sends parts in order, so resetting the assembly on part 0 is safe. If `Texture2D.FromStream` throws a different exception type for bad data in this MonoGame version, catch that type instead.

- [ ] **Step 7: Run the tests.** Run the two Verify commands. Expected: 4 + 2 + 2 passed. Build the client to check `PictureFilePicker` and `CollegePictureTransfers` compile.

```json:metadata
{"files": ["Chaos.Client/Systems/College/PicturePrep.cs", "Chaos.Client/Systems/College/CollegePictureCache.cs", "Chaos.Client/Systems/College/PictureAssembly.cs", "Chaos.Client/Systems/College/CollegePictureTransfers.cs", "Chaos.Client/Systems/College/PictureFilePicker.cs", "Tests/Chaos.Client.Tests/College/PicturePrepTests.cs", "Tests/Chaos.Client.Tests/College/CollegePictureCacheTests.cs", "Tests/Chaos.Client.Tests/College/PictureAssemblyTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\\Users\\Michael\\Documents\\GitHub\\worktrees\\college-server -- --treenode-filter \"/*/*/PicturePrepTests/*\"", "acceptanceCriteria": ["FitSize within 400x300 keeping shape", "PNG if transparent else JPEG 85->60, <= 204800 bytes, lower-case SHA-256", "cache by hash with 50 MB cap", "assembly completes only with all parts", "file picker on STA thread"], "modelTier": "standard"}
```

---

### Task 15: Shared piece view and the reader window (client worktree)

**Goal:** One scrolling `PieceView` that shows a piece (headings, wrapped text, pictures or a loading line) and the `CollegeReaderControl` window with its five footers.

**Files:**
- Create: `Chaos.Client/Controls/World/Popups/College/PieceView.cs`, `Chaos.Client/Controls/World/Popups/College/CollegeReaderControl.cs`, `Chaos.Client/Controls/World/Popups/College/CollegeTiers.cs`

**Acceptance Criteria:**
- [ ] `PieceView.Show(piece)` lays out blocks top to bottom: headings in gold, text word-wrapped in white (line breaks kept), pictures centred at their real size, or "Loading picture..." until the texture arrives; it relayouts when `CollegePictureTransfers.PictureReady` fires for one of its hashes; the mouse wheel scrolls.
- [ ] The reader's title bar shows the piece title and the server's header line; the footer matches the context: Judge (tier buttons, comment box of 300, Save vote, Prev, Next), Verdict (vote summary, Votes and comments, tier buttons, note box of 500, Post verdict, Remove entry), Gallery (author and award, plus Remove for moderators), HandIn (student and Show to class), Shown and Own (header only).
- [ ] Each button sends the matching `CollegeActionArgs` through `ActionRequested`.
- [ ] The window is at most 560 x 440 and fits the 640 x 480 screen.

**Verify:** The client builds; in Task 18's in-game check the reader shows a two-picture piece correctly in each context.

**Steps:**

- [ ] **Step 1: Tier helpers.** Create `Chaos.Client/Controls/World/Popups/College/CollegeTiers.cs`:

```csharp
using Chaos.Client.Rendering;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Controls.World.Popups.College;

public static class CollegeTiers
{
    public static readonly CollegeTierCode[] Votable =
        [CollegeTierCode.None, CollegeTierCode.Clave, CollegeTierCode.Village, CollegeTierCode.Kingdom, CollegeTierCode.Aisling];

    public static string Name(CollegeTierCode tier) => tier == CollegeTierCode.None ? "No award" : tier.ToString();

    public static string Name(byte tier) => tier == CollegeProtocol.NO_TIER ? "-" : Name((CollegeTierCode)tier);

    public static Color Badge(CollegeTierCode tier)
        => tier switch
        {
            CollegeTierCode.Clave   => new Color(205, 140, 82),
            CollegeTierCode.Village => new Color(200, 204, 212),
            CollegeTierCode.Kingdom => new Color(236, 200, 80),
            CollegeTierCode.Aisling => new Color(156, 204, 240),
            _                       => LegendColors.Gray
        };
}
```

- [ ] **Step 2: The piece view.** Create `Chaos.Client/Controls/World/Popups/College/PieceView.cs`. It is a `UIPanel` with a content panel inside a `ScrollViewerControl`, as `ArticleReadControl` does for its body:

```csharp
#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Scrolling;
using Chaos.Client.Rendering;
using Chaos.Client.Systems.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>A read-only piece: headings, wrapped text and pictures, scrolling top to bottom. Pictures load by hash.</summary>
public sealed class PieceView : UIPanel
{
    private const int GAP = 6;
    private const int LOADING_HEIGHT = 20;

    private readonly UIPanel Content;
    private readonly HashSet<string> Waiting = new(StringComparer.Ordinal);
    private readonly CollegePictureTransfers Transfers;
    private CollegePieceInfo? Piece;

    public PieceView(CollegePictureTransfers transfers, int width, int height)
    {
        Transfers = transfers;
        Width = width;
        Height = height;
        Content = new UIPanel { Width = width - 12, Height = height };

        AddChild(
            new ScrollViewerControl(Content)
            {
                Width = width,
                Height = height
            });

        Transfers.PictureReady += OnPictureReady;
    }

    public override void Dispose()
    {
        Transfers.PictureReady -= OnPictureReady;
        base.Dispose();
    }

    public void Show(CollegePieceInfo piece)
    {
        Piece = piece;
        Layout();
    }

    private void OnPictureReady(string hash)
    {
        if (Piece is not null && Waiting.Contains(hash))
            Layout();
    }

    private void Layout()
    {
        foreach (var child in Content.Children.ToList())
            child.Dispose();

        Content.Children.Clear();
        Waiting.Clear();

        var y = 0;
        var width = Content.Width;

        foreach (var block in Piece!.Blocks)
        {
            switch (block.Kind)
            {
                case CollegeBlockKind.Heading:
                    y += AddText(block.Text, y, width, LegendColors.Gold) + 2;

                    break;
                case CollegeBlockKind.Text:
                    y += AddText(block.Text, y, width, LegendColors.White);

                    break;
                case CollegeBlockKind.Picture:
                    if (Transfers.TryGetTexture(block.Hash, out var texture))
                    {
                        Content.AddChild(
                            new UIImage
                            {
                                Texture = texture,
                                X = Math.Max(0, (width - texture.Width) / 2),
                                Y = y,
                                Width = texture.Width,
                                Height = texture.Height,
                                IsHitTestVisible = false
                            });

                        y += texture.Height;
                    } else
                    {
                        Waiting.Add(block.Hash);
                        y += AddText("Loading picture...", y, width, LegendColors.Gray);
                    }

                    break;
            }

            y += GAP;
        }

        Content.Height = Math.Max(Height, y);
    }

    private int AddText(string text, int y, int width, Microsoft.Xna.Framework.Color color)
    {
        var lines = text.Split('\n').Sum(paragraph => Math.Max(1, TextRenderer.WrapText(paragraph, width).Count));
        var height = lines * TextRenderer.CHAR_HEIGHT;

        Content.AddChild(
            new UILabel
            {
                X = 0,
                Y = y,
                Width = width,
                Height = height,
                WordWrap = true,
                VerticalAlignment = VerticalAlignment.Top,
                ForegroundColor = color,
                IsHitTestVisible = false,
                Text = text
            });

        return height;
    }
}
```

`UIImage` doesn't own textures it is given here; `CollegePictureTransfers.Clear` disposes them. If `UIImage.Dispose` disposes its `Texture`, set `Texture = null` before disposing the image in `Layout` (check `UIImage.Dispose` at `Controls/Components/UIImage.cs:13`). If `UILabel` doesn't keep `\n` as a line break when word-wrapping, add one label per paragraph instead.

- [ ] **Step 3: The reader window.** Create `Chaos.Client/Controls/World/Popups/College/CollegeReaderControl.cs`, built like `TownBallotControl` on `GuildCloakDialogBase("_nsett", false)`:

```csharp
#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.Generic;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Systems.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>
///     The College reader: one piece, scrolling, with a footer that depends on who is reading (the server's Piece context):
///     a judge's vote, the Director's verdict, the gallery's award line, a Teacher's hand-in tools, or nothing.
/// </summary>
public sealed class CollegeReaderControl : GuildCloakDialogBase
{
    private const int WIDTH = 560;
    private const int HEIGHT = 440;
    private const int LEFT = 16;
    private const int TITLE_TOP = 10;
    private const int HEADER_TOP = 26;
    private const int BODY_TOP = 44;
    private const int TIER_WIDTH = 80;
    private const int FOOTER_HEIGHT = 96;

    private readonly UILabel AuthorLine;
    private readonly UITextBox CommentBox;
    private readonly List<CustomButton> FooterButtons = [];
    private readonly UILabel HeaderLabel;
    private readonly CustomButton[] TierButtons;
    private readonly UILabel TitleLabel;
    private readonly TextPopupControl VotesPopup;
    private readonly PieceView View;

    private CollegeDisplayArgs? Current;
    private IReadOnlyList<int> Siblings = [];
    private byte SelectedTier = CollegeProtocol.NO_TIER;

    public CollegeReaderControl(CollegePictureTransfers transfers, TextPopupControl votesPopup)
        : base("_nsett", false)
    {
        Name = "CollegeReader";
        Visible = false;
        UsesControlStack = true;
        Width = WIDTH;
        Height = HEIGHT;
        this.CenterOnScreen();
        VotesPopup = votesPopup;

        OkButton = CreateCloseButton(Hide, 20, 3);
        TitleLabel = Caption(string.Empty, LEFT, TITLE_TOP, WIDTH - (2 * LEFT), HorizontalAlignment.Center, LegendColors.Gold);
        HeaderLabel = Caption(string.Empty, LEFT, HEADER_TOP, WIDTH - (2 * LEFT), HorizontalAlignment.Center, LegendColors.Gray);

        var footerTop = HEIGHT - BORDER_BOTTOM_HEIGHT - FOOTER_HEIGHT;
        View = new PieceView(transfers, WIDTH - (2 * LEFT), footerTop - BODY_TOP - 4) { X = LEFT, Y = BODY_TOP };
        AddChild(View);

        AuthorLine = Caption(string.Empty, LEFT, footerTop, WIDTH - (2 * LEFT));

        TierButtons = CollegeTiers.Votable
                                  .Select((tier, i) => AddButton(CollegeTiers.Name(tier), TIER_WIDTH, LEFT + (i * (TIER_WIDTH + 4)), footerTop + 16, () => SelectTier((byte)tier)))
                                  .ToArray();

        CommentBox = new UITextBox
        {
            X = LEFT,
            Y = footerTop + 16 + CustomButton.HEIGHT + 4,
            Width = WIDTH - (2 * LEFT),
            Height = TextRenderer.CHAR_HEIGHT + 4,
            IsSelectable = true,
            IsTabStop = true,
            ForegroundColor = TextColors.Default
        };

        AddChild(CommentBox);
    }

    /// <summary>Raised with a vote, a verdict, a removal, a "show to class" or a request for the previous/next entry.</summary>
    public event Action<CollegeActionArgs>? ActionRequested;

    /// <summary>The judging list's order, for Prev and Next.</summary>
    public void SetSiblings(IReadOnlyList<int> entryIds) => Siblings = entryIds;

    public void Open(CollegeDisplayArgs args)
    {
        Current = args;
        TitleLabel.Text = args.Piece?.Title is { Length: > 0 } title ? title : "Untitled";
        HeaderLabel.Text = args.Header;
        View.Show(args.Piece!);
        BuildFooter(args);
        Show();
    }

    private void BuildFooter(CollegeDisplayArgs args)
    {
        foreach (var button in FooterButtons)
            button.Dispose();

        FooterButtons.Clear();

        var footerTop = HEIGHT - BORDER_BOTTOM_HEIGHT - FOOTER_HEIGHT;
        var actionsTop = footerTop + 16 + (2 * (CustomButton.HEIGHT + 4));
        var voting = args.Context is CollegePieceContext.Judge or CollegePieceContext.Verdict;

        foreach (var button in TierButtons)
            button.Visible = voting;

        CommentBox.Visible = voting;
        CommentBox.MaxLength = args.Context == CollegePieceContext.Verdict ? CollegeProtocol.MAX_NOTE_CHARS : CollegeProtocol.MAX_COMMENT_CHARS;
        CommentBox.Text = args.MyComment;
        SelectTier(args.MyTier);

        AuthorLine.Text = args.Context switch
        {
            CollegePieceContext.Gallery => $"by {args.Author}",
            CollegePieceContext.Verdict => $"Votes: {VoteSummary(args.Votes)}",
            CollegePieceContext.Judge   => "Your vote (comments go to the Director only):",
            _                           => string.Empty
        };

        switch (args.Context)
        {
            case CollegePieceContext.Judge:
                FooterAction("Save vote", 100, actionsTop, () => Send(CollegeActionType.Vote));
                FooterAction("< Prev", 80, actionsTop, () => Step(-1));
                FooterAction("Next >", 80, actionsTop, () => Step(1));

                break;
            case CollegePieceContext.Verdict:
                FooterAction("Post verdict", 110, actionsTop, () => Send(CollegeActionType.Verdict));
                FooterAction("Votes and comments", 150, actionsTop, ShowVotes);
                FooterAction("Remove entry", 110, actionsTop, () => Send(CollegeActionType.RemoveEntry));

                break;
            case CollegePieceContext.Gallery when args.CanModerate:
                FooterAction("Remove", 90, actionsTop, () => Send(CollegeActionType.RemoveEntry));

                break;
            case CollegePieceContext.HandIn:
                FooterAction("Show to class", 120, actionsTop, () => Send(CollegeActionType.ShowToClass));

                break;
        }
    }

    private void FooterAction(string caption, int width, int y, Action onClick)
    {
        var x = LEFT + FooterButtons.Sum(b => b.Width + 6);
        FooterButtons.Add(AddButton(caption, width, x, y, onClick));
    }

    private void SelectTier(byte tier)
    {
        SelectedTier = tier;

        for (var i = 0; i < TierButtons.Length; i++)
        {
            var name = CollegeTiers.Name(CollegeTiers.Votable[i]);
            TierButtons[i].Caption = (byte)CollegeTiers.Votable[i] == tier ? $"[{name}]" : name;
        }
    }

    private void Send(CollegeActionType type)
    {
        if (Current is null)
            return;

        if (type is CollegeActionType.Vote or CollegeActionType.Verdict && (SelectedTier == CollegeProtocol.NO_TIER))
            return;

        ActionRequested?.Invoke(
            new CollegeActionArgs
            {
                Type = type,
                Id = Current.Id,
                Tier = SelectedTier == CollegeProtocol.NO_TIER ? CollegeTierCode.None : (CollegeTierCode)SelectedTier,
                Text = CommentBox.Text
            });

        if (type is CollegeActionType.Verdict or CollegeActionType.RemoveEntry)
            Hide();
    }

    private void Step(int by)
    {
        if (Current is null)
            return;

        var at = Siblings.ToList().IndexOf(Current.Id);
        var next = at + by;

        if ((at < 0) || (next < 0) || (next >= Siblings.Count))
            return;

        ActionRequested?.Invoke(new CollegeActionArgs { Type = CollegeActionType.OpenPiece, Source = CollegePieceSource.Entry, Id = Siblings[next] });
    }

    private void ShowVotes()
    {
        if (Current is null)
            return;

        var text = Current.Votes.Count == 0
            ? "No judge has voted."
            : string.Join("\n\n", Current.Votes.Select(v => $"{v.Judge}: {CollegeTiers.Name(v.Tier)}{(v.Comment.Length > 0 ? "\n" + v.Comment : string.Empty)}"));

        VotesPopup.Show(text);
    }

    private static string VoteSummary(List<CollegeVoteInfo> votes)
        => votes.Count == 0
            ? "none yet"
            : string.Join(", ", votes.GroupBy(v => v.Tier).OrderByDescending(g => g.Key).Select(g => $"{CollegeTiers.Name(g.Key)} x{g.Count()}"));

    public override void Hide()
    {
        if (!Visible)
            return;

        CommentBox.IsFocused = false;
        base.Hide();
    }
}
```

Check against the real APIs while building: `CustomButton.Caption` (used by `GuildCloakDialogBase.AddStepButtons`), `CustomButton.Width`, `TextPopupControl.Show(string, PopupStyle)`, `UITextBox.IsFocused` (used by `TownBallotControl.Hide`). Disposing a `CustomButton` must also remove it from this panel's `Children`; if `Dispose` doesn't, call `Children.Remove(button)` first.

- [ ] **Step 4: Build.** `dotnet build Chaos.Client/Chaos.Client.csproj -p:UnoraServerPath=...` -> succeeds.

```json:metadata
{"files": ["Chaos.Client/Controls/World/Popups/College/PieceView.cs", "Chaos.Client/Controls/World/Popups/College/CollegeReaderControl.cs", "Chaos.Client/Controls/World/Popups/College/CollegeTiers.cs"], "verifyCommand": "dotnet build Chaos.Client/Chaos.Client.csproj -p:UnoraServerPath=C:\\Users\\Michael\\Documents\\GitHub\\worktrees\\college-server", "acceptanceCriteria": ["PieceView lays out headings/text/pictures and relayouts on PictureReady", "reader footer per context", "buttons send the matching action", "window fits 640x480"], "modelTier": "frontier"}
```

---

### Task 16: The writing window (client worktree)

**Goal:** The writing window: a title box, a scrolling stack of editable blocks driven by `WritingDocument`, Heading, Insert picture, Save, Submit (or Hand in), the counter and a status line.

**Files:**
- Create: `Chaos.Client/Controls/World/Popups/College/BlockTextBox.cs`, `Chaos.Client/Controls/World/Popups/College/CollegeWriterControl.cs`

**Acceptance Criteria:**
- [ ] Opening with a draft shows its title and blocks; a blank piece shows one empty text block.
- [ ] Typing grows a text block's height with its wrapped lines; the stack scrolls with the mouse wheel.
- [ ] Heading turns the caret's line into a heading (and back); Insert picture opens the file picker, prepares the picture, starts its upload and inserts the block at the caret ("Uploading..." until accepted, then the picture); a refused upload removes the block and shows the message.
- [ ] Backspace at a block's start and Enter at the end of a heading follow `WritingDocument`; each picture block has a remove button.
- [ ] The counter shows `2,140 / 10,000   1 / 5`. Save sends `SaveDraft`; Submit asks "Enter this piece for 3 Educated Marks?" (or "...for your free entry?") and sends `Submit` with the piece on the second click; closing with changes saves first.
- [ ] Hand-in mode shows the prompt above the title, Hand in instead of Save/Submit, and "Close without handing in?" on the first Close click when there is unsent text.
- [ ] `WriterResult` messages show on the status line; Submitted and HandedIn close the window.

**Verify:** The client builds; Task 18's in-game checklist items 1, 2 and 7 pass.

**Steps:**

- [ ] **Step 1: The block text box.** Create `Chaos.Client/Controls/World/Popups/College/BlockTextBox.cs`:

```csharp
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>A text box that reports Backspace at its very start and Enter in a one-line box, so the writer can join or move between blocks.</summary>
public sealed class BlockTextBox : UITextBox
{
    public event Action? BackspaceAtStart;
    public event Action? EnterPressed;

    public void PlaceCaret(int position)
    {
        CursorPosition = Math.Clamp(position, 0, Text.Length);
        ClearSelection();
        IsFocused = true;
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (IsFocused && (e.Keycode == Keycode.Backspace) && (CursorPosition == 0) && !HasSelection)
        {
            BackspaceAtStart?.Invoke();
            e.Handled = true;

            return;
        }

        if (IsFocused && !IsMultiLine && e.Keycode is Keycode.Return or Keycode.KpEnter)
        {
            EnterPressed?.Invoke();
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }
}
```

Use the real `Keycode` names from the SDL keycode enum the client uses (`grep -n "Backspace\|Return" Chaos.Client/Definitions/*.cs`).

- [ ] **Step 2: The writer.** Create `Chaos.Client/Controls/World/Popups/College/CollegeWriterControl.cs`. The window rebuilds its block controls from the document after every structural edit (heading, picture, merge) and copies text from each box into the document every frame:

```csharp
#region
using System.Globalization;
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.Scrolling;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Systems.College;
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>
///     The College writing window for drafts and class hand-ins. Every editing rule is in <see cref="WritingDocument" />;
///     this window shows the blocks, forwards keys and buttons, and runs picture uploads through
///     <see cref="CollegePictureTransfers" />. The server checks the piece again on Save, Submit and Hand in.
/// </summary>
public sealed class CollegeWriterControl : GuildCloakDialogBase
{
    private const int WIDTH = 560;
    private const int HEIGHT = 440;
    private const int LEFT = 16;
    private const int TITLE_TOP = 30;
    private const int BLOCK_GAP = 6;
    private const int FOOTER_HEIGHT = 44;
    private const string UPLOADING = "Uploading picture...";

    private readonly UIPanel Blocks;
    private readonly List<UIElement> BlockViews = [];
    private readonly UILabel Counter;
    private readonly CustomButton HeadingButton;
    private readonly CustomButton PictureButton;
    private readonly UILabel PromptLabel;
    private readonly CustomButton SaveButton;
    private readonly UILabel Status;
    private readonly CustomButton SubmitButton;
    private readonly UILabel TitleCaption;
    private readonly UITextBox TitleBox;
    private readonly CollegePictureTransfers Transfers;
    private readonly HashSet<string> Uploading = new(StringComparer.Ordinal);

    private bool CloseArmed;
    private WritingDocument Document = new();
    private int FocusedIndex;
    private Task<string?>? Picking;
    private CollegeDisplayArgs? Session;
    private bool SubmitArmed;

    public CollegeWriterControl(CollegePictureTransfers transfers)
        : base("_nsett", false)
    {
        Name = "CollegeWriter";
        Visible = false;
        UsesControlStack = true;
        Width = WIDTH;
        Height = HEIGHT;
        this.CenterOnScreen();
        Transfers = transfers;
        Transfers.UploadFinished += OnUploadFinished;
        Transfers.PictureReady += _ => Rebuild();

        OkButton = CreateCloseButton(OnCloseClicked, 20, 3);
        TitleCaption = Caption(string.Empty, LEFT, 10, WIDTH - (2 * LEFT), HorizontalAlignment.Center, LegendColors.Gold);
        PromptLabel = Caption(string.Empty, LEFT, TITLE_TOP - 2, WIDTH - (2 * LEFT), color: LegendColors.Gray);

        TitleBox = new UITextBox
        {
            X = LEFT,
            Y = TITLE_TOP,
            Width = WIDTH - (2 * LEFT),
            Height = TextRenderer.CHAR_HEIGHT + 4,
            MaxLength = CollegeProtocol.MAX_TITLE_CHARS,
            IsSelectable = true,
            IsTabStop = true,
            ForegroundColor = TextColors.Default
        };

        AddChild(TitleBox);

        var bodyTop = TITLE_TOP + TextRenderer.CHAR_HEIGHT + 10;
        var footerTop = HEIGHT - BORDER_BOTTOM_HEIGHT - FOOTER_HEIGHT;
        Blocks = new UIPanel { Width = WIDTH - (2 * LEFT) - 12, Height = footerTop - bodyTop };

        AddChild(
            new ScrollViewerControl(Blocks)
            {
                X = LEFT,
                Y = bodyTop,
                Width = WIDTH - (2 * LEFT),
                Height = footerTop - bodyTop - 4
            });

        HeadingButton = AddButton("Heading", 80, LEFT, footerTop, OnHeadingClicked);
        PictureButton = AddButton("Insert picture", 110, LEFT + 86, footerTop, OnPictureClicked);
        SaveButton = AddButton("Save", 60, LEFT + 202, footerTop, SendSave);
        SubmitButton = AddButton("Submit", 130, LEFT + 268, footerTop, OnSubmitClicked);
        Counter = Caption(string.Empty, LEFT + 404, footerTop + 5, WIDTH - LEFT - 404 - LEFT, HorizontalAlignment.Right, LegendColors.Gray);
        Status = Caption(string.Empty, LEFT, footerTop + CustomButton.HEIGHT + 4, WIDTH - (2 * LEFT), color: LegendColors.White);
    }

    public event Action<CollegeActionArgs>? ActionRequested;

    private bool HandInMode => Session?.Mode == CollegeWriterMode.HandIn;

    public void Open(CollegeDisplayArgs args)
    {
        Session = args;
        Document = WritingDocument.From(args.Piece);
        TitleBox.Text = Document.Title;
        TitleCaption.Text = HandInMode ? $"{args.Subject}: class hand-in" : $"{args.Subject}: draft";
        PromptLabel.Text = HandInMode && (args.Prompt.Length > 0) ? $"Prompt: {args.Prompt}" : string.Empty;
        PromptLabel.Visible = PromptLabel.Text.Length > 0;
        TitleBox.Y = TITLE_TOP + (PromptLabel.Visible ? TextRenderer.CHAR_HEIGHT + 2 : 0);
        SaveButton.Visible = !HandInMode;
        SubmitButton.Caption = HandInMode ? "Hand in" : args.FreeEntries > 0 ? "Submit (free)" : $"Submit ({args.EntryCost} marks)";
        Status.Text = string.Empty;
        SubmitArmed = CloseArmed = false;
        FocusedIndex = 0;
        Rebuild();
        Show();
    }

    public void ShowResult(CollegeDisplayArgs args)
    {
        Status.Text = args.Message;

        if (args.Result is CollegeWriterResult.Saved)
            Document.MarkClean();

        if (args.Result is CollegeWriterResult.Submitted or CollegeWriterResult.HandedIn)
        {
            Document.MarkClean();
            Hide();
        }
    }

    public override void Update(GameTime gameTime)
    {
        if (Visible)
        {
            PullText();
            FinishPicking();
            Counter.Text = string.Create(
                CultureInfo.InvariantCulture,
                $"{Document.TextLength:N0} / {CollegeProtocol.MAX_TEXT_CHARS:N0}   {Document.PictureCount} / {CollegeProtocol.MAX_PICTURES}");
            LayoutBlocks();
        }

        base.Update(gameTime);
    }

    // ---- blocks ----

    private void Rebuild()
    {
        foreach (var view in BlockViews)
        {
            Blocks.Children.Remove(view);
            view.Dispose();
        }

        BlockViews.Clear();

        for (var i = 0; i < Document.Blocks.Count; i++)
        {
            var index = i;
            var block = Document.Blocks[i];

            UIElement view = block.Kind switch
            {
                WritingBlockKind.Picture => PictureView(block.Hash, index),
                _                        => TextView(block, index)
            };

            BlockViews.Add(view);
            Blocks.AddChild(view);
        }

        LayoutBlocks();
    }

    private BlockTextBox TextView(WritingBlock block, int index)
    {
        var heading = block.Kind == WritingBlockKind.Heading;

        var box = new BlockTextBox
        {
            Width = Blocks.Width,
            Height = TextRenderer.CHAR_HEIGHT + 4,
            IsMultiLine = !heading,
            MaxLength = heading ? CollegeProtocol.MAX_HEADING_CHARS : CollegeProtocol.MAX_TEXT_CHARS,
            IsSelectable = true,
            IsTabStop = true,
            ForegroundColor = heading ? LegendColors.Gold : TextColors.Default,
            Text = block.Text
        };

        box.OnFocused += _ => FocusedIndex = index;
        box.BackspaceAtStart += () => Edit(Document.BackspaceAtStart(index));

        if (heading)
            box.EnterPressed += () => Edit(Document.EnterAfterHeading(index));

        return box;
    }

    private UIPanel PictureView(string hash, int index)
    {
        var panel = new UIPanel { Width = Blocks.Width };

        if (Uploading.Contains(hash) || !Transfers.TryGetTexture(hash, out var texture))
        {
            panel.Height = TextRenderer.CHAR_HEIGHT + 4;
            panel.AddChild(new UILabel { Width = Blocks.Width - 30, Height = TextRenderer.CHAR_HEIGHT, Text = UPLOADING, ForegroundColor = LegendColors.Gray });
        } else
        {
            panel.Height = texture.Height;
            panel.AddChild(new UIImage { Texture = texture, X = Math.Max(0, (Blocks.Width - texture.Width) / 2), Width = texture.Width, Height = texture.Height });
        }

        var remove = new CustomButton("x", 22) { X = Blocks.Width - 24, Y = 0 };
        remove.Clicked += () => Edit(Document.RemovePicture(index));
        panel.AddChild(remove);

        return panel;
    }

    private void LayoutBlocks()
    {
        var y = 0;

        for (var i = 0; i < BlockViews.Count; i++)
        {
            var view = BlockViews[i];

            if (view is BlockTextBox { IsMultiLine: true } box)
            {
                var lines = box.Text.Split('\n').Sum(p => Math.Max(1, TextRenderer.WrapText(p, box.Width - 4).Count));
                box.Height = (lines * TextRenderer.CHAR_HEIGHT) + 4;
            }

            view.Y = y;
            y += view.Height + BLOCK_GAP;
        }

        Blocks.Height = y;
    }

    private void PullText()
    {
        Document.Title = TitleBox.Text;

        for (var i = 0; (i < BlockViews.Count) && (i < Document.Blocks.Count); i++)
            if (BlockViews[i] is BlockTextBox box)
            {
                Document.SetText(i, box.Text);

                //SetText may cut the text to the budget; show what was kept
                if (box.Text != Document.Blocks[i].Text)
                    box.Text = Document.Blocks[i].Text;
            }
    }

    private void Edit((int Index, int Caret) at)
    {
        Rebuild();
        FocusedIndex = Math.Clamp(at.Index, 0, BlockViews.Count - 1);

        if (BlockViews[FocusedIndex] is BlockTextBox box)
            box.PlaceCaret(at.Caret);
    }

    private int FocusedCaret() => BlockViews.ElementAtOrDefault(FocusedIndex) is BlockTextBox box ? box.CursorPosition : 0;

    // ---- buttons ----

    private void OnHeadingClicked()
    {
        PullText();
        Edit(Document.ToggleHeading(FocusedIndex, FocusedCaret()));
    }

    private void OnPictureClicked()
    {
        if (Picking is not null)
            return;

        if (Document.PictureCount >= CollegeProtocol.MAX_PICTURES)
        {
            Status.Text = "Pieces can hold 5 pictures at most.";

            return;
        }

        Status.Text = "Choose a picture...";
        Picking = PictureFilePicker.PickAsync();
    }

    private void FinishPicking()
    {
        if (Picking is not { IsCompleted: true } done)
            return;

        Picking = null;

        if (done.IsFaulted || done.Result is not { } path)
        {
            Status.Text = string.Empty;

            return;
        }

        var picture = PicturePrep.Prepare(File.ReadAllBytes(path), out var error);

        if (picture is null)
        {
            Status.Text = error;

            return;
        }

        PullText();

        if (Document.InsertPicture(FocusedIndex, FocusedCaret(), picture.Hash) is not { } at)
        {
            Status.Text = "That picture is already in this piece.";

            return;
        }

        Uploading.Add(picture.Hash);
        Transfers.Upload(picture);
        Status.Text = UPLOADING;
        Edit(at);
    }

    private void OnUploadFinished(string hash, bool ok, string message)
    {
        if (!Uploading.Remove(hash))
            return;

        if (!ok)
        {
            var index = Document.Blocks.FindIndex(b => (b.Kind == WritingBlockKind.Picture) && (b.Hash == hash));

            if (index >= 0)
                Document.RemovePicture(index);

            Status.Text = message;
        } else
            Status.Text = "Picture added.";

        Rebuild();
    }

    private void OnSubmitClicked()
    {
        PullText();

        if (Uploading.Count > 0)
        {
            Status.Text = "Wait for the pictures to finish uploading.";

            return;
        }

        if (HandInMode)
        {
            Send(CollegeActionType.HandIn);

            return;
        }

        if (!SubmitArmed)
        {
            SubmitArmed = true;
            Status.Text = Session!.FreeEntries > 0 ? "Enter this piece for your free entry? Click Submit again." : $"Enter this piece for {Session.EntryCost} Educated Marks? Click Submit again.";

            return;
        }

        SubmitArmed = false;
        Send(CollegeActionType.Submit);
    }

    private void SendSave()
    {
        if (HandInMode || Session is null)
            return;

        PullText();

        if (Uploading.Count > 0)
        {
            Status.Text = "Wait for the pictures to finish uploading.";

            return;
        }

        Send(CollegeActionType.SaveDraft);
    }

    private void Send(CollegeActionType type)
        => ActionRequested?.Invoke(
            new CollegeActionArgs
            {
                Type = type,
                Subject = Session!.Subject,
                Piece = Document.ToInfo(Session.Subject)
            });

    private void OnCloseClicked()
    {
        PullText();

        if (HandInMode && Document.IsDirty && (Document.TextLength > 0) && !CloseArmed)
        {
            CloseArmed = true;
            Status.Text = "Close without handing in? Click close again.";

            return;
        }

        if (!HandInMode && Document.IsDirty && (Uploading.Count == 0))
            Send(CollegeActionType.SaveDraft);

        Hide();
    }

    public override void Hide()
    {
        if (!Visible)
            return;

        TitleBox.IsFocused = false;

        foreach (var view in BlockViews.OfType<BlockTextBox>())
            view.IsFocused = false;

        base.Hide();
    }
}
```

Notes for the implementer:
- `CreateCloseButton` takes a `ClickedHandler`; if `OnCloseClicked` doesn't convert, wrap it in a lambda of that delegate type.
- Submit carries the whole piece; the server saves it as the draft and then submits it in one handler call, so no separate Save is needed first.
- The disposal of block views in `Rebuild` must not dispose textures owned by `CollegePictureTransfers` (see Task 15's note on `UIImage.Dispose`).
- If `UITextBox` draws its own scroll inside a multi-line box, set its height large enough that it never scrolls (the line count above), or turn its own scrolling off if there is a flag.

- [ ] **Step 3: Build.** `dotnet build Chaos.Client/Chaos.Client.csproj -p:UnoraServerPath=...` -> succeeds.

```json:metadata
{"files": ["Chaos.Client/Controls/World/Popups/College/BlockTextBox.cs", "Chaos.Client/Controls/World/Popups/College/CollegeWriterControl.cs"], "verifyCommand": "dotnet build Chaos.Client/Chaos.Client.csproj -p:UnoraServerPath=C:\\Users\\Michael\\Documents\\GitHub\\worktrees\\college-server", "acceptanceCriteria": ["opens draft or blank", "blocks grow and scroll", "heading/picture/backspace/enter via WritingDocument; remove button", "counter; Save; two-click Submit; save on close", "hand-in mode prompt and close confirm", "WriterResult status; Submitted/HandedIn close"], "modelTier": "frontier"}
```

---

### Task 17: Judging, gallery and hand-in lists, and the world-screen wiring (client worktree)

**Goal:** The three list windows and `WorldScreen.College.cs`, which builds the College windows on first use and routes every `CollegeDisplay` sub-type.

**Files:**
- Create: `Chaos.Client/Controls/World/Popups/College/CollegeListRow.cs`, `CollegeJudgingControl.cs`, `CollegeGalleryControl.cs`, `CollegeHandInsControl.cs` (all in `Controls/World/Popups/College/`)
- Create: `Chaos.Client/Screens/WorldScreen.College.cs`
- Modify: `Chaos.Client/Screens/WorldScreen.cs` (call `WireCollege()` beside `WireTownBallot()`, `UnwireCollege()` beside `UnwireTownBallot()`, and `College?.Transfers.Clear()` beside `GuildEmblemTextures.Clear()`)
- Modify: `CLAUDE.md` (client) — one line for the `College/` popups folder in the Popups list

**Acceptance Criteria:**
- [ ] The judging list shows tabs "Voting (N)" and, for moderators, "Awaiting verdict (N)", rows of subject, title, days left and your vote; double-click or Read opens the entry (reader in Judge or Verdict context) and passes the tab's order to the reader for Prev/Next.
- [ ] The gallery opens on its subject, has six subject tabs that send `GalleryPage`, sorts as the server sent, shows 10 rows a page with a page line, tier badges coloured by tier, and "No pieces yet" when empty.
- [ ] The hand-in list shows student, title and time; Read opens the hand-in; Show to class sends `ShowToClass`.
- [ ] `WorldScreen` routes OpenWriter, WriterResult, PictureReply, PicturePart, JudgingList, Piece, GalleryList and HandInList to the right window, and sends every window's actions with `SendCollegeAction`.
- [ ] College textures are freed on logout.

**Verify:** The client builds; the full client test suite passes (`dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=... -- --no-ansi`); Task 18's in-game checklist passes.

**Steps:**

- [ ] **Step 1: The shared row.** Create `CollegeListRow.cs`, modelled on `TownBallotRow` (selected background, `Clicked`), with up to four column labels and a `DoubleClicked` event:

```csharp
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>One row of a College list: up to four columns at fixed x offsets; click selects, double-click opens.</summary>
public sealed class CollegeListRow : UIPanel
{
    public const int HEIGHT = TextRenderer.CHAR_HEIGHT + 6;
    private static readonly Color SelectedColor = new(107, 79, 42);

    private readonly UILabel[] Columns;

    public CollegeListRow(int width, IReadOnlyList<int> columnX)
    {
        Width = width;
        Height = HEIGHT;
        Columns = new UILabel[columnX.Count];

        for (var i = 0; i < columnX.Count; i++)
        {
            var right = i + 1 < columnX.Count ? columnX[i + 1] : width;

            Columns[i] = new UILabel
            {
                X = columnX[i] + 4,
                Y = 3,
                Width = right - columnX[i] - 8,
                Height = TextRenderer.CHAR_HEIGHT,
                ForegroundColor = LegendColors.White,
                IsHitTestVisible = false
            };

            AddChild(Columns[i]);
        }
    }

    public int Id { get; private set; }

    public bool Selected
    {
        get;
        set
        {
            field = value;
            BackgroundColor = value ? SelectedColor : null;
        }
    }

    public event Action? Clicked;
    public event Action? DoubleClicked;

    public void Set(int id, params (string Text, Color Color)[] columns)
    {
        Id = id;

        for (var i = 0; i < Columns.Length; i++)
        {
            Columns[i].Text = i < columns.Length ? columns[i].Text : string.Empty;
            Columns[i].ForegroundColor = i < columns.Length ? columns[i].Color : LegendColors.White;
        }
    }

    public override void OnClick(ClickEvent e)
    {
        if (e.Button != MouseButton.Left)
            return;

        Clicked?.Invoke();
        e.Handled = true;
    }

    public override void OnDoubleClick(DoubleClickEvent e)
    {
        DoubleClicked?.Invoke();
        e.Handled = true;
    }
}
```

- [ ] **Step 2: The three list windows.** Each is a `GuildCloakDialogBase("_nsett", false)`, 500 x 360, centred, with a gold title caption, a row of tab buttons (judging, gallery) under it, a column header line in gold, 12 `CollegeListRow`s (10 for the gallery), a page line, and action buttons at the bottom; each raises `event Action<CollegeActionArgs>? ActionRequested`. Write them following this shape (judging shown in full; gallery and hand-ins differ only as listed after it):

```csharp
#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>The judging list: open entries without authors; moderators also see entries awaiting a verdict.</summary>
public sealed class CollegeJudgingControl : GuildCloakDialogBase
{
    private const int WIDTH = 500;
    private const int HEIGHT = 360;
    private const int LEFT = 16;
    private const int ROWS = 12;
    private static readonly int[] ColumnX = [0, 80, 340, 400];

    private readonly UILabel Empty;
    private readonly CustomButton ReadButton;
    private readonly CollegeListRow[] Rows = new CollegeListRow[ROWS];
    private readonly CustomButton VotingTab;
    private readonly CustomButton WaitingTab;

    private List<CollegeJudgingRowInfo> All = [];
    private int Page;
    private int SelectedId;
    private bool ShowingWaiting;

    public CollegeJudgingControl()
        : base("_nsett", false)
    {
        Name = "CollegeJudging";
        Visible = false;
        UsesControlStack = true;
        Width = WIDTH;
        Height = HEIGHT;
        this.CenterOnScreen();
        OkButton = CreateCloseButton(Hide, 20, 3);

        Caption("Judge entries", 0, 10, WIDTH, HorizontalAlignment.Center, LegendColors.Gold);
        VotingTab = AddButton("Voting", 130, LEFT, 28, () => SwitchTab(false));
        WaitingTab = AddButton("Awaiting verdict", 160, LEFT + 136, 28, () => SwitchTab(true));

        var headerTop = 28 + CustomButton.HEIGHT + 6;
        Caption("Subject", LEFT + ColumnX[0] + 4, headerTop, 70, color: LegendColors.Gold);
        Caption("Title", LEFT + ColumnX[1] + 4, headerTop, 250, color: LegendColors.Gold);
        Caption("Days", LEFT + ColumnX[2] + 4, headerTop, 50, color: LegendColors.Gold);
        Caption("Your vote", LEFT + ColumnX[3] + 4, headerTop, 80, color: LegendColors.Gold);

        var rowsTop = headerTop + TextRenderer.CHAR_HEIGHT + 4;

        for (var i = 0; i < ROWS; i++)
        {
            var row = new CollegeListRow(WIDTH - (2 * LEFT), ColumnX) { X = LEFT, Y = rowsTop + (i * CollegeListRow.HEIGHT), Visible = false };
            row.Clicked += () => Select(row.Id);
            row.DoubleClicked += () => OpenEntry(row.Id);
            AddChild(row);
            Rows[i] = row;
        }

        Empty = Caption("Nothing to judge right now.", LEFT, rowsTop + 40, WIDTH - (2 * LEFT), HorizontalAlignment.Center, LegendColors.Gray);
        var bottom = HEIGHT - BORDER_BOTTOM_HEIGHT - CustomButton.HEIGHT - 4;
        AddButton("<", 28, LEFT, bottom, () => Turn(-1));
        AddButton(">", 28, LEFT + 32, bottom, () => Turn(1));
        ReadButton = AddButton("Read", 80, LEFT + 70, bottom, () => OpenEntry(SelectedId));
    }

    public event Action<CollegeActionArgs>? ActionRequested;

    /// <summary>The visible tab's entry ids in order, for the reader's Prev and Next.</summary>
    public IReadOnlyList<int> VisibleOrder => Visible ? Filtered().Select(r => r.Id).ToList() : [];

    public void Open(CollegeDisplayArgs args)
    {
        All = args.JudgingRows;
        WaitingTab.Visible = args.CanModerate;

        if (!args.CanModerate)
            ShowingWaiting = false;

        Refresh();

        if (!Visible)
            Show();
    }

    private List<CollegeJudgingRowInfo> Filtered() => All.Where(r => r.Waiting == ShowingWaiting).ToList();

    private void SwitchTab(bool waiting)
    {
        ShowingWaiting = waiting;
        Page = 0;
        Refresh();
    }

    private void Turn(int by)
    {
        var pages = Math.Max(1, (Filtered().Count + ROWS - 1) / ROWS);
        Page = Math.Clamp(Page + by, 0, pages - 1);
        Refresh();
    }

    private void Select(int id)
    {
        SelectedId = id;

        foreach (var row in Rows)
            row.Selected = row.Visible && (row.Id == id);
    }

    private void OpenEntry(int id)
    {
        if (id != 0)
            ActionRequested?.Invoke(new CollegeActionArgs { Type = CollegeActionType.OpenPiece, Source = CollegePieceSource.Entry, Id = id });
    }

    private void Refresh()
    {
        VotingTab.Caption = $"Voting ({All.Count(r => !r.Waiting)})";
        WaitingTab.Caption = $"Awaiting verdict ({All.Count(r => r.Waiting)})";
        var rows = Filtered().Skip(Page * ROWS).Take(ROWS).ToList();

        for (var i = 0; i < ROWS; i++)
        {
            Rows[i].Visible = i < rows.Count;

            if (i < rows.Count)
                Rows[i].Set(
                    rows[i].Id,
                    (rows[i].Subject.ToString(), LegendColors.Gray),
                    (rows[i].Title, LegendColors.White),
                    (rows[i].Waiting ? "-" : rows[i].DaysLeft.ToString(), LegendColors.White),
                    (CollegeTiers.Name(rows[i].MyTier), LegendColors.Gold));
        }

        Empty.Visible = rows.Count == 0;
        ReadButton.Enabled = rows.Count > 0;
        Select(rows.Any(r => r.Id == SelectedId) ? SelectedId : rows.FirstOrDefault()?.Id ?? 0);
    }
}
```

`CollegeGalleryControl` differs from this as follows:
- title `$"Gallery of {subject}"`; six subject tab buttons (Art, Music, Literature, History, Lore, Philosophy, 72 px each) that send `new CollegeActionArgs { Type = CollegeActionType.GalleryPage, Subject = code }`; the current subject's tab caption is shown in brackets;
- `ROWS = 10`, columns Title (x 0), Author (x 260), Award (x 380); the award column text is `CollegeTiers.Name(row.Tier)` in `CollegeTiers.Badge(row.Tier)`;
- empty text "No pieces yet.";
- reading sends `OpenPiece` with `Source = CollegePieceSource.Gallery`;
- `Open(args)` takes `args.Subject` and `args.GalleryRows`, resets `Page` to 0 when the subject changes, and shows "Page N of M" in a caption beside the arrows.

`CollegeHandInsControl` differs as follows:
- title "Class hand-ins"; no tabs;
- columns Student (x 0), Title (x 120), Handed in (x 360) where the time is `DateTimeOffset.FromUnixTimeSeconds(row.HandedInUnix).ToLocalTime().ToString("ddd HH:mm")`;
- empty text "No hand-ins yet.";
- buttons Read (sends `OpenPiece` with `Source = CollegePieceSource.HandIn`) and "Show to class" (sends `ShowToClass` with the selected id).

- [ ] **Step 3: Wire the world screen.** Create `Chaos.Client/Screens/WorldScreen.College.cs`:

```csharp
#region
using Chaos.Client.Controls.Generic;
using Chaos.Client.Controls.World.Popups.College;
using Chaos.Client.Systems.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Screens;

/// <summary>The Mileth College windows: writer, reader, judging list, gallery and hand-in list.</summary>
public sealed partial class WorldScreen
{
    private sealed class CollegeWindows
    {
        public required CollegeGalleryControl Gallery { get; init; }
        public required CollegeHandInsControl HandIns { get; init; }
        public required CollegeJudgingControl Judging { get; init; }
        public required CollegeReaderControl Reader { get; init; }
        public required CollegePictureTransfers Transfers { get; init; }
        public required CollegeWriterControl Writer { get; init; }
    }

    //built on first use
    private CollegeWindows? College;

    private void WireCollege() => Game.Connection.OnCollegeDisplay += HandleCollegeDisplay;

    private void UnwireCollege()
    {
        Game.Connection.OnCollegeDisplay -= HandleCollegeDisplay;
        College?.Transfers.Clear();
    }

    private void SendCollegeAction(CollegeActionArgs args) => Game.Connection.SendCollegeAction(args);

    private CollegeWindows BuildCollege()
    {
        var transfers = new CollegePictureTransfers(SendCollegeAction, new CollegePictureCache());
        var votesPopup = new TextPopupControl { ZIndex = 3 };

        var windows = new CollegeWindows
        {
            Transfers = transfers,
            Writer = new CollegeWriterControl(transfers) { ZIndex = 2 },
            Reader = new CollegeReaderControl(transfers, votesPopup) { ZIndex = 2 },
            Judging = new CollegeJudgingControl { ZIndex = 2 },
            Gallery = new CollegeGalleryControl { ZIndex = 2 },
            HandIns = new CollegeHandInsControl { ZIndex = 2 }
        };

        windows.Writer.ActionRequested += SendCollegeAction;
        windows.Reader.ActionRequested += SendCollegeAction;
        windows.Judging.ActionRequested += SendCollegeAction;
        windows.Gallery.ActionRequested += SendCollegeAction;
        windows.HandIns.ActionRequested += SendCollegeAction;

        Root!.AddChild(windows.Writer);
        Root.AddChild(windows.Reader);
        Root.AddChild(windows.Judging);
        Root.AddChild(windows.Gallery);
        Root.AddChild(windows.HandIns);
        Root.AddChild(votesPopup);

        return windows;
    }

    private void HandleCollegeDisplay(CollegeDisplayArgs args)
    {
        //the world screen has not built its controls yet
        if (Root is null)
            return;

        College ??= BuildCollege();

        switch (args.Type)
        {
            case CollegeDisplayType.OpenWriter:
                College.Writer.Open(args);

                break;
            case CollegeDisplayType.WriterResult:
                College.Writer.ShowResult(args);

                break;
            case CollegeDisplayType.PictureReply:
                College.Transfers.OnReply(args);

                break;
            case CollegeDisplayType.PicturePart:
                College.Transfers.OnPart(args);

                break;
            case CollegeDisplayType.JudgingList:
                College.Judging.Open(args);

                break;
            case CollegeDisplayType.Piece:
                College.Reader.SetSiblings(args.Context is CollegePieceContext.Judge or CollegePieceContext.Verdict ? College.Judging.VisibleOrder : []);
                College.Reader.Open(args);

                break;
            case CollegeDisplayType.GalleryList:
                College.Gallery.Open(args);

                break;
            case CollegeDisplayType.HandInList:
                College.HandIns.Open(args);

                break;
        }
    }
}
```

In `WorldScreen.cs` add `WireCollege();` after `WireTowerLeaderboard();` (around line 900), `UnwireCollege();` beside `UnwireTownBallot();` (around line 1128). If `TextPopupControl` needs the screen's existing popup instance instead of a new one, reuse that instance (`grep -n "TextPopupControl" Chaos.Client/Screens/*.cs`).

- [ ] **Step 4: Document the folder.** In the client `CLAUDE.md`, in the Popups list, add after the `PumpkinCarving/` entry: `` `College/` (CollegeWriterControl — the writing window for drafts and hand-ins; CollegeReaderControl — one piece with a footer per reader; PieceView — the shared scrolling piece; CollegeJudgingControl, CollegeGalleryControl, CollegeHandInsControl — the lists; the editor model is `ViewModel/College/WritingDocument`, pictures go through `Systems/College/CollegePictureTransfers`) ``.

- [ ] **Step 5: Build and run the client tests.** Build, then run the full client test suite: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=... -- --no-ansi`. Expected: all pass (the earlier count plus the new College tests).

```json:metadata
{"files": ["Chaos.Client/Controls/World/Popups/College/CollegeListRow.cs", "Chaos.Client/Controls/World/Popups/College/CollegeJudgingControl.cs", "Chaos.Client/Controls/World/Popups/College/CollegeGalleryControl.cs", "Chaos.Client/Controls/World/Popups/College/CollegeHandInsControl.cs", "Chaos.Client/Screens/WorldScreen.College.cs", "Chaos.Client/Screens/WorldScreen.cs", "CLAUDE.md"], "verifyCommand": "dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\\Users\\Michael\\Documents\\GitHub\\worktrees\\college-server -- --no-ansi", "acceptanceCriteria": ["judging tabs, rows, Read, sibling order", "gallery subject tabs, paging, badges, empty text", "hand-in list Read and Show to class", "every display sub-type routed; actions sent", "textures freed on logout"], "modelTier": "frontier"}
```

---

### Task 18: Verify everything and commit (all three worktrees)

**Goal:** Run every test suite, do a local end-to-end check, and make one commit per repo.

**Files:**
- All files changed by Tasks 1-17 in `worktrees/college-server`, `worktrees/college-unora` and `worktrees/college-client`.

**Acceptance Criteria:**
- [ ] The full server suite passes except the known baseline failures.
- [ ] The full client suite passes.
- [ ] `python -m unittest Tools.College.test_college_maps`, `python -m unittest Tools.College.test_emblem_tiers`, `python Tools/College/check_dialogs.py` and `python -m unittest Tools/Emblems/test_emblems.py` pass in the Unora worktree.
- [ ] A local server and client run through checklist steps 1-3 below without errors in the server log.
- [ ] Each worktree has exactly one new commit on `feat/college-core`, staged by explicit paths, with the attribution lines. Nothing is pushed or merged.

**Verify:** `git -C <each worktree> log --oneline -2` shows the new commit on top of 69f8fbe6b / a460b5a7d / client main; `git -C <each worktree> status --short` shows nothing left that belongs to this work.

**Steps:**

- [ ] **Step 1: Run every suite.** Don't build the server and client at the same time.
- Server: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --no-ansi` in `worktrees/college-server`. Expected: only the known baseline failures.
- Unora: the four Python commands in the Acceptance Criteria, from `worktrees/college-unora`.
- Client: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\college-server -- --no-ansi` in `worktrees/college-client`.

- [ ] **Step 2: Local end-to-end check.** Point a local server's staging directory at the Unora worktree's `Data` (see the "Local StagingDirectory" note: never commit that setting), start it, and launch a client from the client worktree (see the "Client worktree version" note). As an admin character:
1. Talk to Veyrin: Write an entry -> History. Type a heading and two paragraphs, insert a PNG with transparency and a JPG photo, Save, close, reopen: both pictures and the text come back.
2. `/college marks <you> give 3`, Submit (two clicks). The Educated mark drops by 3; My entries shows the entry in voting.
3. With a second character given `/college award <name> lore village`, open Judge entries, read the entry (no author shown), vote Village with a comment, change it to Kingdom.
Stop the server and client afterwards. Record anything broken as a fix before committing.

- [ ] **Step 3: Commit the server worktree.** In `worktrees/college-server`, list changes with `git status --short`, then stage every file this plan created or changed by explicit path (never `git add -A` or `.`), and commit. Write the message to a file with UTF-8 without BOM (see the "PowerShell commit BOM" note) and commit with `git commit -F <file>`:

```text
Mileth College part 2: entries, judging, verdicts, rewards, gallery, hand-ins

Pieces are saved one file each with pictures stored once by hash. EntryBook holds the
entry rules; CollegeService runs drafts, submit, voting, verdicts, claims, hand-ins and
the tick's reminders and cleanup. CollegeAction (144) / CollegeDisplay (152) carry the
windows; CLIENT_VERSION 772.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01FtxoAKkUAaqcCKBWNB2Gmz
```

- [ ] **Step 4: Commit the Unora worktree.** Same rules. Message:

```text
Mileth College part 2: entry dialogs, gallery stands, College emblems

Registrar and lectern dialogs for entries, judging and hand-ins; Activity format for the
writing subjects; six gallery stands in the Lyceum hall (college_maps.py); 24 College
emblems (art 259-282) and their templates.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01FtxoAKkUAaqcCKBWNB2Gmz
```

- [ ] **Step 5: Commit the client worktree.** Same rules. Message:

```text
Mileth College part 2: writing window, reader, judging, gallery and hand-in windows

WritingDocument holds the editor rules; pictures are shrunk, uploaded in parts and
cached by hash; one reader window serves judges, the Director, the gallery and classes.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01FtxoAKkUAaqcCKBWNB2Gmz
```

- [ ] **Step 6: Report.** Tell the user the three commit hashes, the setoa batch folder from Task 11, the test counts, and the in-game checklist below. Update the `unora-mileth-college` memory note with the commits and the state (not merged, not pushed). Do not push or merge.

**In-game checklist for the user** (from the spec):

1. Write a History draft with a heading and two pictures. Save, close, reopen.
2. Submit it. Check the marks drop and the Educated mark updates.
3. As a second character with `/college award ... village`, judge it: read, vote, change the vote.
4. End voting with `/college closevoting <id>`, then post a verdict as the Director. Check the mail.
5. Claim it. Check the legend mark, the title and the emblem. Relog.
6. Open the History stand and read the piece. Hide it, then show it.
7. Run a writing activity class: set a prompt, hand in from two characters, show one to the class.
8. Withdraw an entry, and remove one as an admin.

```json:metadata
{"files": [], "verifyCommand": "git -C C:/Users/Michael/Documents/GitHub/worktrees/college-server log --oneline -2", "acceptanceCriteria": ["server suite at known baseline", "client suite passes", "Unora checks pass", "local end-to-end steps 1-3 work", "one commit per worktree, explicit paths, not pushed"], "modelTier": "standard"}
```
