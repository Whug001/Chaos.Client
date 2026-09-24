using Chaos.Client.Systems;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public class StageLightingPanelStateTests
{
    private static readonly Rectangle Stage = new(0, 12, 9, 9);

    private static StageLightingInteractionArgs Edit(byte level) => new() { Action = StageLightingAction.SetHouseLevel, Level = level };

    [Test]
    public void Throttle_sends_at_most_every_125_ms_and_flush_sends_the_last()
    {
        var state = new StageLightingPanelState();

        state.Throttle(Edit(1), 1000).Should().NotBeNull();
        state.Throttle(Edit(2), 1050).Should().BeNull();
        state.Throttle(Edit(3), 1100).Should().BeNull();
        state.Flush(1110)!.Level.Should().Be(3);
        state.Flush(1120).Should().BeNull();
        state.Throttle(Edit(4), 1300).Should().NotBeNull();
    }


    [Test]
    public void TakeDue_sends_a_held_edit_once_the_interval_has_passed()
    {
        var state = new StageLightingPanelState();

        state.Throttle(Edit(1), 1000).Should().NotBeNull();
        state.Throttle(Edit(2), 1050).Should().BeNull();
        state.TakeDue(1100).Should().BeNull();
        state.TakeDue(1125)!.Level.Should().Be(2);
        state.TakeDue(1300).Should().BeNull();
    }

    [Test]
    public void Reconcile_keeps_a_valid_selection_or_picks_the_first()
    {
        var state = new StageLightingPanelState();
        StageLightInfo[] lights = [new() { Id = 2 }, new() { Id = 5 }];

        state.Reconcile(lights, 0);
        state.SelectedLightId.Should().Be((byte)2);

        state.Select(5);
        state.Reconcile(lights, 0);
        state.SelectedLightId.Should().Be((byte)5);

        state.Reconcile([new StageLightInfo { Id = 2 }], 0);
        state.SelectedLightId.Should().Be((byte)2);

        state.Reconcile([], 0);
        state.SelectedLightId.Should().BeNull();
    }

    [Test]
    public void Reconcile_selects_a_new_light_after_add()
    {
        var state = new StageLightingPanelState();
        state.Reconcile([new StageLightInfo { Id = 1 }], 0);

        state.ExpectNewLight(0);
        state.Reconcile([new StageLightInfo { Id = 1 }, new StageLightInfo { Id = 2 }], 100);

        state.SelectedLightId.Should().Be((byte)2);
    }

    [Test]
    public void Reconcile_gives_up_expecting_a_new_light_after_the_deadline()
    {
        var state = new StageLightingPanelState();
        state.Reconcile([new StageLightInfo { Id = 1 }], 0);

        state.ExpectNewLight(0);
        state.Reconcile([new StageLightInfo { Id = 1 }, new StageLightInfo { Id = 2 }], 3500);
        state.SelectedLightId.Should().Be((byte)1);

        state.Reconcile([new StageLightInfo { Id = 1 }, new StageLightInfo { Id = 3 }], 3600);
        state.SelectedLightId.Should().Be((byte)1);
    }

    [Test]
    public void Selecting_cancels_a_follow_pick()
    {
        var state = new StageLightingPanelState();
        state.Select(1);
        state.BeginFollowPick();
        state.AwaitingFollowPick.Should().BeTrue();

        state.Select(2);
        state.AwaitingFollowPick.Should().BeFalse();
    }

    [Test]
    public void New_light_sits_on_the_stage_centre()
    {
        var light = StageLightingPanelState.NewLight(Stage);

        light.X.Should().Be(64);
        light.Y.Should().Be(256);
        light.Brightness.Should().Be(80);
    }

    [Test]
    public void Sweep_puts_the_second_point_two_tiles_along_x_or_back_at_the_edge()
    {
        var middle = StageLightingPanelState.WithMotion(new StageLightInfo { X = 64, Y = 256 }, StageLightMotion.Sweep, Stage);
        middle.X2.Should().Be(96);
        middle.Y2.Should().Be(256);

        var edge = StageLightingPanelState.WithMotion(new StageLightInfo { X = 128, Y = 256 }, StageLightMotion.Circle, Stage);
        edge.X2.Should().Be(96);
    }

    [Test]
    public void Leaving_follow_clears_the_target()
        => StageLightingPanelState.WithMotion(new StageLightInfo { Motion = StageLightMotion.Follow, FollowId = 9 }, StageLightMotion.Still, Stage)
                                  .FollowId
                                  .Should()
                                  .Be(0u);

    [Test]
    public void Step_wraps_both_ways()
    {
        StageLightingPanelState.Step(StageLightMotion.Follow, 1).Should().Be(StageLightMotion.Still);
        StageLightingPanelState.Step(StageLightMotion.Still, -1).Should().Be(StageLightMotion.Follow);
        StageLightingPanelState.Step(StageLightEffect.None, 1).Should().Be(StageLightEffect.Pulse);
    }

    [Test]
    public void Geometry_centres_the_stage_and_round_trips()
    {
        var centre = StageViewGeometry.TileToView(new Vector2(4, 16), Stage, 200, 150);
        centre.Should().Be(new Vector2(100, 75));

        var tile = new Vector2(2.25f, 18.5f);
        var back = StageViewGeometry.ViewToTile(StageViewGeometry.TileToView(tile, Stage, 200, 150), Stage, 200, 150);
        back.X.Should().BeApproximately(tile.X, 0.001f);
        back.Y.Should().BeApproximately(tile.Y, 0.001f);
    }

    [Test]
    public void ToUnits_clamps_to_the_stage()
    {
        StageViewGeometry.ToUnits(new Vector2(-3, 40), Stage).Should().Be(((ushort)0, (ushort)320));
        StageViewGeometry.ToUnits(new Vector2(4.5f, 16.25f), Stage).Should().Be(((ushort)72, (ushort)260));
    }
}
