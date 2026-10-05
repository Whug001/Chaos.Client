# Mileth College Part 3 (Art Canvas) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Players draw Art entries and Art hand-ins in a 96 x 72 pixel canvas inside the client. Writers can also draw pictures into writing pieces. Judges, the reader and the gallery's Art tab all show the drawings.

**Architecture:**
- **Server:** an Art piece holds one drawing block: a 32-colour palette plus one swatch number per pixel. This is canvas data, not an image file. Pure rules in `PieceRules` check it.
- **Messages:** the protocol gains a `Drawing` block kind and a `DrawingFetch` / `Drawing` message pair, used for gallery thumbnails.
- **Client:** a pure `PixelDrawing` + `ArtEditor` model does every edit. `ArtCanvasControl` is the window, with three modes: Draft, Hand-in and Picture.
- **Drawings in writing pieces:** they become 384 x 288 PNG pictures that travel through the existing part 2 picture path.

**Tech Stack:** C# 14 / .NET 10, TUnit + FluentAssertions + Moq (server and client tests), MonoGame + SkiaSharp (client).

**Spec:** `Chaos.Client/docs/superpowers/specs/2026-10-05-mileth-college-art-design.md`. Also read the parent design `2026-10-04-mileth-college-design.md` and the part 2 spec `2026-10-05-mileth-college-entries-design.md` in the same folder.

## Global Constraints

- **Branches and folders.** Every path in a task is relative to the repo that task names:
  - server: `C:\Users\Michael\Documents\GitHub\worktrees\college-art-server` (branch `feat/college-art` from server `master` dbb1adaf2);
  - Unora: `C:\Users\Michael\Documents\GitHub\worktrees\college-art-unora` (branch `feat/college-art` from Unora `main` 019ab74d7);
  - client: `C:\Users\Michael\Documents\GitHub\worktrees\college-art-client` (branch `feat/college-art` from client `main`).
  
  Task 0 creates all three.
- **Shared checkouts.** Other Claude sessions use the main checkouts.
  - Never run `git stash`, `git reset`, `git add -A` or `git add .` anywhere. Stage by explicit path.
  - Never delete files with `rm`; the Boost safety check refuses it. Leave deletions to the user.
- **Builds.**
  - A running `Chaos.exe` or game client locks `bin/`. If a build fails with a file lock, ask the user to stop it.
  - Never build the server and the client at the same time.
  - The client worktree builds `Chaos.Client/Chaos.Client.csproj`, not the `.slnx`, with `-p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\college-art-server`.
- **Commits:** one commit per repo at the end (Task 11). Implementers must NOT commit. Leave all changes in the working tree.
- **Tests:** `dotnet test` does not work. Run TUnit through `dotnet run`:
  - server: `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/<Class>/*"`;
  - client: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\college-art-server -- --treenode-filter "/*/*/<Class>/*"`.
- **Known server baseline:** the full server suite fails only `OnItemDroppedOn` stackable, plus `GiveAbility` if it fails on master. Any other failure is a regression.
- **Code style:**
  - Match the surrounding code. Use `Lock` with `EnterScope()`, never `lock`.
  - Comments only for a non-obvious "why". No explanatory comments in tests.
  - Test methods use the `Snake_case_sentence` names the existing College tests use.
  - Use Serena's symbolic tools for code reads and edits where they fit (user CLAUDE.md).
- **Drawing format (both sides agree through `CollegeProtocol`):**
  - 96 x 72 pixels, row by row from the top left; 6,912 bytes, each a swatch number 0-31;
  - palette of exactly 32 colours, 96 bytes R, G, B;
  - shown at zoom 4 (384 x 288);
  - Art note 300 characters at most;
  - Submit needs a title and 346 drawn pixels; a hand-in needs 70 drawn pixels. "Drawn" means not on the drawing's most-used swatch.
- **Messages:** `CollegeBlockKind.Drawing = 3`, `CollegeActionType.DrawingFetch = 12`, `CollegeDisplayType.Drawing = 8`. `CLIENT_VERSION` 772 -> 773. No new opcodes.
- **Text limits:** dialog option text 35 characters, dialog text 360, orange bar 45, no em dashes in Unora JSON.

**User decisions (already made):**
- Canvas 96 x 72 at zoom 4.
- Each piece has its own 32 colours, starting from a default palette; any swatch can be changed with a colour picker.
- Tools: Pen, Eraser, Undo/Redo, grid on/off, plus Fill, Pick, Line, brush sizes 1-3 and Mirror. (Shapes, dither and select/copy were not picked.)
- An Art entry is a title, one drawing and an artist's note of up to 300 characters.
- Writers get a **Draw** button in part 3.
- No importing pictures into the canvas; Art is drawn in the game.
- Art activity classes work like writing classes: prompt, hand-ins, Show to class.
- The gallery's Art tab is a picture grid, 8 per page, framed in the award colour.
- Canvas window layout A: everything on one page, with the title above the canvas and the note below it.
- Storage approach 1: canvas data in the piece; drawings inside writing pieces become normal pictures.

**Plan-time adjustments to the spec (record them in the spec, Task 10):**
- The canvas window is 560 x **464**, not 440. The window frame's bottom border is 47 px, so 440 leaves no room for a 3-line note under a 288-px canvas.
- The gallery keeps its part 2 **Read** button; the spec's "View" means this button.
- Unora needs four dialog-text edits ("write" becomes "enter" or "make"). So Unora is part of this change.

## File structure

**Server (`worktrees/college-art-server`)**

| File | Change |
|---|---|
| `Chaos.DarkAges/Definitions/CollegeProtocol.cs` | Drawing constants and `DrawnPixels` |
| `Chaos.DarkAges/Definitions/Enums.cs` | `CollegeBlockKind.Drawing`, `CollegeActionType.DrawingFetch`, `CollegeDisplayType.Drawing` |
| `Chaos.DarkAges/Definitions/CONSTANTS.cs` | `CLIENT_VERSION` 773 |
| `Chaos.Networking/Entities/Server/CollegeInfos.cs` | `CollegeBlockInfo.Palette`, `.Pixels` |
| `Chaos.Networking/Entities/Client/CollegeActionArgs.cs` | `Id` doc covers DrawingFetch |
| `Chaos.Networking/Entities/Server/CollegeDisplayArgs.cs` | `Palette`, `Pixels` |
| `Chaos.Networking/Converters/CollegeCodec.cs` | Drawing block read/write, `WriteFixed` |
| `Chaos.Networking/Converters/Client/CollegeActionConverter.cs` | DrawingFetch |
| `Chaos.Networking/Converters/Server/CollegeDisplayConverter.cs` | Drawing |
| `Chaos/Services/College/CollegePiece.cs` | `PieceBlockType.Drawing`, block palette/pixels, new `PieceCheck` values |
| `Chaos/Services/College/DrawingRules.cs` (new) | Palette hex/bytes conversion, drawing validity (pure) |
| `Chaos/Services/College/PieceRules.cs` | Art piece shape, note limit, drawn-pixel minimums |
| `Chaos/Services/College/CollegeText.cs` | Art messages, "(drawing class)" |
| `Chaos/Services/College/CollegeOptions.cs` | Art open, `HasActivity` |
| `Chaos/Services/College/CollegeService.cs`, `Timetable.cs` | Activity gate uses `HasActivity` |
| `Chaos/Services/College/CollegeService.Entries.cs` | Subject set before checks, Art hand-ins, `GalleryDrawing` |
| `Chaos/Services/College/CollegePanel.cs` | Drawing mapping, `DrawingFetch` handling |
| `Chaos/Scripting/DialogScripts/Temuair/College/CollegeRegistrarScript.cs` | "Make an entry", Art activity |
| `Chaos/Scripting/DialogScripts/Temuair/College/CollegeLecternScript.cs` | Art activity, "Draw a hand-in" |
| Tests in `Tests/Chaos.Tests/College/` and `Tests/Chaos.Tests/Networking/` | As each task says |

**Unora (`worktrees/college-art-unora`)**: four dialog JSON files in `Data/Configuration/Templates/Dialogs/Temauir/mileth/college/`.

**Client (`worktrees/college-art-client`)**

| File | Responsibility |
|---|---|
| `Chaos.Client/ViewModel/College/ArtPalette.cs` (new) | The default 32 colours |
| `Chaos.Client/ViewModel/College/PixelDrawing.cs` (new) | Palette, pixels, fill, swatch colours, undo/redo |
| `Chaos.Client/ViewModel/College/ArtEditor.cs` (new) | Tools, brush, mirror, strokes, line preview |
| `Chaos.Client/Systems/College/DrawingTextures.cs` (new) | Drawing -> colours (pure) and -> `Texture2D` |
| `Chaos.Client/Systems/College/DrawingPictures.cs` (new) | Drawing <-> 384 x 288 PNG |
| `Chaos.Client/Systems/College/CollegeDrawings.cs` (new) | Gallery thumbnail cache by entry id |
| `Chaos.Client/Systems/College/CollegePictureTransfers.cs` | `BytesOf(hash)` |
| `Chaos.Client/Controls/World/Popups/College/ArtCanvas.cs` (new) | The zoomed canvas element |
| `Chaos.Client/Controls/World/Popups/College/ArtSwatches.cs` (new) | The 32 swatches |
| `Chaos.Client/Controls/World/Popups/College/ArtCanvasControl.cs` (new) | The canvas window |
| `Chaos.Client/Controls/World/Popups/College/GalleryArtCell.cs` (new) | One gallery grid cell |
| `Chaos.Client/Controls/World/Popups/College/CollegeListWindow.cs` | Hooks for a grid page |
| `Chaos.Client/Controls/World/Popups/College/CollegeGalleryControl.cs` | Art tab grid |
| `Chaos.Client/Controls/World/Popups/College/PieceView.cs` | Drawing block |
| `Chaos.Client/Controls/World/Popups/College/CollegeWriterControl.cs` | Draw and Edit |
| `Chaos.Client/ViewModel/College/WritingDocument.cs` | `ReplacePicture`; skip drawing blocks |
| `Chaos.Client/Screens/WorldScreen.College.cs`, `Screens/WorldScreen.cs` | Routing, logout clear |
| `CLAUDE.md` | College control list |
| Tests in `Tests/Chaos.Client.Tests/College/` | As each task says |

---

### Task 0: Create the worktrees

**Goal:** Three worktrees on branch `feat/college-art`, building on the local mains.

**Files:**
- Create: `C:\Users\Michael\Documents\GitHub\worktrees\college-art-server`, `...\college-art-unora`, `...\college-art-client`

**Acceptance Criteria:**
- [ ] `git -C <worktree> branch --show-current` prints `feat/college-art` in all three
- [ ] The server worktree's HEAD is dbb1adaf2; Unora's is 019ab74d7; the client's is client `main`'s tip
- [ ] The server worktree builds

**Verify:** `dotnet build C:\Users\Michael\Documents\GitHub\worktrees\college-art-server\Chaos.slnx` -> Build succeeded

**Steps:**

- [ ] **Step 1: Check the mains.**

```bash
git -C C:/Users/Michael/Documents/GitHub/Chaos.Client/Chaos-Server log --oneline -1 master
git -C C:/Users/Michael/Documents/GitHub/Unora log --oneline -1 main
git -C C:/Users/Michael/Documents/GitHub/Chaos.Client log --oneline -1 main
```

Expected: `dbb1adaf2 ...`, `019ab74d7 ...`, and client main's tip (1d0a32f0 or later; docs-only commits on top are fine). If server master or Unora main moved past these commits, stop and ask the user which commit to build on.

- [ ] **Step 2: Create the worktrees.**

```bash
git -C C:/Users/Michael/Documents/GitHub/Chaos.Client/Chaos-Server worktree add C:/Users/Michael/Documents/GitHub/worktrees/college-art-server -b feat/college-art master
git -C C:/Users/Michael/Documents/GitHub/Unora worktree add C:/Users/Michael/Documents/GitHub/worktrees/college-art-unora -b feat/college-art main
git -C C:/Users/Michael/Documents/GitHub/Chaos.Client worktree add C:/Users/Michael/Documents/GitHub/worktrees/college-art-client -b feat/college-art main
```

- [ ] **Step 3: Build the server worktree.** Run `dotnet build Chaos.slnx` in the server worktree. Expected: Build succeeded.

```json:metadata
{"files": [], "verifyCommand": "dotnet build C:\\Users\\Michael\\Documents\\GitHub\\worktrees\\college-art-server\\Chaos.slnx", "acceptanceCriteria": ["three worktrees on feat/college-art", "server at dbb1adaf2, Unora at 019ab74d7", "server builds"], "modelTier": "mechanical"}
```

---

### Task 1: Protocol: drawing block, DrawingFetch, Drawing, CLIENT_VERSION 773 (server repo)

**Goal:** Both message converters carry drawing blocks and the new sub-types; `CLIENT_VERSION` is 773.

**Files:**
- Modify: `Chaos.DarkAges/Definitions/CollegeProtocol.cs`
- Modify: `Chaos.DarkAges/Definitions/Enums.cs` (the `#region Mileth College` enums)
- Modify: `Chaos.DarkAges/Definitions/CONSTANTS.cs:23`
- Modify: `Chaos.Networking/Entities/Server/CollegeInfos.cs`
- Modify: `Chaos.Networking/Entities/Client/CollegeActionArgs.cs`
- Modify: `Chaos.Networking/Entities/Server/CollegeDisplayArgs.cs`
- Modify: `Chaos.Networking/Converters/CollegeCodec.cs`
- Modify: `Chaos.Networking/Converters/Client/CollegeActionConverter.cs`
- Modify: `Chaos.Networking/Converters/Server/CollegeDisplayConverter.cs`
- Test: `Tests/Chaos.Tests/Networking/CollegePacketConverterTests.cs`, `Tests/Chaos.Tests/Networking/GuildEmblemPacketConverterTests.cs`, new `Tests/Chaos.Tests/College/DrawnPixelsTests.cs`

**Acceptance Criteria:**
- [ ] Every action and display sub-type round-trips, including an Art piece with a drawing block, `DrawingFetch` and `Drawing`
- [ ] A drawing block with short palette or pixels is written at full size (96 and 6,912 bytes), padded with zeros
- [ ] `CollegeProtocol.DrawnPixels` counts pixels off the most-used value
- [ ] `CLIENT_VERSION` is 773

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/CollegePacketConverterTests/*"` -> all pass; same for `GuildEmblemPacketConverterTests` and `DrawnPixelsTests`

**Steps:**

- [ ] **Step 1: Write the failing tests.** In `CollegePacketConverterTests.cs`, add after `Piece()`:

```csharp
    private static CollegePieceInfo ArtPiece()
        => new()
        {
            Subject = CollegeSubjectCode.Art,
            Title = "Mileth at dusk",
            Blocks =
            [
                new CollegeBlockInfo
                {
                    Kind = CollegeBlockKind.Drawing,
                    Palette = Enumerable.Range(0, CollegeProtocol.DRAWING_PALETTE_BYTES).Select(i => (byte)(i * 2)).ToArray(),
                    Pixels = Enumerable.Range(0, CollegeProtocol.DRAWING_PIXELS).Select(i => (byte)(i % 32)).ToArray()
                },
                new CollegeBlockInfo { Kind = CollegeBlockKind.Text, Text = "The well at dusk." }
            ]
        };
```

In `Actions()`, add:

```csharp
        yield return new CollegeActionArgs { Type = CollegeActionType.SaveDraft, Subject = CollegeSubjectCode.Art, Piece = ArtPiece() };
        yield return new CollegeActionArgs { Type = CollegeActionType.DrawingFetch, Id = 31 };
```

In `Displays()`, add:

```csharp
        yield return new CollegeDisplayArgs
        {
            Type = CollegeDisplayType.Drawing,
            Id = 31,
            Palette = ArtPiece().Blocks[0].Palette,
            Pixels = ArtPiece().Blocks[0].Pixels
        };
```

Add a test at the end of the class:

```csharp
    [Test]
    public void A_short_drawing_is_written_at_full_size()
    {
        var original = new CollegeActionArgs
        {
            Type = CollegeActionType.HandIn,
            Piece = new CollegePieceInfo
            {
                Subject = CollegeSubjectCode.Art,
                Blocks = [new CollegeBlockInfo { Kind = CollegeBlockKind.Drawing, Palette = [1, 2, 3], Pixels = [7] }]
            }
        };

        var block = RoundTrip(new CollegeActionConverter(), original).Piece!.Blocks.Single();

        block.Palette.Should().HaveCount(CollegeProtocol.DRAWING_PALETTE_BYTES);
        block.Palette[..4].Should().Equal(1, 2, 3, 0);
        block.Pixels.Should().HaveCount(CollegeProtocol.DRAWING_PIXELS);
        block.Pixels[..2].Should().Equal(7, 0);
    }
```

In `GuildEmblemPacketConverterTests.cs`, change the pin:

```csharp
    [Test]
    public void Client_version_is_773()
        => CONSTANTS.CLIENT_VERSION.Should().Be(773);
```

Create `Tests/Chaos.Tests/College/DrawnPixelsTests.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Tests.College;

public sealed class DrawnPixelsTests
{
    [Test]
    public void Drawn_pixels_count_against_the_most_used_value()
    {
        var pixels = Enumerable.Repeat((byte)9, CollegeProtocol.DRAWING_PIXELS).ToArray();

        CollegeProtocol.DrawnPixels(pixels).Should().Be(0);

        for (var i = 0; i < 400; i++)
            pixels[i] = 2;

        CollegeProtocol.DrawnPixels(pixels).Should().Be(400);
    }

    [Test]
    public void A_tie_counts_either_value_as_most_used()
        => CollegeProtocol.DrawnPixels([1, 1, 2, 2]).Should().Be(2);
}
```

- [ ] **Step 2: Run to see them fail.** Run the `CollegePacketConverterTests` filter. Expected: build errors for `DRAWING_PALETTE_BYTES`, `CollegeBlockKind.Drawing`, `DrawingFetch`, `Palette` and the others.

- [ ] **Step 3: Add the constants.** In `CollegeProtocol`, after `MAX_PROMPT_CHARS`:

```csharp
    public const int DRAWING_WIDTH = 96;
    public const int DRAWING_HEIGHT = 72;
    public const int DRAWING_PIXELS = DRAWING_WIDTH * DRAWING_HEIGHT;
    public const int DRAWING_COLOURS = 32;
    public const int DRAWING_PALETTE_BYTES = DRAWING_COLOURS * 3;
    public const int DRAWING_ZOOM = 4;
    public const int MAX_ART_NOTE_CHARS = 300;
    public const int MIN_ENTRY_DRAWN_PIXELS = 346;
    public const int MIN_HAND_IN_DRAWN_PIXELS = 70;
```

and after `PartCount`:

```csharp
    /// <summary>
    ///     Pixels that are not on the drawing's most-used swatch, so a canvas filled with one colour (whichever it is)
    ///     counts as empty.
    /// </summary>
    public static int DrawnPixels(ReadOnlySpan<byte> pixels)
    {
        Span<int> counts = stackalloc int[256];

        foreach (var pixel in pixels)
            counts[pixel]++;

        var most = 0;

        foreach (var count in counts)
            most = Math.Max(most, count);

        return pixels.Length - most;
    }
```

- [ ] **Step 4: Add the enum values.** In `Enums.cs`, in the College region:
  - add `DrawingFetch = 12` after `ShowToClass = 11` in `CollegeActionType`;
  - add `Drawing = 8` after `HandInList = 7` in `CollegeDisplayType`;
  - add `Drawing = 3` after `Picture = 2` in `CollegeBlockKind`.

  In `CONSTANTS.cs`, change `CLIENT_VERSION = 772` to `CLIENT_VERSION = 773`.

- [ ] **Step 5: Add the fields.** Replace `CollegeBlockInfo` in `CollegeInfos.cs` with:

```csharp
/// <summary>
///     One block of a piece: text (may hold line breaks), a one-line heading, a picture named by its hash, or a drawing
///     (palette R, G, B per swatch, then one swatch number per pixel).
/// </summary>
public sealed record CollegeBlockInfo
{
    public CollegeBlockKind Kind { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public byte[] Palette { get; set; } = [];
    public byte[] Pixels { get; set; } = [];
}
```

In `CollegeActionArgs.cs`, change the `Id` summary to `/// <summary>OpenPiece, Vote, Verdict, RemoveEntry, ShowToClass, DrawingFetch.</summary>`.

In `CollegeDisplayArgs.cs`, change the `Id` summary to `/// <summary>Piece: the entry or hand-in id. Drawing: the entry id.</summary>`. Then add before `GalleryRows`:

```csharp
    //Drawing
    public byte[] Palette { get; set; } = [];
    public byte[] Pixels { get; set; } = [];
```

- [ ] **Step 6: Codec.** In `CollegeCodec.ReadPiece`, replace the `kind switch` with:

```csharp
                kind switch
                {
                    CollegeBlockKind.Text    => new CollegeBlockInfo { Kind = kind, Text = reader.ReadString16() },
                    CollegeBlockKind.Heading => new CollegeBlockInfo { Kind = kind, Text = reader.ReadString8() },
                    CollegeBlockKind.Drawing => new CollegeBlockInfo
                    {
                        Kind = kind,
                        Palette = reader.ReadBytes(CollegeProtocol.DRAWING_PALETTE_BYTES),
                        Pixels = reader.ReadBytes(CollegeProtocol.DRAWING_PIXELS)
                    },
                    _ => new CollegeBlockInfo { Kind = kind, Hash = reader.ReadString8() }
                });
```

In `WritePiece`, add before `default:`:

```csharp
                case CollegeBlockKind.Drawing:
                    WriteFixed(ref writer, block.Palette, CollegeProtocol.DRAWING_PALETTE_BYTES);
                    WriteFixed(ref writer, block.Pixels, CollegeProtocol.DRAWING_PIXELS);

                    break;
```

Add the helper after `WriteOptionalPiece`:

```csharp
    /// <summary>Writes exactly <paramref name="length" /> bytes: short data is padded with zeros, long data is cut.</summary>
    public static void WriteFixed(ref SpanWriter writer, byte[] bytes, int length)
    {
        if (bytes.Length == length)
        {
            writer.WriteBytes(bytes);

            return;
        }

        var sized = new byte[length];
        bytes.AsSpan(0, Math.Min(bytes.Length, length)).CopyTo(sized);
        writer.WriteBytes(sized);
    }
```

- [ ] **Step 7: Converters.** In `CollegeActionConverter.Deserialize`, add `case CollegeActionType.DrawingFetch:` to the `RemoveEntry` / `ShowToClass` group (it reads `args.Id = reader.ReadInt32();`). Do the same in `Serialize`.

In `CollegeDisplayConverter.Deserialize`, add before the closing brace of the switch:

```csharp
            case CollegeDisplayType.Drawing:
                args.Id = reader.ReadInt32();
                args.Palette = reader.ReadBytes(CollegeProtocol.DRAWING_PALETTE_BYTES);
                args.Pixels = reader.ReadBytes(CollegeProtocol.DRAWING_PIXELS);

                break;
```

and in `Serialize`:

```csharp
            case CollegeDisplayType.Drawing:
                writer.WriteInt32(args.Id);
                CollegeCodec.WriteFixed(ref writer, args.Palette, CollegeProtocol.DRAWING_PALETTE_BYTES);
                CollegeCodec.WriteFixed(ref writer, args.Pixels, CollegeProtocol.DRAWING_PIXELS);

                break;
```

- [ ] **Step 8: Run the tests.** Run the `CollegePacketConverterTests`, `GuildEmblemPacketConverterTests` and `DrawnPixelsTests` filters. Expected: all pass.

```json:metadata
{"files": ["Chaos.DarkAges/Definitions/CollegeProtocol.cs", "Chaos.DarkAges/Definitions/Enums.cs", "Chaos.DarkAges/Definitions/CONSTANTS.cs", "Chaos.Networking/Entities/Server/CollegeInfos.cs", "Chaos.Networking/Entities/Client/CollegeActionArgs.cs", "Chaos.Networking/Entities/Server/CollegeDisplayArgs.cs", "Chaos.Networking/Converters/CollegeCodec.cs", "Chaos.Networking/Converters/Client/CollegeActionConverter.cs", "Chaos.Networking/Converters/Server/CollegeDisplayConverter.cs", "Tests/Chaos.Tests/Networking/CollegePacketConverterTests.cs", "Tests/Chaos.Tests/Networking/GuildEmblemPacketConverterTests.cs", "Tests/Chaos.Tests/College/DrawnPixelsTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/*/CollegePacketConverterTests/*\"", "acceptanceCriteria": ["every sub-type round-trips incl. drawing block, DrawingFetch, Drawing", "short drawing written at full size", "DrawnPixels counts off the most-used value", "CLIENT_VERSION 773"], "modelTier": "mechanical"}
```

---

### Task 2: Server Art pieces: model, rules, mapping, storage (server repo)

**Goal:** The server stores and checks Art pieces: one valid drawing, an optional note of up to 300 characters, the drawn-pixel minimums, and lossless mapping to and from messages.

**Files:**
- Modify: `Chaos/Services/College/CollegePiece.cs`
- Create: `Chaos/Services/College/DrawingRules.cs`
- Modify: `Chaos/Services/College/PieceRules.cs`
- Modify: `Chaos/Services/College/CollegeText.cs`
- Modify: `Chaos/Services/College/CollegePanel.cs` (`ToPiece`, `ToInfo`)
- Test: `Tests/Chaos.Tests/College/PieceRulesTests.cs`, `CollegePanelMappingTests.cs`, `CollegePieceStoreTests.cs`

**Acceptance Criteria:**
- [ ] An Art piece needs exactly one drawing: 32 `#rrggbb` colours and 6,912 pixel values below 32. Otherwise it gets `BadDrawing`.
- [ ] Headings and pictures in Art, or a drawing outside Art, give `WrongBlock`
- [ ] An Art note over 300 characters gives `TooMuchText`
- [ ] Art Submit needs a title and 346 drawn pixels; an Art hand-in needs 70. Below that is `TooLittleDrawn`.
- [ ] `ToPiece` then `ToInfo` round-trips a drawing exactly
- [ ] A drawing piece file round-trips; a writing piece file has no `Palette` or `Pixels` keys

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/*/PieceRulesTests/*"` -> all pass; same for `CollegePanelMappingTests`, `CollegePieceStoreTests`

**Steps:**

- [ ] **Step 1: Write the failing tests.** Add to `PieceRulesTests` (add `using Chaos.DarkAges.Definitions;`):

```csharp
    private static PieceBlock Drawing(int drawn = 400, int colours = 32, int pixels = CollegeProtocol.DRAWING_PIXELS, byte value = 5)
    {
        var cells = new byte[pixels];

        for (var i = 0; i < Math.Min(drawn, pixels); i++)
            cells[i] = value;

        return new PieceBlock { Type = PieceBlockType.Drawing, Palette = Enumerable.Repeat("#112233", colours).ToList(), Pixels = cells };
    }

    private static CollegePiece Art(string title, params PieceBlock[] blocks)
        => new() { Id = CollegePiece.NewId(), Subject = CollegeSubject.Art, Title = title, Blocks = blocks.ToList() };

    [Test]
    public void An_art_piece_needs_one_whole_drawing()
    {
        PieceRules.Check(Art("t"), Exists).Should().Be(PieceCheck.BadDrawing);
        PieceRules.Check(Art("t", Drawing(), Drawing()), Exists).Should().Be(PieceCheck.BadDrawing);
        PieceRules.Check(Art("t", Drawing(colours: 31)), Exists).Should().Be(PieceCheck.BadDrawing);
        PieceRules.Check(Art("t", Drawing(colours: 33)), Exists).Should().Be(PieceCheck.BadDrawing);
        PieceRules.Check(Art("t", Drawing(pixels: CollegeProtocol.DRAWING_PIXELS - 1)), Exists).Should().Be(PieceCheck.BadDrawing);
        PieceRules.Check(Art("t", Drawing(pixels: CollegeProtocol.DRAWING_PIXELS + 1)), Exists).Should().Be(PieceCheck.BadDrawing);
        PieceRules.Check(Art("t", Drawing(value: 32)), Exists).Should().Be(PieceCheck.BadDrawing);
        PieceRules.Check(Art("t", Drawing(value: 31)), Exists).Should().Be(PieceCheck.Ok);
    }

    [Test]
    public void A_bad_colour_code_is_a_bad_drawing()
    {
        var drawing = Drawing();
        drawing.Palette![3] = "red";

        PieceRules.Check(Art("t", drawing), Exists).Should().Be(PieceCheck.BadDrawing);
    }

    [Test]
    public void An_art_note_holds_three_hundred_characters()
    {
        PieceRules.Check(Art("t", Drawing(), Text(new string('n', 300))), Exists).Should().Be(PieceCheck.Ok);
        PieceRules.Check(Art("t", Drawing(), Text(new string('n', 301))), Exists).Should().Be(PieceCheck.TooMuchText);
    }

    [Test]
    public void Art_holds_no_headings_or_pictures_and_writing_holds_no_drawings()
    {
        PieceRules.Check(Art("t", Drawing(), Heading("h")), Exists).Should().Be(PieceCheck.WrongBlock);
        PieceRules.Check(Art("t", Drawing(), Picture(HashA)), Exists).Should().Be(PieceCheck.WrongBlock);
        PieceRules.Check(Piece("t", Text("x"), Drawing()), Exists).Should().Be(PieceCheck.WrongBlock);
    }

    [Test]
    public void An_art_entry_needs_a_title_and_346_drawn_pixels()
    {
        PieceRules.CheckEntry(Art("", Drawing(400)), Exists).Should().Be(PieceCheck.NoTitle);
        PieceRules.CheckEntry(Art("t", Drawing(345)), Exists).Should().Be(PieceCheck.TooLittleDrawn);
        PieceRules.CheckEntry(Art("t", Drawing(346)), Exists).Should().Be(PieceCheck.Ok);
    }

    [Test]
    public void An_art_hand_in_needs_70_drawn_pixels()
    {
        PieceRules.CheckHandIn(Art("", Drawing(69)), Exists).Should().Be(PieceCheck.TooLittleDrawn);
        PieceRules.CheckHandIn(Art("", Drawing(70)), Exists).Should().Be(PieceCheck.Ok);
    }

    [Test]
    public void A_canvas_filled_with_one_colour_draws_nothing()
    {
        var full = Drawing(CollegeProtocol.DRAWING_PIXELS, value: 7);

        PieceRules.DrawnPixels(Art("t", full)).Should().Be(0);
    }

    [Test]
    public void Normalize_keeps_a_drawing_with_lower_case_colours()
    {
        var drawing = Drawing();
        drawing.Palette![0] = " #AABBCC ";

        var clean = PieceRules.Normalize(Art("t", drawing));

        clean.Blocks.Single().Palette![0].Should().Be("#aabbcc");
        clean.Blocks.Single().Pixels.Should().Equal(drawing.Pixels);
    }
```

Add to `CollegePanelMappingTests`:

```csharp
    [Test]
    public void Drawings_map_both_ways()
    {
        var info = new CollegePieceInfo
        {
            Subject = CollegeSubjectCode.Art,
            Title = "Dusk",
            Blocks =
            [
                new CollegeBlockInfo
                {
                    Kind = CollegeBlockKind.Drawing,
                    Palette = Enumerable.Range(0, CollegeProtocol.DRAWING_PALETTE_BYTES).Select(i => (byte)(i * 2)).ToArray(),
                    Pixels = Enumerable.Range(0, CollegeProtocol.DRAWING_PIXELS).Select(i => (byte)(i % 32)).ToArray()
                },
                new CollegeBlockInfo { Kind = CollegeBlockKind.Text, Text = "A note" }
            ]
        };

        var piece = CollegePanel.ToPiece(info, CollegeSubject.Art);

        piece.Kind.Should().Be(PieceKind.Drawing);
        piece.Blocks[0].Palette![1].Should().Be("#06080a");
        CollegePanel.ToInfo(piece).Should().BeEquivalentTo(info);
    }
```

Add to `CollegePieceStoreTests`. First look at how the file's existing tests make a store and find its folder, and use the same names. The code below assumes a `Store(out var dir)` helper; rename it to match the file:

```csharp
    [Test]
    public void A_drawing_round_trips_and_writing_has_no_drawing_keys()
    {
        var store = Store(out var dir);
        var drawing = new CollegePiece
        {
            Id = CollegePiece.NewId(),
            Subject = CollegeSubject.Art,
            Kind = PieceKind.Drawing,
            Title = "Dusk",
            Blocks =
            [
                new PieceBlock
                {
                    Type = PieceBlockType.Drawing,
                    Palette = Enumerable.Repeat("#112233", 32).ToList(),
                    Pixels = Enumerable.Range(0, 6912).Select(i => (byte)(i % 32)).ToArray()
                }
            ]
        };
        var writing = new CollegePiece { Id = CollegePiece.NewId(), Subject = CollegeSubject.Lore, Title = "Words", Blocks = [new PieceBlock { Type = PieceBlockType.Text, Text = "x" }] };

        store.Save(drawing);
        store.Save(writing);

        store.Load(drawing.Id)!.Blocks.Single().Pixels.Should().Equal(drawing.Blocks[0].Pixels);
        store.Load(drawing.Id)!.Blocks.Single().Palette.Should().Equal(drawing.Blocks[0].Palette);
        var text = File.ReadAllText(Path.Combine(dir, writing.Id + ".json"));
        text.Should().NotContain("Palette").And.NotContain("Pixels");
    }
```

- [ ] **Step 2: Run to see them fail.** Run the `PieceRulesTests` filter. Expected: build errors (`PieceBlockType.Drawing`, `Palette`, `BadDrawing` and the others).

- [ ] **Step 3: Model.** Replace `CollegePiece.cs` with:

```csharp
using System.Text.Json.Serialization;

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
    Picture,
    Drawing
}

public sealed class PieceBlock
{
    public PieceBlockType Type { get; set; }
    public string Text { get; set; } = "";
    public string Hash { get; set; } = "";

    /// <summary>Drawing blocks: 32 colours as "#rrggbb".</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Palette { get; set; }

    /// <summary>Drawing blocks: one swatch number per pixel, row by row (saved as base64).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte[]? Pixels { get; set; }
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

    public static PieceKind KindOf(CollegeSubject subject) => subject == CollegeSubject.Art ? PieceKind.Drawing : PieceKind.Writing;

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
    TooShort,
    BadDrawing,
    WrongBlock,
    TooLittleDrawn
}
```

- [ ] **Step 4: DrawingRules.** Create `Chaos/Services/College/DrawingRules.cs`:

```csharp
using System.Globalization;
using Chaos.DarkAges.Definitions;

namespace Chaos.Services.College;

/// <summary>Drawing-block checks and the palette's two forms: "#rrggbb" strings on disk, R, G, B bytes in messages. No game-world access.</summary>
public static class DrawingRules
{
    public static bool IsColour(string text)
        => (text.Length == 7) && (text[0] == '#') && text.Skip(1).All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    public static bool IsValid(PieceBlock block)
        => block is { Type: PieceBlockType.Drawing, Palette: { Count: CollegeProtocol.DRAWING_COLOURS } palette, Pixels: { Length: CollegeProtocol.DRAWING_PIXELS } pixels }
           && palette.All(IsColour)
           && pixels.All(p => p < CollegeProtocol.DRAWING_COLOURS);

    public static List<string> PaletteToHex(byte[] palette)
    {
        var colours = new List<string>(palette.Length / 3);

        for (var i = 0; (i * 3) + 2 < palette.Length; i++)
            colours.Add($"#{palette[i * 3]:x2}{palette[(i * 3) + 1]:x2}{palette[(i * 3) + 2]:x2}");

        return colours;
    }

    /// <summary>Three bytes per colour; a colour that isn't "#rrggbb" becomes black.</summary>
    public static byte[] PaletteToBytes(IReadOnlyList<string>? palette)
    {
        if (palette is null)
            return [];

        var bytes = new byte[palette.Count * 3];

        for (var i = 0; i < palette.Count; i++)
        {
            var colour = palette[i].Trim().ToLowerInvariant();

            if (!IsColour(colour))
                continue;

            for (var c = 0; c < 3; c++)
                bytes[(i * 3) + c] = byte.Parse(colour.AsSpan(1 + (c * 2), 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        return bytes;
    }
}
```

- [ ] **Step 5: PieceRules.** Replace `PieceRules.cs` with:

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
                case PieceBlockType.Drawing:
                    blocks.Add(
                        new PieceBlock
                        {
                            Type = PieceBlockType.Drawing,
                            Palette = block.Palette?.Select(c => c.Trim().ToLowerInvariant()).ToList(),
                            Pixels = block.Pixels?.ToArray()
                        });

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

    public static int TextLength(CollegePiece piece)
        => piece.Blocks.Where(b => b.Type is PieceBlockType.Text or PieceBlockType.Heading).Sum(b => b.Text.Length);

    public static int DrawnPixels(CollegePiece piece)
        => piece.Blocks.FirstOrDefault(b => b.Type == PieceBlockType.Drawing)?.Pixels is { } pixels ? CollegeProtocol.DrawnPixels(pixels) : 0;

    public static bool IsHash(string hash)
        => (hash.Length == CollegeProtocol.HASH_CHARS) && hash.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    public static PieceCheck Check(CollegePiece piece, Func<string, bool> pictureExists)
    {
        if (piece.Subject == CollegeSubject.Art)
            return CheckArt(piece);

        if (piece.Blocks.Any(b => b.Type == PieceBlockType.Drawing))
            return PieceCheck.WrongBlock;

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

        if (piece.Subject == CollegeSubject.Art)
            return DrawnPixels(piece) < CollegeProtocol.MIN_ENTRY_DRAWN_PIXELS ? PieceCheck.TooLittleDrawn : PieceCheck.Ok;

        return TextLength(piece) < CollegeProtocol.MIN_ENTRY_CHARS ? PieceCheck.TooShort : PieceCheck.Ok;
    }

    public static PieceCheck CheckHandIn(CollegePiece piece, Func<string, bool> pictureExists)
    {
        var check = Check(piece, pictureExists);

        if (check != PieceCheck.Ok)
            return check;

        if (piece.Subject == CollegeSubject.Art)
            return DrawnPixels(piece) < CollegeProtocol.MIN_HAND_IN_DRAWN_PIXELS ? PieceCheck.TooLittleDrawn : PieceCheck.Ok;

        return TextLength(piece) < CollegeProtocol.MIN_HAND_IN_CHARS ? PieceCheck.TooShort : PieceCheck.Ok;
    }

    //an Art piece is a title, one drawing and at most one text block, the artist's note
    private static PieceCheck CheckArt(CollegePiece piece)
    {
        if (piece.Title.Length > CollegeProtocol.MAX_TITLE_CHARS)
            return PieceCheck.TitleTooLong;

        if (piece.Blocks.Any(b => b.Type is PieceBlockType.Heading or PieceBlockType.Picture)
            || (piece.Blocks.Count(b => b.Type == PieceBlockType.Text) > 1))
            return PieceCheck.WrongBlock;

        var drawings = piece.Blocks.Where(b => b.Type == PieceBlockType.Drawing).ToList();

        if ((drawings.Count != 1) || !DrawingRules.IsValid(drawings[0]))
            return PieceCheck.BadDrawing;

        return TextLength(piece) > CollegeProtocol.MAX_ART_NOTE_CHARS ? PieceCheck.TooMuchText : PieceCheck.Ok;
    }
}
```

- [ ] **Step 6: Texts.** In `CollegeText.cs`, add after `NOT_OPEN`:

```csharp
    public const string DRAW_MORE = "Draw more before entering it.";
    public const string DRAW_MORE_HAND_IN = "Draw more before handing it in.";
```

and replace `PieceProblem` with:

```csharp
    public static string PieceProblem(PieceCheck check, bool art = false)
        => check switch
        {
            PieceCheck.TooMuchText when art => "Notes can be 300 characters at most.",
            PieceCheck.NoTitle when art     => "Give your drawing a title.",
            PieceCheck.WrongBlock when art  => "Art pieces hold one drawing and a note.",
            PieceCheck.TooManyBlocks        => "That piece has too many parts.",
            PieceCheck.TitleTooLong         => "Titles can be 40 characters at most.",
            PieceCheck.HeadingTooLong       => "Headings can be 60 characters at most.",
            PieceCheck.TooMuchText          => "Pieces can be 10,000 characters at most.",
            PieceCheck.TooManyPictures      => "Pieces can hold 5 pictures at most.",
            PieceCheck.BadHash              => "A picture is damaged. Insert it again.",
            PieceCheck.DuplicatePicture     => "A picture appears twice.",
            PieceCheck.MissingPicture       => "A picture didn't upload. Insert it again.",
            PieceCheck.NoTitle              => ENTRY_TOO_SHORT,
            PieceCheck.TooShort             => ENTRY_TOO_SHORT,
            PieceCheck.BadDrawing           => "That drawing is damaged. Save it again.",
            PieceCheck.WrongBlock           => "That piece holds a drawing it can't.",
            PieceCheck.TooLittleDrawn       => DRAW_MORE,
            _                               => string.Empty
        };
```

- [ ] **Step 7: Mapping.** In `CollegePanel.cs`, replace `ToPiece` and `ToInfo` with:

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
                                     _                        => PieceBlockType.Text
                                 },
                                 Text = b.Kind is CollegeBlockKind.Text or CollegeBlockKind.Heading ? b.Text : "",
                                 Hash = b.Kind == CollegeBlockKind.Picture ? b.Hash : "",
                                 Palette = b.Kind == CollegeBlockKind.Drawing ? DrawingRules.PaletteToHex(b.Palette) : null,
                                 Pixels = b.Kind == CollegeBlockKind.Drawing ? b.Pixels.ToArray() : null
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
                                      PieceBlockType.Drawing => CollegeBlockKind.Drawing,
                                      _                      => CollegeBlockKind.Text
                                  },
                                  Text = b.Text,
                                  Hash = b.Hash,
                                  Palette = b.Type == PieceBlockType.Drawing ? DrawingRules.PaletteToBytes(b.Palette) : [],
                                  Pixels = b.Type == PieceBlockType.Drawing ? b.Pixels ?? [] : []
                              })
                          .ToList()
        };
```

- [ ] **Step 8: Run the tests.** Run `PieceRulesTests`, `CollegePanelMappingTests` and `CollegePieceStoreTests`. Expected: all pass. Then run all College tests (`--treenode-filter "/*/Chaos.Tests.College/*/*"`). Expected: they all pass, except the Art-closed tests Task 3 updates. Those are `CollegeEntryServiceTests.Saving_refuses_closed_subjects_and_broken_pieces`, `EntryBookTests.Submit_checks_subject_open_entry_draft_and_marks` and two `TimetableTests`. They still pass here, because Art is not open until Task 3.

```json:metadata
{"files": ["Chaos/Services/College/CollegePiece.cs", "Chaos/Services/College/DrawingRules.cs", "Chaos/Services/College/PieceRules.cs", "Chaos/Services/College/CollegeText.cs", "Chaos/Services/College/CollegePanel.cs", "Tests/Chaos.Tests/College/PieceRulesTests.cs", "Tests/Chaos.Tests/College/CollegePanelMappingTests.cs", "Tests/Chaos.Tests/College/CollegePieceStoreTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/Chaos.Tests.College/*/*\"", "acceptanceCriteria": ["Art piece shape and drawing validity", "WrongBlock both ways", "note 300", "346 / 70 drawn-pixel minimums", "drawing mapping round trip", "piece file round trip without drawing keys on writing"], "modelTier": "standard"}
```

---

### Task 3: Server service: open Art, Art activities and hand-ins, DrawingFetch (server repo)

**Goal:** Art entries and Art activity classes work end to end on the server, and the gallery can fetch one claimed drawing by entry id.

**Files:**
- Modify: `Chaos/Services/College/CollegeOptions.cs`
- Modify: `Chaos/Services/College/CollegeService.cs` (`StartWalkIn`)
- Modify: `Chaos/Services/College/Timetable.cs` (`TryBook`)
- Modify: `Chaos/Services/College/CollegeService.Entries.cs` (`SaveDraft`, `Submit`, `HandIn`, new `GalleryDrawing`)
- Modify: `Chaos/Services/College/CollegeText.cs` (`ClassBegun`)
- Modify: `Chaos/Services/College/CollegePanel.cs` (`Handle`, `SendPicture`, new `SendDrawing`)
- Test: `Tests/Chaos.Tests/College/CollegeEntryServiceTests.cs`, `EntryBookTests.cs`, `TimetableTests.cs`, `CollegeServiceTests.cs`

**Acceptance Criteria:**
- [ ] Art drafts save and submit, with the Art messages for each refusal
- [ ] Art Activity classes can be booked and started; Music Activity still gives `FormatNotAvailable`
- [ ] Hand-ins need a running Activity class in Art or a writing subject, in its room. The piece is checked as that class's subject, so a drawing in a writing class and writing in an Art class are both refused.
- [ ] `GalleryDrawing(id)` returns the drawing only for a `Claimed`, not hidden, Art entry
- [ ] `DrawingFetch` is answered outside the 250 ms throttle, at most 60 a minute per player
- [ ] An Art activity class is announced with " (drawing class)"

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter "/*/Chaos.Tests.College/*/*"` -> all pass

**Steps:**

- [ ] **Step 1: Update the tests that expected Art to be closed.** Run `grep -rn "CollegeSubject.Art" Tests/Chaos.Tests/College/` and change these:
  - `CollegeEntryServiceTests.Saving_refuses_closed_subjects_and_broken_pieces`: `CollegeSubject.Art` -> `CollegeSubject.Music`.
  - `EntryBookTests.Submit_checks_subject_open_entry_draft_and_marks`: `CollegeSubject.Art` -> `CollegeSubject.Music`.
  - `TimetableTests` (around line 46): the bad-booking test's `ClassFormat.Activity, CollegeSubject.Art` -> `CollegeSubject.Music`.
  - `TimetableTests.Activity_can_be_booked_for_writing_subjects_only`: rename it to `Activity_can_be_booked_for_every_subject_but_music` and replace its body with:

```csharp
        var state = new CollegeState();

        Book(state, OneOClock, 30, ClassFormat.Activity, CollegeSubject.Music).Should().Be(BookResult.FormatNotAvailable);
        Book(state, OneOClock, 30, ClassFormat.Activity, CollegeSubject.Art).Should().Be(BookResult.Booked);
        Book(state, OneOClock.AddHours(1), 30, ClassFormat.Activity, CollegeSubject.History).Should().Be(BookResult.Booked);
```

  Also run `grep -rn "FormatNotAvailable" Tests/Chaos.Tests/College/`. Any other test that expects it for **Art** changes to Music the same way.

- [ ] **Step 2: Write the failing tests.** Add to `CollegeEntryServiceTests` (add `using Chaos.DarkAges.Definitions;` if missing):

```csharp
    private static CollegePiece ArtPiece(string title, int drawn, string note = "")
    {
        var pixels = new byte[CollegeProtocol.DRAWING_PIXELS];

        for (var i = 0; i < drawn; i++)
            pixels[i] = 5;

        var blocks = new List<PieceBlock>
        {
            new() { Type = PieceBlockType.Drawing, Palette = Enumerable.Repeat("#112233", 32).ToList(), Pixels = pixels }
        };

        if (note.Length > 0)
            blocks.Add(new PieceBlock { Type = PieceBlockType.Text, Text = note });

        return new CollegePiece { Title = title, Blocks = blocks };
    }

    [Test]
    public void An_art_draft_saves_and_submits()
    {
        var rig = Create();
        rig.State.StudentFor("Aroha").Marks += 3;

        rig.Service.SaveDraft("Aroha", CollegeSubject.Art, ArtPiece("Dusk", 345)).Result.Should().Be(CollegeWriterResult.Saved);
        rig.Service.Submit("Aroha", CollegeSubject.Art).Message.Should().Be(CollegeText.DRAW_MORE);
        rig.Service.SaveDraft("Aroha", CollegeSubject.Art, ArtPiece("", 400)).Result.Should().Be(CollegeWriterResult.Saved);
        rig.Service.Submit("Aroha", CollegeSubject.Art).Message.Should().Be("Give your drawing a title.");

        rig.Service.SaveDraft("Aroha", CollegeSubject.Art, ArtPiece("Dusk", 346, "The well at dusk.")).Result.Should().Be(CollegeWriterResult.Saved);
        rig.Service.Submit("Aroha", CollegeSubject.Art).Result.Should().Be(CollegeWriterResult.Submitted);

        var piece = rig.Pieces.Load(rig.State.Entries.Single().PieceId)!;
        piece.Kind.Should().Be(PieceKind.Drawing);
        piece.Blocks.Select(b => b.Type).Should().Equal(PieceBlockType.Drawing, PieceBlockType.Text);
        piece.Blocks[0].Pixels.Should().Equal(ArtPiece("Dusk", 346).Blocks[0].Pixels);
    }

    [Test]
    public void Art_and_writing_drafts_refuse_each_others_pieces()
    {
        var rig = Create();

        rig.Service.SaveDraft("Aroha", CollegeSubject.Art, Writing("Words", 300)).Result.Should().Be(CollegeWriterResult.Refused);
        rig.Service.SaveDraft("Aroha", CollegeSubject.History, ArtPiece("Dusk", 400)).Result.Should().Be(CollegeWriterResult.Refused);
    }

    [Test]
    public void Art_hand_ins_need_an_art_activity_class()
    {
        var rig = Create();
        rig.Service.StartWalkIn("Sir", "m0", CollegeSubject.Art, ClassFormat.Activity, "college_creation", true, false)
           .Result.Should().Be(StartResult.Started);

        rig.Service.HandIn("Aroha", "college_creation", ArtPiece("", 69)).Message.Should().Be(CollegeText.DRAW_MORE_HAND_IN);
        rig.Service.HandIn("Aroha", "college_creation", Writing("Words", 30)).Result.Should().Be(CollegeWriterResult.Refused);
        rig.Service.HandIn("Aroha", "college_creation", ArtPiece("", 70)).Result.Should().Be(CollegeWriterResult.HandedIn);

        var handIn = rig.State.HandIns.Single();
        handIn.Subject.Should().Be(CollegeSubject.Art);
        handIn.Title.Should().Be("Untitled");
        rig.Pieces.Load(handIn.PieceId)!.Subject.Should().Be(CollegeSubject.Art);
    }

    [Test]
    public void Writing_classes_refuse_drawings()
    {
        var rig = Create();
        rig.Service.StartWalkIn("Sir", "m0", CollegeSubject.Lore, ClassFormat.Activity, "college_codex", true, false)
           .Result.Should().Be(StartResult.Started);

        rig.Service.HandIn("Aroha", "college_codex", ArtPiece("", 400)).Result.Should().Be(CollegeWriterResult.Refused);
        rig.Service.HandIn("Aroha", "college_codex", Writing("Words", 30)).Result.Should().Be(CollegeWriterResult.HandedIn);
    }

    [Test]
    public void Gallery_drawings_come_only_from_visible_claimed_art()
    {
        var rig = Create();
        rig.Service.SetDirector("Dir");
        rig.State.StudentFor("Aroha").Marks += 3;
        rig.Service.SaveDraft("Aroha", CollegeSubject.Art, ArtPiece("Dusk", 400));
        rig.Service.Submit("Aroha", CollegeSubject.Art).Result.Should().Be(CollegeWriterResult.Submitted);
        var id = rig.State.Entries.Single().Id;

        rig.Service.GalleryDrawing(id).Should().BeNull();

        rig.Service.CloseVoting(id);
        rig.Service.PostVerdict("Dir", false, id, AwardTier.Village, "").Should().Be(VerdictResult.Posted);
        rig.Service.Claim("Aroha", id, out _, out _).Should().Be(ClaimResult.Claimed);

        rig.Service.GalleryDrawing(id)!.Pixels.Should().HaveCount(CollegeProtocol.DRAWING_PIXELS);

        rig.Service.SetHidden("Aroha", id, true);
        rig.Service.GalleryDrawing(id).Should().BeNull();
        rig.Service.GalleryDrawing(999).Should().BeNull();

        var history = SubmitOne(rig, "Brannoc");
        rig.Service.CloseVoting(history);
        rig.Service.PostVerdict("Dir", false, history, AwardTier.Village, "");
        rig.Service.Claim("Brannoc", history, out _, out _).Should().Be(ClaimResult.Claimed);
        rig.Service.GalleryDrawing(history).Should().BeNull();
    }
```

Add to `CollegeServiceTests`:

```csharp
    [Test]
    public void An_art_activity_is_announced_as_a_drawing_class()
        => CollegeText.ClassBegun(CollegeSubject.Art, "Sir", ClassFormat.Activity).Should().Be("A Art class has begun! (drawing class)");
```

- [ ] **Step 3: Run to see them fail.** Run the `CollegeEntryServiceTests` filter. Expected: build error (`GalleryDrawing` missing).

- [ ] **Step 4: Options.** In `CollegeOptions.cs`, replace the `OpenSubjects` summary and default with:

```csharp
    /// <summary>Subjects whose entries are open. Music joins when its composer ships (part 4).</summary>
    public List<CollegeSubject> OpenSubjects { get; set; } =
        [CollegeSubject.Art, CollegeSubject.History, CollegeSubject.Literature, CollegeSubject.Lore, CollegeSubject.Philosophy];
```

and add after `IsWritingSubject`:

```csharp
    /// <summary>Subjects whose classes can be Activities: the writing subjects and Art. Music waits for its composer.</summary>
    public static bool HasActivity(CollegeSubject subject) => subject != CollegeSubject.Music;
```

- [ ] **Step 5: Activity gates.**
  - In `CollegeService.StartWalkIn` and `Timetable.TryBook`, change `!CollegeOptions.IsWritingSubject(subject)` to `!CollegeOptions.HasActivity(subject)`.
  - In `CollegeText.ClassBegun`, replace `private const string WRITING_CLASS = " (writing class)";` with:

```csharp
    private const string WRITING_CLASS = " (writing class)";
    private const string DRAWING_CLASS = " (drawing class)";
```

  - In the method, add a first line `var label = subject == CollegeSubject.Art ? DRAWING_CLASS : WRITING_CLASS;`. Then replace every `WRITING_CLASS` in the method body with `label`.
  - Change its summary to: `A writing class ends with " (writing class)" and a drawing class with " (drawing class)" when that fits; the prompt is never shown here.`

- [ ] **Step 6: Entries.** In `CollegeService.Entries.cs`:

  1. In `SaveDraft`, set the subject before the check, and pass the Art flag. The start of the method body after the `IsOpen` check becomes:

```csharp
        var clean = PieceRules.Normalize(piece);
        clean.Subject = subject;
        clean.Kind = CollegePiece.KindOf(subject);
        var check = PieceRules.Check(clean, Pictures.Has);

        if (check != PieceCheck.Ok)
            return Refused(CollegeText.PieceProblem(check, subject == CollegeSubject.Art));

        clean.Id = State.DraftOf(name, subject) ?? CollegePiece.NewId();
        clean.UpdatedUtc = Now;
```

     (Remove the old `clean.Subject = subject;` line further down.)

  2. In `Submit`, change `return Refused(CollegeText.PieceProblem(check));` to `return Refused(CollegeText.PieceProblem(check, subject == CollegeSubject.Art));`.

  3. In `HandIn`, replace everything from the first `if` through the check with:

```csharp
        if (State.RunningClass is not { Format: ClassFormat.Activity } running
            || !CollegeOptions.HasActivity(running.Subject)
            || !running.Room.Equals(roomInstanceId, StringComparison.OrdinalIgnoreCase))
            return Refused("You can hand in only during an activity class.");

        if (running.Teacher.Equals(name, StringComparison.OrdinalIgnoreCase) || running.Removed.Contains(CollegeState.Key(name)))
            return Refused("You can't hand in to this class.");

        //the piece is checked as the class's subject, so a drawing can't go to a writing class or writing to an Art class
        var clean = PieceRules.Normalize(piece);
        clean.Subject = running.Subject;
        clean.Kind = CollegePiece.KindOf(running.Subject);
        var check = PieceRules.CheckHandIn(clean, Pictures.Has);

        if (check != PieceCheck.Ok)
            return Refused(
                check switch
                {
                    PieceCheck.TooShort       => "Hand-ins need at least 20 characters.",
                    PieceCheck.TooLittleDrawn => CollegeText.DRAW_MORE_HAND_IN,
                    _                         => CollegeText.PieceProblem(check, running.Subject == CollegeSubject.Art)
                });

        clean.Id = CollegePiece.NewId();
        clean.UpdatedUtc = Now;
```

     (Remove the old `clean.Subject = running.Subject;` line that followed.)

  4. Add after `Gallery`:

```csharp
    /// <summary>A gallery thumbnail: the drawing of a claimed, shown Art entry, or null.</summary>
    public PieceBlock? GalleryDrawing(int id)
    {
        using var scope = Sync.EnterScope();

        if (EntryBook.Find(State, id) is not { Subject: CollegeSubject.Art, Status: EntryStatus.Claimed, Hidden: false } entry)
            return null;

        return Pieces.Load(entry.PieceId)?.Blocks.FirstOrDefault(b => (b.Type == PieceBlockType.Drawing) && DrawingRules.IsValid(b));
    }
```

- [ ] **Step 7: Panel.** In `CollegePanel.cs`:
  - Add the constant `private const int DRAWING_FETCHES_PER_MINUTE = 60;` and the field `private readonly Dictionary<string, List<DateTime>> DrawingFetches = new(StringComparer.OrdinalIgnoreCase);`.
  - In `Handle`, add to the first switch (the one with the picture actions):

```csharp
            case CollegeActionType.DrawingFetch:
                SendDrawing(viewer, args.Id);

                return;
```

  - Replace the rate-limit block at the top of `SendPicture` with `if (!TakeFetch(Fetches, viewer.Name, FETCHES_PER_MINUTE)) return;`, and add:

```csharp
    private bool TakeFetch(Dictionary<string, List<DateTime>> log, string name, int perMinute)
    {
        using var scope = FetchSync.EnterScope();

        if (!log.TryGetValue(name, out var times))
            log[name] = times = [];

        times.RemoveAll(t => Now - t >= TimeSpan.FromMinutes(1));

        if (times.Count >= perMinute)
            return false;

        times.Add(Now);

        return true;
    }

    //thumbnails for the gallery's Art tab; a page asks for up to 8 at once, so they skip the 250 ms throttle
    private void SendDrawing(Aisling viewer, int id)
    {
        if (!TakeFetch(DrawingFetches, viewer.Name, DRAWING_FETCHES_PER_MINUTE) || college.GalleryDrawing(id) is not { } drawing)
            return;

        viewer.Client.SendCollegeDisplay(
            new CollegeDisplayArgs
            {
                Type = CollegeDisplayType.Drawing,
                Id = id,
                Palette = DrawingRules.PaletteToBytes(drawing.Palette),
                Pixels = drawing.Pixels!
            });
    }
```

- [ ] **Step 8: Run the tests.** Run `--treenode-filter "/*/Chaos.Tests.College/*/*"`. Expected: all pass. Then run `dotnet build Chaos.slnx`. Expected: Build succeeded. The Registrar and lectern still compile, because `IsWritingSubject` remains.

```json:metadata
{"files": ["Chaos/Services/College/CollegeOptions.cs", "Chaos/Services/College/CollegeService.cs", "Chaos/Services/College/Timetable.cs", "Chaos/Services/College/CollegeService.Entries.cs", "Chaos/Services/College/CollegeText.cs", "Chaos/Services/College/CollegePanel.cs", "Tests/Chaos.Tests/College/CollegeEntryServiceTests.cs", "Tests/Chaos.Tests/College/EntryBookTests.cs", "Tests/Chaos.Tests/College/TimetableTests.cs", "Tests/Chaos.Tests/College/CollegeServiceTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj -- --treenode-filter \"/*/Chaos.Tests.College/*/*\"", "acceptanceCriteria": ["Art drafts save and submit with Art messages", "Art activity bookable/startable, Music not", "hand-ins checked as the class subject", "GalleryDrawing only for visible claimed Art", "DrawingFetch outside the throttle, 60/min", "drawing class announcement"], "modelTier": "standard"}
```

---

### Task 4: Registrar, lectern and dialog text (server + Unora)

**Goal:** Players reach the canvas from the Registrar and the lectern, and the dialog text no longer says "write" for Art.

**Files:**
- Modify (server): `Chaos/Scripting/DialogScripts/Temuair/College/CollegeRegistrarScript.cs`
- Modify (server): `Chaos/Scripting/DialogScripts/Temuair/College/CollegeLecternScript.cs`
- Modify (Unora): `Data/Configuration/Templates/Dialogs/Temauir/mileth/college/college_registrar_write_subject.json`, `college_lectern_prompt.json`, `college_registrar_book_format.json`, `college_registrar_about2.json`

**Acceptance Criteria:**
- [ ] The Registrar main menu shows "Make an entry"; Art opens the canvas; Music answers that its entries open soon
- [ ] Booking and starting an Activity is offered for Art, not Music
- [ ] During an Art activity, students see "Draw a hand-in"; during a writing activity, "Write a hand-in"
- [ ] The four Unora texts read as below and are valid JSON

**Verify:** `dotnet build Chaos.slnx` in the server worktree -> Build succeeded. Then in the Unora worktree, `python -c "import json,glob;[json.load(open(f,encoding='utf-8')) for f in glob.glob('Data/Configuration/Templates/Dialogs/Temauir/mileth/college/*.json')]"` -> no error.

**Steps:**

- [ ] **Step 1: Registrar.** In `CollegeRegistrarScript.cs`:
  - `private const string WRITE = "Write an entry";` -> `private const string WRITE = "Make an entry";`
  - in `college_registrar_write_open`, change the else text to `Subject.Text = "Music entries open soon. Its composer is still being made.";`
  - in `college_registrar_book_format`, change `!CollegeOptions.IsWritingSubject(` to `!CollegeOptions.HasActivity(`
  - in `ShowEntries`, change `"You have no entries. Write one first."` to `"You have no entries. Make one first."`

- [ ] **Step 2: Lectern.** In `CollegeLecternScript.cs`:
  - add `private const string DRAW_HAND_IN = "Draw a hand-in";` after `WRITE_HAND_IN`;
  - in `college_lectern_begin_now_format`, change `CollegeOptions.IsWritingSubject(subjectNow)` to `CollegeOptions.HasActivity(subjectNow)`;
  - in `college_lectern_begin_done`, change `" Set the prompt your students will write about."` to `" Set the prompt your students will work on."`;
  - in `college_lectern_prompt` (`OnNext`), change `"The prompt is set. Students can now write a hand-in here."` to `"The prompt is set. Students can now hand in work here."`;
  - in `ShowMain`, replace `Subject.AddOption(WRITE_HAND_IN, "college_lectern_handin");` with:

```csharp
                Subject.AddOption(CollegeOptions.IsWritingSubject(running.Subject) ? WRITE_HAND_IN : DRAW_HAND_IN, "college_lectern_handin");
```

- [ ] **Step 3: Unora texts.** In the Unora worktree, set each file's `"text"` value (keep everything else):

| File | New text |
|---|---|
| `college_registrar_write_subject.json` | `Which subject will you enter? Your draft is kept until you submit it.` |
| `college_lectern_prompt.json` | `What should your students make? Keep it to one line.` |
| `college_registrar_book_format.json` | `Will it be a lecture, a discussion or an activity?` |
| `college_registrar_about2.json` | Replace `enter their own writing for 3 marks` with `enter their own work for 3 marks` (the rest of the text is unchanged) |

- [ ] **Step 4: Build and check.** Run the Verify commands. Expected: Build succeeded, and the JSON check prints nothing.

```json:metadata
{"files": ["Chaos/Scripting/DialogScripts/Temuair/College/CollegeRegistrarScript.cs", "Chaos/Scripting/DialogScripts/Temuair/College/CollegeLecternScript.cs", "Data/Configuration/Templates/Dialogs/Temauir/mileth/college/college_registrar_write_subject.json", "Data/Configuration/Templates/Dialogs/Temauir/mileth/college/college_lectern_prompt.json", "Data/Configuration/Templates/Dialogs/Temauir/mileth/college/college_registrar_book_format.json", "Data/Configuration/Templates/Dialogs/Temauir/mileth/college/college_registrar_about2.json"], "verifyCommand": "dotnet build Chaos.slnx", "acceptanceCriteria": ["Make an entry; Art opens canvas; Music opens soon", "Activity for Art not Music", "Draw a hand-in vs Write a hand-in", "four Unora texts updated, valid JSON"], "modelTier": "mechanical"}
```

---

### Task 5: Client drawing model: ArtPalette, PixelDrawing, ArtEditor (client repo)

**Goal:** A pure, unit-tested model performs every canvas edit: brush sizes, fill, line, pick, mirror, swatch colours and 50-step undo.

**Files:**
- Create: `Chaos.Client/ViewModel/College/ArtPalette.cs`
- Create: `Chaos.Client/ViewModel/College/PixelDrawing.cs`
- Create: `Chaos.Client/ViewModel/College/ArtEditor.cs`
- Test: `Tests/Chaos.Client.Tests/College/PixelDrawingTests.cs`, `Tests/Chaos.Client.Tests/College/ArtEditorTests.cs`

**Acceptance Criteria:**
- [ ] A blank drawing is 6,912 swatch-0 pixels with the default palette; `From` pads short data and clears values of 32 and above
- [ ] Fill uses up, down, left and right only
- [ ] Each brush size works at all four edges; a drag fills every pixel between moves
- [ ] Mirror applies to Pen, Line and Fill, and the centre columns mirror onto each other
- [ ] Pick returns to the Pen; the right button always erases; a line changes nothing until release, with a preview while dragging
- [ ] One stroke, fill, line or swatch change is one undo step; at most 50 steps; a new edit clears redo

**Verify:** `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\college-art-server -- --treenode-filter "/*/*/PixelDrawingTests/*"` -> all pass; same for `ArtEditorTests`

**Steps:**

- [ ] **Step 1: Write the failing tests.** Create `Tests/Chaos.Client.Tests/College/PixelDrawingTests.cs`:

```csharp
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests.College;

public class PixelDrawingTests
{
    [Test]
    public void A_blank_drawing_is_swatch_0_with_the_default_palette()
    {
        var drawing = PixelDrawing.Blank();

        drawing.Pixels.Should().HaveCount(CollegeProtocol.DRAWING_PIXELS).And.OnlyContain(p => p == 0);
        drawing.Palette.Should().Equal(ArtPalette.Default);
        drawing.IsDirty.Should().BeFalse();
        drawing.CanUndo.Should().BeFalse();
    }

    [Test]
    public void From_pads_short_data_and_clears_bad_swatches()
    {
        var drawing = PixelDrawing.From([1, 2, 3], [5, 40]);

        drawing.Palette[0].Should().Be(new Color(1, 2, 3));
        drawing.Palette[1].Should().Be(ArtPalette.Default[1]);
        drawing.Pixels[0].Should().Be(5);
        drawing.Pixels[1].Should().Be(0);
        drawing.Pixels.Should().HaveCount(CollegeProtocol.DRAWING_PIXELS);
    }

    [Test]
    public void Palette_bytes_round_trip()
    {
        var drawing = PixelDrawing.Blank();

        PixelDrawing.From(drawing.PaletteBytes(), drawing.Pixels).Palette.Should().Equal(drawing.Palette);
        drawing.PaletteBytes().Should().HaveCount(CollegeProtocol.DRAWING_PALETTE_BYTES);
    }

    [Test]
    public void A_step_that_changes_nothing_leaves_nothing_to_undo()
    {
        var drawing = PixelDrawing.Blank();

        drawing.BeginStep();
        drawing.Set(3, 3, 0);
        drawing.EndStep();

        drawing.CanUndo.Should().BeFalse();
        drawing.IsDirty.Should().BeFalse();
    }

    [Test]
    public void Undo_and_redo_restore_pixels()
    {
        var drawing = PixelDrawing.Blank();
        drawing.BeginStep();
        drawing.Set(3, 3, 9);
        drawing.EndStep();

        drawing.Undo();
        drawing.SwatchAt(3, 3).Should().Be(0);
        drawing.CanRedo.Should().BeTrue();

        drawing.Redo();
        drawing.SwatchAt(3, 3).Should().Be(9);
        drawing.IsDirty.Should().BeTrue();
    }

    [Test]
    public void Undo_keeps_fifty_steps()
    {
        var drawing = PixelDrawing.Blank();

        for (var i = 0; i < 60; i++)
        {
            drawing.BeginStep();
            drawing.Set(i, 0, 1);
            drawing.EndStep();
        }

        var undone = 0;

        while (drawing.CanUndo)
        {
            drawing.Undo();
            undone++;
        }

        undone.Should().Be(PixelDrawing.MAX_UNDO);
        drawing.SwatchAt(9, 0).Should().Be(1);
        drawing.SwatchAt(10, 0).Should().Be(0);
    }

    [Test]
    public void A_new_edit_clears_redo()
    {
        var drawing = PixelDrawing.Blank();
        drawing.BeginStep();
        drawing.Set(1, 1, 2);
        drawing.EndStep();
        drawing.Undo();

        drawing.BeginStep();
        drawing.Set(2, 2, 2);
        drawing.EndStep();

        drawing.CanRedo.Should().BeFalse();
    }

    [Test]
    public void Changing_a_swatch_recolours_its_pixels_and_undoes()
    {
        var drawing = PixelDrawing.Blank();
        var before = drawing.Palette[0];

        drawing.SetColour(0, new Color(10, 20, 30, 77));

        drawing.Palette[0].Should().Be(new Color(10, 20, 30));
        drawing.CanUndo.Should().BeTrue();

        drawing.Undo();
        drawing.Palette[0].Should().Be(before);
    }

    [Test]
    public void Fill_does_not_cross_corner_touches()
    {
        var drawing = PixelDrawing.Blank();
        drawing.Set(0, 2, 3);
        drawing.Set(1, 1, 3);
        drawing.Set(2, 0, 3);

        drawing.Fill(0, 0, 5);

        drawing.SwatchAt(0, 0).Should().Be(5);
        drawing.SwatchAt(1, 0).Should().Be(5);
        drawing.SwatchAt(0, 1).Should().Be(5);
        drawing.SwatchAt(2, 2).Should().Be(0);
        drawing.SwatchAt(1, 1).Should().Be(3);
    }

    [Test]
    public void Drawn_pixels_count_off_the_most_used_swatch()
    {
        var drawing = PixelDrawing.Blank();
        drawing.Fill(0, 0, 4);
        drawing.Set(0, 0, 1);

        drawing.DrawnPixels.Should().Be(1);
    }
}
```

Create `Tests/Chaos.Client.Tests/College/ArtEditorTests.cs`:

```csharp
using Chaos.Client.ViewModel.College;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests.College;

public class ArtEditorTests
{
    private static ArtEditor Editor() => new(PixelDrawing.Blank()) { Current = 7 };

    private static void Tap(ArtEditor editor, int x, int y, bool erase = false)
    {
        editor.Press(x, y, erase);
        editor.Release(x, y);
    }

    private static int Painted(ArtEditor editor) => editor.Drawing.Pixels.Count(p => p != 0);

    [Test]
    public void Brush_size_cycles_one_to_three()
    {
        var editor = Editor();

        editor.BrushSize.Should().Be(1);
        editor.CycleBrush();
        editor.BrushSize.Should().Be(2);
        editor.CycleBrush();
        editor.BrushSize.Should().Be(3);
        editor.CycleBrush();
        editor.BrushSize.Should().Be(1);
    }

    [Test]
    public void Each_brush_size_stays_inside_the_edges()
    {
        var editor = Editor();
        Tap(editor, 0, 0);
        Painted(editor).Should().Be(1);

        editor = Editor();
        editor.CycleBrush();
        Tap(editor, 95, 71);
        Painted(editor).Should().Be(1);
        Tap(editor, 0, 0);
        Painted(editor).Should().Be(5);

        editor = Editor();
        editor.CycleBrush();
        editor.CycleBrush();
        Tap(editor, 0, 0);
        Painted(editor).Should().Be(4);
        Tap(editor, 95, 71);
        Painted(editor).Should().Be(8);
        Tap(editor, 50, 30);
        Painted(editor).Should().Be(17);
    }

    [Test]
    public void A_drag_paints_every_pixel_between_moves()
    {
        var editor = Editor();

        editor.Press(0, 0, false);
        editor.Drag(10, 0);
        editor.Release(10, 0);

        Enumerable.Range(0, 11).Should().OnlyContain(x => editor.Drawing.SwatchAt(x, 0) == 7);
    }

    [Test]
    public void Mirror_copies_the_pen_and_the_centre_columns_meet()
    {
        var editor = Editor();
        editor.Mirror = true;

        Tap(editor, 2, 3);
        Tap(editor, 47, 5);

        editor.Drawing.SwatchAt(93, 3).Should().Be(7);
        editor.Drawing.SwatchAt(48, 5).Should().Be(7);
    }

    [Test]
    public void Mirror_copies_a_line()
    {
        var editor = Editor();
        editor.Mirror = true;
        editor.Tool = ArtTool.Line;

        editor.Press(0, 0, false);
        editor.Drag(3, 0);
        editor.Release(3, 0);

        Enumerable.Range(92, 4).Should().OnlyContain(x => editor.Drawing.SwatchAt(x, 0) == 7);
    }

    [Test]
    public void Mirror_copies_a_fill()
    {
        var editor = Editor();

        for (var y = 0; y < 72; y++)
        {
            editor.Drawing.Set(40, y, 3);
            editor.Drawing.Set(55, y, 3);
        }

        editor.Mirror = true;
        editor.Tool = ArtTool.Fill;
        Tap(editor, 0, 0);

        editor.Drawing.SwatchAt(0, 0).Should().Be(7);
        editor.Drawing.SwatchAt(95, 71).Should().Be(7);
        editor.Drawing.SwatchAt(47, 10).Should().Be(0);
    }

    [Test]
    public void Pick_takes_the_colour_and_returns_to_the_pen()
    {
        var editor = Editor();
        editor.Drawing.Set(4, 4, 12);
        editor.Tool = ArtTool.Pick;

        Tap(editor, 4, 4);

        editor.Current.Should().Be(12);
        editor.Tool.Should().Be(ArtTool.Pen);
        editor.Drawing.CanUndo.Should().BeFalse();
    }

    [Test]
    public void The_right_button_always_erases()
    {
        var editor = Editor();
        editor.Drawing.Set(5, 5, 3);
        editor.Tool = ArtTool.Fill;

        Tap(editor, 5, 5, true);

        editor.Drawing.SwatchAt(5, 5).Should().Be(0);
    }

    [Test]
    public void A_line_waits_for_release_and_shows_a_preview()
    {
        var editor = Editor();
        editor.Tool = ArtTool.Line;

        editor.Press(0, 0, false);
        editor.Drag(4, 0);

        Painted(editor).Should().Be(0);
        editor.PreviewPixels().Should().Contain(new Point(4, 0));

        editor.Release(4, 0);

        Painted(editor).Should().Be(5);
        editor.PreviewPixels().Should().BeEmpty();
    }

    [Test]
    public void A_stroke_is_one_undo_step()
    {
        var editor = Editor();

        editor.Press(0, 0, false);
        editor.Drag(20, 20);
        editor.Release(20, 20);
        editor.Undo();

        Painted(editor).Should().Be(0);
        editor.Drawing.CanUndo.Should().BeFalse();
    }

    [Test]
    public void Loading_a_drawing_ends_any_stroke()
    {
        var editor = Editor();
        editor.Press(0, 0, false);

        editor.Load(PixelDrawing.Blank());

        editor.IsPressing.Should().BeFalse();
        editor.Drawing.SwatchAt(0, 0).Should().Be(0);
    }
}
```

- [ ] **Step 2: Run to see them fail.** Run the `PixelDrawingTests` filter. Expected: build errors (the types don't exist).

- [ ] **Step 3: ArtPalette.** Create `Chaos.Client/ViewModel/College/ArtPalette.cs`:

```csharp
using System.Globalization;
using Microsoft.Xna.Framework;

namespace Chaos.Client.ViewModel.College;

/// <summary>The 32 colours a new drawing starts with: the DawnBringer set, its parchment first so it is the background (swatch 0).</summary>
public static class ArtPalette
{
    private static readonly string[] Hex =
    [
        "eec39a", "222034", "45283c", "663931", "8f563b", "df7126", "d9a066", "000000",
        "fbf236", "99e550", "6abe30", "37946e", "4b692f", "524b24", "323c39", "3f3f74",
        "306082", "5b6ee1", "639bff", "5fcde4", "cbdbfc", "ffffff", "9badb7", "847e87",
        "696a6a", "595652", "76428a", "ac3232", "d95763", "d77bba", "8f974a", "8a6f30"
    ];

    public static IReadOnlyList<Color> Default { get; } = Hex.Select(Parse).ToArray();

    private static Color Parse(string hex)
        => new(
            byte.Parse(hex.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(hex.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(hex.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
}
```

- [ ] **Step 4: PixelDrawing.** Create `Chaos.Client/ViewModel/College/PixelDrawing.cs`:

```csharp
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.ViewModel.College;

/// <summary>
///     A 96 x 72 College drawing: 32 swatch colours and one swatch number per pixel. Edits made between
///     <see cref="BeginStep" /> and <see cref="EndStep" /> undo as one step; up to <see cref="MAX_UNDO" /> steps are kept.
/// </summary>
public sealed class PixelDrawing
{
    public const int WIDTH = CollegeProtocol.DRAWING_WIDTH;
    public const int HEIGHT = CollegeProtocol.DRAWING_HEIGHT;
    public const int COLOURS = CollegeProtocol.DRAWING_COLOURS;
    public const int MAX_UNDO = 50;

    private readonly List<byte[]> RedoSteps = [];
    private readonly List<byte[]> UndoSteps = [];
    private byte[]? StepStart;

    private PixelDrawing(Color[] palette, byte[] pixels)
    {
        Palette = palette;
        Pixels = pixels;
    }

    public Color[] Palette { get; }
    public byte[] Pixels { get; }

    /// <summary>Goes up on every change, so a texture knows when to redraw.</summary>
    public int Version { get; private set; }

    /// <summary>Changed since it was loaded or last saved.</summary>
    public bool IsDirty { get; private set; }

    public bool CanUndo => UndoSteps.Count > 0;
    public bool CanRedo => RedoSteps.Count > 0;
    public int DrawnPixels => CollegeProtocol.DrawnPixels(Pixels);

    public static PixelDrawing Blank() => new(ArtPalette.Default.ToArray(), new byte[WIDTH * HEIGHT]);

    /// <summary>A palette shorter than 32 colours keeps the default for the rest; a swatch number of 32 or more becomes 0.</summary>
    public static PixelDrawing From(byte[] palette, byte[] pixels)
    {
        var colours = ArtPalette.Default.ToArray();

        for (var i = 0; (i < COLOURS) && ((i * 3) + 2 < palette.Length); i++)
            colours[i] = new Color(palette[i * 3], palette[(i * 3) + 1], palette[(i * 3) + 2]);

        var cells = new byte[WIDTH * HEIGHT];

        for (var i = 0; (i < cells.Length) && (i < pixels.Length); i++)
            cells[i] = pixels[i] < COLOURS ? pixels[i] : (byte)0;

        return new PixelDrawing(colours, cells);
    }

    public static bool InBounds(int x, int y) => (x >= 0) && (y >= 0) && (x < WIDTH) && (y < HEIGHT);

    public byte[] PaletteBytes()
    {
        var bytes = new byte[COLOURS * 3];

        for (var i = 0; i < COLOURS; i++)
        {
            bytes[i * 3] = Palette[i].R;
            bytes[(i * 3) + 1] = Palette[i].G;
            bytes[(i * 3) + 2] = Palette[i].B;
        }

        return bytes;
    }

    public byte SwatchAt(int x, int y) => Pixels[(y * WIDTH) + x];

    public void BeginStep() => StepStart ??= Snapshot();

    public void EndStep()
    {
        if (StepStart is null)
            return;

        if (!StepStart.AsSpan().SequenceEqual(Snapshot()))
        {
            UndoSteps.Add(StepStart);

            if (UndoSteps.Count > MAX_UNDO)
                UndoSteps.RemoveAt(0);

            RedoSteps.Clear();
            IsDirty = true;
        }

        StepStart = null;
    }

    public void Set(int x, int y, byte swatch)
    {
        if (!InBounds(x, y))
            return;

        var index = (y * WIDTH) + x;

        if (Pixels[index] == swatch)
            return;

        Pixels[index] = swatch;
        Version++;
    }

    /// <summary>Fills the area of one swatch around a pixel. Only up, down, left and right connect.</summary>
    public void Fill(int x, int y, byte swatch)
    {
        if (!InBounds(x, y))
            return;

        var target = SwatchAt(x, y);

        if (target == swatch)
            return;

        var stack = new Stack<(int X, int Y)>();
        stack.Push((x, y));

        while (stack.Count > 0)
        {
            var (px, py) = stack.Pop();

            if (!InBounds(px, py) || (SwatchAt(px, py) != target))
                continue;

            Pixels[(py * WIDTH) + px] = swatch;
            stack.Push((px + 1, py));
            stack.Push((px - 1, py));
            stack.Push((px, py + 1));
            stack.Push((px, py - 1));
        }

        Version++;
    }

    /// <summary>Changes a swatch's colour as one undo step; every pixel on that swatch changes with it.</summary>
    public void SetColour(int swatch, Color colour)
    {
        BeginStep();
        Palette[swatch] = new Color(colour.R, colour.G, colour.B);
        Version++;
        EndStep();
    }

    public void Undo()
    {
        if ((StepStart is not null) || !CanUndo)
            return;

        RedoSteps.Add(Snapshot());
        Restore(UndoSteps[^1]);
        UndoSteps.RemoveAt(UndoSteps.Count - 1);
    }

    public void Redo()
    {
        if ((StepStart is not null) || !CanRedo)
            return;

        UndoSteps.Add(Snapshot());
        Restore(RedoSteps[^1]);
        RedoSteps.RemoveAt(RedoSteps.Count - 1);
    }

    public void MarkClean() => IsDirty = false;

    public void MarkDirty() => IsDirty = true;

    private byte[] Snapshot()
    {
        var snapshot = new byte[Pixels.Length + (COLOURS * 3)];
        Pixels.CopyTo(snapshot, 0);
        PaletteBytes().CopyTo(snapshot, Pixels.Length);

        return snapshot;
    }

    private void Restore(byte[] snapshot)
    {
        snapshot.AsSpan(0, Pixels.Length).CopyTo(Pixels);

        for (var i = 0; i < COLOURS; i++)
        {
            var at = Pixels.Length + (i * 3);
            Palette[i] = new Color(snapshot[at], snapshot[at + 1], snapshot[at + 2]);
        }

        IsDirty = true;
        Version++;
    }
}
```

- [ ] **Step 5: ArtEditor.** Create `Chaos.Client/ViewModel/College/ArtEditor.cs`:

```csharp
using Microsoft.Xna.Framework;

namespace Chaos.Client.ViewModel.College;

public enum ArtTool
{
    Pen,
    Eraser,
    Fill,
    Line,
    Pick
}

/// <summary>
///     The canvas tools over a <see cref="PixelDrawing" />, in canvas pixels. A press, drags and a release make one stroke
///     (one undo step). The right button always erases with the current brush. A line changes nothing until release.
/// </summary>
public sealed class ArtEditor(PixelDrawing drawing)
{
    private bool Erasing;
    private Point Last;
    private Point LineStart;

    public PixelDrawing Drawing { get; private set; } = drawing;
    public ArtTool Tool { get; set; } = ArtTool.Pen;
    public int BrushSize { get; private set; } = 1;
    public bool Mirror { get; set; }

    /// <summary>The swatch the Pen, Fill and Line paint with.</summary>
    public byte Current { get; set; } = 1;

    public bool IsPressing { get; private set; }

    private bool DrawingLine => IsPressing && !Erasing && (Tool == ArtTool.Line);

    public static int Mirrored(int x) => PixelDrawing.WIDTH - 1 - x;

    public void Load(PixelDrawing drawing)
    {
        Drawing = drawing;
        IsPressing = false;
    }

    public void CycleBrush() => BrushSize = (BrushSize % 3) + 1;

    public void Undo()
    {
        if (!IsPressing)
            Drawing.Undo();
    }

    public void Redo()
    {
        if (!IsPressing)
            Drawing.Redo();
    }

    public void SetColour(int swatch, Color colour)
    {
        if (!IsPressing)
            Drawing.SetColour(swatch, colour);
    }

    public void Press(int x, int y, bool erase)
    {
        if (IsPressing)
            return;

        Erasing = erase;

        if (!erase)
            switch (Tool)
            {
                case ArtTool.Fill:
                    Drawing.BeginStep();
                    Drawing.Fill(x, y, Current);

                    if (Mirror)
                        Drawing.Fill(Mirrored(x), y, Current);

                    Drawing.EndStep();

                    return;
                case ArtTool.Pick:
                    if (PixelDrawing.InBounds(x, y))
                    {
                        Current = Drawing.SwatchAt(x, y);
                        Tool = ArtTool.Pen;
                    }

                    return;
                case ArtTool.Line:
                    IsPressing = true;
                    LineStart = Last = new Point(x, y);

                    return;
            }

        IsPressing = true;
        Drawing.BeginStep();
        Last = new Point(x, y);
        Stamp(x, y);
    }

    public void Drag(int x, int y)
    {
        if (!IsPressing)
            return;

        var to = new Point(x, y);

        if (!DrawingLine)
            foreach (var point in LinePoints(Last, to).Skip(1))
                Stamp(point.X, point.Y);

        Last = to;
    }

    public void Release(int x, int y)
    {
        if (!IsPressing)
            return;

        Drag(x, y);

        if (DrawingLine)
        {
            Drawing.BeginStep();

            foreach (var point in LinePoints(LineStart, Last))
                Stamp(point.X, point.Y);
        }

        Drawing.EndStep();
        IsPressing = false;
    }

    /// <summary>Ends a stroke the window lost (focus or capture): what was painted stays, and a line in progress is dropped.</summary>
    public void Cancel()
    {
        if (!IsPressing)
            return;

        Drawing.EndStep();
        IsPressing = false;
    }

    /// <summary>The pixels a line being dragged would paint, mirrored if Mirror is on. Empty when no line is being dragged.</summary>
    public IReadOnlyCollection<Point> PreviewPixels()
    {
        if (!DrawingLine)
            return [];

        var pixels = new HashSet<Point>();

        foreach (var point in LinePoints(LineStart, Last))
            foreach (var pixel in Brush(point.X, point.Y))
            {
                if (PixelDrawing.InBounds(pixel.X, pixel.Y))
                    pixels.Add(pixel);

                if (Mirror && PixelDrawing.InBounds(Mirrored(pixel.X), pixel.Y))
                    pixels.Add(new Point(Mirrored(pixel.X), pixel.Y));
            }

        return pixels;
    }

    public static List<Point> LinePoints(Point from, Point to)
    {
        var points = new List<Point> { from };
        var dx = Math.Abs(to.X - from.X);
        var dy = -Math.Abs(to.Y - from.Y);
        var sx = from.X < to.X ? 1 : -1;
        var sy = from.Y < to.Y ? 1 : -1;
        var error = dx + dy;
        var x = from.X;
        var y = from.Y;

        while ((x != to.X) || (y != to.Y))
        {
            var doubled = 2 * error;

            if (doubled >= dy)
            {
                error += dy;
                x += sx;
            }

            if (doubled <= dx)
            {
                error += dx;
                y += sy;
            }

            points.Add(new Point(x, y));
        }

        return points;
    }

    //size 2 covers the pixel, the one right, below and below right; size 3 is centred on the pixel
    private IEnumerable<Point> Brush(int x, int y)
    {
        var (from, to) = BrushSize switch
        {
            1 => (0, 0),
            2 => (0, 1),
            _ => (-1, 1)
        };

        for (var dy = from; dy <= to; dy++)
            for (var dx = from; dx <= to; dx++)
                yield return new Point(x + dx, y + dy);
    }

    private void Stamp(int x, int y)
    {
        var swatch = Erasing || (Tool == ArtTool.Eraser) ? (byte)0 : Current;

        foreach (var pixel in Brush(x, y))
        {
            Drawing.Set(pixel.X, pixel.Y, swatch);

            if (Mirror)
                Drawing.Set(Mirrored(pixel.X), pixel.Y, swatch);
        }
    }
}
```

- [ ] **Step 6: Run the tests.** Run the `PixelDrawingTests` and `ArtEditorTests` filters. Expected: all pass (10 + 11).

```json:metadata
{"files": ["Chaos.Client/ViewModel/College/ArtPalette.cs", "Chaos.Client/ViewModel/College/PixelDrawing.cs", "Chaos.Client/ViewModel/College/ArtEditor.cs", "Tests/Chaos.Client.Tests/College/PixelDrawingTests.cs", "Tests/Chaos.Client.Tests/College/ArtEditorTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\\Users\\Michael\\Documents\\GitHub\\worktrees\\college-art-server -- --treenode-filter \"/*/*/ArtEditorTests/*\"", "acceptanceCriteria": ["blank/From/palette bytes", "4-way fill", "brush sizes at edges; drag gap-free", "mirror pen/line/fill and centre columns", "pick, right-button erase, line preview", "undo steps, 50 limit, redo cleared"], "modelTier": "standard"}
```

---

### Task 6: Client drawing pictures, textures and the gallery cache (client repo)

**Goal:** Drawings turn into colours and textures. They also turn into 384 x 288 PNGs and back. The gallery keeps fetched thumbnails by entry id.

**Files:**
- Create: `Chaos.Client/Systems/College/DrawingTextures.cs`
- Create: `Chaos.Client/Systems/College/DrawingPictures.cs`
- Create: `Chaos.Client/Systems/College/CollegeDrawings.cs`
- Modify: `Chaos.Client/Systems/College/CollegePictureTransfers.cs` (add `BytesOf`)
- Test: `Tests/Chaos.Client.Tests/College/DrawingPicturesTests.cs`, `Tests/Chaos.Client.Tests/College/CollegeDrawingsTests.cs`

**Acceptance Criteria:**
- [ ] `DrawingTextures.ToColors` gives `(96 x zoom) x (72 x zoom)` opaque colours, each canvas pixel a `zoom x zoom` block
- [ ] A drawing turned into a picture is a 384 x 288 PNG, and reading it back gives the same colour at every pixel
- [ ] A picture of a different size, with a mixed 4 x 4 block, or with 33 colours, isn't read as a drawing
- [ ] A rebuilt palette lists the colours in order of first appearance, then fills with unused default colours up to 32
- [ ] `CollegeDrawings.NeedsFetch` is true once per id until the drawing arrives or the cache is cleared

**Verify:** `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\college-art-server -- --treenode-filter "/*/*/DrawingPicturesTests/*"` -> all pass; same for `CollegeDrawingsTests`

**Steps:**

- [ ] **Step 1: Write the failing tests.** Create `Tests/Chaos.Client.Tests/College/DrawingPicturesTests.cs`:

```csharp
using Chaos.Client.Systems.College;
using Chaos.Client.ViewModel.College;
using FluentAssertions;
using Microsoft.Xna.Framework;
using SkiaSharp;

namespace Chaos.Client.Tests.College;

public class DrawingPicturesTests
{
    private static PixelDrawing Sample()
    {
        var drawing = PixelDrawing.Blank();

        for (var x = 0; x < 30; x++)
            drawing.Set(x, 10, 5);

        drawing.Set(95, 71, 21);

        return drawing;
    }

    private static byte[] Png(int width, int height, Func<int, int, SKColor> colour)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);

        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                bitmap.SetPixel(x, y, colour(x, y));

        using var image = SKImage.FromBitmap(bitmap);

        return image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
    }

    [Test]
    public void Colours_are_blocks_of_the_zoom()
    {
        var drawing = Sample();

        var colours = DrawingTextures.ToColors(drawing.Palette, drawing.Pixels, 2);

        colours.Should().HaveCount(192 * 144);
        colours[(20 * 192) + 0].Should().Be(drawing.Palette[5]);
        colours[(21 * 192) + 1].Should().Be(drawing.Palette[5]);
        colours[0].Should().Be(drawing.Palette[0]);
        colours.Should().OnlyContain(c => c.A == 255);
    }

    [Test]
    public void A_drawing_round_trips_through_its_picture()
    {
        var drawing = Sample();

        var picture = DrawingPictures.ToPicture(drawing);

        (picture.Width, picture.Height).Should().Be((384, 288));
        picture.Bytes[..4].Should().Equal(0x89, 0x50, 0x4E, 0x47);
        picture.Hash.Should().MatchRegex("^[0-9a-f]{64}$");

        var back = DrawingPictures.TryRead(picture.Bytes)!;

        for (var i = 0; i < drawing.Pixels.Length; i++)
            back.Palette[back.Pixels[i]].Should().Be(drawing.Palette[drawing.Pixels[i]]);
    }

    [Test]
    public void The_rebuilt_palette_keeps_first_seen_colours_then_defaults()
    {
        var drawing = PixelDrawing.Blank();
        drawing.Set(0, 0, 21);

        var back = DrawingPictures.TryRead(DrawingPictures.ToPicture(drawing).Bytes)!;

        back.Palette[0].Should().Be(ArtPalette.Default[21]);
        back.Palette[1].Should().Be(ArtPalette.Default[0]);
        back.Palette[2].Should().Be(ArtPalette.Default[1]);
        back.Palette.Distinct().Should().HaveCount(32);
        back.Pixels[0].Should().Be(0);
        back.Pixels[1].Should().Be(1);
    }

    [Test]
    public void Other_pictures_are_not_drawings()
    {
        DrawingPictures.TryRead(Png(100, 100, (_, _) => SKColors.Red)).Should().BeNull();
        DrawingPictures.TryRead(Png(384, 288, (x, y) => (x == 1) && (y == 1) ? SKColors.Blue : SKColors.Red)).Should().BeNull();
        DrawingPictures.TryRead(Png(384, 288, (x, y) => (y < 4) && (x < 33 * 4) ? new SKColor((byte)(x / 4), 0, 0) : SKColors.White)).Should().BeNull();
        DrawingPictures.TryRead([1, 2, 3]).Should().BeNull();
    }
}
```

Create `Tests/Chaos.Client.Tests/College/CollegeDrawingsTests.cs`:

```csharp
using Chaos.Client.Systems.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class CollegeDrawingsTests
{
    private static CollegeDisplayArgs Drawing(int id)
        => new() { Type = CollegeDisplayType.Drawing, Id = id, Palette = new byte[96], Pixels = new byte[6912] };

    [Test]
    public void Each_drawing_is_fetched_once()
    {
        var drawings = new CollegeDrawings();

        drawings.NeedsFetch(4).Should().BeTrue();
        drawings.NeedsFetch(4).Should().BeFalse();
    }

    [Test]
    public void An_arriving_drawing_is_kept_and_announced()
    {
        var drawings = new CollegeDrawings();
        var ready = new List<int>();
        drawings.DrawingReady += ready.Add;
        drawings.NeedsFetch(4);

        drawings.OnDrawing(Drawing(4));

        drawings.Has(4).Should().BeTrue();
        drawings.NeedsFetch(4).Should().BeFalse();
        ready.Should().Equal(4);
    }

    [Test]
    public void Clearing_forgets_drawings_and_requests()
    {
        var drawings = new CollegeDrawings();
        drawings.NeedsFetch(4);
        drawings.OnDrawing(Drawing(5));

        drawings.Clear();

        drawings.Has(5).Should().BeFalse();
        drawings.NeedsFetch(4).Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run to see them fail.** Run the `DrawingPicturesTests` filter. Expected: build errors.

- [ ] **Step 3: DrawingTextures.** Create `Chaos.Client/Systems/College/DrawingTextures.cs`:

```csharp
using Chaos.Client.Rendering;
using Chaos.Client.ViewModel.College;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Systems.College;

/// <summary>Turns a drawing into colours, each canvas pixel a zoom x zoom block, and into a texture the caller owns.</summary>
public static class DrawingTextures
{
    public static Color[] ToColors(IReadOnlyList<Color> palette, byte[] pixels, int zoom)
    {
        var width = PixelDrawing.WIDTH * zoom;
        var colours = new Color[width * PixelDrawing.HEIGHT * zoom];

        for (var y = 0; y < PixelDrawing.HEIGHT; y++)
            for (var x = 0; x < PixelDrawing.WIDTH; x++)
            {
                var c = palette[pixels[(y * PixelDrawing.WIDTH) + x]];
                var colour = new Color(c.R, c.G, c.B);

                for (var dy = 0; dy < zoom; dy++)
                    Array.Fill(colours, colour, (((y * zoom) + dy) * width) + (x * zoom), zoom);
            }

        return colours;
    }

    public static Texture2D Build(PixelDrawing drawing, int zoom)
    {
        var texture = new Texture2D(TextureConverter.Device, PixelDrawing.WIDTH * zoom, PixelDrawing.HEIGHT * zoom);
        texture.SetData(ToColors(drawing.Palette, drawing.Pixels, zoom));

        return texture;
    }
}
```

- [ ] **Step 4: DrawingPictures.** Create `Chaos.Client/Systems/College/DrawingPictures.cs`:

```csharp
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using SkiaSharp;

namespace Chaos.Client.Systems.College;

/// <summary>
///     A drawing as a picture for a writing piece: a 384 x 288 PNG, each canvas pixel a 4 x 4 block. Reading one back
///     recognises only pictures made that way (exact size, solid blocks, at most 32 colours).
/// </summary>
public static class DrawingPictures
{
    private const int ZOOM = CollegeProtocol.DRAWING_ZOOM;
    public const int WIDTH = PixelDrawing.WIDTH * ZOOM;
    public const int HEIGHT = PixelDrawing.HEIGHT * ZOOM;

    public static PreparedPicture ToPicture(PixelDrawing drawing)
    {
        var colours = DrawingTextures.ToColors(drawing.Palette, drawing.Pixels, ZOOM);

        using var bitmap = new SKBitmap(WIDTH, HEIGHT, SKColorType.Rgba8888, SKAlphaType.Opaque);
        bitmap.Pixels = colours.Select(c => new SKColor(c.R, c.G, c.B)).ToArray();

        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        var bytes = png.ToArray();

        return new PreparedPicture(bytes, PicturePrep.Hash(bytes), WIDTH, HEIGHT);
    }

    /// <summary>The drawing a picture was made from, or null. The palette lists colours as first seen, then unused defaults.</summary>
    public static PixelDrawing? TryRead(byte[] bytes)
    {
        using var bitmap = SKBitmap.Decode(bytes);

        if (bitmap is null || (bitmap.Width != WIDTH) || (bitmap.Height != HEIGHT))
            return null;

        var source = bitmap.Pixels;
        var colours = new List<Color>();
        var swatches = new Dictionary<SKColor, byte>();
        var pixels = new byte[PixelDrawing.WIDTH * PixelDrawing.HEIGHT];

        for (var y = 0; y < PixelDrawing.HEIGHT; y++)
            for (var x = 0; x < PixelDrawing.WIDTH; x++)
            {
                var colour = source[(y * ZOOM * WIDTH) + (x * ZOOM)];

                if (colour.Alpha != 255)
                    return null;

                for (var dy = 0; dy < ZOOM; dy++)
                    for (var dx = 0; dx < ZOOM; dx++)
                        if (source[(((y * ZOOM) + dy) * WIDTH) + (x * ZOOM) + dx] != colour)
                            return null;

                if (!swatches.TryGetValue(colour, out var swatch))
                {
                    if (colours.Count == PixelDrawing.COLOURS)
                        return null;

                    swatch = (byte)colours.Count;
                    swatches[colour] = swatch;
                    colours.Add(new Color(colour.Red, colour.Green, colour.Blue));
                }

                pixels[(y * PixelDrawing.WIDTH) + x] = swatch;
            }

        foreach (var fallback in ArtPalette.Default)
            if ((colours.Count < PixelDrawing.COLOURS) && !colours.Contains(fallback))
                colours.Add(fallback);

        var palette = new byte[PixelDrawing.COLOURS * 3];

        for (var i = 0; i < colours.Count; i++)
        {
            palette[i * 3] = colours[i].R;
            palette[(i * 3) + 1] = colours[i].G;
            palette[(i * 3) + 2] = colours[i].B;
        }

        return PixelDrawing.From(palette, pixels);
    }
}
```

- [ ] **Step 5: CollegeDrawings.** Create `Chaos.Client/Systems/College/CollegeDrawings.cs`:

```csharp
using Chaos.Client.ViewModel.College;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Systems.College;

/// <summary>Gallery thumbnails by entry id: each is fetched once per session and kept until logout.</summary>
public sealed class CollegeDrawings(Func<PixelDrawing, Texture2D>? makeTexture = null)
{
    private readonly Dictionary<int, PixelDrawing> Drawings = new();
    private readonly Func<PixelDrawing, Texture2D> MakeTexture = makeTexture ?? (d => DrawingTextures.Build(d, 1));
    private readonly HashSet<int> Requested = [];
    private readonly Dictionary<int, Texture2D> Textures = new();

    public event Action<int>? DrawingReady;

    public bool Has(int id) => Drawings.ContainsKey(id);

    /// <summary>True the first time an id without a drawing is asked about; the caller then sends DrawingFetch.</summary>
    public bool NeedsFetch(int id) => !Drawings.ContainsKey(id) && Requested.Add(id);

    public void OnDrawing(CollegeDisplayArgs args)
    {
        Drawings[args.Id] = PixelDrawing.From(args.Palette, args.Pixels);
        Requested.Remove(args.Id);

        if (Textures.Remove(args.Id, out var old))
            old.Dispose();

        DrawingReady?.Invoke(args.Id);
    }

    /// <summary>The thumbnail (96 x 72), owned by this cache.</summary>
    public bool TryGetTexture(int id, out Texture2D texture)
    {
        if (Textures.TryGetValue(id, out texture!))
            return true;

        if (!Drawings.TryGetValue(id, out var drawing))
            return false;

        texture = Textures[id] = MakeTexture(drawing);

        return true;
    }

    public void Clear()
    {
        foreach (var texture in Textures.Values)
            texture.Dispose();

        Textures.Clear();
        Drawings.Clear();
        Requested.Clear();
    }
}
```

- [ ] **Step 6: BytesOf.** In `CollegePictureTransfers.cs`, add after `TryGetTexture`:

```csharp
    /// <summary>A picture's file bytes, if this client has them (its own upload or the disk cache).</summary>
    public byte[]? BytesOf(string hash) => Uploads.GetValueOrDefault(hash) ?? cache.TryRead(hash);
```

  (`Uploads` is the existing `Dictionary<string, byte[]>` that `Upload` fills. Check its name with `get_symbols_overview` and use whatever the file calls it.)

- [ ] **Step 7: Run the tests.** Run the `DrawingPicturesTests` and `CollegeDrawingsTests` filters. Expected: all pass (4 + 3). Then run the whole client suite (no filter). Expected: everything passes.

```json:metadata
{"files": ["Chaos.Client/Systems/College/DrawingTextures.cs", "Chaos.Client/Systems/College/DrawingPictures.cs", "Chaos.Client/Systems/College/CollegeDrawings.cs", "Chaos.Client/Systems/College/CollegePictureTransfers.cs", "Tests/Chaos.Client.Tests/College/DrawingPicturesTests.cs", "Tests/Chaos.Client.Tests/College/CollegeDrawingsTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\\Users\\Michael\\Documents\\GitHub\\worktrees\\college-art-server -- --treenode-filter \"/*/*/DrawingPicturesTests/*\"", "acceptanceCriteria": ["zoom blocks, opaque", "PNG round trip per pixel colour", "non-drawings refused", "palette rebuild order", "fetch once until arrival or clear"], "modelTier": "standard"}
```

---

### Task 7: Client canvas window: ArtCanvas, ArtSwatches, ArtCanvasControl (client repo)

**Goal:** The 560 x 464 canvas window, with layout A, the tools, the swatches, the colour picker and the Draft, Hand-in and Picture modes.

**Files:**
- Create: `Chaos.Client/Controls/World/Popups/College/ArtCanvas.cs`
- Create: `Chaos.Client/Controls/World/Popups/College/ArtSwatches.cs`
- Create: `Chaos.Client/Controls/World/Popups/College/ArtCanvasControl.cs`

**Acceptance Criteria:**
- [ ] The tool column, zoom-4 canvas, swatch columns, note box and footer fit in 560 x 464 without overlapping. Tool column: Pen, Erase, Fill, Line, Pick | Size | Mirror, Grid | Undo, Redo. Swatches: 2 x 16 with the current colour and its hex code.
- [ ] Left button uses the tool; right button erases; a fast drag leaves no gaps; a line shows its preview
- [ ] Double-clicking a swatch opens `StageColorPicker`; OK applies the colour as one undo step; Cancel or Escape leaves it unchanged
- [ ] Ctrl+Z and Ctrl+Y undo and redo while neither text box has focus
- [ ] **Draft:** Save; Submit (with confirmation); closing with changes saves first
- [ ] **HandIn:** prompt in the caption; "Hand in"; "Close without handing in?"
- [ ] **Picture:** no title or note; Insert raises `PictureMade` with a 384 x 288 PNG and the hash it replaces; Cancel asks before throwing changes away
- [ ] Submit and Hand in warn without sending when too little is drawn or the title is missing

**Verify:** `dotnet build Chaos.Client/Chaos.Client.csproj -p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\college-art-server` -> Build succeeded with no new warnings in these files. The window is checked in game (Task 10 checklist).

**Steps:**

- [ ] **Step 1: ArtCanvas.** Create `Chaos.Client/Controls/World/Popups/College/ArtCanvas.cs`:

```csharp
#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Systems.College;
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>
///     The drawing at zoom 4, with grid lines, the mirror line and a line tool's preview. Mouse input goes to the
///     <see cref="ArtEditor" /> in canvas pixels; the texture is rebuilt only when the drawing's version changes.
/// </summary>
public sealed class ArtCanvas : UIElement
{
    private const int ZOOM = CollegeProtocol.DRAWING_ZOOM;

    private static readonly Color GridLine = new(0, 0, 0, 50);
    private static readonly Color MirrorLine = new(255, 255, 255, 150);

    private readonly ArtEditor Editor;
    private bool Painting;
    private bool RightButton;
    private Texture2D? Texture;
    private PixelDrawing? TextureDrawing;
    private int TextureVersion = -1;

    public ArtCanvas(ArtEditor editor)
    {
        Editor = editor;
        Width = PixelDrawing.WIDTH * ZOOM;
        Height = PixelDrawing.HEIGHT * ZOOM;
    }

    public bool ShowGrid { get; set; } = true;

    /// <summary>Raised after a press, a drag or a release, so the window can refresh its buttons.</summary>
    public event Action? Changed;

    public override void Dispose()
    {
        Texture?.Dispose();
        base.Dispose();
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        base.Draw(spriteBatch);
        EnsureTexture();
        DrawTexture(spriteBatch, Texture, new Vector2(ScreenX, ScreenY), Color.White);

        var preview = Editor.Drawing.Palette[Editor.Current];

        foreach (var pixel in Editor.PreviewPixels())
            DrawRectClipped(spriteBatch, new Rectangle(ScreenX + (pixel.X * ZOOM), ScreenY + (pixel.Y * ZOOM), ZOOM, ZOOM), preview);

        if (ShowGrid)
        {
            for (var x = 1; x < PixelDrawing.WIDTH; x++)
                DrawRectClipped(spriteBatch, new Rectangle(ScreenX + (x * ZOOM), ScreenY, 1, Height), GridLine);

            for (var y = 1; y < PixelDrawing.HEIGHT; y++)
                DrawRectClipped(spriteBatch, new Rectangle(ScreenX, ScreenY + (y * ZOOM), Width, 1), GridLine);
        }

        if (Editor.Mirror)
            DrawRectClipped(spriteBatch, new Rectangle(ScreenX + (Width / 2), ScreenY, 1, Height), MirrorLine);
    }

    public override void OnMouseDown(MouseDownEvent e)
    {
        e.Handled = true;

        if (Painting || ((e.Button != MouseButton.Left) && (e.Button != MouseButton.Right)))
            return;

        RightButton = e.Button == MouseButton.Right;
        var (x, y) = CellAt(e.ScreenX, e.ScreenY);
        Editor.Press(x, y, RightButton);
        Painting = Editor.IsPressing;
        Changed?.Invoke();
    }

    public override void OnMouseMove(MouseMoveEvent e)
    {
        if (!Painting)
            return;

        e.Handled = true;
        var (x, y) = CellAt(e.ScreenX, e.ScreenY);

        //the button came up where no mouse-up reached this canvas (e.g. the window lost focus mid-stroke)
        if (!(RightButton ? InputBuffer.IsRightButtonHeld : InputBuffer.IsLeftButtonHeld))
        {
            Finish(x, y);

            return;
        }

        Editor.Drag(x, y);
    }

    public override void OnMouseUp(MouseUpEvent e)
    {
        if (!Painting)
            return;

        e.Handled = true;
        var (x, y) = CellAt(e.ScreenX, e.ScreenY);
        Finish(x, y);
    }

    public override void ResetInteractionState()
    {
        base.ResetInteractionState();

        if (!Painting)
            return;

        Painting = false;
        Editor.Cancel();
        Changed?.Invoke();
    }

    private (int X, int Y) CellAt(int screenX, int screenY)
        => ((int)Math.Floor((screenX - ScreenX) / (double)ZOOM), (int)Math.Floor((screenY - ScreenY) / (double)ZOOM));

    private void Finish(int x, int y)
    {
        Painting = false;
        Editor.Release(x, y);
        Changed?.Invoke();
    }

    private void EnsureTexture()
    {
        var drawing = Editor.Drawing;

        if ((Texture is not null) && ReferenceEquals(TextureDrawing, drawing) && (TextureVersion == drawing.Version))
            return;

        Texture ??= new Texture2D(TextureConverter.Device, Width, Height);
        Texture.SetData(DrawingTextures.ToColors(drawing.Palette, drawing.Pixels, ZOOM));
        TextureDrawing = drawing;
        TextureVersion = drawing.Version;
    }
}
```

  The `UIElement`, `InputBuffer` and `MouseButton` names come from the same namespaces `PumpkinCarvingCanvas` uses. Copy its `using` block if the build reports a missing name.

- [ ] **Step 2: ArtSwatches.** Create `Chaos.Client/Controls/World/Popups/College/ArtSwatches.cs`:

```csharp
#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.ViewModel.College;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>The drawing's 32 swatches in two columns. A click picks a swatch; a double-click asks to change its colour.</summary>
public sealed class ArtSwatches : UIElement
{
    private const int CELL_WIDTH = 20;
    private const int CELL_HEIGHT = 14;
    private const int COLUMNS = 2;
    private const int STEP_X = CELL_WIDTH + 2;
    private const int STEP_Y = CELL_HEIGHT + 1;
    private const int ROWS = PixelDrawing.COLOURS / COLUMNS;

    private readonly ArtEditor Editor;

    public ArtSwatches(ArtEditor editor)
    {
        Editor = editor;
        Width = (COLUMNS * STEP_X) - 2;
        Height = (ROWS * STEP_Y) - 1;
    }

    public event Action<int>? EditRequested;
    public event Action<int>? Picked;

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        base.Draw(spriteBatch);

        for (var i = 0; i < PixelDrawing.COLOURS; i++)
        {
            var bounds = new Rectangle(ScreenX + ((i % COLUMNS) * STEP_X), ScreenY + ((i / COLUMNS) * STEP_Y), CELL_WIDTH, CELL_HEIGHT);
            DrawRectClipped(spriteBatch, bounds, Editor.Drawing.Palette[i]);
            DrawBorder(spriteBatch, bounds, i == Editor.Current ? Color.White : Color.Black);
        }
    }

    public override void OnClick(ClickEvent e)
    {
        e.Handled = true;

        if (SwatchAt(e.ScreenX, e.ScreenY) is { } swatch)
            Picked?.Invoke(swatch);
    }

    public override void OnDoubleClick(DoubleClickEvent e)
    {
        e.Handled = true;

        if (SwatchAt(e.ScreenX, e.ScreenY) is { } swatch)
            EditRequested?.Invoke(swatch);
    }

    private int? SwatchAt(int screenX, int screenY)
    {
        var x = screenX - ScreenX;
        var y = screenY - ScreenY;

        if ((x < 0) || (y < 0))
            return null;

        var column = x / STEP_X;
        var row = y / STEP_Y;

        return (column < COLUMNS) && (row < ROWS) ? (row * COLUMNS) + column : null;
    }
}
```

- [ ] **Step 3: ArtCanvasControl.** Create `Chaos.Client/Controls/World/Popups/College/ArtCanvasControl.cs`:

```csharp
#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.Controls.World.Popups.Theatre;
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

public enum ArtCanvasMode
{
    Draft,
    HandIn,
    Picture
}

/// <summary>
///     The Art canvas window (spec: docs/superpowers/specs/2026-10-05-mileth-college-art-design.md): title, tools, the
///     zoom-4 canvas, the 32 swatches, the artist's note and the footer. Draft and HandIn send the piece to the server;
///     Picture mode (from the writing window's Draw or Edit) raises <see cref="PictureMade" /> and sends nothing.
/// </summary>
public sealed class ArtCanvasControl : GuildCloakDialogBase
{
    private const int WIDTH = 560;
    private const int HEIGHT = 464;
    private const int LEFT = 16;
    private const int INNER_WIDTH = WIDTH - (2 * LEFT);
    private const int CAPTION_TOP = 10;
    private const int TITLE_TOP = 26;
    private const int TOOL_WIDTH = 42;
    private const int TOOL_STEP = CustomButton.HEIGHT + 2;
    private const int GROUP_GAP = 6;
    private const int CANVAS_X = LEFT + TOOL_WIDTH + 4;
    private const int CANVAS_Y = 52;
    private const int CANVAS_WIDTH = PixelDrawing.WIDTH * CollegeProtocol.DRAWING_ZOOM;
    private const int CANVAS_HEIGHT = PixelDrawing.HEIGHT * CollegeProtocol.DRAWING_ZOOM;
    private const int PALETTE_X = CANVAS_X + CANVAS_WIDTH + 8;
    private const int CURRENT_SIZE = 24;
    private const int SWATCHES_Y = CANVAS_Y + CURRENT_SIZE + 18;
    private const int NOTE_TOP = CANVAS_Y + CANVAS_HEIGHT + 3;
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
    private const string DRAW_MORE = "Draw more before entering it.";
    private const string DRAW_MORE_HAND_IN = "Draw more before handing it in.";
    private const string SAVING = "Saving...";

    private readonly CustomButton CancelButton;
    private readonly ArtCanvas Canvas;
    private readonly UILabel CaptionLabel;
    private readonly UIPanel CurrentBox;
    private readonly UILabel CurrentHex;
    private readonly ArtEditor Editor = new(PixelDrawing.Blank());
    private readonly CustomButton GridButton;
    private readonly CustomButton InsertButton;
    private readonly CustomButton MirrorButton;
    private readonly CustomTextBox NoteBox;
    private readonly UILabel NoteCounter;
    private readonly StageColorPicker Picker;
    private readonly CustomButton PickerCancel;
    private readonly CustomButton RedoButton;
    private readonly CustomButton SaveButton;
    private readonly CustomButton SizeButton;
    private readonly UILabel Status;
    private readonly CustomButton SubmitButton;
    private readonly ArtSwatches Swatches;
    private readonly CustomTextBox TitleBox;
    private readonly Dictionary<ArtTool, CustomButton> ToolButtons = new();
    private readonly CustomButton UndoButton;

    private bool CloseArmed;

    //Close sent a save and waits for its Saved before hiding
    private bool ClosingOnSave;
    private int EditingSwatch = -1;
    private string? ReplacingHash;
    private string SavedNote = string.Empty;
    private string SavedTitle = string.Empty;
    private CollegeDisplayArgs? Session;
    private bool SubmitArmed;

    public ArtCanvasControl()
        : base("_nsett", false)
    {
        Name = "ArtCanvas";
        Visible = false;
        UsesControlStack = true;
        Width = WIDTH;
        Height = HEIGHT;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(OnCloseClicked, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);
        CaptionLabel = Caption(string.Empty, LEFT, CAPTION_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gold);

        TitleBox = new CustomTextBox
        {
            X = LEFT,
            Y = TITLE_TOP,
            Width = INNER_WIDTH,
            Height = CustomButton.HEIGHT,
            MaxLength = CollegeProtocol.MAX_TITLE_CHARS,
            IsSelectable = true,
            IsTabStop = true,
            HintText = "Title"
        };

        AddChild(TitleBox);

        Canvas = new ArtCanvas(Editor)
        {
            X = CANVAS_X,
            Y = CANVAS_Y
        };

        Canvas.Changed += () =>
        {
            DisarmSubmit();
            CloseArmed = false;
            RefreshControls();
        };

        AddChild(Canvas);

        var y = CANVAS_Y;

        foreach (var (tool, caption) in new[]
                 {
                     (ArtTool.Pen, "Pen"),
                     (ArtTool.Eraser, "Erase"),
                     (ArtTool.Fill, "Fill"),
                     (ArtTool.Line, "Line"),
                     (ArtTool.Pick, "Pick")
                 })
        {
            ToolButtons[tool] = ToolButton(caption, y, () => Editor.Tool = tool);
            y += TOOL_STEP;
        }

        y += GROUP_GAP;
        SizeButton = ToolButton("Size 1", y, Editor.CycleBrush);
        y += TOOL_STEP + GROUP_GAP;
        MirrorButton = ToolButton("Mirror", y, () => Editor.Mirror = !Editor.Mirror);
        y += TOOL_STEP;
        GridButton = ToolButton("Grid", y, () => Canvas.ShowGrid = !Canvas.ShowGrid);
        y += TOOL_STEP + GROUP_GAP;
        UndoButton = ToolButton("Undo", y, Editor.Undo);
        y += TOOL_STEP;
        RedoButton = ToolButton("Redo", y, Editor.Redo);

        CurrentBox = new UIPanel
        {
            X = PALETTE_X,
            Y = CANVAS_Y,
            Width = CURRENT_SIZE,
            Height = CURRENT_SIZE,
            BorderColor = Color.White,
            IsHitTestVisible = false
        };

        AddChild(CurrentBox);
        CurrentHex = Caption(string.Empty, PALETTE_X, CANVAS_Y + CURRENT_SIZE + 3, WIDTH - LEFT - PALETTE_X, color: LegendColors.Gray);

        Swatches = new ArtSwatches(Editor)
        {
            X = PALETTE_X,
            Y = SWATCHES_Y
        };

        Swatches.Picked += swatch =>
        {
            Editor.Current = (byte)swatch;

            if (Editor.Tool is ArtTool.Eraser or ArtTool.Pick)
                Editor.Tool = ArtTool.Pen;

            RefreshControls();
        };

        Swatches.EditRequested += OpenPicker;
        AddChild(Swatches);

        NoteBox = new CustomTextBox
        {
            X = LEFT,
            Y = NOTE_TOP,
            Width = INNER_WIDTH,
            Height = NOTE_HEIGHT,
            IsMultiLine = true,
            IsSelectable = true,
            IsTabStop = true,
            MaxLength = CollegeProtocol.MAX_ART_NOTE_CHARS,
            HintText = "Artist's note"
        };

        AddChild(NoteBox);

        var footerTextY = BUTTONS_TOP + ((CustomButton.HEIGHT - TextRenderer.CHAR_HEIGHT) / 2);
        SaveButton = AddButton("Save", SAVE_WIDTH, LEFT, BUTTONS_TOP, OnSaveClicked);
        SubmitButton = AddButton("Submit", SUBMIT_WIDTH, SUBMIT_X, BUTTONS_TOP, OnSubmitClicked);
        InsertButton = AddButton("Insert", SAVE_WIDTH, LEFT, BUTTONS_TOP, OnInsertClicked);
        CancelButton = AddButton("Cancel", SAVE_WIDTH, SUBMIT_X, BUTTONS_TOP, OnCloseClicked);
        Status = Caption(string.Empty, STATUS_X, footerTextY, LEFT + INNER_WIDTH - COUNTER_WIDTH - 4 - STATUS_X);
        NoteCounter = Caption(string.Empty, LEFT + INNER_WIDTH - COUNTER_WIDTH, footerTextY, COUNTER_WIDTH, HorizontalAlignment.Right, LegendColors.Gray);

        Picker = new StageColorPicker
        {
            X = PALETTE_X - 132,
            Y = SWATCHES_Y,
            ZIndex = 1
        };

        Picker.Closed += ApplyPickedColour;
        AddChild(Picker);
        PickerCancel = AddButton("Cancel", 60, Picker.X, Picker.Y + Picker.Height + 2, ClosePicker);
        PickerCancel.ZIndex = 1;
        PickerCancel.Visible = false;
    }

    /// <summary>Raised with SaveDraft, Submit and HandIn (Draft and HandIn modes).</summary>
    public event Action<CollegeActionArgs>? ActionRequested;

    /// <summary>Picture mode's Insert: the picture, and the hash of the picture it replaces (null for a new one).</summary>
    public event Action<PreparedPicture, string?>? PictureMade;

    public ArtCanvasMode Mode { get; private set; }

    private bool IsDirty
        => Editor.Drawing.IsDirty || ((Mode != ArtCanvasMode.Picture) && ((TitleBox.Text != SavedTitle) || (NoteBox.Text != SavedNote)));

    private string SubmitCaption
        => Mode == ArtCanvasMode.HandIn ? "Hand in"
            : Session?.FreeEntries > 0 ? "Submit (free entry)"
            : $"Submit ({Session?.EntryCost} {Marks(Session?.EntryCost ?? 0)})";

    /// <summary>Opens an Art draft or hand-in (a null piece is a blank canvas).</summary>
    public void Open(CollegeDisplayArgs args)
    {
        Session = args;
        Mode = args.Mode == CollegeWriterMode.HandIn ? ArtCanvasMode.HandIn : ArtCanvasMode.Draft;
        ReplacingHash = null;

        Editor.Load(
            args.Piece?.Blocks.FirstOrDefault(b => b.Kind == CollegeBlockKind.Drawing) is { } block
                ? PixelDrawing.From(block.Palette, block.Pixels)
                : PixelDrawing.Blank());

        TitleBox.Text = args.Piece?.Title ?? string.Empty;
        NoteBox.Text = args.Piece?.Blocks.FirstOrDefault(b => b.Kind == CollegeBlockKind.Text)?.Text ?? string.Empty;

        CaptionLabel.Text = Mode == ArtCanvasMode.HandIn
            ? OneLine(args.Prompt.Length > 0 ? $"Hand-in: {args.Prompt}" : "Art: class hand-in")
            : "Art: draft";

        Begin();
    }

    /// <summary>Opens Picture mode for the writing window: a blank canvas, or a drawing being edited.</summary>
    public void OpenPicture(PixelDrawing? drawing, string? replacing)
    {
        Session = null;
        Mode = ArtCanvasMode.Picture;
        ReplacingHash = replacing;
        Editor.Load(drawing ?? PixelDrawing.Blank());
        TitleBox.Text = string.Empty;
        NoteBox.Text = string.Empty;
        CaptionLabel.Text = replacing is null ? "Draw a picture" : "Edit the picture";
        Begin();
    }

    /// <summary>Applies a WriterResult for this window's session. True when a hidden window was shown again (a refused save).</summary>
    public bool ShowResult(CollegeDisplayArgs args)
    {
        if (Session is null || (Mode == ArtCanvasMode.Picture) || (args.Mode != Session.Mode) || (args.Subject != Session.Subject))
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

                //drawing done while the save was on its way keeps the window open
                if (!IsDirty)
                    Hide();

                break;
            case CollegeWriterResult.Refused:
                ClosingOnSave = false;
                Editor.Drawing.MarkDirty();

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
        if (Visible && Session is not null && (Mode == ArtCanvasMode.Draft) && IsDirty)
            Send(CollegeActionType.SaveDraft);
    }

    public override void Hide()
    {
        if (!Visible)
            return;

        TitleBox.IsFocused = false;
        NoteBox.IsFocused = false;
        ClosePicker();
        base.Hide();
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            if (Picker.Visible)
                ClosePicker();
            else
                OnCloseClicked();

            e.Handled = true;

            return;
        }

        if (e.Accelerator && !TitleBox.IsFocused && !NoteBox.IsFocused && e.Keycode is Keycode.Z or Keycode.Y)
        {
            if (e.Keycode == Keycode.Z)
                Editor.Undo();
            else
                Editor.Redo();

            DisarmSubmit();
            RefreshControls();
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    public override void Update(GameTime gameTime)
    {
        if (Visible)
        {
            var counter = $"{NoteBox.Text.Length}/{CollegeProtocol.MAX_ART_NOTE_CHARS}";

            if (NoteCounter.Text != counter)
                NoteCounter.Text = counter;
        }

        base.Update(gameTime);
    }

    private void Begin()
    {
        SavedTitle = TitleBox.Text;
        SavedNote = NoteBox.Text;
        Editor.Drawing.MarkClean();
        CloseArmed = false;
        ClosingOnSave = false;
        ClosePicker();

        var picture = Mode == ArtCanvasMode.Picture;
        TitleBox.Visible = !picture;
        NoteBox.Visible = !picture;
        NoteCounter.Visible = !picture;
        TitleBox.IsFocused = false;
        NoteBox.IsFocused = false;
        SaveButton.Visible = Mode == ArtCanvasMode.Draft;
        SubmitButton.Visible = !picture;
        SubmitButton.X = Mode == ArtCanvasMode.HandIn ? LEFT : SUBMIT_X;
        InsertButton.Visible = picture;
        CancelButton.Visible = picture;
        Status.Text = string.Empty;
        DisarmSubmit();
        RefreshControls();
        Show();
    }

    private void RefreshControls()
    {
        foreach (var (tool, button) in ToolButtons)
            button.Selected = Editor.Tool == tool;

        SizeButton.Caption = $"Size {Editor.BrushSize}";
        MirrorButton.Selected = Editor.Mirror;
        GridButton.Selected = Canvas.ShowGrid;
        UndoButton.Enabled = Editor.Drawing.CanUndo;
        RedoButton.Enabled = Editor.Drawing.CanRedo;

        var colour = Editor.Drawing.Palette[Editor.Current];
        CurrentBox.BackgroundColor = colour;
        CurrentHex.Text = $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}";
    }

    private CustomButton ToolButton(string caption, int y, Action onClick)
        => AddButton(
            caption,
            TOOL_WIDTH,
            LEFT,
            y,
            () =>
            {
                onClick();
                DisarmSubmit();
                RefreshControls();
            });

    private void OpenPicker(int swatch)
    {
        EditingSwatch = swatch;
        Picker.Open(Editor.Drawing.Palette[swatch]);
        PickerCancel.Visible = true;
    }

    //StageColorPicker's OK hides the picker, then raises Closed
    private void ApplyPickedColour()
    {
        if (EditingSwatch >= 0)
            Editor.SetColour(EditingSwatch, Picker.Current);

        EditingSwatch = -1;
        PickerCancel.Visible = false;
        DisarmSubmit();
        RefreshControls();
    }

    private void ClosePicker()
    {
        EditingSwatch = -1;
        Picker.Visible = false;
        PickerCancel.Visible = false;
    }

    private void OnSaveClicked()
    {
        DisarmSubmit();
        CloseArmed = false;

        if ((Mode != ArtCanvasMode.Draft) || Session is null)
            return;

        Status.Text = SAVING;
        Send(CollegeActionType.SaveDraft);
    }

    private void OnSubmitClicked()
    {
        CloseArmed = false;

        if (Session is null)
            return;

        if (Mode == ArtCanvasMode.HandIn)
        {
            if (Editor.Drawing.DrawnPixels < CollegeProtocol.MIN_HAND_IN_DRAWN_PIXELS)
            {
                Status.Text = DRAW_MORE_HAND_IN;

                return;
            }

            Status.Text = "Handing in...";
            Send(CollegeActionType.HandIn);

            return;
        }

        if (TitleBox.Text.Trim().Length == 0)
        {
            DisarmSubmit();
            Status.Text = "Give your drawing a title.";

            return;
        }

        if (Editor.Drawing.DrawnPixels < CollegeProtocol.MIN_ENTRY_DRAWN_PIXELS)
        {
            DisarmSubmit();
            Status.Text = DRAW_MORE;

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

    private void OnInsertClicked()
    {
        if (Mode != ArtCanvasMode.Picture)
            return;

        PictureMade?.Invoke(DrawingPictures.ToPicture(Editor.Drawing), ReplacingHash);
        Editor.Drawing.MarkClean();
        Hide();
    }

    private void OnCloseClicked()
    {
        DisarmSubmit();

        //a second Close while the save is on its way hides anyway; a refusal still brings the piece back
        if ((Mode == ArtCanvasMode.Draft) && Session is not null && !ClosingOnSave && IsDirty)
        {
            ClosingOnSave = true;
            Status.Text = SAVING;
            Send(CollegeActionType.SaveDraft);

            return;
        }

        if ((Mode != ArtCanvasMode.Draft) && IsDirty && !CloseArmed)
        {
            CloseArmed = true;
            Status.Text = Mode == ArtCanvasMode.HandIn ? "Close without handing in? Click Close again." : "Close without inserting? Click again.";

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
            new()
            {
                Kind = CollegeBlockKind.Drawing,
                Palette = Editor.Drawing.PaletteBytes(),
                Pixels = Editor.Drawing.Pixels.ToArray()
            }
        };

        if (NoteBox.Text.Trim().Length > 0)
            blocks.Add(new CollegeBlockInfo { Kind = CollegeBlockKind.Text, Text = NoteBox.Text });

        ActionRequested?.Invoke(
            new CollegeActionArgs
            {
                Type = type,
                Subject = CollegeSubjectCode.Art,
                Piece = new CollegePieceInfo
                {
                    Subject = CollegeSubjectCode.Art,
                    Title = TitleBox.Text.Trim(),
                    Blocks = blocks
                }
            });

        //marked saved as it is sent, so drawing done while the reply is on its way still counts as a change
        MarkSaved();
    }

    private void MarkSaved()
    {
        SavedTitle = TitleBox.Text;
        SavedNote = NoteBox.Text;
        Editor.Drawing.MarkClean();
    }

    private void DisarmSubmit()
    {
        SubmitArmed = false;
        SubmitButton.Caption = SubmitCaption;
    }

    private static string OneLine(string text)
    {
        var lines = TextRenderer.WrapText(text, INNER_WIDTH - 8);

        return lines.Count <= 1 ? text : lines[0].TrimEnd() + "...";
    }

    private static string Marks(int count) => count == 1 ? "Mark" : "Marks";
}
```

  Notes for the implementer:
  - `ToolButton` captures `tool` from a `foreach` deconstruction; C# gives each iteration its own variable, so the lambda is correct.
  - If `UIPanel.BackgroundColor` / `BorderColor` are not settable that way, use what `StageColorPicker`'s constructor uses; it sets both.
  - If `CustomTextBox` has no `HintText`, drop that line. (The writer's title box sets it, so it should exist.)

- [ ] **Step 4: Build.** Run the Verify build command. Expected: Build succeeded. Fix any missing `using` the build reports by matching `CollegeWriterControl.cs` and `PumpkinCarvingControl.cs`.

```json:metadata
{"files": ["Chaos.Client/Controls/World/Popups/College/ArtCanvas.cs", "Chaos.Client/Controls/World/Popups/College/ArtSwatches.cs", "Chaos.Client/Controls/World/Popups/College/ArtCanvasControl.cs"], "verifyCommand": "dotnet build Chaos.Client/Chaos.Client.csproj -p:UnoraServerPath=C:\\Users\\Michael\\Documents\\GitHub\\worktrees\\college-art-server", "acceptanceCriteria": ["layout A fits 560x464", "tools/mouse/right-erase/line preview", "picker OK applies as one step, Cancel/Escape not", "Ctrl+Z/Y outside text boxes", "Draft/HandIn/Picture behaviours", "client-side minimum warnings"], "modelTier": "standard"}
```

---

### Task 8: Client reader, routing and the gallery grid (client repo)

**Goal:** The reader shows drawings, Art `OpenWriter` and `WriterResult` reach the canvas, `Drawing` replies feed the cache, and the gallery's Art tab is an 8-picture grid.

**Files:**
- Modify: `Chaos.Client/Controls/World/Popups/College/PieceView.cs`
- Create: `Chaos.Client/Controls/World/Popups/College/GalleryArtCell.cs`
- Modify: `Chaos.Client/Controls/World/Popups/College/CollegeListWindow.cs`
- Modify: `Chaos.Client/Controls/World/Popups/College/CollegeGalleryControl.cs`
- Modify: `Chaos.Client/Screens/WorldScreen.College.cs`
- Modify: `Chaos.Client/Screens/WorldScreen.cs` (logout clear, line ~1164)
- Modify: `Chaos.Client/ViewModel/College/WritingDocument.cs` (`From` skips drawing blocks)

**Acceptance Criteria:**
- [ ] `PieceView` shows a drawing block at 384 x 288, centred, and disposes its own drawing textures (not the transfer-owned picture textures)
- [ ] Art `OpenWriter` opens the canvas; other subjects open the writer; `WriterResult` for Art goes to the canvas
- [ ] Logging out saves an open Art draft and clears the thumbnail cache
- [ ] The Art tab shows 8 cells per page (4 x 2), framed in the award colour, with title and author. Click selects; double-click or Read opens the reader.
- [ ] Each visible cell without a thumbnail sends one `DrawingFetch` and shows "Loading..." until the `Drawing` arrives. The other tabs keep their rows and headers.

**Verify:** the client builds (`dotnet build Chaos.Client/Chaos.Client.csproj -p:UnoraServerPath=...`), and the whole client suite passes (`dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=... -- --no-ansi`)

**Steps:**

- [ ] **Step 1: PieceView.** In `PieceView.cs`:
  - add `using Chaos.Client.ViewModel.College;` and the field `private readonly List<Texture2D> OwnedTextures = [];` (add `using Microsoft.Xna.Framework.Graphics;`);
  - in `Layout`'s switch, add:

```csharp
                case CollegeBlockKind.Drawing:
                    y += AddDrawing(block, y);

                    break;
```

  - add the method:

```csharp
    private int AddDrawing(CollegeBlockInfo block, int y)
    {
        var texture = DrawingTextures.Build(PixelDrawing.From(block.Palette, block.Pixels), CollegeProtocol.DRAWING_ZOOM);
        OwnedTextures.Add(texture);

        Content.AddChild(
            new UIImage
            {
                Texture = texture,
                X = Math.Max(0, (Content.Width - texture.Width) / 2),
                Y = y,
                Width = texture.Width,
                Height = texture.Height,
                IsHitTestVisible = false
            });

        return texture.Height;
    }
```

  - at the end of `ClearContent` (after `Waiting.Clear();`) add:

```csharp
        foreach (var texture in OwnedTextures)
            texture.Dispose();

        OwnedTextures.Clear();
```

  (`ClearContent` already nulls every `UIImage.Texture` before disposing the image. So the drawing textures are disposed only here, once.)

- [ ] **Step 2: WritingDocument.From.** In `WritingDocument.From`, inside the `foreach`, add first: `if (block.Kind == CollegeBlockKind.Drawing) continue;`. A writing piece never holds a drawing block; this keeps a stray one from becoming a text block.

- [ ] **Step 3: CollegeListWindow hooks.** In `CollegeListWindow.cs`:
  - Add the field `private readonly List<UILabel> Headers = [];`. In the constructor's column loop, change `Caption(columns[i].Header, ...)` to `Headers.Add(Caption(columns[i].Header, ...));`.
  - Add these protected members after `Items`:

```csharp
    /// <summary>Items per page; the rows hold at most their own count.</summary>
    protected virtual int PageSize => Rows.Length;

    /// <summary>False while a subclass shows the page another way (the gallery's picture grid): rows and headers hide.</summary>
    protected virtual bool ShowsRows => true;

    /// <summary>Called with the page's items each time the rows are shown.</summary>
    protected virtual void PageShown(IReadOnlyList<TItem> shown) { }

    /// <summary>Called after the selection changes.</summary>
    protected virtual void SelectionChanged(int id) { }

    protected void SelectId(int id) => Select(id);

    protected void OpenId(int id) => OpenItem(id);
```

  - Replace `ShowRows` with:

```csharp
    protected void ShowRows()
    {
        var items = Items;
        var pageSize = PageSize;
        var pages = Math.Max(1, (items.Count + pageSize - 1) / pageSize);
        Page = Math.Clamp(Page, 0, pages - 1);

        var shown = items.Skip(Page * pageSize)
                         .Take(pageSize)
                         .ToList();

        for (var i = 0; i < Rows.Length; i++)
        {
            Rows[i].Visible = ShowsRows && (i < shown.Count);

            if (Rows[i].Visible)
                Rows[i]
                    .Set(IdOf(shown[i]), ColumnsOf(shown[i]));
        }

        foreach (var header in Headers)
            header.Visible = ShowsRows;

        Empty.Visible = shown.Count == 0;
        PageLabel.Text = $"Page {Page + 1} of {pages}";
        PrevButton.Enabled = Page > 0;
        NextButton.Enabled = Page < (pages - 1);
        PageShown(shown);

        var ids = shown.Select(IdOf)
                       .ToList();

        Select(ids.Contains(SelectedId) ? SelectedId : ids.FirstOrDefault());
    }
```

  - At the end of the private `Select`, add `SelectionChanged(id);`.

- [ ] **Step 4: GalleryArtCell.** Create `Chaos.Client/Controls/World/Popups/College/GalleryArtCell.cs`:

```csharp
#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.ViewModel.College;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>One gallery picture: the drawing at its real size in a frame of its award colour, the title and the author below.</summary>
public sealed class GalleryArtCell : UIPanel
{
    public const int WIDTH = 117;
    public const int HEIGHT = PICTURE_HEIGHT + (2 * FRAME) + 2 + (2 * TextRenderer.CHAR_HEIGHT);
    private const int FRAME = 2;
    private const int PICTURE_HEIGHT = PixelDrawing.HEIGHT;
    private const int PICTURE_WIDTH = PixelDrawing.WIDTH;

    private static readonly Color SelectedFill = new(255, 255, 255, 36);

    private readonly UILabel AuthorLabel;
    private readonly UILabel TitleLabel;
    private Color Frame;

    public GalleryArtCell()
    {
        Width = WIDTH;
        Height = HEIGHT;
        Visible = false;

        var textTop = PICTURE_HEIGHT + (2 * FRAME) + 2;
        TitleLabel = Label(textTop, LegendColors.White);
        AuthorLabel = Label(textTop + TextRenderer.CHAR_HEIGHT, LegendColors.Gray);
    }

    public int Id { get; private set; }
    public bool Selected { get; set; }

    /// <summary>The thumbnail, owned by <c>CollegeDrawings</c>; null shows "Loading...".</summary>
    public Texture2D? Picture { get; set; }

    public event Action? Clicked;
    public event Action? DoubleClicked;

    public void Set(int id, string title, string author, Color frame)
    {
        Id = id;
        TitleLabel.Text = title;
        AuthorLabel.Text = author;
        Frame = frame;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        if (Selected)
            DrawRectClipped(spriteBatch, new Rectangle(ScreenX, ScreenY, Width, Height), SelectedFill);

        var left = ScreenX + ((WIDTH - PICTURE_WIDTH - (2 * FRAME)) / 2);
        DrawRectClipped(spriteBatch, new Rectangle(left, ScreenY, PICTURE_WIDTH + (2 * FRAME), PICTURE_HEIGHT + (2 * FRAME)), Frame);

        if (Picture is not null)
            DrawTexture(spriteBatch, Picture, new Vector2(left + FRAME, ScreenY + FRAME), Color.White);
        else
        {
            DrawRectClipped(spriteBatch, new Rectangle(left + FRAME, ScreenY + FRAME, PICTURE_WIDTH, PICTURE_HEIGHT), Color.Black);
            DrawTextClipped(spriteBatch, new Vector2(left + FRAME + 18, ScreenY + FRAME + 30), "Loading...", LegendColors.Gray, false);
        }

        base.Draw(spriteBatch);
    }

    public override void OnClick(ClickEvent e)
    {
        e.Handled = true;
        Clicked?.Invoke();
    }

    public override void OnDoubleClick(DoubleClickEvent e)
    {
        e.Handled = true;
        DoubleClicked?.Invoke();
    }

    private UILabel Label(int y, Color color)
    {
        var label = new UILabel
        {
            X = 0,
            Y = y,
            Width = WIDTH,
            Height = TextRenderer.CHAR_HEIGHT,
            HorizontalAlignment = HorizontalAlignment.Center,
            ForegroundColor = color,
            IsHitTestVisible = false
        };

        AddChild(label);

        return label;
    }
}
```

  (Match `DrawTextClipped`'s argument list to how `StageColorPicker.Draw` calls it.)

- [ ] **Step 5: CollegeGalleryControl.** In `CollegeGalleryControl.cs`:
  - Add `using Chaos.Client.Systems.College;`.
  - Add the constants `private const int ART_COLUMNS = 4; private const int ART_PAGE = 8; private const int CELL_GAP = 4;`.
  - Add the fields `private readonly GalleryArtCell[] Cells; private readonly CollegeDrawings Drawings;`.
  - Change the constructor signature to `public CollegeGalleryControl(CollegeDrawings drawings)`, and at its end add:

```csharp
        Drawings = drawings;
        Drawings.DrawingReady += OnDrawingReady;

        Cells = new GalleryArtCell[ART_PAGE];

        for (var i = 0; i < ART_PAGE; i++)
        {
            var cell = new GalleryArtCell
            {
                X = LEFT + ((i % ART_COLUMNS) * GalleryArtCell.WIDTH),
                Y = TABBED_HEADER_TOP + ((i / ART_COLUMNS) * (GalleryArtCell.HEIGHT + CELL_GAP))
            };

            cell.Clicked += () => SelectId(cell.Id);
            cell.DoubleClicked += () => OpenId(cell.Id);
            AddChild(cell);
            Cells[i] = cell;
        }
```

  - Add the members:

```csharp
    private bool IsArt => Subject == CollegeSubjectCode.Art;

    protected override int PageSize => IsArt ? ART_PAGE : base.PageSize;

    protected override bool ShowsRows => !IsArt;

    public override void Dispose()
    {
        Drawings.DrawingReady -= OnDrawingReady;

        //the thumbnails belong to CollegeDrawings
        foreach (var cell in Cells)
            cell.Picture = null;

        base.Dispose();
    }

    protected override void PageShown(IReadOnlyList<CollegeGalleryRowInfo> shown)
    {
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
    }

    protected override void SelectionChanged(int id)
    {
        foreach (var cell in Cells)
            cell.Selected = cell.Visible && (cell.Id == id);
    }

    private void OnDrawingReady(int id)
    {
        foreach (var cell in Cells)
            if (cell.Visible && (cell.Id == id) && Drawings.TryGetTexture(id, out var texture))
                cell.Picture = texture;
    }
```

  `PageShown` runs inside `ShowRows`, which the base constructor can't reach before `Cells` exists. It is only called from `Open` and from page turns. Keep a null guard anyway: `if (Cells is null) return;` at the top of `PageShown` and `SelectionChanged`. The base constructor calls `AddSelectionButton`, and `Select` may run then.

- [ ] **Step 6: WorldScreen.College.** In `WorldScreen.College.cs`:
  - add `required` properties `ArtCanvasControl Canvas`, `ArtCanvasControl PictureCanvas` and `CollegeDrawings Drawings` to `CollegeWindows`;
  - in `BuildCollege`, create `var drawings = new CollegeDrawings();` and pass it with `Gallery = new CollegeGalleryControl(drawings) { ZIndex = 2 }`. Add `Drawings = drawings`, `Canvas = new ArtCanvasControl { ZIndex = 2 }` and `PictureCanvas = new ArtCanvasControl { ZIndex = 2 }` to the initializer;
  - after the existing `ActionRequested` wiring add `windows.Canvas.ActionRequested += SendCollegeAction;`, then `Root.AddChild(windows.Canvas);` and `Root.AddChild(windows.PictureCanvas);` before `Root.AddChild(votesPopup);`. Task 9 wires `PictureCanvas` to the writer.
  - change `SaveCollegeDraft` to:

```csharp
    private void SaveCollegeDraft()
    {
        College?.Writer.SaveIfDirty();
        College?.Canvas.SaveIfDirty();
    }
```

  - in `HandleCollegeDisplay`, replace the `OpenWriter` and `WriterResult` cases and add `Drawing`:

```csharp
            case CollegeDisplayType.OpenWriter when args.Subject == CollegeSubjectCode.Art:
                BringToFront(College.Canvas);
                College.Canvas.Open(args);

                break;
            case CollegeDisplayType.OpenWriter:
                BringToFront(College.Writer);
                College.Writer.Open(args);

                break;
            case CollegeDisplayType.WriterResult when args.Subject == CollegeSubjectCode.Art:
                if (College.Canvas.ShowResult(args))
                    BringToFront(College.Canvas);

                break;
            case CollegeDisplayType.WriterResult:
                //a refused save shows a hidden writer again, over the other windows
                if (College.Writer.ShowResult(args))
                    BringToFront(College.Writer);

                break;
            case CollegeDisplayType.Drawing:
                College.Drawings.OnDrawing(args);

                break;
```

  - Update the class summary to `/// <summary>The Mileth College windows: writer, Art canvas, reader, judging list, gallery and hand-in list.</summary>`.

- [ ] **Step 7: Logout.** In `WorldScreen.cs`, after `College?.Transfers.Clear();` (around line 1164), add `College?.Drawings.Clear();`.

- [ ] **Step 8: Build and test.** Run the Verify commands. Expected: Build succeeded; the client suite passes.

```json:metadata
{"files": ["Chaos.Client/Controls/World/Popups/College/PieceView.cs", "Chaos.Client/Controls/World/Popups/College/GalleryArtCell.cs", "Chaos.Client/Controls/World/Popups/College/CollegeListWindow.cs", "Chaos.Client/Controls/World/Popups/College/CollegeGalleryControl.cs", "Chaos.Client/Screens/WorldScreen.College.cs", "Chaos.Client/Screens/WorldScreen.cs", "Chaos.Client/ViewModel/College/WritingDocument.cs"], "verifyCommand": "dotnet build Chaos.Client/Chaos.Client.csproj -p:UnoraServerPath=C:\\Users\\Michael\\Documents\\GitHub\\worktrees\\college-art-server", "acceptanceCriteria": ["reader shows and disposes drawings", "Art OpenWriter/WriterResult to canvas", "logout saves Art draft, clears cache", "Art tab 4x2 grid with frames and selection", "one DrawingFetch per visible missing thumbnail; other tabs unchanged"], "modelTier": "standard"}
```

---

### Task 9: Draw and Edit in the writing window (client repo)

**Goal:** Writers draw a picture into a writing piece, and they can edit a picture they drew.

**Files:**
- Modify: `Chaos.Client/ViewModel/College/WritingDocument.cs` (`ReplacePicture`)
- Modify: `Chaos.Client/Controls/World/Popups/College/CollegeWriterControl.cs`
- Modify: `Chaos.Client/Screens/WorldScreen.College.cs`
- Test: `Tests/Chaos.Client.Tests/College/WritingDocumentTests.cs`

**Acceptance Criteria:**
- [ ] The footer reads Heading (64), Picture (60), Draw (46), Save (50), Submit (130), then the counter, without overlap
- [ ] **Draw** is refused at 5 pictures; otherwise it opens the picture canvas on a blank drawing
- [ ] **Insert** puts the PNG at the caret and uploads it through the part 2 path
- [ ] A picture made by the canvas shows **Edit**. Edit reopens it, and **Insert** replaces that block's hash, refusing a duplicate.
- [ ] A picture finished after the writer closed or moved to another piece is dropped

**Verify:** `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\Users\Michael\Documents\GitHub\worktrees\college-art-server -- --treenode-filter "/*/*/WritingDocumentTests/*"` -> all pass; the client builds

**Steps:**

- [ ] **Step 1: Write the failing tests.** Add to `WritingDocumentTests`:

```csharp
    private static readonly string HashB = new('b', 64);

    [Test]
    public void A_picture_can_be_replaced_by_a_new_one()
    {
        var doc = WithText("before\nafter");
        doc.InsertPicture(0, 7, HashA);
        doc.MarkClean();

        doc.ReplacePicture(HashA, HashB).Should().BeTrue();

        doc.Blocks.Single(b => b.Kind == WritingBlockKind.Picture).Hash.Should().Be(HashB);
        doc.IsDirty.Should().BeTrue();
    }

    [Test]
    public void Replacing_refuses_a_missing_or_duplicate_picture()
    {
        var doc = WithText("before\nafter");
        doc.InsertPicture(0, 7, HashA);
        doc.InsertPicture(2, 0, HashB);

        doc.ReplacePicture(HashA, HashB).Should().BeFalse();
        doc.ReplacePicture(new string('c', 64), new string('d', 64)).Should().BeFalse();
    }

    [Test]
    public void A_drawing_block_never_becomes_text()
    {
        var doc = WritingDocument.From(
            new CollegePieceInfo
            {
                Title = "T",
                Blocks = [new CollegeBlockInfo { Kind = CollegeBlockKind.Drawing }, new CollegeBlockInfo { Kind = CollegeBlockKind.Text, Text = "x" }]
            });

        Shape(doc).Should().Be("T:x");
    }
```

  (If `HashB` already exists in the class, don't add it again.)

- [ ] **Step 2: Run to see them fail.** Run the `WritingDocumentTests` filter. Expected: build error, `ReplacePicture` missing.

- [ ] **Step 3: ReplacePicture.** Add to `WritingDocument` after `RemovePicture`:

```csharp
    /// <summary>Swaps a picture for an edited one in the same place. False when the old one is gone or the new one is already in the piece.</summary>
    public bool ReplacePicture(string oldHash, string newHash)
    {
        if (Blocks.Any(b => (b.Kind == WritingBlockKind.Picture) && (b.Hash == newHash))
            || Blocks.FirstOrDefault(b => (b.Kind == WritingBlockKind.Picture) && (b.Hash == oldHash)) is not { } block)
            return false;

        block.Hash = newHash;
        IsDirty = true;

        return true;
    }
```

- [ ] **Step 4: Writer footer.** In `CollegeWriterControl.cs`, replace the footer constants:

```csharp
    private const int HEADING_X = LEFT;
    private const int PICTURE_X = HEADING_X + 64 + 6;
    private const int DRAW_X = PICTURE_X + 60 + 6;
    private const int SAVE_X = DRAW_X + 46 + 6;
    private const int SUBMIT_X = SAVE_X + 50 + 6;
```

  In the constructor, replace the four footer `AddButton` lines with:

```csharp
        AddButton("Heading", 64, HEADING_X, BUTTONS_TOP, OnHeadingClicked);
        AddButton("Picture", 60, PICTURE_X, BUTTONS_TOP, OnPictureClicked);
        AddButton("Draw", 46, DRAW_X, BUTTONS_TOP, OnDrawClicked);
        SaveButton = AddButton("Save", 50, SAVE_X, BUTTONS_TOP, OnSaveClicked);
        SubmitButton = AddButton("Submit", SUBMIT_WIDTH, SUBMIT_X, BUTTONS_TOP, OnSubmitClicked);
```

  (`COUNTER_X` stays `SUBMIT_X + SUBMIT_WIDTH + 6`, which is now 396.)

- [ ] **Step 5: Draw and Edit.** In `CollegeWriterControl.cs`:
  - Add `using Chaos.Client.ViewModel.College;` if missing.
  - Add the fields:

```csharp
    //hashes known to be canvas drawings, and those checked and found not to be
    private readonly HashSet<string> DrawingHashes = new(StringComparer.Ordinal);
    private readonly HashSet<string> NotDrawings = new(StringComparer.Ordinal);

    //the session a Draw or Edit was started for; its picture goes nowhere else
    private CollegeDisplayArgs? DrawSession;
```

  - Add the event `public event Action<PixelDrawing?, string?>? DrawRequested;` with the summary `/// <summary>Draw (null, null) or Edit (the drawing, its hash): open the picture canvas.</summary>`.
  - Add the methods:

```csharp
    /// <summary>The picture canvas's Insert: a new picture at the caret, or an edited one in place of <paramref name="replacing" />.</summary>
    public void InsertDrawnPicture(PreparedPicture picture, string? replacing)
    {
        if (!Visible || !ReferenceEquals(DrawSession, Session))
            return;

        PullText();
        (int Index, int Caret)? at = null;

        if (replacing is not null)
        {
            if (!Document.ReplacePicture(replacing, picture.Hash))
            {
                Status.Text = "That picture is already in this piece.";

                return;
            }

            Uploading.Remove(replacing);
        } else
        {
            if (Document.PictureCount >= CollegeProtocol.MAX_PICTURES)
            {
                Status.Text = $"A piece can hold {CollegeProtocol.MAX_PICTURES} pictures at most.";

                return;
            }

            var index = Math.Clamp(FocusIndex, 0, Document.Blocks.Count - 1);

            if (Document.InsertPicture(index, FocusCaret, picture.Hash) is not { } inserted)
            {
                Status.Text = "That picture is already in this piece.";

                return;
            }

            at = inserted;
        }

        DrawingHashes.Add(picture.Hash);
        Uploading.Add(picture.Hash);
        Transfers.Upload(picture);
        Status.Text = UPLOADING;
        Rebuild(at, at is not null);
    }

    private void OnDrawClicked()
    {
        DisarmSubmit();
        CloseArmed = false;

        if (Document.PictureCount >= CollegeProtocol.MAX_PICTURES)
        {
            Status.Text = $"A piece can hold {CollegeProtocol.MAX_PICTURES} pictures at most.";

            return;
        }

        DrawSession = Session;
        DrawRequested?.Invoke(null, null);
    }

    private void OnEditClicked(string hash)
    {
        DisarmSubmit();
        CloseArmed = false;

        if (Transfers.BytesOf(hash) is not { } bytes || DrawingPictures.TryRead(bytes) is not { } drawing)
        {
            Status.Text = "That picture can't be edited.";

            return;
        }

        DrawSession = Session;
        DrawRequested?.Invoke(drawing, hash);
    }

    private bool IsDrawing(string hash)
    {
        if (DrawingHashes.Contains(hash))
            return true;

        if (NotDrawings.Contains(hash) || Transfers.BytesOf(hash) is not { } bytes)
            return false;

        if (DrawingPictures.TryRead(bytes) is null)
        {
            NotDrawings.Add(hash);

            return false;
        }

        DrawingHashes.Add(hash);

        return true;
    }
```

  - In `PictureView`, after the `RemoveButton` wiring, add:

```csharp
        view.EditButton.Clicked += () => OnEditClicked(hash);
        view.Loaded += () => view.EditButton.Visible = IsDrawing(hash);
```

  - In the nested `PictureBlock`:
    - add `private const int EDIT_WIDTH = 36;`, `public CustomButton EditButton { get; }` and `public event Action? Loaded;`;
    - in its constructor, set the `Label` width to `width - REMOVE_WIDTH - EDIT_WIDTH - 8`, then add after `RemoveButton`:

```csharp
            EditButton = new CustomButton("Edit", EDIT_WIDTH)
            {
                X = width - REMOVE_WIDTH - 4 - EDIT_WIDTH,
                Visible = false
            };
```

    - add `AddChild(EditButton);` after `AddChild(RemoveButton);`;
    - in `Refresh`, at the end (after `Height = Math.Max(texture.Height, CustomButton.HEIGHT);`), add `Loaded?.Invoke();`.

  The picture is drawn centred, under the two buttons on the right edge. A 384-px drawing in the ~490-px content width leaves those buttons clear, as the remove button is today.

- [ ] **Step 6: Wire the picture canvas.** In `WorldScreen.College.cs` `BuildCollege`, after the `ActionRequested` wiring, add:

```csharp
        windows.PictureCanvas.PictureMade += windows.Writer.InsertDrawnPicture;

        windows.Writer.DrawRequested += (drawing, replacing) =>
        {
            BringToFront(windows.PictureCanvas);
            windows.PictureCanvas.OpenPicture(drawing, replacing);
        };
```

- [ ] **Step 7: Run the tests and build.** Run the `WritingDocumentTests` filter. Expected: all pass. Then build the client and run the whole client suite. Expected: both pass.

```json:metadata
{"files": ["Chaos.Client/ViewModel/College/WritingDocument.cs", "Chaos.Client/Controls/World/Popups/College/CollegeWriterControl.cs", "Chaos.Client/Screens/WorldScreen.College.cs", "Tests/Chaos.Client.Tests/College/WritingDocumentTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:\\Users\\Michael\\Documents\\GitHub\\worktrees\\college-art-server -- --treenode-filter \"/*/*/WritingDocumentTests/*\"", "acceptanceCriteria": ["footer fits", "Draw refused at 5 pictures", "Insert at caret via upload path", "Edit on canvas-made pictures replaces hash, refuses duplicates", "stale-session pictures dropped"], "modelTier": "standard"}
```

---

### Task 10: Full checks, docs and the spec record (all repos)

**Goal:** Both suites are at their baselines, both builds are clean, the client docs list the new controls, and the spec records the plan-time adjustments.

**Files:**
- Modify (client worktree): `CLAUDE.md` (College line under Popups)
- Modify (client main checkout): `docs/superpowers/specs/2026-10-05-mileth-college-art-design.md`

**Acceptance Criteria:**
- [ ] Server: `dotnet build Chaos.slnx` succeeds. The full suite fails only the known baseline tests.
- [ ] Client: the `Chaos.Client.csproj` build succeeds and the full client suite passes
- [ ] `CLAUDE.md` names `ArtCanvasControl` (with `ArtCanvas`, `ArtSwatches`), `GalleryArtCell`, `ViewModel/College/PixelDrawing` + `ArtEditor`, and `Systems/College/DrawingPictures` + `CollegeDrawings`
- [ ] The spec says 560 x 464 for the canvas window and "Read" for the gallery button, and lists the Unora text edits under Content

**Verify:** `dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj` in the server worktree -> only baseline failures. `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=... -- --no-ansi` in the client worktree -> all pass.

**Steps:**

- [ ] **Step 1: Server.** In the server worktree, run `dotnet build Chaos.slnx`, then the full suite. Record the pass and fail counts. Any failure outside the baseline is a regression: fix it before going on.

- [ ] **Step 2: Client.** In the client worktree, build `Chaos.Client/Chaos.Client.csproj` with `UnoraServerPath`, then run the full client suite. Record the counts.

- [ ] **Step 3: Client CLAUDE.md.** In the client worktree's `CLAUDE.md`, extend the `College/` entry in the Popups paragraph so it also reads:

  > ArtCanvasControl — the Art canvas window (Draft, HandIn and Picture modes; ArtCanvas the zoom-4 drawing, ArtSwatches the 32 swatches, the Stage Lighting colour picker for a swatch); GalleryArtCell — one picture in the gallery's Art grid; the drawing model is `ViewModel/College/PixelDrawing` with `ArtEditor` (tools, mirror, undo); `Systems/College/DrawingPictures` turns a drawing into a 384 x 288 PNG and back, and `CollegeDrawings` keeps gallery thumbnails.

- [ ] **Step 4: Spec record.** In the main client checkout (`C:\Users\Michael\Documents\GitHub\Chaos.Client`), edit the spec:
  - Change "560 x 440, the same size as the writing window" to "560 x 464 (the frame's 47-px bottom border leaves no room for a 3-line note under the canvas at 440)".
  - Change "double-click or **View** opens the reader" to "double-click or **Read** opens the reader".
  - In "No map or art changes", add: "Unora changes four dialog texts so they no longer say 'write' (Registrar subject choice, booking format, About page 2, the lectern prompt question)."
  - In "Shipping", change "(+ Unora only if the plan finds a change)" to "+ Unora (dialog text)".

  Leave the spec uncommitted; Task 11 commits it with the plan.

```json:metadata
{"files": ["CLAUDE.md", "docs/superpowers/specs/2026-10-05-mileth-college-art-design.md"], "verifyCommand": "dotnet run --project Tests/Chaos.Tests/Chaos.Tests.csproj", "acceptanceCriteria": ["server build + suite at baseline", "client build + suite pass", "CLAUDE.md lists the new controls and model", "spec records 464, Read, Unora text"], "modelTier": "standard"}
```

---

### Task 11: Commit the full implementation

**Goal:** One commit per repo on `feat/college-art`. The client's `Chaos-Server` gitlink points at the server commit. The spec update is committed on client main.

**Files:**
- All changes from Tasks 1-10

**Acceptance Criteria:**
- [ ] Server, Unora and client each have exactly one new commit on `feat/college-art`, with explicit-path staging only
- [ ] The client commit's `Chaos-Server` gitlink equals the server commit
- [ ] Nothing is pushed

**Verify:** `git -C <each worktree> log --oneline -2` shows the new commit on top of its base. `git -C <client worktree> ls-tree HEAD Chaos-Server` shows the server commit's SHA.

**Steps:**

- [ ] **Step 1: Server commit.** In the server worktree, run `git status --short` and stage each changed and new file by its path (from Tasks 1-4). Write the message to a file with UTF-8 without BOM, then run `git commit -F <file>`:

```
Mileth College part 3: Art pieces, drawing blocks and DrawingFetch

Art opens: an Art piece is a title, one 96 x 72 drawing (32-colour palette plus a swatch per pixel) and a 300-character
note. Submit needs 346 drawn pixels, a hand-in 70. Art activity classes and hand-ins; hand-ins are checked as the
class's subject. DrawingFetch sends one claimed drawing for the gallery's thumbnails. CLIENT_VERSION 773.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01FtxoAKkUAaqcCKBWNB2Gmz
```

- [ ] **Step 2: Unora commit.** In the Unora worktree, stage the four JSON files by path and commit:

```
Mileth College part 3: dialog text for Art entries and activities

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01FtxoAKkUAaqcCKBWNB2Gmz
```

- [ ] **Step 3: Client commit.** In the client worktree, point the submodule at the server commit with `git update-index --cacheinfo 160000,<server sha>,Chaos-Server`. Stage the client files by path (Tasks 5-10, including `CLAUDE.md`), then commit:

```
Mileth College part 3: the Art canvas

ArtCanvasControl (Draft, HandIn and Picture modes) on a pure PixelDrawing/ArtEditor model: pen, eraser, fill, line,
pick, brush sizes 1-3, mirror, 50-step undo, swatch colours through the Stage Lighting picker. The reader shows
drawings, the gallery's Art tab is a picture grid fed by DrawingFetch, and the writing window gets Draw and Edit
(drawings travel as 384 x 288 PNG pictures). Chaos-Server points at the part 3 server commit.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01FtxoAKkUAaqcCKBWNB2Gmz
```

- [ ] **Step 4: Spec commit.** In the main client checkout, run `git add -f docs/superpowers/specs/2026-10-05-mileth-college-art-design.md docs/superpowers/plans/2026-10-05-mileth-college-art.md docs/superpowers/plans/2026-10-05-mileth-college-art.md.tasks.json`. Commit with message `Mileth College part 3: spec record and plan status`, plus the two attribution lines.

- [ ] **Step 5: Report.** Tell the user:
  - the four commit SHAs;
  - that nothing is pushed;
  - the merge order: server, then Unora, then client;
  - the in-game checklist from the spec's Testing section.

```json:metadata
{"files": [], "verifyCommand": "git -C C:\\Users\\Michael\\Documents\\GitHub\\worktrees\\college-art-client ls-tree HEAD Chaos-Server", "acceptanceCriteria": ["one commit per repo on feat/college-art", "client gitlink = server commit", "spec/plan committed on client main", "nothing pushed"], "modelTier": "mechanical"}
```
