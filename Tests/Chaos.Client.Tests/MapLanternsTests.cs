using Chaos.Client.Systems;
using DALib.Data;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public class MapLanternsTests
{
    private static MapFile Map()
    {
        var map = new MapFile(10, 10);

        for (var y = 0; y < 10; y++)
            for (var x = 0; x < 10; x++)
                map.Tiles[x, y] = new MapTile();

        return map;
    }

    private static void Left(MapFile map, int x, int y, short id) => map.Tiles[x, y].LeftForeground = id;

    private static void Right(MapFile map, int x, int y, short id) => map.Tiles[x, y].RightForeground = id;

    [Test]
    public void A_north_wall_pair_lights_between_its_halves_and_into_the_room()
    {
        var map = Map();
        Left(map, 4, 2, 17171);
        Left(map, 5, 2, 17172);

        MapLanterns.Find(map)
                   .Should()
                   .Equal(new Vector2(4.5f, 2.5f));
    }

    [Test]
    public void A_west_wall_pair_stacked_upside_down_still_counts()
    {
        //in the Veltain maps the second half sits above the first
        var map = Map();
        Right(map, 1, 6, 17149);
        Right(map, 1, 5, 17150);

        MapLanterns.Find(map)
                   .Should()
                   .Equal(new Vector2(1.5f, 5.5f));
    }

    [Test]
    public void A_standing_lantern_with_both_halves_on_one_tile_lights_from_that_tile()
    {
        var map = Map();
        Left(map, 3, 7, 2071);
        Right(map, 3, 7, 2072);

        MapLanterns.Find(map)
                   .Should()
                   .Equal(new Vector2(3f, 7f));
    }

    [Test]
    public void A_lone_half_or_halves_in_different_slots_give_no_light()
    {
        var map = Map();
        Left(map, 2, 2, 17157);
        Left(map, 6, 6, 17163);
        Right(map, 7, 6, 17164);

        MapLanterns.Find(map)
                   .Should()
                   .BeEmpty();
    }
}
