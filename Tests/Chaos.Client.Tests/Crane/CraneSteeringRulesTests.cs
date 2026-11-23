using Chaos.Client.ViewModel;
using Chaos.Geometry.Abstractions.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class CraneSteeringRulesTests
{
    [Test]
    [Arguments(CraneInput.Arrow)]
    [Arguments(CraneInput.Space)]
    [Arguments(CraneInput.PointerMove)]
    [Arguments(CraneInput.QueuedOrPathStep)]
    public void NotSteering_EveryInputBehavesAsUsual(CraneInput input)
        => CraneSteeringRules.Decide(false, input).Should().Be(CraneInputOutcome.Normal);

    [Test]
    public void Steering_ArrowsAreSentWithoutLocalPrediction()
        => CraneSteeringRules.Decide(true, CraneInput.Arrow).Should().Be(CraneInputOutcome.SendOnly);

    [Test]
    public void Steering_SpaceIsSentWithoutLocalPrediction()
        => CraneSteeringRules.Decide(true, CraneInput.Space).Should().Be(CraneInputOutcome.SendOnly);

    [Test]
    public void Steering_PointerMovementIsSuppressed()
        => CraneSteeringRules.Decide(true, CraneInput.PointerMove).Should().Be(CraneInputOutcome.Suppress);

    [Test]
    public void Steering_QueuedWalksAndPathStepsAreSuppressed()
        => CraneSteeringRules.Decide(true, CraneInput.QueuedOrPathStep).Should().Be(CraneInputOutcome.Suppress);

    [Test]
    [Arguments(Direction.Up, CraneKey.Up)]
    [Arguments(Direction.Right, CraneKey.Right)]
    [Arguments(Direction.Down, CraneKey.Down)]
    [Arguments(Direction.Left, CraneKey.Left)]
    public void KeyFor_MapsEachDirectionToItsKeycap(Direction direction, CraneKey key)
        => CraneSteeringRules.KeyFor(direction).Should().Be(key);

    [Test]
    public void ShouldSendSteer_FirstPressAlwaysGoes()
        => CraneSteeringRules.ShouldSendSteer(5_000, 0).Should().BeTrue();

    [Test]
    public void ShouldSendSteer_AHeldKeyIsPaced()
    {
        CraneSteeringRules.ShouldSendSteer(1_000 + CraneSteeringRules.STEER_SEND_INTERVAL_MS - 1, 1_000).Should().BeFalse();
        CraneSteeringRules.ShouldSendSteer(1_000 + CraneSteeringRules.STEER_SEND_INTERVAL_MS, 1_000).Should().BeTrue();
    }

    [Test]
    public void SteerSendInterval_IsFasterThanTheClaw()
        => CraneSteeringRules.STEER_SEND_INTERVAL_MS.Should().BeLessThan(1000 / 4, "the claw's own pace (3.5 steps a second by default) stays the limit");
}
