using Chaos.Client.Data.Definitions;
using Chaos.Client.Models;
using Chaos.Client.Systems;
using FluentAssertions;

namespace Chaos.Client.Tests;

[NotInParallel]
public class EffectSpawnerTests
{
    private static (int FrameCount, int FileIntervalMs, bool IsEfa, EffectBlendMode BlendMode) Info(int frames = 4)
        => (frames, 100, false, EffectBlendMode.Normal);

    [Test]
    public void SameEffectOnSameEntityTwiceLeavesOneEntry()
    {
        var effects = new List<Animation>();
        EffectSpawner.CreateEffect(effects, 1, 50, Info(), targetEntityId: 7);
        effects[0].CurrentFrame = 1;

        EffectSpawner.CreateEffect(effects, 1, 50, Info(), targetEntityId: 7);

        effects.Should().ContainSingle();
        effects[0].CurrentFrame.Should().Be(0);
    }

    [Test]
    public void SameEffectOnSameTileTwiceLeavesOneEntry()
    {
        //the crane redraws its hovering claw on one tile before the last copy ends; a repeat restarts it, never stacks
        var effects = new List<Animation>();
        EffectSpawner.CreateEffect(effects, 449, 100, Info(), tileX: 7, tileY: 7, restartOnTile: true);
        effects[0].CurrentFrame = 3;

        EffectSpawner.CreateEffect(effects, 449, 100, Info(), tileX: 7, tileY: 7, restartOnTile: true);

        effects.Should().ContainSingle();
        effects[0].CurrentFrame.Should().Be(0);
    }

    [Test]
    public void SameEffectOnSameTileTwiceStacksOutsideTheArcade()
    {
        //a spell cast again on the same tile (an Elementalist's summon) must not cut the last cast's animation short
        var effects = new List<Animation>();
        EffectSpawner.CreateEffect(effects, 449, 100, Info(), tileX: 7, tileY: 7);
        effects[0].CurrentFrame = 1;

        EffectSpawner.CreateEffect(effects, 449, 100, Info(), tileX: 7, tileY: 7);

        effects.Should().HaveCount(2);
        effects[0].CurrentFrame.Should().Be(1);
    }

    [Test]
    public void RepeatAsTileCopyFinishesLoopsItInsteadOfStacking()
    {
        //a hole the server re-sends just before its 1s art ends: one copy, looping, never two drawn over each other
        var effects = new List<Animation>();
        EffectSpawner.CreateEffect(effects, 434, 100, Info(10), tileX: 7, tileY: 7);
        EffectSpawner.AdvanceEffects(effects, 850);

        EffectSpawner.CreateEffect(effects, 434, 100, Info(10), tileX: 7, tileY: 7);

        effects.Should().ContainSingle();
        effects[0].LoopOnce.Should().BeTrue();

        EffectSpawner.AdvanceEffects(effects, 200);

        effects.Should().ContainSingle();
        effects[0].CurrentFrame.Should().Be(0);
        effects[0].LoopOnce.Should().BeFalse();

        EffectSpawner.AdvanceEffects(effects, 1000);

        effects.Should().BeEmpty();
    }

    [Test]
    public void RepeatAsEntityCopyFinishesLoopsItInsteadOfRestarting()
    {
        var effects = new List<Animation>();
        EffectSpawner.CreateEffect(effects, 49, 100, Info(8), targetEntityId: 7);
        EffectSpawner.AdvanceEffects(effects, 700);

        EffectSpawner.CreateEffect(effects, 49, 100, Info(8), targetEntityId: 7);

        effects.Should().ContainSingle();
        effects[0].CurrentFrame.Should().Be(7);
        effects[0].LoopOnce.Should().BeTrue();
    }

    [Test]
    public void SecondRepeatInOneLoopStacks()
    {
        //a copy already set to loop once is not asked twice; the extra repeat is treated like any other
        var effects = new List<Animation>();
        EffectSpawner.CreateEffect(effects, 434, 100, Info(10), tileX: 7, tileY: 7);
        EffectSpawner.AdvanceEffects(effects, 850);
        EffectSpawner.CreateEffect(effects, 434, 100, Info(10), tileX: 7, tileY: 7);

        EffectSpawner.CreateEffect(effects, 434, 100, Info(10), tileX: 7, tileY: 7);

        effects.Should().HaveCount(2);
    }

    [Test]
    public void ArcadeRepeatStillRestarts()
    {
        var effects = new List<Animation>();
        EffectSpawner.CreateEffect(effects, 449, 100, Info(10), tileX: 7, tileY: 7, restartOnTile: true);
        EffectSpawner.AdvanceEffects(effects, 850);

        EffectSpawner.CreateEffect(effects, 449, 100, Info(10), tileX: 7, tileY: 7, restartOnTile: true);

        effects.Should().ContainSingle();
        effects[0].CurrentFrame.Should().Be(0);
        effects[0].LoopOnce.Should().BeFalse();
    }

    [Test]
    public void SameEffectOnOtherTilesStays()
    {
        var effects = new List<Animation>();
        EffectSpawner.CreateEffect(effects, 449, 100, Info(), tileX: 7, tileY: 7);
        EffectSpawner.CreateEffect(effects, 449, 100, Info(), tileX: 8, tileY: 7);
        EffectSpawner.CreateEffect(effects, 450, 100, Info(), tileX: 7, tileY: 7);

        effects.Should().HaveCount(3);
    }

    [Test]
    public void ThirdEffectOnOneEntityEvictsOldest()
    {
        var previous = ClientSettings.MaxEffectAnimationsPerEntity;
        ClientSettings.MaxEffectAnimationsPerEntity = 2;

        try
        {
            var effects = new List<Animation>();
            EffectSpawner.CreateEffect(effects, 1, 50, Info(), targetEntityId: 7);
            EffectSpawner.CreateEffect(effects, 2, 50, Info(), targetEntityId: 7);
            EffectSpawner.CreateEffect(effects, 3, 50, Info(), targetEntityId: 7);

            effects.Select(e => e.EffectId).Should().Equal(2, 3);
        }
        finally
        {
            ClientSettings.MaxEffectAnimationsPerEntity = previous;
        }
    }

    [Test]
    public void EffectsOnOtherEntitiesDoNotEvict()
    {
        var previous = ClientSettings.MaxEffectAnimationsPerEntity;
        ClientSettings.MaxEffectAnimationsPerEntity = 2;

        try
        {
            var effects = new List<Animation>();
            EffectSpawner.CreateEffect(effects, 1, 50, Info(), targetEntityId: 7);
            EffectSpawner.CreateEffect(effects, 2, 50, Info(), targetEntityId: 8);
            EffectSpawner.CreateEffect(effects, 3, 50, Info(), targetEntityId: 8);

            effects.Select(e => e.EffectId).Should().Equal(1, 2, 3);
        }
        finally
        {
            ClientSettings.MaxEffectAnimationsPerEntity = previous;
        }
    }

    [Test]
    public void CapZeroAddsNothing()
    {
        var previous = ClientSettings.MaxEffectAnimationsPerEntity;
        ClientSettings.MaxEffectAnimationsPerEntity = 0;

        try
        {
            var effects = new List<Animation>();
            EffectSpawner.CreateEffect(effects, 1, 50, Info(), targetEntityId: 7);

            effects.Should().BeEmpty();
        }
        finally
        {
            ClientSettings.MaxEffectAnimationsPerEntity = previous;
        }
    }

    [Test]
    public void TileEffectIsNeverCapped()
    {
        var previous = ClientSettings.MaxEffectAnimationsPerEntity;
        ClientSettings.MaxEffectAnimationsPerEntity = 0;

        try
        {
            var effects = new List<Animation>();
            EffectSpawner.CreateEffect(effects, 1, 50, Info(), tileX: 3, tileY: 4);

            effects.Should().ContainSingle();
            effects[0].TileX.Should().Be(3);
            effects[0].TileY.Should().Be(4);
        }
        finally
        {
            ClientSettings.MaxEffectAnimationsPerEntity = previous;
        }
    }
}
