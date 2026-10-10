#region
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using FluentAssertions;
#endregion

namespace Chaos.Client.Tests;

public class TumbleDiveFadeTests
{
    private static TumbleDiveFrame Dive(double secondsIn, int segment = 0, bool intoDarkness = false)
    {
        var length = segment == 0 ? TumbleTowerTiming.DIVE_SECONDS : TumbleTowerTiming.CHAIN_DIVE_SECONDS;

        return new TumbleDiveFrame(TumbleDivePhase.Dive, secondsIn / length, segment, segment, intoDarkness ? null : segment + 1, intoDarkness);
    }

    [Test]
    public void Reduce_motion_picks_the_fade_and_the_default_picks_the_dive_through()
    {
        TumbleDiveFade.StyleFor(true).Should().Be(TumbleDiveStyle.Fade);
        TumbleDiveFade.StyleFor(false).Should().Be(TumbleDiveStyle.DiveThrough);
    }

    [Test]
    public void The_floor_being_left_fades_to_black_in_a_quarter_second()
    {
        var start = TumbleDiveFade.At(Dive(0));
        start.ShowUpper.Should().BeTrue();
        start.Black.Should().BeApproximately(0, 0.0001);

        var half = TumbleDiveFade.At(Dive(0.125));
        half.ShowUpper.Should().BeTrue();
        half.Black.Should().BeApproximately(0.5, 0.0001);

        var black = TumbleDiveFade.At(Dive(0.25));
        black.ShowUpper.Should().BeFalse();
        black.Black.Should().BeApproximately(1, 0.0001);
    }

    [Test]
    public void The_floor_below_fades_in_for_the_rest_of_the_dive()
    {
        var mid = TumbleDiveFade.At(Dive(0.25 + 0.45 / 2));
        mid.ShowUpper.Should().BeFalse();
        mid.Black.Should().BeApproximately(0.5, 0.0001);

        TumbleDiveFade.At(Dive(0.7)).Black.Should().BeApproximately(0, 0.0001);
    }

    [Test]
    public void A_dive_into_darkness_stays_black()
    {
        TumbleDiveFade.At(Dive(0.6, intoDarkness: true)).Should().Be(new TumbleFadeFrame(false, 1));
    }

    [Test]
    public void A_chain_dive_fades_out_and_back_in_within_its_shorter_segment()
    {
        TumbleDiveFade.At(Dive(0.1, segment: 1)).Black.Should().BeApproximately(0.5, 0.0001);
        TumbleDiveFade.At(Dive(0.4, segment: 1)).Black.Should().BeApproximately(0, 0.0001);
    }
}
