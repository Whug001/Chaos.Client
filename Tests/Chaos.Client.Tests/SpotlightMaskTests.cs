using Chaos.Client.Data.Models;
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

    [Test]
    public void Glow_above_the_pool_narrows_toward_the_top()
    {
        //a glow that keeps its width all the way up reads as a lit box behind the performer
        var mask = SpotlightMasks.Build(1.5f);

        LitWidth(mask, 30).Should().BeGreaterThan(LitWidth(mask, 60));
    }

    [Test]
    [Arguments(StageLightSize.Small)]
    [Arguments(StageLightSize.Medium)]
    [Arguments(StageLightSize.Large)]
    public void Performer_stays_lit_up_to_head_height(StageLightSize size)
    {
        var mask = SpotlightMasks.Get(size);

        //a standing character covers about 22 pixels across and 56 up from the floor point; all of it gets at least a third of full light
        for (var height = 1; height <= 56; height++)
            for (var dx = -11; dx <= 11; dx++)
                At(mask, dx, height).Should().BeGreaterThanOrEqualTo(11, $"({dx}, {height}) is on the performer");
    }

    [Test]
    public void Beam_foot_continues_the_beam_without_a_seam()
    {
        //a beam that stops at full strength draws a bright/dark line through the middle of the pool
        var beam = SpotlightRenderer.BeamAlphas();
        var foot = SpotlightRenderer.BeamFootAlphas();
        var lastRow = beam.GetLength(0) - 1;

        foot.GetLength(1).Should().Be(beam.GetLength(1));

        for (var x = 0; x < beam.GetLength(1); x++)
            ((int)foot[0, x]).Should().BeCloseTo(beam[lastRow, x], 3, $"column {x} is where the beam meets its foot");
    }

    [Test]
    public void Beam_foot_fades_out_by_the_front_of_the_pool()
    {
        var foot = SpotlightRenderer.BeamFootAlphas();
        var lastRow = foot.GetLength(0) - 1;

        for (var x = 0; x < foot.GetLength(1); x++)
            foot[lastRow, x].Should().BeLessThanOrEqualTo(1, $"column {x} is on the front edge");
    }

    private static byte At(LightMask mask, int dx, int height)
        => mask.Pixels[(((mask.Height / 2) - height) * mask.Width) + (mask.Width / 2) + dx];

    private static int LitWidth(LightMask mask, int height)
    {
        var count = 0;

        for (var dx = -(mask.Width / 2); dx <= mask.Width / 2; dx++)
            if (At(mask, dx, height) > 0)
                count++;

        return count;
    }
}
