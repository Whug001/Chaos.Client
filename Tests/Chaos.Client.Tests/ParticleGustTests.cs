using Chaos.Client.Rendering;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class ParticleGustTests
{
    private const float TOLERANCE = 1e-4f;

    [Test]
    public void Gust_StaysInRange()
    {
        for (var t = 0.0; t <= 600.0; t += 0.05)
            ParticleRenderer.Gust(t)
                            .Should()
                            .BeInRange(0f, 1f, $"at {t:F2} s");
    }

    [Test]
    public void Gust_HasStrongGustsAndLulls()
    {
        var firstMinute = Enumerable.Range(0, 1200)
                                    .Select(i => ParticleRenderer.Gust(i * 0.05))
                                    .ToArray();

        firstMinute.Should()
                   .Contain(g => g > 0.62f, "the wind lines need a strong gust to show")
                   .And
                   .Contain(g => g < 0.4f, "the breeze needs lulls");
    }

    //formatter:off
    [Test]
    [Arguments(0f)]
    [Arguments(0.5f)]
    [Arguments(1f)]
    //formatter:on
    public void Defaults_LeaveSpeedAndAlphaUnchanged(float gust)
    {
        ParticleRenderer.GustSpeedFactor(ParticleStyle.Leaves, gust)
                        .Should()
                        .Be(1f);

        ParticleRenderer.GustAlphaFactor(ParticleStyle.Leaves, gust)
                        .Should()
                        .Be(1f);
    }

    [Test]
    public void SpeedFactor_LerpsBetweenLowAndHigh()
    {
        ParticleRenderer.GustSpeedFactor(ParticleStyle.ChaffFlecks, 0f)
                        .Should()
                        .BeApproximately(0.5f, TOLERANCE);

        ParticleRenderer.GustSpeedFactor(ParticleStyle.ChaffFlecks, 1f)
                        .Should()
                        .BeApproximately(1.67f, TOLERANCE);
    }

    [Test]
    public void AlphaGate_HidesBelowAndShowsAbove()
    {
        ParticleRenderer.GustAlphaFactor(ParticleStyle.ChaffWindLines, 0.6f)
                        .Should()
                        .Be(0f);

        ParticleRenderer.GustAlphaFactor(ParticleStyle.ChaffWindLines, 0.77f)
                        .Should()
                        .BeApproximately(1f, TOLERANCE);

        ParticleRenderer.GustAlphaFactor(ParticleStyle.ChaffWindLines, 0.695f)
                        .Should()
                        .BeGreaterThan(0f)
                        .And
                        .BeLessThan(1f);
    }

    [Test]
    public void ChaffPresets_MatchSpec()
    {
        ParticleStyle.ChaffFlecks.Count.Should().Be(70);
        ParticleStyle.ChaffWindLines.Count.Should().Be(6);
        ParticleStyle.ChaffHeads.Count.Should().Be(2);

        ParticleStyle.ChaffFlecks.Shape.Should().Be(ParticleShape.Streak);
        ParticleStyle.ChaffWindLines.Shape.Should().Be(ParticleShape.Streak);
        ParticleStyle.ChaffHeads.Shape.Should().Be(ParticleShape.WheatHead);

        ParticleStyle.ChaffHeads.Spin.Should().Be(7f);
        ParticleStyle.ChaffWindLines.GustGate.Should().Be(0.62f);
    }

    [Test]
    public void ChaffMist_MatchesSpec()
    {
        MistStyle.Chaff.WashAlpha.Should().Be(0.07f);
        MistStyle.Chaff.Seed.Should().Be(5050);
        MistStyle.Chaff.Layers.Should().ContainSingle();
    }
}
