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
}
