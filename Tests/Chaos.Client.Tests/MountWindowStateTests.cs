using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class MountWindowStateTests
{
    private static MountWindowMount M(byte id, string name) => new() { Mount = id, Name = name, Sprite = (ushort)(900 + id) };

    private static MountWindowDisplayArgs Open(int count, byte current = 0, DisplayColor color = DisplayColor.Default)
        => new()
        {
            Type = MountWindowDisplayType.Open,
            Mounts = Enumerable.Range(1, count).Select(i => M((byte)i, $"M{i:00}")).ToList(),
            Colors = [DisplayColor.Default, DisplayColor.Black],
            CurrentMount = current,
            CurrentColor = color
        };

    [Test]
    public async Task Opens_on_current_mount_and_its_page()
    {
        var state = new MountWindowState();
        state.Open(Open(20, current: 14));

        state.Selected!.Mount.Should().Be(14);
        state.Page.Should().Be(1);
        state.PageCount.Should().Be(2);
        state.PageItems.Should().HaveCount(8);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Opens_on_first_mount_without_a_current_one()
    {
        var state = new MountWindowState();
        state.Open(Open(5, current: 0));
        state.Selected!.Mount.Should().Be(1);
        state.Page.Should().Be(0);

        state.Open(Open(5, current: 29));
        state.Selected!.Mount.Should().Be(1);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Empty_list_has_no_selection()
    {
        var state = new MountWindowState();
        state.Open(Open(0));

        state.Selected.Should().BeNull();
        state.PageCount.Should().Be(1);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Empty_list_cannot_choose()
    {
        var state = new MountWindowState();
        state.Open(Open(0));

        state.Selected.Should().BeNull();
        state.CanChoose.Should().BeFalse();

        state.Open(Open(2));

        state.Selected.Should().NotBeNull();
        state.CanChoose.Should().BeTrue();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Keeps_owned_current_color_and_ignores_unowned_ones()
    {
        var state = new MountWindowState();
        state.Open(Open(3, color: DisplayColor.Black));
        state.Color.Should().Be(DisplayColor.Black);

        state.SetColor(DisplayColor.LightBlue);
        state.Color.Should().Be(DisplayColor.Black);

        state.Open(Open(3, color: DisplayColor.LightBlue));
        state.Color.Should().Be(DisplayColor.Default);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Turn_goes_down_left_up_right()
    {
        var state = new MountWindowState();
        state.Open(Open(1));

        var seen = new List<int> { state.Facing };

        for (var i = 0; i < 4; i++)
        {
            state.Turn();
            seen.Add(state.Facing);
        }

        seen.Should().Equal(0, 3, 2, 1, 0);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Paging_stays_in_range()
    {
        var state = new MountWindowState();
        state.Open(Open(13));

        state.StepPage(-1);
        state.Page.Should().Be(0);
        state.StepPage(1);
        state.Page.Should().Be(1);
        state.StepPage(1);
        state.Page.Should().Be(1);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Select_changes_selection_but_not_page()
    {
        var state = new MountWindowState();
        state.Open(Open(13));
        state.StepPage(1);
        state.Select(state.PageItems[0]);

        state.Selected!.Mount.Should().Be(13);
        state.Page.Should().Be(1);

        await Task.CompletedTask;
    }
}
