using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class TuneScalesTests
{
    [Test]
    public void Melody_starts_at_middle_C_and_bass_two_octaves_lower()
    {
        TuneScales.Midi(TuneScale.Major, TuneLayer.Melody, 0).Should().Be(60);
        TuneScales.Midi(TuneScale.Major, TuneLayer.Bass, 0).Should().Be(36);
    }

    [Test]
    public void A_seven_note_scale_spans_two_octaves_and_one_note()
    {
        TuneScales.Midi(TuneScale.Major, TuneLayer.Melody, 7).Should().Be(72);
        TuneScales.Midi(TuneScale.Major, TuneLayer.Melody, 14).Should().Be(84);
        TuneScales.Midi(TuneScale.Minor, TuneLayer.Melody, 2).Should().Be(63);
        TuneScales.Midi(TuneScale.Dorian, TuneLayer.Melody, 5).Should().Be(69);
        TuneScales.Midi(TuneScale.Mixolydian, TuneLayer.Melody, 6).Should().Be(70);
        TuneScales.Midi(TuneScale.Desert, TuneLayer.Bass, 6).Should().Be(47);
    }

    [Test]
    public void Five_note_spans_three_octaves()
    {
        TuneScales.Midi(TuneScale.FiveNote, TuneLayer.Melody, 5).Should().Be(72);
        TuneScales.Midi(TuneScale.FiveNote, TuneLayer.Melody, 14).Should().Be(93);
    }

    [Test]
    public void Drums_have_no_pitch()
        => TuneScales.Midi(TuneScale.Major, TuneLayer.Drums, 0).Should().Be(-1);

    [Test]
    public void Row_names_use_the_note_and_octave()
    {
        TuneScales.RowName(TuneScale.Major, TuneLayer.Melody, 0).Should().Be("C4");
        TuneScales.RowName(TuneScale.Minor, TuneLayer.Melody, 2).Should().Be("Eb4");
        TuneScales.RowName(TuneScale.Desert, TuneLayer.Bass, 6).Should().Be("B2");
        TuneScales.RowName(TuneScale.Major, TuneLayer.Drums, 0).Should().Be("Drum");
        TuneScales.RowName(TuneScale.Major, TuneLayer.Drums, 1).Should().Be("Tamb");
        TuneScales.RowName(TuneScale.Major, TuneLayer.Drums, 2).Should().Be("Wood");
    }

    [Test]
    public void Home_rows_repeat_every_scale_length()
    {
        TuneScales.IsHome(TuneScale.Major, 0).Should().BeTrue();
        TuneScales.IsHome(TuneScale.Major, 7).Should().BeTrue();
        TuneScales.IsHome(TuneScale.FiveNote, 5).Should().BeTrue();
        TuneScales.IsHome(TuneScale.FiveNote, 7).Should().BeFalse();
    }

    [Test]
    public void A_column_is_half_a_beat()
    {
        TuneScales.StepSeconds(TuneSpeed.Steady).Should().BeApproximately(0.25, 1e-9);
        TuneScales.StepSeconds(TuneSpeed.Slow).Should().BeApproximately(0.375, 1e-9);
        TuneScales.StepSeconds(TuneSpeed.Quick).Should().BeApproximately(0.1875, 1e-9);
    }
}
