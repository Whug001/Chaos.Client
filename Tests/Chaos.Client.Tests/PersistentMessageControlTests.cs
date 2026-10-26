using Chaos.Client.Controls.World.ViewPort;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public sealed class PersistentMessageControlTests
{
    private static readonly Rectangle Viewport = new(0, 0, 400, 300);
    private const int MIN_WIDTH = 25 * 6;

    [Test]
    public void Short_text_keeps_the_default_width_and_position()
    {
        var control = new PersistentMessageControl(Viewport);

        control.SetMessage("Hi");

        control.Width.Should().Be(MIN_WIDTH);
        control.X.Should().Be(Viewport.Right - MIN_WIDTH - 2);
        control.Visible.Should().BeTrue();
    }

    [Test]
    public void Long_text_widens_leftward_with_the_same_right_edge()
    {
        var control = new PersistentMessageControl(Viewport);
        var text = "Shinewood Gold Beetle 123/200";

        control.SetMessage(text);

        control.Width.Should().Be(text.Length * 6);
        control.Width.Should().BeGreaterThan(MIN_WIDTH);
        control.X.Should().BeLessThan(Viewport.Right - MIN_WIDTH - 2);
        (control.X + control.Width).Should().Be(Viewport.Right - 2);
    }

    [Test]
    public void Empty_text_hides_the_control()
    {
        var control = new PersistentMessageControl(Viewport);
        control.SetMessage("Hi");

        control.SetMessage(string.Empty);

        control.Visible.Should().BeFalse();
        control.Text.Should().BeEmpty();
    }

    [Test]
    public void Very_long_text_is_capped_at_the_viewport_width()
    {
        var control = new PersistentMessageControl(Viewport);

        control.SetMessage(new string('a', 100));

        control.Width.Should().Be(Viewport.Width - 4);
        (control.X + control.Width).Should().Be(Viewport.Right - 2);
    }
}
