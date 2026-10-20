using Chaos.Client.Rendering;
using FluentAssertions;
using SkiaSharp;

namespace Chaos.Client.Tests;

public class OmbreHairPainterTests
{
    private static readonly SKColor Root = new(0, 0, 0);
    private static readonly SKColor Tip = new(200, 100, 50);

    private static SKColor[] Palette(SKColor dye)
    {
        var palette = Enumerable.Repeat(new SKColor(99, 99, 99), 256).ToArray();

        for (var i = 98; i <= 103; i++)
            palette[i] = dye;

        return palette;
    }

    private static byte[] Solid(int width, int height, byte value) => Enumerable.Repeat(value, width * height).ToArray();

    [Test]
    public async Task Blend_runs_from_root_to_tip()
    {
        OmbreHairPainter.Blend(Root, Tip, 10, 10, 20).Should().Be(Root);
        OmbreHairPainter.Blend(Root, Tip, 20, 10, 20).Should().Be(Tip);
        OmbreHairPainter.Blend(Root, Tip, 15, 10, 20).Should().Be(new SKColor(100, 50, 25));
        OmbreHairPainter.Blend(Root, Tip, 5, 10, 20).Should().Be(Root);
        OmbreHairPainter.Blend(Root, Tip, 25, 10, 20).Should().Be(Tip);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Blend_of_a_one_row_hair_area_is_the_root()
    {
        OmbreHairPainter.Blend(Root, Tip, 7, 7, 7).Should().Be(Root);
        OmbreHairPainter.Blend(Root, Tip, 7, 9, 3).Should().Be(Root);

        await Task.CompletedTask;
    }

    [Test]
    public async Task HairRows_finds_the_first_and_last_dye_rows()
    {
        var data = Solid(4, 6, 0);
        data[(1 * 4) + 2] = 98;
        data[(4 * 4) + 0] = 103;
        data[(5 * 4) + 1] = 50;

        OmbreHairPainter.HairRows(4, 6, data).Should().Be((1, 4));
        OmbreHairPainter.HairRows(4, 6, Solid(4, 6, 50)).Should().BeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task HairRows_ignores_bytes_past_the_frame()
    {
        var data = Solid(4, 3, 0).Concat(Solid(4, 2, 98)).ToArray();

        OmbreHairPainter.HairRows(4, 3, data).Should().BeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Paint_blends_dye_pixels_and_keeps_the_rest()
    {
        var data = Solid(2, 3, 98);
        data[0] = 0;
        data[1] = 50;

        var pixels = OmbreHairPainter.Paint(2, 3, frameTop: 10, data, Palette(Root), Palette(Tip), fadeTop: 10, fadeBottom: 12);

        pixels[0].Should().Be(SKColor.Empty);
        pixels[1].Should().Be(new SKColor(99, 99, 99));
        pixels[2].Should().Be(new SKColor(100, 50, 25));
        pixels[4].Should().Be(Tip);
        pixels[5].Should().Be(Tip);

        await Task.CompletedTask;
    }
}
