using Chaos.Client.ViewModel;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class CraneRevealStateTests
{
    private static CranePrizeRevealArgs Prize(byte tier = 0)
        => new()
        {
            Tier = tier,
            PrizeName = "Capsule"
        };

    private static CraneRevealState Started(byte tier = 0)
    {
        var state = new CraneRevealState();
        state.Start(Prize(tier));

        return state;
    }

    private static CraneRevealState FaceDown()
    {
        var state = Started();
        state.Advance(CraneRevealState.OPEN_MS + CraneRevealState.ARRIVE_MS);

        return state;
    }

    [Test]
    public void Start_EntersOpening()
    {
        var state = new CraneRevealState();
        state.Phase.Should().Be(CraneRevealPhase.Idle);

        var prize = Prize(2);
        state.Start(prize);

        state.Phase.Should().Be(CraneRevealPhase.Opening);
        state.Prize.Should().BeSameAs(prize);
    }

    [Test]
    public void Advance_OpeningThenArrivingThenFaceDown()
    {
        var state = Started();

        state.Advance(299);
        state.Phase.Should().Be(CraneRevealPhase.Opening);

        state.Advance(1);
        state.Phase.Should().Be(CraneRevealPhase.Arriving);

        state.Advance(299);
        state.Phase.Should().Be(CraneRevealPhase.Arriving);

        state.Advance(1);
        state.Phase.Should().Be(CraneRevealPhase.FaceDown);
    }

    [Test]
    public void Flip_IgnoredBeforeFaceDown()
    {
        var state = Started();

        state.Flip().Should().BeFalse();
        state.Phase.Should().Be(CraneRevealPhase.Opening);

        state.Advance(CraneRevealState.OPEN_MS);
        state.Flip().Should().BeFalse();
        state.Phase.Should().Be(CraneRevealPhase.Arriving);
    }

    [Test]
    public void Flip_FaceDownToFlippingToFaceUp()
    {
        var state = FaceDown();
        state.FlipProgress.Should().Be(0);

        state.Flip().Should().BeTrue();
        state.Phase.Should().Be(CraneRevealPhase.Flipping);
        state.Flip().Should().BeFalse();

        state.Advance(CraneRevealState.FLIP_MS / 2);
        state.Phase.Should().Be(CraneRevealPhase.Flipping);
        state.FlipProgress.Should().BeApproximately(0.5, 0.0001);

        state.Advance(CraneRevealState.FLIP_MS / 2);
        state.Phase.Should().Be(CraneRevealPhase.FaceUp);
        state.FlipProgress.Should().Be(1);
    }

    [Test]
    public void Close_IgnoredBeforeFaceUp()
    {
        var state = FaceDown();

        state.Close().Should().BeFalse();
        state.Flip();
        state.Close().Should().BeFalse();
        state.Phase.Should().Be(CraneRevealPhase.Flipping);

        state.Advance(CraneRevealState.FLIP_MS);
        state.Close().Should().BeTrue();
        state.Phase.Should().Be(CraneRevealPhase.Idle);
    }

    [Test]
    public void Start_WhileActive_Resets()
    {
        var state = FaceDown();
        state.Flip();
        state.Advance(CraneRevealState.FLIP_MS);

        var next = Prize(3);
        state.Start(next);

        state.Phase.Should().Be(CraneRevealPhase.Opening);
        state.Prize.Should().BeSameAs(next);
        state.FlipProgress.Should().Be(0);

        state.Advance(299);
        state.Phase.Should().Be(CraneRevealPhase.Opening);
    }

    [Test]
    public void TierLabel_MapsAllFour()
    {
        CraneRevealState.TierLabel(0).Should().Be("Common");
        CraneRevealState.TierLabel(1).Should().Be("Uncommon");
        CraneRevealState.TierLabel(2).Should().Be("Rare");
        CraneRevealState.TierLabel(3).Should().Be("Gold");
    }
}
