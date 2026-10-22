using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Geometry.Abstractions.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public sealed class GuildHallEditorStateTests
{
    private static GuildHallEditorDisplayArgs Open()
        => new()
        {
            Kind = GuildHallEditorDisplayKind.Open,
            Sets =
            [
                new GuildHallPaletteSet("free", "Free set", true, 0, [11, 12, 13], [new GuildHallForegroundInfo(500, 0)], [new GuildHallForegroundInfo(0, 700)]),
                new GuildHallPaletteSet("stone", "Stone Keep", false, 10000000, [21], [new GuildHallForegroundInfo(800, 801)], [])
            ],
            Npcs = [new GuildHallNpcOption("tibbs", "Tibbs", 61, 99, 39, true), new GuildHallNpcOption("loom", "Loom", 70, 0, 0, false)],
            EntranceX = 98,
            EntranceY = 46
        };

    [Test]
    public async Task Open_fills_state_and_selects_first_owned_floor()
    {
        var state = new GuildHallEditorState();
        state.Open(Open());

        state.IsOpen.Should().BeTrue();
        state.Sets.Should().HaveCount(2);
        state.Npcs.Should().HaveCount(2);
        state.Entrance.Should().Be(new Point(98, 46));
        state.Tab.Should().Be(GuildHallTab.Floors);
        state.Layer.Should().Be(GuildHallLayer.Floor);
        state.Tool.Should().Be(GuildHallTool.Paint);
        state.SelectedFloor.Should().Be((ushort)11);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Second_open_keeps_tab_tool_and_selection()
    {
        var state = new GuildHallEditorState();
        state.Open(Open());
        state.SetTab(GuildHallTab.Walls);
        state.Select(new GuildHallPick(0, 500, 0, null));
        state.SetTool(GuildHallTool.Rect);

        var again = Open();
        again.EntranceX = 90;
        state.Open(again);

        state.Tab.Should().Be(GuildHallTab.Walls);
        state.Tool.Should().Be(GuildHallTool.Rect);
        state.SelectedForeground.Should().Be(new GuildHallForegroundInfo(500, 0));
        state.Entrance.Should().Be(new Point(90, 46));

        await Task.CompletedTask;
    }

    [Test]
    public async Task Tabs_list_their_items_and_set_the_layer()
    {
        var state = new GuildHallEditorState();
        state.Open(Open());

        state.SetTab(GuildHallTab.Walls);
        state.Layer.Should().Be(GuildHallLayer.Foreground);
        state.Items.Should().Equal(new GuildHallPick(0, 500, 0, null));

        state.SetTab(GuildHallTab.Decor);
        state.Items.Should().Equal(new GuildHallPick(0, 0, 700, null));

        state.SetTab(GuildHallTab.Npcs);
        state.Tool.Should().Be(GuildHallTool.Npc);
        state.Items.Select(p => p.NpcKey).Should().Equal("tibbs", "loom");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Locked_set_items_cannot_be_selected()
    {
        var state = new GuildHallEditorState();
        state.Open(Open());
        state.StepSet(1);

        state.CurrentSet!.Key.Should().Be("stone");
        state.CurrentSetLocked.Should().BeTrue();
        state.Select(new GuildHallPick(21, 0, 0, null)).Should().BeFalse();
        state.SelectedFloor.Should().Be((ushort)11);

        await Task.CompletedTask;
    }

    [Test]
    public async Task ArgsAt_builds_each_single_tile_action()
    {
        var state = new GuildHallEditorState();
        state.Open(Open());

        var paint = state.ArgsAt(new Point(5, 6))!;
        paint.Action.Should().Be(GuildHallEditorAction.Paint);
        paint.Layer.Should().Be(GuildHallLayer.Floor);
        paint.Background.Should().Be(11);
        (paint.X, paint.Y).Should().Be(((byte)5, (byte)6));

        state.SetTab(GuildHallTab.Walls);
        state.Select(new GuildHallPick(0, 500, 0, null));
        state.SetTool(GuildHallTool.Fill);
        var fill = state.ArgsAt(new Point(1, 2))!;
        fill.Action.Should().Be(GuildHallEditorAction.Fill);
        fill.Layer.Should().Be(GuildHallLayer.Foreground);
        (fill.Left, fill.Right).Should().Be(((ushort)500, (ushort)0));

        state.SetTool(GuildHallTool.Erase);
        state.ArgsAt(new Point(1, 2))!.Action.Should().Be(GuildHallEditorAction.Erase);

        state.SetTool(GuildHallTool.Entrance);
        state.ArgsAt(new Point(40, 41))!.Action.Should().Be(GuildHallEditorAction.MoveEntrance);

        state.SetTab(GuildHallTab.Npcs);
        state.ArgsAt(new Point(3, 3)).Should().BeNull();
        state.Select(new GuildHallPick(0, 0, 0, "loom"));
        state.TurnNpc();
        var npc = state.ArgsAt(new Point(3, 3))!;
        npc.Action.Should().Be(GuildHallEditorAction.PlaceNpc);
        npc.NpcKey.Should().Be("loom");
        npc.Direction.Should().Be(Direction.Left);

        await Task.CompletedTask;
    }

    [Test]
    public async Task ArgsAt_refuses_tiles_outside_the_byte_range()
    {
        var state = new GuildHallEditorState();
        state.Open(Open());

        state.ArgsAt(new Point(-1, 0)).Should().BeNull();
        state.ArgsAt(new Point(0, 256)).Should().BeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task ActiveRefusal_shows_then_expires()
    {
        var state = new GuildHallEditorState();
        state.Open(Open());
        state.ActiveRefusal.Should().BeNull();

        state.Refuse("That wall would trap Mike.");
        state.ActiveRefusal.Should().Be("That wall would trap Mike.");
        state.ActiveRefusalAt(state.RefusedAtUtc + TimeSpan.FromSeconds(4)).Should().Be("That wall would trap Mike.");
        state.ActiveRefusalAt(state.RefusedAtUtc + TimeSpan.FromSeconds(6)).Should().BeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Successful_actions_clear_the_refusal()
    {
        var state = new GuildHallEditorState();
        state.Open(Open());

        state.Refuse("Slow down a moment.");
        state.Select(new GuildHallPick(11, 0, 0, null));
        state.ActiveRefusal.Should().BeNull();

        state.Refuse("Slow down a moment.");
        state.SetTool(GuildHallTool.Rect);
        state.ActiveRefusal.Should().BeNull();

        state.Refuse("Slow down a moment.");
        state.SetTab(GuildHallTab.Walls);
        state.ActiveRefusal.Should().BeNull();

        state.Refuse("Slow down a moment.");
        state.StepSet(1);
        state.ActiveRefusal.Should().BeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Reopen_clears_the_refusal_including_the_locked_set_hint()
    {
        var state = new GuildHallEditorState();
        state.Open(Open());
        state.Refuse("Buy Stone Keep from Tibbs to use these tiles.");
        state.SetTab(GuildHallTab.Walls);

        state.Open(Open());
        state.ActiveRefusal.Should().BeNull();
        state.Tab.Should().Be(GuildHallTab.Walls);

        await Task.CompletedTask;
    }

    [Test]
    public async Task ClearRefusal_clears_the_active_refusal()
    {
        var state = new GuildHallEditorState();
        state.Open(Open());

        state.Refuse("Someone is standing there.");
        state.ActiveRefusal.Should().Be("Someone is standing there.");

        state.ClearRefusal();
        state.ActiveRefusal.Should().BeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Empty_hint_restarts_at_each_press_start()
    {
        var state = new GuildHallEditorState();
        state.Open(Open());

        //a Paint stroke with nothing selected: the press tile shows the hint, later drag steps stay quiet
        state.ResetEmptyHint();
        state.TakeEmptyHint().Should().BeTrue();
        state.TakeEmptyHint().Should().BeFalse();

        //the next stroke restarts it, so the hint shows exactly once per stroke including the press tile
        state.ResetEmptyHint();
        state.TakeEmptyHint().Should().BeTrue();
        state.TakeEmptyHint().Should().BeFalse();

        //a Fill click restarts it too
        state.ResetEmptyHint();
        state.TakeEmptyHint().Should().BeTrue();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Empty_hint_shows_once_per_stroke()
    {
        var state = new GuildHallEditorState();
        state.Open(Open());

        state.TakeEmptyHint().Should().BeTrue();
        state.TakeEmptyHint().Should().BeFalse();

        state.ResetEmptyHint();
        state.TakeEmptyHint().Should().BeTrue();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Refuse_stores_reason_and_close_resets()
    {
        var state = new GuildHallEditorState();
        state.Open(Open());
        state.Refuse("That wall would trap Mike.");
        state.LastRefusedReason.Should().Be("That wall would trap Mike.");

        state.Close();
        state.IsOpen.Should().BeFalse();
        state.Sets.Should().BeEmpty();
        state.Npcs.Should().BeEmpty();
        state.LastRefusedReason.Should().BeNull();
        state.SelectedFloor.Should().BeNull();
        state.Tool.Should().Be(GuildHallTool.Paint);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Pages_step_within_bounds()
    {
        var floors = Enumerable.Range(1, 45).Select(i => (ushort)i).ToList();
        var args = Open();
        args.Sets = [new GuildHallPaletteSet("free", "Free set", true, 0, floors, [], [])];
        var state = new GuildHallEditorState();
        state.Open(args);

        state.PageCount.Should().Be(3);
        state.PageItems.Should().HaveCount(GuildHallEditorState.PAGE_SIZE);
        state.StepPage(5);
        state.Page.Should().Be(2);
        state.PageItems.Should().HaveCount(5);
        state.SetTab(GuildHallTab.Walls);
        state.Page.Should().Be(0);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Owned_press_is_swallowed_once_then_cleared()
    {
        var state = new GuildHallEditorState();
        state.ConsumePress().Should().BeFalse();

        state.Open(Open());
        state.ClaimPress();
        state.PressOwned.Should().BeTrue();

        state.ConsumePress().Should().BeTrue();
        state.PressOwned.Should().BeFalse();
        state.ConsumePress().Should().BeFalse();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Press_claim_needs_an_open_editor_and_close_clears_it()
    {
        var state = new GuildHallEditorState();
        state.ClaimPress();
        state.PressOwned.Should().BeFalse();

        state.Open(Open());
        state.ClaimPress();
        state.Close();
        state.PressOwned.Should().BeFalse();

        await Task.CompletedTask;
    }
}
