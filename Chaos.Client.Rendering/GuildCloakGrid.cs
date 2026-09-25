#region
using DALib.Drawing;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     The outline of one cloak frame: which pixels it has, and the first and last pixel of each row. Coordinates are local
///     to the frame (0,0 is its top-left). Built for the three reference frames the design grids are painted on, and for
///     every frame the painter maps a design onto.
/// </summary>
public sealed class GuildCloakGrid
{
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
    }

    public int BottomRow { get; }
    public int Height { get; }
    public bool IsEmpty => TopRow < 0;
    public int TopRow { get; }
    public int Width { get; }

    /// <summary>
    ///     The pixel at fractions (<paramref name="u" />, <paramref name="v" />) of this outline: v picks the row between the
    ///     top and bottom rows (an empty row moves to the nearest filled row), u picks the column between that row's first and
    ///     last pixel.
    /// </summary>
    public (int X, int Y) CellAt(float u, float v)
    {
        var y = (int)MathF.Round(TopRow + (v * (BottomRow - TopRow)));

        for (var d = 0; d <= BottomRow - TopRow; d++)
        {
            if ((y + d <= BottomRow) && (RowFirst[y + d] >= 0))
            {
                y += d;

                break;
            }

            if ((y - d >= TopRow) && (RowFirst[y - d] >= 0))
            {
                y -= d;

                break;
            }
        }

        var x = (int)MathF.Round(RowFirst[y] + (u * (RowLast[y] - RowFirst[y])));

        return (x, y);
    }

    public static GuildCloakGrid FromFrame(EpfFrame frame) => new(frame.PixelWidth, frame.PixelHeight, frame.Data);

    public static bool HasPixels(EpfFrame frame)
        => (frame.PixelWidth > 0) && (frame.PixelHeight > 0) && frame.Data.AsSpan().ContainsAnyExcept((byte)0);

    public bool IsFilled(int x, int y) => (x >= 0) && (y >= 0) && (x < Width) && (y < Height) && Filled[(y * Width) + x];

    /// <summary>The first and last pixel of a row, or (-1, -1) for an empty row.</summary>
    public (int First, int Last) Row(int y) => (RowFirst[y], RowLast[y]);
}
