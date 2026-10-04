using Chaos.Client.Rendering;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class PumpkinLitCacheTests
{
    [Test]
    public void Past_the_limit_the_least_recently_drawn_entries_are_released_down_to_the_trim_size()
    {
        var released = new List<string>();
        var cache = new PumpkinLitCache<int, string>(4, 2, released.Add);

        for (var i = 0; i < 4; i++)
            cache.Add(i, $"v{i}");

        cache.TryGet(0, out _).Should().BeTrue();
        cache.TryGet(1, out _).Should().BeTrue();
        cache.Add(4, "v4");

        cache.Count.Should().Be(2);
        released.Should().BeEquivalentTo(["v2", "v3", "v0"]);
        cache.TryGet(1, out var kept).Should().BeTrue();
        kept.Should().Be("v1");
        cache.TryGet(4, out _).Should().BeTrue();
    }

    [Test]
    public void Entries_drawn_every_frame_stay_cached_as_new_carvings_arrive()
    {
        var released = new List<string>();
        var cache = new PumpkinLitCache<int, string>(8, 6, released.Add);
        var paints = 0;

        for (var round = 0; round < 50; round++)
        {
            cache.Add(1000 + round, "old");

            for (var key = 0; key < 4; key++)
                if (!cache.TryGet(key, out _))
                {
                    cache.Add(key, $"v{key}");
                    paints++;
                }
        }

        paints.Should().Be(4);
        cache.Count.Should().BeLessThanOrEqualTo(8);
        released.Should().OnlyContain(value => value == "old");
    }

    [Test]
    public void Replacing_a_key_and_clearing_release_their_values()
    {
        var released = new List<string>();
        var cache = new PumpkinLitCache<int, string>(10, 5, released.Add);

        cache.Add(1, "a");
        cache.Add(1, "b");
        cache.Add(2, "c");
        cache.Clear();

        released.Should().Equal("a", "b", "c");
        cache.Count.Should().Be(0);
    }
}
