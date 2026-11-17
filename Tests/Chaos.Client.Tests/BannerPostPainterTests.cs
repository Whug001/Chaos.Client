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
        var block = BannerPostPainter.EmblemBlock(3, 5);

                for (var dy = 0; dy <= 1; dy++)
            for (var dx = 0; dx <= 1; dx++)
            {
                var pixel = At(image, block.X + dx, block.Y + dy);
                //paint on cloth is a touch duller than the design, but red still dominates
                pixel.R.Should().BeGreaterThan(150);
                pixel.G.Should().BeLessThan((byte)(pixel.R / 4));
                pixel.B.Should().BeLessThan((byte)(pixel.R / 4));
            }
    }

    [Test]
    public void The_cloth_is_sheared_along_the_iso_axis()
    {
        var first = BannerPostPainter.EmblemBlock(0, 0);
        var next = BannerPostPainter.EmblemBlock(1, 0);

        next.X.Should().Be(first.X + 2);
        next.Y.Should().Be(first.Y - 1);
    }

    [Test]
    public void Transparent_emblem_pixels_show_the_field()
    {
        var image = BannerPostPainter.Paint(RedDot());
        var origin = BannerPostPainter.EmblemOrigin;

        //the field is (90, 4, 4) = (200, 10, 10) x 0.45, lit a little on the cloth's left side
        var pixel = At(image, origin.X, origin.Y);
        pixel.R.Should().BeInRange(80, 110);
        pixel.G.Should().BeLessThan(10);
        pixel.B.Should().BeLessThan(10);
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
