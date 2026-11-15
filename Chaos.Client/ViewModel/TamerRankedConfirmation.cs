#region
using Chaos.Networking.Entities.Client;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>What a Yes to a ranked question came to.</summary>
public enum TamerRankedAnswer : byte
{
    /// <summary>Nothing changed and the button is still on: the request goes.</summary>
    Send = 0,

    /// <summary>New data arrived while the question was up, so the question shown no longer matches: nothing goes.</summary>
    Changed = 1,

    /// <summary>The data is the same but the request no longer applies (another log entry was selected): nothing goes.</summary>
    Refused = 2
}

/// <summary>
///     One ranked question as it was asked: what it was about and the revisions of the data on screen then. A Yes
///     sends only when no relevant display or snapshot has been applied since and the button would still be on. Set
///     defence, Attack and Revenge each field or save a squad, so they watch the stable's revision as well as the
///     ladder's; Refresh watches the ladder's alone. Pure: no textures.
/// </summary>
public sealed class TamerRankedConfirmation
{
    private enum QuestionKind : byte
    {
        SetDefence,
        Refresh,
        Attack,
        Revenge
    }

    private readonly QuestionKind Kind;
    private readonly int Number;
    private readonly string Name;
    private readonly Guid EntryId;
    private readonly int RankedRevision;
    private readonly int StableRevision;
    private readonly bool WatchesStable;

    private TamerRankedConfirmation(
        QuestionKind kind,
        string question,
        TamerRankedState ranked,
        TamerStableState stable,
        int number = 0,
        string name = "",
        Guid entryId = default)
    {
        Kind = kind;
        Question = question;
        Number = number;
        Name = name;
        EntryId = entryId;
        RankedRevision = ranked.Revision;
        StableRevision = stable.Revision;
        WatchesStable = kind != QuestionKind.Refresh;
    }

    /// <summary>The words the window asks.</summary>
    public string Question { get; }

    /// <summary>The Set from squad question for a 0-based squad, or null while that button is off.</summary>
    public static TamerRankedConfirmation? SetDefence(TamerRankedState ranked, TamerStableState stable, byte squad)
    {
        if ((ranked is null) || (stable is null) || !ranked.CanSetDefence(squad, stable, out _))
            return null;

        return new TamerRankedConfirmation(QuestionKind.SetDefence, TamerRankedText.SetDefenceQuestion(squad), ranked, stable, squad);
    }

    /// <summary>The Refresh question, or null while that button is off.</summary>
    public static TamerRankedConfirmation? Refresh(TamerRankedState ranked, TamerStableState stable)
    {
        if ((ranked is null) || (stable is null) || !ranked.CanRefresh)
            return null;

        return new TamerRankedConfirmation(QuestionKind.Refresh, TamerRankedText.RefreshQuestion, ranked, stable);
    }

    /// <summary>The Attack question for an offer slot, or null while that button is off.</summary>
    public static TamerRankedConfirmation? Attack(TamerRankedState ranked, TamerStableState stable, int slot)
    {
        if ((ranked is null)
            || (stable is null)
            || !ranked.CanAttack(slot, stable, out _)
            || (ranked.OfferAt(slot) is not { } offer)
            || (ranked.AttackQuestion(slot) is not { } question))
            return null;

        return new TamerRankedConfirmation(QuestionKind.Attack, question, ranked, stable, slot, offer.Name);
    }

    /// <summary>The Revenge question for the selected log entry, or null while that button is off.</summary>
    public static TamerRankedConfirmation? Revenge(TamerRankedState ranked, TamerStableState stable)
    {
        if ((ranked is null)
            || (stable is null)
            || !ranked.CanRevenge(stable, out _)
            || (ranked.SelectedEntry is not { } entry)
            || (ranked.RevengeQuestion() is not { } question))
            return null;

        return new TamerRankedConfirmation(QuestionKind.Revenge, question, ranked, stable, entryId: entry.Id);
    }

    /// <summary>
    ///     The Yes: the request to send, or null with why not. New data since the question was asked voids it, even
    ///     when the button would still be on, because the player said Yes to what was on screen then.
    /// </summary>
    public TamerActionArgs? Confirm(TamerRankedState ranked, TamerStableState stable, out TamerRankedAnswer answer)
    {
        if ((ranked is null) || (stable is null))
        {
            answer = TamerRankedAnswer.Refused;

            return null;
        }

        if ((ranked.Revision != RankedRevision) || (WatchesStable && (stable.Revision != StableRevision)))
        {
            answer = TamerRankedAnswer.Changed;

            return null;
        }

        var action = Kind switch
        {
            QuestionKind.SetDefence => ranked.CanSetDefence(Number, stable, out _) ? TamerRankedState.SetDefenceAction((byte)Number) : null,
            QuestionKind.Refresh    => ranked.CanRefresh ? TamerRankedState.RefreshAction() : null,
            QuestionKind.Attack => ranked.CanAttack(Number, stable, out _) && (ranked.OfferAt(Number)?.Name == Name)
                ? TamerRankedState.AttackAction((byte)Number, Name)
                : null,
            QuestionKind.Revenge => (ranked.SelectedEntry?.Id == EntryId) && ranked.CanRevenge(stable, out _)
                ? TamerRankedState.RevengeAction(EntryId)
                : null,
            _ => null
        };

        answer = action is null ? TamerRankedAnswer.Refused : TamerRankedAnswer.Send;

        return action;
    }
}
