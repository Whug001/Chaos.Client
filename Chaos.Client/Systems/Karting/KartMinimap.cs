#region
using Chaos.DarkAges.Karting;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Systems.Karting;

/// <summary>
///     The track seen from the screen's angle and shrunk into a box: tile (x, y) sits at (x - y, (x + y) / 2) before
///     scaling, the same diamond layout the map uses. Only drivable tiles (road, boost pads, pits) are kept.
/// </summary>
public sealed class KartMinimap
{
    private readonly double MinX;
    private readonly double MinY;
    private readonly double Scale;
    private readonly Vector2 Offset;

    public KartMinimap(TrackSurface surface, int width, int height, int padding)
    {
        var tiles = new List<Point>();

        for (var y = 0; y < surface.Height; y++)
            for (var x = 0; x < surface.Width; x++)
                if (surface.At(x, y) is SurfaceKind.Road or SurfaceKind.BoostPad or SurfaceKind.Pit)
                    tiles.Add(new Point(x, y));

        if (tiles.Count == 0)
            tiles.Add(Point.Zero);

        var corners = tiles.SelectMany(tile => new[] { Iso(tile.X, tile.Y), Iso(tile.X + 1, tile.Y + 1), Iso(tile.X + 1, tile.Y), Iso(tile.X, tile.Y + 1) })
                           .ToArray();
        MinX = corners.Min(corner => corner.X);
        MinY = corners.Min(corner => corner.Y);
        var spanX = Math.Max(1, corners.Max(corner => corner.X) - MinX);
        var spanY = Math.Max(1, corners.Max(corner => corner.Y) - MinY);
        var innerWidth = width - padding * 2;
        var innerHeight = height - padding * 2;
        Scale = Math.Min(innerWidth / spanX, innerHeight / spanY);
        Offset = new Vector2((float)(padding + (innerWidth - spanX * Scale) / 2), (float)(padding + (innerHeight - spanY * Scale) / 2));
        Tiles = tiles;
    }

    public IReadOnlyList<Point> Tiles { get; }

    /// <summary>Where a track position lands inside the box, in pixels from its top left.</summary>
    public Vector2 Project(double x, double y)
    {
        var (isoX, isoY) = Iso(x, y);

        return Offset + new Vector2((float)((isoX - MinX) * Scale), (float)((isoY - MinY) * Scale));
    }

    private static (double X, double Y) Iso(double x, double y) => (x - y, (x + y) / 2);
}
