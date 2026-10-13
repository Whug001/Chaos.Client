using Chaos.Client.Rendering;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

/// <summary>A wide creature takes clicks only on the pixels its frame actually draws.</summary>
public class OpaqueMaskTests
{
    //3 x 2: only the top-left pixel and the bottom-right pixel are drawn
    private static OpaqueMask Mask()
        => new(
            [
                Color.Red, Color.Transparent, Color.Transparent,
                Color.Transparent, Color.Transparent, new Color(0, 0, 0, 1)
            ],
            3,
            2);

    [Test]
    public void Only_drawn_pixels_are_opaque()
    {
        var mask = Mask();

        mask.IsOpaque(0, 0, false).Should().BeTrue();
        mask.IsOpaque(2, 1, false).Should().BeTrue();
        mask.IsOpaque(1, 0, false).Should().BeFalse();
        mask.IsOpaque(0, 1, false).Should().BeFalse();
    }

    [Test]
    public void A_flipped_draw_mirrors_the_mask()
    {
        var mask = Mask();

        mask.IsOpaque(2, 0, true).Should().BeTrue();
        mask.IsOpaque(0, 0, true).Should().BeFalse();
        mask.IsOpaque(0, 1, true).Should().BeTrue();
    }

    [Test]
    public void Points_outside_the_frame_are_not_opaque()
    {
        var mask = Mask();

        mask.IsOpaque(-1, 0, false).Should().BeFalse();
        mask.IsOpaque(3, 0, false).Should().BeFalse();
        mask.IsOpaque(0, 2, false).Should().BeFalse();
    }
}
