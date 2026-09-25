using Chaos.Client.Rendering;
using DALib.Drawing;
using FluentAssertions;
using SkiaSharp;

namespace Chaos.Client.Tests;

public class GuildCloakPainterTests
{
    private static readonly SKColor Red = new(100, 0, 0);
    private static readonly SKColor Green = new(0, 100, 0);

    /// <summary>Every slot is gray 99 (the cape's lightest cloth), slot 29 is gray 31, and the dye slots are bright magenta.</summary>
    private static SKColor[] Palette()
    {
        var palette = Enumerable.Repeat(new SKColor(99, 99, 99), 256).ToArray();
        palette[29] = new SKColor(31, 31, 31);

        for (var i = 98; i <= 103; i++)
            palette[i] = new SKColor(255, 0, 255);

        return palette;
    }

    private static byte[] Solid(int width, int height, byte value) => Enumerable.Repeat(value, width * height).ToArray();

    /// <summary>A 5x5 grid whose two left columns are color 1 and the rest color 2.</summary>
    private static byte[] HalfAndHalf()
    {
        var cells = new byte[25];

        for (var y = 0; y < 5; y++)
            for (var x = 0; x < 5; x++)
                cells[(y * 5) + x] = (byte)(x < 2 ? 1 : 2);

        return cells;
    }

    private static float InnerShade(int brightness)
        => GuildCloakPainter.BASE_SHADE + (GuildCloakPainter.LIGHT_SHADE * brightness / GuildCloakPainter.FULL_BRIGHTNESS);

    [Test]
    public void A_frame_painted_on_its_own_grid_takes_each_cells_color()
    {
        var data = Solid(5, 5, 25);
        var reference = new GuildCloakGrid(5, 5, data);

        var pixels = GuildCloakPainter.Paint(5, 5, data, Palette(), reference, HalfAndHalf(), [Red, Green], false);

        pixels[(2 * 5) + 1].Should().Be(GuildCloakPainter.Shade(Red, InnerShade(99)));
        pixels[(2 * 5) + 3].Should().Be(GuildCloakPainter.Shade(Green, InnerShade(99)));
        pixels[0].Should().Be(GuildCloakPainter.Shade(Red, GuildCloakPainter.EDGE_SHADE));
    }

    [Test]
    public void A_flipped_draw_reads_the_design_mirrored()
    {
        var data = Solid(5, 5, 25);
        var reference = new GuildCloakGrid(5, 5, data);

        var pixels = GuildCloakPainter.Paint(5, 5, data, Palette(), reference, HalfAndHalf(), [Red, Green], true);

        pixels[(2 * 5) + 1].Should().Be(GuildCloakPainter.Shade(Green, InnerShade(99)));
        pixels[(2 * 5) + 3].Should().Be(GuildCloakPainter.Shade(Red, InnerShade(99)));
    }

    [Test]
    public void A_frame_of_another_size_maps_by_fraction_of_its_outline()
    {
        var reference = new GuildCloakGrid(5, 5, Solid(5, 5, 25));
        var data = Solid(9, 3, 25);

        var pixels = GuildCloakPainter.Paint(9, 3, data, Palette(), reference, HalfAndHalf(), [Red, Green], false);

        pixels[(1 * 9) + 1].Should().Be(GuildCloakPainter.Shade(Red, InnerShade(99)));
        pixels[(1 * 9) + 7].Should().Be(GuildCloakPainter.Shade(Green, InnerShade(99)));
    }

    [Test]
    public void Dye_pixels_take_the_brightness_of_the_cloth_around_them()
    {
        var data = Solid(5, 5, 29);
        data[12] = 100;
        var reference = new GuildCloakGrid(5, 5, data);

        var pixels = GuildCloakPainter.Paint(5, 5, data, Palette(), reference, Solid(5, 5, 1), [Red], false);

        pixels[12].Should().Be(GuildCloakPainter.Shade(Red, InnerShade(31)));
    }

    [Test]
    public void An_unpainted_cell_takes_the_nearest_painted_cell_in_its_row()
    {
        var reference = new GuildCloakGrid(5, 5, Solid(5, 5, 25));
        var cells = Solid(5, 5, 2);
        cells[(2 * 5) + 2] = 0;
        cells[(2 * 5) + 3] = 1;

        GuildCloakPainter.ColorNumberAt(reference, cells, 2, 2, 2).Should().Be(2);

        for (var x = 0; x < 5; x++)
            cells[(4 * 5) + x] = 0;

        GuildCloakPainter.ColorNumberAt(reference, cells, 2, 4, 2).Should().Be(1);
    }

    [Test]
    public void An_empty_frame_paints_nothing()
    {
        var reference = new GuildCloakGrid(5, 5, Solid(5, 5, 25));

        var pixels = GuildCloakPainter.Paint(3, 3, new byte[9], Palette(), reference, HalfAndHalf(), [Red], false);

        pixels.Should().OnlyContain(color => color == default);
    }

    [Test]
    public void HasPixels_reads_only_the_frame_box()
    {
        //DALib returns most retail frames with the later frames' bytes after their own: a blank 1x1 frame stays blank
        var frame = new EpfFrame
        {
            Left = 0,
            Top = 0,
            Right = 1,
            Bottom = 1,
            Data = [0, 25, 25, 25]
        };

        GuildCloakGrid.HasPixels(frame).Should().BeFalse();
    }

    [Test]
    public void CellAt_skips_empty_rows_to_the_nearest_filled_row()
    {
        var data = new byte[15];

        foreach (var row in new[] { 0, 1, 4 })
            for (var x = 0; x < 3; x++)
                data[(row * 3) + x] = 25;

        var grid = new GuildCloakGrid(3, 5, data);

        grid.CellAt(0.5f, 0.5f).Y.Should().Be(1);
        grid.CellAt(0.5f, 0.8f).Y.Should().Be(4);
        grid.CellAt(1f, 0f).Should().Be((2, 0));
    }

    [Test]
    public void ToImage_places_pixels_at_the_frame_position()
    {
        var frame = new EpfFrame
        {
            Left = 2,
            Top = 3,
            Right = 4,
            Bottom = 5,
            Data = [25, 25, 25, 25]
        };

        var pixels = new SKColor[4];
        pixels[0] = Red;

        using var image = GuildCloakPainter.ToImage(frame, pixels);
        using var bitmap = SKBitmap.FromImage(image);

        image.Width.Should().Be(4);
        image.Height.Should().Be(5);
        bitmap.GetPixel(2, 3).Should().Be(Red);
        bitmap.GetPixel(0, 0).Alpha.Should().Be(0);
    }
}
