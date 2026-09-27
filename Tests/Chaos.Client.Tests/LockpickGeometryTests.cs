using Chaos.Client.Controls.World.Popups.Lockpicking;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class LockpickGeometryTests
{
    [Test]
    public async Task Straight_up_is_90()
    {
        LockpickGeometry.PickDegreesFrom(0, -50).Should().BeApproximately(90f, 0.01f);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Left_is_0_and_right_is_180()
    {
        LockpickGeometry.PickDegreesFrom(-50, 0).Should().BeApproximately(0f, 0.01f);
        LockpickGeometry.PickDegreesFrom(50, 0).Should().BeApproximately(180f, 0.01f);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Up_left_is_45()
    {
        LockpickGeometry.PickDegreesFrom(-50, -50).Should().BeApproximately(45f, 0.01f);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Below_the_centre_snaps_to_the_nearer_end()
    {
        LockpickGeometry.PickDegreesFrom(-50, 10).Should().Be(0f);
        LockpickGeometry.PickDegreesFrom(50, 10).Should().Be(180f);
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_centre_is_90()
    {
        LockpickGeometry.PickDegreesFrom(0, 0).Should().Be(90f);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Cylinder_frames_run_0_to_18()
    {
        LockpickGeometry.CylinderFrame(0f).Should().Be(0);
        LockpickGeometry.CylinderFrame(47f).Should().Be(9);
        LockpickGeometry.CylinderFrame(90f).Should().Be(18);
        LockpickGeometry.CylinderFrame(120f).Should().Be(18);
        await Task.CompletedTask;
    }
}
