using Microsoft.Xna.Framework;

namespace Chaos.Client.Systems.Cinema;

/// <summary>
///     The film's recess on the Loures Cinema's right-hand back wall (y = 0), in world pixels of the 16-high room.
///     <see cref="Bottom" /> is the bottom edge of the leftmost 2 px column; each column to its right is 1 px lower, as the
///     wall slopes.
/// </summary>
public readonly record struct CinemaWallSpot(int Left, int Bottom, int Width, int Height)
{
    public int Columns => Width / CinemaScreenLayout.COLUMN_WIDTH;
}

/// <summary>One 2 px column of the wall screen: the dark back, and the part of the film drawn into it (empty in a dark bar).</summary>
public readonly record struct CinemaWallColumn(int Cell, Rectangle Back, Rectangle Picture, Rectangle Source);

/// <summary>Where the film sits on the Loures Cinema wall, and picture fitting.</summary>
public static class CinemaScreenLayout
{
    public const int ROOM_MAP_ID = CinemaSeats.MAP_ID;
    public const int VIDEO_WIDTH = 854;
    public const int VIDEO_HEIGHT = 480;

    public const int COLUMN_WIDTH = 2;

    /// <summary>Alpha of the black Loures Cinema house dim; the film on the wall stays at full brightness.</summary>
    public const float CINEMA_HOUSE_DARKNESS = 0.6f;

    //world X of wall cell 0's left edge on the 16-high room: (16 - 1) * 28
    public const int WALL_ROW_LEFT = 420;

    public static CinemaWallSpot Wall { get; } = new(Left: 524, Bottom: 34, Width: 296, Height: 166);

    public static int WallCell(int worldX) => (worldX - WALL_ROW_LEFT) / 28;

    //drawn after the cell's own stripe, so its wall strip lies under the film and people standing at y = 1 cover it
    public static int StripeOf(int cell) => cell + 1;

    /// <summary>The largest rectangle with the picture's shape that fits centred in <paramref name="area" />.</summary>
    public static Rectangle FitPicture(Rectangle area, int pictureWidth, int pictureHeight)
    {
        if ((long)area.Width * pictureHeight > (long)area.Height * pictureWidth)
        {
            var width = area.Height * pictureWidth / pictureHeight;

            return new Rectangle(area.X + (area.Width - width) / 2, area.Y, width, area.Height);
        }

        var height = area.Width * pictureHeight / pictureWidth;

        return new Rectangle(area.X, area.Y + (area.Height - height) / 2, area.Width, height);
    }

    /// <summary>
    ///     The stripe to draw the screen in. The picture hangs above its anchor, so it can be on screen while the anchor
    ///     tile's stripe is not; then it is drawn in the nearest visible stripe.
    /// </summary>
    public static int DrawStripe(int spotDepth, int minDepth, int maxDepth) => Math.Clamp(spotDepth, minDepth, maxDepth);

    /// <summary>
    ///     House-dim rectangles covering <paramref name="viewport" /> except the screen <paramref name="columns" />:
    ///     one left of the first column, one right of the last column, and above and below each column. Everything
    ///     is clipped to the viewport and empty rectangles are dropped, so columns partly or wholly outside the
    ///     viewport still work.
    /// </summary>
    /// <param name="columns">
    ///     Vertical hole columns in screen space, all the same width, side by side from left to right, each
    ///     possibly a different Y (the slanted wall screen is 1 px lower per column).
    /// </param>
    public static IReadOnlyList<Rectangle> DimAroundColumns(Rectangle viewport, IReadOnlyList<Rectangle> columns)
    {
        var rects = new List<Rectangle>(2 + 2 * columns.Count);

        if (columns.Count == 0)
        {
            rects.Add(viewport);

            return rects;
        }

        var first = columns[0];
        var last = columns[columns.Count - 1];

        AddClipped(rects, viewport, new Rectangle(viewport.X, viewport.Y, first.X - viewport.X, viewport.Height));
        AddClipped(rects, viewport, new Rectangle(last.Right, viewport.Y, viewport.Right - last.Right, viewport.Height));

        foreach (var column in columns)
        {
            AddClipped(rects, viewport, new Rectangle(column.X, viewport.Y, column.Width, column.Y - viewport.Y));
            AddClipped(rects, viewport, new Rectangle(column.X, column.Bottom, column.Width, viewport.Bottom - column.Bottom));
        }

        return rects;
    }

    private static void AddClipped(List<Rectangle> rects, Rectangle viewport, Rectangle candidate)
    {
        var clipped = Rectangle.Intersect(viewport, candidate);

        if (clipped != Rectangle.Empty)
            rects.Add(clipped);
    }

    /// <summary>
    ///     Cuts <paramref name="spot" /> into 2 px columns, each 1 px lower than the one to its left, with the picture fitted
    ///     into the unslanted recess by <see cref="FitPicture" />.
    /// </summary>
    public static IReadOnlyList<CinemaWallColumn> WallColumns(CinemaWallSpot spot, int pictureWidth, int pictureHeight)
    {
        var fit = FitPicture(new Rectangle(0, 0, spot.Width, spot.Height), pictureWidth, pictureHeight);
        var columns = new CinemaWallColumn[spot.Columns];

        for (var k = 0; k < columns.Length; k++)
        {
            var localX = k * COLUMN_WIDTH;
            var top = spot.Bottom - spot.Height + k;
            var back = new Rectangle(spot.Left + localX, top, COLUMN_WIDTH, spot.Height);
            var start = Math.Max(localX, fit.X);
            var end = Math.Min(localX + COLUMN_WIDTH, fit.Right);

            if (end <= start)
            {
                columns[k] = new CinemaWallColumn(WallCell(back.X), back, Rectangle.Empty, Rectangle.Empty);

                continue;
            }

            var sourceX = (start - fit.X) * pictureWidth / fit.Width;
            var sourceRight = (end - fit.X) * pictureWidth / fit.Width;

            columns[k] = new CinemaWallColumn(
                WallCell(back.X),
                back,
                new Rectangle(spot.Left + start, top + fit.Y, end - start, fit.Height),
                new Rectangle(sourceX, 0, sourceRight - sourceX, pictureHeight));
        }

        return columns;
    }
}
