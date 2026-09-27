using Chaos.DarkAges.Definitions;

namespace Chaos.Client.Controls.World.Popups.Lockpicking;

public enum LockpickAnimState
{
    Idle,
    Turning,
    Straining,
    Returning,
    Done,
    Snapped,
    Finished
}

/// <summary>
///     The lockpick window's animation, with no drawing and no MonoGame. A turn starts on a key press and turns the
///     cylinder at once. The server's answer arrives within a few frames and sets how far the cylinder may go. If
///     the answer is late, the cylinder waits at <see cref="WAIT_AT_DEGREES" />.
/// </summary>
public sealed class LockpickAnimator
{
    public const float TURN_DEGREES_PER_SECOND = 360f;
    public const float WAIT_AT_DEGREES = 20f;
    public const float FULL_TURN_DEGREES = 90f;
    public const float STRAIN_SECONDS = 0.3f;
    public const float RETURN_SECONDS = 0.2f;
    public const float CLOSE_DELAY_SECONDS = 1f;
    public const float REPLY_TIMEOUT_SECONDS = 2f;
    public const float PICK_STEP_DEGREES = 2f;
    private const float SHAKE_PIXELS = 2f;
    private const float SHAKE_FLIPS_PER_SECOND = 40f;

    private LockpickTurnOutcome? Outcome;
    private float ReturnFrom;
    private float TargetDegrees;
    private bool TurnKeyHeld;
    private float WaitSeconds;

    public LockpickAnimState State { get; private set; } = LockpickAnimState.Idle;
    public float PickDegrees { get; private set; } = 90f;
    public float CylinderDegrees { get; private set; }

    /// <summary>Seconds spent in the current state.</summary>
    public float StateSeconds { get; private set; }

    /// <summary>Set when the last turn got no answer in time. Cleared when the next turn starts.</summary>
    public bool TimedOut { get; private set; }

    /// <summary>Sideways pick offset in pixels while straining, flipping between +2 and -2.</summary>
    public float PickShakeOffset
        => State == LockpickAnimState.Straining
            ? (((int)(StateSeconds * SHAKE_FLIPS_PER_SECOND) % 2) == 0 ? SHAKE_PIXELS : -SHAKE_PIXELS)
            : 0f;

    public void SetPick(float degrees)
    {
        if (State == LockpickAnimState.Idle)
            PickDegrees = Math.Clamp(degrees, 0f, LockpickGeometry.MAX_PICK_DEGREES);
    }

    public void NudgePick(float deltaDegrees) => SetPick(PickDegrees + deltaDegrees);

    /// <summary>
    ///     The turn key or button went down. Returns true when a turn starts, and the caller then sends it to the
    ///     server. Repeats, and presses without a release in between, never start a turn.
    /// </summary>
    public bool PressTurn(bool isRepeat)
    {
        if (isRepeat || TurnKeyHeld)
            return false;

        TurnKeyHeld = true;

        if (State != LockpickAnimState.Idle)
            return false;

        Enter(LockpickAnimState.Turning);
        CylinderDegrees = 0f;
        TargetDegrees = WAIT_AT_DEGREES;
        Outcome = null;
        WaitSeconds = 0f;
        TimedOut = false;

        return true;
    }

    public void ReleaseTurn() => TurnKeyHeld = false;

    /// <summary>The server's answer to the turn in progress. Ignored outside a turn.</summary>
    public void ApplyResult(LockpickTurnOutcome outcome, byte turnPercent)
    {
        if ((State != LockpickAnimState.Turning) || Outcome.HasValue)
            return;

        Outcome = outcome;

        TargetDegrees = outcome == LockpickTurnOutcome.Opened
            ? FULL_TURN_DEGREES
            : Math.Clamp(turnPercent, (byte)0, (byte)100) / 100f * FULL_TURN_DEGREES;

        if (CylinderDegrees > TargetDegrees)
            CylinderDegrees = TargetDegrees;
    }

    public void Update(float seconds)
    {
        StateSeconds += seconds;

        switch (State)
        {
            case LockpickAnimState.Turning:
                CylinderDegrees = Math.Min(TargetDegrees, CylinderDegrees + (TURN_DEGREES_PER_SECOND * seconds));

                if (!Outcome.HasValue)
                {
                    WaitSeconds += seconds;

                    if (WaitSeconds >= REPLY_TIMEOUT_SECONDS)
                    {
                        TimedOut = true;
                        StartReturn();
                    }

                    break;
                }

                if (CylinderDegrees >= TargetDegrees)
                    Enter(Outcome == LockpickTurnOutcome.Opened ? LockpickAnimState.Done : LockpickAnimState.Straining);

                break;

            case LockpickAnimState.Straining:
                if (StateSeconds >= STRAIN_SECONDS)
                {
                    if (Outcome == LockpickTurnOutcome.Broke)
                        Enter(LockpickAnimState.Snapped);
                    else
                        StartReturn();
                }

                break;

            case LockpickAnimState.Returning:
                if (StateSeconds >= RETURN_SECONDS)
                {
                    CylinderDegrees = 0f;
                    Enter(LockpickAnimState.Idle);
                } else
                    CylinderDegrees = ReturnFrom * (1f - (StateSeconds / RETURN_SECONDS));

                break;

            case LockpickAnimState.Done:
            case LockpickAnimState.Snapped:
                if (StateSeconds >= CLOSE_DELAY_SECONDS)
                    Enter(LockpickAnimState.Finished);

                break;
        }
    }

    /// <summary>Back to a fresh lock with the pick straight up, for a new window.</summary>
    public void Reset()
    {
        Enter(LockpickAnimState.Idle);
        PickDegrees = 90f;
        CylinderDegrees = 0f;
        Outcome = null;
        TimedOut = false;
        TurnKeyHeld = false;
    }

    private void Enter(LockpickAnimState state)
    {
        State = state;
        StateSeconds = 0f;
    }

    private void StartReturn()
    {
        ReturnFrom = CylinderDegrees;
        Enter(LockpickAnimState.Returning);
    }
}
