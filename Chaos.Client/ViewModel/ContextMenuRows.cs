namespace Chaos.Client.ViewModel;

/// <summary>
///     The row arithmetic of the player menu (<c>AislingContextMenu</c>): which option a y coordinate is on, where an
///     option's row starts, and how the popup art, which fits three options, is made taller by repeating its third
///     option's band. Pure: no textures.
/// </summary>
public static class ContextMenuRows
{
    /// <summary>The height of the name row and of every option row.</summary>
    public const int ROW_HEIGHT = 14;

    /// <summary>Where the name row starts.</summary>
    public const int NAME_TOP = 6;

    /// <summary>The gap between the name row and the first option.</summary>
    public const int OPTIONS_GAP = 2;

    /// <summary>Where the first option starts.</summary>
    public const int OPTIONS_TOP = NAME_TOP + OPTIONS_GAP + ROW_HEIGHT;

    /// <summary>The width of a row's text.</summary>
    public const int ROW_WIDTH = 74;

    /// <summary>The options the art fits as drawn.</summary>
    public const int ART_ROWS = 3;

    /// <summary>The art's row just under its third option: everything from here down moves lower in a taller frame.</summary>
    public const int ART_BAND_END = OPTIONS_TOP + (ART_ROWS * ROW_HEIGHT);

    /// <summary>The fifth option: a friendly tamer bout with the clicked player.</summary>
    public const string TamerBout = "tamer bout";

    /// <summary>The top of an option's row.</summary>
    public static int RowTop(int row) => OPTIONS_TOP + (row * ROW_HEIGHT);

    /// <summary>The option a y coordinate inside the menu is on, or -1 above the first or below the last.</summary>
    public static int RowAt(int y, int rowCount)
    {
        if (y < OPTIONS_TOP)
            return -1;

        var row = (y - OPTIONS_TOP) / ROW_HEIGHT;

        return row < rowCount ? row : -1;
    }

    /// <summary>The bands added to the art for a menu of <paramref name="rowCount" /> options.</summary>
    public static int ExtraBands(int rowCount) => Math.Max(0, rowCount - ART_ROWS);

    /// <summary>The frame's height for a menu of <paramref name="rowCount" /> options: one band more per option past three.</summary>
    public static int FrameHeight(int sourceHeight, int rowCount) => sourceHeight + (ExtraBands(rowCount) * ROW_HEIGHT);

    /// <summary>
    ///     The art row a row of the taller frame is copied from: rows above the third option's end stay put, each added
    ///     band is the third option's band again, and the rest follows moved down.
    /// </summary>
    public static int SourceRow(int y, int extraBands)
    {
        if (y < ART_BAND_END)
            return y;

        var added = Math.Max(0, extraBands) * ROW_HEIGHT;

        return y < (ART_BAND_END + added) ? ART_BAND_END - ROW_HEIGHT + ((y - ART_BAND_END) % ROW_HEIGHT) : y - added;
    }
}
