using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class TuneLabelsTests
{
    [Test]
    public void Scales_speeds_and_instruments_have_display_names()
    {
        TuneLabels.Scale(TuneScale.FiveNote).Should().Be("Five-note");
        TuneLabels.Scale(TuneScale.Desert).Should().Be("Desert");
        TuneLabels.Speed(TuneSpeed.Walking).Should().Be("Walking");
        TuneLabels.Instrument(TuneInstrument.Bells).Should().Be("Bells");
    }

    [Test]
    public void The_summary_names_scale_speed_and_instrument()
        => TuneLabels.Summary(new TuneData(TuneScale.Dorian, TuneSpeed.Lively, TuneInstrument.Harp, []))
                     .Should()
                     .Be("Dorian, Lively, Harp");

    [Test]
    public void Next_cycles_and_wraps()
    {
        TuneLabels.Next(TuneScale.Major).Should().Be(TuneScale.Minor);
        TuneLabels.Next(TuneScale.Desert).Should().Be(TuneScale.Major);
        TuneLabels.Next(TuneSpeed.Quick).Should().Be(TuneSpeed.Slow);
        TuneLabels.Next(TuneInstrument.Bells).Should().Be(TuneInstrument.Lute);
    }
}
