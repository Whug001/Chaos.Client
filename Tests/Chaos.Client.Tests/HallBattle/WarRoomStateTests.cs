using Chaos.Client.Controls.World.Popups.HallBattle;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class WarRoomStateTests
{
    private static HallBattleDisplayArgs Board(bool leader)
        => new()
        {
            Kind = HallBattleDisplayKind.Board,
            ViewerGuild = "Wolves",
            ViewerIsLeader = leader,
            BoardRows = [new("Lotus", 1100, 2, 1, 0, true), new("Wolves", 1200, 3, 0, 0, true), new("Ravens", 1100, 0, 0, 0, true)]
        };

    [Test]
    public void Board_IsSortedByRatingThenName()
    {
        var state = new WarRoomState();
        state.Apply(Board(leader: false));

        state.Rows.Select(r => r.GuildName).Should().Equal("Wolves", "Lotus", "Ravens");
    }

    [Test]
    public void NonLeader_NeverSeesChallengeForm()
    {
        var state = new WarRoomState();
        state.Apply(Board(leader: false));
        state.Apply(new HallBattleDisplayArgs { Kind = HallBattleDisplayKind.GuildDetail, DetailGuild = "Lotus", CanChallenge = false });

        state.ShowChallengeForm.Should().BeFalse();
    }

    [Test]
    public void Leader_SeesForm_OrBlockedReason()
    {
        var state = new WarRoomState();
        state.Apply(Board(leader: true));
        state.Apply(new HallBattleDisplayArgs { Kind = HallBattleDisplayKind.GuildDetail, DetailGuild = "Lotus", CanChallenge = true });
        state.ShowChallengeForm.Should().BeTrue();
        state.ShowChallengeButton.Should().BeTrue();

        state.Apply(
            new HallBattleDisplayArgs
            {
                Kind = HallBattleDisplayKind.GuildDetail,
                DetailGuild = "Lotus",
                CanChallenge = true,
                ChallengeBlockedReason = "No leader of Lotus is online."
            });
        state.ShowChallengeButton.Should().BeFalse();
        state.BlockedReason.Should().Be("No leader of Lotus is online.");
        state.ShowBlockedReason.Should().BeTrue();
    }

    [Test]
    public void WagerBox_OnlyForRanked()
    {
        var state = new WarRoomState { Ranked = false };
        state.WagerEnabled.Should().BeFalse();
        state.Ranked = true;
        state.WagerEnabled.Should().BeTrue();
    }

    [Test]
    public void TeamDisplay_SwitchesToTeamPage()
    {
        var state = new WarRoomState();
        state.Apply(new HallBattleDisplayArgs { Kind = HallBattleDisplayKind.Team, OurGuild = "Wolves", TeamSize = 3, OurMembers = ["W1"] });

        state.Page.Should().Be(WarRoomPage.Team);
        state.OurCountText.Should().Be("1/3");
    }

    [Test]
    public void ChallengeAction_CarriesTheForm()
    {
        var state = new WarRoomState { Ranked = true, ChosenSize = 5, WagerText = "2500", FormingOpenInvite = true };
        state.Apply(new HallBattleDisplayArgs { Kind = HallBattleDisplayKind.GuildDetail, DetailGuild = "Lotus", CanChallenge = true });

        var action = state.ChallengeAction();

        action.Action.Should().Be(HallBattleActionKind.Challenge);
        action.GuildName.Should().Be("Lotus");
        action.TeamSize.Should().Be(5);
        action.Ranked.Should().BeTrue();
        action.Wager.Should().Be(2500u);
        action.Mode.Should().Be(1);
    }

    [Test]
    public void ChallengeAction_DropsTheWagerWhenFriendly()
    {
        var state = new WarRoomState { Ranked = false, WagerText = "2500" };

        state.ChallengeAction().Wager.Should().Be(0u);
    }

    [Test]
    public void PickAction_TogglesAnInvitedCandidate()
    {
        var state = new WarRoomState();
        state.Apply(new HallBattleDisplayArgs { Kind = HallBattleDisplayKind.Team, Candidates = ["Ann", "Bo"], Invited = ["Bo"] });

        state.PickAction("Ann").Picked.Should().BeTrue();
        state.PickAction("Bo").Picked.Should().BeFalse();
        state.PickAction("Bo").Action.Should().Be(HallBattleActionKind.Pick);
    }
}
