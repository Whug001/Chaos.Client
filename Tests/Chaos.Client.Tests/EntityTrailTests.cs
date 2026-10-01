using Chaos.Client.Systems;
using Chaos.Geometry.Abstractions.Definitions;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public class EntityTrailTests
{
    [Test]
    public async Task Returns_the_latest_sample_at_or_before_the_time()
    {
        var trail = new EntityTrail();
        trail.Record(1, 1000, new Vector2(1, 1), Direction.Up);
        trail.Record(1, 1100, new Vector2(1, 2), Direction.Down);
        trail.Record(1, 1200, new Vector2(1, 3), Direction.Left);

        trail.TryGet(1, 1150, out var tile, out var facing).Should().BeTrue();
        tile.Should().Be(new Vector2(1, 2));
        facing.Should().Be(Direction.Down);

        trail.TryGet(1, 1200, out tile, out _).Should().BeTrue();
        tile.Should().Be(new Vector2(1, 3));

        trail.TryGet(1, 999, out _, out _).Should().BeFalse();
        trail.TryGet(2, 1200, out _, out _).Should().BeFalse();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Keeps_two_seconds_and_one_sample_before()
    {
        var trail = new EntityTrail();

        for (var ms = 0L; ms <= 5000; ms += 100)
            trail.Record(1, ms, new Vector2(ms, 0), Direction.Up);

        trail.TryGet(1, 3000, out var tile, out _).Should().BeTrue();
        tile.X.Should().Be(3000);

        trail.TryGet(1, 2899, out _, out _).Should().BeFalse();
        trail.Count(1).Should().BeLessThanOrEqualTo(22);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Prune_and_clear()
    {
        var trail = new EntityTrail();
        trail.Record(1, 1000, Vector2.Zero, Direction.Up);
        trail.Record(2, 4000, Vector2.Zero, Direction.Up);

        trail.Prune(4500);

        trail.TryGet(1, 1000, out _, out _).Should().BeFalse();
        trail.TryGet(2, 4000, out _, out _).Should().BeTrue();

        trail.Clear();
        trail.TryGet(2, 4000, out _, out _).Should().BeFalse();
        await Task.CompletedTask;
    }
}
