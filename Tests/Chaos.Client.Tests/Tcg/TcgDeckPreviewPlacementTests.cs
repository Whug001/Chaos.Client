#region
using Chaos.Client.Controls.World.Popups.Tcg;
using FluentAssertions;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Tests;

public sealed class TcgDeckPreviewPlacementTests
{
    [Test]
    public void Puts_the_preview_right_of_the_cursor_on_the_left_grid()
        => TcgDeckBuilderControl.PreviewPosition(40, 100, 640, 420).Should().Be(new Point(52, 100));

    [Test]
    public void Flips_the_preview_left_near_the_right_edge()
        => TcgDeckBuilderControl.PreviewPosition(600, 100, 640, 420).Should().Be(new Point(468, 100));

    [Test]
    public void Clamps_the_preview_inside_near_the_bottom()
        => TcgDeckBuilderControl.PreviewPosition(40, 380, 640, 420).Should().Be(new Point(52, 252));

    [Test]
    public void Keeps_the_preview_inside_the_builder()
    {
        for (var x = 0; x < 640; x += 7)
            for (var y = 0; y < 420; y += 11)
            {
                var position = TcgDeckBuilderControl.PreviewPosition(x, y, 640, 420);

                (position is { X: >= 0, Y: >= 0 } && ((position.X + 120) <= 640) && ((position.Y + 168) <= 420))
                    .Should()
                    .BeTrue($"({x}, {y}) gave {position}");
            }
    }
}
