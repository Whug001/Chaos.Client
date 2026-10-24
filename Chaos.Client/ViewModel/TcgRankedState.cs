#region
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>What the ranked queue box shows.</summary>
public enum TcgQueueMode
{
    Hidden,
    Searching,
    ReadyCheck,
    Accepted
}

/// <summary>
///     The ranked queue's mode plus the last ranked info snapshot. Updated from <c>TcgRankedDisplay</c> packets,
///     read by the queue bar and the binder Ranked tab.
/// </summary>
public sealed class TcgRankedState
{
    public const int TOP_STEP = 14;
    public const int STEP_POINTS = 100;
    public const int LEADERBOARD_SIZE = 10;

    private static readonly string[] TierNames = ["Copper", "Bronze", "Silver", "Gold", "Mythril"];

    private DateTime SearchStart = DateTime.MaxValue;
    private DateTime ReadyEnd = DateTime.MinValue;

    public TcgQueueMode Mode { get; private set; } = TcgQueueMode.Hidden;

    /// <summary>How many players are waiting in the ranked queue, including the viewer.</summary>
    public ushort QueueSize { get; private set; }

    /// <summary>The last ranked info snapshot, or null before the first one arrives.</summary>
    public TcgRankedDisplayArgs? Info { get; private set; }

    public bool ShowLeave => Mode == TcgQueueMode.Searching;
    public bool ShowAcceptDecline => Mode == TcgQueueMode.ReadyCheck;

    /// <summary>Applies a ranked display packet. Returns its message when it has one, else null.</summary>
    public string? Apply(TcgRankedDisplayArgs args, DateTime now)
    {
        switch (args.Kind)
        {
            case TcgRankedDisplayKind.Queue:
                Mode = TcgQueueMode.Searching;
                SearchStart = now.AddSeconds(-args.SecondsWaited);
                QueueSize = args.QueueSize;

                break;
            case TcgRankedDisplayKind.QueueEnded:
            case TcgRankedDisplayKind.Result:
                Mode = TcgQueueMode.Hidden;

                break;
            case TcgRankedDisplayKind.ReadyCheck:
                Mode = TcgQueueMode.ReadyCheck;
                ReadyEnd = now.AddSeconds(args.SecondsLeft);

                break;
            case TcgRankedDisplayKind.Info:
                Info = args;

                break;
        }

        return args.Message.Length > 0 ? args.Message : null;
    }

    /// <summary>Moves a ready check to accepted. Any other mode stays.</summary>
    public void MarkAccepted()
    {
        if (Mode == TcgQueueMode.ReadyCheck)
            Mode = TcgQueueMode.Accepted;
    }

    /// <summary>Whole seconds spent searching, or 0.</summary>
    public int SecondsWaited(DateTime now) => (int)Math.Max(0, (now - SearchStart).TotalSeconds);

    /// <summary>Whole seconds left to answer the ready check, or 0.</summary>
    public int SecondsLeft(DateTime now) => (int)Math.Max(0, Math.Ceiling((ReadyEnd - now).TotalSeconds));

    public string QueueText(DateTime now)
    {
        var waited = SecondsWaited(now);

        return $"Searching {waited / 60}:{waited % 60:00} ({QueueSize})";
    }

    /// <summary>Only a ready deck with nothing already showing can join the queue.</summary>
    public bool CanFindMatch(TcgDeckStatus status) => (Mode == TcgQueueMode.Hidden) && (status == TcgDeckStatus.Ready);

    /// <summary>Matches the server's ladder names: Copper 3 up to Mythril 1.</summary>
    public static string StepName(int step)
    {
        var clamped = Math.Clamp(step, 0, TOP_STEP);

        return $"{TierNames[clamped / 3]} {3 - clamped % 3}";
    }

    public static string PointsText(int step, int points, int position)
        => IsLeaderboard(step, points)
            ? position is >= 1 and <= LEADERBOARD_SIZE
                ? $"#{position}"
                : $"Leaderboard #{position}"
            : $"{points}/{STEP_POINTS}";

    private static bool IsLeaderboard(int step, int points) => (step >= TOP_STEP) && (points >= STEP_POINTS);

    public static TcgActionArgs JoinAction(string deck) => new() { Type = TcgActionType.QueueJoin, DeckName = deck };

    public static TcgActionArgs LeaveAction() => new() { Type = TcgActionType.QueueLeave };

    public static TcgActionArgs AcceptAction() => new() { Type = TcgActionType.QueueAccept };

    public static TcgActionArgs DeclineAction() => new() { Type = TcgActionType.QueueDecline };

    public static TcgActionArgs InfoAction() => new() { Type = TcgActionType.RankedInfo };

    public void Clear()
    {
        Mode = TcgQueueMode.Hidden;
        Info = null;
        SearchStart = DateTime.MaxValue;
        QueueSize = 0;
        ReadyEnd = DateTime.MinValue;
    }
}
