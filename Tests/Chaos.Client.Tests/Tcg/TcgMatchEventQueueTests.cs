#region
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
#endregion

namespace Chaos.Client.Tests;

public sealed class TcgMatchEventQueueTests
{
    private static TcgDisplayArgs Board(params TcgEventType[] types)
        => new() { Type = TcgDisplayType.Update, Events = types.Select(t => new TcgEventView(t, TcgSeat.A, 1, 2, 3, "")).ToList() };

    [Test]
    public void Every_animation_is_shorter_than_a_second()
    {
        foreach (var type in Enum.GetValues<TcgEventType>())
            TcgMatchEventQueue.DurationMs(type).Should().BeLessThan(1000);
    }

    [Test]
    public void Plays_events_in_order_across_updates()
    {
        var queue = new TcgMatchEventQueue();
        queue.Enqueue(Board(TcgEventType.CardCast, TcgEventType.CreatureEntered));
        queue.Enqueue(Board(TcgEventType.Attacked));

        var seen = new List<TcgEventType>();

        for (var i = 0; (i < 100) && queue.Current is { } current; i++)
        {
            if ((seen.Count == 0) || (seen[^1] != current.Type) || (queue.Progress == 0))
                seen.Add(current.Type);

            queue.Advance(100);
        }

        seen.Distinct().Should().Equal(TcgEventType.CardCast, TcgEventType.CreatureEntered, TcgEventType.Attacked);
    }

    [Test]
    public void Skips_bookkeeping_events()
    {
        var queue = new TcgMatchEventQueue();
        queue.Enqueue(Board(TcgEventType.TurnStarted, TcgEventType.ManaPlaced));

        queue.Current.Should().BeNull();
    }

    [Test]
    public void Shows_the_old_board_while_an_update_plays_and_the_new_one_after()
    {
        var first = Board();
        var second = Board(TcgEventType.CreatureDestroyed);
        var queue = new TcgMatchEventQueue();

        queue.Enqueue(first);
        queue.ShownBoard.Should().BeSameAs(first);

        queue.Enqueue(second);
        queue.ShownBoard.Should().BeSameAs(first);
        queue.PlayingBoard.Should().BeSameAs(second);

        queue.Advance(500);
        queue.ShownBoard.Should().BeSameAs(second);
        queue.Current.Should().BeNull();
    }

    [Test]
    public void Reports_progress_within_the_current_event()
    {
        var queue = new TcgMatchEventQueue();
        queue.Enqueue(Board(TcgEventType.Attacked));

        queue.Advance(450);

        queue.Progress.Should().BeApproximately(0.5f, 0.01f);
    }

    [Test]
    public void Catches_up_when_too_many_events_wait()
    {
        var queue = new TcgMatchEventQueue();
        var boards = Enumerable.Range(0, 6).Select(_ => Board(TcgEventType.Damaged, TcgEventType.Damaged, TcgEventType.Damaged)).ToList();

        foreach (var board in boards)
            queue.Enqueue(board);

        queue.Waiting.Should().BeLessThanOrEqualTo(TcgMatchEventQueue.MAX_WAITING);
        queue.ShownBoard.Should().BeSameAs(boards[1]);
    }

    [Test]
    public void Clear_shows_the_newest_board_at_once()
    {
        var queue = new TcgMatchEventQueue();
        var newest = Board(TcgEventType.Attacked);
        queue.Enqueue(Board());
        queue.Enqueue(newest);

        queue.Clear(newest);

        queue.Current.Should().BeNull();
        queue.ShownBoard.Should().BeSameAs(newest);
    }
}
