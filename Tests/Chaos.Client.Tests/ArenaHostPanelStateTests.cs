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

    private static ArenaHostDisplayArgs State(IReadOnlyList<ArenaHostModeInfo> modes, byte selectedMode = 0)
        => new()
        {
            Type = ArenaHostDisplayType.Update,
            Modes = modes,
            SelectedMode = selectedMode
        };

    [Test]
    public void Clicking_a_mode_selects_it()
    {
        var state = new ArenaHostPanelState();

        state.ClickMode(9)!.Should().BeEquivalentTo(new { Type = ArenaHostActionType.SelectMode, Mode = (byte)9 });
    }

    [Test]
    public void Free_for_all_modes_are_paged_in_eleven_row_slices()
    {
        var state = new ArenaHostPanelState();
        var modes = Enumerable.Range(1, 12).Select(id => new ArenaHostModeInfo { Id = (byte)id }).ToList();
        state.Apply(State(modes));
        var first = ArenaHostPanelState.GetPageSlice(modes.Count, state.FreeForAllPage, 11);
        first.Count.Should().Be(11);
        first.HasNext.Should().BeTrue();
        state.PageModes(false, true);
        var second = ArenaHostPanelState.GetPageSlice(modes.Count, state.FreeForAllPage, 11);
        second.Start.Should().Be(11);
        second.Count.Should().Be(1);
        second.HasPrevious.Should().BeTrue();
        second.HasNext.Should().BeFalse();
    }

    [Test]
    public void Selected_mode_jumps_when_changed_and_pages_clamp()
    {
        var state = new ArenaHostPanelState();
        var modes = Enumerable.Range(1, 23).Select(id => new ArenaHostModeInfo { Id = (byte)id }).ToList();
        state.Apply(State(modes));
        state.Apply(State(modes, 23));
        state.FreeForAllPage.Should().Be(2);
        state.PageModes(false, false);
        state.Apply(State(modes, 23));
        state.FreeForAllPage.Should().Be(1);
        state.PageModes(false, true);
        state.PageModes(false, true);
        state.PageModes(false, true);
        state.FreeForAllPage.Should().Be(2);
        state.Apply(State(modes.Take(11).ToList(), 23));
        state.FreeForAllPage.Should().Be(0);
    }

    [Test]
    public void Team_and_free_for_all_pages_are_independent_and_short_lists_do_not_page()
    {
        var state = new ArenaHostPanelState();
        var modes = Enumerable.Range(1, 24).Select(id => new ArenaHostModeInfo { Id = (byte)id, IsTeam = id > 12 }).ToList();
        state.Apply(State(modes));
        state.PageModes(false, true);
        state.PageModes(true, true);
        state.FreeForAllPage.Should().Be(1);
        state.TeamPage.Should().Be(1);
        ArenaHostPanelState.GetPageSlice(11, 0, 11).HasNext.Should().BeFalse();
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
