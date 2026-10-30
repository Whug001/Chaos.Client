using Chaos.Client.Definitions;
using Chaos.Client.Models;
using Chaos.Client.Rendering;
using Chaos.Client.Systems;
using Chaos.DarkAges.Definitions;
using Chaos.Geometry.Abstractions.Definitions;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

/// <summary>
///     A character riding the Magic Carpet (Unora accessory sprite 320) keeps its standing pose while it walks, so it
///     glides from tile to tile instead of stepping.
/// </summary>
public class CarpetGlideTests
{
    private static WorldEntity Walker(Direction direction, int accessorySprite, int idleFrames = 0)
        => new()
        {
            AnimState = EntityAnimState.Walking,
            Direction = direction,
            AnimFrameIndex = 2,
            IdleAnimFrameCount = idleFrames,
            IdleAnimTick = 11,
            Appearance = new AislingAppearance { Gender = Gender.Male, Accessory2Sprite = accessorySprite }
        };

    [Test]
    public void AWalkerWithoutTheCarpetSteps()
    {
        var frame = AnimationSystem.GetAislingFrame(Walker(Direction.Right, 0));

        frame.FrameIndex.Should().Be(8);
        frame.AnimSuffix.Should().Be("01");
    }

    [Test]
    public void ACarpetRiderHoldsTheStandingFrame()
    {
        AnimationSystem.GetAislingFrame(Walker(Direction.Right, AnimationSystem.MAGIC_CARPET_SPRITE))
                       .Should()
                       .Be((5, false, "01", true));

        AnimationSystem.GetAislingFrame(Walker(Direction.Left, AnimationSystem.MAGIC_CARPET_SPRITE))
                       .Should()
                       .Be((0, true, "01", false));
    }

    /// <summary>
    ///     With an idle sheet the rider plays it while moving, so the carpet's fringe keeps rippling.
    /// </summary>
    [Test]
    public void ACarpetRiderPlaysTheIdleLoopWhileMoving()
    {
        AnimationSystem.GetAislingFrame(Walker(Direction.Down, AnimationSystem.MAGIC_CARPET_SPRITE, 16))
                       .Should()
                       .Be((11, true, "04", true));
    }

    private static WorldEntity StridingRider(Direction direction, int accessorySprite)
    {
        var entity = new WorldEntity
        {
            Direction = direction,
            Appearance = new AislingAppearance { Gender = Gender.Male, Accessory2Sprite = accessorySprite }
        };

        AnimationSystem.StartWalk(entity, direction);

        return entity;
    }

    /// <summary>
    ///     The rider's sprite holds its standing pose while moving, so its slide must glide smoothly even when
    ///     the player picked the classic stepped walk-slide. Stepped offsets jump once per walk frame.
    /// </summary>
    [Test]
    public void ACarpetRiderGlidesSmoothlyUnderTheSteppedSetting()
    {
        var stepped = StridingRider(Direction.Right, AnimationSystem.MAGIC_CARPET_SPRITE);
        var smooth = StridingRider(Direction.Right, AnimationSystem.MAGIC_CARPET_SPRITE);

        AnimationSystem.Advance(stepped, 100f, WalkSlide.Stepped);
        AnimationSystem.Advance(smooth, 100f, WalkSlide.Exact);

        stepped.VisualOffset.Should().Be(smooth.VisualOffset);
        stepped.VisualOffset.Should().NotBe(Vector2.Zero);
    }

    [Test]
    public void AWalkerWithoutTheCarpetStillSteps()
    {
        var stepped = StridingRider(Direction.Right, 0);
        var smooth = StridingRider(Direction.Right, 0);

        AnimationSystem.Advance(stepped, 100f, WalkSlide.Stepped);
        AnimationSystem.Advance(smooth, 100f, WalkSlide.Exact);

        stepped.VisualOffset.Should().NotBe(smooth.VisualOffset);
    }

    [Test]
    public void AnyAccessorySlotCounts()
    {
        var entity = Walker(Direction.Up, 0);
        entity.Appearance = new AislingAppearance { Gender = Gender.Female, Accessory3Sprite = AnimationSystem.MAGIC_CARPET_SPRITE };

        AnimationSystem.GetAislingFrame(entity).FrameIndex.Should().Be(0);
    }

    /// <summary>
    ///     A mounted rider (the head form, body id 5) walks with the mount's steps. The mount art replaces the carpet, so
    ///     the carpet must not hold the standing pose.
    /// </summary>
    [Test]
    public void AMountedCarpetWearerSteps()
    {
        var entity = Walker(Direction.Right, AnimationSystem.MAGIC_CARPET_SPRITE, 16);
        entity.Appearance = entity.Appearance!.Value with { BodySpriteId = 5 };

        AnimationSystem.GetAislingFrame(entity)
                       .Should()
                       .Be((8, false, "01", true));
    }
}
