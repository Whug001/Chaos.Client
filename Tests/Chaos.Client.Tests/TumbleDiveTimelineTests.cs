#region
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using FluentAssertions;
#endregion

namespace Chaos.Client.Tests;

public class TumbleDiveTimelineTests
{
    private static TumbleFall Fall(int dropped, TumbleTowerFallOutcome outcome = TumbleTowerFallOutcome.Landed)
        => new(1, 5, 5, 23, 5, dropped, outcome, 0);

    [Test]
    public void A_one_floor_fall_sinks_dives_lands_then_finishes()
    {
        var fall = Fall(1);

        TumbleDiveTimeline.At(fall, 0.35, fromFloor: 0).Phase.Should().Be(TumbleDivePhase.Sink);
        TumbleDiveTimeline.At(fall, 0.35, 0).Progress.Should().BeApproximately(0.5, 0.0001);

        var dive = TumbleDiveTimeline.At(fall, 1.05, 0);
        dive.Phase.Should().Be(TumbleDivePhase.Dive);
        dive.UpperFloor.Should().Be(0);
        dive.LowerFloor.Should().Be(1);
        dive.Progress.Should().BeApproximately(0.5, 0.0001);

        TumbleDiveTimeline.At(fall, 1.5, 0).Phase.Should().Be(TumbleDivePhase.Land);
        TumbleDiveTimeline.At(fall, 2.7, 0).Phase.Should().Be(TumbleDivePhase.Done);
    }

    [Test]
    public void A_chain_fall_dives_one_floor_at_a_time()
    {
        var fall = Fall(2);

        var second = TumbleDiveTimeline.At(fall, 1.4 + 0.2, 0);
        second.Phase.Should().Be(TumbleDivePhase.Dive);
        second.Segment.Should().Be(1);
        second.UpperFloor.Should().Be(1);
        second.LowerFloor.Should().Be(2);
        second.Progress.Should().BeApproximately(0.5, 0.0001);
    }

    [Test]
    [Arguments(TumbleTowerFallOutcome.KnockedOut)]
    [Arguments(TumbleTowerFallOutcome.Respawned)]
    public void A_fall_through_the_bottom_dives_into_darkness(TumbleTowerFallOutcome outcome)
    {
        var frame = TumbleDiveTimeline.At(Fall(1, outcome), 1.05, 2);

        frame.UpperFloor.Should().Be(2);
        frame.LowerFloor.Should().BeNull();
        frame.IntoDarkness.Should().BeTrue();
    }

    [Test]
    public void Total_length_matches_the_shared_server_timing_plus_landing()
        => TumbleDiveTimeline.TotalSeconds(3).Should().BeApproximately(TumbleTowerTiming.FallSeconds(3) + TumbleDiveTimeline.LAND_SECONDS, 0.0001);
}
