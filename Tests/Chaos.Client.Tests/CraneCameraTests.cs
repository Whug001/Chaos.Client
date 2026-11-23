using Chaos.Client.Systems;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class CraneCameraTests
{
    [Test]
    public void OnThePlateTheCameraCentresOnThePit()
    {
        CraneCamera.TryTarget(32004, 12, 12, out var x, out var y).Should().BeTrue();
        x.Should().Be(7.5f);
        y.Should().Be(7.5f);
    }

    [Test]
    public void OffThePlateTheCameraFollowsThePlayer()
        => CraneCamera.TryTarget(32004, 12, 11, out _, out _).Should().BeFalse();

    [Test]
    public void OnAnotherMapTheCameraFollowsThePlayer()
        => CraneCamera.TryTarget(729, 12, 12, out _, out _).Should().BeFalse();
}
