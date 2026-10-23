using Chaos.Client.Controls.World;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class HallBattleBarTextTests
{
    [Test]
    public void RoundText()
        => HallBattleBarControl.TextFor(
                                   new HallBattleDisplayArgs
                                   {
                                       Kind = HallBattleDisplayKind.Bar,
                                       Round = 1,
                                       DefendingGuild = "Lotus",
                                       SecondsLeft = 754,
                                       HeartPercent = 60
                                   })
                               .Should()
                               .Be("Round 1 - Lotus defends - 12:34");

    [Test]
    public void BreakText()
        => HallBattleBarControl.TextFor(new HallBattleDisplayArgs { Kind = HallBattleDisplayKind.Bar, Round = 2, InBreak = true, SecondsLeft = 45 })
                               .Should()
                               .Be("Break - Round 2 in 0:45");

    [Test]
    public void Fill_FollowsHeartPercent()
        => HallBattleBarControl.FillWidth(60, 200).Should().Be(120);

    [Test]
    public void Fill_ClampsOverOneHundred()
        => HallBattleBarControl.FillWidth(250, 200).Should().Be(200);
}
