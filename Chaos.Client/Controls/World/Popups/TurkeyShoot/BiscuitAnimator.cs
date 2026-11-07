namespace Chaos.Client.Controls.World.Popups.TurkeyShoot;

public enum BiscuitAct
{
    Nap,
    Wake,
    Alert,
    Bound,
    Feathers,
    ChaseTail,
    Flop,
    Spin,
    Wag
}

/// <summary>
///     Biscuit the farm dog, drawn from a 20-frame sheet. Each act plays its frames, then moves on. Biscuit never
///     laughs at a miss, never holds up a bird and never dives into the wheat.
/// </summary>
/// <param name="random">Returns a number from 0 up to the given limit. Tests pass a fixed roll.</param>
public sealed class BiscuitAnimator(Func<int, int> random)
{
    //DurationMs 0 means the act holds until a cue changes it
    private static readonly Dictionary<BiscuitAct, ActSpec> Acts = new()
    {
        [BiscuitAct.Nap] = new ActSpec(0, 2, 600, true, 0, BiscuitAct.Nap),
        [BiscuitAct.Wake] = new ActSpec(2, 2, 200, false, 400, BiscuitAct.Alert),
        [BiscuitAct.Alert] = new ActSpec(3, 1, 1_000, false, 0, BiscuitAct.Alert),
        [BiscuitAct.Bound] = new ActSpec(4, 4, 120, false, 480, BiscuitAct.Feathers),
        [BiscuitAct.Feathers] = new ActSpec(8, 2, 250, true, 1_500, BiscuitAct.Alert),
        [BiscuitAct.ChaseTail] = new ActSpec(10, 4, 100, true, 1_600, BiscuitAct.Nap),
        [BiscuitAct.Flop] = new ActSpec(14, 2, 300, false, 1_600, BiscuitAct.Nap),
        [BiscuitAct.Spin] = new ActSpec(16, 2, 120, true, 1_200, BiscuitAct.Alert),
        [BiscuitAct.Wag] = new ActSpec(18, 2, 200, true, 0, BiscuitAct.Wag)
    };

    private int ActMs;
    private bool NapWhenDone;

    public BiscuitAct Act { get; private set; } = BiscuitAct.Nap;

    public int Frame
    {
        get
        {
            var spec = Acts[Act];
            var step = ActMs / spec.FrameMs;

            return spec.FirstFrame + (spec.Loop ? step % spec.FrameCount : Math.Min(step, spec.FrameCount - 1));
        }
    }

    public void Play(BiscuitAct act)
    {
        Act = act;
        ActMs = 0;
        NapWhenDone = false;
    }

    public void Cue(TurkeyShootCue cue)
    {
        if (Act == BiscuitAct.Wag)
            return;
        switch (cue)
        {
            case TurkeyShootCue.FlockTakeoff:
                Play(BiscuitAct.Wake);

                break;
            case TurkeyShootCue.GoldenTakeoff:
                Play(BiscuitAct.Spin);

                break;
            case TurkeyShootCue.TurkeyHit or TurkeyShootCue.GoldenHit:
                Play(BiscuitAct.Bound);

                break;
            case TurkeyShootCue.FlockEscaped:
                Play(random(2) == 0 ? BiscuitAct.ChaseTail : BiscuitAct.Flop);

                break;
            case TurkeyShootCue.FlockCleared:
                if (Act == BiscuitAct.Alert)
                    Play(BiscuitAct.Nap);
                else
                    NapWhenDone = true;

                break;
            case TurkeyShootCue.Results:
                Play(BiscuitAct.Wag);

                break;
        }
    }

    public void Tick(int elapsedMs)
    {
        ActMs += elapsedMs;

        var spec = Acts[Act];

        if ((spec.DurationMs <= 0) || (ActMs < spec.DurationMs))
            return;

        var next = NapWhenDone && (spec.Next == BiscuitAct.Alert) ? BiscuitAct.Nap : spec.Next;
        var napWhenDone = NapWhenDone && (next != BiscuitAct.Nap);
        Play(next);
        NapWhenDone = napWhenDone;
    }

    private sealed record ActSpec(int FirstFrame, int FrameCount, int FrameMs, bool Loop, int DurationMs, BiscuitAct Next);
}
