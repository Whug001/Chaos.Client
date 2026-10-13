using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;

namespace Chaos.Client.ViewModel.College;

/// <summary>
///     What a Scramble card shows from the latest server message. A wrong-guess card marks the guesser's card, and the mark
///     stays through the broadcasts that follow until the student guesses again or the next word comes.
/// </summary>
public sealed class ScrambleCardState
{
    public const string WRONG_TEXT = "Not quite. Keep trying.";

    private int WrongNumber = -1;

    public CollegeScrambleInfo Info { get; private set; } = new();

    public bool Revealed => Info.Phase is ScramblePhase.Revealed or ScramblePhase.Ended;

    public bool ShowWrong => (Info.Phase == ScramblePhase.Open) && (Info.MyPlace == 0) && (WrongNumber == Info.Number);

    public bool BoxOpen => (Info.Phase == ScramblePhase.Open) && (Info.MyPlace == 0);

    public string Tiles => Revealed && (Info.Word.Length > 0) ? Info.Word : Info.Scrambled;

    public void Apply(CollegeScrambleInfo info)
    {
        Info = info;

        if (info.WrongGuess)
            WrongNumber = info.Number;
        else if (WrongNumber != info.Number)
            WrongNumber = -1;
    }

    public void NoteGuess() => WrongNumber = -1;

    public static string Title(CollegeScrambleInfo info) => $"Scramble {info.Number}/{info.Count}: {info.Title}";

    public static string SolvedLine(CollegeScrambleInfo info) => $"{info.SolvedCount} of {info.Students} got it";

    /// <summary>"Solved: Aroha (3), you (2), Bryn, Kael (1)": points follow the last name that earned them.</summary>
    public static string SolversLine(CollegeScrambleInfo info, string viewer)
    {
        if (info.Solvers.Count == 0)
            return "No one solved it.";

        var parts = new List<string>();

        for (var i = 0; i < info.Solvers.Count; i++)
        {
            var name = info.Solvers[i].Equals(viewer, StringComparison.OrdinalIgnoreCase) ? "you" : info.Solvers[i];
            var points = ClassToolText.ScramblePoints(i + 1);
            var last = (i == info.Solvers.Count - 1) || (ClassToolText.ScramblePoints(i + 2) != points);
            parts.Add(last ? $"{name} ({points})" : name);
        }

        return "Solved: " + string.Join(", ", parts);
    }

    public static string TeacherStatus(CollegeScrambleInfo info, int secondsLeft)
        => info.Phase == ScramblePhase.Open
            ? $"Solved {info.SolvedCount} of {info.Students} · {ClassToolText.Clock(secondsLeft)} left"
            : $"Solved {info.SolvedCount} of {info.Students}";
}
