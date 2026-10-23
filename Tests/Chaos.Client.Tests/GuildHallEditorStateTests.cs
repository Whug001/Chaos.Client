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
    private static GuildHallEditorDisplayArgs Open(List<GuildHallPrefabInfo>? prefabs = null)
        => new()
        {
            Kind = GuildHallEditorDisplayKind.Open,
            Sets =
            [
                new GuildHallPaletteSet("free", "Free set", true, 0, [11, 12, 13], [new GuildHallForegroundInfo(500, 0)], [new GuildHallForegroundInfo(0, 700)]) with { Prefabs = prefabs ?? [] },
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

    [Test]
    public async Task Side_defaults_to_Both_and_flows_into_args()
    {
        var state = new GuildHallEditorState();
        state.Open(Open());
        state.Side.Should().Be(GuildHallSide.Both);

        state.SetTab(GuildHallTab.Walls);
        state.Select(new GuildHallPick(0, 500, 0, null));
        state.SetSide(GuildHallSide.Left);

        var paint = state.ArgsAt(new Point(5, 6))!;
        paint.Action.Should().Be(GuildHallEditorAction.Paint);
        paint.Side.Should().Be(GuildHallSide.Left);
        (paint.Left, paint.Right).Should().Be(((ushort)500, (ushort)0));

        state.SetTool(GuildHallTool.Rect);
        var rect = state.RectArgs(new Point(5, 6), new Point(7, 8))!;
        rect.Action.Should().Be(GuildHallEditorAction.Rect);
        rect.Side.Should().Be(GuildHallSide.Left);

        state.SetTool(GuildHallTool.Erase);
        state.ArgsAt(new Point(5, 6))!.Side.Should().Be(GuildHallSide.Left);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Second_open_keeps_the_side_and_close_resets_it()
    {
        var state = new GuildHallEditorState();
        state.Open(Open());
        state.SetSide(GuildHallSide.Right);

        state.Open(Open());
        state.Side.Should().Be(GuildHallSide.Right);

        state.Close();
        state.Side.Should().Be(GuildHallSide.Both);

        await Task.CompletedTask;
    }

    [Test]
    public async Task SetSide_ignores_unknown_values()
    {
        var state = new GuildHallEditorState();
        state.Open(Open());

        state.SetSide((GuildHallSide)99);
        state.Side.Should().Be(GuildHallSide.Both);

        state.SetSide(GuildHallSide.Right);
        state.SetSide((GuildHallSide)99);
        state.Side.Should().Be(GuildHallSide.Right);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Erase_from_the_Floors_tab_sends_Both_even_after_a_side_was_picked()
    {
        var state = new GuildHallEditorState();
        state.Open(Open());
        state.SetTab(GuildHallTab.Walls);
        state.SetSide(GuildHallSide.Left);
        state.SetTab(GuildHallTab.Floors);
        state.SetTool(GuildHallTool.Erase);

        var erase = state.ArgsAt(new Point(5, 6))!;
        erase.Action.Should().Be(GuildHallEditorAction.Erase);
        erase.Side.Should().Be(GuildHallSide.Both);

        state.SetTool(GuildHallTool.Paint);
        var paint = state.ArgsAt(new Point(5, 6))!;
        paint.Action.Should().Be(GuildHallEditorAction.Paint);
        paint.Side.Should().Be(GuildHallSide.Both);

        await Task.CompletedTask;
    }

    private static GuildHallPrefabInfo Dresser() =>
        new("FG_3588_11488", 1, 5, [0, 0, 0, 0, 11488], [11493, 11492, 11491, 11490, 11489]);

    private static GuildHallPick PrefabPick(string key) => new(0, 0, 0, null, key);

    [Test]
    public async Task Prefabs_tab_forces_paint_and_ignores_shape_tools()
    {
        var state = new GuildHallEditorState();
        state.Open(Open(prefabs: [Dresser()]));
        state.SetTool(GuildHallTool.Fill);

        state.SetTab(GuildHallTab.Prefabs);
        state.Tool.Should().Be(GuildHallTool.Paint);

        state.SetTool(GuildHallTool.Rect);
        state.SetTool(GuildHallTool.Fill);
        state.SetTool(GuildHallTool.Erase);
        state.Tool.Should().Be(GuildHallTool.Paint);

        state.SetTool(GuildHallTool.Entrance);
        state.Tool.Should().Be(GuildHallTool.Entrance);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Places_prefab_only_with_paint_on_prefabs_tab()
    {
        var state = new GuildHallEditorState();
        state.Open(Open(prefabs: [Dresser()]));
        state.PlacesPrefab.Should().BeFalse();

        state.SetTab(GuildHallTab.Prefabs);
        state.PlacesPrefab.Should().BeTrue();

        state.SetTool(GuildHallTool.Entrance);
        state.PlacesPrefab.Should().BeFalse();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Prefabs_tab_lists_and_selects_the_current_sets_prefabs()
    {
        var state = new GuildHallEditorState();
        state.Open(Open(prefabs: [Dresser()]));
        state.SetTab(GuildHallTab.Prefabs);

        state.Items.Should().Equal(PrefabPick("FG_3588_11488"));
        state.Select(PrefabPick("FG_3588_11488")).Should().BeTrue();
        state.SelectedPrefab!.Key.Should().Be("FG_3588_11488");
        state.Selected.Should().Be(PrefabPick("FG_3588_11488"));

        await Task.CompletedTask;
    }

    [Test]
    public async Task Prefab_from_a_locked_set_cannot_be_selected()
    {
        var state = new GuildHallEditorState();
        state.Open(Open(prefabs: [Dresser()]));
        state.StepSet(1);
        state.SetTab(GuildHallTab.Prefabs);

        state.Select(PrefabPick("FG_3588_11488")).Should().BeFalse();
        state.SelectedPrefab.Should().BeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Click_on_prefabs_tab_sends_the_prefab_key()
    {
        var state = new GuildHallEditorState();
        state.Open(Open(prefabs: [Dresser()]));
        state.SetTab(GuildHallTab.Prefabs);
        state.ArgsAt(new Point(4, 6)).Should().BeNull();

        state.Select(PrefabPick("FG_3588_11488"));
        var args = state.ArgsAt(new Point(4, 6))!;

        args.Action.Should().Be(GuildHallEditorAction.Prefab);
        args.NpcKey.Should().Be("FG_3588_11488");
        args.X.Should().Be(4);
        args.Y.Should().Be(6);
        args.Layer.Should().Be(GuildHallLayer.Foreground);
        args.Side.Should().Be(GuildHallSide.Both);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Reopen_keeps_the_selected_prefab_and_close_clears_it()
    {
        var state = new GuildHallEditorState();
        state.Open(Open(prefabs: [Dresser()]));
        state.SetTab(GuildHallTab.Prefabs);
        state.Select(PrefabPick("FG_3588_11488"));

        state.Open(Open(prefabs: [Dresser()]));
        state.SelectedPrefab!.Key.Should().Be("FG_3588_11488");

        state.Close();
        state.SelectedPrefab.Should().BeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Prefab_footprint_outlines_the_whole_piece()
    {
        var state = new GuildHallEditorState();
        state.Open(Open(prefabs: [Dresser()]));
        state.SetTab(GuildHallTab.Prefabs);
        state.PrefabFootprint(new Point(3, 4)).Should().BeEmpty();

        state.Select(PrefabPick("FG_3588_11488"));

        state.PrefabFootprint(new Point(3, 4)).Should().BeEquivalentTo(
            [new Point(3, 4), new Point(3, 5), new Point(3, 6), new Point(3, 7), new Point(3, 8)]);

        await Task.CompletedTask;
    }

    private static GuildHallEditorDisplayArgs PlanArgs(bool practice = false)
        => new()
        {
            Kind = GuildHallEditorDisplayKind.Plan,
            PlanPieces = [new GuildHallPlanPieceInfo("spikeTrap", 20, 21)],
            AttackerEntranceX = 10,
            AttackerEntranceY = 11,
            PointsUsed = 3,
            PracticeRunning = practice
        };

    private static GuildHallEditorState OpenDefenses()
    {
        var state = new GuildHallEditorState();
        state.Open(Open());
        state.SetTab(GuildHallTab.Defenses);
        state.SelectedDefense = "spikeTrap";

        return state;
    }

    [Test]
    public async Task ApplyPlan_copies_fields()
    {
        var state = new GuildHallEditorState();

        state.ApplyPlan(PlanArgs());

        state.PlanPieces.Should().ContainSingle(p => p.Key == "spikeTrap");
        state.AttackerEntrance.Should().Be((10, 11));
        state.Heart.Should().BeNull();
        state.PointsUsed.Should().Be(3);
        state.PracticeRunning.Should().BeFalse();

        await Task.CompletedTask;
    }

    [Test]
    public async Task ArgsAt_builds_plan_actions()
    {
        var state = OpenDefenses();

        state.Tool.Should().Be(GuildHallTool.Defense);
        var place = state.ArgsAt(new Point(20, 21))!;
        place.Action.Should().Be(GuildHallEditorAction.PlaceDefense);
        place.NpcKey.Should().Be("spikeTrap");
        place.X.Should().Be(20);
        place.Y.Should().Be(21);

        state.SetTool(GuildHallTool.AttackerEntrance);
        state.ArgsAt(new Point(20, 21))!.Action.Should().Be(GuildHallEditorAction.SetAttackerEntrance);

        state.SetTool(GuildHallTool.Heart);
        state.ArgsAt(new Point(20, 21))!.Action.Should().Be(GuildHallEditorAction.SetHeart);

        state.SetTool(GuildHallTool.Erase);
        state.ArgsAt(new Point(20, 21))!.Action.Should().Be(GuildHallEditorAction.RemoveDefense);

        await Task.CompletedTask;
    }

    [Test]
    public async Task ArgsAt_during_practice_is_null()
    {
        var state = OpenDefenses();
        state.ApplyPlan(PlanArgs(practice: true));

        state.ArgsAt(new Point(20, 21)).Should().BeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task PlanMarkers_only_on_the_defenses_tab()
    {
        var state = new GuildHallEditorState();
        state.Open(Open());
        state.ApplyPlan(PlanArgs());

        state.PlanMarkers().Should().BeEmpty();

        state.SetTab(GuildHallTab.Defenses);
        state.PlanMarkers().Should().HaveCount(2);

        await Task.CompletedTask;
    }
}
