using Chaos.CannonFair;
using Chaos.Client.Controls.World.Popups.CannonFair;
using FluentAssertions;

namespace Chaos.Client.Tests.CannonFair;

public sealed class CannonFairAimTests
{
    [Test]
    public void Dragging_down_and_left_aims_up_and_right()
    {
        var aim = new CannonFairAim();

        aim.Drag(100f, 100f, 50f, 150f);

        aim.AngleDegrees.Should().BeApproximately(45f, 0.01f);
        aim.Power.Should().BeApproximately(70.71f / CannonFairAim.MAX_DRAG_PIXELS, 0.01f);
    }

    [Test]
    public void Dragging_past_the_limit_clamps_power_and_angle()
    {
        var aim = new CannonFairAim();

        aim.Drag(100f, 100f, 400f, 100f);

        aim.AngleDegrees.Should().Be(CannonFairRules.MAX_ANGLE);
        aim.Power.Should().Be(CannonFairRules.MAX_POWER);
    }

    [Test]
    public void A_zero_length_drag_keeps_the_current_aim()
    {
        var aim = new CannonFairAim();
        var before = aim.AngleDegrees;

        aim.Drag(100f, 100f, 100f, 100f);

        aim.AngleDegrees.Should().Be(before);
    }

    [Test]
    public void Nudge_stays_inside_the_legal_range()
    {
        var aim = new CannonFairAim();

        aim.Nudge(500f, -5f);

        aim.AngleDegrees.Should().Be(CannonFairRules.MAX_ANGLE);
        aim.Power.Should().Be(CannonFairRules.MIN_POWER);
    }

    [Test]
    public void Path_starts_at_the_cannon()
    {
        var point = new CannonFairAim().Path(3f, 0f, ShotKind.Plain);

        point.X.Should().Be(3f);
        point.Y.Should().Be(CannonFairRules.CANNON_Y);
    }
}
