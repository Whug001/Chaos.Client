using System.Diagnostics;
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;

namespace Chaos.Client.Systems.College;

/// <summary>
///     Plays one College tune at a time. A tune is rendered whole on a background thread, then played as one sound, so
///     its timing is exact. Each play names an owner (the window or row that started it); starting another tune stops
///     the first and raises <see cref="Stopped" /> with the old owner. Call <see cref="Update" /> once per frame.
/// </summary>
public sealed class TunePlayer
{
    private const int MAX_PREVIEWS = 64;
    private const double LOOP_LEAD_SECONDS = 0.5;

    private readonly Func<double> Clock;
    private readonly ITuneOutput Output;
    private readonly Dictionary<(TuneScale, TuneInstrument, TuneLayer, int), byte[]> Previews = [];
    private readonly Func<TuneScale, TuneInstrument, TuneLayer, int, short[]> RenderNote;
    private readonly Func<TuneData, CancellationToken, short[]> RenderTune;
    private readonly Func<Func<byte[]>, CancellationToken, Task<byte[]>> Run;

    private bool AskedForNext;
    private TuneData? Current;
    private int From;
    private Func<TuneData?>? Loop;
    private CancellationTokenSource? NextCancel;
    private Task<byte[]>? NextPass;
    private TuneData? NextTune;
    private bool Playing;
    private CancellationTokenSource? RenderCancel;
    private Task<byte[]>? Rendering;
    private double StartedAt;

    public TunePlayer(ITuneOutput output)
        : this(
            output,
            static () => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency,
            static (work, cancel) => Task.Run(work, cancel),
            TuneSynth.Render,
            TuneSynth.RenderNote) { }

    internal TunePlayer(
        ITuneOutput output,
        Func<double> clock,
        Func<Func<byte[]>, CancellationToken, Task<byte[]>> run,
        Func<TuneData, CancellationToken, short[]> renderTune,
        Func<TuneScale, TuneInstrument, TuneLayer, int, short[]> renderNote)
    {
        Output = output;
        Clock = clock;
        Run = run;
        RenderTune = renderTune;
        RenderNote = renderNote;
    }

    public object? Owner { get; private set; }
    public bool IsRendering => (Owner is not null) && (Rendering is not null);
    public bool IsPlaying => (Owner is not null) && Playing;

    /// <summary>The playhead in columns while a tune plays, at most 64; null otherwise.</summary>
    public double? Column
        => IsPlaying && Current is not null
            ? Math.Min(CollegeProtocol.TUNE_STEPS, From + ((Clock() - StartedAt) / TuneScales.StepSeconds(Current.Speed)))
            : null;

    public event Action<object>? Stopped;

    /// <summary>
    ///     Starts a tune from a column. With <paramref name="loop" />, each next pass is taken from it while the current
    ///     pass plays, so edits are heard on the next pass. A null from it ends the loop after the current pass.
    /// </summary>
    public void Play(TuneData tune, object owner, int fromColumn = 0, Func<TuneData?>? loop = null)
    {
        Stop();

        Owner = owner;
        Current = tune;
        From = Math.Clamp(fromColumn, 0, CollegeProtocol.TUNE_STEPS - 1);
        Loop = loop;

        var from = From;
        RenderCancel = new CancellationTokenSource();
        var cancel = RenderCancel.Token;
        Rendering = Run(() => TuneWav.Wrap(Slice(RenderTune(tune, cancel), tune.Speed, from)), cancel);
    }

    public void Stop()
    {
        if (Owner is null)
            return;

        var old = Owner;
        Owner = null;
        Current = null;
        Loop = null;
        NextPass = null;
        NextTune = null;
        Rendering = null;
        RenderCancel?.Cancel();
        RenderCancel = null;
        NextCancel?.Cancel();
        NextCancel = null;
        AskedForNext = false;
        Playing = false;
        Output.StopTune();
        Stopped?.Invoke(old);
    }

    public void StopIfOwner(object owner)
    {
        if (ReferenceEquals(Owner, owner))
            Stop();
    }

    /// <summary>Plays one note on its own; skipped while a tune plays or renders, since both use the same channel.</summary>
    public void Preview(TuneScale scale, TuneInstrument instrument, TuneLayer layer, int row)
    {
        if (Owner is not null)
            return;

        var key = layer switch
        {
            TuneLayer.Drums => (TuneScale.Major, TuneInstrument.Lute, layer, row),
            TuneLayer.Bass  => (scale, TuneInstrument.Lute, layer, row),
            _               => (scale, instrument, layer, row)
        };

        if (!Previews.TryGetValue(key, out var wav))
        {
            if (Previews.Count >= MAX_PREVIEWS)
                Previews.Clear();

            wav = TuneWav.Wrap(RenderNote(key.Item1, key.Item2, key.Item3, key.Item4));
            Previews[key] = wav;
        }

        Output.PlayPreview(wav);
    }

    public void Update()
    {
        if (Owner is null)
            return;

        if (Rendering is { IsCompleted: true } rendered)
        {
            Rendering = null;
            RenderCancel = null;

            if (!rendered.IsCompletedSuccessfully || !Output.PlayTune(rendered.Result))
            {
                Stop();

                return;
            }

            Playing = true;
            StartedAt = Clock();
            AskedForNext = false;
        }

        if (!Playing)
            return;

        if (Loop is not null)
        {
            if (!AskedForNext && (CollegeProtocol.TUNE_STEPS - Column) * TuneScales.StepSeconds(Current!.Speed) <= LOOP_LEAD_SECONDS + 1e-9)
            {
                AskedForNext = true;

                if (Loop() is { } tune)
                {
                    NextTune = tune;
                    NextCancel = new CancellationTokenSource();
                    var cancel = NextCancel.Token;
                    NextPass = Run(() => TuneWav.Wrap(RenderTune(tune, cancel)), cancel);
                } else
                    Loop = null;
            }

            if ((Column < CollegeProtocol.TUNE_STEPS) || NextPass is not { IsCompleted: true } next)
                return;

            if (!next.IsCompletedSuccessfully || !Output.PlayTune(next.Result))
            {
                Stop();

                return;
            }

            Current = NextTune;
            From = 0;
            StartedAt = Clock();
            NextPass = null;
            NextCancel = null;
            NextTune = null;
            AskedForNext = false;

            return;
        }

        if (!Output.IsTunePlaying)
            Stop();
    }

    private static short[] Slice(short[] samples, TuneSpeed speed, int fromColumn)
    {
        if (fromColumn == 0)
            return samples;

        var skip = (int)Math.Round(fromColumn * TuneScales.StepSeconds(speed) * TuneSynth.SAMPLE_RATE) * 2;

        return skip >= samples.Length ? [] : samples[skip..];
    }
}
