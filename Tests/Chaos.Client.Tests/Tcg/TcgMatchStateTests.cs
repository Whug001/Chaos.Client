#region
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
#endregion

namespace Chaos.Client.Tests;

public sealed class TcgMatchStateTests
{
    private static readonly DateTime T0 = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);

    private static TcgDisplayArgs Update(params TcgEventView[] events)
        => new()
        {
            Type = TcgDisplayType.Update,
            YourSeat = TcgSeat.A,
            ActiveSeat = TcgSeat.A,
            Phase = TcgPhase.Main1,
            Decision = TcgDecision.Main,
            Decider = TcgSeat.A,
            DecisionMs = 30_000,
            TurnMs = 10_000,
            Players = [new TcgPlayerView { Name = "Ana", ReserveMs = 120_000 }, new TcgPlayerView { Name = "Bo" }],
            Events = events.ToList()
        };

    private static TcgEventView Event(TcgEventType type, TcgSeat seat) => new(type, seat, 0, 0, 0, "");

    [Test]
    public void Keeps_the_newest_update()
    {
        var state = new TcgMatchState();
        var second = Update();

        state.Apply(Update(), T0, k => k);
        state.Apply(second, T0.AddSeconds(1), k => k);

        state.Latest.Should().BeSameAs(second);
        state.OpponentName.Should().Be("Bo");
    }

    [Test]
    public void Counts_timers_down_from_the_receive_time()
    {
        var state = new TcgMatchState();
        state.Apply(Update(), T0, k => k);

        state.DecisionSecondsLeft(T0.AddSeconds(5)).Should().Be(25);
        state.TurnSecondsLeft(T0.AddSeconds(4)).Should().Be(6);
        state.ReserveSecondsLeft(T0.AddSeconds(4)).Should().Be(120);
        state.ReserveSecondsLeft(T0.AddSeconds(15)).Should().Be(115);
        state.DecisionSecondsLeft(T0.AddMinutes(5)).Should().Be(0);
    }

    [Test]
    public void Tracks_the_once_per_turn_mana_placement()
    {
        var state = new TcgMatchState();

        state.Apply(Update(Event(TcgEventType.ManaPlaced, TcgSeat.A)), T0, k => k);
        state.ManaPlacedThisTurn.Should().BeTrue();

        state.Apply(Update(Event(TcgEventType.TurnStarted, TcgSeat.B)), T0, k => k);
        state.ManaPlacedThisTurn.Should().BeTrue();

        state.Apply(Update(Event(TcgEventType.TurnStarted, TcgSeat.A)), T0, k => k);
        state.ManaPlacedThisTurn.Should().BeFalse();
    }

    [Test]
    public void A_mana_refusal_also_marks_mana_placed_and_logs_the_reason()
    {
        var state = new TcgMatchState();
        var refused = Update();
        refused.Rejected = TcgRejectReason.AlreadyPlacedMana;

        state.Apply(refused, T0, k => k);

        state.ManaPlacedThisTurn.Should().BeTrue();
        state.Log.Last().Should().Be(TcgMatchText.RejectText(TcgRejectReason.AlreadyPlacedMana));
    }

    [Test]
    public void Caps_the_log_at_one_hundred_lines()
    {
        var state = new TcgMatchState();

        for (var i = 0; i < 120; i++)
            state.Apply(Update(new TcgEventView(TcgEventType.ManaPlaced, TcgSeat.B, 0, 0, 0, "")), T0, k => k);

        state.Log.Should().HaveCount(TcgMatchState.MAX_LOG);
    }

    [Test]
    public void Redraw_is_done_once_sent_or_once_your_redrew_event_arrives()
    {
        var state = new TcgMatchState();
        var redraw = Update();
        redraw.Phase = TcgPhase.Redraw;
        redraw.Decision = TcgDecision.Redraw;

        state.Apply(redraw, T0, k => k);
        state.NeedsRedraw.Should().BeTrue();

        state.MarkRedrawSent();
        state.NeedsRedraw.Should().BeFalse();
    }

    [Test]
    public void The_turn_clock_only_runs_on_a_main_decision()
    {
        var state = new TcgMatchState();
        state.Apply(Update(), T0, k => k);

        state.TurnSecondsLeft(T0.AddSeconds(4)).Should().Be(6);

        var respond = Update();
        respond.Decision = TcgDecision.Respond;
        state.Apply(respond, T0.AddSeconds(4), k => k);

        state.TurnSecondsLeft(T0.AddMinutes(5)).Should().Be(10);
    }

    [Test]
    public void A_refused_redraw_brings_keep_back()
    {
        var state = new TcgMatchState();
        var redraw = Update();
        redraw.Phase = TcgPhase.Redraw;
        redraw.Decision = TcgDecision.Redraw;

        state.Apply(redraw, T0, k => k);
        state.MarkRedrawSent();
        state.NeedsRedraw.Should().BeFalse();

        redraw.Rejected = TcgRejectReason.NotInHand;
        state.Apply(redraw, T0.AddSeconds(1), k => k);

        state.NeedsRedraw.Should().BeTrue();
    }

    [Test]
    public void Reads_the_hint_from_the_update()
    {
        var state = new TcgMatchState();
        var update = Update();
        update.Hint = "Attack with your owl.";

        state.Apply(update, T0, k => k);

        state.Hint.Should().Be("Attack with your owl.");
    }

    [Test]
    public void An_update_without_a_hint_leaves_hint_empty()
    {
        var state = new TcgMatchState();

        state.Apply(Update(), T0, k => k);

        state.Hint.Should().BeEmpty();
    }

    [Test]
    public void A_later_update_without_a_hint_clears_it()
    {
        var state = new TcgMatchState();
        var hinted = Update();
        hinted.Hint = "Block with your wall.";
        state.Apply(hinted, T0, k => k);

        state.Apply(Update(), T0.AddSeconds(1), k => k);

        state.Hint.Should().BeEmpty();
    }

    [Test]
    public void Ending_records_the_outcome_and_stakes()
    {
        var state = new TcgMatchState();
        var update = Update();
        update.Stakes = TcgStakes.Gold;
        update.StakeGold = 1000;
        state.Apply(update, T0, k => k);

        state.End(new TcgDisplayArgs { Type = TcgDisplayType.Close, HasWinner = true, Winner = TcgSeat.B, EndReason = TcgEndReason.Concede }, k => k);

        state.Ended!.Outcome.Should().Be(TcgEndOutcome.Defeat);
        state.Ended.StakesText.Should().Be("You lost 1,000 gold.");
        state.Ended.ReasonText.Should().Be("A player conceded.");
    }
}
