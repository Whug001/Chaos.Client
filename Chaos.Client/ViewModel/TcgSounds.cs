#region
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>
///     Legend.dat sound ids for the card game window, and which match moment plays each one. Pure, so the window only
///     decides when to ask.
/// </summary>
public static class TcgSounds
{
    public const int CARD_DRAWN = 187;
    public const int CREATURE_PLAYED = 188;
    public const int SPELL_PLAYED = 189;
    public const int TRAP_SET = 190;
    public const int TRAP_REVEALED = 191;
    public const int ATTACK = 192;
    public const int BLOCK = 193;
    public const int CREATURE_DAMAGED = 194;
    public const int PLAYER_DAMAGED = 195;
    public const int CREATURE_DESTROYED = 196;
    public const int YOUR_TURN = 197;
    public const int VICTORY = 198;
    public const int DEFEAT = 199;
    public const int BELL = 200;
    public const int COUNTDOWN_TICK = 201;

    /// <summary>
    ///     The sound for an event when the animation queue reaches it. A cast whose card is a creature stays silent:
    ///     the cast animation still plays, and CREATURE_PLAYED follows when it enters.
    /// </summary>
    public static int? ForQueuedEvent(TcgEventView e, Func<string, TcgCatalogCardView?>? lookup = null)
        => e.Type switch
        {
            TcgEventType.CreatureEntered   => CREATURE_PLAYED,
            TcgEventType.CardCast when lookup?.Invoke(e.Key) is { Type: TcgCardType.Creature } => null,
            TcgEventType.CardCast          => SPELL_PLAYED,
            TcgEventType.TrapRevealed      => TRAP_REVEALED,
            TcgEventType.Attacked          => ATTACK,
            TcgEventType.Blocked           => BLOCK,
            TcgEventType.Damaged           => CREATURE_DAMAGED,
            TcgEventType.PlayerDamaged     => PLAYER_DAMAGED,
            TcgEventType.CreatureDestroyed => CREATURE_DESTROYED,
            _                              => null
        };

    /// <summary>The sound for an event with no animation, played when its update arrives.</summary>
    public static int? ForAppliedEvent(TcgEventView e)
        => e.Type switch
        {
            TcgEventType.CardDrawn => CARD_DRAWN,
            TcgEventType.TrapSet   => TRAP_SET,
            _                      => null
        };

    public static int? ForTurnStart(TcgEventView started, TcgSeat yourSeat, bool spectating)
        => !spectating && (started.Seat == yourSeat) ? YOUR_TURN : null;

    public static int ForOutcome(TcgEndOutcome outcome)
        => outcome switch
        {
            TcgEndOutcome.Victory => VICTORY,
            TcgEndOutcome.Defeat  => DEFEAT,
            _                     => BELL
        };

    /// <summary>
    ///     True when the window should play the countdown tick this frame: the viewer (not a spectator) is the decider
    ///     of a live decision with 5 s or less left, and that second has not ticked yet. The window passes back the
    ///     second it ticked as <paramref name="lastTickedSecond" />, and clears it when a new decision arrives.
    /// </summary>
    public static bool ShouldTickDecision(TcgDisplayArgs board, int secondsLeft, int? lastTickedSecond)
        => !board.Spectating
            && (board.Decision != TcgDecision.None)
            && (board.Decider == board.YourSeat)
            && (secondsLeft >= 1)
            && (secondsLeft <= 5)
            && (lastTickedSecond != secondsLeft);
}
