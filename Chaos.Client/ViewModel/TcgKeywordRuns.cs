#region
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>Where a card keyword sits in one drawn line of rules text: its start, its length and its printed name.</summary>
public readonly record struct TcgKeywordRun(int Index, int Length, string Name);

/// <summary>
///     Finds card keywords in a line of rules text, the way the server printed them: case-sensitive, whole words only.
/// </summary>
public static class TcgKeywordRuns
{
    //longest first, so a name that is part of a longer one can never claim the longer one's text
    private static readonly string[] Names = TcgKeywordText.HoverableNames
                                                           .OrderByDescending(name => name.Length)
                                                           .ToArray();

    public static List<TcgKeywordRun> Find(string line)
    {
        var runs = new List<TcgKeywordRun>();

        foreach (var name in Names)
            for (var index = line.IndexOf(name, StringComparison.Ordinal);
                 index >= 0;
                 index = line.IndexOf(name, index + 1, StringComparison.Ordinal))
                if (IsWholeWord(line, index, name.Length) && !runs.Any(run => Overlaps(run.Index, run.Length, index, name.Length)))
                    runs.Add(new TcgKeywordRun(index, name.Length, name));

        runs.Sort((a, b) => a.Index.CompareTo(b.Index));

        return runs;
    }

    /// <summary>
    ///     Finds card keywords across wrapped lines of rules text. A wrap break stands where the rules text had a space,
    ///     so a space inside a name matches a space or a line break: a multi-word name split across lines reports one
    ///     run per drawn line it touches, each carrying the full name, so every fragment underlines and hover-resolves.
    /// </summary>
    public static List<List<TcgKeywordRun>> FindInLines(IReadOnlyList<string> lines)
    {
        var perLine = lines.Select(line => new List<TcgKeywordRun>()).ToList();

        if (lines.Count == 0)
            return perLine;

        var joined = string.Join('\n', lines);
        var starts = new int[lines.Count];
        var offset = 0;

        for (var i = 0; i < lines.Count; i++)
        {
            starts[i] = offset;
            offset += lines[i].Length + 1;
        }

        var claimed = new List<(int Index, int Length)>();

        foreach (var name in Names)
            for (var index = IndexOfName(joined, name, 0); index >= 0; index = IndexOfName(joined, name, index + 1))
            {
                if (!IsWholeWord(joined, index, name.Length) || claimed.Any(span => Overlaps(span.Index, span.Length, index, name.Length)))
                    continue;

                claimed.Add((index, name.Length));

                var end = index + name.Length;

                for (var line = 0; line < lines.Count; line++)
                {
                    var fragStart = Math.Max(index, starts[line]);
                    var fragEnd = Math.Min(end, starts[line] + lines[line].Length);

                    if (fragStart < fragEnd)
                        perLine[line].Add(new TcgKeywordRun(fragStart - starts[line], fragEnd - fragStart, name));
                }
            }

        foreach (var runs in perLine)
            runs.Sort((a, b) => a.Index.CompareTo(b.Index));

        return perLine;
    }

    //like IndexOf with StringComparison.Ordinal, except a space in the name also matches a line break
    private static int IndexOfName(string joined, string name, int start)
    {
        for (var i = start; i + name.Length <= joined.Length; i++)
        {
            var match = true;

            for (var j = 0; j < name.Length; j++)
            {
                var want = name[j];
                var got = joined[i + j];

                if (want == ' ' ? (got != ' ' && got != '\n') : (got != want))
                {
                    match = false;

                    break;
                }
            }

            if (match)
                return i;
        }

        return -1;
    }

    private static bool IsWholeWord(string line, int index, int length)
        => ((index == 0) || !char.IsLetter(line[index - 1]))
           && ((index + length >= line.Length) || !char.IsLetter(line[index + length]));

    private static bool Overlaps(int firstIndex, int firstLength, int secondIndex, int secondLength)
        => (secondIndex < firstIndex + firstLength) && (firstIndex < secondIndex + secondLength);
}
