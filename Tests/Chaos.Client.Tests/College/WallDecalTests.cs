using Chaos.Client.Rendering;
using Chaos.DarkAges.Definitions;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests.College;

public class WallDecalTests
{
    private static Color[] Numbered(int width, int height)
        => Enumerable.Range(0, width * height).Select(i => new Color(i % width, i / width, 0, 255)).ToArray();

    [Test]
    public void The_spots_match_the_room_layout()
    {
        WallDecalGeometry.Frames.Should().HaveCount(CollegeProtocol.WALL_SLOTS);
        WallDecalGeometry.Frames[0].TileOf(0).Should().Be((1, 0));
        WallDecalGeometry.Frames[1].TileOf(3).Should().Be((18, 0));
        WallDecalGeometry.Frames[2].TileOf(0).Should().Be((0, 4));
        WallDecalGeometry.Frames[2].TileOf(3).Should().Be((0, 1));
        WallDecalGeometry.Frames[5].TileOf(0).Should().Be((0, 19));
        WallDecalGeometry.Board.TileOf(0).Should().Be((7, 0));
        WallDecalGeometry.Board.Width.Should().Be(WallDecalGeometry.BOARD_IMAGE_WIDTH);
    }

    [Test]
    public void A_north_strip_drops_to_the_right_and_a_west_strip_rises()
    {
        WallDecalGeometry.StripRow(MirrorSide.North, 0, 0).Should().Be(0);
        WallDecalGeometry.StripRow(MirrorSide.North, 27, 0).Should().Be(13);
        WallDecalGeometry.StripRow(MirrorSide.West, 0, 0).Should().Be(13);
        WallDecalGeometry.StripRow(MirrorSide.West, 27, 0).Should().Be(0);
        WallDecalGeometry.StripHeight(88).Should().Be(101);
    }

    [Test]
    public void The_picture_bottom_sits_just_above_the_wall_base()
    {
        var top = WallDecalGeometry.StripTop(88);

        (top + WallDecalGeometry.StripRow(MirrorSide.North, 0, 87)).Should().Be(MirrorGeometry.BaseRow(MirrorSide.North, 0) - WallDecalGeometry.BOTTOM_GAP);
        (top + WallDecalGeometry.StripRow(MirrorSide.West, 27, 87)).Should().Be(MirrorGeometry.BaseRow(MirrorSide.West, 27) - WallDecalGeometry.BOTTOM_GAP);
    }

    [Test]
    public void Strips_cut_the_picture_left_to_right_on_screen()
    {
        var spot = WallDecalGeometry.Frames[2];
        var image = Numbered(spot.Width, 88);

        var strips = WallDecalPainter.Strips(image, 88, spot);

        strips.Should().HaveCount(4);
        strips[0].Should().HaveCount(28 * 101);
        strips[0][13 * 28 + 0].Should().Be(image[0]);
        strips[0][0 * 28 + 27].Should().Be(image[27]);
        strips[1][13 * 28 + 0].Should().Be(image[28]);
        strips[0][0].Should().Be(default(Color));
    }

    [Test]
    public void A_framed_picture_has_wood_round_the_drawing()
    {
        var palette = Enumerable.Range(0, 32).Select(i => new Color(i * 8, 0, 0, 255)).ToArray();
        var pixels = new byte[CollegeProtocol.DRAWING_PIXELS];
        pixels[0] = 5;

        var image = WallDecalPainter.Framed(palette, pixels);

        image.Should().HaveCount(112 * 88);
        image[0].Should().Be(WallDecalPainter.FrameOuter);
        image[5 * 112 + 5].Should().Be(WallDecalPainter.FrameInner);
        image[7 * 112 + 7].Should().Be(WallDecalPainter.FrameLine);
        image[8 * 112 + 8].Should().Be(palette[5]);
        image[8 * 112 + 9].Should().Be(palette[0]);
    }

    [Test]
    public void The_board_shows_the_pane_where_nothing_is_drawn()
    {
        var palette = Enumerable.Range(0, 32).Select(i => new Color(0, i * 8, 0, 255)).ToArray();
        var pixels = new byte[CollegeProtocol.BOARD_PIXELS];
        pixels[1] = 9;

        var image = WallDecalPainter.Board(palette, pixels);
        var blank = WallDecalPainter.Board(null, null);

        image.Should().HaveCount(168 * 88);
        image[0].Should().Be(WallDecalPainter.PaneEdge);
        image[4 * 168 + 4].Should().Be(WallDecalPainter.PaneFill);
        image[4 * 168 + 5].Should().Be(palette[9]);
        blank[40 * 168 + 80].Should().Be(WallDecalPainter.PaneFill);
    }

    [Test]
    public void The_shimmer_band_drifts_across_and_repeats()
    {
        var peak = WallDecalPainter.ShimmerIntensity(1.5 / 8 * 6, 0, 6);

        peak.Should().BeApproximately(0.3f, 0.001f);
        WallDecalPainter.ShimmerIntensity(1.5 / 8 * 6, 2, 6).Should().Be(0);
        WallDecalPainter.ShimmerIntensity((1.5 / 8 * 6) + 6, 0, 6).Should().BeApproximately(peak, 0.001f);
    }
}
