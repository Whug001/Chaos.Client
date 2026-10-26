using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests.College;

public class SketchToolboxTests
{
    private static SketchToolbox Toolbox() => new() { Colour = 2 };

    private static void Tap(SketchToolbox toolbox, int x, int y)
    {
        toolbox.Press(x, y);
        toolbox.Release(x, y);
    }

    [Test]
    public void A_new_canvas_is_blank_with_the_sketch_colours()
    {
        var toolbox = new SketchToolbox();

        toolbox.Drawing.Pixels.Should().HaveCount(CollegeProtocol.DRAWING_PIXELS).And.OnlyContain(p => p == 0);
        toolbox.Pixels().Should().HaveCount(CollegeProtocol.DRAWING_PIXELS).And.OnlyContain(p => p == 0);
        SketchToolbox.Colours.Should().HaveCount(CollegeProtocol.SKETCH_COLOURS);
        SketchToolbox.Colours[0].Should().Be(new Color(255, 255, 255));
        toolbox.CanUndo.Should().BeFalse();
    }

    [Test]
    public void The_pen_sets_one_pixel()
    {
        var toolbox = Toolbox();
        toolbox.Tool = SketchTool.Pen;

        Tap(toolbox, 3, 3);

        toolbox.Drawing.SwatchAt(3, 3).Should().Be(2);
        toolbox.Drawing.Pixels.Count(p => p != 0).Should().Be(1);
    }

    [Test]
    public void The_big_pen_sets_a_three_by_three_block()
    {
        var toolbox = Toolbox();
        toolbox.Tool = SketchTool.BigPen;

        Tap(toolbox, 50, 30);

        for (var y = 29; y <= 31; y++)
            for (var x = 49; x <= 51; x++)
                toolbox.Drawing.SwatchAt(x, y).Should().Be(2);

        toolbox.Drawing.Pixels.Count(p => p != 0).Should().Be(9);
    }

    [Test]
    public void Fill_covers_the_canvas()
    {
        var toolbox = Toolbox();
        toolbox.Tool = SketchTool.Fill;

        Tap(toolbox, 0, 0);

        toolbox.Drawing.Pixels.Should().OnlyContain(p => p == 2);
    }

    [Test]
    public void Erase_writes_zero()
    {
        var toolbox = Toolbox();
        Tap(toolbox, 5, 5);

        toolbox.Tool = SketchTool.Erase;
        Tap(toolbox, 5, 5);

        toolbox.Drawing.SwatchAt(5, 5).Should().Be(0);
        toolbox.Drawing.Pixels.Should().OnlyContain(p => p == 0);
    }

    [Test]
    public void Undo_takes_back_a_stroke()
    {
        var toolbox = Toolbox();

        Tap(toolbox, 3, 3);
        toolbox.Undo();

        toolbox.Drawing.Pixels.Should().OnlyContain(p => p == 0);
        toolbox.CanUndo.Should().BeFalse();
    }

    [Test]
    public void Clear_empties_the_canvas()
    {
        var toolbox = Toolbox();
        Tap(toolbox, 3, 3);
        Tap(toolbox, 10, 10);

        toolbox.Clear();

        toolbox.Drawing.Pixels.Should().OnlyContain(p => p == 0);
    }

    [Test]
    public void Pixels_stay_inside_the_eight_colours()
    {
        var toolbox = new SketchToolbox();

        foreach (var colour in new byte[] { 1, 3, 7 })
        {
            toolbox.Colour = colour;
            Tap(toolbox, colour, colour);
        }

        var pixels = toolbox.Pixels();

        pixels.Should().HaveCount(CollegeProtocol.DRAWING_PIXELS);
        pixels.Should().OnlyContain(p => p < CollegeProtocol.SKETCH_COLOURS);
        pixels.Count(p => p != 0).Should().Be(3);
    }
}
