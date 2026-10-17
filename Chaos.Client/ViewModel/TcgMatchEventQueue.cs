#region
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>
///     Plays each Update's animated events one at a time. While an Update's events play, the board from before it stays
///     on screen; when they finish, that Update's board is shown.
/// </summary>
public sealed class TcgMatchEventQueue
{
    public const int MAX_WAITING = 12;

    private readonly Queue<(TcgDisplayArgs Board, List<TcgEventView> Events)> Entries = new();
    private int EventIndex;
    private double ElapsedMs;

    public TcgDisplayArgs? ShownBoard { get; private set; }
    public TcgDisplayArgs? PlayingBoard => Entries.Count > 0 ? Entries.Peek().Board : null;

    /// <summary>Events of the playing entry that already finished: the board pass replays their effects from the new board.</summary>
    public IReadOnlyList<TcgEventView> PastEvents
        => Entries.Count == 0 ? [] : Entries.Peek().Events.Take(EventIndex).ToList();
    public TcgEventView? Current => Entries.Count > 0 ? Entries.Peek().Events[EventIndex] : null;
    public float Progress => Current is { } e ? (float)Math.Min(1, ElapsedMs / DurationMs(e.Type)) : 0;
    public int Waiting => Entries.Sum(entry => entry.Events.Count) - EventIndex;

    public static int DurationMs(TcgEventType type)
        => type switch
        {
            TcgEventType.CreatureEntered   => 400,
            TcgEventType.CardCast          => 500,
            TcgEventType.TrapRevealed      => 500,
            TcgEventType.Attacked          => 900,
            TcgEventType.Blocked           => 300,
            TcgEventType.Damaged           => 600,
            TcgEventType.PlayerDamaged     => 600,
            TcgEventType.LifeGained        => 600,
            TcgEventType.CreatureDestroyed => 500,
            _                              => 0
        };

    public void Enqueue(TcgDisplayArgs board)
    {
        var events = board.Events.Where(e => DurationMs(e.Type) > 0).ToList();

        if ((events.Count == 0) && (Entries.Count == 0))
        {
            ShownBoard = board;

            return;
        }

        //an Update with nothing to animate still waits behind the entries ahead of it; SkipEmpty settles it at the front
        Entries.Enqueue((board, events));

        while ((Waiting > MAX_WAITING) && (Entries.Count > 1))
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

            if (EventIndex >= Entries.Peek().Events.Count)
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
        ShownBoard = Entries.Dequeue().Board;
        EventIndex = 0;
        ElapsedMs = 0;
        SkipEmpty();
    }

    private void SkipEmpty()
    {
        while ((Entries.Count > 0) && (Entries.Peek().Events.Count == 0))
            ShownBoard = Entries.Dequeue().Board;
    }
}
