using Chaos.Client.Rendering;
using DALib.Data;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public class MapSurfaceTests
{
    private const int WALL = 100;
    private const int DOOR = 200;
    private const int WATER = 5;
    private const int FLOOR = 1;

    private static MapSurface Build(MapFile map, int height = 60)
        => MapSurface.Build(7, map, id => id == WALL, id => id == DOOR, id => id == WATER, _ => height);

    private static MapFile Floors(int width = 8, int height = 8)
    {
        var map = new MapFile(width, height);

        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                map.Tiles[x, y] = new MapTile { Background = FLOOR };

        return map;
    }

    private static void Wall(MapFile map, int x, int y)
        => map.Tiles[x, y] = new MapTile { Background = FLOOR, LeftForeground = WALL, RightForeground = WALL };

    [Test]
    public void LoneWall_GivesNoFaces()
    {
        var map = Floors();
        Wall(map, 3, 3);

        Build(map).WallFaces.Should().BeEmpty();
    }

    [Test]
    public void TwoTileRun_SkipsTheFaceHiddenByTheNextWall()
    {
        var map = Floors();
        Wall(map, 3, 3);
        Wall(map, 4, 3);

        Build(map).WallFaces.Should().HaveCount(3);
    }

    [Test]
    public void ShortSprites_GiveNoFaces()
    {
        var map = Floors();
        Wall(map, 3, 3);
        Wall(map, 4, 3);

        Build(map, 44).WallFaces.Should().BeEmpty();
    }

    [Test]
    public void DoorNextToWall_DoesNotMakeARun()
    {
        var map = Floors();
        Wall(map, 3, 3);
        map.Tiles[4, 3] = new MapTile { Background = FLOOR, LeftForeground = DOOR };

        var surface = Build(map);

        surface.WallFaces.Should().BeEmpty();
        surface.IsFloor(4, 3).Should().BeFalse();
    }

    [Test]
    public void Floor_ExcludesWallsAndBlockedGround()
    {
        var map = Floors();
        Wall(map, 1, 1);
        map.Tiles[2, 2] = new MapTile { Background = WATER };

        var surface = Build(map);

        surface.IsFloor(1, 1).Should().BeFalse();
        surface.IsFloor(2, 2).Should().BeFalse();
        surface.IsFloor(3, 3).Should().BeTrue();
        surface.IsFloor(-1, 0).Should().BeFalse();
        surface.IsFloor(8, 0).Should().BeFalse();
    }

    [Test]
    public void IsFloorAtWorld_ReadsTheTileUnderItsCenter()
    {
        var map = Floors();
        Wall(map, 2, 5);
        var surface = Build(map);

        var wallCenter = Camera.TileToWorld(2, 5, 8) + new Vector2(28, 14);
        var floorCenter = Camera.TileToWorld(5, 2, 8) + new Vector2(28, 14);

        surface.IsFloorAtWorld(wallCenter.X, wallCenter.Y).Should().BeFalse();
        surface.IsFloorAtWorld(floorCenter.X, floorCenter.Y).Should().BeTrue();
    }

    [Test]
    public void SteamVents_AreStableAcrossBuilds()
    {
        var first = Build(Floors(64, 64)).SteamVents;
        var second = Build(Floors(64, 64)).SteamVents;

        first.Should().NotBeEmpty();
        first.Should().Equal(second);
    }

    [Test]
    public void AllWallMap_HasNoFloorOrVents()
    {
        var map = Floors(4, 4);

        for (var y = 0; y < 4; y++)
            for (var x = 0; x < 4; x++)
                Wall(map, x, y);

        var surface = Build(map);

        surface.SteamVents.Should().BeEmpty();
        surface.IsFloor(1, 1).Should().BeFalse();
    }
}
