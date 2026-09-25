# Guild Cloak Stretch Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-extended-cc:subagent-driven-development (recommended) or superpowers-extended-cc:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Paint guild cloak designs so the back emblem stays over the old rune, the hem runs along each frame's real lower edge, and inside-of-cape frames show the lining.

**Architecture:** `GuildCloakGrid` learns each column's bottom row and the old rune's centre, and gets a new cell lookup (`CellFor`) that replaces `CellAt(u, v)`. `GuildCloakPainter.Paint` calls it, with rune centring on for the Back part only. `AislingRenderer.RenderGuildCloakLayer` picks the lining canvas for inside-of-cape frames. Client only; no server, packet or data change.

**Tech Stack:** C# 14 / .NET 10, DALib 0.7.0 (`EpfFrame`, `EpfView`), SkiaSharp, TUnit 1.1.10 + FluentAssertions 8.8.0. Throwaway parity check in Python 3 (Unora's `acclib`) and a tiny .NET console program.

**Spec:** `docs/superpowers/specs/2026-09-25-guild-cloak-stretch-design.md` (read it; the rule's formulas are there). Deferred-work notes: `docs/superpowers/specs/2026-09-25-guild-cloak-stretch-notes.md`.

## Global Constraints

- **Work only in the client worktree** `C:/Users/Michael/Documents/GitHub/worktrees/cloak-stretch` on branch `fix/guild-cloak-stretch`. Never edit, stash, reset, clean or switch branches in the shared `C:/Users/Michael/Documents/GitHub/Chaos.Client` tree: other Claude sessions have uncommitted work there.
- **Build against the server worktree** `C:/Users/Michael/Documents/GitHub/worktrees/cloak-stretch-server` (detached at `0d8c1727f`, the client's submodule commit). Another session is editing guild cloak files in the shared `Chaos-Server` checkout; never compile against it. A client worktree's own `Chaos-Server` folder is empty.
- **Build command (BUILD):** `dotnet build C:/Users/Michael/Documents/GitHub/worktrees/cloak-stretch/Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/cloak-stretch-server`
- **Test command (TEST):** `dotnet run --no-build --project C:/Users/Michael/Documents/GitHub/worktrees/cloak-stretch/Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi`. Painter tests only: append `--treenode-filter "/*/*/GuildCloakPainterTests/*"`. Tests are TUnit executables: never `dotnet test`, never `--filter`. Always run BUILD first.
- **Serena paths** for worktree files are relative to `C:/Users/Michael/Documents/GitHub`, e.g. `worktrees/cloak-stretch/Chaos.Client.Rendering/GuildCloakGrid.cs`. Use Serena's symbol tools for C# reads and edits (user rule); built-in Read/Edit are fine for markdown.
- **Line endings:** the C# files are CRLF. A Serena needle that spans lines must use regex mode with `\r?\n` or `\s*`; a multi-line literal needle will not match. New lines written with LF are fine: `core.autocrlf=true` normalises them on commit.
- **Constants (exact):** dye slots 98–103 (`DYE_FIRST`, `DYE_LAST`); `HEM_DEPTH = 3`; `RUNE_MIN_PIXELS = 10`; `INSIDE_MIN_PIXELS = 150` (a frame needs *more than* 150 pixels).
- **Maths:** the new lookup computes in `double` and rounds with `Math.Round(double)` (midpoint to even). That matches the Python spike, which Task 3 checks cell for cell.
- **Frame bytes:** anything that scans `EpfFrame.Data` reads only the first `PixelWidth * PixelHeight` bytes. DALib returns most retail frames with the later frames' bytes after their own.
- **Commit strategy: at-end.** No per-task commits. Task 5 makes the single commit. Stage by explicit path only; never `git add -A` or `git add .`.
- **Never commit** `Chaos.Client/Properties/launchSettings.json` or a changed `Chaos-Server` submodule pointer.
- **Code style:** match the surrounding code: `//lowercase comments` without a space, XML doc summaries on public members, members roughly alphabetical within a visibility group, parenthesised compound conditions.
- A running game client or server locks build output in the shared tree. The worktree has its own `bin/`, so this should not happen; if a build fails with a locked file, stop and ask the user.

**User decisions (already made):**
- Rule: "edges + rune centre" (picked on the stepper page over "edges only" and hand-fitted per-frame data).
- Scope: fix the heart jump (problem 1), the thin hem on front walk frames 7–9 (problem 2) and the inside-of-cape frames (sheet c frames 3, 15, 20). Leave the side-on squeeze (problem 3) and the editor mirror (problem 4).
- Inside frames are found by a rule (no lining layer, more than 150 pixels, no dye pixel), not by a list of frame numbers.
- The design was approved in three sections and the spec was approved as written ("looks good").

---

## Workspace setup (coordinator, before Task 1)

Run from Git Bash. `0d8c1727fa96ead96dae9d5f059b3d667231448b` is the `Chaos-Server` commit client `main` points at.

```bash
git -C C:/Users/Michael/Documents/GitHub/Chaos.Client worktree add -b fix/guild-cloak-stretch C:/Users/Michael/Documents/GitHub/worktrees/cloak-stretch main
git -C C:/Users/Michael/Documents/GitHub/Chaos.Client/Chaos-Server -c core.longpaths=true worktree add --detach C:/Users/Michael/Documents/GitHub/worktrees/cloak-stretch-server 0d8c1727fa96ead96dae9d5f059b3d667231448b
dotnet build C:/Users/Michael/Documents/GitHub/worktrees/cloak-stretch/Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/cloak-stretch-server
dotnet run --no-build --project C:/Users/Michael/Documents/GitHub/worktrees/cloak-stretch/Tests/Chaos.Client.Tests/Chaos.Client.Tests.csproj -- --no-ansi
```

Expected: the build succeeds, and the test run ends with a summary line. Record the baseline counts (total, passed, failed) in the ledger. Any failure here existed before this work.

---

### Task 1: The edges + rune centre stretch rule

**Goal:** `GuildCloakGrid.CellFor` maps a frame pixel to a canvas cell by the spec's rule, and `GuildCloakPainter.Paint` uses it, with rune centring for the Back part only.

**Files:**
- Modify: `worktrees/cloak-stretch/Chaos.Client.Rendering/GuildCloakGrid.cs` (whole class)
- Modify: `worktrees/cloak-stretch/Chaos.Client.Rendering/GuildCloakPainter.cs` (class summary, dye constants, `Brightness`, both `Paint` overloads)
- Test: `worktrees/cloak-stretch/Tests/Chaos.Client.Tests/GuildCloakPainterTests.cs`

**Acceptance Criteria:**
- [ ] A 9×10 bell frame (with and without a rune) painted onto its own grid returns `(x, y)` for every filled pixel, with centring on and off
- [ ] On a 4×11 flap whose column bottoms lie at rows 10, 9, 8, 7, every column's bottom pixel paints in the canvas's hem colour (rows 9–10)
- [ ] Canvas rune centre 6 and frame rune centre 2: frame column 2 reads canvas column 6, flipped or not; without centring it reads 2 (8 flipped)
- [ ] Dye pixels within 3 rows of their column's bottom are not rune; 9 rune pixels give no centre, 10 give their mean
- [ ] `CellFor` skips empty canvas rows to the nearest filled row (the old `CellAt` test, ported)
- [ ] All existing `GuildCloakPainterTests` still pass; `CellAt` no longer exists

**Verify:** BUILD, then TEST with `--treenode-filter "/*/*/GuildCloakPainterTests/*"` → all pass, 0 failed.

**Steps:**

- [ ] **Step 1: Add the test helpers.** Insert after `InnerShade` in `GuildCloakPainterTests` (Serena `insert_after_symbol`, name path `GuildCloakPainterTests/InnerShade`):

```csharp

    /// <summary>
    ///     A 9x10 bell: rows 0-5 fill columns 2-6, row 6 columns 1-7, rows 7-8 columns 0-8 and row 9 columns 1-7, so columns 0
    ///     and 8 end a row higher. Cloth is 25. With <paramref name="rune" />, dye 100 fills columns 3-5 of rows 1-4. The bottom
    ///     pixel of every column is hem (dye 102).
    /// </summary>
    private static byte[] Bell(bool rune)
    {
        var data = new byte[9 * 10];
        (int First, int Last)[] rows = [(2, 6), (2, 6), (2, 6), (2, 6), (2, 6), (2, 6), (1, 7), (0, 8), (0, 8), (1, 7)];

        for (var y = 0; y < 10; y++)
            for (var x = rows[y].First; x <= rows[y].Last; x++)
                data[(y * 9) + x] = 25;

        if (rune)
            for (var y = 1; y <= 4; y++)
                for (var x = 3; x <= 5; x++)
                    data[(y * 9) + x] = 100;

        for (var x = 0; x < 9; x++)
        {
            var bottom = (x is 0 or 8) ? 8 : 9;
            data[(bottom * 9) + x] = 102;
        }

        return data;
    }

    /// <summary>A width x height grid of cloth (25) with dye 100 in columns first-last of rows top-bottom.</summary>
    private static byte[] WithDye(int width, int height, int first, int last, int top, int bottom)
    {
        var data = Solid(width, height, 25);

        for (var y = top; y <= bottom; y++)
            for (var x = first; x <= last; x++)
                data[(y * width) + x] = 100;

        return data;
    }
```

- [ ] **Step 2: Write the failing tests.** Replace the whole `CellAt_skips_empty_rows_to_the_nearest_filled_row` method (Serena `replace_symbol_body`, name path `GuildCloakPainterTests/CellAt_skips_empty_rows_to_the_nearest_filled_row`) with the ported test plus the new ones:

```csharp
    [Test]
    public void CellFor_skips_empty_rows_to_the_nearest_filled_row()
    {
        var data = new byte[15];

        foreach (var row in new[] { 0, 1, 4 })
            for (var x = 0; x < 3; x++)
                data[(row * 3) + x] = 25;

        var grid = new GuildCloakGrid(3, 5, data);
        var target = new GuildCloakGrid(3, 5, Solid(3, 5, 25));

        grid.CellFor(target, 1, 2, false, false).Y.Should().Be(1);
        grid.CellFor(target, 1, 3, false, false).Y.Should().Be(4);
        grid.CellFor(target, 2, 0, false, false).Should().Be((2, 0));
    }

    [Test]
    public void A_frame_painted_on_its_own_grid_reads_its_own_cells()
    {
        foreach (var rune in new[] { true, false })
        {
            var grid = new GuildCloakGrid(9, 10, Bell(rune));

            for (var y = 0; y < 10; y++)
                for (var x = 0; x < 9; x++)
                {
                    if (!grid.IsFilled(x, y))
                        continue;

                    grid.CellFor(grid, x, y, false, true).Should().Be((x, y));
                    grid.CellFor(grid, x, y, false, false).Should().Be((x, y));
                }
        }
    }

    [Test]
    public void Column_bottoms_low_in_the_frame_read_the_canvas_hem()
    {
        //a flap whose bottom edge runs diagonally through the lowest third: column x fills rows 0 to 10 - x
        var data = new byte[4 * 11];

        for (var x = 0; x < 4; x++)
            for (var y = 0; y <= 10 - x; y++)
                data[(y * 4) + x] = 25;

        var canvas = new GuildCloakGrid(4, 11, Solid(4, 11, 25));
        var cells = Solid(4, 11, 1);

        for (var i = 9 * 4; i < 11 * 4; i++)
            cells[i] = 2;

        var pixels = GuildCloakPainter.Paint(4, 11, data, Palette(), canvas, cells, [Red, Green], false);

        for (var x = 0; x < 4; x++)
            pixels[((10 - x) * 4) + x].Should().Be(GuildCloakPainter.Shade(Green, GuildCloakPainter.EDGE_SHADE), $"column {x}");
    }

    [Test]
    public void The_rune_centre_reads_the_canvas_rune_centre_and_a_flip_keeps_it_there()
    {
        //canvas rune: columns 5-7 (centre 6; the box's middle is 5). Frame rune: columns 1-3 (centre 2)
        var canvas = new GuildCloakGrid(11, 8, WithDye(11, 8, 5, 7, 1, 4));
        var target = new GuildCloakGrid(11, 8, WithDye(11, 8, 1, 3, 1, 4));

        canvas.RuneCentre.Should().Be(6);
        target.RuneCentre.Should().Be(2);

        canvas.CellFor(target, 2, 3, false, true).X.Should().Be(6);
        canvas.CellFor(target, 2, 3, true, true).X.Should().Be(6);

        //without centring the row stretches as a whole
        canvas.CellFor(target, 2, 3, false, false).X.Should().Be(2);
        canvas.CellFor(target, 2, 3, true, false).X.Should().Be(8);
    }

    [Test]
    public void Dye_pixels_near_the_bottom_of_their_column_are_hem_not_rune()
    {
        var grid = new GuildCloakGrid(12, 6, WithDye(12, 6, 0, 11, 3, 5));

        grid.RuneCentre.Should().BeNull();
    }

    [Test]
    public void A_rune_needs_ten_pixels()
    {
        new GuildCloakGrid(12, 8, WithDye(12, 8, 0, 8, 1, 1)).RuneCentre.Should().BeNull();
        new GuildCloakGrid(12, 8, WithDye(12, 8, 0, 9, 1, 1)).RuneCentre.Should().Be(4.5);
    }
```

- [ ] **Step 3: Run the tests to see them fail.** Run BUILD. Expected: compile errors `CS1061` for `CellFor` and `RuneCentre` on `GuildCloakGrid`. All the new tests share one file, so the compile error is the red state. (`Column_bottoms_low_in_the_frame_read_the_canvas_hem` uses only the existing `Paint` and would also fail on today's rule, which reads rows 7 and 8 for columns 3 and 2.)

- [ ] **Step 4: Rewrite `GuildCloakGrid`.** The code block below starts with the new class summary, then the class. In `worktrees/cloak-stretch/Chaos.Client.Rendering/GuildCloakGrid.cs`:
  1. Replace the old summary with Serena `replace_content` in regex mode, needle `///\s*<summary>\s*///\s*The outline of one cloak frame.*?///\s*</summary>`, replacement the six summary lines below.
  2. Replace the class (from `public sealed class GuildCloakGrid` to its closing brace) with Serena `replace_symbol_body`, name path `GuildCloakGrid`.

```csharp
/// <summary>
///     The outline of one cloak frame: which pixels it has, the first and last pixel of each row, the last pixel of each
///     column, and the centre of the old rune the cape art draws on its back. Coordinates are local to the frame (0,0 is its
///     top-left). Built for the three reference frames the design grids are painted on, and for every frame the painter
///     maps a design onto. The mapping rule is in <c>docs/superpowers/specs/2026-09-25-guild-cloak-stretch-design.md</c>.
/// </summary>
public sealed class GuildCloakGrid
{
    /// <summary>The first palette slot of the dye colors the cape art draws its rune and hem in.</summary>
    public const byte DYE_FIRST = 98;

    public const byte DYE_LAST = 103;

    /// <summary>A dye pixel fewer than this many rows above the bottom of its column is hem, not rune.</summary>
    public const int HEM_DEPTH = 3;

    /// <summary>A frame with fewer rune pixels than this has no rune centre.</summary>
    public const int RUNE_MIN_PIXELS = 10;

    private readonly int[] ColumnLast;
    private readonly bool[] Filled;
    private readonly int[] RowFirst;
    private readonly int[] RowLast;

    /// <param name="pixels">Palette indexes, row-major; 0 is empty.</param>
    public GuildCloakGrid(int width, int height, ReadOnlySpan<byte> pixels)
    {
        Width = width;
        Height = height;
        Filled = new bool[width * height];
        RowFirst = new int[height];
        RowLast = new int[height];
        ColumnLast = new int[width];
        Array.Fill(ColumnLast, -1);
        TopRow = -1;
        BottomRow = -1;

        for (var y = 0; y < height; y++)
        {
            RowFirst[y] = -1;
            RowLast[y] = -1;

            for (var x = 0; x < width; x++)
            {
                if (pixels[(y * width) + x] == 0)
                    continue;

                Filled[(y * width) + x] = true;
                ColumnLast[x] = y;

                if (RowFirst[y] < 0)
                    RowFirst[y] = x;

                RowLast[y] = x;
            }

            if (RowFirst[y] < 0)
                continue;

            if (TopRow < 0)
                TopRow = y;

            BottomRow = y;
        }

        RuneCentre = FindRuneCentre(pixels);
    }

    public int BottomRow { get; }
    public int Height { get; }
    public bool IsEmpty => TopRow < 0;

    /// <summary>
    ///     The mean column of the old rune: dye pixels at least <see cref="HEM_DEPTH" /> rows above the bottom of their column.
    ///     Null when there are fewer than <see cref="RUNE_MIN_PIXELS" />. It is the original artist's idea of where the middle
    ///     of the back is on this frame.
    /// </summary>
    public double? RuneCentre { get; }

    public int TopRow { get; }
    public int Width { get; }

    /// <summary>
    ///     The cell of this reference grid that pixel (<paramref name="x" />, <paramref name="y" />) of
    ///     <paramref name="target" /> reads. Across, the pixel's place in its row picks the column; with
    ///     <paramref name="centreOnRune" /> each row is split at the rune's centre, so the target's rune centre reads this
    ///     grid's. Down, the top of the layer reads by height as a fraction of the layer (the collar line stays put), and lower
    ///     down the pixel reads more and more by its distance above the bottom of its own column (a hem follows the real lower
    ///     edge). A frame painted onto its own grid reads its own cells.
    /// </summary>
    public (int X, int Y) CellFor(GuildCloakGrid target, int x, int y, bool flip, bool centreOnRune)
    {
        var centre = centreOnRune && target.RuneCentre.HasValue;
        var u = target.Across(x, y, centre);

        if (flip)
            u = 1 - u;

        var targetHeight = target.BottomRow - target.TopRow;
        var height = BottomRow - TopRow;
        var v = targetHeight == 0 ? 0.5 : (y - target.TopRow) / (double)targetHeight;
        var aboveBottom = target.ColumnLast[x] - y;

        var topReading = NearestRow((int)Math.Round(TopRow + (v * height)));
        var topColumn = NearestColumn((int)Math.Round(ColumnAt(topReading, u, centre)));
        var bottomReading = ColumnLast[topColumn] - (aboveBottom * height / (double)Math.Max(1, targetHeight));
        var cellY = NearestRow((int)Math.Round(((1 - v) * topReading) + (v * bottomReading)));
        var cellX = (int)Math.Round(ColumnAt(cellY, u, centre));

        return (Math.Clamp(cellX, RowFirst[cellY], RowLast[cellY]), cellY);
    }

    /// <summary>The last filled row of a column, or -1 for an empty column.</summary>
    public int ColumnBottom(int x) => ColumnLast[x];

    public static GuildCloakGrid FromFrame(EpfFrame frame) => new(frame.PixelWidth, frame.PixelHeight, frame.Data);

    /// <summary>
    ///     True when the frame's own box holds a pixel. DALib returns most retail frames with the later frames' bytes after
    ///     their own (their table's end address is not a real one), so only the first width x height bytes are the frame.
    /// </summary>
    public static bool HasPixels(EpfFrame frame)
    {
        var size = frame.PixelWidth * frame.PixelHeight;

        return (frame.PixelWidth > 0)
               && (frame.PixelHeight > 0)
               && frame.Data.AsSpan(0, Math.Min(size, frame.Data.Length)).ContainsAnyExcept((byte)0);
    }

    /// <summary>True for the dye slots, which the cape art uses only for its rune and hem.</summary>
    public static bool IsDye(byte index) => index is >= DYE_FIRST and <= DYE_LAST;

    public bool IsFilled(int x, int y) => (x >= 0) && (y >= 0) && (x < Width) && (y < Height) && Filled[(y * Width) + x];

    /// <summary>The first and last pixel of a row, or (-1, -1) for an empty row.</summary>
    public (int First, int Last) Row(int y) => (RowFirst[y], RowLast[y]);

    /// <summary>Where a pixel sits across its row: 0 at the first pixel, 1 at the last, and 0.5 at the rune with a centre.</summary>
    private double Across(int x, int y, bool centre)
    {
        var first = RowFirst[y];
        var last = RowLast[y];

        if (centre && RuneCentre is { } rune && (first < rune) && (rune < last))
            return x <= rune ? 0.5 * (x - first) / (rune - first) : 0.5 + (0.5 * (x - rune) / (last - rune));

        return last == first ? 0.5 : (x - first) / (double)(last - first);
    }

    /// <summary>The column at fraction <paramref name="u" /> of a row: the inverse of <see cref="Across" />.</summary>
    private double ColumnAt(int y, double u, bool centre)
    {
        var first = RowFirst[y];
        var last = RowLast[y];

        if (centre && RuneCentre is { } rune && (first < rune) && (rune < last))
            return u <= 0.5 ? first + (2 * u * (rune - first)) : rune + (((2 * u) - 1) * (last - rune));

        return first + (u * (last - first));
    }

    private double? FindRuneCentre(ReadOnlySpan<byte> pixels)
    {
        var sum = 0;
        var count = 0;

        for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
            {
                if (!IsDye(pixels[(y * Width) + x]) || (ColumnLast[x] - y < HEM_DEPTH))
                    continue;

                sum += x;
                count++;
            }

        return count >= RUNE_MIN_PIXELS ? sum / (double)count : null;
    }

    /// <summary>The filled column nearest <paramref name="x" />, the left one on a tie.</summary>
    private int NearestColumn(int x)
    {
        for (var d = 0; d < Width; d++)
        {
            if ((x - d >= 0) && (x - d < Width) && (ColumnLast[x - d] >= 0))
                return x - d;

            if ((x + d >= 0) && (x + d < Width) && (ColumnLast[x + d] >= 0))
                return x + d;
        }

        return Math.Clamp(x, 0, Width - 1);
    }

    /// <summary>The filled row nearest <paramref name="y" />, trying the row below before the row above.</summary>
    private int NearestRow(int y)
    {
        y = Math.Clamp(y, TopRow, BottomRow);

        for (var d = 0; d <= BottomRow - TopRow; d++)
        {
            if ((y + d <= BottomRow) && (RowFirst[y + d] >= 0))
                return y + d;

            if ((y - d >= TopRow) && (RowFirst[y - d] >= 0))
                return y - d;
        }

        return y;
    }
}
```

- [ ] **Step 5: Point the painter at the new lookup.** In `worktrees/cloak-stretch/Chaos.Client.Rendering/GuildCloakPainter.cs`:

  a. Delete the two private constants (lines 30–31, after `DARK_BRIGHTNESS`) with Serena `replace_content` in regex mode, needle `    private const byte DYE_FIRST = 98;\r?\n    private const byte DYE_LAST = 103;\r?\n\r?\n`, replacement empty. `DARK_BRIGHTNESS`, one blank line and the `Brightness` summary should follow each other afterwards.

  b. In `Brightness`, replace `if (index is < DYE_FIRST or > DYE_LAST)` with `if (!GuildCloakGrid.IsDye(index))`, and replace `if (neighbour is 0 or (>= DYE_FIRST and <= DYE_LAST))` with `if ((neighbour == 0) || GuildCloakGrid.IsDye(neighbour))`.

  c. Replace the `EpfFrame` overload (name path `GuildCloakPainter/Paint[0]`):
  ```csharp
      public static SKColor[] Paint(
          EpfFrame frame,
          IList<SKColor> palette,
          GuildCloakGrid reference,
          ReadOnlySpan<byte> cells,
          IReadOnlyList<SKColor> colors,
          bool flip,
          bool centreOnRune = false)
          => Paint(
              frame.PixelWidth,
              frame.PixelHeight,
              frame.Data,
              palette,
              reference,
              cells,
              colors,
              flip,
              centreOnRune);
  ```

  d. Replace the pixel overload (name path `GuildCloakPainter/Paint[1]`) with `replace_symbol_body`, using the method below without its summary. Then replace its summary with `replace_content` in regex mode, needle `///\s*<summary>\s*///\s*The painted pixels of a frame.*?///\s*</summary>`, replacement the summary lines below:
  ```csharp
      /// <summary>
      ///     The painted pixels of a frame, row-major, the frame's size. Empty pixels stay transparent. For a draw the renderer
      ///     will flip, <paramref name="flip" /> reads the design mirrored, so the flip turns it the right way round.
      ///     <paramref name="centreOnRune" /> centres each row on the old rune (<see cref="GuildCloakGrid.RuneCentre" />); the
      ///     renderer sets it for the Back part only.
      /// </summary>
      public static SKColor[] Paint(
          int width,
          int height,
          byte[] data,
          IList<SKColor> palette,
          GuildCloakGrid reference,
          ReadOnlySpan<byte> cells,
          IReadOnlyList<SKColor> colors,
          bool flip,
          bool centreOnRune = false)
      {
          var result = new SKColor[width * height];
          var target = new GuildCloakGrid(width, height, data);

          if (target.IsEmpty || reference.IsEmpty || (colors.Count == 0))
              return result;

          for (var y = target.TopRow; y <= target.BottomRow; y++)
          {
              (var first, var last) = target.Row(y);

              if (first < 0)
                  continue;

              for (var x = first; x <= last; x++)
              {
                  if (data[(y * width) + x] == 0)
                      continue;

                  (var cellX, var cellY) = reference.CellFor(target, x, y, flip, centreOnRune);
                  var color = colors[ColorNumberAt(reference, cells, cellX, cellY, colors.Count) - 1];

                  var shade = IsEdge(target, x, y)
                      ? EDGE_SHADE
                      : BASE_SHADE + (LIGHT_SHADE * Brightness(data, palette, width, height, x, y) / FULL_BRIGHTNESS);

                  result[(y * width) + x] = Shade(color, shade);
              }
          }

          return result;
      }
  ```

  e. Update the class summary above `public static class GuildCloakPainter` with `replace_content` in regex mode, needle `///\s*<summary>\s*///\s*Paints a guild cloak design.*?///\s*</summary>`, replacement:
  ```csharp
  /// <summary>
  ///     Paints a guild cloak design onto one frame of the guild cloak sprite. Each pixel takes its color from the reference
  ///     grid's cell that <see cref="GuildCloakGrid.CellFor" /> picks, and keeps the frame's own light and shadow. The shading
  ///     numbers come from the design session's mapping test
  ///     (<c>Unora/.superpowers/brainstorm/345065-1790308407/spike/fullpaint.py</c>); the mapping rule is in
  ///     <c>docs/superpowers/specs/2026-09-25-guild-cloak-stretch-design.md</c>.
  /// </summary>
  ```

- [ ] **Step 6: Run the painter tests.** BUILD, then TEST with `--treenode-filter "/*/*/GuildCloakPainterTests/*"`. Expected: 14 tests pass (the 9 existing ones, with `CellAt_…` now `CellFor_…`, plus the 5 new ones), 0 failed. `AislingRenderer` needs no change in this task, because the new `Paint` parameter is optional. If the build reports another caller of the removed `CellAt`, stop and report it; the painter and one test were its only callers when this plan was written.

```json:metadata
{"files": ["Chaos.Client.Rendering/GuildCloakGrid.cs", "Chaos.Client.Rendering/GuildCloakPainter.cs", "Tests/Chaos.Client.Tests/GuildCloakPainterTests.cs"], "verifyCommand": "BUILD && TEST --treenode-filter \"/*/*/GuildCloakPainterTests/*\"", "acceptanceCriteria": ["bell frame maps onto itself exactly, centring on and off", "flap column bottoms at rows 10-7 paint the hem colour", "rune centre 2 reads canvas centre 6, flipped or not; 2 and 8 without centring", "hem-depth and ten-pixel rune rules", "CellFor skips empty rows", "existing painter tests pass; CellAt removed"], "modelTier": "standard"}
```

---

### Task 2: Paint inside-of-cape frames from the lining

**Goal:** A main (`c`) layer frame that doesn't face the viewer, has more than 150 pixels and no dye pixel is painted from the lining canvas; the Back part is painted with rune centring.

**Files:**
- Modify: `worktrees/cloak-stretch/Chaos.Client.Rendering/GuildCloakGrid.cs` (add `INSIDE_MIN_PIXELS`, `IsInsideView`)
- Modify: `worktrees/cloak-stretch/Chaos.Client.Rendering/AislingRenderer.cs` (`RenderGuildCloakLayer`)
- Test: `worktrees/cloak-stretch/Tests/Chaos.Client.Tests/GuildCloakPainterTests.cs`

**Acceptance Criteria:**
- [ ] `IsInsideView` is true for a 13×12 frame of cloth (156 pixels, no dye)
- [ ] It is false for the same frame with one dye pixel, and for a 15×10 frame (150 pixels)
- [ ] Dye bytes after the frame's width × height are ignored
- [ ] `RenderGuildCloakLayer` uses `GuildCloakPart.Lining` for such a `c` frame and passes `centreOnRune: true` only for `GuildCloakPart.Back`

**Verify:** BUILD, then TEST with `--treenode-filter "/*/*/GuildCloakPainterTests/*"` → all pass, 0 failed.

**Steps:**

- [ ] **Step 1: Write the failing tests.** Insert after `WithDye` in `GuildCloakPainterTests` (Serena `insert_after_symbol`, name path `GuildCloakPainterTests/WithDye`):

```csharp

    private static EpfFrame Box(int width, int height, byte[] data)
        => new()
        {
            Left = 0,
            Top = 0,
            Right = (short)width,
            Bottom = (short)height,
            Data = data
        };
```

Then insert after `A_rune_needs_ten_pixels` (name path `GuildCloakPainterTests/A_rune_needs_ten_pixels`):

```csharp

    [Test]
    public void A_big_frame_with_no_dye_pixel_shows_the_inside_of_the_cape()
    {
        GuildCloakGrid.IsInsideView(Box(13, 12, Solid(13, 12, 25))).Should().BeTrue();
    }

    [Test]
    public void A_dye_pixel_or_a_small_frame_is_not_the_inside()
    {
        var withDye = Solid(13, 12, 25);
        withDye[40] = 100;

        GuildCloakGrid.IsInsideView(Box(13, 12, withDye)).Should().BeFalse();
        GuildCloakGrid.IsInsideView(Box(15, 10, Solid(15, 10, 25))).Should().BeFalse();
    }

    [Test]
    public void IsInsideView_reads_only_the_frame_box()
    {
        //the bytes after the box belong to later frames (see HasPixels_reads_only_the_frame_box)
        var data = Solid(13, 12, 25).Concat(Solid(4, 1, 100)).ToArray();

        GuildCloakGrid.IsInsideView(Box(13, 12, data)).Should().BeTrue();
    }
```

- [ ] **Step 2: Run to see them fail.** BUILD. Expected: compile error `CS0117` — `GuildCloakGrid` has no `IsInsideView`.

- [ ] **Step 3: Add the check to `GuildCloakGrid`.** Insert the constant after `RUNE_MIN_PIXELS` (`replace_content`, literal: find `public const int RUNE_MIN_PIXELS = 10;` and replace with):

```csharp
public const int RUNE_MIN_PIXELS = 10;

    /// <summary>A frame showing the inside of the cape has more pixels than this; a collar-only frame has fewer.</summary>
    public const int INSIDE_MIN_PIXELS = 150;
```

Insert the method after `IsFilled` (`insert_after_symbol`, name path `GuildCloakGrid/IsFilled`):

```csharp

    /// <summary>
    ///     True when a cape frame shows the inside of the cape, as when it flies out behind a side-on body: more than
    ///     <see cref="INSIDE_MIN_PIXELS" /> pixels and no dye pixel (the art draws its rune and hem only on the outside). Reads
    ///     only the frame's own box (see <see cref="HasPixels" />).
    /// </summary>
    public static bool IsInsideView(EpfFrame frame)
    {
        if ((frame.PixelWidth <= 0) || (frame.PixelHeight <= 0))
            return false;

        var count = 0;

        foreach (var index in frame.Data.AsSpan(0, Math.Min(frame.PixelWidth * frame.PixelHeight, frame.Data.Length)))
        {
            if (IsDye(index))
                return false;

            if (index != 0)
                count++;
        }

        return count > INSIDE_MIN_PIXELS;
    }
```

- [ ] **Step 4: Use it in the renderer.** In `worktrees/cloak-stretch/Chaos.Client.Rendering/AislingRenderer.cs`, method `RenderGuildCloakLayer`, this block exists today:

```csharp
        var part = (typeLetter, frontFacing) switch
        {
            ('g', true) => GuildCloakPart.Lining,
            ('c', true) => GuildCloakPart.Collar,
            _           => GuildCloakPart.Back
        };
```

Replace it with Serena `replace_content` in regex mode, needle `var part = \(typeLetter, frontFacing\) switch.*?_\s+=> GuildCloakPart\.Back\s*\};` (it matches only inside this method; if Serena reports more than one match, stop and report), with:

```csharp
        //a cape flying out behind a side-on body shows its inside: no lining layer, and none of the old art's rune or hem
        var insideView = (typeLetter == 'c') && !frontFacing && GuildCloakGrid.IsInsideView(frame);

        var part = insideView
            ? GuildCloakPart.Lining
            : (typeLetter, frontFacing) switch
            {
                ('g', true) => GuildCloakPart.Lining,
                ('c', true) => GuildCloakPart.Collar,
                _           => GuildCloakPart.Back
            };
```

Then pass the centring flag to `Paint` in the same method: `replace_content` in regex mode, needle `GuildCloakPainter\.ToColors\(design\.Colors\),(\s*)flip\);`, replacement `GuildCloakPainter.ToColors(design.Colors),$!1flip,$!1part == GuildCloakPart.Back);`. Afterwards the call's last two arguments read:

```csharp
            GuildCloakPainter.ToColors(design.Colors),
            flip,
            part == GuildCloakPart.Back);
```

Check the result with `find_symbol` `AislingRenderer/RenderGuildCloakLayer` `include_body=true`.

- [ ] **Step 5: Run the tests.** BUILD, then TEST with `--treenode-filter "/*/*/GuildCloakPainterTests/*"`. Expected: 17 tests pass, 0 failed.

```json:metadata
{"files": ["Chaos.Client.Rendering/GuildCloakGrid.cs", "Chaos.Client.Rendering/AislingRenderer.cs", "Tests/Chaos.Client.Tests/GuildCloakPainterTests.cs"], "verifyCommand": "BUILD && TEST --treenode-filter \"/*/*/GuildCloakPainterTests/*\"", "acceptanceCriteria": ["13x12 cloth frame is inside view", "one dye pixel or 150 pixels is not", "bytes past width x height ignored", "renderer picks Lining for inside frames and centres only the Back part"], "modelTier": "mechanical"}
```

---

### Task 3: Check the client against the spike on real frames

**Goal:** Every cloak pixel of sprite 328 (both bodies, sheets 01, 02, 03, b, c, d, e, f, flipped and not) maps to the same canvas cell in the client as in the Python spike's version of the spec's rule.

**Files (throwaway, never committed):**
- Create: `worktrees/cloak-stretch/.superpowers/parity/Parity.csproj`
- Create: `worktrees/cloak-stretch/.superpowers/parity/Program.cs`
- Create: `C:/Users/Michael/Documents/GitHub/Chaos.Client/.superpowers/brainstorm/142375-1790333274/spike/parity.py`

**Acceptance Criteria:**
- [ ] The C# program writes one line per painted pixel per flip to `cells_cs.txt`
- [ ] `parity.py` writes the same lines from the spike to `cells_py.txt` and reports `0 differ` with equal line counts
- [ ] Inside-of-cape frames are sheet c frames 3, 15 and 20 on both bodies, and no others (printed by the C# program)

**Verify:** `python .../spike/parity.py C:/Users/Michael/Documents/GitHub/worktrees/cloak-stretch/.superpowers/parity/cells_cs.txt` → prints `0 differ` and the two equal counts.

**Steps:**

- [ ] **Step 1: Create the project.** `.superpowers/` is gitignored in the client, so nothing here is committed. `Parity.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\Chaos.Client.Rendering\Chaos.Client.Rendering.csproj" />
  </ItemGroup>
</Project>
```

`Program.cs`:

```csharp
using Chaos.Client.Rendering;
using DALib.Data;
using DALib.Drawing;
using DALib.Drawing.Virtualized;

//THROWAWAY: the client's GuildCloakGrid.CellFor on every cloak pixel of sprite 328, for comparing with the spike
var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Unora", "Unora Files");
var archives = new Dictionary<string, DataArchive>();
using var output = new StreamWriter(args.Length > 0 ? args[0] : "cells_cs.txt");
var lines = 0;

EpfView? Load(char body, char letter, string anim)
{
    var name = letter == 'c' ? $"khan{body}ad.dat" : $"khan{body}eh.dat";

    if (!archives.TryGetValue(name, out var archive))
        archives[name] = archive = DataArchive.FromFile(Path.Combine(dir, name));

    return archive.TryGetValue($"{body}{letter}328{anim}.epf", out var entry) ? EpfView.FromEntry(entry) : null;
}

void Write(char body, string anim, char layer, int index, EpfFrame frame, GuildCloakGrid reference, bool centre)
{
    var target = GuildCloakGrid.FromFrame(frame);

    if (target.IsEmpty)
        return;

    foreach (var flip in new[] { false, true })
        for (var y = target.TopRow; y <= target.BottomRow; y++)
            for (var x = 0; x < target.Width; x++)
            {
                if (!target.IsFilled(x, y))
                    continue;

                (var cx, var cy) = reference.CellFor(target, x, y, flip, centre);
                output.WriteLine($"{body} {anim} {layer} {index} {(flip ? 1 : 0)} {x} {y} {cx} {cy}");
                lines++;
            }
}

var maleC = Load('m', 'c', "01")!;
var maleG = Load('m', 'g', "01")!;
var back = GuildCloakGrid.FromFrame(maleC[0]);
var lining = GuildCloakGrid.FromFrame(maleG[5]);
var collar = GuildCloakGrid.FromFrame(maleC[5]);

foreach (var body in "mw")
    foreach (var anim in new[] { "01", "02", "03", "b", "c", "d", "e", "f" })
    {
        var c = Load(body, 'c', anim);
        var g = Load(body, 'g', anim);

        if (c is null)
            continue;

        for (var i = 0; i < c.Count; i++)
        {
            var front = g is not null && (i < g.Count) && GuildCloakGrid.HasPixels(g[i]);

            if (front)
                Write(body, anim, 'g', i, g![i], lining, false);

            if (!GuildCloakGrid.HasPixels(c[i]))
                continue;

            var inside = !front && GuildCloakGrid.IsInsideView(c[i]);

            if (inside)
                Console.WriteLine($"inside: {body} {anim} {i}");

            Write(body, anim, 'c', i, c[i], front ? collar : inside ? lining : back, !front && !inside);
        }
    }

Console.WriteLine($"{lines} lines");
```

- [ ] **Step 2: Build and run it.**

```bash
dotnet build C:/Users/Michael/Documents/GitHub/worktrees/cloak-stretch/.superpowers/parity/Parity.csproj -p:UnoraServerPath=C:/Users/Michael/Documents/GitHub/worktrees/cloak-stretch-server
dotnet C:/Users/Michael/Documents/GitHub/worktrees/cloak-stretch/.superpowers/parity/bin/Debug/net10.0/Parity.dll C:/Users/Michael/Documents/GitHub/worktrees/cloak-stretch/.superpowers/parity/cells_cs.txt
```

Expected: six `inside:` lines (`m c 3`, `m c 15`, `m c 20`, `w c 3`, `w c 15`, `w c 20`) and a line count in the hundreds of thousands. If the build output folder differs, find `Parity.dll` under `bin/`.

- [ ] **Step 3: Write `parity.py`** in the spike folder (it imports the spike's `stretch` and `mapping` modules):

```python
"""THROWAWAY: the spec's rule on every cloak pixel of sprite 328 via the spike, compared with the client's output."""
import sys
sys.path.insert(0, r"C:\Users\Michael\Documents\GitHub\Chaos.Client\.superpowers\brainstorm\142375-1790333274\spike")
import stretch as s
import mapping as m

CS = sys.argv[1]
PY = CS.replace("cells_cs.txt", "cells_py.txt")


class Local:
    """A frame's pixels in the frame's own coordinates, as the client's grid sees them."""

    def __init__(self, f):
        self._px = {(x - f.left, y - f.top): v for (x, y), v in f.pixels().items()}

    def pixels(self):
        return self._px


def spec_cell(t, c, x, y, flip, centre):
    """mapping.anchored_cell with the spec's two edge rules: centring needs the target's rune centre, and a one-row
    layer uses v = 0.5."""
    centre = centre and t.cx is not None
    u = t.u_centred(x, y) if centre else t.u_row(x, y)
    if flip:
        u = 1 - u
    fh = t.bot - t.top
    ch = c.bot - c.top
    v = 0.5 if fh == 0 else (y - t.top) / fh
    d = t.cols[x][1] - y
    y_top = m._near_row(c, int(round(c.top + v * ch)))
    x_top = m._near_col(c, int(round(m._u_x(c, y_top, u, centre))))
    y_bot = c.cols[x_top][1] - d * ch / max(1, fh)
    Y = m._near_row(c, int(round((1 - v) * y_top + v * y_bot)))
    X = int(round(m._u_x(c, Y, u, centre)))
    lo, hi = c.rows[Y]
    return max(lo, min(hi, X)), Y


back = m.Shape(Local(s.frame("m", "c", "01", 0)))
lining = m.Shape(Local(s.frame("m", "g", "01", 5)))
collar = m.Shape(Local(s.frame("m", "c", "01", 5)))
lines = []
for g in "mw":
    for anim in ("01", "02", "03", "b", "c", "d", "e", "f"):
        cs = s.sheet(g, "c", anim)
        gs = s.sheet(g, "g", anim)
        for i, cf in enumerate(cs):
            gf = gs[i] if i < len(gs) else None
            front = gf is not None and bool(gf.pixels())
            layers = []
            if front:
                layers.append(("g", gf, lining, False))
            px = cf.pixels()
            if px:
                inside = not front and len(px) > 150 and not any(v in m.DYE for v in px.values())
                layers.append(("c", cf, collar if front else lining if inside else back, not front and not inside))
            for layer, f, canvas, centre in layers:
                t = m.Shape(Local(f))
                for flip in (0, 1):
                    for (x, y) in t.px:
                        X, Y = spec_cell(t, canvas, x, y, flip, centre)
                        lines.append(f"{g} {anim} {layer} {i} {flip} {x} {y} {X} {Y}")

open(PY, "w").write("\n".join(sorted(lines)) + "\n")
theirs = set(open(CS).read().split("\n")) - {""}
ours = set(lines)
diff = sorted(ours ^ theirs)
print(f"python {len(ours)} lines, client {len(theirs)} lines, {len(diff)} differ")
for line in diff[:20]:
    print(("only python: " if line in ours else "only client: ") + line)
```

- [ ] **Step 4: Run it.**

```bash
python C:/Users/Michael/Documents/GitHub/Chaos.Client/.superpowers/brainstorm/142375-1790333274/spike/parity.py C:/Users/Michael/Documents/GitHub/worktrees/cloak-stretch/.superpowers/parity/cells_cs.txt
```

Expected: `python N lines, client N lines, 0 differ` with the same N. If lines differ, pick one frame from the output and compare the two implementations step by step (`Across`, `topReading`, `topColumn`, `bottomReading`, `cellY`, `cellX`) against the spec's formulas. Fix the C# where it departs from the spec, re-run Task 1's and Task 2's tests, then re-run this task. If the Python departs from the spec, fix `spec_cell` instead and say so in the report.

```json:metadata
{"files": [".superpowers/parity/Parity.csproj", ".superpowers/parity/Program.cs", "C:/Users/Michael/Documents/GitHub/Chaos.Client/.superpowers/brainstorm/142375-1790333274/spike/parity.py"], "verifyCommand": "python C:/Users/Michael/Documents/GitHub/Chaos.Client/.superpowers/brainstorm/142375-1790333274/spike/parity.py C:/Users/Michael/Documents/GitHub/worktrees/cloak-stretch/.superpowers/parity/cells_cs.txt", "acceptanceCriteria": ["client writes cells_cs.txt", "0 differ with equal counts", "inside frames are exactly c 3, 15, 20 on both bodies"], "modelTier": "standard"}
```

---

### Task 4: Update the stretch notes

**Goal:** The deferred-work notes say what this change fixed, describe the new rule, and keep the side-on squeeze and the mirror as open work.

**Files:**
- Modify: `worktrees/cloak-stretch/docs/superpowers/specs/2026-09-25-guild-cloak-stretch-notes.md`

**Acceptance Criteria:**
- [ ] Line 3 says problems 1 and 2 and the inside frames are fixed by `2026-09-25-guild-cloak-stretch-design.md`, and problems 3 and 4 are open
- [ ] "How the stretch works today" describes the new rule, not the old one
- [ ] Problems 1 and 2 are marked fixed; problems 3, 4 and 5 stay; the new spike folder is listed under "How to judge an approach"

**Verify:** `grep -n "not started\|Nothing ties the paint" <notes file>` → no output; `grep -c "Fixed" <notes file>` → at least 2.

**Steps:**

- [ ] **Step 1: Status line.** Replace line 3 (`Date: 2026-09-25. Status: **not started**. The user chose to fix the painting bugs first and come back to this.`) with:

```markdown
Date: 2026-09-25. Status: **partly done.** Problems 1 and 2 below, and the inside-of-cape frames, are fixed by
`2026-09-25-guild-cloak-stretch-design.md`. Problems 3 and 4 are still open.
```

- [ ] **Step 2: Rule description.** Replace the whole "## How the stretch works today" section (from that heading up to, not including, "## What still goes wrong") with:

```markdown
## How the stretch works today

For each pixel of a frame layer, `GuildCloakGrid.CellFor` picks a canvas cell. The full rule, with formulas, is in
`2026-09-25-guild-cloak-stretch-design.md`.

1. Across: the pixel's place in its row. On back frames each row is split at the old rune's centre (dye pixels at
   least 3 rows above the bottom of their column), so the rune's centre reads the canvas's rune centre.
2. Down: at the top of the layer, the pixel's height as a fraction of the layer (the old rule). Lower down, more and
   more by its distance above the bottom of its own column, so a hem follows the real lower edge.

A main-layer frame with no lining layer, more than 150 pixels and no dye pixel shows the inside of the cape. It is
painted from the lining canvas: sheet c frames 3, 15 and 20 on both bodies.
```

- [ ] **Step 3: Mark what is fixed.** In "## What still goes wrong", start item 1 with `**Fixed** by the stretch design (the heart now stays within about 1 pixel of the rune sideways).` and item 2 with `**Fixed** by the stretch design.` Leave items 3, 4 and 5 as they are, and append to item 5: `Rechecked with the new rule on the 2026-09-25 stepper page: fine.`

- [ ] **Step 4: Spike folder.** At the end of "## How to judge an approach", add:

```markdown

The stretch design's spike is in `Chaos.Client/.superpowers/brainstorm/142375-1790333274/` (not committed):
`spike/mapping.py` holds the candidate rules (the chosen one is `map_anchored(centred=True)`), `spike/parity.py`
checks the client against it, and `content/compare-rules-v2.html` steps through every frame.
```

```json:metadata
{"files": ["docs/superpowers/specs/2026-09-25-guild-cloak-stretch-notes.md"], "verifyCommand": "grep -n \"not started\\|Nothing ties the paint\" docs/superpowers/specs/2026-09-25-guild-cloak-stretch-notes.md (expect no output)", "acceptanceCriteria": ["status line updated", "rule section rewritten", "problems 1 and 2 marked fixed, 3-5 kept, spike folder listed"], "modelTier": "mechanical"}
```

---

### Task 5: Commit the full implementation

**Goal:** One commit on `fix/guild-cloak-stretch` in the client worktree holding the whole change.

**Files:**
- Stage (explicit paths, in the worktree): `Chaos.Client.Rendering/GuildCloakGrid.cs`, `Chaos.Client.Rendering/GuildCloakPainter.cs`, `Chaos.Client.Rendering/AislingRenderer.cs`, `Tests/Chaos.Client.Tests/GuildCloakPainterTests.cs`, `docs/superpowers/specs/2026-09-25-guild-cloak-stretch-notes.md`

**Acceptance Criteria:**
- [ ] The full client test suite passes, or fails only in tests that also failed in the setup baseline
- [ ] `git -C <worktree> show --stat HEAD` lists exactly the five files above
- [ ] Nothing under `.superpowers/`, no `launchSettings.json`, no `Chaos-Server` pointer change is committed
- [ ] The message ends with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`

**Verify:** `git -C C:/Users/Michael/Documents/GitHub/worktrees/cloak-stretch show --stat HEAD` → the five files; `git -C C:/Users/Michael/Documents/GitHub/worktrees/cloak-stretch status --short` → empty.

**Steps:**

- [ ] **Step 1: Full suite.** BUILD, then TEST with no filter. Compare with the setup baseline. Stop and report if a test that passed in the baseline now fails.

- [ ] **Step 2: Check the change set.**

```bash
git -C C:/Users/Michael/Documents/GitHub/worktrees/cloak-stretch status --short
```

Expected: exactly the five files, all ` M`. Anything else: stop and report.

- [ ] **Step 3: Commit.**

```bash
cd C:/Users/Michael/Documents/GitHub/worktrees/cloak-stretch
git add Chaos.Client.Rendering/GuildCloakGrid.cs Chaos.Client.Rendering/GuildCloakPainter.cs Chaos.Client.Rendering/AislingRenderer.cs Tests/Chaos.Client.Tests/GuildCloakPainterTests.cs docs/superpowers/specs/2026-09-25-guild-cloak-stretch-notes.md
git commit -m "$(cat <<'EOF'
Stretch guild cloaks by column bottoms and the old rune

The hem now runs along each frame's real lower edge (front walk frames 7-9), the back
emblem stays over the old rune when the cape billows (walk frame 3 and its copies), and
inside-of-cape frames (sheet c frames 3, 15 and 20) paint from the lining.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
git show --stat HEAD
```

- [ ] **Step 4: Leave the merge to the finishing step.** Do not merge, push or remove the worktrees here. After the merge into client `main` (done from the shared tree with `git merge --no-ff fix/guild-cloak-stretch`, HEAD never leaving `main`), run `graphify update .` in the shared tree.

```json:metadata
{"files": ["Chaos.Client.Rendering/GuildCloakGrid.cs", "Chaos.Client.Rendering/GuildCloakPainter.cs", "Chaos.Client.Rendering/AislingRenderer.cs", "Tests/Chaos.Client.Tests/GuildCloakPainterTests.cs", "docs/superpowers/specs/2026-09-25-guild-cloak-stretch-notes.md"], "verifyCommand": "git -C C:/Users/Michael/Documents/GitHub/worktrees/cloak-stretch show --stat HEAD", "acceptanceCriteria": ["full suite passes vs baseline", "exactly five files committed", "no .superpowers, launchSettings or submodule change", "attribution line"], "modelTier": "mechanical"}
```

---

## After the plan (the user's steps)

The spec's hand check, after the merge: open the cloak editor with the Richards draft and watch the preview walk away (the heart stays over the middle of the back) and toward you (the white hem runs along the flap's lower edge). Then wear the cloak, use a skill that plays sheet c (the cape's inside shows the lining), and repeat on a female character. The change ships with the next client patch.
