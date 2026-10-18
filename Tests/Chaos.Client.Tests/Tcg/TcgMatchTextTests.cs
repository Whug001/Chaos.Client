#region
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
#endregion

namespace Chaos.Client.Tests;

public sealed class TcgMatchTextTests
{
    private static string Name(string key) => key == "bolt" ? "Bolt" : key;

    [Test]
    [Arguments(TcgPhase.Main1, TcgSeat.A, "Main phase 1, your turn")]
    [Arguments(TcgPhase.Combat, TcgSeat.B, "Combat, Bo's turn")]
    [Arguments(TcgPhase.Main2, TcgSeat.A, "Main phase 2, your turn")]
    [Arguments(TcgPhase.Redraw, TcgSeat.A, "Redraw")]
    public void Builds_the_phase_line(TcgPhase phase, TcgSeat active, string expected)
        => TcgMatchText.PhaseLine(phase, active, TcgSeat.A, "Bo").Should().Be(expected);

    [Test]
    public void Logs_a_cast_with_the_card_name()
        => TcgMatchText.LogLine(new TcgEventView(TcgEventType.CardCast, TcgSeat.B, 4, 0, 0, "bolt"), TcgSeat.A, ["Ana", "Bo"], Name)
                       .Should().Be("Bo cast Bolt.");

    [Test]
    public void Logs_player_damage_against_you()
        => TcgMatchText.LogLine(new TcgEventView(TcgEventType.PlayerDamaged, TcgSeat.A, 4, -1, 3, ""), TcgSeat.A, ["Ana", "Bo"], Name)
                       .Should().Be("You take 3 damage.");

    [Test]
    [Arguments(TcgEventType.TurnStarted)]
    [Arguments(TcgEventType.PhaseChanged)]
    [Arguments(TcgEventType.WindowOpened)]
    public void Skips_bookkeeping_events(TcgEventType type)
        => TcgMatchText.LogLine(new TcgEventView(type, TcgSeat.A, 0, 0, 1, ""), TcgSeat.A, ["Ana", "Bo"], Name).Should().BeNull();

    [Test]
    public void Has_a_sentence_for_every_reject_reason()
    {
        foreach (var reason in Enum.GetValues<TcgRejectReason>().Where(r => r != TcgRejectReason.None))
            TcgMatchText.RejectText(reason).Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public void Describes_gold_stakes_for_the_winner_and_the_loser()
    {
        TcgMatchText.StakesResult(TcgEndOutcome.Victory, TcgStakes.Gold, 2000, [], TcgSeat.A, Name).Should().Be("You won 4,000 gold.");
        TcgMatchText.StakesResult(TcgEndOutcome.Defeat, TcgStakes.Gold, 2000, [], TcgSeat.A, Name).Should().Be("You lost 2,000 gold.");
        TcgMatchText.StakesResult(TcgEndOutcome.Draw, TcgStakes.Gold, 2000, [], TcgSeat.A, Name).Should().Be("Both stakes were returned.");
    }

    [Test]
    public void Describes_ante_cards_moving()
    {
        List<TcgAnteView> ante = [new(TcgSeat.A, "bear", false), new(TcgSeat.B, "bolt", true)];

        TcgMatchText.StakesResult(TcgEndOutcome.Victory, TcgStakes.Ante, 0, ante, TcgSeat.A, Name).Should().Be("You won Bolt (foil) and kept bear.");
        TcgMatchText.StakesResult(TcgEndOutcome.Defeat, TcgStakes.Ante, 0, ante, TcgSeat.A, Name).Should().Be("You lost bear.");
    }

    [Test]
    [Arguments(TcgEndReason.Life, "Life reached zero.")]
    [Arguments(TcgEndReason.EmptyDeck, "A deck ran out.")]
    [Arguments(TcgEndReason.Concede, "A player conceded.")]
    [Arguments(TcgEndReason.Disconnect, "A player stayed disconnected.")]
    [Arguments(TcgEndReason.Draw, "Both players fell together.")]
    public void Explains_why_the_match_ended(TcgEndReason reason, string expected)
        => TcgMatchText.EndReasonText(reason).Should().Be(expected);

    [Test]
    public void Names_both_seats_for_a_watcher_phase_line()
    {
        TcgMatchText.PhaseLine(TcgPhase.Main1, TcgSeat.A, null, "Bo", "Ana").Should().Be("Main phase 1, Ana's turn");
        TcgMatchText.PhaseLine(TcgPhase.Main1, TcgSeat.B, null, "Bo", "Bo").Should().Be("Main phase 1, Bo's turn");
    }

    [Test]
    public void Logs_every_cast_with_names_for_a_watcher()
        => TcgMatchText.LogLine(new TcgEventView(TcgEventType.CardCast, TcgSeat.A, 4, 0, 0, "bolt"), null, ["Ana", "Bo"], Name)
                       .Should().Be("Ana cast Bolt.");

    [Test]
    public void Logs_damage_with_names_for_a_watcher()
        => TcgMatchText.LogLine(new TcgEventView(TcgEventType.PlayerDamaged, TcgSeat.A, 4, -1, 3, ""), null, ["Ana", "Bo"], Name)
                       .Should().Be("Ana takes 3 damage.");

    [Test]
    public void Hides_draws_from_a_watcher()
        => TcgMatchText.LogLine(new TcgEventView(TcgEventType.CardDrawn, TcgSeat.A, 1, 0, 0, "bolt"), null, ["Ana", "Bo"], Name)
                       .Should().BeNull();

    [Test]
    public void Names_the_watched_winner_on_the_banner()
    {
        TcgMatchText.BannerText(TcgEndOutcome.Watched, "Bo").Should().Be("Bo wins!");
        TcgMatchText.BannerText(TcgEndOutcome.Draw).Should().Be("A draw.");
    }

    [Test]
    public void Names_the_watched_winner_in_the_gold_stakes()
        => TcgMatchText.StakesResult(TcgEndOutcome.Watched, TcgStakes.Gold, 2000, [], TcgSeat.A, Name, TcgSeat.B, "Bo")
                       .Should().Be("Bo won 4,000 gold.");

    [Test]
    public void Names_the_watched_winner_in_the_ante_stakes()
    {
        List<TcgAnteView> ante = [new(TcgSeat.A, "bear", false), new(TcgSeat.B, "bolt", true)];

        TcgMatchText.StakesResult(TcgEndOutcome.Watched, TcgStakes.Ante, 0, ante, TcgSeat.A, Name, TcgSeat.B, "Bo")
                     .Should().Be("Bo won bear and kept Bolt (foil).");
    }
}
