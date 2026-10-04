using Chaos.Client.Controls.World.Popups.Beauty;
using Chaos.Client.Rendering;
using FluentAssertions;
using Microsoft.Xna.Framework;
using SkiaSharp;

namespace Chaos.Client.Tests;

public class MirrorBackdropTests
{
    [Test]
    public async Task Backdrops_run_plain_then_the_five_places()
    {
        MirrorBackdrops.All.Should()
                       .Equal(
                           MirrorBackdrop.Plain,
                           MirrorBackdrop.Mileth,
                           MirrorBackdrop.Woodlands,
                           MirrorBackdrop.Beach,
                           MirrorBackdrop.FrozenCave,
                           MirrorBackdrop.Crypt);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Names_are_short_ascii_words()
    {
        MirrorBackdrops.All.Select(MirrorBackdrops.Name).Should().Equal("Plain", "Mileth", "Woodlands", "Beach", "Frozen Cave", "Crypt");
        MirrorBackdrops.All.Select(MirrorBackdrops.Name).Should().OnlyContain(name => name.All(c => c < 128));

        await Task.CompletedTask;
    }

    [Test]
    public async Task Only_plain_has_no_picture()
    {
        MirrorBackdrops.Key(MirrorBackdrop.Plain).Should().BeNull();
        MirrorBackdrops.All.Skip(1).Select(MirrorBackdrops.Key).Should().Equal("mileth", "woodlands", "beach", "frozencave", "crypt");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Every_place_has_its_embedded_pictures()
    {
        foreach (var key in MirrorBackdrops.All.Select(MirrorBackdrops.Key).OfType<string>())
        {
            using (var stream = MirrorBackdropAssets.Open(MirrorBackdropAssets.ResourceName(key)))
            using (var picture = SKBitmap.Decode(stream))
            {
                picture.Width.Should().Be(MirrorBackdrops.SOURCE_WIDTH);
                picture.Height.Should().Be(MirrorBackdrops.SOURCE_HEIGHT);
            }

            using (var stream = MirrorBackdropAssets.Open(MirrorBackdropAssets.ThumbnailResourceName(key)))
            using (var thumbnail = SKBitmap.Decode(stream))
            {
                thumbnail.Width.Should().Be(MirrorBackdrops.THUMBNAIL_SIZE);
                thumbnail.Height.Should().Be(MirrorBackdrops.THUMBNAIL_SIZE);
            }
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task A_missing_picture_names_itself()
    {
        var open = () => MirrorBackdropAssets.Open("mirrorbackdrop.nowhere.png");

        open.Should().Throw<InvalidOperationException>().WithMessage("*mirrorbackdrop.nowhere.png*");

        await Task.CompletedTask;
    }

    [Test]
    public async Task The_feet_sit_28_pixels_below_the_centre()
    {
        MirrorBackdrops.FEET_BELOW_CENTER.Should().Be(28);

        await Task.CompletedTask;
    }

    [Test]
    [Arguments(226, 246, 1, 113, 151)]
    [Arguments(226, 246, 2, 113, 179)]
    [Arguments(256, 156, 1, 128, 106)]
    public async Task The_standing_tile_shows_under_the_feet(int width, int height, int scale, int feetX, int feetY)
    {
        MirrorBackdrops.SourcePixel(feetX, feetY, width, height, scale)
                       .Should()
                       .Be(new Point(MirrorBackdrops.SOURCE_ANCHOR_X, MirrorBackdrops.SOURCE_ANCHOR_Y));

        await Task.CompletedTask;
    }

    [Test]
    public async Task At_2x_each_picture_pixel_covers_two_box_pixels()
    {
        MirrorBackdrops.SourcePixel(112, 178, 226, 246, 2).Should().Be(new Point(129, 152));
        MirrorBackdrops.SourcePixel(114, 180, 226, 246, 2).Should().Be(new Point(130, 153));
        MirrorBackdrops.SourcePixel(115, 181, 226, 246, 2).Should().Be(new Point(131, 154));

        await Task.CompletedTask;
    }

    [Test]
    [Arguments(226, 246, 1, 17, 2, 242, 247)]
    [Arguments(226, 246, 2, 73, 63, 186, 186)]
    [Arguments(256, 156, 1, 2, 47, 257, 202)]
    public async Task Box_corners_stay_inside_the_picture(int width, int height, int scale, int left, int top, int right, int bottom)
    {
        MirrorBackdrops.SourcePixel(0, 0, width, height, scale).Should().Be(new Point(left, top));
        MirrorBackdrops.SourcePixel(width - 1, height - 1, width, height, scale).Should().Be(new Point(right, bottom));
        right.Should().BeLessThan(MirrorBackdrops.SOURCE_WIDTH);
        bottom.Should().BeLessThan(MirrorBackdrops.SOURCE_HEIGHT);

        await Task.CompletedTask;
    }
}
