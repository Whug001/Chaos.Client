using Chaos.Client.Rendering;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class PumpkinCarvingCacheTests
{
    private sealed class Painted(int number)
    {
        public int Number { get; } = number;
    }

    private static byte[] Grid(int cuts)
    {
        var grid = PumpkinGrid.Empty();

        for (var i = 0; i < cuts; i++)
            PumpkinGrid.SetCut(grid, i % PumpkinGrid.WIDTH, i / PumpkinGrid.WIDTH, true);

        return grid;
    }

    [Test]
    public void An_unchanged_carving_is_not_repainted()
    {
        var released = new List<Painted>();
        var cache = new PumpkinCarvingCache<Painted>(released.Add);
        var paints = 0;

        var first = cache.GetOrPaint(7, 1, Grid(3), () => new Painted(++paints));
        var second = cache.GetOrPaint(7, 1, Grid(3), () => new Painted(++paints));

        second.Should().BeSameAs(first);
        paints.Should().Be(1);
        released.Should().BeEmpty();
    }

    [Test]
    public void A_new_carving_replaces_the_old_image_instead_of_adding_one()
    {
        var released = new List<Painted>();
        var cache = new PumpkinCarvingCache<Painted>(released.Add);
        var paints = 0;

        var old = cache.GetOrPaint(7, 1, Grid(3), () => new Painted(++paints))!;

        for (var cuts = 4; cuts < 40; cuts++)
            cache.GetOrPaint(7, 1, Grid(cuts), () => new Painted(++paints));

        cache.Count.Should().Be(1);
        released.Should().HaveCount(36).And.Contain(old);
    }

    [Test]
    public void Each_pumpkin_and_frame_has_its_own_entry_and_clear_releases_them()
    {
        var released = new List<Painted>();
        var cache = new PumpkinCarvingCache<Painted>(released.Add);

        cache.GetOrPaint(7, 0, Grid(3), () => new Painted(1));
        cache.GetOrPaint(7, 1, Grid(3), () => new Painted(2));
        cache.GetOrPaint(8, 1, Grid(3), () => new Painted(3));

        cache.Count.Should().Be(3);

        cache.Clear();

        cache.Count.Should().Be(0);
        released.Select(p => p.Number).Should().BeEquivalentTo([1, 2, 3]);
    }

    [Test]
    public void The_cached_grid_is_a_copy()
    {
        var cache = new PumpkinCarvingCache<Painted>(_ => { });
        var grid = Grid(3);
        var paints = 0;

        cache.GetOrPaint(7, 1, grid, () => new Painted(++paints));
        grid[0] = 0;
        cache.GetOrPaint(7, 1, Grid(3), () => new Painted(++paints));

        paints.Should().Be(1);
    }

    [Test]
    public void Forget_releases_every_frame_of_that_pumpkin_only()
    {
        var released = new List<Painted>();
        var cache = new PumpkinCarvingCache<Painted>(released.Add);

        cache.GetOrPaint(7, 0, Grid(3), () => new Painted(1));
        cache.GetOrPaint(7, 1, Grid(3), () => new Painted(2));
        cache.GetOrPaint(8, 1, Grid(3), () => new Painted(3));

        cache.Forget(7);

        cache.Count.Should().Be(1);
        released.Select(p => p.Number).Should().BeEquivalentTo([1, 2]);
        cache.TryGet(8, 1, Grid(3), out _).Should().BeTrue();
    }

    [Test]
    public void Forget_of_an_unknown_pumpkin_does_nothing()
    {
        var released = new List<Painted>();
        var cache = new PumpkinCarvingCache<Painted>(released.Add);

        cache.GetOrPaint(7, 0, Grid(3), () => new Painted(1));

        cache.Forget(99);

        cache.Count.Should().Be(1);
        released.Should().BeEmpty();
    }

    [Test]
    public void TryGet_hits_only_for_the_same_grid()
    {
        var cache = new PumpkinCarvingCache<Painted>(_ => { });
        var painted = cache.GetOrPaint(7, 1, Grid(3), () => new Painted(1));

        cache.TryGet(7, 1, Grid(3), out var hit).Should().BeTrue();
        hit.Should().BeSameAs(painted);
        cache.TryGet(7, 1, Grid(4), out _).Should().BeFalse();
        cache.TryGet(7, 2, Grid(3), out _).Should().BeFalse();
        cache.TryGet(8, 1, Grid(3), out _).Should().BeFalse();
    }
}
