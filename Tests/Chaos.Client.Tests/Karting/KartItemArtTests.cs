#region
using Chaos.Client.Rendering;
using FluentAssertions;
#endregion

namespace Chaos.Client.Tests.Karting;

public class KartItemArtTests
{
    [Test]
    public async Task No_spin_keeps_the_heading_frame()
    {
        KartItemArt.SpinFrame(1.0, 0).Should().Be(KartFrames.FrameFor(1.0));
    }

    [Test]
    public async Task A_spin_turns_the_frame_and_fifteen_ticks_make_a_full_turn()
    {
        KartItemArt.SpinFrame(0, 15).Should().Be(KartFrames.FrameFor(0));
        KartItemArt.SpinFrame(0, 4).Should().NotBe(KartFrames.FrameFor(0));
    }

    //formatter:off
    [Test]
    [Arguments(KartItemShape.Cache)]
    [Arguments(KartItemShape.Wisp)]
    [Arguments(KartItemShape.Bolt)]
    [Arguments(KartItemShape.Snare)]
    [Arguments(KartItemShape.Ward)]
    //formatter:on
    public async Task Every_shape_has_visible_pixels(KartItemShape shape)
    {
        var (width, height, pixels) = KartItemArt.Pixels(shape);

        pixels.Should().HaveCount(width * height);
        pixels.Should().Contain(p => p.A > 0);
    }

    [Test]
    public async Task Raven_frames_point_different_ways()
    {
        KartItemArt.RavenPixels(0).Should().NotEqual(KartItemArt.RavenPixels(KartFrames.FRAMES / 4));
    }
}
