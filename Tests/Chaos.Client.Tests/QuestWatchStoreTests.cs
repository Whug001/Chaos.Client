using Chaos.Client.ViewModel;
using FluentAssertions;

namespace Chaos.Client.Tests;

[NotInParallel]
public sealed class QuestWatchStoreTests
{
    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);

        return dir;
    }

    [Test]
    public async Task Round_trips_seen_pin_and_dismissed()
    {
        var dir = NewTempDir();

        try
        {
            var path = Path.Combine(dir, QuestWatchStore.FILE_NAME);
            var data = new QuestWatchData
            {
                PinnedKey = "b",
                Seen = new Dictionary<string, QuestSeen>
                {
                    ["a"] = new("stage one", false),
                    ["b"] = new("stage two", true)
                },
                Dismissed = ["c"]
            };

            QuestWatchStore.Save(path, data);

            File.Exists(path).Should().BeTrue();

            var loaded = QuestWatchStore.Load(path);

            loaded.PinnedKey.Should().Be("b");
            loaded.Seen.Should().BeEquivalentTo(data.Seen);
            loaded.Dismissed.Should().BeEquivalentTo(data.Dismissed);
        }
        finally
        {
            Directory.Delete(dir, true);
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task Missing_file_loads_empty()
    {
        var dir = NewTempDir();

        try
        {
            var loaded = QuestWatchStore.Load(Path.Combine(dir, "no-such-dir", QuestWatchStore.FILE_NAME));

            loaded.Seen.Should().BeEmpty();
            loaded.PinnedKey.Should().BeNull();
            loaded.Dismissed.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(dir, true);
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task Corrupt_file_loads_empty()
    {
        var dir = NewTempDir();

        try
        {
            var path = Path.Combine(dir, QuestWatchStore.FILE_NAME);
            File.WriteAllText(path, "{not json");

            var loaded = QuestWatchStore.Load(path);

            loaded.Seen.Should().BeEmpty();
            loaded.PinnedKey.Should().BeNull();
            loaded.Dismissed.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(dir, true);
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task Empty_file_loads_empty()
    {
        var dir = NewTempDir();

        try
        {
            var path = Path.Combine(dir, QuestWatchStore.FILE_NAME);
            File.WriteAllText(path, string.Empty);

            var loaded = QuestWatchStore.Load(path);

            loaded.Seen.Should().BeEmpty();
            loaded.PinnedKey.Should().BeNull();
            loaded.Dismissed.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(dir, true);
        }

        await Task.CompletedTask;
    }
}
