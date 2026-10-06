using Chaos.DarkAges.Definitions;

namespace Chaos.Client.ViewModel.College;

/// <summary>
///     Where a tune row sits in pitch. Rows are steps of the tune's scale, so changing the scale moves every note into
///     the new scale.
/// </summary>
public static class TuneScales
{
    public const int MELODY_BASE = 60;
    public const int BASS_BASE = 36;

    private static readonly int[] Major = [0, 2, 4, 5, 7, 9, 11];
    private static readonly int[] Minor = [0, 2, 3, 5, 7, 8, 10];
    private static readonly int[] FiveNote = [0, 2, 4, 7, 9];
    private static readonly int[] Dorian = [0, 2, 3, 5, 7, 9, 10];
    private static readonly int[] Mixolydian = [0, 2, 4, 5, 7, 9, 10];
    private static readonly int[] Desert = [0, 2, 3, 5, 7, 8, 11];

    private static readonly string[] NoteNames = ["C", "C#", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"];
    private static readonly string[] DrumNames = ["Drum", "Tamb", "Wood"];

    public static IReadOnlyList<int> StepsOf(TuneScale scale)
        => scale switch
        {
            TuneScale.Minor      => Minor,
            TuneScale.FiveNote   => FiveNote,
            TuneScale.Dorian     => Dorian,
            TuneScale.Mixolydian => Mixolydian,
            TuneScale.Desert     => Desert,
            _                    => Major
        };

    /// <summary>The row's MIDI note number; drums have no pitch and give -1.</summary>
    public static int Midi(TuneScale scale, TuneLayer layer, int row)
    {
        if (layer == TuneLayer.Drums)
            return -1;

        var steps = StepsOf(scale);
        var root = layer == TuneLayer.Melody ? MELODY_BASE : BASS_BASE;

        return root + (12 * (row / steps.Count)) + steps[row % steps.Count];
    }

    public static double Frequency(int midi) => 440.0 * Math.Pow(2, (midi - 69) / 12.0);

    public static string RowName(TuneScale scale, TuneLayer layer, int row)
    {
        if (layer == TuneLayer.Drums)
            return (row >= 0) && (row < DrumNames.Length) ? DrumNames[row] : string.Empty;

        var midi = Midi(scale, layer, row);

        return NoteNames[midi % 12] + ((midi / 12) - 1);
    }

    /// <summary>The scale's home note (C) on each octave.</summary>
    public static bool IsHome(TuneScale scale, int row) => row % StepsOf(scale).Count == 0;

    /// <summary>One column is half a beat.</summary>
    public static double StepSeconds(TuneSpeed speed) => 60.0 / CollegeProtocol.TuneBpm(speed) / 2;
}
