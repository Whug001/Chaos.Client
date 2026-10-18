#region
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>
///     Plays each Update's animated events one at a time. While an Update's events play, the board from before it stays
///     on screen; when they finish, that Update's board is shown. The turn banner is not queued: it draws as an overlay
///     once the board carrying it is shown.
/// </summary>
public sealed class TcgMatchEventQueue
{
    public const int MAX_WAITING = 12;
    public const int MAX_QUEUED = 48;

    private readonly List<(TcgDisplayArgs Board, List<TcgEventView> Events)> Entries = [];
    private int EventIndex;
    private double ElapsedMs;

    public TcgDisplayArgs? ShownBoard { get; private set; }
    public TcgDisplayArgs? PlayingBoard => Entries.Count > 0 ? Entries[0].Board : null;

    /// <summary>Events of the playing entry that already finished: the board pass replays their effects from the new board.</summary>
    public IReadOnlyList<TcgEventView> PastEvents
        => Entries.Count == 0 ? [] : Entries[0].Events.Take(EventIndex).ToList();
    public TcgEventView? Current => Entries.Count > 0 ? Entries[0].Events[EventIndex] : null;
    public float Progress => Current is { } e ? (float)Math.Min(1, ElapsedMs / DurationMs(e.Type)) : 0;
    public int Waiting => Entries.Sum(entry => entry.Events.Count) - EventIndex;

    public static int DurationMs(TcgEventType type)
        => type switch
        {
            TcgEventType.TurnStarted       => 1500,
            TcgEventType.CreatureEntered   => 400,
            TcgEventType.CardCast          => 500,
            TcgEventType.TrapRevealed      => 500,
            TcgEventType.Attacked          => 900,
            TcgEventType.Blocked           => 300,
            TcgEventType.Damaged           => 600,
            TcgEventType.PlayerDamaged     => 600,
            TcgEventType.LifeGained        => 600,
            TcgEventType.CreatureDestroyed => 800,
            _                              => 0
        };

    /// <summary>Deaths and the damage that caused them are never skipped to catch up; older entries without them go first.</summary>
    private static bool IsProtectedEvent(TcgEventView e)
        => e.Type is TcgEventType.CreatureDestroyed or TcgEventType.Damaged or TcgEventType.PlayerDamaged;

    public void Enqueue(TcgDisplayArgs board)
    {
        var events = board.Events.Where(e => (e.Type != TcgEventType.TurnStarted) && (DurationMs(e.Type) > 0)).ToList();

        if ((events.Count == 0) && (Entries.Count == 0))
        {
            ShownBoard = board;

            return;
        }

        //an Update with nothing to animate still waits behind the entries ahead of it; SkipEmpty settles it at the front
        Entries.Add((board, events));

        while ((Waiting > MAX_WAITING) && (Entries.Count > 1))
            if (!DropOldestSkippable())
                break;

        while (((Waiting > MAX_QUEUED) || (Entries.Count > MAX_QUEUED)) && (Entries.Count > 1))
            Finish();

        SkipEmpty();
    }

    public void Advance(double elapsedMs)
    {
        ElapsedMs += elapsedMs;

        while (Current is { } e && (ElapsedMs >= DurationMs(e.Type)))
        {
            ElapsedMs -= DurationMs(e.Type);
            EventIndex++;

            if (EventIndex >= Entries[0].Events.Count)
                Finish();
        }

        if (Current is null)
            ElapsedMs = 0;
    }

    public void Clear(TcgDisplayArgs? newest)
    {
        Entries.Clear();
        EventIndex = 0;
        ElapsedMs = 0;
        ShownBoard = newest;
    }

    private void Finish()
    {
        ShownBoard = Entries[0].Board;
        Entries.RemoveAt(0);
        EventIndex = 0;
        ElapsedMs = 0;
        SkipEmpty();
    }

    private bool DropOldestSkippable()
    {
        for (var i = 0; i < Entries.Count - 1; i++)
        {
            var unplayed = i == 0 ? Entries[i].Events.Skip(EventIndex) : Entries[i].Events;

            if (unplayed.Any(IsProtectedEvent))
                continue;

            if (i == 0)
                Finish();
            else
                Entries.RemoveAt(i);

            return true;
        }

        return false;
    }

    private void SkipEmpty()
    {
        while ((Entries.Count > 0) && (Entries[0].Events.Count == 0))
        {
            ShownBoard = Entries[0].Board;
            Entries.RemoveAt(0);
        }
    }
}
