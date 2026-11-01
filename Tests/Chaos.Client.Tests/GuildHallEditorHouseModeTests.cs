using Chaos.Client.Controls.World.Popups.GuildHall;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public sealed class GuildHallEditorHouseModeTests
{
    private static GuildHallEditorDisplayArgs Open()
        => new()
        {
            Kind = GuildHallEditorDisplayKind.Open,
            Sets =
            [
                new GuildHallPaletteSet("free", "Free set", true, 0, [11, 12, 13], [], [])
            ],
            Npcs = [],
            EntranceX = 98,
            EntranceY = 46
        };

    [Test]
    public async Task House_shows_the_house_title()
    {
        GuildHallEditorControl.TitleFor(isHouse: true).Should().Be("HOUSE EDITOR");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Hall_keeps_the_guild_hall_title()
    {
        GuildHallEditorControl.TitleFor(isHouse: false).Should().Be("GUILD HALL EDITOR");

        await Task.CompletedTask;
    }

    [Test]
    public async Task House_hides_the_entrance_tool_and_keeps_the_rest()
    {
        foreach (var tool in Enum.GetValues<GuildHallTool>())
            GuildHallEditorControl.IsToolVisible(tool, isHouse: true).Should().Be(tool != GuildHallTool.Entrance, $"tool {tool}");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Hall_keeps_every_tool()
    {
        foreach (var tool in Enum.GetValues<GuildHallTool>())
            GuildHallEditorControl.IsToolVisible(tool, isHouse: false).Should().BeTrue($"tool {tool}");

        await Task.CompletedTask;
    }

    [Test]
    public async Task House_hides_reset_while_hall_keeps_it()
    {
        GuildHallEditorControl.IsResetVisible(isHouse: true).Should().BeFalse();
        GuildHallEditorControl.IsResetVisible(isHouse: false).Should().BeTrue();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Locked_set_points_houses_at_the_land_agent()
    {
        GuildHallEditorControl.LockedSetMessage("Stone Keep", isHouse: true)
                               .Should()
                               .Be("Buy Stone Keep from the Land Agent to use these tiles.");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Locked_set_points_halls_at_tibbs()
    {
        GuildHallEditorControl.LockedSetMessage("Stone Keep", isHouse: false)
                               .Should()
                               .Be("Buy Stone Keep from Tibbs to use these tiles.");

        await Task.CompletedTask;
    }

    [Test]
    public async Task House_cannot_pick_the_entrance_tool()
    {
        var state = new GuildHallEditorState();
        state.Open(Open() with { SpaceKind = EditableSpaceKind.House });

        state.SetTool(GuildHallTool.Entrance);

        state.Tool.Should().Be(GuildHallTool.Paint);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Reopen_as_house_resets_the_entrance_tool()
    {
        var state = new GuildHallEditorState();
        state.Open(Open());
        state.SetTool(GuildHallTool.Entrance);
        state.Tool.Should().Be(GuildHallTool.Entrance);

        state.Open(Open() with { SpaceKind = EditableSpaceKind.House });

        state.IsHouse.Should().BeTrue();
        state.Tool.Should().Be(GuildHallTool.Paint);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Hall_keeps_the_entrance_tool()
    {
        var state = new GuildHallEditorState();
        state.Open(Open());

        state.SetTool(GuildHallTool.Entrance);

        state.Tool.Should().Be(GuildHallTool.Entrance);
        state.ArgsAt(new Point(40, 41))!.Action.Should().Be(GuildHallEditorAction.MoveEntrance);

        await Task.CompletedTask;
    }
}
