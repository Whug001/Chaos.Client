using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests.College;

public class PixelDrawingTests
{
    [Test]
    public void A_blank_drawing_is_swatch_0_with_the_default_palette()
    {
        var drawing = PixelDrawing.Blank();

        drawing.Pixels.Should().HaveCount(CollegeProtocol.DRAWING_PIXELS).And.OnlyContain(p => p == 0);
        drawing.Palette.Should().Equal(ArtPalette.Default);
        drawing.IsDirty.Should().BeFalse();
        drawing.CanUndo.Should().BeFalse();
    }

    [Test]
    public void From_pads_short_data_and_clears_bad_swatches()
    {
        var drawing = PixelDrawing.From([1, 2, 3], [5, 40]);

        drawing.Palette[0].Should().Be(new Color(1, 2, 3));
        drawing.Palette[1].Should().Be(ArtPalette.Default[1]);
        drawing.Pixels[0].Should().Be(5);
        drawing.Pixels[1].Should().Be(0);
        drawing.Pixels.Should().HaveCount(CollegeProtocol.DRAWING_PIXELS);
    }

    [Test]
    public void Palette_bytes_round_trip()
    {
        var drawing = PixelDrawing.Blank();

        PixelDrawing.From(drawing.PaletteBytes(), drawing.Pixels).Palette.Should().Equal(drawing.Palette);
        drawing.PaletteBytes().Should().HaveCount(CollegeProtocol.DRAWING_PALETTE_BYTES);
    }

    [Test]
    public void A_step_that_changes_nothing_leaves_nothing_to_undo()
    {
        var drawing = PixelDrawing.Blank();

        drawing.BeginStep();
        drawing.Set(3, 3, 0);
        drawing.EndStep();

        drawing.CanUndo.Should().BeFalse();
        drawing.IsDirty.Should().BeFalse();
    }

    [Test]
    public void Undo_and_redo_restore_pixels()
    {
        var drawing = PixelDrawing.Blank();
        drawing.BeginStep();
        drawing.Set(3, 3, 9);
        drawing.EndStep();

        drawing.Undo();
        drawing.SwatchAt(3, 3).Should().Be(0);
        drawing.CanRedo.Should().BeTrue();

        drawing.Redo();
        drawing.SwatchAt(3, 3).Should().Be(9);
        drawing.IsDirty.Should().BeTrue();
    }

    [Test]
    public void Undo_keeps_fifty_steps()
    {
        var drawing = PixelDrawing.Blank();

        for (var i = 0; i < 60; i++)
        {
            drawing.BeginStep();
            drawing.Set(i, 0, 1);
            drawing.EndStep();
        }

        var undone = 0;

        while (drawing.CanUndo)
        {
            drawing.Undo();
            undone++;
        }

        undone.Should().Be(PixelDrawing.MAX_UNDO);
        drawing.SwatchAt(9, 0).Should().Be(1);
        drawing.SwatchAt(10, 0).Should().Be(0);
    }

    [Test]
    public void A_new_edit_clears_redo()
    {
        var drawing = PixelDrawing.Blank();
        drawing.BeginStep();
        drawing.Set(1, 1, 2);
        drawing.EndStep();
        drawing.Undo();

        drawing.BeginStep();
        drawing.Set(2, 2, 2);
        drawing.EndStep();

        drawing.CanRedo.Should().BeFalse();
    }

    [Test]
    public void Changing_a_swatch_recolours_its_pixels_and_undoes()
    {
        var drawing = PixelDrawing.Blank();
        var before = drawing.Palette[0];

        drawing.SetColour(0, new Color(10, 20, 30, 77));

        drawing.Palette[0].Should().Be(new Color(10, 20, 30));
        drawing.CanUndo.Should().BeTrue();

        drawing.Undo();
        drawing.Palette[0].Should().Be(before);
    }

    [Test]
    public void Fill_does_not_cross_corner_touches()
    {
        var drawing = PixelDrawing.Blank();
        drawing.Set(0, 2, 3);
        drawing.Set(1, 1, 3);
        drawing.Set(2, 0, 3);

        drawing.Fill(0, 0, 5);

        drawing.SwatchAt(0, 0).Should().Be(5);
        drawing.SwatchAt(1, 0).Should().Be(5);
        drawing.SwatchAt(0, 1).Should().Be(5);
        drawing.SwatchAt(2, 2).Should().Be(0);
        drawing.SwatchAt(1, 1).Should().Be(3);
    }

    [Test]
    public void Drawn_pixels_count_off_the_most_used_swatch()
    {
        var drawing = PixelDrawing.Blank();
        drawing.Fill(0, 0, 4);
        drawing.Set(0, 0, 1);

        drawing.DrawnPixels.Should().Be(1);
    }
}
