#region
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
#endregion

namespace Chaos.Client.Tests;

public sealed class TcgMatchStaleEndTests
{
    private static readonly DateTime T0 = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);

    private static TcgDisplayArgs Update(string matchId)
        => new()
        {
            Type = TcgDisplayType.Update,
            MatchId = matchId,
            YourSeat = TcgSeat.A,
            ActiveSeat = TcgSeat.A,
            Phase = TcgPhase.Main1,
            Decision = TcgDecision.Main,
            Decider = TcgSeat.A,
            DecisionMs = 30_000,
            TurnMs = 10_000,
            Players = [new TcgPlayerView { Name = "Ana", ReserveMs = 120_000 }, new TcgPlayerView { Name = "Bo" }],
            Events = []
        };

    private static TcgDisplayArgs Close(string matchId)
        => new()
        {
            Type = TcgDisplayType.Close,
            MatchId = matchId,
            HasWinner = true,
            Winner = TcgSeat.B,
            EndReason = TcgEndReason.Concede
        };

    [Test]
    public void A_stale_close_does_not_end_the_showing_match()
    {
        var state = new TcgMatchState();
        state.Apply(Update("match-new"), T0, k => k);

        state.End(Close("match-old"), k => k);

        state.Ended.Should().BeNull();
        state.Latest!.MatchId.Should().Be("match-new");
    }

    [Test]
    public void The_match_still_ends_on_its_own_close_after_a_stale_one()
    {
        var state = new TcgMatchState();
        state.Apply(Update("match-new"), T0, k => k);

        state.End(Close("match-old"), k => k);
        state.End(Close("match-new"), k => k);

        state.Ended.Should().NotBeNull();
        state.Ended!.Outcome.Should().Be(TcgEndOutcome.Defeat);
    }

    [Test]
    public void A_matching_close_ends_the_match()
    {
        var state = new TcgMatchState();
        state.Apply(Update("match-1"), T0, k => k);

        state.End(Close("match-1"), k => k);

        state.Ended.Should().NotBeNull();
        state.Ended!.Outcome.Should().Be(TcgEndOutcome.Defeat);
    }

    [Test]
    public void A_close_with_no_match_active_ends_as_before()
    {
        var state = new TcgMatchState();

        state.End(Close("match-1"), k => k);

        state.Ended.Should().NotBeNull();
    }
}
