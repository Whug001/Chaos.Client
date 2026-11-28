using Chaos.Client.ViewModel;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class QuestWatchStateTests
{
    private static QuestLogEntryInfo Entry(string key, string text, params (ushort have, ushort need)[] progress)
        => new()
        {
            Key = key,
            Title = key.ToUpperInvariant(),
            Area = "Mileth",
            GivenBy = "Noahn",
            Text = text,
            CanGiveUp = true,
            Progress = [.. progress.Select((p, i) => new QuestLogProgressInfo($"P{i}", p.have, p.need))]
        };

    private static QuestWatchState WithSeen(params (string key, string text, bool ready)[] seen)
    {
        var state = new QuestWatchState();
        var data = new QuestWatchData();

        foreach (var (key, text, ready) in seen)
            data.Seen[key] = new QuestSeen(text, ready);

        state.Load(data);

        return state;
    }

    [Test]
    public async Task New_quest_lights_the_badge()
    {
        var state = new QuestWatchState();

        state.OnEntries([Entry("a", "t")], 1, true);

        state.HasUnseen.Should().BeTrue();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Changed_stage_text_lights_the_badge()
    {
        var state = WithSeen(("a", "old", false));

        state.OnEntries([Entry("a", "new")], 1, true);

        state.HasUnseen.Should().BeTrue();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Newly_ready_quest_lights_the_badge()
    {
        var state = WithSeen(("a", "t", false));

        state.OnEntries([Entry("a", "t", (5, 5))], 1, true);

        state.HasUnseen.Should().BeTrue();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Unchanged_list_does_not_light_the_badge()
    {
        var state = WithSeen(("a", "t", false));

        state.OnEntries([Entry("a", "t")], 1, true);

        state.HasUnseen.Should().BeFalse();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Removed_quest_does_not_light_the_badge()
    {
        var state = WithSeen(("a", "ta", false), ("b", "tb", false));

        state.OnEntries([Entry("a", "ta")], 1, true);

        state.HasUnseen.Should().BeFalse();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Quest_without_progress_lines_is_never_ready()
    {
        QuestWatchState.IsReady(Entry("a", "t")).Should().BeFalse();

        await Task.CompletedTask;
    }

    [Test]
    public async Task MarkSeen_clears_the_badge_and_drops_missing_keys()
    {
        var state = WithSeen(("a", "old", false), ("b", "x", false));
        state.OnEntries([Entry("a", "new")], 1, true);
        state.HasUnseen.Should().BeTrue();

        state.MarkSeen();

        state.HasUnseen.Should().BeFalse();
        state.Export().Seen.Keys.Should().BeEquivalentTo("a");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Auto_pins_the_first_new_quest_at_level_41()
    {
        var state = new QuestWatchState();

        state.OnEntries([Entry("a", "ta"), Entry("b", "tb")], 41, true);

        state.PinnedKey.Should().Be("a");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Does_not_auto_pin_at_level_42()
    {
        var state = new QuestWatchState();

        state.OnEntries([Entry("a", "ta"), Entry("b", "tb")], 42, true);

        state.PinnedKey.Should().BeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Does_not_auto_pin_when_tracker_is_off()
    {
        var state = new QuestWatchState();

        state.OnEntries([Entry("a", "ta"), Entry("b", "tb")], 1, false);

        state.PinnedKey.Should().BeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Keeps_an_existing_pin_at_level_42()
    {
        var state = new QuestWatchState();
        state.Load(new QuestWatchData { PinnedKey = "a" });

        state.OnEntries([Entry("a", "ta")], 60, true);

        state.PinnedKey.Should().Be("a");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Manual_unpin_is_not_repinned()
    {
        var state = new QuestWatchState();
        state.OnEntries([Entry("a", "ta")], 1, true);
        state.PinnedKey.Should().Be("a");

        state.Unpin();
        state.OnEntries([Entry("a", "ta")], 1, true);

        state.PinnedKey.Should().BeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Finished_pin_unpins_and_auto_pin_picks_the_next_new_quest()
    {
        var state = new QuestWatchState();
        state.OnEntries([Entry("a", "ta")], 1, true);
        state.PinnedKey.Should().Be("a");

        state.OnEntries([Entry("b", "tb")], 1, true);

        state.PinnedKey.Should().Be("b");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Open_window_marks_each_update_seen()
    {
        var state = new QuestWatchState();

        state.OnEntries([Entry("a", "ta")], 1, true);
        state.MarkSeen();

        state.HasUnseen.Should().BeFalse();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Dirty_tracks_persisted_changes()
    {
        var state = new QuestWatchState();
        state.Dirty.Should().BeFalse();

        state.OnEntries([Entry("a", "ta")], 42, true);
        state.Dirty.Should().BeFalse();

        state.MarkSeen();
        state.Dirty.Should().BeTrue();

        state.ClearDirty();
        state.Dirty.Should().BeFalse();

        state.Pin("a");
        state.Dirty.Should().BeTrue();

        state.ClearDirty();
        state.Unpin();
        state.Dirty.Should().BeTrue();

        await Task.CompletedTask;
    }
}
