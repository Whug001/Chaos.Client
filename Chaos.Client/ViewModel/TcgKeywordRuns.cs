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
    private static readonly string[] Names = TcgKeywordText.All
                                                           .Select(entry => entry.Name)
                                                           .OrderByDescending(name => name.Length)
                                                           .ToArray();

    public static List<TcgKeywordRun> Find(string line)
    {
        var runs = new List<TcgKeywordRun>();

        foreach (var name in Names)
            for (var index = line.IndexOf(name, StringComparison.Ordinal);
                 index >= 0;
                 index = line.IndexOf(name, index + 1, StringComparison.Ordinal))
                if (IsWholeWord(line, index, name.Length) && !runs.Any(run => Overlaps(run, index, name.Length)))
                    runs.Add(new TcgKeywordRun(index, name.Length, name));

        runs.Sort((a, b) => a.Index.CompareTo(b.Index));

        return runs;
    }

    private static bool IsWholeWord(string line, int index, int length)
        => ((index == 0) || !char.IsLetter(line[index - 1]))
           && ((index + length >= line.Length) || !char.IsLetter(line[index + length]));

    private static bool Overlaps(TcgKeywordRun run, int index, int length)
        => (index < run.Index + run.Length) && (run.Index < index + length);
}
