using Chaos.Client.Systems.College;
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class TuneSynthTests
{
    private static TuneData Tune(TuneSpeed speed, TuneInstrument instrument, params TuneNote[] notes)
        => new(TuneScale.Major, speed, instrument, notes);

    private static int ZeroCrossings(short[] stereo, double fromSeconds, double toSeconds)
    {
        var from = (int)(fromSeconds * TuneSynth.SAMPLE_RATE);
        var to = (int)(toSeconds * TuneSynth.SAMPLE_RATE);
        var crossings = 0;

        for (var frame = from + 1; frame < to; frame++)
            if ((stereo[(frame - 1) * 2] < 0) != (stereo[frame * 2] < 0))
                crossings++;

        return crossings;
    }

    [Test]
    public void A_tune_lasts_64_columns_plus_the_ring_out()
    {
        TuneSynth.LengthInFrames(TuneSpeed.Quick).Should().Be(648_000);
        TuneSynth.LengthInFrames(TuneSpeed.Slow).Should().Be(1_224_000);

        foreach (var speed in Enum.GetValues<TuneSpeed>())
            TuneSynth.Render(Tune(speed, TuneInstrument.Lute)).Should().HaveCount(TuneSynth.LengthInFrames(speed) * 2);
    }

    [Test]
    public void An_empty_tune_is_silent()
        => TuneSynth.Render(Tune(TuneSpeed.Quick, TuneInstrument.Lute)).Should().OnlyContain(s => s == 0);

    [Test]
    public void The_same_tune_gives_the_same_samples()
    {
        var tune = Tune(
            TuneSpeed.Quick,
            TuneInstrument.Harp,
            new TuneNote(TuneLayer.Melody, 4, 0, 3),
            new TuneNote(TuneLayer.Bass, 0, 0, 8),
            new TuneNote(TuneLayer.Drums, 0, 0, 1),
            new TuneNote(TuneLayer.Drums, 1, 4, 1),
            new TuneNote(TuneLayer.Drums, 2, 6, 1));

        TuneSynth.Render(tune).Should().Equal(TuneSynth.Render(tune));
    }

    [Test]
    public void Every_row_at_once_stays_inside_the_16_bit_range()
    {
        var notes = new List<TuneNote>();

        for (var row = 0; row < CollegeProtocol.TUNE_PITCH_ROWS; row++)
        {
            notes.Add(new TuneNote(TuneLayer.Melody, row, 0, 64));
            notes.Add(new TuneNote(TuneLayer.Bass, row, 0, 64));
        }

        var samples = TuneSynth.Render(Tune(TuneSpeed.Quick, TuneInstrument.Bells, notes.ToArray()));
        var peak = samples.Max(s => Math.Abs((int)s));

        peak.Should().BeLessThanOrEqualTo((int)Math.Ceiling(0.9 * short.MaxValue));
        peak.Should().BeGreaterThan(10_000);
    }

    [Test]
    public void A_note_an_octave_higher_crosses_zero_twice_as_often()
    {
        var low = TuneSynth.Render(Tune(TuneSpeed.Slow, TuneInstrument.Flute, new TuneNote(TuneLayer.Melody, 0, 0, 4)));
        var high = TuneSynth.Render(Tune(TuneSpeed.Slow, TuneInstrument.Flute, new TuneNote(TuneLayer.Melody, 7, 0, 4)));

        var ratio = ZeroCrossings(high, 0.2, 1.2) / (double)ZeroCrossings(low, 0.2, 1.2);

        ratio.Should().BeInRange(1.9, 2.1);
    }

    [Test]
    public void A_long_held_pluck_fades_out_before_its_buffer_ends()
    {
        var samples = TuneSynth.Render(Tune(TuneSpeed.Slow, TuneInstrument.Lute, new TuneNote(TuneLayer.Bass, 0, 0, 64)));
        var peak = samples.Max(s => Math.Abs((int)s));
        var lastFrame = Array.FindLastIndex(samples, s => s != 0) / 2;
        var from = lastFrame - (TuneSynth.SAMPLE_RATE / 500);

        for (var frame = from; frame <= lastFrame; frame++)
            Math.Abs((int)samples[frame * 2]).Should().BeLessThan(peak / 100);
    }

    [Test]
    public void A_single_note_renders_a_short_sound()
    {
        foreach (var instrument in Enum.GetValues<TuneInstrument>())
        {
            var note = TuneSynth.RenderNote(TuneScale.Major, instrument, TuneLayer.Melody, 7);
            note.Length.Should().BeInRange(2, 3 * TuneSynth.SAMPLE_RATE * 2);
            note.Should().Contain(s => s != 0);
        }

        TuneSynth.RenderNote(TuneScale.Minor, TuneInstrument.Lute, TuneLayer.Bass, 3).Should().Contain(s => s != 0);

        for (var drum = 0; drum < CollegeProtocol.TUNE_DRUM_ROWS; drum++)
            TuneSynth.RenderNote(TuneScale.Major, TuneInstrument.Lute, TuneLayer.Drums, drum).Should().Contain(s => s != 0);
    }

    [Test]
    public void Both_channels_carry_the_same_sound()
    {
        var samples = TuneSynth.RenderNote(TuneScale.Major, TuneInstrument.Lute, TuneLayer.Melody, 2);

        for (var frame = 0; frame < samples.Length / 2; frame++)
            samples[frame * 2].Should().Be(samples[(frame * 2) + 1]);
    }
}
