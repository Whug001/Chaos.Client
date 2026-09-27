using Chaos.Client.ViewModel;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class EmblemBookTests
{
    private static readonly DateTime Now = new(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task Apply_stores_the_book_and_raises_changed()
    {
        var book = new EmblemBook();
        var changed = 0;
        book.Changed += () => changed++;

        book.Apply(new EmblemBookArgs { ShownKey = "carnun", Entries = [new EmblemBookEntry { Key = "carnun" }] }, Now);

        book.ShownKey.Should().Be("carnun");
        book.Entries.Select(e => e.Key).Should().Equal("carnun");
        changed.Should().Be(1);

        book.Clear();
        book.Entries.Should().BeEmpty();
        book.ShownKey.Should().BeEmpty();

        //an open Emblem tab re-binds from the emptied book instead of showing the last character's emblems
        changed.Should().Be(2);
        await Task.CompletedTask;
    }

    [Test]
    public async Task SecondsLeftAt_counts_down_from_arrival_and_stops_at_zero()
    {
        var book = new EmblemBook();
        var entry = new EmblemBookEntry { Key = "newyear", Owned = true, SecondsLeft = 100 };
        book.Apply(new EmblemBookArgs { Entries = [entry] }, Now);

        book.SecondsLeftAt(entry, Now.AddSeconds(30)).Should().Be(70);
        book.SecondsLeftAt(entry, Now.AddSeconds(500)).Should().Be(0);
        book.SecondsLeftAt(new EmblemBookEntry { SecondsLeft = 0 }, Now.AddSeconds(30)).Should().Be(0);
        await Task.CompletedTask;
    }
}
