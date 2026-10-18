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
    public void Every_animation_except_the_turn_banner_is_shorter_than_a_second()
    {
        foreach (var type in Enum.GetValues<TcgEventType>())
        {
            if (type == TcgEventType.TurnStarted)
                continue;

            TcgMatchEventQueue.DurationMs(type).Should().BeLessThan(1000);
        }
    }

    [Test]
    public void Turn_banner_holds_the_board()
    {
        TcgMatchEventQueue.DurationMs(TcgEventType.TurnStarted).Should().Be(1500);
    }

    [Test]
    public void Destroyed_creatures_stay_visible()
    {
        TcgMatchEventQueue.DurationMs(TcgEventType.CreatureDestroyed).Should().Be(800);
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
        queue.Enqueue(Board(TcgEventType.PhaseChanged, TcgEventType.ManaPlaced));

        queue.Current.Should().BeNull();
    }

    [Test]
    public void Turn_started_plays_before_later_updates()
    {
        var queue = new TcgMatchEventQueue();
        queue.Enqueue(Board(TcgEventType.TurnStarted));
        queue.Enqueue(Board(TcgEventType.CardCast));

        queue.Current.Should().NotBeNull();
        queue.Current!.Type.Should().Be(TcgEventType.TurnStarted);

        queue.Advance(1500);

        queue.Current.Should().NotBeNull();
        queue.Current!.Type.Should().Be(TcgEventType.CardCast);
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

        queue.Advance(800);
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
        var boards = Enumerable.Range(0, 6).Select(_ => Board(TcgEventType.Attacked, TcgEventType.Attacked, TcgEventType.Attacked)).ToList();

        foreach (var board in boards)
            queue.Enqueue(board);

        queue.Waiting.Should().BeLessThanOrEqualTo(TcgMatchEventQueue.MAX_WAITING);
        queue.ShownBoard.Should().BeSameAs(boards[1]);
    }

    [Test]
    public void Overflow_keeps_death_and_damage_events()
    {
        var queue = new TcgMatchEventQueue();
        queue.Enqueue(Board(TcgEventType.Blocked, TcgEventType.Damaged, TcgEventType.CreatureDestroyed));

        foreach (var _ in Enumerable.Range(0, 5))
            queue.Enqueue(Board(TcgEventType.Attacked, TcgEventType.Attacked, TcgEventType.Attacked));

        queue.Waiting.Should().BeLessThanOrEqualTo(TcgMatchEventQueue.MAX_WAITING);

        var played = Drain(queue);
        played.Should().Contain(TcgEventType.CreatureDestroyed);
        played.Should().Contain(TcgEventType.Damaged);
    }

    [Test]
    public void Queue_stays_bounded_when_everything_is_combat()
    {
        var queue = new TcgMatchEventQueue();

        foreach (var _ in Enumerable.Range(0, 20))
            queue.Enqueue(Board(TcgEventType.Damaged, TcgEventType.Damaged, TcgEventType.Damaged));

        queue.Waiting.Should().BeLessThanOrEqualTo(TcgMatchEventQueue.MAX_QUEUED);
    }

    private static List<TcgEventType> Drain(TcgMatchEventQueue queue)
    {
        var played = new List<TcgEventType>();

        for (var i = 0; (i < 400) && (queue.Current is { } current); i++)
        {
            played.Add(current.Type);
            queue.Advance(TcgMatchEventQueue.DurationMs(current.Type));
        }

        return played;
    }

    [Test]
    public void Combat_deaths_play_before_the_next_turn_takes_over()
    {
        var queue = new TcgMatchEventQueue();
        queue.Enqueue(Board(TcgEventType.Blocked, TcgEventType.Damaged, TcgEventType.CreatureDestroyed));

        foreach (var _ in Enumerable.Range(0, 6))
            queue.Enqueue(Board(TcgEventType.Damaged, TcgEventType.Damaged, TcgEventType.Damaged));

        Drain(queue).Should().Contain(TcgEventType.CreatureDestroyed);
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
