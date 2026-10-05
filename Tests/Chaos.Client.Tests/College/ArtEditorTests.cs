using Chaos.Client.ViewModel.College;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests.College;

public class ArtEditorTests
{
    private static ArtEditor Editor() => new(PixelDrawing.Blank()) { Current = 7 };

    private static void Tap(ArtEditor editor, int x, int y, bool erase = false)
    {
        editor.Press(x, y, erase);
        editor.Release(x, y);
    }

    private static int Painted(ArtEditor editor) => editor.Drawing.Pixels.Count(p => p != 0);

    [Test]
    public void Brush_size_cycles_one_to_three()
    {
        var editor = Editor();

        editor.BrushSize.Should().Be(1);
        editor.CycleBrush();
        editor.BrushSize.Should().Be(2);
        editor.CycleBrush();
        editor.BrushSize.Should().Be(3);
        editor.CycleBrush();
        editor.BrushSize.Should().Be(1);
    }

    [Test]
    public void Each_brush_size_stays_inside_the_edges()
    {
        var editor = Editor();
        Tap(editor, 0, 0);
        Painted(editor).Should().Be(1);

        editor = Editor();
        editor.CycleBrush();
        Tap(editor, 95, 71);
        Painted(editor).Should().Be(1);
        Tap(editor, 0, 0);
        Painted(editor).Should().Be(5);

        editor = Editor();
        editor.CycleBrush();
        editor.CycleBrush();
        Tap(editor, 0, 0);
        Painted(editor).Should().Be(4);
        Tap(editor, 95, 71);
        Painted(editor).Should().Be(8);
        Tap(editor, 50, 30);
        Painted(editor).Should().Be(17);
    }

    [Test]
    public void A_drag_paints_every_pixel_between_moves()
    {
        var editor = Editor();

        editor.Press(0, 0, false);
        editor.Drag(10, 0);
        editor.Release(10, 0);

        Enumerable.Range(0, 11).Should().OnlyContain(x => editor.Drawing.SwatchAt(x, 0) == 7);
    }

    [Test]
    public void Mirror_copies_the_pen_and_the_centre_columns_meet()
    {
        var editor = Editor();
        editor.Mirror = true;

        Tap(editor, 2, 3);
        Tap(editor, 47, 5);

        editor.Drawing.SwatchAt(93, 3).Should().Be(7);
        editor.Drawing.SwatchAt(48, 5).Should().Be(7);
    }

    [Test]
    public void Mirror_copies_a_line()
    {
        var editor = Editor();
        editor.Mirror = true;
        editor.Tool = ArtTool.Line;

        editor.Press(0, 0, false);
        editor.Drag(3, 0);
        editor.Release(3, 0);

        Enumerable.Range(92, 4).Should().OnlyContain(x => editor.Drawing.SwatchAt(x, 0) == 7);
    }

    [Test]
    public void Mirror_copies_a_fill()
    {
        var editor = Editor();

        for (var y = 0; y < 72; y++)
        {
            editor.Drawing.Set(40, y, 3);
            editor.Drawing.Set(55, y, 3);
        }

        editor.Mirror = true;
        editor.Tool = ArtTool.Fill;
        Tap(editor, 0, 0);

        editor.Drawing.SwatchAt(0, 0).Should().Be(7);
        editor.Drawing.SwatchAt(95, 71).Should().Be(7);
        editor.Drawing.SwatchAt(47, 10).Should().Be(0);
    }

    [Test]
    public void Pick_takes_the_colour_and_returns_to_the_pen()
    {
        var editor = Editor();
        editor.Drawing.Set(4, 4, 12);
        editor.Tool = ArtTool.Pick;

        Tap(editor, 4, 4);

        editor.Current.Should().Be(12);
        editor.Tool.Should().Be(ArtTool.Pen);
        editor.Drawing.CanUndo.Should().BeFalse();
    }

    [Test]
    public void The_right_button_always_erases()
    {
        var editor = Editor();
        editor.Drawing.Set(5, 5, 3);
        editor.Tool = ArtTool.Fill;

        Tap(editor, 5, 5, true);

        editor.Drawing.SwatchAt(5, 5).Should().Be(0);
    }

    [Test]
    public void A_line_waits_for_release_and_shows_a_preview()
    {
        var editor = Editor();
        editor.Tool = ArtTool.Line;

        editor.Press(0, 0, false);
        editor.Drag(4, 0);

        Painted(editor).Should().Be(0);
        editor.PreviewPixels().Should().Contain(new Point(4, 0));

        editor.Release(4, 0);

        Painted(editor).Should().Be(5);
        editor.PreviewPixels().Should().BeEmpty();
    }

    [Test]
    public void A_stroke_is_one_undo_step()
    {
        var editor = Editor();

        editor.Press(0, 0, false);
        editor.Drag(20, 20);
        editor.Release(20, 20);
        editor.Undo();

        Painted(editor).Should().Be(0);
        editor.Drawing.CanUndo.Should().BeFalse();
    }

    [Test]
    public void Loading_a_drawing_ends_any_stroke()
    {
        var editor = Editor();
        editor.Press(0, 0, false);

        editor.Load(PixelDrawing.Blank());

        editor.IsPressing.Should().BeFalse();
        editor.Drawing.SwatchAt(0, 0).Should().Be(0);
    }
}
