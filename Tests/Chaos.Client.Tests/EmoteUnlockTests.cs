using System.Buffers;
using System.Text;
using Chaos.Client.Collections;
using Chaos.Client.Networking;
using Chaos.Client.Rendering.CustomEmotes;
using Chaos.Client.ViewModel;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Packets;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class EmoteUnlockTests
{
    //GameClient decodes with the Korean code page, which the test host does not load by default
    static EmoteUnlockTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private static ServerPacket UnlocksPacket(params byte[] owned)
    {
        //same wire shape the server sends: one count byte, then that many body-animation bytes
        var rented = ArrayPool<byte>.Shared.Rent(owned.Length + 1);
        rented[0] = (byte)owned.Length;
        owned.CopyTo(rented, 1);

        return new ServerPacket((byte)ServerOpCode.EmoteUnlocks, 0, rented, owned.Length + 1);
    }
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

        //every prize byte is pinned: a Replace that smuggled in an extra unlock (or dropped one)
        //must fail here, not just the three bytes the old test checked
        for (var bodyAnimation = 48; bodyAnimation <= 57; bodyAnimation++)
            state.IsLocked(bodyAnimation)
                 .Should()
                 .Be(bodyAnimation != 50 && bodyAnimation != 57, $"byte {bodyAnimation}");

        //replace, not merge: 50 and 57 drop back out
        state.Replace([48]);

        for (var bodyAnimation = 48; bodyAnimation <= 57; bodyAnimation++)
            state.IsLocked(bodyAnimation).Should().Be(bodyAnimation != 48, $"byte {bodyAnimation}");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Unknown_bytes_in_the_list_are_ignored()
    {
        var state = new EmoteUnlockState();

        var act = () => state.Replace([50, 99, 0]);

        act.Should().NotThrow();

        for (var bodyAnimation = 48; bodyAnimation <= 57; bodyAnimation++)
            state.IsLocked(bodyAnimation).Should().Be(bodyAnimation != 50, $"byte {bodyAnimation}");

        state.IsLocked(99).Should().BeFalse("bytes with no emote are never locked");
        state.IsLocked(0).Should().BeFalse("bytes with no emote are never locked");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Login_time_unlocks_land_before_any_world_screen_exists()
    {
        //world entry delivers the unlock packet through ChaosGame.Update before the login screen
        //switches to WorldScreen, so no screen can subscribe yet: state tracking is wired at
        //startup instead, and the packet must land there.
        using var manager = new ConnectionManager();
        WorldState.SubscribeTo(manager);

        try
        {
            manager.ProcessPackets([UnlocksPacket(50, 57)]);

            WorldState.EmoteUnlocks.IsLocked(50).Should().BeFalse("the login packet listed it");
            WorldState.EmoteUnlocks.IsLocked(57).Should().BeFalse("the login packet listed it");
            WorldState.EmoteUnlocks.IsLocked(48).Should().BeTrue("the login packet did not list it");
        } finally
        {
            WorldState.ResetAll();
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task A_later_unlock_packet_replaces_through_the_pipeline()
    {
        //a prize win re-sends the whole owned list: dispatch plus state must replace, not merge
        using var manager = new ConnectionManager();
        WorldState.SubscribeTo(manager);

        try
        {
            manager.ProcessPackets([UnlocksPacket(50)]);
            WorldState.EmoteUnlocks.IsLocked(50).Should().BeFalse();

            manager.ProcessPackets([UnlocksPacket(48)]);

            WorldState.EmoteUnlocks.IsLocked(48).Should().BeFalse();
            WorldState.EmoteUnlocks.IsLocked(50).Should().BeTrue("a later packet replaces, not merges");
        } finally
        {
            WorldState.ResetAll();
        }

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
