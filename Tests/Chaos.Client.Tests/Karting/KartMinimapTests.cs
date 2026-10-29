#region
using Chaos.Client.Systems.Karting;
using Chaos.DarkAges.Karting;
using FluentAssertions;
#endregion

namespace Chaos.Client.Tests.Karting;

public class KartMinimapTests
{
    private static readonly TrackSurface Square = TrackSurface.FromRows(["WWWW", "WRRW", "WRRW", "WWWW"]);

    [Test]
    public async Task Only_drivable_tiles_are_kept()
        => new KartMinimap(Square, 100, 100, 0).Tiles.Should().HaveCount(4);

    [Test]
    public async Task The_road_fills_the_box_and_is_centred()
    {
        var map = new KartMinimap(Square, 100, 100, 5);

        var left = map.Project(1, 3);
        var right = map.Project(3, 1);
        var top = map.Project(1, 1);
        var bottom = map.Project(3, 3);

        left.X.Should().BeApproximately(5, 0.01f);
        right.X.Should().BeApproximately(95, 0.01f);
        ((top.Y + bottom.Y) / 2).Should().BeApproximately(50, 0.01f);
        (bottom.Y - top.Y).Should().BeApproximately(45, 0.01f);
    }
}
