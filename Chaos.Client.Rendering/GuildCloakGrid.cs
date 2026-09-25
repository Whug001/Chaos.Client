#region
using DALib.Drawing;
#endregion

namespace Chaos.Client.Rendering;

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

    /// <summary>The last dye slot (see <see cref="DYE_FIRST" />).</summary>
    public const byte DYE_LAST = 103;

    /// <summary>A dye pixel fewer than this many rows above the bottom of its column is hem, not rune.</summary>
    public const int HEM_DEPTH = 3;

    /// <summary>A frame with fewer rune pixels than this has no rune centre.</summary>
    public const int RUNE_MIN_PIXELS = 10;

    /// <summary>A frame showing the inside of the cape has more pixels than this; a collar-only frame has fewer.</summary>
    public const int INSIDE_MIN_PIXELS = 150;

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
    ///     edge). A frame painted onto its own grid reads its own cells. (<paramref name="x" />, <paramref name="y" />) must
    ///     be a filled pixel of <paramref name="target" />, and neither grid may be empty;
    ///     <see cref="GuildCloakPainter.Paint(int, int, byte[], System.Collections.Generic.IList{SkiaSharp.SKColor}, GuildCloakGrid, System.ReadOnlySpan{byte}, System.Collections.Generic.IReadOnlyList{SkiaSharp.SKColor}, bool, bool)" />
    ///     guarantees both.
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
