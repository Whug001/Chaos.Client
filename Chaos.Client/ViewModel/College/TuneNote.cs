using Chaos.DarkAges.Definitions;

namespace Chaos.Client.ViewModel.College;

/// <summary>One note of a College tune: a row of a layer, from a start column for a number of columns.</summary>
public readonly record struct TuneNote(TuneLayer Layer, int Row, int Start, int Length)
{
    public int End => Start + Length;

    public bool Covers(TuneLayer layer, int row, int column) => (Layer == layer) && (Row == row) && (column >= Start) && (column < End);

    /// <summary>Inside the grid: a known layer and row, within the 64 columns, and drum notes one column long.</summary>
    public bool IsInGrid
        => Enum.IsDefined(Layer)
           && (Row >= 0)
           && (Row < CollegeProtocol.TuneRows(Layer))
           && (Start >= 0)
           && (Length >= 1)
           && (End <= CollegeProtocol.TUNE_STEPS)
           && ((Layer != TuneLayer.Drums) || (Length == 1));
}
