using DALib.Data;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Rendering;

/// <summary>One 28 px wide wall face. Its foot at column c is at <c>BaseWorldY + BaseSlope * c</c>.</summary>
public readonly record struct WallFace(float WorldX, float BaseWorldY, float BaseSlope, int Height);

/// <summary>What ambient effects need to know about a map's tiles, built once per map.</summary>
public sealed class MapSurface
{
    public const int FACE_WIDTH = 28;
    private const int MIN_FACE_HEIGHT = 45;
    private const int VENT_SPACING = 60;
    private const int HALF_TILE_W = 28;
    private const int HALF_TILE_H = 14;

    private readonly bool[,] Floor;
    private readonly int Width;

    public int MapId { get; }
    public int Height { get; }
    public IReadOnlyList<WallFace> WallFaces { get; }
    public IReadOnlyList<Point> SteamVents { get; }

    private MapSurface(int mapId, bool[,] floor, int width, int height, List<WallFace> faces, List<Point> vents)
    {
        MapId = mapId;
        Floor = floor;
        Width = width;
        Height = height;
        WallFaces = faces;
        SteamVents = vents;
    }

    public static MapSurface Build(
        int mapId,
        MapFile map,
        Func<int, bool> isWall,
        Func<int, bool> isDoor,
        Func<int, bool> isBlockedGround,
        Func<int, int> foregroundHeight)
    {
        var width = map.Width;
        var height = map.Height;
        var walls = new bool[width, height];
        var floor = new bool[width, height];
        var vents = new List<Point>();

        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var tile = map.Tiles[x, y];
                var door = isDoor(tile.LeftForeground) || isDoor(tile.RightForeground);
                var wall = !door && (isWall(tile.LeftForeground) || isWall(tile.RightForeground));

                walls[x, y] = wall;
                floor[x, y] = !door && !wall && !isBlockedGround(tile.Background);

                if (floor[x, y] && (StableHash(mapId, x, y) % VENT_SPACING == 0))
                    vents.Add(new Point(x, y));
            }

        bool WallAt(int x, int y) => (x >= 0) && (y >= 0) && (x < width) && (y < height) && walls[x, y];

        var faces = new List<WallFace>();

        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                if (!walls[x, y] || !(WallAt(x - 1, y) || WallAt(x + 1, y) || WallAt(x, y - 1) || WallAt(x, y + 1)))
                    continue;

                var tile = map.Tiles[x, y];
                var world = Camera.TileToWorld(x, y, height);

                if (isWall(tile.LeftForeground) && !WallAt(x, y + 1) && (foregroundHeight(tile.LeftForeground) >= MIN_FACE_HEIGHT))
                    faces.Add(new WallFace(world.X, world.Y + 14, 0.5f, foregroundHeight(tile.LeftForeground)));

                if (isWall(tile.RightForeground) && !WallAt(x + 1, y) && (foregroundHeight(tile.RightForeground) >= MIN_FACE_HEIGHT))
                    faces.Add(new WallFace(world.X + FACE_WIDTH, world.Y + 28, -0.5f, foregroundHeight(tile.RightForeground)));
            }

        return new MapSurface(mapId, floor, width, height, faces, vents);
    }

    public bool IsFloor(int tileX, int tileY)
        => (tileX >= 0) && (tileY >= 0) && (tileX < Width) && (tileY < Height) && Floor[tileX, tileY];

    public bool IsFloorAtWorld(float worldX, float worldY)
    {
        var xMinusY = ((worldX - HALF_TILE_W) / HALF_TILE_W) - (Height - 1);
        var xPlusY = (worldY - HALF_TILE_H) / HALF_TILE_H;

        return IsFloor((int)MathF.Round((xPlusY + xMinusY) / 2f), (int)MathF.Round((xPlusY - xMinusY) / 2f));
    }

    private static uint StableHash(int mapId, int x, int y)
    {
        unchecked
        {
            var hash = ((uint)mapId * 73856093u) ^ ((uint)x * 19349663u) ^ ((uint)y * 83492791u);
            hash ^= hash >> 15;
            hash *= 2246822519u;
            hash ^= hash >> 13;

            return hash;
        }
    }
}
