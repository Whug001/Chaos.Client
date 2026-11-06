#region
using Chaos.Client.Collections;
using Chaos.Client.Rendering;
using Chaos.Client.Systems;
using FluentAssertions;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Tests;

public class GiantLookTests
{
    [Test]
    public void Giant_hitbox_is_scaled()
    {
        var baseRect = new Rectangle(100, 200, 28, 60);
        var r = EntityLookMath.HitBox(baseRect, 2f);
        r.Width.Should().Be(56);
        r.Height.Should().Be(120);
        r.Bottom.Should().Be(baseRect.Bottom);
        r.Center.X.Should().Be(baseRect.Center.X);
    }

    [Test]
    public void Overlay_offset_scales() => EntityLookMath.OverlayOffset(64, 2f).Should().Be(128);

    [Test]
    public void Overlay_anchor_rises_when_floating()
    {
        EntityLookMath.OverlayAnchorY(64, 2f, false, 1.0).Should().Be(128f);
        EntityLookMath.OverlayAnchorY(64, 2f, true, 1.0)
            .Should()
            .BeApproximately(128f + EntityLookMath.FloatRaise(1.0), 0.001f);
    }

    [Test]
    public void Shake_is_bounded_and_ends()
    {
        var shake = new GiantStepShake();
        var rng = new Random(1);
        shake.OffsetAt(0, rng).Should().Be(Vector2.Zero);
        shake.Trigger(10);

        for (var t = 10.0; t < 10.15; t += 0.01)
        {
            var o = shake.OffsetAt(t, rng);
            o.X.Should().BeInRange(-2f, 2f);
            o.Y.Should().BeInRange(-2f, 2f);
        }

        shake.OffsetAt(10.16, rng).Should().Be(Vector2.Zero);
    }

    [Test]
    public void ScreenToWorld_ignores_shake()
    {
        var cam = new Camera(640, 480) { Position = new Vector2(500, 500) };
        var before = cam.ScreenToWorld(new Vector2(320, 240));
        cam.ShakeOffset = new Vector2(2, -2);
        cam.ScreenToWorld(new Vector2(320, 240)).Should().Be(before);
    }
}
