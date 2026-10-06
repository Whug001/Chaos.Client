using Chaos.Client.Systems.College;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class HeldCacheTests
{
    private sealed class Thing(string name)
    {
        public string Name { get; } = name;
    }

    [Test]
    public void The_least_recently_used_goes_first_when_over_capacity()
    {
        var released = new List<string>();
        var cache = new HeldCache<Thing>(2, t => released.Add(t.Name));

        cache.Add("a", new Thing("a"));
        cache.Add("b", new Thing("b"));
        cache.TryGet("a", out _).Should().BeTrue();
        cache.Add("c", new Thing("c"));

        released.Should().Equal("b");
        cache.TryGet("b", out _).Should().BeFalse();
        cache.TryGet("a", out _).Should().BeTrue();
        cache.TryGet("c", out _).Should().BeTrue();
    }

    [Test]
    public void A_held_value_is_kept_until_it_is_released()
    {
        var released = new List<string>();
        var cache = new HeldCache<Thing>(1, t => released.Add(t.Name));

        cache.Add("a", new Thing("a"));
        cache.Hold("a");
        cache.Add("b", new Thing("b"));

        released.Should().BeEmpty();
        cache.Count.Should().Be(2);

        cache.Add("c", new Thing("c"));
        cache.Hold("c");

        released.Should().Equal("b");
        cache.Count.Should().Be(2);

        cache.Release("a");

        released.Should().Equal("b", "a");
        cache.TryGet("c", out _).Should().BeTrue();
    }

    [Test]
    public void A_value_held_twice_needs_two_releases()
    {
        var released = new List<string>();
        var cache = new HeldCache<Thing>(0, t => released.Add(t.Name));

        cache.Add("a", new Thing("a"));
        cache.Hold("a");
        cache.Hold("a");
        cache.Release("a");

        released.Should().BeEmpty();

        cache.Release("a");

        released.Should().Equal("a");
    }

    [Test]
    public void Clearing_releases_everything()
    {
        var released = new List<string>();
        var cache = new HeldCache<Thing>(5, t => released.Add(t.Name));
        cache.Add("a", new Thing("a"));
        cache.Hold("a");
        cache.Add("b", new Thing("b"));

        cache.Clear();

        released.Should().BeEquivalentTo("a", "b");
        cache.Count.Should().Be(0);
    }
}
