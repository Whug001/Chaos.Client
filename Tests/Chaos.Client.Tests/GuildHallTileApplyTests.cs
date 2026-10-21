using Chaos.Client.Systems;
using Chaos.Networking.Entities.Server;
using DALib.Data;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class GuildHallTileApplyTests
{
    [Test]
    public async Task Writes_each_tile_as_signed_shorts()
    {
        var map = new MapFile(100, 100);

        var applied = GuildHallTileApply.Apply(map, [new GuildHallTileInfo(4, 7, 11, 500, 40000), new GuildHallTileInfo(99, 99, 1, 0, 0)]);

        applied.Should().Be(2);
        map.Tiles[4, 7].Background.Should().Be(11);
        map.Tiles[4, 7].LeftForeground.Should().Be(500);
        map.Tiles[4, 7].RightForeground.Should().Be(unchecked((short)40000));
        map.Tiles[99, 99].Background.Should().Be(1);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Skips_tiles_outside_the_map()
    {
        var map = new MapFile(64, 64);

        GuildHallTileApply.Apply(map, [new GuildHallTileInfo(64, 0, 1, 0, 0), new GuildHallTileInfo(0, 200, 1, 0, 0)]).Should().Be(0);

        await Task.CompletedTask;
    }
}
