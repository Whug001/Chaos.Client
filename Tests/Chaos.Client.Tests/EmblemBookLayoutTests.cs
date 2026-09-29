using Chaos.Client.Controls.World.Popups.Profile;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class EmblemBookLayoutTests
{
    [Test]
    public async Task Pages_hold_25_and_wrap()
    {
        EmblemBookLayout.PageCount(0).Should().Be(1);
        EmblemBookLayout.PageCount(25).Should().Be(1);
        EmblemBookLayout.PageCount(26).Should().Be(2);
        EmblemBookLayout.NextPage(1, 26).Should().Be(0);
        EmblemBookLayout.PrevPage(0, 26).Should().Be(1);
        EmblemBookLayout.PrevPage(0, 3).Should().Be(0);
        EmblemBookLayout.PageText(1, 60).Should().Be("2/3");
        EmblemBookLayout.SlotOffset(0).Should().Be((0, 0));
        EmblemBookLayout.SlotOffset(7).Should().Be((86, 45));
        await Task.CompletedTask;
    }

    [Test]
    public async Task Time_left_text_covers_every_case()
    {
        var owned = new EmblemBookEntry { Owned = true, SecondsLeft = 1 };

        EmblemBookLayout.TimeLeftText(new EmblemBookEntry { Record = true, Owned = true }, 0).Should().Be("While first place");
        EmblemBookLayout.TimeLeftText(new EmblemBookEntry(), 0).Should().Be("Locked");
        EmblemBookLayout.TimeLeftText(new EmblemBookEntry { Owned = true }, 0).Should().Be("Never");
        EmblemBookLayout.TimeLeftText(owned, (12 * 24 + 4) * 3600 + 59).Should().Be("12d 4h");
        EmblemBookLayout.TimeLeftText(owned, 3 * 3600 + 20 * 60).Should().Be("3h 20m");
        EmblemBookLayout.TimeLeftText(owned, 20 * 60 + 5).Should().Be("20m");
        EmblemBookLayout.TimeLeftText(owned, 10).Should().Be("1m");
        EmblemBookLayout.TimeLeftText(owned, 0).Should().Be("Expired");
        await Task.CompletedTask;
    }

    [Test]
    public async Task Status_text_says_held_earned_or_locked()
    {
        EmblemBookLayout.StatusText(new EmblemBookEntry { Record = true, Holder = "Aldric" }).Should().Be("Held by Aldric");
        EmblemBookLayout.StatusText(new EmblemBookEntry { Record = true }).Should().Be("Locked");
        EmblemBookLayout.StatusText(new EmblemBookEntry { Owned = true, GrantedUnix = 1_790_000_000 }).Should().Be("Earned 2026-09-21");
        EmblemBookLayout.StatusText(new EmblemBookEntry()).Should().Be("Locked");
        await Task.CompletedTask;
    }

    [Test]
    public async Task IndexOf_matches_keys_ignoring_case()
    {
        List<EmblemBookEntry> entries = [new() { Key = "carnun" }, new() { Key = "newyear" }];

        EmblemBookLayout.IndexOf(entries, "NEWYEAR").Should().Be(1);
        EmblemBookLayout.IndexOf(entries, "missing").Should().Be(-1);
        EmblemBookLayout.IndexOf(entries, null).Should().Be(-1);
        await Task.CompletedTask;
    }

    [Test]
    public void Guild_emblem_texts_name_the_guild()
    {
        var entry = new EmblemBookEntry { Key = "guild", Name = "Richards", Owned = true, Guild = true, GuildEmblemId = 42 };

        EmblemBookLayout.StatusText(entry).Should().Be("While you are in Richards");
        EmblemBookLayout.TimeLeftText(entry, 0).Should().Be("While in guild");
    }
}
