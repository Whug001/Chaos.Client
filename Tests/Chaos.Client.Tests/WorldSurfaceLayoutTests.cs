using Chaos.Client.Rendering;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public class WorldSurfaceLayoutTests
{
    [Test]
    public void AUniformScaleKeepsTheRatio()
    {
        var layout = WorldSurfaceLayout.From(new Rectangle(0, 0, 2560, 1920), new Rectangle(0, 0, 640, 330), 640, 480);

        layout.Destination.Should().Be(new Rectangle(0, 0, 2560, 1320));
        layout.ScaleX.Should().Be(4f);
        layout.ScaleY.Should().Be(4f);
    }

    [Test]
    public void AStretchedWindowScalesEachAxis()
    {
        var layout = WorldSurfaceLayout.From(new Rectangle(0, 0, 2560, 1350), new Rectangle(0, 0, 640, 480), 640, 480);

        layout.Destination.Should().Be(new Rectangle(0, 0, 2560, 1350));
        layout.ScaleX.Should().Be(4f);
        layout.ScaleY.Should().Be(2.8125f);
    }

    [Test]
    public void LetterboxBarsOffsetTheDestination()
    {
        var layout = WorldSurfaceLayout.From(new Rectangle(160, 0, 1440, 1080), new Rectangle(8, 16, 624, 320), 640, 480);

        layout.Destination.Should().Be(new Rectangle(178, 36, 1404, 720));
        layout.ScaleX.Should().Be(2.25f);
        layout.ScaleY.Should().Be(2.25f);
        layout.Viewport.Should().Be(new Rectangle(8, 16, 624, 320));
    }

    [Test]
    public void TheMatrixMapsTheViewportOntoTheWholeTarget()
    {
        var viewport = new Rectangle(8, 16, 624, 320);
        var layout = WorldSurfaceLayout.From(new Rectangle(160, 0, 1440, 1080), viewport, 640, 480);

        Vector2.Transform(new Vector2(viewport.Left, viewport.Top), layout.SurfaceMatrix).Should().Be(Vector2.Zero);
        Vector2.Transform(new Vector2(viewport.Right, viewport.Bottom), layout.SurfaceMatrix)
               .Should()
               .Be(new Vector2(layout.Destination.Width, layout.Destination.Height));
    }
}
