using Chaos.Client.ViewModel.Portraits;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests.Portraits;

public sealed class PortraitWindowStateTests
{
    [Test]
    public void Picking_updates_the_current_colour()
    {
        var state = new PortraitWindowState();
        state.Pick(new Color(9, 8, 7));
        state.Current.Should().Be(new Color(9, 8, 7));
    }

    [Test]
    public void A_new_window_uses_pen_and_starts_clean()
    {
        var state = new PortraitWindowState();
        state.Tool.Should().Be(PortraitTool.Pen);
        state.Current.Should().Be(Color.White);
        state.IsDirty.Should().BeFalse();
    }

    [Test]
    public void Picking_while_the_picker_is_out_returns_to_pen()
    {
        var state = new PortraitWindowState();
        state.Tool = PortraitTool.Pick;
        state.Pick(new Color(9, 8, 7));
        state.Tool.Should().Be(PortraitTool.Pen);
    }

    [Test]
    public void Picking_a_swatch_keeps_another_tool()
    {
        var state = new PortraitWindowState();
        state.Tool = PortraitTool.Fill;
        state.Pick(new Color(9, 8, 7));
        state.Tool.Should().Be(PortraitTool.Fill);
        state.Current.Should().Be(new Color(9, 8, 7));
    }

    [Test]
    public void Drawing_dirties_and_opening_marks_clean()
    {
        var state = new PortraitWindowState();
        state.Canvas.BeginStep();
        state.Canvas.Set(0, 0, new Color(9, 8, 7));
        state.Canvas.EndStep();
        state.IsDirty.Should().BeTrue();

        state.MarkClean();
        state.IsDirty.Should().BeFalse();
    }

    [Test]
    public void A_matching_picture_confirms_the_set()
    {
        var state = new PortraitWindowState();
        state.BeginSet(new string('a', 64));

        state.SettleOwnPicture(new string('a', 64), 100).Should().Be("Portrait set.");
        state.PendingSetHash.Should().BeNull();
    }

    [Test]
    public void A_refused_set_clears_the_status()
    {
        var state = new PortraitWindowState();
        state.BeginSet(new string('a', 64));

        state.SettleOwnPicture(new string('b', 64), 100).Should().BeEmpty();
        state.PendingSetHash.Should().BeNull();
    }

    [Test]
    public void An_empty_picture_confirms_the_remove()
    {
        var state = new PortraitWindowState();
        state.BeginRemove();

        state.SettleOwnPicture(string.Empty, 0).Should().Be("Portrait removed.");
        state.PendingRemove.Should().BeFalse();
    }

    [Test]
    public void An_earlier_set_landing_after_remove_keeps_the_remove_pending()
    {
        var state = new PortraitWindowState();
        state.BeginSet(new string('a', 64));
        state.BeginRemove();

        state.SettleOwnPicture(new string('a', 64), 100).Should().BeNull();
        state.PendingRemove.Should().BeTrue();
        state.SettleOwnPicture(string.Empty, 0).Should().Be("Portrait removed.");
    }

    [Test]
    public void An_extra_picture_with_nothing_pending_changes_nothing()
    {
        var state = new PortraitWindowState();

        state.SettleOwnPicture(new string('a', 64), 100).Should().BeNull();
    }

    [Test]
    public void Opening_clears_a_pending_set_and_remove()
    {
        var state = new PortraitWindowState();
        state.BeginSet(new string('a', 64));
        state.Open(null);
        state.PendingSetHash.Should().BeNull();
        state.SettleOwnPicture(new string('a', 64), 100).Should().BeNull();

        state.BeginRemove();
        state.Open(null);
        state.SettleOwnPicture(string.Empty, 0).Should().BeNull();
    }

    [Test]
    public void Open_with_real_png_loads_pixels_and_refills_swatches()
    {
        var source = PortraitCanvas.Blank();
        source.Set(1, 1, new Color(200, 10, 10));
        var state = new PortraitWindowState();

        state.Open(source.ToPng());

        state.Canvas.ColorAt(1, 1).Should().Be(new Color(200, 10, 10));
        state.Canvas.Swatches.Should().Contain(new Color(200, 10, 10));
    }

    [Test]
    public void Open_with_empty_or_invalid_bytes_loads_a_blank_canvas()
    {
        var state = new PortraitWindowState();

        state.Open([]);
        state.Canvas.ColorAt(0, 0).Should().Be(new Color(0, 0, 0, 0));

        state.Open([1, 2, 3]);
        state.Canvas.ColorAt(0, 0).Should().Be(new Color(0, 0, 0, 0));
    }

    [Test]
    public void Open_resets_the_tool_and_current_colour()
    {
        var state = new PortraitWindowState();
        state.Tool = PortraitTool.Fill;
        state.Pick(new Color(200, 10, 10));

        state.Open(null);

        state.Tool.Should().Be(PortraitTool.Pen);
        state.Current.Should().Be(state.Canvas.Swatches[0]);
    }

    [Test]
    public void A_confirmed_remove_empties_the_canvas()
    {
        var source = PortraitCanvas.Blank();
        source.Set(2, 2, new Color(10, 20, 30));
        var png = source.ToPng();
        var state = new PortraitWindowState();
        state.Open(png);
        state.BeginRemove();

        state.SettleOwnPicture(PortraitCanvas.HashOf(png), png.Length).Should().BeNull();
        state.Canvas.ColorAt(2, 2).Should().Be(new Color(10, 20, 30));

        state.SettleOwnPicture(string.Empty, 0).Should().Be("Portrait removed.");
        state.Canvas.ColorAt(2, 2).Should().Be(new Color(0, 0, 0, 0));
        state.Current.Should().Be(state.Canvas.Swatches[0]);
    }
}
