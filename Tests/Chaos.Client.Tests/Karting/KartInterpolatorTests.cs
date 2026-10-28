#region
using Chaos.Client.Systems.Karting;
using Chaos.DarkAges.Karting;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
#endregion

namespace Chaos.Client.Tests.Karting;

public class KartInterpolatorTests
{
    private static KartRacerSnapshot Snap(byte index, double x, double heading = 0, double vx = 0)
        => new() { Index = index, X = x, Y = 20, Heading = heading, Vx = vx };

    [Test]
    public void A_kart_between_two_snapshots_is_blended()
    {
        var interpolator = new KartInterpolator();
        interpolator.Add(10, [Snap(0, 0)]);
        interpolator.Add(12, [Snap(0, 2)]);

        var body = interpolator.Sample(0, 14)!.Value;

        body.X.Should().BeApproximately(1, 1e-9);
        body.Y.Should().BeApproximately(20, 1e-9);
    }

    [Test]
    public void The_heading_turns_the_short_way_across_zero()
    {
        var interpolator = new KartInterpolator();
        interpolator.Add(10, [Snap(0, 0, 6.2)]);
        interpolator.Add(12, [Snap(0, 0, 0.1)]);

        var body = interpolator.Sample(0, 14)!.Value;

        body.Heading.Should().BeApproximately(KartPhysics.NormalizeHeading(6.2 + (0.1 + 2 * Math.PI - 6.2) / 2), 1e-9);
    }

    [Test]
    public void Before_the_oldest_snapshot_the_oldest_is_drawn()
    {
        var interpolator = new KartInterpolator();
        interpolator.Add(10, [Snap(0, 5)]);
        interpolator.Add(12, [Snap(0, 7)]);

        interpolator.Sample(0, 11)!.Value.X.Should().BeApproximately(5, 1e-9);
    }

    [Test]
    public void Past_the_newest_snapshot_the_kart_carries_on_then_holds()
    {
        var interpolator = new KartInterpolator();
        interpolator.Add(10, [Snap(0, 12, vx: 9)]);

        interpolator.Sample(0, 14.5)!.Value.X.Should().BeApproximately(12.45, 1e-9);
        interpolator.Sample(0, 20)!.Value.X.Should().BeApproximately(12.9, 1e-9);
    }

    [Test]
    public void A_snapshot_that_is_not_newer_is_ignored()
    {
        var interpolator = new KartInterpolator();
        interpolator.Add(12, [Snap(0, 2)]);
        interpolator.Add(10, [Snap(0, 100)]);
        interpolator.Add(12, [Snap(0, 50)]);

        interpolator.Sample(0, 15)!.Value.X.Should().BeApproximately(2, 1e-9);
    }

    [Test]
    public void A_jump_of_more_than_three_tiles_is_not_blended()
    {
        var interpolator = new KartInterpolator();
        interpolator.Add(10, [Snap(0, 0)]);
        interpolator.Add(12, [Snap(0, 10)]);

        interpolator.Sample(0, 14)!.Value.X.Should().BeApproximately(10, 1e-9);
    }

    [Test]
    public void An_unknown_kart_has_no_body()
    {
        var interpolator = new KartInterpolator();
        interpolator.Sample(0, 14).Should().BeNull();

        interpolator.Add(10, [Snap(0, 0)]);
        interpolator.Sample(5, 14).Should().BeNull();

        interpolator.Clear();
        interpolator.Sample(0, 14).Should().BeNull();
    }

    [Test]
    public void The_buffer_keeps_the_newest_eight_snapshots()
    {
        var interpolator = new KartInterpolator();

        for (uint tick = 0; tick < 10; tick++)
            interpolator.Add(tick * 2, [Snap(0, tick)]);

        interpolator.Sample(0, 0)!.Value.X.Should().BeApproximately(2, 1e-9);
    }

    [Test]
    public void An_unknown_kart_after_clear_returns_null()
    {
        var interpolator = new KartInterpolator();
        interpolator.Add(10, [Snap(0, 0)]);

        interpolator.Clear();

        interpolator.Sample(0, 14).Should().BeNull();
    }
}
