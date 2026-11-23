#region
using Chaos.Client.Definitions;
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
    public static bool Apply(CraneRevealState state, CraneRevealKey key)
        => key switch
        {
            CraneRevealKey.Advance => state.Flip() || state.Close(),
            CraneRevealKey.Close   => state.Close(),
            _                      => false
        };

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
