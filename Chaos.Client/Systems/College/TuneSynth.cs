using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;

namespace Chaos.Client.Systems.College;

/// <summary>
///     Turns a College tune into 16-bit stereo samples at the mixer's rate. The instruments are modelled in code, not
///     recorded: plucked strings use the Karplus-Strong method (a burst of noise fed through a short delay line that
///     softens it on each pass). Every random part is seeded from the note's layer, row and start, so a tune always
///     gives exactly the same samples on every PC.
/// </summary>
public static class TuneSynth
{
    public const int SAMPLE_RATE = 48000;
    public const double TAIL_SECONDS = 1.5;

    private const float PEAK = 0.9f;
    private const double PLUCK_FADE_SECONDS = 0.02;
    private const double PREVIEW_SECONDS = 0.25;
    private const double TAU = Math.PI * 2;

    public static int LengthInFrames(TuneSpeed speed)
        => (int)Math.Round(((CollegeProtocol.TUNE_STEPS * TuneScales.StepSeconds(speed)) + TAIL_SECONDS) * SAMPLE_RATE);

    public static short[] Render(TuneData tune, CancellationToken cancel = default)
    {
        var step = TuneScales.StepSeconds(tune.Speed);
        var mix = new float[LengthInFrames(tune.Speed)];
        float[]? line = null;

        foreach (var note in tune.Notes)
        {
            cancel.ThrowIfCancellationRequested();

            var at = (int)Math.Round(note.Start * step * SAMPLE_RATE);
            var rng = new Noise(Seed(note));
            AddNote(mix, at, tune.Scale, tune.Instrument, note.Layer, note.Row, note.Length * step, ref rng, ref line);
        }

        return ToStereo(mix);
    }

    /// <summary>One short note on its own, for the preview when a note is placed.</summary>
    public static short[] RenderNote(TuneScale scale, TuneInstrument instrument, TuneLayer layer, int row)
    {
        var mix = new float[(int)Math.Ceiling(VoiceSeconds(instrument, layer, row, PREVIEW_SECONDS) * SAMPLE_RATE)];
        var rng = new Noise(Seed(new TuneNote(layer, row, 0, 1)));
        float[]? line = null;
        AddNote(mix, 0, scale, instrument, layer, row, PREVIEW_SECONDS, ref rng, ref line);

        return ToStereo(mix);
    }

    private static double VoiceSeconds(TuneInstrument instrument, TuneLayer layer, int row, double held)
        => layer switch
        {
            TuneLayer.Drums => row switch
            {
                0 => 0.35,
                1 => 0.2,
                _ => 0.07
            },
            TuneLayer.Bass => held + 0.4,
            _ => instrument switch
            {
                TuneInstrument.Harp  => Math.Max(held, 0.9) + 0.4,
                TuneInstrument.Flute => held + 0.3,
                TuneInstrument.Bells => 2.0,
                _                    => held + 0.4
            }
        };

    private static uint Seed(TuneNote note)
        => ((uint)((((int)note.Layer * 64) + note.Row) * 64 + note.Start) * 2654435761u) ^ 0x5BD1E995u;

    private static void AddNote(
        float[] mix,
        int at,
        TuneScale scale,
        TuneInstrument instrument,
        TuneLayer layer,
        int row,
        double held,
        ref Noise rng,
        ref float[]? line)
    {
        if (layer == TuneLayer.Drums)
        {
            AddDrum(mix, at, row, ref rng);

            return;
        }

        var frequency = TuneScales.Frequency(TuneScales.Midi(scale, layer, row));

        if (layer == TuneLayer.Bass)
        {
            AddPluck(mix, at, frequency, held, 0.7, 0.997, 0.25, 3, ref rng, ref line);

            return;
        }

        switch (instrument)
        {
            case TuneInstrument.Harp:
                AddPluck(mix, at, frequency, Math.Max(held, 0.9), 0.4, 0.9985, 0.35, 3, ref rng, ref line);

                break;
            case TuneInstrument.Flute:
                AddFlute(mix, at, frequency, held, ref rng);

                break;
            case TuneInstrument.Bells:
                AddBells(mix, at, frequency);

                break;
            default:
                AddPluck(mix, at, frequency, held, 0.5, 0.996, 0.55, 2, ref rng, ref line);

                break;
        }
    }

    private static void AddPluck(
        float[] mix,
        int at,
        double frequency,
        double held,
        double volume,
        double decay,
        double brightness,
        double ringSeconds,
        ref Noise rng,
        ref float[]? line)
    {
        var length = (int)(Math.Min(ringSeconds, held + 0.4) * SAMPLE_RATE);
        var period = Math.Max(2, (int)Math.Round(SAMPLE_RATE / frequency));

        //one delay line serves every pluck of a render: each pluck writes all of its first length samples before reading
        //any, so what an earlier pluck left behind is never heard
        if ((line is null) || (line.Length < length))
            line = new float[length];
        var previous = 0f;

        for (var i = 0; (i < period) && (i < length); i++)
        {
            previous += (float)(brightness * (rng.NextSigned() - previous));
            line[i] = previous;
        }

        if (period < length)
            line[period] = (float)(line[0] * decay);

        for (var i = period + 1; i < length; i++)
            line[i] = (float)(decay * 0.5 * (line[i - period] + line[i - period - 1]));

        var hold = Math.Max(0, held - 0.05);
        var envelope = new Envelope(volume, 0.002, hold, 0.25);
        var envelopeEnd = Math.Max(1, (int)(0.002 * SAMPLE_RATE)) + (int)(hold * SAMPLE_RATE) + (int)(0.25 * SAMPLE_RATE);
        var fadeFrames = envelopeEnd > length ? Math.Min(length, (int)(PLUCK_FADE_SECONDS * SAMPLE_RATE)) : 0;

        for (var i = 0; (i < length) && (at + i < mix.Length); i++)
        {
            var fade = (fadeFrames > 0) && (i >= length - fadeFrames) ? (length - i) / (float)fadeFrames : 1f;
            mix[at + i] += line[i] * envelope.Next() * fade;
        }
    }

    private static void AddFlute(float[] mix, int at, double frequency, double held, ref Noise rng)
    {
        const int BLOCK = 32;

        var hold = Math.Max(0, held - 0.12);
        var tone = new Envelope(0.16, 0.07, hold, 0.12);
        var overtone = new Envelope(0.025, 0.07, hold, 0.12);
        var length = Math.Min((int)((held + 0.3) * SAMPLE_RATE), Math.Min(tone.Length, mix.Length - at));
        var overtoneStep = TAU * frequency * 2 / SAMPLE_RATE;
        var overtoneCoefficient = 2 * Math.Cos(overtoneStep);
        double overtone1 = Math.Sin(-overtoneStep), overtone2 = Math.Sin(-2 * overtoneStep);
        double phase = 0;

        for (var start = 0; start < length; start += BLOCK)
        {
            var seconds = start / (double)SAMPLE_RATE;
            var vibrato = seconds >= 0.15 ? frequency * 0.006 * Math.Sin(TAU * 5.2 * (seconds - 0.15)) : 0;
            var step = TAU * (frequency + vibrato) / SAMPLE_RATE;
            var coefficient = 2 * Math.Cos(step);
            var tone1 = Math.Sin(phase);
            var tone2 = Math.Sin(phase - step);
            var end = Math.Min(length, start + BLOCK);

            for (var i = start; i < end; i++)
            {
                var toneNow = (coefficient * tone1) - tone2;
                tone2 = tone1;
                tone1 = toneNow;

                var overtoneNow = (overtoneCoefficient * overtone1) - overtone2;
                overtone2 = overtone1;
                overtone1 = overtoneNow;

                mix[at + i] += (float)((toneNow * tone.Next()) + (overtoneNow * overtone.Next()));
            }

            phase += step * (end - start);
        }

        AddNoise(mix, at, Biquad.BandPass(frequency * 2, 2), 0.03, Math.Min(0.12, held), ref rng);
    }

    private static void AddBells(float[] mix, int at, double frequency)
    {
        ReadOnlySpan<(double Ratio, double Volume)> partials = [(1, 0.14), (2.76, 0.05), (5.4, 0.025)];

        foreach (var (ratio, volume) in partials)
        {
            var envelope = new Envelope(volume, 0.002, 0, 1.6 / ratio);
            var length = Math.Min(Math.Min(2 * SAMPLE_RATE, envelope.Length), mix.Length - at);
            var attack = Math.Min(envelope.AttackFrames, length);
            var step = TAU * frequency * ratio / SAMPLE_RATE;
            var coefficient = 2 * Math.Cos(step);
            double previous = Math.Sin(-step), beforePrevious = Math.Sin(-2 * step);

            for (var i = 0; i < attack; i++)
            {
                var value = (coefficient * previous) - beforePrevious;
                beforePrevious = previous;
                previous = value;
                mix[at + i] += (float)(value * envelope.Next());
            }

            // After the attack the gain only falls by a fixed factor per sample, and a sine times such a factor
            // obeys a two-term recurrence of its own, so the release needs no envelope multiply.
            var decay = envelope.Decay;
            var damped = 2 * decay * Math.Cos(step);
            var dampedSquare = decay * decay;
            var now = Math.Sin(step * (attack - 1)) * volume;
            var before = Math.Sin(step * (attack - 2)) * volume / decay;

            for (var i = attack; i < length; i++)
            {
                var value = (damped * now) - (dampedSquare * before);
                before = now;
                now = value;
                mix[at + i] += (float)value;
            }
        }
    }

    private static void AddDrum(float[] mix, int at, int row, ref Noise rng)
    {
        switch (row)
        {
            case 0:
                AddSweep(mix, at, 95, 55, 0.25, new Envelope(0.55, 0.003, 0, 0.3), 0.35);
                AddNoise(mix, at, Biquad.LowPass(400, 0.707), 0.15, 0.05, ref rng);

                break;
            case 1:
                for (var burst = 0; burst < 3; burst++)
                    AddNoise(mix, at + (int)(burst * 0.012 * SAMPLE_RATE), Biquad.BandPass(6500, 3), 0.12, 0.09, ref rng);

                break;
            default:
                AddSweep(mix, at, 1500, 1500, 0.06, new Envelope(0.12, 0.001, 0, 0.04), 0.06);
                AddNoise(mix, at, Biquad.BandPass(2500, 4), 0.04, 0.02, ref rng);

                break;
        }
    }

    /// <summary>A sine whose pitch falls from one frequency to another along an exponential curve.</summary>
    private static void AddSweep(float[] mix, int at, double from, double to, double sweepSeconds, Envelope envelope, double seconds)
    {
        var length = Math.Min((int)(seconds * SAMPLE_RATE), envelope.Length);
        double phase = 0;

        for (var i = 0; (i < length) && (at + i < mix.Length); i++)
        {
            var progress = Math.Min(1, i / (sweepSeconds * SAMPLE_RATE));
            phase += TAU * from * Math.Pow(to / from, progress) / SAMPLE_RATE;
            mix[at + i] += (float)(Math.Sin(phase) * envelope.Next());
        }
    }

    private static void AddNoise(float[] mix, int at, Biquad filter, double volume, double seconds, ref Noise rng)
    {
        var envelope = new Envelope(volume, 0.001, 0, seconds);
        var length = (int)((seconds + 0.05) * SAMPLE_RATE);

        for (var i = 0; (i < length) && (at + i < mix.Length); i++)
            mix[at + i] += filter.Process(rng.NextSigned()) * envelope.Next();
    }

    private static short[] ToStereo(float[] mix)
    {
        var peak = 0f;

        foreach (var sample in mix)
            peak = Math.Max(peak, Math.Abs(sample));

        var scale = (peak > PEAK ? PEAK / peak : 1f) * short.MaxValue;
        var stereo = new short[mix.Length * 2];

        for (var i = 0; i < mix.Length; i++)
        {
            var value = (short)Math.Clamp(Math.Round(mix[i] * scale), short.MinValue, short.MaxValue);
            stereo[i * 2] = value;
            stereo[(i * 2) + 1] = value;
        }

        return stereo;
    }

    /// <summary>
    ///     Gain over time, as Web Audio ramps it in the mockups: a straight rise from near silence to the peak, a hold,
    ///     then an exponential fall back to near silence, then nothing.
    /// </summary>
    private struct Envelope
    {
        private const double FLOOR = 0.0001;

        private readonly int AttackEnd;
        private readonly double AttackStep;
        private readonly int HoldEnd;
        private readonly double Peak;
        private readonly int ReleaseEnd;
        private readonly double ReleaseFactor;
        private double Gain;
        private int Index;

        public Envelope(double peak, double attack, double hold, double release)
        {
            Peak = peak;
            AttackEnd = Math.Max(1, (int)(attack * SAMPLE_RATE));
            HoldEnd = AttackEnd + (int)(hold * SAMPLE_RATE);
            ReleaseEnd = HoldEnd + Math.Max(1, (int)(release * SAMPLE_RATE));
            AttackStep = (peak - FLOOR) / AttackEnd;
            ReleaseFactor = Math.Pow(FLOOR / peak, 1.0 / (ReleaseEnd - HoldEnd));
            Gain = FLOOR;
            Index = 0;
        }

        /// <summary>Samples until the gain reaches zero; every later sample is silent.</summary>
        public int Length => ReleaseEnd;

        public int AttackFrames => AttackEnd;

        /// <summary>The factor the gain is multiplied by each sample of the release.</summary>
        public double Decay => ReleaseFactor;

        public float Next()
        {
            if (Index < AttackEnd)
                Gain = FLOOR + (AttackStep * (Index + 1));
            else if (Index < HoldEnd)
                Gain = Peak;
            else if (Index < ReleaseEnd)
                Gain *= ReleaseFactor;
            else
                Gain = 0;

            Index++;

            return (float)Gain;
        }
    }

    /// <summary>A two-pole filter (the RBJ audio-cookbook forms) for shaping noise.</summary>
    private struct Biquad
    {
        private readonly float A1, A2, B0, B1, B2;
        private float X1, X2, Y1, Y2;

        private Biquad(double b0, double b1, double b2, double a0, double a1, double a2)
        {
            B0 = (float)(b0 / a0);
            B1 = (float)(b1 / a0);
            B2 = (float)(b2 / a0);
            A1 = (float)(a1 / a0);
            A2 = (float)(a2 / a0);
            X1 = X2 = Y1 = Y2 = 0;
        }

        public static Biquad LowPass(double frequency, double q)
        {
            var (cos, alpha) = Shape(frequency, q);

            return new Biquad((1 - cos) / 2, 1 - cos, (1 - cos) / 2, 1 + alpha, -2 * cos, 1 - alpha);
        }

        public static Biquad BandPass(double frequency, double q)
        {
            var (cos, alpha) = Shape(frequency, q);

            return new Biquad(alpha, 0, -alpha, 1 + alpha, -2 * cos, 1 - alpha);
        }

        public float Process(float x)
        {
            var y = (B0 * x) + (B1 * X1) + (B2 * X2) - (A1 * Y1) - (A2 * Y2);
            X2 = X1;
            X1 = x;
            Y2 = Y1;
            Y1 = y;

            return y;
        }

        private static (double Cos, double Alpha) Shape(double frequency, double q)
        {
            var w0 = TAU * Math.Min(frequency, SAMPLE_RATE * 0.45) / SAMPLE_RATE;

            return (Math.Cos(w0), Math.Sin(w0) / (2 * q));
        }
    }

    /// <summary>A small fixed random generator (xorshift), so noise is the same on every PC and .NET version.</summary>
    private struct Noise(uint seed)
    {
        private uint State = seed == 0 ? 0x9E3779B9u : seed;

        public float NextSigned()
        {
            State ^= State << 13;
            State ^= State >> 17;
            State ^= State << 5;

            return ((State / (float)uint.MaxValue) * 2f) - 1f;
        }
    }
}
