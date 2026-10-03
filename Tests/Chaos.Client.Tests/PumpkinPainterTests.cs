using Chaos.Client.Rendering;
using Chaos.DarkAges.Definitions;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public class PumpkinPainterTests
{
    private static readonly Color Skin = new(224, 118, 30, 255);

    [Test]
    public void The_embedded_map_has_a_blank_back_and_a_full_front()
    {
        var map = PumpkinFaceMap.Shared;

        map.FrameCount.Should().Be(2);
        map.For(0).Should().BeEmpty();
        map.For(1).SelectMany(pixel => pixel.Cells).Distinct().Should().HaveCount(PumpkinGrid.CELLS);
        map.For(7).Should().BeEmpty();
    }

    [Test]
    public void A_pixel_glows_only_when_one_of_its_cells_is_cut()
    {
        var map = new[] { new PumpkinFacePixel(1, 0, 0, [0, 1]), new PumpkinFacePixel(2, 0, 0, [5]) };
        var grid = PumpkinGrid.Empty();
        PumpkinGrid.SetCut(grid, 1, 0, true);
        var pixels = Enumerable.Repeat(Skin, 4).ToArray();

        PumpkinPainter.Paint(pixels, 4, 1, 0, 0, map, grid, 0);

        pixels[1].Should().Be(PumpkinPainter.GlowColor(0, 0));
        pixels[2].Should().Be(Skin);
    }

    [Test]
    public void The_frame_offset_is_applied_and_outside_pixels_are_skipped()
    {
        var map = new[] { new PumpkinFacePixel(3, 2, 0, [0]), new PumpkinFacePixel(9, 9, 0, [0]) };
        var grid = PumpkinGrid.Empty();
        PumpkinGrid.SetCut(grid, 0, 0, true);
        var pixels = Enumerable.Repeat(Skin, 4).ToArray();

        PumpkinPainter.Paint(pixels, 2, 2, 2, 1, map, grid, 0);

        pixels[3].Should().Be(PumpkinPainter.GlowColor(0, 0));
        pixels.Count(p => p == Skin).Should().Be(3);
    }

    [Test]
    public void The_glow_matches_the_art_tool_and_dims_toward_the_edge()
    {
        PumpkinPainter.GlowColor(0, 0).Should().Be(new Color(255, 244, 150, 255));
        PumpkinPainter.GlowColor(100, 0).G.Should().BeLessThan(PumpkinPainter.GlowColor(0, 0).G);
        PumpkinPainter.PhaseAt(0).Should().Be(0);
        PumpkinPainter.PhaseAt(PumpkinPainter.FLICKER_MS).Should().Be(1);
        PumpkinPainter.PhaseAt(PumpkinPainter.FLICKER_MS * PumpkinPainter.FLICKER_PHASES).Should().Be(0);
    }

    [Test]
    public void A_carving_shows_cut_pixels_as_dark_holes()
    {
        var map = new[] { new PumpkinFacePixel(1, 0, 0, [0, 1]), new PumpkinFacePixel(2, 0, 100, [5]) };
        var grid = PumpkinGrid.Empty();
        PumpkinGrid.SetCut(grid, 1, 0, true);
        var pixels = Enumerable.Repeat(Skin, 4).ToArray();

        PumpkinPainter.PaintHoles(pixels, 4, 1, 0, 0, map, grid);

        pixels[1].Should().Be(new Color(58, 24, 8, 255));
        pixels[1].Should().Be(PumpkinPainter.HoleColor);
        pixels[2].Should().Be(Skin);
    }
}
