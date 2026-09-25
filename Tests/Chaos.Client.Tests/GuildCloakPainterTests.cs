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

    /// <summary>
    ///     A 9x10 bell: rows 0-5 fill columns 2-6, row 6 columns 1-7, rows 7-8 columns 0-8 and row 9 columns 1-7, so columns 0
    ///     and 8 end a row higher. Cloth is 25. With <paramref name="rune" />, dye 100 fills columns 3-5 of rows 1-4. The bottom
    ///     pixel of every column is hem (dye 102).
    /// </summary>
    private static byte[] Bell(bool rune)
    {
        var data = new byte[9 * 10];
        (int First, int Last)[] rows = [(2, 6), (2, 6), (2, 6), (2, 6), (2, 6), (2, 6), (1, 7), (0, 8), (0, 8), (1, 7)];

        for (var y = 0; y < 10; y++)
            for (var x = rows[y].First; x <= rows[y].Last; x++)
                data[(y * 9) + x] = 25;

        if (rune)
            for (var y = 1; y <= 4; y++)
                for (var x = 3; x <= 5; x++)
                    data[(y * 9) + x] = 100;

        for (var x = 0; x < 9; x++)
        {
            var bottom = (x is 0 or 8) ? 8 : 9;
            data[(bottom * 9) + x] = 102;
        }

        return data;
    }

    /// <summary>A width x height grid of cloth (25) with dye 100 in columns first-last of rows top-bottom.</summary>
    private static byte[] WithDye(int width, int height, int first, int last, int top, int bottom)
    {
        var data = Solid(width, height, 25);

        for (var y = top; y <= bottom; y++)
            for (var x = first; x <= last; x++)
                data[(y * width) + x] = 100;

        return data;
    }

    private static EpfFrame Box(int width, int height, byte[] data)
        => new()
        {
            Left = 0,
            Top = 0,
            Right = (short)width,
            Bottom = (short)height,
            Data = data
        };

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
    public void CellFor_skips_empty_rows_to_the_nearest_filled_row()
    {
        var data = new byte[15];

        foreach (var row in new[] { 0, 1, 4 })
            for (var x = 0; x < 3; x++)
                data[(row * 3) + x] = 25;

        var grid = new GuildCloakGrid(3, 5, data);
        var target = new GuildCloakGrid(3, 5, Solid(3, 5, 25));

        grid.CellFor(target, 1, 2, false, false).Y.Should().Be(1);
        grid.CellFor(target, 1, 3, false, false).Y.Should().Be(4);
        grid.CellFor(target, 2, 0, false, false).Should().Be((2, 0));
    }

    [Test]
    public void A_frame_painted_on_its_own_grid_reads_its_own_cells()
    {
        foreach (var rune in new[] { true, false })
        {
            var grid = new GuildCloakGrid(9, 10, Bell(rune));

            for (var y = 0; y < 10; y++)
                for (var x = 0; x < 9; x++)
                {
                    if (!grid.IsFilled(x, y))
                        continue;

                    grid.CellFor(grid, x, y, false, true).Should().Be((x, y));
                    grid.CellFor(grid, x, y, false, false).Should().Be((x, y));
                }
        }
    }

    [Test]
    public void Column_bottoms_low_in_the_frame_read_the_canvas_hem()
    {
        //a flap whose bottom edge runs diagonally through the lowest third: column x fills rows 0 to 10 - x
        var data = new byte[4 * 11];

        for (var x = 0; x < 4; x++)
            for (var y = 0; y <= 10 - x; y++)
                data[(y * 4) + x] = 25;

        var canvas = new GuildCloakGrid(4, 11, Solid(4, 11, 25));
        var cells = Solid(4, 11, 1);

        for (var i = 9 * 4; i < 11 * 4; i++)
            cells[i] = 2;

        var pixels = GuildCloakPainter.Paint(4, 11, data, Palette(), canvas, cells, [Red, Green], false);

        for (var x = 0; x < 4; x++)
            pixels[((10 - x) * 4) + x].Should().Be(GuildCloakPainter.Shade(Green, GuildCloakPainter.EDGE_SHADE), $"column {x}");
    }

    [Test]
    public void The_rune_centre_reads_the_canvas_rune_centre_and_a_flip_keeps_it_there()
    {
        //canvas rune: columns 5-7 (centre 6; the box's middle is 5). Frame rune: columns 1-3 (centre 2)
        var canvas = new GuildCloakGrid(11, 8, WithDye(11, 8, 5, 7, 1, 4));
        var target = new GuildCloakGrid(11, 8, WithDye(11, 8, 1, 3, 1, 4));

        canvas.RuneCentre.Should().Be(6);
        target.RuneCentre.Should().Be(2);

        canvas.CellFor(target, 2, 3, false, true).X.Should().Be(6);
        canvas.CellFor(target, 2, 3, true, true).X.Should().Be(6);

        //each half of the row maps onto the matching half of the canvas row, and a flip swaps the halves
        canvas.CellFor(target, 6, 3, false, true).X.Should().Be(8);
        canvas.CellFor(target, 1, 3, false, true).X.Should().Be(3);
        canvas.CellFor(target, 6, 3, true, true).X.Should().Be(3);
        canvas.CellFor(target, 1, 3, true, true).X.Should().Be(8);

        //without centring the row stretches as a whole
        canvas.CellFor(target, 2, 3, false, false).X.Should().Be(2);
        canvas.CellFor(target, 2, 3, true, false).X.Should().Be(8);
    }

    [Test]
    public void Dye_pixels_near_the_bottom_of_their_column_are_hem_not_rune()
    {
        var grid = new GuildCloakGrid(12, 6, WithDye(12, 6, 0, 11, 3, 5));

        grid.RuneCentre.Should().BeNull();

        //three rows above the bottom is rune: row 2 of a 6-row grid, across all 12 columns
        new GuildCloakGrid(12, 6, WithDye(12, 6, 0, 11, 2, 5)).RuneCentre.Should().Be(5.5);
    }

    [Test]
    public void A_rune_needs_ten_pixels()
    {
        new GuildCloakGrid(12, 8, WithDye(12, 8, 0, 8, 1, 1)).RuneCentre.Should().BeNull();
        new GuildCloakGrid(12, 8, WithDye(12, 8, 0, 9, 1, 1)).RuneCentre.Should().Be(4.5);
    }

    [Test]
    public void A_big_frame_with_no_dye_pixel_shows_the_inside_of_the_cape()
    {
        GuildCloakGrid.IsInsideView(Box(13, 12, Solid(13, 12, 25))).Should().BeTrue();
    }

    [Test]
    public void A_dye_pixel_or_a_small_frame_is_not_the_inside()
    {
        var withDye = Solid(13, 12, 25);
        withDye[40] = 100;

        GuildCloakGrid.IsInsideView(Box(13, 12, withDye)).Should().BeFalse();
        GuildCloakGrid.IsInsideView(Box(15, 10, Solid(15, 10, 25))).Should().BeFalse();
    }

    [Test]
    public void IsInsideView_reads_only_the_frame_box()
    {
        //the bytes after the box belong to later frames (see HasPixels_reads_only_the_frame_box)
        var data = Solid(13, 12, 25).Concat(Solid(4, 1, 100)).ToArray();

        GuildCloakGrid.IsInsideView(Box(13, 12, data)).Should().BeTrue();
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
