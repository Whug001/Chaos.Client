using Chaos.Client.Systems.College;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class CollegePictureCacheTests
{
    private static CollegePictureCache Create(long cap)
        => new(Path.Combine(Path.GetTempPath(), "college-cache-" + Guid.NewGuid().ToString("N")), cap);

    [Test]
    public void Pictures_are_stored_and_read_by_hash()
    {
        var cache = Create(1_000);
        var hash = new string('a', 64);

        cache.Write(hash, [1, 2, 3]);

        cache.TryRead(hash).Should().Equal(1, 2, 3);
        cache.TryRead(new string('b', 64)).Should().BeNull();
        cache.TryRead("../x").Should().BeNull();
    }

    [Test]
    public void The_oldest_pictures_go_past_the_cap()
    {
        var cache = Create(250);

        for (var i = 0; i < 3; i++)
        {
            cache.Write(new string((char)('a' + i), 64), new byte[100]);
            Thread.Sleep(20);
        }

        cache.TryRead(new string('a', 64)).Should().BeNull();
        cache.TryRead(new string('b', 64)).Should().NotBeNull();
        cache.TryRead(new string('c', 64)).Should().NotBeNull();
    }

    [Test]
    public void A_locked_file_is_a_miss_and_never_throws()
    {
        var directory = Path.Combine(Path.GetTempPath(), "college-cache-" + Guid.NewGuid().ToString("N"));
        var cache = new CollegePictureCache(directory, 1_000);
        var hash = new string('a', 64);
        cache.Write(hash, [1, 2, 3]);

        using (new FileStream(Path.Combine(directory, hash + ".pic"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            cache.TryRead(hash).Should().BeNull();
            cache.Invoking(c => c.Write(hash, [4, 5, 6])).Should().NotThrow();
            cache.Invoking(c => c.Delete(hash)).Should().NotThrow();
        }

        cache.TryRead(hash).Should().Equal(1, 2, 3);
    }
}
