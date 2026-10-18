#region
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

public enum TcgEndOutcome : byte
{
    Victory,
    Defeat,
    Draw
}

public static class TcgMatchText
{
    public static string PhaseLine(TcgPhase phase, TcgSeat active, TcgSeat you, string opponent)
    {
        var name = phase switch
        {
            TcgPhase.Redraw => null,
            TcgPhase.Main1  => "Main phase 1",
            TcgPhase.Combat => "Combat",
            TcgPhase.Main2  => "Main phase 2",
            _               => null
        };

        if (name is null)
            return phase == TcgPhase.Redraw ? "Redraw" : "Match over";

        return active == you ? $"{name}, your turn" : $"{name}, {opponent}'s turn";
    }

    /// <summary>One log line for an event, or null when the event is bookkeeping the player doesn't need to read.</summary>
    public static string? LogLine(TcgEventView e, TcgSeat you, IReadOnlyList<string> names, Func<string, string> cardName)
    {
        var mine = e.Seat == you;
        var who = mine ? "You" : names[(int)e.Seat];
        var card = string.IsNullOrEmpty(e.Key) ? "a card" : cardName(e.Key);

        return e.Type switch
        {
            TcgEventType.CardDrawn         => mine && !string.IsNullOrEmpty(e.Key) ? $"You drew {card}." : null,
            TcgEventType.Redrew            => $"{who} put back {e.Amount} card{(e.Amount == 1 ? "" : "s")}.",
            TcgEventType.ManaPlaced        => $"{who} placed mana.",
            TcgEventType.CardCast          => $"{who} cast {card}.",
            TcgEventType.CreatureEntered   => $"{card} entered play.",
            TcgEventType.TrapSet           => $"{who} set a trap.",
            TcgEventType.TrapRevealed      => $"Trap revealed: {card}.",
            TcgEventType.SpellResolved     => $"{card} resolved.",
            TcgEventType.SpellCountered    => $"{card} was countered.",
            TcgEventType.Attacked          => $"{who} attacked.",
            TcgEventType.Blocked           => $"{who} blocked.",
            TcgEventType.Damaged           => $"A creature took {e.Amount} damage.",
            TcgEventType.PlayerDamaged     => mine ? $"You take {e.Amount} damage." : $"{who} takes {e.Amount} damage.",
            TcgEventType.LifeGained        => mine ? $"You gain {e.Amount} life." : $"{who} gains {e.Amount} life.",
            TcgEventType.Pumped            => $"{card} grew stronger.",
            TcgEventType.CreatureDestroyed => $"{card} was destroyed.",
            TcgEventType.CardDiscarded     => $"{who} discarded {card}.",
            TcgEventType.MatchEnded        => "The match is over.",
            _                              => null
        };
    }

    public static string RejectText(TcgRejectReason reason)
        => reason switch
        {
            TcgRejectReason.MatchOver         => "The match is over.",
            TcgRejectReason.NotInMatch        => "You are not in a match.",
            TcgRejectReason.NotYourDecision   => "It is not your turn to act.",
            TcgRejectReason.WrongPhase        => "You can't do that in this phase.",
            TcgRejectReason.NotInHand         => "That card is not in your hand.",
            TcgRejectReason.AlreadyPlacedMana => "You already placed mana this turn.",
            TcgRejectReason.CannotPay         => "You can't pay for that.",
            TcgRejectReason.InvalidTarget     => "That is not a legal target.",
            TcgRejectReason.TrapLimit         => "You already have 3 traps set.",
            TcgRejectReason.WrongCardType     => "You can't play that card now.",
            TcgRejectReason.NotACreature      => "That is not one of your creatures.",
            TcgRejectReason.CannotAttack      => "That creature can't attack.",
            TcgRejectReason.CannotBlock       => "That creature can't block.",
            TcgRejectReason.GuardMustBlock    => "A Guard creature must block first.",
            TcgRejectReason.AlreadyRedrew     => "You already redrew.",
            TcgRejectReason.InvalidOrder      => "That damage order is not valid.",
            _                                 => string.Empty
        };

    public static string EndReasonText(TcgEndReason reason)
        => reason switch
        {
            TcgEndReason.Life       => "Life reached zero.",
            TcgEndReason.EmptyDeck  => "A deck ran out.",
            TcgEndReason.Concede    => "A player conceded.",
            TcgEndReason.Disconnect => "A player stayed disconnected.",
            TcgEndReason.Draw       => "Both players fell together.",
            _                       => "The match ended."
        };

    public static string BannerText(TcgEndOutcome outcome)
        => outcome switch
        {
            TcgEndOutcome.Victory => "Victory",
            TcgEndOutcome.Defeat  => "Defeat",
            _                     => "Draw"
        };

    public static string StakesResult(
        TcgEndOutcome outcome,
        TcgStakes stakes,
        int gold,
        IReadOnlyList<TcgAnteView> ante,
        TcgSeat you,
        Func<string, string> cardName)
    {
        if (stakes == TcgStakes.None)
            return string.Empty;

        if (outcome == TcgEndOutcome.Draw)
            return "Both stakes were returned.";

        if (stakes == TcgStakes.Gold)
            return outcome == TcgEndOutcome.Victory ? $"You won {gold * 2L:N0} gold." : $"You lost {gold:N0} gold.";

        string Describe(TcgAnteView card)
            => card.Printing == TcgPrinting.Normal ? cardName(card.Key) : $"{cardName(card.Key)} ({card.Printing.Label.ToLowerInvariant()})";

        var yours = ante.FirstOrDefault(card => card.Seat == you);
        var theirs = ante.FirstOrDefault(card => card.Seat != you);

        if (outcome == TcgEndOutcome.Victory)
            return theirs is null ? string.Empty : $"You won {Describe(theirs)}{(yours is null ? "" : $" and kept {Describe(yours)}")}.";

        return yours is null ? string.Empty : $"You lost {Describe(yours)}.";
    }
}
