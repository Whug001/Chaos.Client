#region
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Builds small pixel-art textures from text grids, one string per row, top to bottom. <c>'X'</c> is opaque white,
///     tinted at draw time. <c>'o'</c> is an opaque dark detail color chosen by the caller (the draw tint multiplies it
///     too). <c>'.'</c> is clear. Also holds the bat and ghost frames the Halloween effects draw.
/// </summary>
public static class SpriteGrid
{
    /// <summary>Bat frames, 19 px wide: wings up, level, down. Played 0, 1, 2, 1 per flap.</summary>
    public static IReadOnlyList<string[]> BatFrames { get; } =
    [
        [
            "X.................X",
            "XX...............XX",
            "XXX.............XXX",
            ".XXX....X.X....XXX.",
            "..XXXX..XXX..XXXX..",
            "...XXXXXXXXXXXXX...",
            ".......XXXXX.......",
            "........XXX........",
            ".........X........."
        ],
        [
            "........X.X........",
            "........XXX........",
            "XXX....XXXXX....XXX",
            "XXXXXXXXXXXXXXXXXXX",
            ".XXXXXXXXXXXXXXXXX.",
            "..X..X..XXX..X..X..",
            "........XXX........",
            ".........X........."
        ],
        [
            "........X.X........",
            "........XXX........",
            ".......XXXXX.......",
            "....XXXXXXXXXXX....",
            "..XXXXXX.XXX.XXXXX.",
            ".XXXX....XXX...XXXX",
            "XXX.......X......XX",
            "XX................X"
        ]
    ];

    /// <summary>Sheet-ghost frames, 11 x 13, differing only in the hem. <c>'o'</c> marks the eyes and mouth.</summary>
    public static IReadOnlyList<string[]> GhostFrames { get; } =
    [
        [
            "....XXX....",
            "..XXXXXXX..",
            ".XXXXXXXXX.",
            ".XXoXXXoXX.",
            ".XXoXXXoXX.",
            "XXXXXXXXXXX",
            "XXXXXoXXXXX",
            "XXXXXXXXXXX",
            "XXXXXXXXXXX",
            "XXXXXXXXXXX",
            "XXXXXXXXXXX",
            "XX.XXX.XXX.",
            "X...X...X.."
        ],
        [
            "....XXX....",
            "..XXXXXXX..",
            ".XXXXXXXXX.",
            ".XXoXXXoXX.",
            ".XXoXXXoXX.",
            "XXXXXXXXXXX",
            "XXXXXoXXXXX",
            "XXXXXXXXXXX",
            "XXXXXXXXXXX",
            "XXXXXXXXXXX",
            "XXXXXXXXXXX",
            ".XXX.XXX.XX",
            "..X...X...X"
        ]
    ];

    /// <summary>
    ///     The grid's pixels, row by row from the top. Throws <see cref="ArgumentException" /> for no rows, rows of
    ///     different or zero width, or a character other than X, o and dot.
    /// </summary>
    public static Color[] ToPixels(IReadOnlyList<string> rows, Color dark)
    {
        if (rows.Count == 0)
            throw new ArgumentException("A sprite needs at least one row.", nameof(rows));

        var width = rows[0].Length;

        if ((width == 0) || rows.Any(row => row.Length != width))
            throw new ArgumentException("Every sprite row must have the same, non-zero width.", nameof(rows));

        var detail = new Color(dark.R, dark.G, dark.B, (byte)255);
        var pixels = new Color[width * rows.Count];

        for (var y = 0; y < rows.Count; y++)
            for (var x = 0; x < width; x++)
                pixels[(y * width) + x] = rows[y][x] switch
                {
                    'X'       => Color.White,
                    'o'       => detail,
                    '.'       => Color.Transparent,
                    var other => throw new ArgumentException($"Unknown sprite character '{other}'.", nameof(rows))
                };

        return pixels;
    }

    /// <summary>Builds the grid as a texture. The caller owns and disposes it.</summary>
    public static Texture2D Build(GraphicsDevice device, IReadOnlyList<string> rows, Color dark)
    {
        var pixels = ToPixels(rows, dark);
        var texture = new Texture2D(device, rows[0].Length, rows.Count);
        texture.SetData(pixels);

        return texture;
    }
}
