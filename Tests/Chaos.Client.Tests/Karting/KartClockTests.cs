#region
using Chaos.Client.Systems.Karting;
using FluentAssertions;
#endregion

namespace Chaos.Client.Tests.Karting;

public class KartClockTests
{
    [Test]
    public void Snapshot_ticks_set_smooth_and_snap_the_server_step_estimate()
    {
        var clock = new KartClock();
        clock.OnSnapshot(300, 10.0);
        clock.Estimate(10.0).Should().Be(300);
        clock.Estimate(11.0).Should().Be(330);
        clock.OnSnapshot(340, 11.0);
        clock.Estimate(11.0).Should().Be(331);
        clock.OnSnapshot(400, 11.0);
        clock.Estimate(11.0).Should().Be(400);
    }
}
