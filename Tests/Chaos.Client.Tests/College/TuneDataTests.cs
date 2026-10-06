using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class TuneDataTests
{
    [Test]
    public void A_tune_round_trips_through_a_block()
    {
        var tune = new TuneData(
            TuneScale.Dorian,
            TuneSpeed.Lively,
            TuneInstrument.Harp,
            [new TuneNote(TuneLayer.Melody, 4, 0, 3), new TuneNote(TuneLayer.Drums, 2, 63, 1)]);

        var block = tune.ToBlock();
        var back = TuneData.From(block);

        block.Kind.Should().Be(CollegeBlockKind.Tune);
        block.Notes.Should().Equal(0, 4, 0, 3, 2, 2, 63, 1);
        back.Scale.Should().Be(TuneScale.Dorian);
        back.Speed.Should().Be(TuneSpeed.Lively);
        back.Instrument.Should().Be(TuneInstrument.Harp);
        back.Notes.Should().Equal(tune.Notes);
    }

    [Test]
    public void From_drops_notes_outside_the_grid_and_trailing_bytes()
    {
        var block = new CollegeBlockInfo
        {
            Kind = CollegeBlockKind.Tune,
            Notes =
            [
                0, 3, 10, 2,
                0, 15, 0, 1,
                2, 3, 0, 1,
                1, 0, 0, 0,
                1, 0, 60, 5,
                2, 0, 4, 2,
                9
            ]
        };

        TuneData.From(block).Notes.Should().Equal(new TuneNote(TuneLayer.Melody, 3, 10, 2));
    }

    [Test]
    public void The_empty_tune_is_major_steady_lute()
    {
        TuneData.Empty.Scale.Should().Be(TuneScale.Major);
        TuneData.Empty.Speed.Should().Be(TuneSpeed.Steady);
        TuneData.Empty.Instrument.Should().Be(TuneInstrument.Lute);
        TuneData.Empty.Notes.Should().BeEmpty();
    }
}
