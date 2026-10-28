#region
using Chaos.Client.Rendering;
using FluentAssertions;
#endregion

namespace Chaos.Client.Tests.Karting;

public class KartFramesTests
{
    [Test]
    [Arguments(-Math.PI / 4, 0)]
    [Arguments(0, 1)]
    [Arguments(Math.PI / 4, 4)]
    [Arguments(3 * Math.PI / 4, 8)]
    public async Task Heading_maps_to_screen_frame(double heading, int frame)
    {
        KartFrames.FrameFor(heading).Should().Be(frame);
    }

    [Test]
    [Arguments(10.5, 10.5, 10, 10, 0f, 0f)]
    [Arguments(11.0, 10.5, 11, 10, -14f, -7f)]
    [Arguments(10.75, 10.5, 10, 10, 7f, 3.5f)]
    public async Task Ride_offset_returns_tile_and_pixel_offset(double x, double y, int tileX, int tileY, float offsetX, float offsetY)
    {
        var result = KartFrames.RideOffset(x, y);
        result.TileX.Should().Be(tileX);
        result.TileY.Should().Be(tileY);
        result.OffsetX.Should().BeApproximately(offsetX, 1e-6f);
        result.OffsetY.Should().BeApproximately(offsetY, 1e-6f);
    }
}
