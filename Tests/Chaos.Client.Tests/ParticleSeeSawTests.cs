using Chaos.Client.Rendering;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public class ParticleSeeSawTests
{
    private const float TOLERANCE = 1e-5f;

    [Test]
    public void Tilt_IsZeroAtTheEndsOfTheSwing()
    {
        ParticleRenderer.SeeSawTilt(0.55f, 0f)
                        .Should()
                        .BeApproximately(0f, TOLERANCE);

        ParticleRenderer.SeeSawTilt(0.55f, MathF.PI)
                        .Should()
                        .BeApproximately(0f, TOLERANCE);
    }

    [Test]
    public void Tilt_PeaksMidSwing_FollowingTheSidewaysVelocity()
    {
        ParticleRenderer.SeeSawTilt(0.55f, MathF.PI / 2)
                        .Should()
                        .BeApproximately(0.55f, TOLERANCE);

        ParticleRenderer.SeeSawTilt(0.55f, 3 * MathF.PI / 2)
                        .Should()
                        .BeApproximately(-0.55f, TOLERANCE);
    }

    [Test]
    public void Drop_IsSlowAtTheEnds_FastMidSwing()
    {
        ParticleRenderer.SeeSawDrop(0f)
                        .Should()
                        .BeApproximately(0.5f, TOLERANCE);

        ParticleRenderer.SeeSawDrop(MathF.PI / 2)
                        .Should()
                        .BeApproximately(1.4f, TOLERANCE);
    }

    [Test]
    public void ExistingPresets_HaveNoSeeSaw()
    {
        ParticleStyle.Leaves
                     .SeeSaw
                     .Should()
                     .Be(0f);

        ParticleStyle.Petals
                     .SeeSaw
                     .Should()
                     .Be(0f);
    }

    [Test]
    public void TurkeyFeathers_MatchesSpec()
    {
        var style = ParticleStyle.TurkeyFeathers;

        style.Count
             .Should()
             .Be(10);

        style.Shape
             .Should()
             .Be(ParticleShape.Feather);

        style.SeeSaw
             .Should()
             .Be(0.55f);

        style.SizeMin
             .Should()
             .Be(11f);

        style.SizeMax
             .Should()
             .Be(14f);

        style.Sway
             .Should()
             .Be(new Vector2(40f, 0f));
    }
}
