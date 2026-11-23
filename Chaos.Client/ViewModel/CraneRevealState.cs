#region
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>Where the crane prize reveal is in its timeline.</summary>
public enum CraneRevealPhase : byte
{
    Idle,

    /// <summary>The capsule pops open.</summary>
    Opening,

    /// <summary>The card slides out to its place.</summary>
    Arriving,

    /// <summary>The card waits, face down, for a click.</summary>
    FaceDown,

    Flipping,

    /// <summary>The prize shows; the next click closes the reveal.</summary>
    FaceUp
}

/// <summary>
///     The crane prize reveal's timeline: the capsule opens, the card arrives face down, a click flips it, and a
///     second click closes it. Time only moves in <see cref="Advance" />, so tests can step it.
/// </summary>
public sealed class CraneRevealState
{
    public const double ARRIVE_MS = 300;
    public const double FLIP_MS = TcgRevealState.FLIP_MS;
    public const double OPEN_MS = 300;

    //time spent in the current phase
    private double PhaseMs;

    public double FlipProgress
        => Phase switch
        {
            CraneRevealPhase.Flipping => Math.Clamp(PhaseMs / FLIP_MS, 0, 1),
            CraneRevealPhase.FaceUp   => 1,
            _                         => 0
        };

    public CraneRevealPhase Phase { get; private set; }
    public CranePrizeRevealArgs? Prize { get; private set; }

    public void Advance(double elapsedMs)
    {
        if (Phase is CraneRevealPhase.Idle or CraneRevealPhase.FaceDown or CraneRevealPhase.FaceUp)
            return;

        PhaseMs += elapsedMs;

        //carry any overflow into the next phase so a long frame doesn't lose time
        while (true)
        {
            var length = Phase switch
            {
                CraneRevealPhase.Opening  => OPEN_MS,
                CraneRevealPhase.Arriving => ARRIVE_MS,
                CraneRevealPhase.Flipping => FLIP_MS,
                _                         => double.PositiveInfinity
            };

            if (PhaseMs < length)
                return;

            PhaseMs -= length;

            Phase = Phase switch
            {
                CraneRevealPhase.Opening  => CraneRevealPhase.Arriving,
                CraneRevealPhase.Arriving => CraneRevealPhase.FaceDown,
                _                         => CraneRevealPhase.FaceUp
            };

            if (Phase is CraneRevealPhase.FaceDown or CraneRevealPhase.FaceUp)
            {
                PhaseMs = 0;

                return;
            }
        }
    }

    /// <summary>Closes a revealed card. Ignored in any other phase.</summary>
    public bool Close()
    {
        if (Phase != CraneRevealPhase.FaceUp)
            return false;

        Phase = CraneRevealPhase.Idle;
        Prize = null;
        PhaseMs = 0;

        return true;
    }

    /// <summary>Flips the waiting card. Ignored in any other phase.</summary>
    public bool Flip()
    {
        if (Phase != CraneRevealPhase.FaceDown)
            return false;

        Phase = CraneRevealPhase.Flipping;
        PhaseMs = 0;

        return true;
    }

    /// <summary>Starts a reveal, replacing any reveal still open.</summary>
    public void Start(CranePrizeRevealArgs prize)
    {
        Prize = prize;
        Phase = CraneRevealPhase.Opening;
        PhaseMs = 0;
    }

    public static string TierLabel(byte tier)
        => tier switch
        {
            1 => "Uncommon",
            2 => "Rare",
            3 => "Gold",
            _ => "Common"
        };
}
