using Chaos.Client.Rendering;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public class ShatterPatternTests
{
    private const int WIDTH = 640;
    private const int HEIGHT = 480;

    [Test]
    public void Every_pixel_belongs_to_exactly_one_shard()
    {
        var pattern = ShatterPattern.Build(7, WIDTH, HEIGHT);
        var counted = pattern.Shards.Sum(shard => shard.PixelCount);

        pattern.Shards.Count.Should().Be(ShatterPattern.RAYS * ShatterPattern.RINGS);
        counted.Should().Be(WIDTH * HEIGHT);

        for (var y = 0; y < HEIGHT; y += 7)
            for (var x = 0; x < WIDTH; x += 7)
                pattern.ShardAt(x, y).Should().BeInRange(0, pattern.Shards.Count - 1);
    }

    [Test]
    public void The_same_seed_gives_the_same_pattern()
    {
        var a = ShatterPattern.Build(3, WIDTH, HEIGHT);
        var b = ShatterPattern.Build(3, WIDTH, HEIGHT);
        var c = ShatterPattern.Build(4, WIDTH, HEIGHT);

        a.Shards.Select(s => s.Bounds).Should().Equal(b.Shards.Select(s => s.Bounds));
        a.Shards.Select(s => s.Bounds).Should().NotEqual(c.Shards.Select(s => s.Bounds));
    }

    [Test]
    public void The_centre_piece_is_ring_zero_and_the_corners_are_the_outer_ring()
    {
        var pattern = ShatterPattern.Build(7, WIDTH, HEIGHT);
        var centre = pattern.ShardAt((int)pattern.Centre.X, (int)pattern.Centre.Y);

        pattern.Shards[centre].Ring.Should().Be(0);
        pattern.Shards[pattern.ShardAt(0, 0)].Ring.Should().Be(ShatterPattern.RINGS - 1);
        pattern.Shards[pattern.ShardAt(WIDTH - 1, HEIGHT - 1)].Ring.Should().Be(ShatterPattern.RINGS - 1);
    }

    [Test]
    public void Cutting_keeps_each_shards_pixels_and_edges_its_cracks()
    {
        var pattern = ShatterPattern.Build(7, WIDTH, HEIGHT);
        var frame = new Color[WIDTH * HEIGHT];
        Array.Fill(frame, new Color(10, 20, 30));

        var pieces = pattern.Cut(frame);
        var shard = pattern.Shards[pattern.ShardAt((int)pattern.Centre.X, (int)pattern.Centre.Y)];
        var piece = pieces[shard.Index];
        var opaque = piece.Count(p => p.A == 255);
        var cracks = piece.Count(p => p == ShatterPattern.CrackColour);

        piece.Length.Should().Be(shard.Bounds.Width * shard.Bounds.Height);
        opaque.Should().Be(shard.PixelCount);
        cracks.Should().BeGreaterThan(0);
        piece.Should().Contain(new Color(10, 20, 30));
    }

    [Test]
    public void Shards_hold_still_until_released_then_fly_outward_and_fall()
    {
        var pattern = ShatterPattern.Build(7, WIDTH, HEIGHT);

        foreach (var shard in pattern.Shards)
        {
            ShatterPattern.Motion(shard, shard.Delay).Offset.Should().Be(Vector2.Zero);

            var (offset, _) = ShatterPattern.Motion(shard, shard.Delay + 0.2);
            var outward = shard.Centroid - pattern.Centre;

            Vector2.Dot(offset, outward).Should().BePositive();
        }

        var any = pattern.Shards[0];
        var early = ShatterPattern.Motion(any, any.Delay + 0.2).Offset;
        var late = ShatterPattern.Motion(any, any.Delay + 0.6).Offset;
        (late.Y - early.Y).Should().BeGreaterThan((float)(Math.Sin(any.Direction) * any.Speed * 0.4));
    }
}
