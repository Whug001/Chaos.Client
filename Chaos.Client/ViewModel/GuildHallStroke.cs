using Microsoft.Xna.Framework;

namespace Chaos.Client.ViewModel;

/// <summary>Tile math for the hall editor's drag tools. No graphics, so it is testable.</summary>
public static class GuildHallStroke
{
    /// <summary>
    ///     Every tile from <paramref name="from" /> to <paramref name="to" /> (Bresenham, as GuildEmblemCanvas.PaintLine),
    ///     without <paramref name="from" />, which the previous step already sent.
    /// </summary>
    public static List<Point> Line(Point from, Point to)
    {
        var tiles = new List<Point>();
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

            tiles.Add(new Point(x, y));
        }

        return tiles;
    }

    public static (Point Min, Point Max) Normalize(Point a, Point b)
        => (new Point(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y)), new Point(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y)));

    /// <summary>The edge tiles of the inclusive rectangle between two corners, each once.</summary>
    public static List<Point> Perimeter(Point a, Point b)
    {
        (var min, var max) = Normalize(a, b);
        var tiles = new List<Point>();

        for (var x = min.X; x <= max.X; x++)
        {
            tiles.Add(new Point(x, min.Y));

            if (max.Y != min.Y)
                tiles.Add(new Point(x, max.Y));
        }

        for (var y = min.Y + 1; y < max.Y; y++)
        {
            tiles.Add(new Point(min.X, y));

            if (max.X != min.X)
                tiles.Add(new Point(max.X, y));
        }

        return tiles;
    }
}
