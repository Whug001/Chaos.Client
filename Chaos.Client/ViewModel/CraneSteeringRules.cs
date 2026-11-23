#region
using Chaos.Geometry.Abstractions.Definitions;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>A keycap on the crane controls panel.</summary>
public enum CraneKey : byte
{
    Up,
    Left,
    Down,
    Right,
    Space
}

/// <summary>The kinds of player input the crane steering rules decide on.</summary>
public enum CraneInput : byte
{
    /// <summary>An arrow key (a bound move action).</summary>
    Arrow,

    /// <summary>The assail key (Space by default).</summary>
    Space,

    /// <summary>Right-click pathing, shift+right-click turning, double-right-click follow-and-assail.</summary>
    PointerMove,

    /// <summary>A queued walk or a pathfinding step run by the update loop.</summary>
    QueuedOrPathStep
}

/// <summary>What the client does with an input.</summary>
public enum CraneInputOutcome : byte
{
    /// <summary>The usual handling, local prediction included.</summary>
    Normal,

    /// <summary>
    ///     Sent to the server only: no local step, no local facing change, no pathfinding, no queued walk, no local
    ///     animation. The server consumes it as claw steering or the drop.
    /// </summary>
    SendOnly,

    /// <summary>Dropped: neither sent nor predicted.</summary>
    Suppress
}

/// <summary>
///     How input behaves while the player steers the crane claw. The server consumes the operator's walks and turns as
///     steering and the spacebar as the drop, and answers with the operator's true position and facing; any local
///     prediction would show the character step or turn and snap back. So while steering, the arrows and Space go to
///     the server and nothing else, and pointer movement and queued steps do not run at all.
/// </summary>
public static class CraneSteeringRules
{
    /// <summary>
    ///     The least time between two steering arrows sent while a key is held (OS key repeat is far faster). Below the
    ///     claw's own pace, so the server's claw throttle stays the limit and a held key keeps the claw moving.
    /// </summary>
    public const long STEER_SEND_INTERVAL_MS = 100;

    /// <summary>Whether a steering arrow pressed at <paramref name="nowMs" /> goes to the server.</summary>
    public static bool ShouldSendSteer(long nowMs, long lastSentMs) => (lastSentMs == 0) || ((nowMs - lastSentMs) >= STEER_SEND_INTERVAL_MS);

    public static CraneInputOutcome Decide(bool steering, CraneInput input)
    {
        if (!steering)
            return CraneInputOutcome.Normal;

        return input switch
        {
            CraneInput.Arrow or CraneInput.Space => CraneInputOutcome.SendOnly,
            _                                    => CraneInputOutcome.Suppress
        };
    }

    /// <summary>The keycap that lights for an arrow press in <paramref name="direction" />.</summary>
    public static CraneKey KeyFor(Direction direction)
        => direction switch
        {
            Direction.Up    => CraneKey.Up,
            Direction.Right => CraneKey.Right,
            Direction.Down  => CraneKey.Down,
            _               => CraneKey.Left
        };
}
