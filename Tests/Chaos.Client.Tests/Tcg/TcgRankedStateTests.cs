#region
using Chaos.Client.Collections;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
#endregion

namespace Chaos.Client.Tests;

public sealed class TcgRankedStateTests
{
    private static readonly DateTime Now = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);

    private static TcgRankedDisplayArgs Args(
        TcgRankedDisplayKind kind,
        uint waited = 0,
        byte left = 0,
        string message = "",
        ushort count = 0)
        => new()
        {
            Kind = kind,
            SecondsWaited = waited,
            SecondsLeft = left,
            Message = message,
            QueueSize = count
        };

    [Test]
    public void Queue_starts_searching_and_returns_the_message()
    {
        var state = new TcgRankedState();

        state.Apply(Args(TcgRankedDisplayKind.Queue, message: "Queued."), Now).Should().Be("Queued.");
        state.Mode.Should().Be(TcgQueueMode.Searching);
        state.ShowLeave.Should().BeTrue();
        state.ShowAcceptDecline.Should().BeFalse();
    }

    [Test]
    public void Queue_shows_how_many_players_are_waiting()
    {
        var state = new TcgRankedState();
        state.Apply(Args(TcgRankedDisplayKind.Queue, waited: 10, count: 3), Now);

        state.SecondsWaited(Now).Should().Be(10);
        state.QueueSize.Should().Be(3);
        state.QueueText(Now).Should().Be("Searching 0:10 (3)");
        state.QueueText(Now.AddSeconds(73)).Should().Be("Searching 1:23 (3)");
        state.QueueText(Now.AddMinutes(2)).Should().Be("Searching 2:10 (3)");
    }

    [Test]
    [Arguments(TcgRankedDisplayKind.QueueEnded)]
    [Arguments(TcgRankedDisplayKind.Result)]
    public void QueueEnded_and_Result_hide(TcgRankedDisplayKind kind)
    {
        var state = new TcgRankedState();
        state.Apply(Args(TcgRankedDisplayKind.Queue), Now);

        state.Apply(Args(kind), Now).Should().BeNull();
        state.Mode.Should().Be(TcgQueueMode.Hidden);
        state.ShowLeave.Should().BeFalse();
        state.ShowAcceptDecline.Should().BeFalse();
    }

    [Test]
    public void ReadyCheck_counts_down_to_the_end()
    {
        var state = new TcgRankedState();

        state.Apply(Args(TcgRankedDisplayKind.ReadyCheck, left: 15), Now).Should().BeNull();
        state.Mode.Should().Be(TcgQueueMode.ReadyCheck);
        state.SecondsLeft(Now.AddSeconds(5)).Should().Be(10);
        state.SecondsLeft(Now.AddMinutes(1)).Should().Be(0);
        state.ShowAcceptDecline.Should().BeTrue();
        state.ShowLeave.Should().BeFalse();
    }

    [Test]
    public void Info_is_stored_without_changing_the_mode()
    {
        var state = new TcgRankedState();
        var info = Args(TcgRankedDisplayKind.Info);
        info.Step = 7;
        info.Points = 40;

        state.Apply(info, Now).Should().BeNull();
        state.Info.Should().BeSameAs(info);
        state.Mode.Should().Be(TcgQueueMode.Hidden);
    }

    [Test]
    public void MarkAccepted_only_leaves_the_ready_check()
    {
        var accepted = new TcgRankedState();
        accepted.Apply(Args(TcgRankedDisplayKind.ReadyCheck, left: 15), Now);
        accepted.MarkAccepted();
        accepted.Mode.Should().Be(TcgQueueMode.Accepted);
        accepted.ShowAcceptDecline.Should().BeFalse();

        var searching = new TcgRankedState();
        searching.Apply(Args(TcgRankedDisplayKind.Queue), Now);
        searching.MarkAccepted();
        searching.Mode.Should().Be(TcgQueueMode.Searching);

        var hidden = new TcgRankedState();
        hidden.MarkAccepted();
        hidden.Mode.Should().Be(TcgQueueMode.Hidden);
    }

    [Test]
    public void Only_a_ready_deck_with_nothing_showing_can_find_a_match()
    {
        var state = new TcgRankedState();
        state.CanFindMatch(TcgDeckStatus.Ready).Should().BeTrue();
        state.CanFindMatch(TcgDeckStatus.Missing).Should().BeFalse();
        state.CanFindMatch(TcgDeckStatus.Incomplete).Should().BeFalse();

        state.Apply(Args(TcgRankedDisplayKind.Queue), Now);
        state.CanFindMatch(TcgDeckStatus.Ready).Should().BeFalse();
    }

    [Test]
    [Arguments(0, "Copper 3")]
    [Arguments(2, "Copper 1")]
    [Arguments(3, "Bronze 3")]
    [Arguments(7, "Silver 2")]
    [Arguments(14, "Mythril 1")]
    public void StepName_names_the_step(int step, string expected) => TcgRankedState.StepName(step).Should().Be(expected);

    [Test]
    [Arguments(7, 40, 0, "40/100")]
    [Arguments(13, 85, 0, "85/100")]
    [Arguments(14, 99, 0, "99/100")]
    [Arguments(14, 100, 1, "#1")]
    [Arguments(14, 100, 3, "#3")]
    [Arguments(14, 100, 10, "#10")]
    [Arguments(14, 100, 12, "Leaderboard #12")]
    public void PointsText_names_points_or_the_leaderboard_spot(int step, int points, int position, string expected)
        => TcgRankedState.PointsText(step, points, position).Should().Be(expected);

    [Test]
    public void Builds_ranked_actions_with_types_15_to_19()
    {
        var join = TcgRankedState.JoinAction("Main");
        join.Should().Match<TcgActionArgs>(a => a.Type == TcgActionType.QueueJoin && a.DeckName == "Main");
        ((byte)join.Type).Should().Be(15);
        ((byte)TcgRankedState.LeaveAction().Type).Should().Be(16);
        ((byte)TcgRankedState.AcceptAction().Type).Should().Be(17);
        ((byte)TcgRankedState.DeclineAction().Type).Should().Be(18);
        ((byte)TcgRankedState.InfoAction().Type).Should().Be(19);
    }

    [Test]
    public void Clear_hides_and_drops_the_snapshot()
    {
        var state = new TcgRankedState();
        state.Apply(Args(TcgRankedDisplayKind.Queue), Now);
        state.Apply(Args(TcgRankedDisplayKind.Info), Now);

        state.Clear();

        state.Mode.Should().Be(TcgQueueMode.Hidden);
        state.Info.Should().BeNull();
        state.SecondsWaited(Now).Should().Be(0);
        state.SecondsLeft(Now).Should().Be(0);
    }

    [Test]
    public void Clear_resets_the_queue_count()
    {
        var state = new TcgRankedState();
        state.Apply(Args(TcgRankedDisplayKind.Queue, count: 3), Now);
        state.QueueSize.Should().Be(3);

        state.Clear();

        state.QueueSize.Should().Be(0);
    }

    [Test]
    public void WorldState_Clear_keeps_the_queue_across_maps()
    {
        WorldState.TcgRanked.Should().NotBeNull();
        WorldState.TcgRanked.Apply(Args(TcgRankedDisplayKind.Queue), Now);
        WorldState.TcgRanked.Apply(Args(TcgRankedDisplayKind.Info), Now);

        WorldState.Clear();

        WorldState.TcgRanked.Mode.Should().Be(TcgQueueMode.Searching);
        WorldState.TcgRanked.Info.Should().NotBeNull();

        WorldState.TcgRanked.Clear();
    }

    [Test]
    public void WorldState_ResetAll_clears_ranked_on_logout()
    {
        WorldState.TcgRanked.Apply(Args(TcgRankedDisplayKind.Queue), Now);
        WorldState.TcgRanked.Apply(Args(TcgRankedDisplayKind.Info), Now);

        WorldState.ResetAll();

        WorldState.TcgRanked.Mode.Should().Be(TcgQueueMode.Hidden);
        WorldState.TcgRanked.Info.Should().BeNull();
    }
}
