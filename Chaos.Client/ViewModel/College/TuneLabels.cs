using Chaos.DarkAges.Definitions;

namespace Chaos.Client.ViewModel.College;

/// <summary>What the composer's buttons and the reader's summary line call a tune's scale, speed and instrument.</summary>
public static class TuneLabels
{
    public static string Scale(TuneScale scale)
        => scale switch
        {
            TuneScale.FiveNote => "Five-note",
            _                  => scale.ToString()
        };

    public static string Speed(TuneSpeed speed) => speed.ToString();

    public static string Instrument(TuneInstrument instrument) => instrument.ToString();

    /// <summary>"Dorian, Lively, Harp".</summary>
    public static string Summary(TuneData tune) => $"{Scale(tune.Scale)}, {Speed(tune.Speed)}, {Instrument(tune.Instrument)}";

    /// <summary>The value after <paramref name="value" />, wrapping to the first: one click of a cycle button.</summary>
    public static T Next<T>(T value) where T : struct, Enum
    {
        var values = Enum.GetValues<T>();

        return values[(Array.IndexOf(values, value) + 1) % values.Length];
    }
}
