using Chaos.Client.Rendering;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public class FlyByRendererTests
{
    private static readonly GameTime Tick = new(TimeSpan.Zero, TimeSpan.FromSeconds(0.05));
    private static readonly Rectangle Viewport = new(0, 0, 640, 480);

    private static void Step(FlyByRenderer renderer) => renderer.Update(Tick, Viewport, Vector2.Zero);

    //switches the bats on and runs until a group is in the air
    private static FlyByRenderer WithGroupUp()
    {
        var renderer = new FlyByRenderer(FlyByStyle.Bats);
        renderer.SetActive(true);

        for (var i = 0; (i < 400) && (renderer.FlyerCount == 0); i++)
            Step(renderer);

        renderer.FlyerCount.Should().BeGreaterThan(0, "the setup needs a group in the air");

        return renderer;
    }

    [Test]
    public void ImmediateOnWhileActive_KeepsFlyers()
    {
        var renderer = WithGroupUp();

        renderer.SetActive(true, true);

        renderer.FlyerCount.Should().BeGreaterThan(0);
    }

    [Test]
    public void ImmediateOff_ClearsFlyers()
    {
        var renderer = WithGroupUp();

        renderer.SetActive(false, true);

        renderer.FlyerCount.Should().Be(0);
        renderer.IsActive.Should().BeFalse();
    }

    [Test]
    public void OnAfterFullFadeOut_ClearsStaleFlyers()
    {
        var renderer = WithGroupUp();

        renderer.SetActive(false);

        for (var i = 0; (i < 400) && renderer.IsActive; i++)
            Step(renderer);

        renderer.IsActive.Should().BeFalse();

        renderer.SetActive(true);

        renderer.FlyerCount.Should().Be(0);
    }

    [Test]
    public void OffWhileFading_KeepsFlyersUntilFaded()
    {
        var renderer = WithGroupUp();

        renderer.SetActive(false);
        Step(renderer);

        renderer.IsActive.Should().BeTrue();
        renderer.FlyerCount.Should().BeGreaterThan(0);
    }
}
