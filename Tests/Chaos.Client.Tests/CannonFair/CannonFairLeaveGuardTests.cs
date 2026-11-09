using Chaos.Client.Controls.World.Popups.CannonFair;
using FluentAssertions;

namespace Chaos.Client.Tests.CannonFair;

public sealed class CannonFairLeaveGuardTests
{
    [Test]
    public void The_first_press_only_warns()
        => new CannonFairLeaveGuard().Press(0).Should().BeFalse();

    [Test]
    public void A_second_press_within_three_seconds_leaves()
    {
        var guard = new CannonFairLeaveGuard();
        guard.Press(0);

        guard.Press(2999).Should().BeTrue();
    }

    [Test]
    public void A_late_second_press_only_warns_again()
    {
        var guard = new CannonFairLeaveGuard();
        guard.Press(0);

        guard.Press(3001).Should().BeFalse();
        guard.Warning(3002).Should().BeTrue();
    }
}
