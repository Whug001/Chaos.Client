using Chaos.Client.Collections;
using Chaos.Client.Rendering.CustomEmotes;
using Chaos.Client.ViewModel;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class EmoteUnlockTests
{
    [Test]
    public async Task Only_the_ten_batch_three_emotes_require_an_unlock()
    {
        foreach (var emote in CustomEmoteRegistry.All)
        {
            var expected = (emote.BodyAnimation >= 48) && (emote.BodyAnimation <= 57);
            emote.RequiresUnlock.Should().Be(expected, emote.Name);
        }

        CustomEmoteRegistry.All.Count(e => e.RequiresUnlock).Should().Be(10);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Prize_emotes_start_locked_and_nothing_else_is()
    {
        var state = new EmoteUnlockState();

        for (var bodyAnimation = 48; bodyAnimation <= 57; bodyAnimation++)
            state.IsLocked(bodyAnimation).Should().BeTrue($"byte {bodyAnimation} is a prize emote");

        state.IsLocked(9).Should().BeFalse("older drawn emotes stay free");
        state.IsLocked(19).Should().BeFalse("older drawn emotes stay free");
        state.IsLocked(45).Should().BeFalse("older drawn emotes stay free");
        state.IsLocked(47).Should().BeFalse("older drawn emotes stay free");
        state.IsLocked(58).Should().BeFalse("bytes with no emote are never locked");
        state.IsLocked(200).Should().BeFalse("bytes with no emote are never locked");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Replace_unlocks_exactly_the_listed_bytes()
    {
        var state = new EmoteUnlockState();

        state.Replace([50, 57]);

        state.IsLocked(50).Should().BeFalse();
        state.IsLocked(57).Should().BeFalse();
        state.IsLocked(48).Should().BeTrue();

        //replace, not merge: 50 drops back out
        state.Replace([48]);

        state.IsLocked(48).Should().BeFalse();
        state.IsLocked(50).Should().BeTrue();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Unknown_bytes_in_the_list_are_ignored()
    {
        var state = new EmoteUnlockState();

        var act = () => state.Replace([50, 99, 0]);

        act.Should().NotThrow();
        state.IsLocked(50).Should().BeFalse();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Unlocks_are_cleared_between_logins()
    {
        var state = new EmoteUnlockState();
        state.Replace([50]);
        state.IsLocked(50).Should().BeFalse();

        state.Clear();
        state.IsLocked(50).Should().BeTrue("a fresh login starts with everything locked");

        //the per-character reset must drop unlocks too, so a second character
        //on the same client never inherits the first one's prizes
        WorldState.EmoteUnlocks.Replace([50]);
        WorldState.ResetAll();
        WorldState.EmoteUnlocks.IsLocked(50).Should().BeTrue();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Changed_fires_on_replace_and_clear()
    {
        var state = new EmoteUnlockState();
        var count = 0;
        state.Changed += () => count++;

        state.Replace([50]);
        count.Should().Be(1);

        state.Clear();
        count.Should().Be(2);

        await Task.CompletedTask;
    }
}
