using Chaos.Client.Rendering;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class HalloweenPresetsTests
{
    //the F4 flicker section leaves Harvest Moon out because its sparks flicker slower than Embers, which that section
    //also leaves out. keep it that way
    [Test]
    public void HarvestSparks_FlickerSlowerThanEmbers()
        => ParticleStyle.HarvestSparks
                        .TwinkleFreqMax
                        .Should()
                        .BeLessThan(ParticleStyle.Embers.TwinkleFreqMin);

    [Test]
    public void GhostFrameAt_AlternatesAtTheHemRate()
    {
        ParticleRenderer.GhostFrameAt(0f, 0f)
                        .Should()
                        .Be(0);

        //0.5 s x 2.2 frames/s = 1.1 -> frame 1
        ParticleRenderer.GhostFrameAt(0.5f, 0f)
                        .Should()
                        .Be(1);

        //1 s x 2.2 = 2.2 -> frame 2, which wraps to 0
        ParticleRenderer.GhostFrameAt(1f, 0f)
                        .Should()
                        .Be(0);
    }

    [Test]
    public void GhostFrameAt_PhaseOffsetsTheRipple()
        => ParticleRenderer.GhostFrameAt(0f, 1.2f)
                           .Should()
                           .Be(1, "each ghost's phase shifts its hem so they don't ripple in step");

    [Test]
    public void Ghosts_FadeFullyOutBetweenAppearances()
        => ParticleStyle.Ghosts
                        .Twinkle
                        .Should()
                        .Be(1f, "a twinkle of 1 takes each ghost to zero opacity at the bottom of its fade");
}
