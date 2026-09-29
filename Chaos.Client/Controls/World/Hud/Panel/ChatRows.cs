#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Hud.Panel;

/// <summary>
///     One wrapped chat row. <paramref name="LineId" /> ties the row to its chat-log message (0 = none) and
///     <paramref name="HoverText" /> is the tooltip shown over it (the untranslated original), or null.
/// </summary>
public record struct ChatLine(string Text, Color Color, uint LineId = 0, string? HoverText = null);

/// <summary>
///     Pure row-list logic for <see cref="ChatPanel" />, kept apart from the graphics-bound panel so it can be tested.
/// </summary>
public static class ChatRows
{
    /// <summary>
    ///     Word-wraps tooltip text to <paramref name="maxWidth" />. <paramref name="findBreak" /> returns the length of the
    ///     line that fits (TextRenderer.FindLineBreak in the panel). Lines are trimmed at the break, so only the spaces at
    ///     a break are dropped; the loop always advances at least one character.
    /// </summary>
    public static List<string> WrapTooltip(string text, int maxWidth, Func<string, int, int> findBreak)
    {
        var lines = new List<string>();
        var remaining = text;

        while (remaining.Length > 0)
        {
            var lineEnd = Math.Clamp(findBreak(remaining, maxWidth), 1, remaining.Length);

            var line = remaining[..lineEnd]
                .TrimEnd();

            if (line.Length > 0)
                lines.Add(line);

            remaining = remaining[lineEnd..]
                .TrimStart();
        }

        return lines;
    }

    /// <summary>
    ///     Y (panel-local) for a tooltip of <paramref name="height" /> beside the row at <paramref name="rowY" />: above
    ///     the row when it fits, else below it, else pinned to the top of the panel (never outside the visible area).
    /// </summary>
    public static int TooltipY(int rowY, int rowHeight, int height, int panelHeight)
    {
        if (height <= rowY - 1)
            return rowY - height - 1;

        if (rowY + rowHeight + 1 + height <= panelHeight)
            return rowY + rowHeight + 1;

        return 0;
    }

    /// <summary>
    ///     Replaces the rows of the newest message with <paramref name="lineId" /> by <paramref name="newRows" />, in
    ///     place. Returns the index the first old row sat at and how many rows were removed; (-1, 0) when no row carries
    ///     the id (nothing changes), e.g. the line already scrolled out of the panel.
    /// </summary>
    public static (int Index, int Removed) Replace(List<ChatLine> rows, uint lineId, IReadOnlyList<ChatLine> newRows)
    {
        if (lineId == 0)
            return (-1, 0);

        var last = -1;

        for (var i = rows.Count - 1; i >= 0; i--)
            if (rows[i].LineId == lineId)
            {
                last = i;

                break;
            }

        if (last < 0)
            return (-1, 0);

        //a message's rows are contiguous; walk up to its first row
        var first = last;

        while ((first > 0) && (rows[first - 1].LineId == lineId))
            first--;

        var removed = last - first + 1;
        rows.RemoveRange(first, removed);
        rows.InsertRange(first, newRows);

        return (first, removed);
    }
}
