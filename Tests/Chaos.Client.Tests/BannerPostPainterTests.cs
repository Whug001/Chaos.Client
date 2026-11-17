using Chaos.Client.Rendering;
using Chaos.Client.Systems;
using Chaos.Client.Controls.World.Emblems;
using Chaos.DarkAges.Definitions;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public sealed class BannerPostPainterTests
{
    //pixel (3, 5) is colour 1 = (200, 10, 10); every other pixel, (0, 0) included, is see-through
    private static GuildEmblemDesign RedDot()
        => new()
        {
            Colors = [new GuildCloakColor(200, 10, 10)],
            Pixels = Enumerable.Range(0, GuildEmblemProtocol.PIXEL_COUNT)
                               .Select(i => i == (5 * GuildEmblemProtocol.SIZE) + 3 ? (byte)1 : (byte)0)
                               .ToArray()
        };

    private static Color At(Color[] image, int x, int y) => image[(y * BannerPostPainter.WIDTH) + x];

    [Test]
    public void Paint_is_the_declared_size()
        => BannerPostPainter.Paint(RedDot()).Length.Should().Be(BannerPostPainter.WIDTH * BannerPostPainter.HEIGHT);

    [Test]
    public void The_foot_pixel_is_pole_and_the_corners_are_transparent()
    {
        var image = BannerPostPainter.Paint(RedDot());

        At(image, BannerPostPainter.FOOT_X, BannerPostPainter.FOOT_Y).A.Should().Be(255);
        At(image, 0, 0).Should().Be(Color.Transparent);
        At(image, BannerPostPainter.WIDTH - 1, BannerPostPainter.HEIGHT - 1).Should().Be(Color.Transparent);
    }

    [Test]
    public void An_emblem_pixel_fills_its_2x2_block()
    {
        var image = BannerPostPainter.Paint(RedDot());
        var origin = BannerPostPainter.EmblemOrigin;

        for (var dy = 10; dy <= 11; dy++)
            for (var dx = 6; dx <= 7; dx++)
                At(image, origin.X + dx, origin.Y + dy).Should().Be(new Color(200, 10, 10, 255));
    }

    [Test]
    public void Transparent_emblem_pixels_show_the_field()
    {
        var image = BannerPostPainter.Paint(RedDot());
        var origin = BannerPostPainter.EmblemOrigin;

        At(image, origin.X, origin.Y).Should().Be(new Color(90, 4, 4, 255));
    }

    [Test]
    public void Nothing_is_drawn_until_the_design_is_known_then_it_is()
    {
        //an id no other test uses: the emblem store is shared by tests running in parallel
        const int ID = 731_116;

        BannerPostTextures.CanDraw(ID).Should().BeFalse();
        GuildEmblemTextures.Set(ID, RedDot());
        BannerPostTextures.CanDraw(ID).Should().BeTrue();
    }

    [Test]
    public void Clicks_hit_the_pole_and_cloth_but_not_the_air_beside_the_pole()
    {
        BannerPostPainter.Hits(BannerPostPainter.FOOT_X, BannerPostPainter.FOOT_Y).Should().BeTrue();
        BannerPostPainter.Hits(BannerPostPainter.EmblemOrigin.X, BannerPostPainter.EmblemOrigin.Y).Should().BeTrue();
        BannerPostPainter.Hits(2, 80).Should().BeFalse();
        BannerPostPainter.Hits(-1, 20).Should().BeFalse();
    }
}
