#region
using System.Text;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Hud;

/// <summary>
///     Pure geometry and text for the pinned-quest tracker: where it sits in the viewport and how the stage text
///     wraps. Kept free of rendering types so the rules are unit-testable.
/// </summary>
public static class QuestTrackerLayout
{
    public const int WIDTH = 180;
    public const int MARGIN = 8;
    public const int MAX_TEXT_LINES = 3;

    private const string ELLIPSIS = "...";

    /// <summary>Top-right inside the viewport, left of any bars docked on the right.</summary>
    public static Point Position(Rectangle viewport, int rightReserved)
        => new(viewport.Right - MARGIN - rightReserved - WIDTH, viewport.Y + MARGIN);

    /// <summary>One progress line, for example "Rats: 3 / 5".</summary>
    public static string ProgressLine(QuestLogProgressInfo p) => $"{p.Label}: {p.Have} / {p.Need}";

    /// <summary>
    ///     Greedy word-wrap capped at <see cref="MAX_TEXT_LINES" />. A word longer than the width hard-breaks.
    ///     When text is cut, the last line ends in "..." and still fits <paramref name="maxWidth" />.
    /// </summary>
    public static IReadOnlyList<string> StageLines(string text, Func<string, int> measure, int maxWidth)
    {
        var lines = new List<string>();
        var current = new StringBuilder();

        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            foreach (var piece in SplitLongWord(word, measure, maxWidth))
            {
                var candidate = current.Length == 0 ? piece : current + " " + piece;

                if (measure(candidate) <= maxWidth)
                    current.Append(current.Length == 0 ? piece : " " + piece);
                else
                {
                    lines.Add(current.ToString());
                    current.Clear().Append(piece);
                }
            }

        if (current.Length > 0)
            lines.Add(current.ToString());

        if (lines.Count <= MAX_TEXT_LINES)
            return lines;

        var kept = lines.Take(MAX_TEXT_LINES).ToList();
        kept[MAX_TEXT_LINES - 1] = Ellipsize(kept[MAX_TEXT_LINES - 1], measure, maxWidth);

        return kept;
    }

    private static IEnumerable<string> SplitLongWord(string word, Func<string, int> measure, int maxWidth)
    {
        if (measure(word) <= maxWidth)
        {
            yield return word;

            yield break;
        }

        var piece = new StringBuilder();

        foreach (var ch in word)
        {
            var candidate = piece + ch.ToString();

            if ((piece.Length > 0) && (measure(candidate) > maxWidth))
            {
                yield return piece.ToString();
                piece.Clear().Append(ch);
            } else
                piece.Append(ch);
        }

        if (piece.Length > 0)
            yield return piece.ToString();
    }

    private static string Ellipsize(string line, Func<string, int> measure, int maxWidth)
    {
        if (measure(line + ELLIPSIS) <= maxWidth)
            return line + ELLIPSIS;

        var trimmed = line;

        while ((trimmed.Length > 0) && (measure(trimmed + ELLIPSIS) > maxWidth))
            trimmed = trimmed[..^1];

        return trimmed + ELLIPSIS;
    }
}
