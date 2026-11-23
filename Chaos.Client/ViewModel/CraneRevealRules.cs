#region
using Chaos.Client.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>What a key does while the crane reveal is open.</summary>
public enum CraneRevealKey : byte
{
    /// <summary>Not the reveal's key: it may go on to the chat and panel toggles.</summary>
    None,

    /// <summary>Flips the waiting card, or closes a revealed one.</summary>
    Advance,

    /// <summary>Closes a revealed card.</summary>
    Close,

    /// <summary>Taken by the reveal and dropped, so it never reaches crane steering or walking.</summary>
    Swallow
}

/// <summary>
///     The crane reveal's input routing and text, kept apart from the control so tests can check them without drawing.
/// </summary>
public static class CraneRevealRules
{
    /// <summary>How long one gold light sweep takes to cross the face, and how often it repeats.</summary>
    public const double SWEEP_MS = 700;

    public const double SWEEP_PERIOD_MS = 2000;

    /// <summary>
    ///     Space advances and Escape closes; the arrows are swallowed. A held key's repeats are swallowed too, so a Space
    ///     still held from dropping the claw can't run straight through the flip and close.
    /// </summary>
    public static CraneRevealKey Classify(Scancode scancode, bool isRepeat)
        => scancode switch
        {
            Scancode.Space                                                => isRepeat ? CraneRevealKey.Swallow : CraneRevealKey.Advance,
            Scancode.Escape                                               => isRepeat ? CraneRevealKey.Swallow : CraneRevealKey.Close,
            Scancode.Up or Scancode.Down or Scancode.Left or Scancode.Right => CraneRevealKey.Swallow,
            _                                                             => CraneRevealKey.None
        };

    /// <summary>Applies a key or click to the reveal. True when it flipped or closed the card.</summary>
    public static bool Apply(CraneRevealState state, CraneRevealKey key) => Apply(state, key, out _);

    /// <summary>
    ///     Applies a key or click to the reveal. True when it flipped or closed the card; <paramref name="flipped" /> is
    ///     true only when this press turned the card face up, the moment the winner is told what they won.
    /// </summary>
    public static bool Apply(CraneRevealState state, CraneRevealKey key, out bool flipped)
    {
        flipped = false;

        switch (key)
        {
            case CraneRevealKey.Advance:
                if (state.Flip())
                {
                    flipped = true;

                    return true;
                }

                return state.Close();
            case CraneRevealKey.Close:
                return state.Close();
            default:
                return false;
        }
    }

    //prize type bytes follow the server's CranePrizeType
    private const byte LOGIN_EFFECT = 0;
    private const byte DEATH_EFFECT = 1;
    private const byte DEATH_SOUND = 2;
    private const byte CHAT_BUBBLE = 3;
    private const byte MOUNT = 4;
    private const byte MOUNT_COLOR = 5;
    private const byte EMBLEM = 6;

    /// <summary>What a non-item prize is, in the same words the server's room line uses; null for items.</summary>
    public static string? KindLabel(byte prizeType)
        => prizeType switch
        {
            LOGIN_EFFECT => "login effect",
            DEATH_EFFECT => "death effect",
            DEATH_SOUND  => "death sound",
            CHAT_BUBBLE  => "chat bubble",
            MOUNT        => "mount",
            MOUNT_COLOR  => "mount color",
            EMBLEM       => "emblem",
            _            => null
        };

    /// <summary>The card's line under the prize name saying what a non-item prize is and when it shows; null for items.</summary>
    public static string? KindLine(byte prizeType)
        => prizeType switch
        {
            LOGIN_EFFECT => "Login effect: plays when you log in",
            DEATH_EFFECT => "Death effect: plays when you die",
            DEATH_SOUND  => "Death sound: plays when you die",
            CHAT_BUBBLE  => "Chat bubble: frames what you say",
            MOUNT        => "Mount: a new mount to ride",
            MOUNT_COLOR  => "Mount color: a new color for your mount",
            EMBLEM       => "Emblem: added to your emblems",
            _            => null
        };

    /// <summary>The winner's own orange-bar line, shown when the card flips.</summary>
    public static string WinLine(CranePrizeRevealArgs prize)
    {
        var name = KindLabel(prize.PrizeType) is { } kind ? $"{prize.PrizeName} ({kind})" : prize.PrizeName;

        if (!prize.IsNew && (prize.TicketsGiven > 0))
            return $"Duplicate {name}: +{prize.TicketsGiven} Crane {(prize.TicketsGiven == 1 ? "Ticket" : "Tickets")}";

        return $"You won {name}!";
    }

    /// <summary>"NEW!" for a new prize, else the duplicate's ticket payout ("Ticket" at 1).</summary>
    public static string OutcomeText(bool isNew, ushort ticketsGiven)
    {
        if (isNew)
            return "NEW!";

        if (ticketsGiven == 0)
            return "Duplicate";

        return $"Duplicate: +{ticketsGiven} Crane {(ticketsGiven == 1 ? "Ticket" : "Tickets")}";
    }

    /// <summary>Where the gold light sweep is across the face (0 to 1), or null between sweeps.</summary>
    public static double? SweepProgress(byte tier, double faceUpMs)
    {
        if ((tier != 3) || (faceUpMs < 0))
            return null;

        var at = faceUpMs % SWEEP_PERIOD_MS;

        return at < SWEEP_MS ? at / SWEEP_MS : null;
    }

    /// <summary>The frame colour for a tier: Common green, Uncommon blue, Rare red, Gold gold.</summary>
    public static Color TierColor(byte tier)
        => tier switch
        {
            1 => new Color(64, 120, 220),
            2 => new Color(200, 48, 40),
            3 => new Color(232, 184, 48),
            _ => new Color(72, 168, 72)
        };
}
