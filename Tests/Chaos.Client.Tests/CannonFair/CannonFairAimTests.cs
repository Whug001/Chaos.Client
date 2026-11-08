using Chaos.CannonFair;
using Chaos.Client.Controls.World.Popups.CannonFair;
using FluentAssertions;

namespace Chaos.Client.Tests.CannonFair;

public sealed class CannonFairAimTests
{
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
