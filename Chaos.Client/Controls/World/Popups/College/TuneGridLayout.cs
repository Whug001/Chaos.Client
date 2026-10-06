using Chaos.DarkAges.Definitions;

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>
///     Where things sit in a <see cref="TuneGrid" />, in pixels from its top left: an optional beat bar, then Melody, Bass
///     and Drums stacked with a gap between them, each with an optional row-name column at the left. Row 0 is a layer's
///     bottom row.
/// </summary>
public sealed record TuneGridLayout(int CellWidth, int CellHeight, int LabelWidth, int BeatBarHeight, int LayerGap)
{
    public const int BEAT_BAR_GAP = 2;
    public const int COLUMNS_PER_BAR = 8;

    /// <summary>The composer: 8 x 7 cells, row names, the bar numbers above.</summary>
    public static readonly TuneGridLayout Composer = new(8, 7, 18, 12, 3);

    /// <summary>The reader: 6 x 4 cells (384 wide, like a drawing), no names, no beat bar.</summary>
    public static readonly TuneGridLayout Reader = new(6, 4, 0, 0, 3);

    public static readonly TuneLayer[] Layers = [TuneLayer.Melody, TuneLayer.Bass, TuneLayer.Drums];

    public int GridX => LabelWidth;
    public int GridWidth => CollegeProtocol.TUNE_STEPS * CellWidth;
    public int Width => LabelWidth + GridWidth;
    public int Height => LayerTop(TuneLayer.Drums) + LayerHeight(TuneLayer.Drums);

    public int LayerHeight(TuneLayer layer) => CollegeProtocol.TuneRows(layer) * CellHeight;

    public int LayerTop(TuneLayer layer)
        => layer switch
        {
            TuneLayer.Melody => BeatBarHeight > 0 ? BeatBarHeight + BEAT_BAR_GAP : 0,
            TuneLayer.Bass   => LayerTop(TuneLayer.Melody) + LayerHeight(TuneLayer.Melody) + LayerGap,
            _                => LayerTop(TuneLayer.Bass) + LayerHeight(TuneLayer.Bass) + LayerGap
        };

    public int RowTop(TuneLayer layer, int row) => LayerTop(layer) + ((CollegeProtocol.TuneRows(layer) - 1 - row) * CellHeight);

    /// <summary>The column under <paramref name="x" />, kept inside the grid (for a drag that leaves it).</summary>
    public int ColumnAt(int x) => Math.Clamp((int)Math.Floor((x - GridX) / (double)CellWidth), 0, CollegeProtocol.TUNE_STEPS - 1);

    public bool TryHitCell(int x, int y, out TuneLayer layer, out int row, out int column)
    {
        layer = TuneLayer.Melody;
        row = 0;
        column = 0;

        if ((x < GridX) || (x >= Width))
            return false;

        foreach (var candidate in Layers)
        {
            var top = LayerTop(candidate);

            if ((y < top) || (y >= (top + LayerHeight(candidate))))
                continue;

            layer = candidate;
            row = CollegeProtocol.TuneRows(candidate) - 1 - ((y - top) / CellHeight);
            column = (x - GridX) / CellWidth;

            return true;
        }

        return false;
    }

    /// <summary>The bar (0-7) whose number is under the point, or null outside the beat bar.</summary>
    public int? BarAt(int x, int y)
        => (BeatBarHeight > 0) && (y >= 0) && (y < BeatBarHeight) && (x >= GridX) && (x < Width)
            ? (x - GridX) / (COLUMNS_PER_BAR * CellWidth)
            : null;
}
