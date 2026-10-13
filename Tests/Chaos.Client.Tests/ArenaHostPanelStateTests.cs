using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class ArenaHostPanelStateTests
{
    private static ArenaHostDisplayArgs State(ArenaHostStatus status = ArenaHostStatus.Idle, bool canStart = true, bool wolfHunt = false)
        => new()
        {
            Type = ArenaHostDisplayType.Update,
            Status = status,
            CanStart = canStart,
            Options = wolfHunt
                ? [new ArenaHostOptionInfo { Option = ArenaHostOption.GreenCaptain, Label = "Green captain", Value = "Pick" }]
                : [new ArenaHostOptionInfo { Option = ArenaHostOption.HostPlaying, Label = "I am", Value = "Playing" }]
        };

    [Test]
    public void Clicking_a_mode_selects_it()
    {
        var state = new ArenaHostPanelState();

        state.ClickMode(9)!.Should().BeEquivalentTo(new { Type = ArenaHostActionType.SelectMode, Mode = (byte)9 });
    }

    [Test]
    public void A_roster_click_cycles_the_team()
        => new ArenaHostPanelState().ClickRosterName("Ivy").Type.Should().Be(ArenaHostActionType.CycleTeam);

    [Test]
    public void After_a_captain_click_the_next_name_is_the_captain()
    {
        var state = new ArenaHostPanelState();
        state.Apply(State(wolfHunt: true));

        state.ClickOption(ArenaHostOption.GreenCaptain, true).Should().BeNull();
        var pick = state.ClickRosterName("Ivy");

        pick.Type.Should().Be(ArenaHostActionType.PickCaptain);
        pick.Option.Should().Be(ArenaHostOption.GreenCaptain);
        state.PickingCaptain.Should().BeNull();
    }

    [Test]
    public void Pick_mode_ends_when_the_mode_has_no_captains()
    {
        var state = new ArenaHostPanelState();
        state.Apply(State(wolfHunt: true));
        state.ClickOption(ArenaHostOption.GreenCaptain, true);

        state.Apply(State());

        state.PickingCaptain.Should().BeNull();
    }

    [Test]
    public void Cancelling_a_pick_leaves_pick_mode_and_says_so()
    {
        var state = new ArenaHostPanelState();
        state.Apply(State(wolfHunt: true));
        state.ClickOption(ArenaHostOption.GreenCaptain, true);

        state.CancelPick().Should().BeTrue();
        state.PickingCaptain.Should().BeNull();
        state.ClickRosterName("Ivy").Type.Should().Be(ArenaHostActionType.CycleTeam);
        state.CancelPick().Should().BeFalse();
    }

    [Test]
    public void The_vote_picker_holds_two_to_five()
    {
        var state = new ArenaHostPanelState();
        state.ToggleVoteMode();
        state.ClickMode(1);

        state.OpenVote().Should().BeNull();

        foreach (byte id in new byte[] { 2, 3, 4, 5, 6 })
            state.ClickMode(id);

        state.VoteSelection.Should().HaveCount(5);
        state.OpenVote()!.VoteModes.Should().HaveCount(5);
    }

    [Test]
    public void The_vote_options_keep_click_order_after_an_untoggle()
    {
        var state = new ArenaHostPanelState();
        state.ToggleVoteMode();

        foreach (byte id in new byte[] { 3, 1, 2, 1, 1 })
            state.ClickMode(id);

        state.VoteSelection.Should().Equal(3, 2, 1);
        state.OpenVote()!.VoteModes.Should().Equal(3, 2, 1);
    }

    [Test]
    public void The_footer_starts_only_when_allowed()
    {
        var state = new ArenaHostPanelState();
        state.Apply(State(canStart: false));
        state.ClickMainButton().Should().BeNull();

        state.Apply(State());
        state.ClickMainButton()!.Type.Should().Be(ArenaHostActionType.Start);
    }

    [Test]
    public void The_footer_cancels_a_countdown()
    {
        var state = new ArenaHostPanelState();
        state.Apply(State(ArenaHostStatus.Countdown));

        state.ClickMainButton()!.Type.Should().Be(ArenaHostActionType.Cancel);
    }

    [Test]
    public void Ending_a_match_takes_two_clicks()
    {
        var state = new ArenaHostPanelState();
        state.Apply(State(ArenaHostStatus.MatchRunning));

        state.ClickMainButton().Should().BeNull();
        state.MainButtonCaption.Should().Be("Are you sure?");
        state.ClickMainButton()!.Type.Should().Be(ArenaHostActionType.EndMatch);
    }
}
