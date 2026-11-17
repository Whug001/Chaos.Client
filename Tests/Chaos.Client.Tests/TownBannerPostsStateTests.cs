using Chaos.Client.ViewModel;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
using GeoPoint = Chaos.Geometry.Point;

namespace Chaos.Client.Tests;

public sealed class TownBannerPostsStateTests
{
    private static TownBannerPostsArgs Banner(int id, params (int X, int Y)[] posts)
        => new()
        {
            DesignId = id,
            TownName = "Mileth",
            Designer = "Ayla",
            Posts = posts.Select(p => new GeoPoint(p.X, p.Y)).ToList()
        };

    [Test]
    public void Blocks_only_listed_tiles_while_a_banner_exists()
    {
        var state = new TownBannerPostsState();

        state.Set(Banner(5, (3, 4)));
        state.Blocks(3, 4).Should().BeTrue();
        state.Blocks(4, 4).Should().BeFalse();

        state.Set(Banner(0, (3, 4)));
        state.Blocks(3, 4).Should().BeFalse();
    }

    [Test]
    public void A_new_map_id_clears_the_posts_and_a_refresh_keeps_them()
    {
        var state = new TownBannerPostsState();

        state.OnMapInfo(397);
        state.Set(Banner(5, (3, 4)));

        state.OnMapInfo(397);
        state.Blocks(3, 4).Should().BeTrue();

        state.OnMapInfo(772);
        state.Blocks(3, 4).Should().BeFalse();
        state.DesignId.Should().Be(0);
    }

    [Test]
    public void BlockedPoints_is_empty_without_a_banner()
    {
        var state = new TownBannerPostsState();

        state.Set(Banner(0, (3, 4)));
        state.BlockedPoints.Should().BeEmpty();

        state.Set(Banner(5, (3, 4)));
        state.BlockedPoints.Select(p => (p.X, p.Y)).Should().Equal((3, 4));
    }
}
