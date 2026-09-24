using Chaos.Client.Rendering;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class SpotlightMaskTests
{
    [Test]
    public void Mask_is_full_in_the_middle_and_empty_in_the_corners()
    {
        var mask = SpotlightMasks.Build(1.5f);
        var centre = (mask.Height / 2 * mask.Width) + (mask.Width / 2);

        mask.Pixels[centre].Should().Be(32);
        mask.Pixels[0].Should().Be(0);
        mask.Pixels[^1].Should().Be(0);
        mask.Pixels.Max().Should().Be(32);
    }

    [Test]
    public void Mask_is_mirror_symmetric()
    {
        var mask = SpotlightMasks.Build(1f);

        for (var y = 0; y < mask.Height; y++)
            for (var x = 0; x < mask.Width / 2; x++)
                mask.Pixels[(y * mask.Width) + x].Should().Be(mask.Pixels[(y * mask.Width) + (mask.Width - 1 - x)]);
    }

    [Test]
    public void Larger_sizes_make_wider_masks()
    {
        SpotlightMasks.Get(StageLightSize.Small).Width.Should().BeLessThan(SpotlightMasks.Get(StageLightSize.Medium).Width);
        SpotlightMasks.Get(StageLightSize.Medium).Width.Should().BeLessThan(SpotlightMasks.Get(StageLightSize.Large).Width);
    }

    [Test]
    public void Colour_is_a_wash_with_the_house_lights_up_and_full_when_dark()
    {
        SpotlightRenderer.ColorScale(1f, 0f).Should().BeApproximately(0.35f, 0.001f);
        SpotlightRenderer.ColorScale(1f, 1f).Should().BeApproximately(1f, 0.001f);
        SpotlightRenderer.ColorScale(0.5f, 1f).Should().BeApproximately(0.5f, 0.001f);
    }
}
