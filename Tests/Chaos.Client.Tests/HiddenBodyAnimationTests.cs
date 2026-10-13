using Chaos.Client.Definitions;
using Chaos.Client.Models;
using Chaos.Client.Rendering;
using Chaos.Client.Systems;
using Chaos.DarkAges.Definitions;
using Chaos.Geometry.Abstractions.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

/// <summary>
///     A hidden body animation (your own, with "Hide my own body animations" on or "Show body animations" off) keeps its
///     full timing, so the player stays in place as long as a shown one would, but draws the standing pose.
/// </summary>
public class HiddenBodyAnimationTests
{
    private static WorldEntity Standing()
        => new()
        {
            Direction = Direction.Right,
            Appearance = new AislingAppearance { Gender = Gender.Male }
        };

    [Test]
    public void AHiddenAttackDrawsTheStandingPose()
    {
        var entity = Standing();

        AnimationSystem.StartBodyAnimation(entity, BodyAnimation.Assail, 25, hidden: true);
        AnimationSystem.Advance(entity, 30);

        AnimationSystem.GetAislingFrame(entity)
                       .Should()
                       .Be((5, false, "01", true));
    }

    [Test]
    public void AHiddenAttackStillHoldsThePlayerInPlace()
    {
        var entity = Standing();

        AnimationSystem.StartBodyAnimation(entity, BodyAnimation.Assail, 25, hidden: true);

        entity.AnimState.Should().Be(EntityAnimState.BodyAnim);
        entity.IsAtRest.Should().BeFalse();
    }

    [Test]
    public void AHiddenAttackEndsAtTheSameTimeAsAShownOne()
    {
        var hidden = Standing();
        var shown = Standing();

        AnimationSystem.StartBodyAnimation(hidden, BodyAnimation.Assail, 25, hidden: true);
        AnimationSystem.StartBodyAnimation(shown, BodyAnimation.Assail, 25);

        var total = shown.AnimFrameCount * shown.AnimFrameIntervalMs;

        AnimationSystem.Advance(hidden, total - 1);
        AnimationSystem.Advance(shown, total - 1);
        hidden.IsAtRest.Should().Be(shown.IsAtRest).And.BeFalse();

        AnimationSystem.Advance(hidden, 2);
        AnimationSystem.Advance(shown, 2);
        hidden.IsAtRest.Should().BeTrue();
        shown.IsAtRest.Should().BeTrue();
        hidden.BodyAnimHidden.Should().BeFalse();
    }

    [Test]
    public void AShownAttackDrawsTheAttackFrames()
    {
        var entity = Standing();

        AnimationSystem.StartBodyAnimation(entity, BodyAnimation.Assail, 25);

        AnimationSystem.GetAislingFrame(entity)
                       .AnimSuffix
                       .Should()
                       .NotBe("04");

        entity.BodyAnimHidden.Should().BeFalse();
    }

    [Test]
    public void ShowBodyAnimationsOffHidesYourEmotesToo()
    {
        AnimationSystem.HidesOwnAnimation(isEmote: true, hideOwnBodyAnimations: false, showBodyAnimations: false).Should().BeTrue();
        AnimationSystem.HidesOwnAnimation(isEmote: false, hideOwnBodyAnimations: false, showBodyAnimations: false).Should().BeTrue();
    }

    [Test]
    public void HideMyOwnBodyAnimationsLeavesYourEmotesShowing()
    {
        AnimationSystem.HidesOwnAnimation(isEmote: true, hideOwnBodyAnimations: true, showBodyAnimations: true).Should().BeFalse();
        AnimationSystem.HidesOwnAnimation(isEmote: false, hideOwnBodyAnimations: true, showBodyAnimations: true).Should().BeTrue();
        AnimationSystem.HidesOwnAnimation(isEmote: false, hideOwnBodyAnimations: false, showBodyAnimations: true).Should().BeFalse();
    }

    [Test]
    public void AHiddenEmoteRunsItsTimeButDrawsNoFrame()
    {
        var entity = Standing();

        entity.StartEmoteOverlay(4, 2, 1000, hidden: true);

        entity.ActiveEmoteFrame.Should().Be(4);
        entity.IsAtRest.Should().BeFalse();
        entity.VisibleEmoteFrame.Should().Be(-1);

        entity.StartEmoteOverlay(4, 2, 1000);

        entity.VisibleEmoteFrame.Should().Be(4);
    }
}
