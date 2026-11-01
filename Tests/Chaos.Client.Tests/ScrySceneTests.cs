using Chaos.Client.Controls.LobbyLogin.Scrying;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Geometry.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class ScrySceneTests
{
    private static ScryEntity Monster(uint id, byte x, byte y, bool look = true)
        => new()
        {
            Id = id, Kind = ScryEntityKind.Creature, X = x, Y = y, Direction = Direction.Down,
            Creature = look ? new CreatureInfo { Id = id, X = x, Y = y, Sprite = 1, Name = string.Empty } : null
        };

    private static ScryFrameArgs Frame(ScryFrameKind kind, short mapId = 500, uint target = 1, params ScryEntity[] entities)
        => new() { Kind = kind, MapId = mapId, Width = 60, Height = 40, CheckSum = 1, TargetId = target, Entities = [.. entities] };

    [Test]
    public void Positions_before_full_are_ignored()
    {
        var scene = new ScryScene();
        scene.Apply(Frame(ScryFrameKind.Positions, entities: [Monster(1, 3, 3)]));

        scene.HasView.Should().BeFalse();
        scene.Entities.Should().BeEmpty();
    }

    [Test]
    public void One_tile_step_walks_and_longer_move_jumps()
    {
        var scene = new ScryScene();
        scene.Apply(Frame(ScryFrameKind.Full, entities: [Monster(1, 3, 3), Monster(2, 10, 10)]));

        scene.Apply(Frame(ScryFrameKind.Positions, entities: [Monster(1, 4, 3, look: false), Monster(2, 14, 10, look: false)]));

        var walker = scene.Entities.Single(e => e.Id == 1);
        walker.TileX.Should().Be(4);
        walker.AnimState.Should().Be(EntityAnimState.Walking);

        var jumper = scene.Entities.Single(e => e.Id == 2);
        jumper.TileX.Should().Be(14);
        jumper.AnimState.Should().NotBe(EntityAnimState.Walking);
    }

    [Test]
    public void Full_frame_replaces_and_positions_remove()
    {
        var scene = new ScryScene();
        scene.Apply(Frame(ScryFrameKind.Full, entities: [Monster(1, 3, 3), Monster(2, 5, 5)]));
        scene.Apply(Frame(ScryFrameKind.Full, entities: [Monster(1, 3, 3)]));
        scene.Entities.Select(e => e.Id).Should().Equal(1u);

        scene.Apply(Frame(ScryFrameKind.Positions, entities: [Monster(3, 6, 6)]) with { Removed = [1] });
        scene.Entities.Select(e => e.Id).Should().Equal(3u);
    }

    [Test]
    public void Map_change_clears_entities()
    {
        var scene = new ScryScene();
        var mapChanged = 0;
        scene.MapChanged += () => mapChanged++;
        scene.Apply(Frame(ScryFrameKind.Full, mapId: 500, entities: [Monster(1, 3, 3)]));

        scene.Apply(Frame(ScryFrameKind.Full, mapId: 501, entities: [Monster(9, 3, 3)]));

        mapChanged.Should().Be(2);
        scene.Entities.Select(e => e.Id).Should().Equal(9u);
    }

    [Test]
    public void None_frame_clears_view()
    {
        var scene = new ScryScene();
        scene.Apply(Frame(ScryFrameKind.Full, entities: [Monster(1, 3, 3)]));

        scene.Apply(new ScryFrameArgs { Kind = ScryFrameKind.None });

        scene.HasView.Should().BeFalse();
        scene.Entities.Should().BeEmpty();
    }

    [Test]
    public void Stale_feed_clears_view()
    {
        var scene = new ScryScene();
        scene.Apply(Frame(ScryFrameKind.Full, entities: [Monster(1, 3, 3)]));

        scene.Update(2999);
        scene.HasView.Should().BeTrue();

        scene.Update(2);
        scene.HasView.Should().BeFalse();
    }

    [Test]
    public void Oval_mask_is_clear_in_corners_and_solid_in_center()
    {
        var pixels = ScryingViewControl.OvalMaskPixels(296, 266);

        pixels[0].A.Should().Be(0);
        pixels[133 * 296 + 148].A.Should().Be(255);
    }

    [Test]
    public void Step_direction_inverts_tile_offset()
    {
        foreach (var direction in new[] { Direction.Up, Direction.Right, Direction.Down, Direction.Left })
        {
            (var dx, var dy) = direction.ToTileOffset();
            ScryScene.StepDirection(dx, dy).Should().Be(direction);
        }
    }
}
