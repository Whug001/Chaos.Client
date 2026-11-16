using Chaos.Client.Rendering;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public class ParticleRendererMotionTests
{
    private static readonly Rectangle Viewport = new(0, 0, 640, 480);

    private static ParticleRenderer Active(ParticleStyle style)
    {
        var renderer = new ParticleRenderer(style);
        renderer.SetActive(true, true);

        return renderer;
    }

    [Test]
    public void GustLevel_ComesFromTotalGameTime_NotTheRenderersOwnClock()
    {
        var total = TimeSpan.FromSeconds(4.1);
        var flecks = Active(ParticleStyle.ChaffFlecks);
        var windLines = Active(ParticleStyle.ChaffWindLines);

        //the wind lines have been running for longer, in different-sized frames
        for (var i = 1; i <= 30; i++)
            windLines.Update(new GameTime(TimeSpan.FromSeconds(i * 0.1), TimeSpan.FromSeconds(0.1)), Viewport, Vector2.Zero);

        flecks.Update(new GameTime(total, TimeSpan.FromSeconds(0.016)), Viewport, Vector2.Zero);
        windLines.Update(new GameTime(total, TimeSpan.FromSeconds(0.05)), Viewport, Vector2.Zero);

        flecks.CurrentGustLevel
              .Should()
              .Be(ParticleRenderer.Gust(total.TotalSeconds));

        windLines.CurrentGustLevel
                 .Should()
                 .Be(flecks.CurrentGustLevel, "the chaff layers gust together");
    }

    [Test]
    public void SeeSawParticles_TiltWithinTheSeeSawAngle_InsteadOfSpinning()
    {
        var feathers = Active(ParticleStyle.TurkeyFeathers);

        for (var i = 1; i <= 40; i++)
        {
            feathers.Update(new GameTime(TimeSpan.FromSeconds(i * 0.05), TimeSpan.FromSeconds(0.05)), Viewport, Vector2.Zero);

            feathers.ParticleRotations
                    .Should()
                    .NotBeEmpty()
                    .And
                    .OnlyContain(rotation => MathF.Abs(rotation) <= ParticleStyle.TurkeyFeathers.SeeSaw + 0.0001f);
        }

        //over two seconds the swing carries the feathers through different tilts, both ways
        feathers.ParticleRotations
                .Should()
                .Contain(rotation => rotation < 0f)
                .And
                .Contain(rotation => rotation > 0f);
    }
}
