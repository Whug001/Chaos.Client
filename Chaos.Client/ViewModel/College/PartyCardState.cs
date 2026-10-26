using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;

namespace Chaos.Client.ViewModel.College;

public enum PartyCardView
{
    Write,
    Draw,
    Note,
    Vote,
    Reveal,
    Final
}

/// <summary>
///     What the party card shows from the latest server message, and which of its prompts is shown. The shown prompt
///     moves on after a send and goes back to the first unanswered one when the step changes.
/// </summary>
public sealed class PartyCardState
{
    private PartyPhase StepPhase = PartyPhase.Waiting;
    private byte StepRound;
    private byte StepItem;

    public CollegePartyInfo Info { get; private set; } = new();

    /// <summary>The shown prompt's index into <see cref="CollegePartyInfo.MyPrompts" />.</summary>
    public int Shown { get; private set; }

    /// <summary>The write step plus the shown prompt: changes whenever the step or the shown prompt does.</summary>
    public (PartyPhase Phase, byte Round, byte Item, int Shown) WriteKey => (Info.Phase, Info.Round, Info.Item, Shown);

    public PartyCardView View => ViewOf(Info);

    public CollegePartyPromptInfo? ShownPrompt => (Shown >= 0) && (Shown < Info.MyPrompts.Count) ? Info.MyPrompts[Shown] : null;

    public static PartyCardView ViewOf(CollegePartyInfo info)
    {
        if (info.Phase == PartyPhase.Revealed)
            return PartyCardView.Reveal;

        if (info.Phase == PartyPhase.Final)
            return PartyCardView.Final;

        if ((info.Phase == PartyPhase.Writing) && info.MyPrompts.Any(prompt => !prompt.Answered))
            return PartyCardView.Write;

        if ((info.Phase == PartyPhase.Drawing) && info.MyPrompts.Any(prompt => !prompt.Answered))
            return PartyCardView.Draw;

        if ((info.Phase == PartyPhase.Voting) && info.CanVote)
            return PartyCardView.Vote;

        return PartyCardView.Note;
    }

    public void Apply(CollegePartyInfo info)
    {
        Info = info;

        if ((info.Phase != StepPhase) || (info.Round != StepRound) || (info.Item != StepItem))
        {
            StepPhase = info.Phase;
            StepRound = info.Round;
            StepItem = info.Item;
            Shown = FirstUnanswered();
        }
        else if ((Shown < 0) || (Shown >= Info.MyPrompts.Count) || Info.MyPrompts[Shown].Answered)
            NoteSent();

        if (Shown >= Info.MyPrompts.Count)
            Shown = Math.Max(0, Info.MyPrompts.Count - 1);
    }

    /// <summary>Moves the shown prompt past the one just sent, to the next unanswered one.</summary>
    public void NoteSent()
    {
        var next = NextUnanswered(Shown);

        if (next >= 0)
        {
            Shown = next;

            return;
        }

        for (var i = 0; (i < Shown) && (i < Info.MyPrompts.Count); i++)
            if (!Info.MyPrompts[i].Answered)
            {
                Shown = i;

                return;
            }
    }

    private int FirstUnanswered()
    {
        for (var i = 0; i < Info.MyPrompts.Count; i++)
            if (!Info.MyPrompts[i].Answered)
                return i;

        return 0;
    }

    private int NextUnanswered(int from)
    {
        for (var i = from + 1; i < Info.MyPrompts.Count; i++)
            if (!Info.MyPrompts[i].Answered)
                return i;

        return -1;
    }
}
