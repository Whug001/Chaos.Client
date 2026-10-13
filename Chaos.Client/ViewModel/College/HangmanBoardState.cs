using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;

namespace Chaos.Client.ViewModel.College;

public enum HangmanLetterState
{
    Untried,
    Right,
    Wrong
}

/// <summary>What the Hangman board shows and which controls are live, worked out from one server board.</summary>
public sealed class HangmanBoardState(CollegeHangmanInfo info)
{
    public CollegeHangmanInfo Info { get; } = info;

    public bool LettersEnabled => Info.MyTurn && (Info.Phase == HangmanPhase.Open);
    public bool ShowBuzz => Info.CanBuzz && !Info.ISolve;
    public bool ShowSolveBox => Info.ISolve;

    /// <summary>Survival draws the gallows; Casual shows the miss count instead.</summary>
    public bool ShowGallows => Info.Mode == HangmanMode.Survival;

    public int GallowsStage => ShowGallows ? Math.Min((int)Info.Misses, CollegeProtocol.HANGMAN_MAX_MISSES) : 0;

    public HangmanLetterState LetterState(char letter)
    {
        letter = char.ToUpperInvariant(letter);

        if (Info.Tried.IndexOf(letter, StringComparison.OrdinalIgnoreCase) < 0)
            return HangmanLetterState.Untried;

        var known = Info.Word.Length > 0 ? Info.Word : Info.Mask;

        return known.Contains(letter, StringComparison.OrdinalIgnoreCase) ? HangmanLetterState.Right : HangmanLetterState.Wrong;
    }
}
