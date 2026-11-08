using Chaos.DarkAges.BrassRing;

namespace Chaos.Client.Controls.World.Popups.BrassRing;

/// <summary>A stand-in opponent for judging the fight locally. It uses only the moves a real player has.</summary>
public sealed class BrassRingDummy(Func<int, int, int> next)
{
    public const int FIRST_WAIT_MS = 1200;
    public const int REACTION_MS = 200;
    private const int MIN_WAIT_MS = 800;
    private const int MAX_WAIT_MS = 1800;
    private const int DODGE_PERCENT = 50;

    private bool Noticed;
    private int ReactMs = -1;
    private int WaitMs = FIRST_WAIT_MS;

    public BrassRingMove? Tick(int ms, BrassRingFight fight, int self)
    {
        var me = fight.Fighters[self];
        var pose = fight.Fighters[1 - self].Pose;
        var incoming = pose is BrassRingPose.WindUpLeft or BrassRingPose.WindUpRight;

        if (!incoming)
        {
            Noticed = false;
            ReactMs = -1;
        } else if (!Noticed)
        {
            Noticed = true;
            ReactMs = next(0, 100) < DODGE_PERCENT ? REACTION_MS : -1;

            return null;
        } else if (ReactMs >= 0)
        {
            ReactMs -= ms;

            if (ReactMs <= 0)
            {
                ReactMs = -1;

                return pose == BrassRingPose.WindUpLeft ? BrassRingMove.DodgeLeft : BrassRingMove.DodgeRight;
            }
        }

        if (me.Pose != BrassRingPose.Idle)
            return null;

        WaitMs -= ms;

        if ((me.CounterMs <= 0) && (WaitMs > 0))
            return null;

        WaitMs = next(MIN_WAIT_MS, MAX_WAIT_MS);

        return next(0, 2) == 0 ? BrassRingMove.PunchLeft : BrassRingMove.PunchRight;
    }
}
