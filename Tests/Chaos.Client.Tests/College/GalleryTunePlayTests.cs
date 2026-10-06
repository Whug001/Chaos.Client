using Chaos.Client.Systems.College;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class GalleryTunePlayTests
{
    private static readonly DateTime Start = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void A_cached_tune_plays_at_once()
    {
        var play = new GalleryTunePlay();

        play.Press(4, false, true, false, Start).Should().Be(TuneRowAction.Play);
        play.Pending.Should().Be(0);
    }

    [Test]
    public void An_uncached_tune_is_fetched_and_its_row_waits()
    {
        var play = new GalleryTunePlay();

        play.Press(4, false, false, true, Start).Should().Be(TuneRowAction.Fetch);
        play.Pending.Should().Be(4);
    }

    [Test]
    public void Pressing_again_while_the_fetch_is_due_sends_nothing()
    {
        var play = new GalleryTunePlay();
        play.Press(4, false, false, true, Start);

        play.Press(4, false, false, false, Start.AddSeconds(1)).Should().Be(TuneRowAction.None);
        play.Pending.Should().Be(4);
    }

    [Test]
    public void Pressing_a_playing_row_stops_it()
    {
        var play = new GalleryTunePlay();

        play.Press(4, true, true, false, Start).Should().Be(TuneRowAction.Stop);
    }

    [Test]
    public void Only_the_waiting_row_plays_when_its_tune_arrives()
    {
        var play = new GalleryTunePlay();
        play.Press(4, false, false, true, Start);

        play.Arrived(5).Should().BeFalse();
        play.Arrived(4).Should().BeTrue();
        play.Arrived(4).Should().BeFalse();
    }

    [Test]
    public void A_row_stops_waiting_after_five_seconds()
    {
        var play = new GalleryTunePlay();
        play.Press(4, false, false, true, Start);

        play.Tick(Start.AddSeconds(4.9));
        play.Pending.Should().Be(4);

        play.Tick(Start.AddSeconds(5));
        play.Pending.Should().Be(0);
        play.Arrived(4).Should().BeFalse();
    }

    [Test]
    public void Cancel_forgets_the_waiting_row()
    {
        var play = new GalleryTunePlay();
        play.Press(4, false, false, true, Start);

        play.Cancel();

        play.Arrived(4).Should().BeFalse();
    }

    [Test]
    public void Captions_read_play_wait_and_stop()
    {
        GalleryTunePlay.Caption(false, false).Should().Be("Play");
        GalleryTunePlay.Caption(false, true).Should().Be("Wait");
        GalleryTunePlay.Caption(true, false).Should().Be("Stop");
    }
}
