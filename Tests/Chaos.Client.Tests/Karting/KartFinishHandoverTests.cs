#region
using Chaos.Client.Systems.Karting;
using Chaos.DarkAges.Karting;
using FluentAssertions;
#endregion

namespace Chaos.Client.Tests.Karting;

public class KartFinishHandoverTests
{
    private static KartBody Frozen => new() { X = 10, Y = 20, Heading = 0 };

    [Test]
    public void It_keeps_the_frozen_body_until_the_sample_reaches_it_along_the_heading()
    {
        KartFinishHandover.ShouldKeepFrozen(Frozen, new KartBody { X = 9, Y = 20 }, 0.2).Should().BeTrue();
        KartFinishHandover.ShouldKeepFrozen(Frozen, new KartBody { X = 10.1, Y = 20 }, 0.2).Should().BeFalse();
    }

    [Test]
    public void It_switches_when_close_or_after_the_safety_timeout()
    {
        KartFinishHandover.ShouldKeepFrozen(Frozen, new KartBody { X = 10, Y = 20.05 }, 0.2).Should().BeFalse();
        KartFinishHandover.ShouldKeepFrozen(Frozen, null, KartFinishHandover.MAX_WAIT_SECONDS).Should().BeFalse();
    }

    [Test]
    public void It_releases_once_and_never_rearms_until_reset()
    {
        var handover = new KartFinishHandover();
        var sample = new KartBody { X = 9, Y = 20 };
        handover.Capture(Frozen, 1);

        handover.BodyFor(sample, 1.1).Should().Be(Frozen);
        var caughtUp = new KartBody { X = 11, Y = 20 };
        handover.BodyFor(caughtUp, 1.2).Should().Be(caughtUp);
        handover.Capture(Frozen, 1.3);
        handover.BodyFor(sample, 1.4).Should().Be(sample);
    }

    [Test]
    public void It_releases_at_the_time_cap_even_when_the_sample_never_catches_up()
    {
        var handover = new KartFinishHandover();
        handover.Capture(Frozen, 1);

        handover.BodyFor(new KartBody { X = 9, Y = 20 }, 1.49).Should().Be(Frozen);
        handover.BodyFor(new KartBody { X = 9, Y = 20 }, 1.5).Should().Be(new KartBody { X = 9, Y = 20 });
    }

    [Test]
    public void Reset_clears_the_completed_state()
    {
        var handover = new KartFinishHandover();
        var sample = new KartBody { X = 9, Y = 20 };
        handover.Capture(Frozen, 1);
        handover.BodyFor(sample, 1.5);
        handover.Reset();
        handover.Capture(Frozen, 2);

        handover.BodyFor(sample, 2.1).Should().Be(Frozen);
    }
}
