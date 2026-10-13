using Chaos.Networking.Entities.Server;

namespace Chaos.Client.ViewModel.College;

public readonly record struct TurnStripEntry(string Name, bool Current, bool Viewer);

/// <summary>
///     The names the board's turn strip shows: up to five consecutive players starting one before the current one, and how
///     many more there are. Removed players are left out.
/// </summary>
public sealed class HangmanTurnStrip
{
    public const int MAX_SHOWN = 5;

    public HangmanTurnStrip(IReadOnlyList<CollegeHangmanPlayerInfo> players, string current, string viewer)
    {
        var playing = players.Where(p => !p.Removed).ToList();

        var index = current.Length == 0
            ? playing.FindIndex(p => p.IsTurn)
            : playing.FindIndex(p => p.Name.Equals(current, StringComparison.OrdinalIgnoreCase));

        var start = index < 0 ? 0 : Math.Clamp(index - 1, 0, Math.Max(0, playing.Count - MAX_SHOWN));
        var shown = playing.Skip(start).Take(MAX_SHOWN).ToList();

        Entries = shown.Select(
                           p => new TurnStripEntry(
                               p.Name,
                               playing.IndexOf(p) == index,
                               p.Name.Equals(viewer, StringComparison.OrdinalIgnoreCase)))
                       .ToList();

        More = playing.Count - shown.Count - start;
    }

    public IReadOnlyList<TurnStripEntry> Entries { get; }

    /// <summary>Players after the last name shown.</summary>
    public int More { get; }
}
