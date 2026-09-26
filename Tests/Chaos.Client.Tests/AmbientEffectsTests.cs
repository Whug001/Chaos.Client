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
    public void ExtendedFlags_CoverEveryBitAboveTheLowByte()
        => ((ulong)AmbientEffects.EXTENDED_FLAGS).Should()
                                                  .Be(0xFFFF_FFFF_FFFF_FF00UL, "SetMapEffects carries every bit above the low byte");
}
