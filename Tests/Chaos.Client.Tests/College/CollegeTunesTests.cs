using Chaos.Client.Systems.College;
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class CollegeTunesTests
{
    private static readonly DateTime Start = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    private static CollegeDisplayArgs Tune(int id)
        => new()
        {
            Type = CollegeDisplayType.Tune,
            Id = id,
            Tune = new CollegeBlockInfo
            {
                Kind = CollegeBlockKind.Tune,
                Scale = TuneScale.Dorian,
                Speed = TuneSpeed.Lively,
                Instrument = TuneInstrument.Harp,
                Notes = [0, 3, 0, 2]
            }
        };

    [Test]
    public void A_tune_is_fetched_once_while_its_reply_is_due()
    {
        var tunes = new CollegeTunes();

        tunes.NeedsFetch(4, Start).Should().BeTrue();
        tunes.NeedsFetch(4, Start.AddSeconds(4.9)).Should().BeFalse();
    }

    [Test]
    public void A_tune_with_no_reply_is_fetched_again_after_five_seconds()
    {
        var tunes = new CollegeTunes();
        tunes.NeedsFetch(4, Start);

        tunes.NeedsFetch(4, Start.AddSeconds(5)).Should().BeTrue();
    }

    [Test]
    public void An_arriving_tune_is_kept_and_announced()
    {
        var tunes = new CollegeTunes();
        var ready = new List<int>();
        tunes.TuneReady += ready.Add;
        tunes.NeedsFetch(4, Start);

        tunes.OnTune(Tune(4));

        tunes.TryGet(4, out var tune).Should().BeTrue();
        tune!.Scale.Should().Be(TuneScale.Dorian);
        tune.Speed.Should().Be(TuneSpeed.Lively);
        tune.Instrument.Should().Be(TuneInstrument.Harp);
        tune.Notes.Should().Equal(new TuneNote(TuneLayer.Melody, 3, 0, 2));
        tunes.NeedsFetch(4, Start.AddSeconds(10)).Should().BeFalse();
        ready.Should().Equal(4);
    }

    [Test]
    public void A_tune_is_waiting_only_while_its_fetch_is_due()
    {
        var tunes = new CollegeTunes();

        tunes.IsWaiting(4, Start).Should().BeFalse();
        tunes.NeedsFetch(4, Start);
        tunes.IsWaiting(4, Start.AddSeconds(1)).Should().BeTrue();
        tunes.IsWaiting(4, Start.AddSeconds(5)).Should().BeFalse();

        tunes.OnTune(Tune(4));
        tunes.IsWaiting(4, Start.AddSeconds(1)).Should().BeFalse();
    }

    [Test]
    public void Clearing_forgets_tunes_and_requests()
    {
        var tunes = new CollegeTunes();
        tunes.NeedsFetch(4, Start);
        tunes.OnTune(Tune(5));

        tunes.Clear();

        tunes.TryGet(5, out _).Should().BeFalse();
        tunes.NeedsFetch(4, Start.AddSeconds(1)).Should().BeTrue();
    }
}
