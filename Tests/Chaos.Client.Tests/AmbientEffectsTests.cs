using Chaos.Client.Rendering;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class AmbientEffectsTests
{
    //single-bit flags that AmbientEffects doesn't draw: map options, not effects. snow, rain and darkness sit below
    //bit 4 and have their own renderers
    private const MapFlags NOT_AMBIENT = MapFlags.NoTabMap | MapFlags.SnowTileset | MapFlags.NoTownMap;

    [Test]
    public void EveryEffectFlag_HasAnOverlay()
    {
        using var effects = new AmbientEffects();

        Enum.GetValues<MapFlags>()
            .Where(flag => ulong.IsPow2((ulong)flag) && ((ulong)flag >= (ulong)MapFlags.Fog))
            .Where(flag => !NOT_AMBIENT.HasFlag(flag))
            .Where(flag => !effects.CoveredFlags.Contains(flag))
            .Should()
            .BeEmpty("every ambient map flag needs at least one overlay in AmbientEffects");
    }

    [Test]
    public void Balloons_StayInsideTheWrapMargin()
        => (ParticleStyle.Balloons.SizeMax / ParticleRenderer.BALLOON_TEX_W * ParticleRenderer.BALLOON_TEX_H / 2f)
           .Should()
           .BeLessThanOrEqualTo(ParticleRenderer.WRAP_MARGIN, "a balloon that wraps while still on screen pops in and out");

    [Test]
    public void ParticleCount_KeepsDensityAtNormalSizes_AndCapsLargeViewports()
    {
        //reference viewport: the tuned count, unchanged
        ParticleRenderer.CappedCount(ParticleStyle.Embers, 640, 480).Should().Be(ParticleStyle.Embers.Count);
        ParticleRenderer.CappedCount(ParticleStyle.Ash, 640, 480).Should().Be(ParticleStyle.Ash.Count);

        //twice the linear size (four times the area): still full density
        ParticleRenderer.CappedCount(ParticleStyle.Embers, 1280, 960).Should().Be(ParticleStyle.Embers.Count * 4);

        //anything larger stops scaling: a 4k viewport would otherwise spawn ~27x the tuned count
        ParticleRenderer.CappedCount(ParticleStyle.Embers, 3840, 2160).Should().Be(ParticleStyle.Embers.Count * ParticleRenderer.MAX_AREA_SCALE);
        ParticleRenderer.CappedCount(ParticleStyle.BlizzardStreaks, 7680, 4320).Should().Be(ParticleStyle.BlizzardStreaks.Count * ParticleRenderer.MAX_AREA_SCALE);
    }

    [Test]
    public void FreshRenderers_HoldNoTextures_AndReleaseIsANoOp()
    {
        var mist = new MistRenderer(MistStyle.Fog);
        var particles = new ParticleRenderer(ParticleStyle.Ash);

        mist.HasAllocatedTextures.Should().BeFalse();
        particles.HasAllocatedTextures.Should().BeFalse();

        mist.ReleaseTextures();
        particles.ReleaseTextures();

        mist.HasAllocatedTextures.Should().BeFalse();
        particles.HasAllocatedTextures.Should().BeFalse();

        mist.Dispose();
        particles.Dispose();
    }

    [Test]
    public void SnappedApply_WithNoFlags_ReleasesInactiveOverlaysWithoutThrowing()
    {
        //the map-change path: everything off, textures dropped, nothing to draw
        using var effects = new AmbientEffects();
        effects.Apply(MapFlags.None, immediate: true);
        effects.ReleaseInactiveTextures();
    }

    [Test]
    public void ExtendedFlags_CoverEveryBitAboveTheLowByte()
        => ((ulong)AmbientEffects.EXTENDED_FLAGS).Should()
                                                  .Be(0xFFFF_FFFF_FFFF_FF00UL, "SetMapEffects carries every bit above the low byte");
}
