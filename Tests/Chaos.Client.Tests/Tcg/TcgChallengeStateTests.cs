#region
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using FluentAssertions;
#endregion

namespace Chaos.Client.Tests;

public sealed class TcgChallengeStateTests
{
    [Test]
    public void Builds_challenge_actions()
    {
        TcgChallengeState.StartAction("Bob").Should().Match<Chaos.Networking.Entities.Client.TcgActionArgs>(a => a.Type == TcgActionType.ChallengeStart && a.TargetName == "Bob");

        var send = TcgChallengeState.SendAction("Bob", "Main", TcgStakes.Gold, 5000);
        send.Type.Should().Be(TcgActionType.ChallengeSend);
        send.DeckName.Should().Be("Main");
        send.Stakes.Should().Be(TcgStakes.Gold);
        send.Gold.Should().Be(5000);

        TcgChallengeState.SendAction("Bob", "Main", TcgStakes.Ante, 5000).Gold.Should().Be(0);
        TcgChallengeState.AcceptAction("Main").DeckName.Should().Be("Main");
        TcgChallengeState.DeclineAction().Type.Should().Be(TcgActionType.ChallengeDecline);
        TcgChallengeState.CancelAction().Type.Should().Be(TcgActionType.ChallengeCancel);
    }

    [Test]
    [Arguments("1000", true, 1000)]
    [Arguments("1,000", true, 1000)]
    [Arguments(" 25000 ", true, 25000)]
    [Arguments("999", false, 0)]
    [Arguments("", false, 0)]
    [Arguments("abc", false, 0)]
    [Arguments("99999999999", false, 0)]
    public void Parses_gold(string text, bool ok, int gold)
    {
        TcgChallengeState.TryParseGold(text, out var parsed, out var error).Should().Be(ok);
        parsed.Should().Be(gold);
        (error.Length > 0).Should().Be(!ok);
    }

    [Test]
    public void Describes_stakes()
    {
        TcgChallengeState.StakesText(TcgStakes.None, 0).Should().Be("No stakes");
        TcgChallengeState.StakesText(TcgStakes.Gold, 5000).Should().Be("5,000 gold each");
        TcgChallengeState.StakesText(TcgStakes.Ante, 0).Should().Be("One card from each deck");
    }

    [Test]
    public void Counts_seconds_down_to_zero()
    {
        var at = new DateTime(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);

        TcgChallengeState.SecondsLeft(30000, at, at).Should().Be(30);
        TcgChallengeState.SecondsLeft(30000, at, at.AddSeconds(10.5)).Should().Be(20);
        TcgChallengeState.SecondsLeft(30000, at, at.AddSeconds(45)).Should().Be(0);
    }
}
