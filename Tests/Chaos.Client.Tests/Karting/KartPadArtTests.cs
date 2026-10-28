#region
using Chaos.Client.Rendering;
using Chaos.DarkAges.Karting;
using FluentAssertions;
#endregion

namespace Chaos.Client.Tests.Karting;

public class KartPadArtTests
{
    [Test]
    public async Task Pad_tiles_include_both_rectangle_bounds()
    {
        var pad = new KartBoostPad(2, 3, 4, 5, 0, BoostLevel.Small);

        KartPadArt.PadTiles(pad).Should().BeEquivalentTo(
        [
            (2, 3), (3, 3), (4, 3),
            (2, 4), (3, 4), (4, 4),
            (2, 5), (3, 5), (4, 5)
        ]);
    }

    [Test]
    public async Task Chevron_frame_uses_kart_screen_frame_mapping()
    {
        KartPadArt.FrameForHeading(0).Should().Be(KartFrames.FrameFor(0));
    }
}