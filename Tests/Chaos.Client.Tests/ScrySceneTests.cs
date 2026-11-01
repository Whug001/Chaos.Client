using Chaos.Client.Collections;
using Chaos.Client.Controls.LobbyLogin.Scrying;
using Chaos.Client.Data.Definitions;
using Chaos.Client.Models;
using Chaos.Client.Rendering.CustomEmotes;
using Chaos.DarkAges.Definitions;
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

    private static ScryEntity Person(uint id, byte x, byte y, bool look = true)
        => new()
        {
            Id = id, Kind = ScryEntityKind.Aisling, X = x, Y = y, Direction = Direction.Down,
            Aisling = look ? new DisplayAislingArgs { Id = id, X = x, Y = y, Direction = Direction.Down, Name = string.Empty } : null
        };

    private static ScryFrameArgs EventsFrame(uint target, params ScryEventEntry[] events)
        => new() { Kind = ScryFrameKind.Events, TargetId = target, Events = [.. events] };

    private static ScryScene SeededScene(uint target = 1)
    {
        var scene = new ScryScene { EffectInfoSource = _ => (4, 100, false, EffectBlendMode.Normal) };
        scene.Apply(Frame(ScryFrameKind.Full, target: target, entities: [Person(1, 3, 3), Monster(2, 5, 5)]));
        scene.ProjectileCreator = (list, meffectId, _, entity, height) =>
            list.Add(
                new Projectile
                {
                    TargetEntityId = entity.Id, MeffectId = meffectId, MapHeight = height, Step = 1, StepDelayMs = 1
                });
        return scene;
    }

    [Test]
    public void Events_for_another_target_are_ignored()
    {
        var scene = SeededScene();
        scene.ApplyEvents(EventsFrame(2, new ScryEventEntry { TargetAnimation = 5, Speed = 50, TargetId = 1 }));
        scene.Effects.Should().BeEmpty();
    }

    [Test]
    public void Events_before_any_view_are_ignored()
    {
        var scene = new ScryScene { EffectInfoSource = _ => (4, 100, false, EffectBlendMode.Normal) };
        scene.ApplyEvents(EventsFrame(1, new ScryEventEntry { TargetAnimation = 5, Speed = 50, TargetId = 1 }));
        scene.Effects.Should().BeEmpty();
    }

    [Test]
    public void Target_effect_adds_to_scene_effects_not_worldstate()
    {
        var scene = SeededScene();
        scene.ApplyEvents(EventsFrame(1, new ScryEventEntry { TargetAnimation = 5, Speed = 50, TargetId = 1 }));
        scene.Effects.Should().ContainSingle().Which.TargetEntityId.Should().Be(1u);
        WorldState.ActiveEffects.Should().BeEmpty();
    }

    [Test]
    public void Point_event_adds_tile_effect()
    {
        var scene = SeededScene();
        scene.ApplyEvents(EventsFrame(1, new ScryEventEntry { TargetAnimation = 5, Speed = 50, PointX = 4, PointY = 6 }));
        var effect = scene.Effects.Should().ContainSingle().Which;
        effect.TileX.Should().Be(4);
        effect.TileY.Should().Be(6);
        effect.TargetEntityId.Should().BeNull();
        WorldState.ActiveEffects.Should().BeEmpty();
    }

    [Test]
    public void Projectile_event_adds_to_scene_projectiles()
    {
        var scene = SeededScene();
        scene.ApplyEvents(EventsFrame(1, new ScryEventEntry { TargetAnimation = 10007, Speed = 50, SourceId = 1, TargetId = 2 }));
        scene.Projectiles.Should().ContainSingle().Which.MeffectId.Should().Be(7);
        WorldState.ActiveProjectiles.Should().BeEmpty();
    }

    [Test]
    public void Projectile_with_source_animation_also_plays_source_effect()
    {
        var scene = SeededScene();
        scene.ApplyEvents(EventsFrame(1, new ScryEventEntry { TargetAnimation = 10007, SourceAnimation = 9, Speed = 50, SourceId = 1, TargetId = 2 }));
        scene.Projectiles.Should().ContainSingle();
        scene.Effects.Should().ContainSingle().Which.TargetEntityId.Should().Be(1u);
    }

    [Test]
    public void Projectile_with_missing_entity_adds_nothing()
    {
        var scene = SeededScene();
        scene.ApplyEvents(EventsFrame(1, new ScryEventEntry { TargetAnimation = 10007, Speed = 50, SourceId = 1, TargetId = 99 }));
        scene.Projectiles.Should().BeEmpty();
    }

    [Test]
    public void Body_event_starts_emote_on_matching_entity()
    {
        var scene = SeededScene();
        scene.ApplyEvents(EventsFrame(1, new ScryEventEntry { Kind = ScryEventKind.Body, SourceId = 1, BodyAnimation = BodyAnimation.Smile }));
        scene.Entities.Single(e => e.Id == 1).ActiveEmoteFrame.Should().Be(0);
        scene.Entities.Single(e => e.Id == 2).ActiveEmoteFrame.Should().Be(-1);
    }

    [Test]
    public void Body_event_with_custom_emote_byte_starts_custom_emote()
    {
        var scene = SeededScene();
        var custom = CustomEmoteRegistry.All[0].BodyAnimation;
        scene.ApplyEvents(EventsFrame(1, new ScryEventEntry { Kind = ScryEventKind.Body, SourceId = 1, BodyAnimation = (BodyAnimation)custom }));
        scene.Entities.Single(e => e.Id == 1).IsPlayingCustomEmote.Should().BeTrue();
    }

    [Test]
    public void Body_emote_expires_then_later_body_event_restarts()
    {
        var scene = SeededScene();
        scene.ApplyEvents(EventsFrame(1, new ScryEventEntry { Kind = ScryEventKind.Body, SourceId = 1, BodyAnimation = BodyAnimation.Smile }));
        var entity = scene.Entities.Single(e => e.Id == 1);
        entity.ActiveEmoteFrame.Should().BeGreaterThanOrEqualTo(0);

        scene.Update(entity.EmoteDurationMs + 100);
        entity.ActiveEmoteFrame.Should().Be(-1);

        scene.ApplyEvents(EventsFrame(1, new ScryEventEntry { Kind = ScryEventKind.Body, SourceId = 1, BodyAnimation = BodyAnimation.Smile }));
        entity.ActiveEmoteFrame.Should().BeGreaterThanOrEqualTo(0);
    }

    [Test]
    public void Custom_emote_expires_then_later_body_event_restarts()
    {
        var scene = SeededScene();
        var custom = CustomEmoteRegistry.All[0];
        scene.ApplyEvents(EventsFrame(1, new ScryEventEntry { Kind = ScryEventKind.Body, SourceId = 1, BodyAnimation = (BodyAnimation)custom.BodyAnimation }));
        var entity = scene.Entities.Single(e => e.Id == 1);
        entity.IsPlayingCustomEmote.Should().BeTrue();

        scene.Update(custom.DurationMs + 100);
        entity.IsPlayingCustomEmote.Should().BeFalse();

        scene.ApplyEvents(EventsFrame(1, new ScryEventEntry { Kind = ScryEventKind.Body, SourceId = 1, BodyAnimation = (BodyAnimation)custom.BodyAnimation }));
        entity.IsPlayingCustomEmote.Should().BeTrue();
    }

    [Test]
    public void Body_event_for_unknown_entity_is_ignored()
    {
        var scene = SeededScene();
        scene.ApplyEvents(EventsFrame(1, new ScryEventEntry { Kind = ScryEventKind.Body, SourceId = 99, BodyAnimation = BodyAnimation.Smile }));
        scene.Entities.Should().OnlyContain(e => e.ActiveEmoteFrame < 0);
    }

    [Test]
    public void New_target_in_full_frame_empties_effects()
    {
        var scene = SeededScene();
        scene.ApplyEvents(EventsFrame(1, new ScryEventEntry { TargetAnimation = 5, Speed = 50, TargetId = 1 }));
        scene.Effects.Should().NotBeEmpty();
        scene.Apply(Frame(ScryFrameKind.Full, target: 2, entities: [Person(2, 3, 3)]));
        scene.Effects.Should().BeEmpty();
        scene.Projectiles.Should().BeEmpty();
    }

    [Test]
    public void Map_change_empties_effects()
    {
        var scene = SeededScene();
        scene.ApplyEvents(EventsFrame(1, new ScryEventEntry { TargetAnimation = 5, Speed = 50, TargetId = 1 }));
        scene.Effects.Should().NotBeEmpty();
        scene.Apply(Frame(ScryFrameKind.Full, mapId: 501, entities: [Person(1, 3, 3)]));
        scene.Effects.Should().BeEmpty();
    }

    [Test]
    public void Clear_empties_effects_and_projectiles()
    {
        var scene = SeededScene();
        scene.ApplyEvents(EventsFrame(1, new ScryEventEntry { TargetAnimation = 5, Speed = 50, TargetId = 1 }));
        scene.ApplyEvents(EventsFrame(1, new ScryEventEntry { TargetAnimation = 10007, Speed = 50, SourceId = 1, TargetId = 2 }));
        scene.Clear();
        scene.Effects.Should().BeEmpty();
        scene.Projectiles.Should().BeEmpty();
    }

    [Test]
    public void Update_advances_and_removes_finished_effects()
    {
        var scene = SeededScene();
        scene.ApplyEvents(EventsFrame(1, new ScryEventEntry { TargetAnimation = 5, Speed = 100, TargetId = 1 }));
        scene.Effects.Should().ContainSingle();
        scene.Update(150);
        scene.Effects.Should().ContainSingle().Which.CurrentFrame.Should().Be(1);
        scene.Update(10000);
        scene.Effects.Should().BeEmpty();
    }
}
