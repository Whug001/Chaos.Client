# Josephine's Mirror Map Backdrops Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let players see their new look standing in five real places by putting a picked map backdrop behind the mirror's preview and Review figures.

**Architecture:** A hand-run generator (`Tools/MirrorBackdrops`) renders five Unora maps with DALib and cuts a 260 × 250 picture and a 24 × 24 thumbnail per place. The client embeds those PNGs in `Chaos.Client.Rendering`. In the mirror, `MirrorBackdropCache` cuts each picture to the box being drawn so the standing tile sits under the figure's feet, `MirrorPreview` draws it between the pedestal and the figure, and a six-cell `ThumbnailGrid` row under the preview picks it.

**Tech Stack:** C# 14 / .NET 10, MonoGame 3.8.4 (`Texture2D`), DALib 0.7.0 + SkiaSharp 3.116 (generator only), TUnit + FluentAssertions.

**Spec:** `docs/superpowers/specs/2026-10-04-mirror-backdrops-design.md` (amended in review on 2026-10-04 — read its "Review notes").

## Global Constraints

- Work on branch `feat/mirror-backdrops` in a worktree from client main (the walkthrough names this branch). Run `git submodule update --init` there before the first build.
- Client only. No server, packet, Unora data or `CLIENT_VERSION` change.
- The generator and `MirrorBackdrops` must agree on: picture 260 × 250, standing point (130, 153), thumbnail 24 × 24, keys `mileth`, `woodlands`, `beach`, `frozencave`, `crypt`, resource names `mirrorbackdrop.<key>.png` and `mirrorbackdrop.<key>-thumb.png`.
- Boxes the code asks the cache for: the 230 × 250 preview less a 2 px inset on every side = 226 × 246 (at 1x and 2x); the 260 × 160 Review figures less the inset = 256 × 156 (1x only).
- The figure's feet sit 28 figure pixels below the box centre: `AislingRenderer.CANVAS_CENTER_Y − AislingRenderer.BODY_CENTER_Y`. Use the constants, never the literal.
- Backdrop state is UI-only. It never goes through `WorldState.BeautyShop`, the `Select`/`Hover` funnels, the confirm dialog, the money, the review status or the head-thumbnail cache.
- Layout in the preview column: caption at y 338 (`Backdrop` gray at x 20, place name gold at x 80); picker at x 20, y 350; 28 px cells, 4 px gaps, 188 × 28, no page arrows. It ends at y 378, above the footer total at y 381.
- Display names are ASCII only.
- Code style: `//comment` with no space after the slashes, as these files already do. Comments only say why. No explanatory comments in tests.
- Shared checkout: several Claude sessions use this repo. Stage by explicit path only. Never `git stash`, `git reset`, `git add -A` or `git add .`. `docs/` is gitignored, so docs need `git add -f`.
- Don't build while a client started from this tree is running — it locks `bin`. Ask the user to close it.
- Run tests with `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi` (never `dotnet test`; filter with `--treenode-filter`, never `--filter`).

**User decisions (already made):**
- Places: Plain + Mileth, West Woodlands, Lynith Beach, Frozen Cave, Crypt.
- Picker: a row of six small pictures under the preview (mockup option A), not a named `< >` switcher.
- Backdrops ship as PNGs embedded in the client, made by a generator from the real maps — not drawn live from the player's map files (players only have maps they have visited).
- The spec's "decisions made for you" stand (the user asked for the plan without changing them, 2026-10-04): no picker on Review — NOW and NEW use the chosen backdrop; the choice is remembered while the game runs, starts as Plain after a restart and is not saved to settings; hover tries a backdrop on; backdrops are daylight stills.

---

## File map

| File | Status | Responsibility |
|---|---|---|
| `Tools/MirrorBackdrops/MirrorBackdrops.csproj` | create | Console project for the generator; not in `Chaos.Client.slnx` |
| `Tools/MirrorBackdrops/Program.cs` | create | Places table; renders maps; writes the 10 PNGs; optional contact sheet |
| `Chaos.Client.Rendering/Assets/MirrorBackdrops/*.png` | create (generated) | `<key>.png` 260 × 250 and `<key>-thumb.png` 24 × 24, five places |
| `Chaos.Client.Rendering/Chaos.Client.Rendering.csproj` | modify | Embeds the PNGs as `mirrorbackdrop.<file>` |
| `Chaos.Client.Rendering/MirrorBackdropAssets.cs` | create | Resource names and `Open` for the embedded PNGs |
| `Chaos.Client/Controls/World/Popups/Beauty/MirrorBackdrop.cs` | create | `MirrorBackdrop` enum + static `MirrorBackdrops`: list, names, keys, constants, `SourcePixel` |
| `Chaos.Client/Controls/World/Popups/Beauty/MirrorBackdropCache.cs` | create | Loads PNGs, cuts and caches textures per box, thumbnails, `Clear` |
| `Chaos.Client/Controls/World/Popups/Beauty/ThumbnailGrid.cs` | modify | Optional cell size and no-arrows option |
| `Chaos.Client/Controls/World/Popups/Beauty/MirrorPreview.cs` | modify | Takes the cache; `Backdrop` property; draws it under the figure |
| `Chaos.Client/Controls/World/Popups/Beauty/Pages/ReviewPage.cs` | modify | Takes the cache; `SetView` gains the backdrop |
| `Chaos.Client/Controls/World/Popups/Beauty/BeautyShopControl.cs` | modify | Owns the cache; chosen/hovered state; caption and picker row |
| `Tests/Chaos.Client.Tests/MirrorBackdropTests.cs` | create | List, names, keys, embedded PNGs, `SourcePixel` maths |
| `Tests/Chaos.Client.Tests/ThumbnailGridTests.cs` | create | `WidthFor`/`HeightFor` with and without arrows |
| `docs/superpowers/plans/2026-08-29-beauty-shop-walkthrough.md` | modify | In-game rows B1–B7 |
| `CLAUDE.md` | modify | One clause in the `Beauty/` entry |

---

### Task 1: Backdrop generator tool

**Goal:** Add `Tools/MirrorBackdrops`, a console tool that renders the five maps and writes the ten backdrop PNGs (and, on request, a contact sheet for review).

**Files:**
- Create: `Tools/MirrorBackdrops/MirrorBackdrops.csproj`
- Create: `Tools/MirrorBackdrops/Program.cs`
- Create (generated): `Chaos.Client.Rendering/Assets/MirrorBackdrops/{mileth,woodlands,beach,frozencave,crypt}.png` and `…-thumb.png`

**Acceptance Criteria:**
- [ ] `dotnet run --project Tools/MirrorBackdrops/MirrorBackdrops.csproj -c Release -- --sheet <path>` run from the repo root prints ten `wrote … 260x250` / `wrote … 24x24` lines and one `wrote <path> 1360x390` line, and exits 0.
- [ ] `Chaos.Client.Rendering/Assets/MirrorBackdrops/` holds exactly ten PNGs: five 260 × 250 and five 24 × 24 `-thumb` files.
- [ ] Run from any other folder, the tool prints `run this from the Chaos.Client repo root` and exits 1.
- [ ] The tool is not added to `Chaos.Client.slnx`.

**Verify:** `dotnet run --project Tools/MirrorBackdrops/MirrorBackdrops.csproj -c Release -- --sheet "$env:TEMP\mirror-backdrops-sheet.png"` (PowerShell, repo root) → ten PNG lines plus the sheet line, exit code 0.

**Steps:**

- [ ] **Step 1: Create the project file**

`Directory.Build.props` and `Directory.Packages.props` at the repo root already supply net10.0, C# 14, nullable, implicit usings and the central versions (DALib 0.7.0, SkiaSharp 3.116.0). DALib 0.7.0 from NuGet has `Graphics.RenderMap(MapFile, DataArchive, DataArchive, int, MapImageCache?)`, so no local dalib reference is needed.

`Tools/MirrorBackdrops/MirrorBackdrops.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <!-- Cuts the Josephine's Mirror backdrops out of real maps. Run by hand; not part of Chaos.Client.slnx. -->
    <PropertyGroup>
        <OutputType>Exe</OutputType>
        <IsPackable>false</IsPackable>
        <RootNamespace>MirrorBackdrops</RootNamespace>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="DALib"/>
        <PackageReference Include="SkiaSharp"/>
    </ItemGroup>
</Project>
```

- [ ] **Step 2: Write the generator**

This exact code was built and run during planning (in a scratch copy) and produced the expected files. `Tools/MirrorBackdrops/Program.cs`:

```csharp
// Cuts the Josephine's Mirror backdrops out of real maps and writes them where the client embeds them.
// Run from the Chaos.Client repo root:
//   dotnet run --project Tools/MirrorBackdrops/MirrorBackdrops.csproj -c Release -- [--sheet <contact-sheet.png>]
// Reads seo.dat / ia.dat from DA_PATH (default C:\Users\Michael\Documents\Unora\Unora Files) and the maps from
// MAPS_PATH (default the Unora repo's Data\Configuration\MapData, the copies the server sends players).
// The picture size and standing point must match MirrorBackdrops in the client (Controls/World/Popups/Beauty).
using DALib.Data;
using DALib.Definitions;
using DALib.Drawing;
using SkiaSharp;

(string Key, int MapId, int Width, int Height, int TileX, int TileY)[] places =
[
    ("mileth", 397, 60, 60, 23, 38),
    ("woodlands", 97, 50, 50, 15, 34),
    ("beach", 336, 20, 20, 4, 4),
    ("frozencave", 443, 20, 30, 5, 15),
    ("crypt", 473, 30, 30, 7, 13)
];

const int PAD = 256;
const int PICTURE_WIDTH = 260;
const int PICTURE_HEIGHT = 250;
const int ANCHOR_X = 130;
const int ANCHOR_Y = 153;
const int THUMB_CROP = 64;
const int THUMB_ANCHOR_X = 32;
const int THUMB_ANCHOR_Y = 48;
const int THUMB_SIZE = 24;
var fill = new SKColor(24, 22, 30);

var sheetPath = args is ["--sheet", var path] ? path : null;

if ((args.Length != 0) && sheetPath is null)
{
    Console.Error.WriteLine("usage: MirrorBackdrops [--sheet <contact-sheet.png>]");
    return 1;
}

var rendering = Path.Combine(Directory.GetCurrentDirectory(), "Chaos.Client.Rendering");

if (!Directory.Exists(rendering))
{
    Console.Error.WriteLine("run this from the Chaos.Client repo root");
    return 1;
}

var outDir = Path.Combine(rendering, "Assets", "MirrorBackdrops");
Directory.CreateDirectory(outDir);

var da = Environment.GetEnvironmentVariable("DA_PATH") ?? @"C:\Users\Michael\Documents\Unora\Unora Files";
var maps = Environment.GetEnvironmentVariable("MAPS_PATH") ?? @"C:\Users\Michael\Documents\GitHub\Unora\Data\Configuration\MapData";

using var seo = DataArchive.FromFile(Path.Combine(da, "seo.dat"));
using var ia = DataArchive.FromFile(Path.Combine(da, "ia.dat"));
var made = new List<(string Key, SKBitmap Picture, SKBitmap Thumb)>();

foreach (var place in places)
{
    var map = MapFile.FromFile(Path.Combine(maps, $"lod{place.MapId}.map"), place.Width, place.Height);
    using var image = Graphics.RenderMap(map, seo, ia, PAD);

    //centre of the standing tile in the rendered map -- the same tile maths Graphics.RenderMap draws with
    var centerX = ((place.Height - 1 + place.TileX - place.TileY) * CONSTANTS.HALF_TILE_WIDTH) + CONSTANTS.HALF_TILE_WIDTH;
    var centerY = PAD + ((place.TileX + place.TileY) * CONSTANTS.HALF_TILE_HEIGHT) + CONSTANTS.HALF_TILE_HEIGHT;

    var picture = Cut(image, centerX - ANCHOR_X, centerY - ANCHOR_Y, PICTURE_WIDTH, PICTURE_HEIGHT);
    using var thumbCrop = Cut(image, centerX - THUMB_ANCHOR_X, centerY - THUMB_ANCHOR_Y, THUMB_CROP, THUMB_CROP);
    var thumb = thumbCrop.Resize(new SKImageInfo(THUMB_SIZE, THUMB_SIZE), new SKSamplingOptions(SKCubicResampler.Mitchell));

    Save(picture, Path.Combine(outDir, $"{place.Key}.png"));
    Save(thumb, Path.Combine(outDir, $"{place.Key}-thumb.png"));
    made.Add((place.Key, picture, thumb));
}

if (sheetPath is not null)
    WriteSheet(sheetPath);

foreach (var (_, picture, thumb) in made)
{
    picture.Dispose();
    thumb.Dispose();
}

return 0;

//a window onto the rendered map; anything outside the map is the pedestal colour
SKBitmap Cut(SKImage image, int left, int top, int width, int height)
{
    var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
    using var canvas = new SKCanvas(bitmap);
    canvas.Clear(fill);
    canvas.DrawImage(image, -left, -top);

    return bitmap;
}

void Save(SKBitmap bitmap, string file)
{
    using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
    File.WriteAllBytes(file, data.ToArray());
    Console.WriteLine($"wrote {file} {bitmap.Width}x{bitmap.Height}");
}

//every backdrop with the part each mirror box shows outlined, a red cross on the standing point, and the
//thumbnail at 4x -- for choosing standing tiles, never shipped
void WriteSheet(string file)
{
    const int MARGIN = 10;
    const int THUMB_ZOOM = 4;
    const int CELL_WIDTH = PICTURE_WIDTH + MARGIN;
    const int THUMB_TOP = MARGIN + PICTURE_HEIGHT + MARGIN;

    //picture pixels each box shows (MirrorBackdrops.SourcePixel at the box corners), right/bottom exclusive
    (SKRect Area, SKColor Color)[] boxes =
    [
        (new SKRect(17, 2, 243, 248), SKColors.White),      //preview at 1x, 226 x 246
        (new SKRect(73, 63, 187, 187), SKColors.Gold),       //preview at 2x
        (new SKRect(2, 47, 258, 203), SKColors.DeepSkyBlue)  //Review figures, 256 x 156 at 1x
    ];

    using var sheet = new SKBitmap(MARGIN + (made.Count * CELL_WIDTH), THUMB_TOP + (THUMB_SIZE * THUMB_ZOOM) + MARGIN + 14);
    using var canvas = new SKCanvas(sheet);
    using var outline = new SKPaint { IsStroke = true, StrokeWidth = 1 };
    using var cross = new SKPaint { Color = SKColors.Red, StrokeWidth = 1 };
    using var font = new SKFont(SKTypeface.Default, 12);
    using var label = new SKPaint { Color = SKColors.White, IsAntialias = true };
    canvas.Clear(new SKColor(40, 40, 40));

    for (var i = 0; i < made.Count; i++)
    {
        var left = MARGIN + (i * CELL_WIDTH);
        canvas.DrawBitmap(made[i].Picture, left, MARGIN);

        foreach (var (area, color) in boxes)
        {
            outline.Color = color;
            canvas.DrawRect(SKRect.Create(left + area.Left, MARGIN + area.Top, area.Width, area.Height), outline);
        }

        var crossX = left + ANCHOR_X + 0.5f;
        var crossY = MARGIN + ANCHOR_Y + 0.5f;
        canvas.DrawLine(crossX - 4, crossY, crossX + 4, crossY, cross);
        canvas.DrawLine(crossX, crossY - 4, crossX, crossY + 4, cross);

        canvas.DrawBitmap(made[i].Thumb, SKRect.Create(left, THUMB_TOP, THUMB_SIZE * THUMB_ZOOM, THUMB_SIZE * THUMB_ZOOM));
        canvas.DrawText(made[i].Key, left, THUMB_TOP + (THUMB_SIZE * THUMB_ZOOM) + MARGIN + 10, font, label);
    }

    using var data = sheet.Encode(SKEncodedImageFormat.Png, 100);
    File.WriteAllBytes(file, data.ToArray());
    Console.WriteLine($"wrote {file} {sheet.Width}x{sheet.Height}");
}
```

- [ ] **Step 3: Run it from the repo root**

Run (PowerShell, repo root): `dotnet run --project Tools/MirrorBackdrops/MirrorBackdrops.csproj -c Release -- --sheet "$env:TEMP\mirror-backdrops-sheet.png"`
Expected: ten lines `wrote …\Chaos.Client.Rendering\Assets\MirrorBackdrops\<key>.png 260x250` / `<key>-thumb.png 24x24`, then `wrote …mirror-backdrops-sheet.png 1360x390`. Build warnings from the repo's code-style rules are fine; errors are not.

- [ ] **Step 4: Check the wrong-folder guard**

Run: `dotnet run --project <repo>\Tools\MirrorBackdrops\MirrorBackdrops.csproj -c Release` from `Tools/MirrorBackdrops` (not the repo root).
Expected: `run this from the Chaos.Client repo root`, exit code 1.

- [ ] **Step 5: Confirm the output folder**

Run: `Get-ChildItem Chaos.Client.Rendering/Assets/MirrorBackdrops | Select-Object Name, Length`
Expected: exactly ten `.png` files. `git status --short Tools Chaos.Client.Rendering/Assets` shows only the new tool files and PNGs (`bin/` and `obj/` are gitignored).

```json:metadata
{"files": ["Tools/MirrorBackdrops/MirrorBackdrops.csproj", "Tools/MirrorBackdrops/Program.cs", "Chaos.Client.Rendering/Assets/MirrorBackdrops/*.png"], "verifyCommand": "dotnet run --project Tools/MirrorBackdrops/MirrorBackdrops.csproj -c Release -- --sheet \"$env:TEMP\\mirror-backdrops-sheet.png\"", "acceptanceCriteria": ["Run from the repo root prints ten PNG lines (five 260x250, five 24x24) and one 1360x390 sheet line, exit 0", "Assets/MirrorBackdrops holds exactly ten PNGs", "Run from another folder prints 'run this from the Chaos.Client repo root' and exits 1", "Tool not added to Chaos.Client.slnx"], "modelTier": "mechanical"}
```

---

### Task 2: Check the standing spots with the user

**Goal:** Show the user the contact sheet, move any standing tile they want moved, and regenerate until they approve the five backdrops and thumbnails.

> **USER-ORDERED GATE — NON-SKIPPABLE.** This task was requested by the user in the current conversation. It MUST NOT be closed by walking around it, by declaring it "verified inline", or by substituting a cheaper check. Close only after every item in `acceptanceCriteria` has been re-validated independently, with output captured.

**Files:**
- Modify (only if the user asks for moves): `Tools/MirrorBackdrops/Program.cs` (the `places` table)
- Regenerate: `Chaos.Client.Rendering/Assets/MirrorBackdrops/*.png`

**Acceptance Criteria:**
- [ ] The contact sheet from the final `places` table was shown to the user (opened on screen and described).
- [ ] The user answered that all five standing spots and thumbnails are approved — their reply is quoted in the task's close note.
- [ ] The PNGs in `Assets/MirrorBackdrops` were regenerated from that final table (the last generator run's output is in the close note).

**Verify:** `dotnet run --project Tools/MirrorBackdrops/MirrorBackdrops.csproj -c Release -- --sheet "$env:TEMP\mirror-backdrops-sheet.png"` → ten PNG lines + sheet line; then the user's explicit approval.

**Steps:**

- [ ] **Step 1: Make and open the sheet**

The coordinator does this task itself, not a subagent. Run the Verify command, then `Start-Process "$env:TEMP\mirror-backdrops-sheet.png"` so it opens on the user's screen, and Read the PNG yourself so you can describe it.

How to read the sheet: each picture has a red cross on the standing point (where the feet go). The white outline is what the preview shows at 1x, the gold one what it shows at 2x (the default), the blue one what the Review figures show. The thumbnails sit underneath at 4x.

What the planning run (2026-10-04, same tiles) showed: Mileth — grass south of the fountain, fence and church in the 1x view. Woodlands — feet right under a small pine by the cabin. Beach — sand, with low plants near the feet. Frozen Cave — snow with a bare tree above the feet. Crypt — dark floor below a coffin. The figure always draws over the whole picture, so anything right at the feet can look like it sits behind the player.

- [ ] **Step 2: Ask the user**

Use AskUserQuestion: "Are the five backdrops and thumbnails right?" Options: "All good" — keep them as they are; "Move some" — they say which place and which way. The picture is fixed in the client either way; only the standing tile changes.

- [ ] **Step 3: Move tiles if asked**

Edit only the `TileX`/`TileY` numbers in the `places` table of `Tools/MirrorBackdrops/Program.cs`. On screen, x + 1 moves the spot down-right by half a tile and y + 1 moves it down-left by half a tile. So "up" is x − 1, y − 1; "right" is x + 1, y − 1. Re-run the Verify command, reopen the sheet and ask again. Repeat until the user picks "All good".

```json:metadata
{"files": ["Tools/MirrorBackdrops/Program.cs", "Chaos.Client.Rendering/Assets/MirrorBackdrops/*.png"], "verifyCommand": "dotnet run --project Tools/MirrorBackdrops/MirrorBackdrops.csproj -c Release -- --sheet \"$env:TEMP\\mirror-backdrops-sheet.png\"", "acceptanceCriteria": ["Contact sheet from the final places table shown to the user (opened on screen and described)", "User explicitly approved all five standing spots and thumbnails; reply quoted in the close note", "PNGs regenerated from that final table; last generator output in the close note"], "modelTier": "frontier", "userGate": true, "tags": ["user-gate"], "requiresUserSpecification": false}
```

---

### Task 3: Embedded backdrops and the backdrop list

**Goal:** Embed the PNGs, add `MirrorBackdropAssets` to open them, and add the `MirrorBackdrop` enum with its pure helpers (`MirrorBackdrops`), all unit-tested.

**Files:**
- Modify: `Chaos.Client.Rendering/Chaos.Client.Rendering.csproj`
- Create: `Chaos.Client.Rendering/MirrorBackdropAssets.cs`
- Create: `Chaos.Client/Controls/World/Popups/Beauty/MirrorBackdrop.cs`
- Test: `Tests/Chaos.Client.Tests/MirrorBackdropTests.cs`

**Acceptance Criteria:**
- [ ] `MirrorBackdropTests` all pass: order Plain, Mileth, Woodlands, Beach, FrozenCave, Crypt; names "Plain", "Mileth", "Woodlands", "Beach", "Frozen Cave", "Crypt" (ASCII); keys null for Plain and `mileth`/`woodlands`/`beach`/`frozencave`/`crypt` otherwise.
- [ ] Each key's `mirrorbackdrop.<key>.png` opens from the Rendering assembly and decodes to 260 × 250; each `-thumb` decodes to 24 × 24. A missing name throws `InvalidOperationException` naming the resource.
- [ ] `SourcePixel` returns (130, 153) at the feet for 226 × 246 at 1x and 2x and 256 × 156 at 1x, and the corner values (17, 2)–(242, 247), (73, 63)–(186, 186) and (2, 47)–(257, 202).
- [ ] The full client test suite has no new failures compared with the baseline taken in Step 1.

**Verify:** `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/MirrorBackdropTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Record the test baseline**

Run: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi`
Write down the pass/fail counts and the names of any failing tests. Later tasks compare against this.

- [ ] **Step 2: Write the failing tests**

`Tests/Chaos.Client.Tests/MirrorBackdropTests.cs`:

```csharp
using Chaos.Client.Controls.World.Popups.Beauty;
using Chaos.Client.Rendering;
using FluentAssertions;
using Microsoft.Xna.Framework;
using SkiaSharp;

namespace Chaos.Client.Tests;

public class MirrorBackdropTests
{
    [Test]
    public async Task Backdrops_run_plain_then_the_five_places()
    {
        MirrorBackdrops.All.Should()
                       .Equal(
                           MirrorBackdrop.Plain,
                           MirrorBackdrop.Mileth,
                           MirrorBackdrop.Woodlands,
                           MirrorBackdrop.Beach,
                           MirrorBackdrop.FrozenCave,
                           MirrorBackdrop.Crypt);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Names_are_short_ascii_words()
    {
        MirrorBackdrops.All.Select(MirrorBackdrops.Name).Should().Equal("Plain", "Mileth", "Woodlands", "Beach", "Frozen Cave", "Crypt");
        MirrorBackdrops.All.Select(MirrorBackdrops.Name).Should().OnlyContain(name => name.All(c => c < 128));

        await Task.CompletedTask;
    }

    [Test]
    public async Task Only_plain_has_no_picture()
    {
        MirrorBackdrops.Key(MirrorBackdrop.Plain).Should().BeNull();
        MirrorBackdrops.All.Skip(1).Select(MirrorBackdrops.Key).Should().Equal("mileth", "woodlands", "beach", "frozencave", "crypt");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Every_place_has_its_embedded_pictures()
    {
        foreach (var key in MirrorBackdrops.All.Select(MirrorBackdrops.Key).OfType<string>())
        {
            using (var stream = MirrorBackdropAssets.Open(MirrorBackdropAssets.ResourceName(key)))
            using (var picture = SKBitmap.Decode(stream))
            {
                picture.Width.Should().Be(MirrorBackdrops.SOURCE_WIDTH);
                picture.Height.Should().Be(MirrorBackdrops.SOURCE_HEIGHT);
            }

            using (var stream = MirrorBackdropAssets.Open(MirrorBackdropAssets.ThumbnailResourceName(key)))
            using (var thumbnail = SKBitmap.Decode(stream))
            {
                thumbnail.Width.Should().Be(MirrorBackdrops.THUMBNAIL_SIZE);
                thumbnail.Height.Should().Be(MirrorBackdrops.THUMBNAIL_SIZE);
            }
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task A_missing_picture_names_itself()
    {
        var open = () => MirrorBackdropAssets.Open("mirrorbackdrop.nowhere.png");

        open.Should().Throw<InvalidOperationException>().WithMessage("*mirrorbackdrop.nowhere.png*");

        await Task.CompletedTask;
    }

    [Test]
    public async Task The_feet_sit_28_pixels_below_the_centre()
    {
        MirrorBackdrops.FEET_BELOW_CENTER.Should().Be(28);

        await Task.CompletedTask;
    }

    [Test]
    [Arguments(226, 246, 1, 113, 151)]
    [Arguments(226, 246, 2, 113, 179)]
    [Arguments(256, 156, 1, 128, 106)]
    public async Task The_standing_tile_shows_under_the_feet(int width, int height, int scale, int feetX, int feetY)
    {
        MirrorBackdrops.SourcePixel(feetX, feetY, width, height, scale)
                       .Should()
                       .Be(new Point(MirrorBackdrops.SOURCE_ANCHOR_X, MirrorBackdrops.SOURCE_ANCHOR_Y));

        await Task.CompletedTask;
    }

    [Test]
    public async Task At_2x_each_picture_pixel_covers_two_box_pixels()
    {
        MirrorBackdrops.SourcePixel(112, 178, 226, 246, 2).Should().Be(new Point(129, 152));
        MirrorBackdrops.SourcePixel(114, 180, 226, 246, 2).Should().Be(new Point(130, 153));
        MirrorBackdrops.SourcePixel(115, 181, 226, 246, 2).Should().Be(new Point(131, 154));

        await Task.CompletedTask;
    }

    [Test]
    [Arguments(226, 246, 1, 17, 2, 242, 247)]
    [Arguments(226, 246, 2, 73, 63, 186, 186)]
    [Arguments(256, 156, 1, 2, 47, 257, 202)]
    public async Task Box_corners_stay_inside_the_picture(int width, int height, int scale, int left, int top, int right, int bottom)
    {
        MirrorBackdrops.SourcePixel(0, 0, width, height, scale).Should().Be(new Point(left, top));
        MirrorBackdrops.SourcePixel(width - 1, height - 1, width, height, scale).Should().Be(new Point(right, bottom));
        right.Should().BeLessThan(MirrorBackdrops.SOURCE_WIDTH);
        bottom.Should().BeLessThan(MirrorBackdrops.SOURCE_HEIGHT);

        await Task.CompletedTask;
    }
}
```

- [ ] **Step 3: Run the tests to see them fail**

Run: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/MirrorBackdropTests/*"`
Expected: build fails — `MirrorBackdrops`, `MirrorBackdrop` and `MirrorBackdropAssets` do not exist.

- [ ] **Step 4: Embed the PNGs**

In `Chaos.Client.Rendering/Chaos.Client.Rendering.csproj`, add this inside the existing `<ItemGroup>` that holds the `MirrorScare` entry, right after the `MirrorScare` `</EmbeddedResource>`:

```xml

        <!-- Josephine's Mirror backdrops, cut from real maps by Tools/MirrorBackdrops. Embedded so every player has
             them, whichever maps they have visited; read back by MirrorBackdropAssets as "mirrorbackdrop.<file>". -->
        <EmbeddedResource Include="Assets\MirrorBackdrops\*.png">
            <LogicalName>mirrorbackdrop.%(Filename)%(Extension)</LogicalName>
        </EmbeddedResource>
```

- [ ] **Step 5: Add `MirrorBackdropAssets`**

`Chaos.Client.Rendering/MirrorBackdropAssets.cs`:

```csharp
namespace Chaos.Client.Rendering;

/// <summary>
///     The Josephine's Mirror backdrop pictures embedded in this assembly: Assets/MirrorBackdrops, cut from real maps by
///     Tools/MirrorBackdrops. Each place has "mirrorbackdrop.&lt;key&gt;.png" and "mirrorbackdrop.&lt;key&gt;-thumb.png".
/// </summary>
public static class MirrorBackdropAssets
{
    public static string ResourceName(string key) => $"mirrorbackdrop.{key}.png";

    public static string ThumbnailResourceName(string key) => $"mirrorbackdrop.{key}-thumb.png";

    public static Stream Open(string resourceName)
        => typeof(MirrorBackdropAssets).Assembly.GetManifestResourceStream(resourceName)
           ?? throw new InvalidOperationException($"missing embedded mirror backdrop {resourceName}");
}
```

- [ ] **Step 6: Add the enum and helpers**

`Chaos.Client/Controls/World/Popups/Beauty/MirrorBackdrop.cs` (same enum-plus-static-class pairing as `MirrorPage`/`MirrorPages`):

```csharp
#region
using Chaos.Client.Rendering;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Beauty;

/// <summary>The places the mirror can stand the player in. Plain is the bare pedestal.</summary>
public enum MirrorBackdrop
{
    Plain,
    Mileth,
    Woodlands,
    Beach,
    FrozenCave,
    Crypt
}

/// <summary>
///     The backdrop list, each place's name and picture, and how a picture lines up with the figure. The pictures are
///     <see cref="SOURCE_WIDTH" /> x <see cref="SOURCE_HEIGHT" /> with the standing tile's centre at
///     (<see cref="SOURCE_ANCHOR_X" />, <see cref="SOURCE_ANCHOR_Y" />); Tools/MirrorBackdrops cuts them to match.
/// </summary>
public static class MirrorBackdrops
{
    public const int SOURCE_WIDTH = 260;
    public const int SOURCE_HEIGHT = 250;
    public const int SOURCE_ANCHOR_X = 130;
    public const int SOURCE_ANCHOR_Y = 153;
    public const int THUMBNAIL_SIZE = 24;

    /// <summary>How far below a box's centre <see cref="MirrorPreview" /> puts the figure's feet, in figure pixels.</summary>
    public const int FEET_BELOW_CENTER = AislingRenderer.CANVAS_CENTER_Y - AislingRenderer.BODY_CENTER_Y;

    public static IReadOnlyList<MirrorBackdrop> All { get; } = Enum.GetValues<MirrorBackdrop>();

    public static string Name(MirrorBackdrop place)
        => place switch
        {
            MirrorBackdrop.Plain      => "Plain",
            MirrorBackdrop.Mileth     => "Mileth",
            MirrorBackdrop.Woodlands  => "Woodlands",
            MirrorBackdrop.Beach      => "Beach",
            MirrorBackdrop.FrozenCave => "Frozen Cave",
            MirrorBackdrop.Crypt      => "Crypt",
            _                         => throw new ArgumentOutOfRangeException(nameof(place), place, null)
        };

    /// <summary>The embedded picture's key (see <see cref="MirrorBackdropAssets" />), or null for Plain, which has none.</summary>
    public static string? Key(MirrorBackdrop place)
        => place switch
        {
            MirrorBackdrop.Plain      => null,
            MirrorBackdrop.Mileth     => "mileth",
            MirrorBackdrop.Woodlands  => "woodlands",
            MirrorBackdrop.Beach      => "beach",
            MirrorBackdrop.FrozenCave => "frozencave",
            MirrorBackdrop.Crypt      => "crypt",
            _                         => throw new ArgumentOutOfRangeException(nameof(place), place, null)
        };

    /// <summary>
    ///     The picture pixel shown at (<paramref name="x" />, <paramref name="y" />) of a box drawn at
    ///     <paramref name="scale" />. The box pixel under the figure's feet shows the standing tile's centre and the rest
    ///     step away from it, so the tile stays under the feet at 2x too, where a source rectangle would land half a
    ///     picture pixel off.
    /// </summary>
    public static Point SourcePixel(int x, int y, int boxWidth, int boxHeight, int scale)
        => new(
            SOURCE_ANCHOR_X + FloorDiv(x - (boxWidth / 2), scale),
            SOURCE_ANCHOR_Y + FloorDiv(y - (boxHeight / 2) - (FEET_BELOW_CENTER * scale), scale));

    private static int FloorDiv(int value, int divisor) => (int)Math.Floor((double)value / divisor);
}
```

- [ ] **Step 7: Run the tests to see them pass**

Run: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/MirrorBackdropTests/*"`
Expected: all MirrorBackdropTests pass (13 test cases counting each `[Arguments]` row).

- [ ] **Step 8: Run the full suite**

Run: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi`
Expected: the Step 1 baseline plus the new tests passing; no new failures.

```json:metadata
{"files": ["Chaos.Client.Rendering/Chaos.Client.Rendering.csproj", "Chaos.Client.Rendering/MirrorBackdropAssets.cs", "Chaos.Client/Controls/World/Popups/Beauty/MirrorBackdrop.cs", "Tests/Chaos.Client.Tests/MirrorBackdropTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/MirrorBackdropTests/*\"", "acceptanceCriteria": ["MirrorBackdropTests pass: order, ASCII names, keys (null for Plain)", "Each mirrorbackdrop.<key>.png decodes to 260x250 and each -thumb to 24x24; a missing name throws InvalidOperationException naming it", "SourcePixel gives (130,153) at the feet and the listed corner values for 226x246 @1x/2x and 256x156 @1x", "Full client suite has no new failures vs the Step 1 baseline"], "modelTier": "mechanical"}
```

---

### Task 4: ThumbnailGrid cell size and no-arrows option

**Goal:** Let `ThumbnailGrid<T>` take a cell size and leave out its page arrows, without changing any existing grid.

**Files:**
- Modify: `Chaos.Client/Controls/World/Popups/Beauty/ThumbnailGrid.cs`
- Test: `Tests/Chaos.Client.Tests/ThumbnailGridTests.cs`

**Acceptance Criteria:**
- [ ] `ThumbnailGrid<int>.WidthFor(8)` is 315 and `HeightFor(2)` is 67 (unchanged defaults).
- [ ] `ThumbnailGrid<int>.WidthFor(6, 4, 28, arrows: false)` is 188 and `HeightFor(1, 4, 28)` is 28.
- [ ] With `arrows: false` the grid creates no arrow buttons and `CellLeft` is 0; with the default it is 19 as before.
- [ ] The Hair, Skin and Face pages compile unchanged; the full client suite has no new failures.

**Verify:** `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/ThumbnailGridTests/*"` → all pass

**Steps:**

- [ ] **Step 1: Write the failing tests**

`Tests/Chaos.Client.Tests/ThumbnailGridTests.cs`:

```csharp
using Chaos.Client.Controls.World.Popups.Beauty;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class ThumbnailGridTests
{
    [Test]
    public async Task Default_grid_keeps_its_arrows_and_32_px_cells()
    {
        ThumbnailGrid<int>.WidthFor(8).Should().Be(315);
        ThumbnailGrid<int>.HeightFor(2).Should().Be(67);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Backdrop_row_is_188_by_28_without_arrows()
    {
        ThumbnailGrid<int>.WidthFor(6, 4, 28, arrows: false).Should().Be(188);
        ThumbnailGrid<int>.HeightFor(1, 4, 28).Should().Be(28);

        await Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/ThumbnailGridTests/*"`
Expected: build fails — `WidthFor` has no 4-argument overload with `arrows`.

- [ ] **Step 3: Generalise the grid**

Edit with Serena (`replace_content`) in `ThumbnailGrid.cs`. Name the new parameter `cellSize`, not `cell` — the constructor's loop already has a local called `cell`.

Class summary — replace:

```csharp
/// <summary>
///     One page of picker cells laid out row by row, with page arrows on both sides that hide when everything fits on
///     one page. Holds no state of its own beyond the items it was last given: the page calls <see cref="SetItems" />
///     on every refresh and reacts to the events.
/// </summary>
```

with:

```csharp
/// <summary>
///     One page of picker cells laid out row by row, with page arrows on both sides that hide when everything fits on
///     one page, or no arrows at all for a short list that always fits. Holds no state of its own beyond the items it
///     was last given: the page calls <see cref="SetItems" /> on every refresh and reacts to the events.
/// </summary>
```

Fields and sizing — replace:

```csharp
    private readonly Cell[] Cells;
    private readonly CustomButton Left;
    private readonly CustomButton Right;
    private readonly int Columns;
    private readonly int ColumnPitch;
    private readonly int RowPitch;
```

with:

```csharp
    private readonly Cell[] Cells;
    private readonly CustomButton? Left;
    private readonly CustomButton? Right;
    private readonly int Columns;
    private readonly int ColumnPitch;
    private readonly int RowPitch;
```

Replace the `CellLeft` property, `WidthFor`, `HeightFor` and the whole constructor (from `/// <summary>X of the first cell column` through the constructor's closing brace) with:

```csharp
    /// <summary>X of the first cell column inside the grid, after the left arrow when there is one.</summary>
    public int CellLeft { get; }

    public static int WidthFor(int columns, int columnGap = GAP, int cellSize = CELL, bool arrows = true)
        => (arrows ? 2 * (ARROW_WIDTH + GAP) : 0) + ((columns * (cellSize + columnGap)) - columnGap);

    public static int HeightFor(int rows, int rowGap = GAP, int cellSize = CELL) => (rows * (cellSize + rowGap)) - rowGap;

    /// <param name="arrows">False leaves out the page arrows and their space, for a list that always fits on one page.</param>
    public ThumbnailGrid(int columns, int rows, int columnGap = GAP, int rowGap = GAP, int cellSize = CELL, bool arrows = true)
    {
        Background = null;
        Columns = columns;
        ColumnPitch = cellSize + columnGap;
        RowPitch = cellSize + rowGap;
        CellLeft = arrows ? ARROW_WIDTH + GAP : 0;
        Width = WidthFor(columns, columnGap, cellSize, arrows);
        Height = HeightFor(rows, rowGap, cellSize);

        var arrowY = (Height - CustomButton.HEIGHT) / 2;

        if (arrows)
        {
            Left = new CustomButton("<", ARROW_WIDTH) { X = 0, Y = arrowY };
            Left.Clicked += () => PageStepped?.Invoke(-1);
            AddChild(Left);
        }

        Cells = new Cell[columns * rows];

        for (var i = 0; i < Cells.Length; i++)
        {
            var origin = CellOrigin(i);
            var cell = new Cell { X = origin.X, Y = origin.Y, Width = cellSize, Height = cellSize, Visible = false };
            cell.Hovered += item => Hovered?.Invoke((T)item);
            cell.HoverCleared += () => HoverCleared?.Invoke();
            cell.Clicked += item => Selected?.Invoke((T)item);
            Cells[i] = cell;
            AddChild(cell);
        }

        if (arrows)
        {
            Right = new CustomButton(">", ARROW_WIDTH) { X = Width - ARROW_WIDTH, Y = arrowY };
            Right.Clicked += () => PageStepped?.Invoke(+1);
            AddChild(Right);
        }
    }
```

`CellOrigin` reads `CellLeft`, which is now set before the cell loop runs. In `SetItems`, replace:

```csharp
        Left.Visible = pageCount > 1;
        Right.Visible = pageCount > 1;
```

with:

```csharp
        if (Left is not null)
            Left.Visible = pageCount > 1;

        if (Right is not null)
            Right.Visible = pageCount > 1;
```

- [ ] **Step 4: Run the tests to see them pass**

Run: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter "/*/*/ThumbnailGridTests/*"`
Expected: 2 tests pass.

- [ ] **Step 5: Run the full suite**

Run: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi`
Expected: no new failures against the Task 3 baseline. `HairPage` (`Styles.CellLeft`) and `SkinPage` (`WidthFor(COLUMNS, COLUMN_GAP)`, `ThumbnailGrid<BodyColor>.CELL`) compile unchanged.

```json:metadata
{"files": ["Chaos.Client/Controls/World/Popups/Beauty/ThumbnailGrid.cs", "Tests/Chaos.Client.Tests/ThumbnailGridTests.cs"], "verifyCommand": "dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi --treenode-filter \"/*/*/ThumbnailGridTests/*\"", "acceptanceCriteria": ["WidthFor(8)==315 and HeightFor(2)==67", "WidthFor(6,4,28,arrows:false)==188 and HeightFor(1,4,28)==28", "arrows:false creates no arrow buttons and CellLeft==0; default CellLeft==19", "Hair/Skin/Face pages compile unchanged; full suite has no new failures"], "modelTier": "mechanical"}
```

---

### Task 5: Draw the backdrop behind the mirror figures

**Goal:** Add `MirrorBackdropCache`, make `MirrorPreview` draw a chosen backdrop between the pedestal and the figure, and pass the window's cache and chosen backdrop to the preview and both Review figures.

**Files:**
- Create: `Chaos.Client/Controls/World/Popups/Beauty/MirrorBackdropCache.cs`
- Modify: `Chaos.Client/Controls/World/Popups/Beauty/MirrorPreview.cs`
- Modify: `Chaos.Client/Controls/World/Popups/Beauty/Pages/ReviewPage.cs`
- Modify: `Chaos.Client/Controls/World/Popups/Beauty/BeautyShopControl.cs`

**Acceptance Criteria:**
- [ ] `MirrorBackdropCache.Get` returns null for Plain, otherwise a texture exactly `width` × `height`, the same instance on a repeat call, filled through `MirrorBackdrops.SourcePixel`.
- [ ] `MirrorPreview` draws `Get(Backdrop, Width − 4, Height − 4, scale)` at (+2, +2) after the pedestal and before the figure, with `scale` the figure's own draw scale (1 when a 2x figure does not fit), and still draws it when the figure is null.
- [ ] `ReviewPage` takes the cache and `SetView(facing, showGear, backdrop)` sets both figures' `Backdrop`.
- [ ] `BeautyShopControl` creates one cache, passes it to the preview and Review, sets `Preview.Backdrop` and Review's backdrop from a static `ChosenBackdrop` (Plain), calls `Clear()` in `Hide()` and `Dispose()` in `Dispose()`.
- [ ] `dotnet build Chaos.Client/Chaos.Client.csproj` succeeds with 0 errors; the full suite has no new failures.

**Verify:** `dotnet build Chaos.Client/Chaos.Client.csproj` → `Build succeeded`, 0 errors; then `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi` → no new failures

**Steps:**

- [ ] **Step 1: Add the cache**

`Chaos.Client/Controls/World/Popups/Beauty/MirrorBackdropCache.cs`:

```csharp
#region
using Chaos.Client.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Beauty;

/// <summary>
///     The mirror's backdrop pictures, read from the client's embedded PNGs on first use. <see cref="Get" /> hands out
///     one texture per (place, box size, zoom), cut on the CPU through <see cref="MirrorBackdrops.SourcePixel" /> so it
///     draws at native size with the standing tile under the figure's feet. Owns every texture it hands out;
///     <see cref="Clear" /> is called when the mirror hides.
/// </summary>
public sealed class MirrorBackdropCache : IDisposable
{
    private readonly Dictionary<(MirrorBackdrop Place, int Width, int Height, int Scale), Texture2D> Cuts = [];
    private readonly Dictionary<MirrorBackdrop, Color[]> Sources = [];
    private readonly Dictionary<MirrorBackdrop, Texture2D> Thumbnails = [];

    /// <summary>The picture for a <paramref name="width" /> x <paramref name="height" /> box at <paramref name="scale" />, or null for Plain.</summary>
    public Texture2D? Get(MirrorBackdrop place, int width, int height, int scale)
    {
        if ((place == MirrorBackdrop.Plain) || (width <= 0) || (height <= 0))
            return null;

        if (Cuts.TryGetValue((place, width, height, scale), out var cut))
            return cut;

        var source = Source(place);
        var pixels = new Color[width * height];

        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var from = MirrorBackdrops.SourcePixel(x, y, width, height, scale);

                //a box bigger than the picture leaves the pedestal showing at its edges
                if ((from.X >= 0) && (from.X < MirrorBackdrops.SOURCE_WIDTH) && (from.Y >= 0) && (from.Y < MirrorBackdrops.SOURCE_HEIGHT))
                    pixels[(y * width) + x] = source[(from.Y * MirrorBackdrops.SOURCE_WIDTH) + from.X];
            }

        cut = new Texture2D(ChaosGame.Device, width, height);
        cut.SetData(pixels);
        Cuts[(place, width, height, scale)] = cut;

        return cut;
    }

    /// <summary>The picker's 24 x 24 picture of a place, or null for Plain.</summary>
    public Texture2D? Thumbnail(MirrorBackdrop place)
    {
        if (MirrorBackdrops.Key(place) is not { } key)
            return null;

        if (Thumbnails.TryGetValue(place, out var thumbnail))
            return thumbnail;

        using var stream = MirrorBackdropAssets.Open(MirrorBackdropAssets.ThumbnailResourceName(key));
        thumbnail = Texture2D.FromStream(ChaosGame.Device, stream);
        Thumbnails[place] = thumbnail;

        return thumbnail;
    }

    private Color[] Source(MirrorBackdrop place)
    {
        if (Sources.TryGetValue(place, out var pixels))
            return pixels;

        var key = MirrorBackdrops.Key(place)!;
        using var stream = MirrorBackdropAssets.Open(MirrorBackdropAssets.ResourceName(key));
        using var texture = Texture2D.FromStream(ChaosGame.Device, stream);

        if ((texture.Width != MirrorBackdrops.SOURCE_WIDTH) || (texture.Height != MirrorBackdrops.SOURCE_HEIGHT))
            throw new InvalidOperationException(
                $"mirror backdrop {key} is {texture.Width}x{texture.Height}, expected {MirrorBackdrops.SOURCE_WIDTH}x{MirrorBackdrops.SOURCE_HEIGHT}");

        pixels = new Color[texture.Width * texture.Height];
        texture.GetData(pixels);
        Sources[place] = pixels;

        return pixels;
    }

    public void Clear()
    {
        foreach (var texture in Cuts.Values)
            texture.Dispose();

        foreach (var texture in Thumbnails.Values)
            texture.Dispose();

        Cuts.Clear();
        Sources.Clear();
        Thumbnails.Clear();
    }

    public void Dispose() => Clear();
}
```

`ChaosGame` lives in the `Chaos.Client` namespace, which encloses this one, so it needs no `using`.

- [ ] **Step 2: Give `MirrorPreview` a backdrop**

In `MirrorPreview.cs` (edit with Serena):

Class summary — append one sentence before `</summary>`, so the last lines read:

```csharp
///     PokerTableControl's PortraitView for the same reasoning). Zoom is a pure draw-time scale -- it never invalidates
///     the cache. The backdrop behind the figure comes from the window's <see cref="MirrorBackdropCache" />, which owns it.
/// </summary>
```

Add the inset constant after `public const int FACING_COUNT = 4;`:

```csharp

    /// <summary>Gap between the pedestal's edge and the backdrop, so the recessed rim still shows.</summary>
    public const int BACKDROP_INSET = 2;
```

Replace the two fields `private readonly AislingRenderer Renderer;` / `private readonly Texture2D Pedestal;` with:

```csharp
    private readonly AislingRenderer Renderer;
    private readonly MirrorBackdropCache Backdrops;
    private readonly Texture2D Pedestal;
```

After `public bool Zoomed { get; set; } = true;` add:

```csharp

    /// <summary>The place drawn behind the figure. Plain draws none.</summary>
    public MirrorBackdrop Backdrop { get; set; } = MirrorBackdrop.Plain;
```

Replace the constructor with:

```csharp
    public MirrorPreview(AislingRenderer renderer, MirrorBackdropCache backdrops, int width, int height)
    {
        Renderer = renderer;
        Backdrops = backdrops;
        Width = width;
        Height = height;
        Pedestal = DialogFrame.BuildRecessedTexture(new SKColor(24, 22, 30), width, height);
    }
```

Replace `Draw` with:

```csharp
    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);

        if (!Visible)
            return;

        DrawTexture(spriteBatch, Pedestal, new Vector2(ScreenX, ScreenY), Color.White);

        var scale = Zoomed ? 2 : 1;

        //DrawTextureFitted culls when the destination rect doesn't intersect ClipRect at all -- it does not
        //clip to it -- so an oversized composite (e.g. Show gear on with a tall equip layer) can paint outside
        //the pedestal. Fall back to 1x for this draw alone (the toggle state itself is untouched) when 2x
        //wouldn't fit.
        if ((Figure is not null) && (((Figure.Height * scale) > (Height - 12)) || ((Figure.Width * scale) > Width)))
            scale = 1;

        //at the figure's own scale, so a 2x figure that falls back to 1x takes its backdrop with it
        DrawTexture(
            spriteBatch,
            Backdrops.Get(Backdrop, Width - (2 * BACKDROP_INSET), Height - (2 * BACKDROP_INSET), scale),
            new Vector2(ScreenX + BACKDROP_INSET, ScreenY + BACKDROP_INSET),
            Color.White);

        if (Figure is null)
            return;

        var w = Figure.Width * scale;
        var h = Figure.Height * scale;

        //anchor on the body centre so the figure stays centred in the pedestal at 1x and 2x and doesn't drift
        //sideways between poses whose padded canvases differ in width
        var x = ScreenX + (Width / 2) - (AislingRenderer.CANVAS_CENTER_X * scale);
        var y = ScreenY + (Height / 2) - (AislingRenderer.BODY_CENTER_Y * scale);

        DrawTextureFitted(spriteBatch, Figure, new Rectangle(x, y, w, h), Color.White);
    }
```

`Dispose` stays as it is: the cache belongs to the window, not the preview.

- [ ] **Step 3: Pass the cache and backdrop through `ReviewPage`**

In `Pages/ReviewPage.cs`, change the constructor signature to:

```csharp
    public ReviewPage(
        MirrorActions actions,
        AislingRenderer renderer,
        MirrorBackdropCache backdrops,
        Action<int> rotate,
        Action toggleGear,
        int width,
        int height)
        : base(actions, width, height)
```

Change the two figure constructions to:

```csharp
        NowFigure = new MirrorPreview(renderer, backdrops, FIGURE_WIDTH, FIGURE_HEIGHT) { X = 0, Y = FIGURE_TOP, Zoomed = false };
        AddChild(NowFigure);
        NewFigure = new MirrorPreview(renderer, backdrops, FIGURE_WIDTH, FIGURE_HEIGHT) { X = newLeft, Y = FIGURE_TOP, Zoomed = false };
        AddChild(NewFigure);
```

Replace `SetView` with:

```csharp
    /// <summary>The shell's facing, Show gear and backdrop, which both figures follow. Call before <see cref="Refresh" />.</summary>
    public void SetView(int facingIndex, bool showGear, MirrorBackdrop backdrop)
    {
        FacingIndex = facingIndex;
        ShowGear = showGear;
        GearToggle.Checked = showGear;
        NowFigure.Backdrop = backdrop;
        NewFigure.Backdrop = backdrop;
    }
```

`MirrorBackdrop` and `MirrorBackdropCache` are in the enclosing `Beauty` namespace, like `MirrorPreview`, so no `using` is needed.

- [ ] **Step 4: Wire the window**

In `BeautyShopControl.cs`:

After `private readonly HeadThumbnailRenderer Thumbnails;` add:

```csharp
    private readonly MirrorBackdropCache Backdrops;
```

After `private long ReviewShownAt;` add:

```csharp

    //static so the choice outlives the window -- closing and reopening it, even logging out; it is not saved to settings
    private static MirrorBackdrop ChosenBackdrop = MirrorBackdrop.Plain;
```

In the constructor, after `Thumbnails = new HeadThumbnailRenderer(renderer);` add:

```csharp
        Backdrops = new MirrorBackdropCache();
```

Change the preview construction to:

```csharp
        Preview = new MirrorPreview(renderer, Backdrops, PREVIEW_WIDTH, PEDESTAL_HEIGHT) { X = PREVIEW_LEFT, Y = PEDESTAL_TOP };
```

Change the Review construction to:

```csharp
        Review = new ReviewPage(actions, renderer, Backdrops, Rotate, ToggleGear, REVIEW_WIDTH, REVIEW_HEIGHT) { X = REVIEW_LEFT, Y = REVIEW_TOP };
```

Replace `Refresh` with:

```csharp
    /// <summary>Repaints the preview, the heading, the visible page and the footer from the view model.</summary>
    public void Refresh()
    {
        RefreshPreview();
        Preview.Backdrop = ChosenBackdrop;
        RefreshHeading();
        Review.SetView(FacingIndex, ShowGear, ChosenBackdrop);
        Pages[(int)CurrentPage].Refresh();
        RefreshFooter();
    }
```

In `Hide()`, after `Thumbnails.Clear();` add `Backdrops.Clear();`. In `Dispose()`, after `Thumbnails.Dispose();` add `Backdrops.Dispose();`.

- [ ] **Step 5: Build**

Run: `dotnet build Chaos.Client/Chaos.Client.csproj`
Expected: `Build succeeded`, 0 errors. If the build says files are locked, a client from this tree is running — ask the user to close it.

- [ ] **Step 6: Run the full suite**

Run: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi`
Expected: no new failures against the Task 3 baseline.

```json:metadata
{"files": ["Chaos.Client/Controls/World/Popups/Beauty/MirrorBackdropCache.cs", "Chaos.Client/Controls/World/Popups/Beauty/MirrorPreview.cs", "Chaos.Client/Controls/World/Popups/Beauty/Pages/ReviewPage.cs", "Chaos.Client/Controls/World/Popups/Beauty/BeautyShopControl.cs"], "verifyCommand": "dotnet build Chaos.Client/Chaos.Client.csproj && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi", "acceptanceCriteria": ["MirrorBackdropCache.Get: null for Plain, else a width x height texture filled via SourcePixel, same instance on repeat", "MirrorPreview draws Get(Backdrop, Width-4, Height-4, scale) at +2,+2 between pedestal and figure, at the figure's draw scale, even with no figure", "ReviewPage takes the cache; SetView(facing, showGear, backdrop) sets both figures", "BeautyShopControl owns one cache, passes it on, uses static ChosenBackdrop (Plain), Clear on Hide, Dispose on Dispose", "Client builds with 0 errors; full suite has no new failures"], "modelTier": "standard"}
```

---

### Task 6: Backdrop picker row in the mirror window

**Goal:** Add the `Backdrop` caption and the six-cell picker under the preview, with click to choose and hover to try on, all UI-only.

**Files:**
- Modify: `Chaos.Client/Controls/World/Popups/Beauty/BeautyShopControl.cs`

**Acceptance Criteria:**
- [ ] The preview column has a gray `Backdrop` label at (20, 338), a gold place-name label at (80, 338), and a `ThumbnailGrid<MirrorBackdrop>` at (20, 350): 6 × 1, 28 px cells, 4 px gaps, no arrows, so 188 × 28.
- [ ] Clicking a cell sets the static `ChosenBackdrop`; hovering sets `HoveredBackdrop`; leaving clears it. The preview and the name show `HoveredBackdrop ?? ChosenBackdrop`; the picker marks `ChosenBackdrop` as selected.
- [ ] Backdrop handlers ignore calls while the window is hidden and never touch `WorldState.BeautyShop`, `Select`, `Hover`, the confirm dialog, `Review.Status` or the head thumbnails.
- [ ] `GoTo` clears `HoveredBackdrop`; `Show()` does not reset `ChosenBackdrop`; Review figures use `ChosenBackdrop`.
- [ ] `dotnet build Chaos.Client/Chaos.Client.csproj` succeeds with 0 errors; the full suite has no new failures.

**Verify:** `dotnet build Chaos.Client/Chaos.Client.csproj` → `Build succeeded`, 0 errors; then `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi` → no new failures

**Steps:**

- [ ] **Step 1: Layout constants**

After `private const int RANDOMIZE_BUTTON_WIDTH = 100;` add:

```csharp

    //backdrop picker: a caption and one row of pictures between the Show gear row and the footer total
    private const int BACKDROP_CAPTION_TOP = PREVIEW_CONTROLS_TOP + (2 * (CustomButton.HEIGHT + 6));
    private const int BACKDROP_CAPTION_WIDTH = 10 * TextRenderer.CHAR_WIDTH;
    private const int BACKDROP_PICKER_TOP = BACKDROP_CAPTION_TOP + TextRenderer.CHAR_HEIGHT;
    private const int BACKDROP_CELL = 28;
    private const int BACKDROP_GAP = 4;
```

These give caption y 338 (282 + 2 × 28), caption width 60 (name at x 80), picker y 350, picker bottom 378.

- [ ] **Step 2: Fields and state**

After `private readonly CustomButton ZoomButton;` add:

```csharp
    private readonly UILabel BackdropName;
    private readonly ThumbnailGrid<MirrorBackdrop> BackdropPicker;
```

After the `ChosenBackdrop` line from Task 5 add:

```csharp
    private MirrorBackdrop? HoveredBackdrop;
```

- [ ] **Step 3: Build the caption and picker**

In the constructor, after `PreviewColumn.AddChild(randomizeButton);` add:

```csharp

        var backdropCaption = NewLabel(PREVIEW_LEFT, BACKDROP_CAPTION_TOP, BACKDROP_CAPTION_WIDTH, LegendColors.Gray);
        backdropCaption.Text = "Backdrop";
        PreviewColumn.AddChild(backdropCaption);

        BackdropName = NewLabel(
            PREVIEW_LEFT + BACKDROP_CAPTION_WIDTH,
            BACKDROP_CAPTION_TOP,
            PREVIEW_WIDTH - BACKDROP_CAPTION_WIDTH,
            LegendColors.Gold);
        PreviewColumn.AddChild(BackdropName);

        BackdropPicker = new ThumbnailGrid<MirrorBackdrop>(MirrorBackdrops.All.Count, 1, BACKDROP_GAP, BACKDROP_GAP, BACKDROP_CELL, false)
        {
            X = PREVIEW_LEFT,
            Y = BACKDROP_PICKER_TOP
        };
        BackdropPicker.Hovered += place => HoverBackdrop(place);
        BackdropPicker.HoverCleared += () => HoverBackdrop(null);
        BackdropPicker.Selected += ChooseBackdrop;
        PreviewColumn.AddChild(BackdropPicker);
```

- [ ] **Step 4: Refresh, page turns and the handlers**

Replace `Refresh` (Task 5 version) with:

```csharp
    /// <summary>Repaints the preview, the heading, the visible page and the footer from the view model.</summary>
    public void Refresh()
    {
        RefreshPreview();
        RefreshBackdrop();
        RefreshHeading();
        Review.SetView(FacingIndex, ShowGear, ChosenBackdrop);
        Pages[(int)CurrentPage].Refresh();
        RefreshFooter();
    }
```

In `GoTo`, after `WorldState.BeautyShop.ClearHover();` add:

```csharp
        HoveredBackdrop = null;
```

After the `ToggleGear` method add:

```csharp

    /// <summary>
    ///     Pointing at a backdrop tries it on. Backdrops are UI-only: they skip the view model, the confirm dialog, the
    ///     money and the review status.
    /// </summary>
    private void HoverBackdrop(MirrorBackdrop? place)
    {
        //InputDispatcher doesn't forget a hovered element across Hide(); ignore any late callback from it
        if (!Visible)
            return;

        HoveredBackdrop = place;
        RefreshBackdrop();
    }

    private void ChooseBackdrop(MirrorBackdrop place)
    {
        if (!Visible)
            return;

        ChosenBackdrop = place;
        RefreshBackdrop();
    }

    private void RefreshBackdrop()
    {
        var shown = HoveredBackdrop ?? ChosenBackdrop;
        Preview.Backdrop = shown;
        BackdropName.Text = MirrorBackdrops.Name(shown);
        BackdropPicker.SetItems(MirrorBackdrops.All, ChosenBackdrop, 1, Backdrops.Thumbnail);
    }
```

`SetItems` re-announces hover only when a cell's item changes. This list never changes, so calling it from the hover handler cannot loop.

- [ ] **Step 5: Build**

Run: `dotnet build Chaos.Client/Chaos.Client.csproj`
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 6: Run the full suite**

Run: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi`
Expected: no new failures against the Task 3 baseline.

```json:metadata
{"files": ["Chaos.Client/Controls/World/Popups/Beauty/BeautyShopControl.cs"], "verifyCommand": "dotnet build Chaos.Client/Chaos.Client.csproj && dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi", "acceptanceCriteria": ["Gray 'Backdrop' label at (20,338), gold name label at (80,338), ThumbnailGrid<MirrorBackdrop> at (20,350): 6x1, 28px cells, 4px gaps, no arrows (188x28)", "Click sets static ChosenBackdrop; hover sets HoveredBackdrop; leaving clears it; preview and name show HoveredBackdrop ?? ChosenBackdrop; picker selects ChosenBackdrop", "Handlers ignore calls while hidden and never touch WorldState.BeautyShop, Select, Hover, the confirm dialog, Review.Status or head thumbnails", "GoTo clears HoveredBackdrop; Show() keeps ChosenBackdrop; Review uses ChosenBackdrop", "Client builds with 0 errors; full suite has no new failures"], "modelTier": "mechanical"}
```

---

### Task 7: Walkthrough rows and CLAUDE.md

**Goal:** Add the in-game checks B1–B7 for the user and mention the backdrop classes in `CLAUDE.md`.

**Files:**
- Modify: `docs/superpowers/plans/2026-08-29-beauty-shop-walkthrough.md`
- Modify: `CLAUDE.md`

**Acceptance Criteria:**
- [ ] The walkthrough ends with a "Map backdrops (2026-10-04)" section holding rows B1–B7, each `PENDING (user)`.
- [ ] `CLAUDE.md`'s `Beauty/` entry names `MirrorBackdrop`/`MirrorBackdrops`, `MirrorBackdropCache` and `Tools/MirrorBackdrops`.

**Verify:** `Select-String -Path docs/superpowers/plans/2026-08-29-beauty-shop-walkthrough.md -Pattern '^\| B[1-7] '` → 7 matches; `Select-String -Path CLAUDE.md -Pattern 'MirrorBackdropCache'` → 1 match

**Steps:**

- [ ] **Step 1: Append the walkthrough section**

Append to the end of `docs/superpowers/plans/2026-08-29-beauty-shop-walkthrough.md`:

```markdown

## Map backdrops (2026-10-04)

Spec: `docs/superpowers/specs/2026-10-04-mirror-backdrops-design.md`. In-game checks for the user; build the client from `feat/mirror-backdrops`.

| # | Check | Expected | Result | Notes |
|---|-------|----------|--------|-------|
| B1 | Open the mirror | Under Show gear / Randomize: gray "Backdrop", gold "Plain", and a row of six cells; Plain (an empty dark cell) has the gold border | PENDING (user) | |
| B2 | Click Mileth, then each other place | The place appears behind the figure with the feet on open ground; the pedestal's recessed rim still shows round the picture; the gold name follows the click | PENDING (user) | |
| B3 | Point at each cell, then move off the row | The preview and the name try each place on; moving off returns to the chosen one; the total and "No changes yet" do not change | PENDING (user) | |
| B4 | Press 2x/1x with a place chosen | The backdrop scales with the figure; the feet stay on the same spot | PENDING (user) | |
| B5 | Show gear on at 2x with your tallest gear | If the figure drops to 1x to fit, the backdrop drops with it | PENDING (user) | |
| B6 | Go to Review | NOW and NEW both stand on the chosen place; rotate and Show gear still work | PENDING (user) | |
| B7 | Close the mirror and reopen it; then restart the client and open it again | Reopening keeps the place; after a restart it starts as Plain | PENDING (user) | |
```

- [ ] **Step 2: Update `CLAUDE.md`**

In the `Beauty/` entry, replace:

```
ThumbnailGrid/SwatchGrid/GenderSelector/PageDots are the pickers, MirrorPreview the pedestal figure; the state is `ViewModel/BeautyShop`)
```

with:

```
ThumbnailGrid/SwatchGrid/GenderSelector/PageDots are the pickers, MirrorPreview the pedestal figure, drawn over a backdrop: `MirrorBackdrop`/`MirrorBackdrops` list the places, and `MirrorBackdropCache` cuts the PNGs embedded in Chaos.Client.Rendering, which `Tools/MirrorBackdrops` renders from real maps; the state is `ViewModel/BeautyShop`)
```

- [ ] **Step 3: Check**

Run the two `Select-String` commands from **Verify**. Expected: 7 matches, then 1 match.

```json:metadata
{"files": ["docs/superpowers/plans/2026-08-29-beauty-shop-walkthrough.md", "CLAUDE.md"], "verifyCommand": "Select-String -Path docs/superpowers/plans/2026-08-29-beauty-shop-walkthrough.md -Pattern '^\\| B[1-7] '; Select-String -Path CLAUDE.md -Pattern 'MirrorBackdropCache'", "acceptanceCriteria": ["Walkthrough ends with a 'Map backdrops (2026-10-04)' section with rows B1-B7, each PENDING (user)", "CLAUDE.md Beauty/ entry names MirrorBackdrop/MirrorBackdrops, MirrorBackdropCache and Tools/MirrorBackdrops"], "modelTier": "mechanical"}
```

---

### Task 8: Commit the full implementation

**Goal:** Stage every file this plan created or changed, by explicit path, and make one commit on `feat/mirror-backdrops`.

**Files:**
- All files from Tasks 1–7 (listed in Step 2)

**Acceptance Criteria:**
- [ ] One new commit on `feat/mirror-backdrops` holding exactly the files in Step 2 — 10 PNGs plus 17 other files (the amended spec, this plan and its tasks file among them).
- [ ] Nothing else is staged or committed (the shared checkout's unrelated changes, such as `.run/` files or the `Chaos-Server` pointer, stay out).
- [ ] The commit message ends with the Co-Authored-By line.

**Verify:** `git show --stat --format=%s HEAD` → subject "Josephine's Mirror: map backdrops behind the preview" and 27 files changed

**Steps:**

- [ ] **Step 1: Final build and test run**

Run: `dotnet build Chaos.Client/Chaos.Client.csproj` → `Build succeeded`, 0 errors.
Run: `dotnet run --project Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi` → no new failures against the Task 3 baseline.

- [ ] **Step 2: Stage by explicit path**

Don't use `git add -A`, `git add .` or `git stash` — other sessions share this checkout.

```bash
git add Tools/MirrorBackdrops/MirrorBackdrops.csproj Tools/MirrorBackdrops/Program.cs
git add Chaos.Client.Rendering/Assets/MirrorBackdrops/mileth.png Chaos.Client.Rendering/Assets/MirrorBackdrops/mileth-thumb.png \
        Chaos.Client.Rendering/Assets/MirrorBackdrops/woodlands.png Chaos.Client.Rendering/Assets/MirrorBackdrops/woodlands-thumb.png \
        Chaos.Client.Rendering/Assets/MirrorBackdrops/beach.png Chaos.Client.Rendering/Assets/MirrorBackdrops/beach-thumb.png \
        Chaos.Client.Rendering/Assets/MirrorBackdrops/frozencave.png Chaos.Client.Rendering/Assets/MirrorBackdrops/frozencave-thumb.png \
        Chaos.Client.Rendering/Assets/MirrorBackdrops/crypt.png Chaos.Client.Rendering/Assets/MirrorBackdrops/crypt-thumb.png
git add Chaos.Client.Rendering/Chaos.Client.Rendering.csproj Chaos.Client.Rendering/MirrorBackdropAssets.cs
git add Chaos.Client/Controls/World/Popups/Beauty/MirrorBackdrop.cs Chaos.Client/Controls/World/Popups/Beauty/MirrorBackdropCache.cs \
        Chaos.Client/Controls/World/Popups/Beauty/MirrorPreview.cs Chaos.Client/Controls/World/Popups/Beauty/ThumbnailGrid.cs \
        Chaos.Client/Controls/World/Popups/Beauty/BeautyShopControl.cs Chaos.Client/Controls/World/Popups/Beauty/Pages/ReviewPage.cs
git add Tests/Chaos.Client.Tests/MirrorBackdropTests.cs Tests/Chaos.Client.Tests/ThumbnailGridTests.cs
git add CLAUDE.md
git add -f docs/superpowers/plans/2026-08-29-beauty-shop-walkthrough.md
# execution ruling: the amended spec and this plan live only in the main checkout; copy them in and commit them too
M=/c/Users/Michael/Documents/GitHub/Chaos.Client/docs/superpowers
cp "$M/specs/2026-10-04-mirror-backdrops-design.md" docs/superpowers/specs/
cp "$M/plans/2026-10-04-mirror-backdrops.md" "$M/plans/2026-10-04-mirror-backdrops.md.tasks.json" docs/superpowers/plans/
git add -f docs/superpowers/specs/2026-10-04-mirror-backdrops-design.md docs/superpowers/plans/2026-10-04-mirror-backdrops.md \n        docs/superpowers/plans/2026-10-04-mirror-backdrops.md.tasks.json
git status --short
```

Expected: the 27 paths above staged (`A`/`M` in the first column); anything else shows only as unstaged or untracked.

- [ ] **Step 3: Commit**

```bash
git commit -F - <<'EOF'
Josephine's Mirror: map backdrops behind the preview

A row of six pictures under the mirror's preview puts the player in Mileth,
West Woodlands, Lynith Beach, Frozen Cave or a crypt, or back on the plain
pedestal. Hover tries one on; Review shows NOW and NEW on the chosen place.
The choice lasts while the game runs.

The pictures are cut from the real maps by Tools/MirrorBackdrops and embedded
in Chaos.Client.Rendering, so they don't depend on which maps a player has.
MirrorBackdropCache cuts each one per box so the standing tile sits under the
figure's feet at 1x and 2x. ThumbnailGrid gains a cell size and a no-arrows
option for the picker row.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

Run the **Verify** command. Expected: the subject line above and `27 files changed`.

```json:metadata
{"files": ["Tools/MirrorBackdrops/MirrorBackdrops.csproj", "Tools/MirrorBackdrops/Program.cs", "Chaos.Client.Rendering/Assets/MirrorBackdrops/*.png", "Chaos.Client.Rendering/Chaos.Client.Rendering.csproj", "Chaos.Client.Rendering/MirrorBackdropAssets.cs", "Chaos.Client/Controls/World/Popups/Beauty/MirrorBackdrop.cs", "Chaos.Client/Controls/World/Popups/Beauty/MirrorBackdropCache.cs", "Chaos.Client/Controls/World/Popups/Beauty/MirrorPreview.cs", "Chaos.Client/Controls/World/Popups/Beauty/ThumbnailGrid.cs", "Chaos.Client/Controls/World/Popups/Beauty/BeautyShopControl.cs", "Chaos.Client/Controls/World/Popups/Beauty/Pages/ReviewPage.cs", "Tests/Chaos.Client.Tests/MirrorBackdropTests.cs", "Tests/Chaos.Client.Tests/ThumbnailGridTests.cs", "CLAUDE.md", "docs/superpowers/plans/2026-08-29-beauty-shop-walkthrough.md", "docs/superpowers/specs/2026-10-04-mirror-backdrops-design.md", "docs/superpowers/plans/2026-10-04-mirror-backdrops.md", "docs/superpowers/plans/2026-10-04-mirror-backdrops.md.tasks.json"], "verifyCommand": "git show --stat --format=%s HEAD", "acceptanceCriteria": ["One new commit on feat/mirror-backdrops with exactly the 27 listed files (10 PNGs + 17 others, incl. spec, plan, tasks.json)", "No unrelated files staged or committed (.run/, Chaos-Server pointer stay out)", "Commit message ends with the Co-Authored-By line"], "modelTier": "mechanical"}
```
