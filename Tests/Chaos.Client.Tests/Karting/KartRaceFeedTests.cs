#region
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Karting;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
#endregion

namespace Chaos.Client.Tests.Karting;

public class KartRaceFeedTests
{
    private static readonly Func<byte, string> Names = index => index switch { 0 => "Dose", 1 => "Whug", _ => "Aldric" };

    private static KartRaceEventArgs Event(KartRaceEventKind kind, byte index, byte by = 255, byte lap = 0, KartItemKind item = KartItemKind.None)
        => new() { Kind = kind, Index = index, By = by, Lap = lap, Item = item };

    [Test]
    public async Task A_hit_names_the_attacker_and_the_item_and_says_you_for_the_local_racer()
    {
        KartRaceFeed.LineFor(Event(KartRaceEventKind.Hit, 0, by: 1, item: KartItemKind.EmberBolt), Names, 0, 3)
                    .Should().Be("Whug hit you with Ember Bolt");
        KartRaceFeed.LineFor(Event(KartRaceEventKind.Hit, 2, by: 0, item: KartItemKind.EmberBolt), Names, 0, 3)
                    .Should().Be("You hit Aldric with Ember Bolt");
    }

    [Test]
    public async Task A_blocked_hit_credits_the_ward_and_a_hit_with_no_attacker_shows_nothing()
    {
        KartRaceFeed.LineFor(Event(KartRaceEventKind.Hit, 2, by: 1, item: KartItemKind.RuneWard), Names, 0, 3)
                    .Should().Be("Aldric's ward blocked Whug");
        KartRaceFeed.LineFor(Event(KartRaceEventKind.Hit, 2, item: KartItemKind.EmberBolt), Names, 0, 3).Should().BeNull();
    }

    [Test]
    public async Task Laps_name_the_next_lap_and_the_last_lap_finishes_instead()
    {
        KartRaceFeed.LineFor(Event(KartRaceEventKind.LapDone, 1, lap: 1), Names, 0, 3).Should().Be("Whug started lap 2");
        KartRaceFeed.LineFor(Event(KartRaceEventKind.LapDone, 1, lap: 2), Names, 0, 3).Should().Be("Whug started the final lap");
        KartRaceFeed.LineFor(Event(KartRaceEventKind.LapDone, 1, lap: 3), Names, 0, 3).Should().BeNull();
        KartRaceFeed.LineFor(Event(KartRaceEventKind.Finished, 1), Names, 0, 3).Should().Be("Whug finished");
    }

    [Test]
    public async Task Lines_keep_the_newest_three_and_fade_out_at_the_end()
    {
        var feed = new KartRaceFeed();

        for (var i = 0; i < 4; i++)
            feed.Add($"line {i}", i);

        feed.Lines(3).Select(line => line.Text).Should().Equal("line 3", "line 2", "line 1");
        feed.Lines(6.5).Should().ContainSingle(line => line.Text == "line 2").Which.Alpha.Should().BeApproximately(0.5f, 0.001f);
        feed.Lines(9).Should().BeEmpty();
    }

    [Test]
    public async Task Moving_up_into_the_top_three_adds_a_line_and_the_first_snapshot_does_not()
    {
        var feed = new KartRaceFeed();
        feed.OnPlaces([new KartRacerSnapshot { Index = 1, Place = 4 }, new KartRacerSnapshot { Index = 0, Place = 2 }], Names, 0, 0);
        feed.Lines(0).Should().BeEmpty();

        feed.OnPlaces([new KartRacerSnapshot { Index = 1, Place = 3 }, new KartRacerSnapshot { Index = 0, Place = 1 }], Names, 0, 1);

        feed.Lines(1).Select(line => line.Text).Should().Equal("You took the lead", "Whug took 3rd");
    }
}
