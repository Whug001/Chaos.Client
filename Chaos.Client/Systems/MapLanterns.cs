#region
using DALib.Data;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Systems;

/// <summary>
///     Wall lanterns drawn into the map itself. Each lantern is two foreground pieces, on one tile or on neighbouring tiles; where a
///     map has both halves together, the lantern lights the floor around it on a dark map the way a player's lantern
///     does, only wider.
/// </summary>
public static class MapLanterns
{
    /// <summary>How far a wall lantern's light reaches, in tiles. A player's large lantern reaches 5.</summary>
    public const int RADIUS_TILES = 9;

    /// <summary>
    ///     The foreground pairs that make one lantern, by tile id. Either half can sit to the east, south, west or north
    ///     of the other: a lantern on a north wall is two pieces side by side, one on a west wall is two pieces stacked. A standing lantern has both on one tile, first in the left slot.
    /// </summary>
    private static readonly (short First, short Second)[] Pairs =
    [
        (17149, 17150),
        (17157, 17158),
        (17163, 17164),
        (17171, 17172),
        (2071, 2072),
        (2074, 2075),
        (2077, 2078),
        (2080, 2081),
        (2083, 2084),
        (2086, 2087),
        (2089, 2090),
        (2092, 2093),
        (2095, 2096),
        (2098, 2099),
        (2101, 2102),
        (2104, 2105),
        (2107, 2108),
        (2110, 2111),
        (2113, 2114),
        (2116, 2117)
    ];

    private static readonly (int Dx, int Dy)[] Neighbours = [(1, 0), (0, 1), (-1, 0), (0, -1)];

    /// <summary>
    ///     Every lantern on the map, as the tile its light is centred on: halfway between its two pieces, half a tile
    ///     out from the wall into the room.
    /// </summary>
    public static Vector2[] Find(MapFile mapFile)
    {
        var found = new List<Vector2>();

        for (var y = 0; y < mapFile.Height; y++)
            for (var x = 0; x < mapFile.Width; x++)
            {
                var tile = mapFile.Tiles[x, y];

                //a standing lantern has both halves on one tile, first on the left and second on the right, and lights
                //from where it stands
                if (IsWholeOnOneTile(tile.LeftForeground, tile.RightForeground))
                {
                    found.Add(new Vector2(x, y));

                    continue;
                }

                //the left slot holds north-wall pieces (the room is to the south), the right slot west-wall pieces
                //(the room is to the east). Both halves of a lantern are always in the same slot
                if (FindPair(mapFile, x, y, tile.LeftForeground, left: true) is { } north)
                    found.Add(north + new Vector2(0f, 0.5f));

                if (FindPair(mapFile, x, y, tile.RightForeground, left: false) is { } west)
                    found.Add(west + new Vector2(0.5f, 0f));
            }

        return found.ToArray();
    }

    /// <summary>
    ///     The midpoint of the lantern whose first half is at (x, y), or null when the piece there is not a first half
    ///     or its second half is missing. Only first halves are matched, so a lantern is never counted twice.
    /// </summary>
    private static bool IsWholeOnOneTile(short left, short right)
    {
        foreach ((var first, var second) in Pairs)
            if ((left == first) && (right == second))
                return true;

        return false;
    }

    private static Vector2? FindPair(MapFile mapFile, int x, int y, short id, bool left)
    {
        foreach ((var first, var second) in Pairs)
        {
            if (id != first)
                continue;

            foreach ((var dx, var dy) in Neighbours)
            {
                var nx = x + dx;
                var ny = y + dy;

                if ((nx < 0) || (ny < 0) || (nx >= mapFile.Width) || (ny >= mapFile.Height))
                    continue;

                var neighbour = mapFile.Tiles[nx, ny];

                if ((left ? neighbour.LeftForeground : neighbour.RightForeground) == second)
                    return new Vector2(x + (dx / 2f), y + (dy / 2f));
            }
        }

        return null;
    }
}
