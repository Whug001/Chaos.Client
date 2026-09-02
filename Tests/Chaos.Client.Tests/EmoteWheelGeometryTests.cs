using Chaos.Client.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class EmoteWheelGeometryTests
{
    private const float INNER = 18f;
    private const float OUTER = 120f;

    [Test]
    public async Task Top_pixel_selects_segment_0()
    {
        EmoteWheelGeometry.GetSegmentIndex(0, -60, INNER, OUTER).Should().Be(0);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Center_hub_returns_minus_one()
    {
        EmoteWheelGeometry.GetSegmentIndex(0, 0, INNER, OUTER).Should().Be(-1);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Outside_ring_returns_minus_one()
    {
        EmoteWheelGeometry.GetSegmentIndex(0, -200, INNER, OUTER).Should().Be(-1);
        await Task.CompletedTask;
    }

    [Test]
    public async Task All_six_segments_are_reachable()
    {
        var hits = new HashSet<int>();

        for (var deg = 0; deg < 360; deg++)
        {
            var rad = deg * MathF.PI / 180f;
            var x = MathF.Sin(rad) * 80f;
            var y = -MathF.Cos(rad) * 80f;
            var seg = EmoteWheelGeometry.GetSegmentIndex(x, y, INNER, OUTER);

            if (seg >= 0)
                hits.Add(seg);
        }

        hits.Should().HaveCount(6);
        await Task.CompletedTask;
    }
}
