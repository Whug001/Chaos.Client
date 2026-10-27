using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class EffectWindowStateTests
{
    private static EffectWindowEntry E(byte id, bool owned = true) => new() { Effect = id, Name = $"E{id}", Animation = (ushort)(400 + id), Owned = owned };

    private static EffectWindowDisplayArgs Open(byte currentLogin = 0, byte currentDeath = 0)
        => new()
        {
            Type = EffectWindowDisplayType.Open,
            LoginEffects = Enumerable.Range(1, 9).Select(i => E((byte)i, owned: i != 3)).ToList(),
            DeathEffects = Enumerable.Range(1, 10).Select(i => E((byte)i, owned: i == 4)).ToList(),
            CurrentLogin = currentLogin,
            CurrentDeath = currentDeath
        };

    [Test]
    public async Task Opens_on_login_tab_with_current_pick_on_its_page()
    {
        var state = new EffectWindowState();
        state.Open(Open(currentLogin: 8));

        state.Tab.Should().Be(EffectWindowKind.LoginLogout);
        state.Entries[0].Should().Be(EffectWindowState.NoneEntry);
        state.Entries.Should().HaveCount(10);
        state.Selected!.Effect.Should().Be(8);
        state.Page.Should().Be(1);
        state.PageCount.Should().Be(2);
        state.PageItems.Should().HaveCount(4);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Selects_none_without_a_known_current_pick()
    {
        var state = new EffectWindowState();
        state.Open(Open(currentLogin: 0));
        state.Selected.Should().Be(EffectWindowState.NoneEntry);

        state.Open(Open(currentLogin: 77));
        state.Selected.Should().Be(EffectWindowState.NoneEntry);
        state.Page.Should().Be(0);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Tabs_keep_their_own_selection_and_page()
    {
        var state = new EffectWindowState();
        state.Open(Open(currentLogin: 2, currentDeath: 4));

        state.SetTab(EffectWindowKind.Death);
        state.Selected!.Effect.Should().Be(4);
        state.StepPage(1);
        state.Select(state.PageItems[0]);
        state.Selected!.Effect.Should().Be(6);

        state.SetTab(EffectWindowKind.LoginLogout);
        state.Selected!.Effect.Should().Be(2);
        state.Page.Should().Be(0);

        state.SetTab(EffectWindowKind.Death);
        state.Selected!.Effect.Should().Be(6);
        state.Page.Should().Be(1);

        await Task.CompletedTask;
    }

    [Test]
    public async Task CanChoose_and_status_follow_ownership()
    {
        var state = new EffectWindowState();
        state.Open(Open(currentLogin: 2));

        state.Select(state.Entries.Single(e => e.Effect == 3));
        state.CanChoose.Should().BeFalse();
        state.StatusText.Should().Be("Sold by Illustria in Abel.");

        state.Select(state.Entries.Single(e => e.Effect == 5));
        state.CanChoose.Should().BeTrue();
        state.StatusText.Should().Be("Current: E2");

        state.Select(EffectWindowState.NoneEntry);
        state.CanChoose.Should().BeTrue();

        state.SetTab(EffectWindowKind.Death);
        state.StatusText.Should().Be("Current: None");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Update_moves_the_current_pick_but_not_the_selection()
    {
        var state = new EffectWindowState();
        state.Open(Open(currentLogin: 2));
        state.Select(state.Entries.Single(e => e.Effect == 5));

        state.Update(new EffectWindowDisplayArgs { Type = EffectWindowDisplayType.Update, CurrentLogin = 5, CurrentDeath = 4 });

        state.CurrentLogin.Should().Be(5);
        state.CurrentDeath.Should().Be(4);
        state.IsCurrent(state.Entries.Single(e => e.Effect == 5)).Should().BeTrue();
        state.IsCurrent(state.Entries.Single(e => e.Effect == 2)).Should().BeFalse();
        state.Selected!.Effect.Should().Be(5);

        await Task.CompletedTask;
    }

    [Test]
    public void Bubble_tab_has_only_the_none_entry()
    {
        var state = new EffectWindowState();
        state.SetTab(EffectWindowKind.ChatBubble);

        state.Entries.Should().ContainSingle().Which.Should().Be(EffectWindowState.NoneEntry);
    }

    [Test]
    public async Task Paging_clamps_and_turn_goes_down_left_up_right()
    {
        var state = new EffectWindowState();
        state.Open(Open());

        state.StepPage(-1);
        state.Page.Should().Be(0);
        state.StepPage(5);
        state.Page.Should().Be(1);

        var seen = new List<int> { state.Facing };

        for (var i = 0; i < 4; i++)
        {
            state.Turn();
            seen.Add(state.Facing);
        }

        seen.Should().Equal(0, 3, 2, 1, 0);

        await Task.CompletedTask;
    }
}
