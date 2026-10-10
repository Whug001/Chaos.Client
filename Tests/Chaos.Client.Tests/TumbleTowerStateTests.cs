#region
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;
#endregion

namespace Chaos.Client.Tests;

public class TumbleTowerStateTests
{
    private static TumbleTowerStateArgs Layout(params TumbleTileInfo[] tiles)
        => new()
        {
            Kind = TumbleTowerMessageKind.Layout,
            MeltMs = 1500,
            Floors =
            [
                new TumbleFloorInfo { OriginX = 1, OriginY = 1, Size = 14 },
                new TumbleFloorInfo { OriginX = 19, OriginY = 1, Size = 14 },
                new TumbleFloorInfo { OriginX = 37, OriginY = 1, Size = 14 }
            ],
            Tiles = [..tiles]
        };

    [Test]
    public void Layout_activates_and_finds_floors()
    {
        var state = new TumbleTowerState();
        state.Apply(Layout(), 10);

        state.IsActive.Should().BeTrue();
        state.FloorsInUse.Should().Be(3);
        state.FloorAt(1, 1).Should().Be(0);
        state.FloorAt(32, 14).Should().Be(1);
        state.FloorAt(16, 5).Should().BeNull();
        state.SameSpotOn(0, 4, 6, 2).Should().Be((40, 6));
    }

    [Test]
    public void Tile_updates_are_back_dated_by_their_age()
    {
        var state = new TumbleTowerState();
        state.Apply(Layout(), 10);
        state.Apply(new TumbleTowerStateArgs
        {
            Kind = TumbleTowerMessageKind.TileUpdates,
            Tiles = [new TumbleTileInfo { X = 5, Y = 5, State = TumbleTowerTileState.Melting, MsSinceChange = 750 }]
        }, 20);

        var tile = state.TileAt(5, 5, 20);
        tile.State.Should().Be(TumbleTowerTileState.Melting);
        tile.Progress.Should().BeApproximately(0.5, 0.0001);
        state.TileAt(5, 5, 30).Progress.Should().Be(1);
        state.TileAt(6, 6, 20).State.Should().Be(TumbleTowerTileState.Solid);
    }

    [Test]
    public void A_new_layout_replaces_tiles_and_clear_empties_everything()
    {
        var state = new TumbleTowerState();
        state.Apply(Layout(new TumbleTileInfo { X = 2, Y = 2, State = TumbleTowerTileState.Open }), 1);
        state.TileAt(2, 2, 1).State.Should().Be(TumbleTowerTileState.Open);

        state.Apply(Layout(), 2);
        state.TileAt(2, 2, 2).State.Should().Be(TumbleTowerTileState.Solid);

        state.Apply(new TumbleTowerStateArgs { Kind = TumbleTowerMessageKind.Clear }, 3);
        state.IsActive.Should().BeFalse();
        state.FloorAt(1, 1).Should().BeNull();
    }

    [Test]
    public void Falls_last_until_their_animation_and_landing_are_over()
    {
        var state = new TumbleTowerState();
        state.Apply(Layout(), 0);
        state.Apply(new TumbleTowerStateArgs
        {
            Kind = TumbleTowerMessageKind.Fall, EntityId = 7, FromX = 5, FromY = 5, ToX = 23, ToY = 5, FloorsDropped = 1,
            Outcome = TumbleTowerFallOutcome.Landed
        }, 100);

        state.ActiveFall(7, 101).Should().NotBeNull();
        state.ActiveFall(7, 100 + TumbleDiveTimeline.TotalSeconds(1) + 0.01).Should().BeNull();
    }

    [Test]
    public void Collecting_active_falls_refills_the_given_list_with_only_the_falls_still_playing()
    {
        var state = new TumbleTowerState();
        state.Apply(Layout(), 0);

        foreach ((var id, var at) in new[] { (7u, 100.0), (8u, 90.0) })
            state.Apply(new TumbleTowerStateArgs
            {
                Kind = TumbleTowerMessageKind.Fall, EntityId = id, FromX = 5, FromY = 5, ToX = 23, ToY = 5, FloorsDropped = 1,
                Outcome = TumbleTowerFallOutcome.Landed
            }, at);

        var into = new List<TumbleFall> { new(99, 0, 0, 0, 0, 1, TumbleTowerFallOutcome.Landed, 0) };
        state.CollectActiveFalls(101, into);

        into.Select(f => f.EntityId).Should().Equal(7u);
        state.ActiveFall(8, 90.5).Should().NotBeNull();
    }

    [Test]
    public void Status_sets_the_label_numbers()
    {
        var state = new TumbleTowerState();
        state.Apply(Layout(), 0);
        state.Apply(new TumbleTowerStateArgs { Kind = TumbleTowerMessageKind.Status, PlayersLeft = 6, SecondsLeft = 42 }, 0);

        state.PlayersLeft.Should().Be(6);
        state.SecondsLeft.Should().Be(42);
    }
}
