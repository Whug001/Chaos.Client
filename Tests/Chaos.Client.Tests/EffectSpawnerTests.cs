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
        effects[0].CurrentFrame = 3;

        EffectSpawner.CreateEffect(effects, 1, 50, Info(), targetEntityId: 7);

        effects.Should().ContainSingle();
        effects[0].CurrentFrame.Should().Be(0);
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
