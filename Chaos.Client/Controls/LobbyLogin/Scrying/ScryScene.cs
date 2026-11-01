#region
using Chaos.Client.Collections;
using Chaos.Client.Data.Definitions;
using Chaos.Client.Models;
using Chaos.Client.Systems;
using Chaos.Geometry.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Controls.LobbyLogin.Scrying;

/// <summary>The entities in the login scrying view, kept apart from WorldState.</summary>
public sealed class ScryScene
{
    public const double STALE_AFTER_MS = 3000;

    private readonly Dictionary<uint, WorldEntity> EntitiesById = [];
    private double MsSinceFrame;

    public event Action? MapChanged;
    public event Action? TargetChanged;

    public IReadOnlyCollection<WorldEntity> Entities => EntitiesById.Values;
    public bool HasView { get; private set; }
    public short MapId { get; private set; } = -1;
    public byte Width { get; private set; }
    public byte Height { get; private set; }
    public ushort CheckSum { get; private set; }
    public uint TargetId { get; private set; }

    private const int PROJECTILE_ANIMATION_BASE = 10000;
    private const int PROJECTILE_ANIMATION_MAX_EXCLUSIVE = 12000;

    public List<Animation> Effects { get; } = [];
    public List<Projectile> Projectiles { get; } = [];

    /// <summary>Spell-effect metadata source. Defaults to the shared resolver the game wires to its effect renderer.</summary>
    public Func<int, (int FrameCount, int FileIntervalMs, bool IsEfa, EffectBlendMode BlendMode)?> EffectInfoSource { get; set; } =
        id => EffectSpawner.EffectInfoResolver(id);

    /// <summary>Projectile factory. Defaults to the shared spawner; tests swap it to avoid needing game data.</summary>
    public Action<List<Projectile>, int, WorldEntity, WorldEntity, int> ProjectileCreator { get; set; } =
        EffectSpawner.CreateProjectile;

    public CreatureRenderer? CreatureRenderer { get; set; }
    public AislingRenderer? AislingRenderer { get; set; }

    public WorldEntity? GetEntity(uint id) => EntitiesById.GetValueOrDefault(id);

    public WorldEntity? Target => EntitiesById.GetValueOrDefault(TargetId);

    public void Apply(ScryFrameArgs frame)
    {
        if (frame.Kind == ScryFrameKind.Events)
            return;

        MsSinceFrame = 0;

        if (frame.Kind == ScryFrameKind.None)
        {
            Clear();

            return;
        }

        if ((frame.Kind == ScryFrameKind.Positions) && !HasView)
            return;

        if ((frame.MapId != MapId) || (frame.Width != Width) || (frame.Height != Height) || (frame.CheckSum != CheckSum))
        {
            EntitiesById.Clear();
            Effects.Clear();
            Projectiles.Clear();
            MapId = frame.MapId;
            Width = frame.Width;
            Height = frame.Height;
            CheckSum = frame.CheckSum;
            MapChanged?.Invoke();
        }

        if (frame.TargetId != TargetId)
        {
            TargetId = frame.TargetId;
            TargetChanged?.Invoke();
            Effects.Clear();
            Projectiles.Clear();
        }

        HasView = true;

        var present = new HashSet<uint>();

        foreach (var entity in frame.Entities)
        {
            present.Add(entity.Id);
            ApplyEntity(entity);
        }

        if (frame.Kind == ScryFrameKind.Full)
        {
            foreach (var id in EntitiesById.Keys.Where(id => !present.Contains(id)).ToList())
                EntitiesById.Remove(id);
        } else
            foreach (var id in frame.Removed)
                EntitiesById.Remove(id);
    }

    public void ApplyEvents(ScryFrameArgs frame)
    {
        if (!HasView || (frame.TargetId != TargetId))
            return;

        foreach (var entry in frame.Events)
            if (entry.Kind == ScryEventKind.Body)
                ApplyBodyEvent(entry);
            else
                ApplyAnimationEvent(entry);
    }

    private void ApplyBodyEvent(ScryEventEntry entry)
    {
        if ((entry.SourceId == 0) || !EntitiesById.TryGetValue(entry.SourceId, out var entity))
            return;

        BodyAnimationPlayer.Start(
            entity,
            new BodyAnimationArgs { SourceId = entry.SourceId, BodyAnimation = entry.BodyAnimation, AnimationSpeed = entry.Speed },
            hidden: false,
            CreatureRenderer,
            AislingRenderer);
    }

    private void ApplyAnimationEvent(ScryEventEntry entry)
    {
        //projectile: both ends present and a meffect id in the target slot
        if ((entry.SourceId != 0)
            && (entry.TargetId != 0)
            && (entry.TargetAnimation >= PROJECTILE_ANIMATION_BASE)
            && (entry.TargetAnimation < PROJECTILE_ANIMATION_MAX_EXCLUSIVE))
        {
            var meffectId = entry.TargetAnimation - PROJECTILE_ANIMATION_BASE;

            if (EntitiesById.TryGetValue(entry.SourceId, out var source)
                && EntitiesById.TryGetValue(entry.TargetId, out var target))
            ProjectileCreator(Projectiles, meffectId, source, target, Height);

            if (entry.SourceAnimation > 0)
                CreateSceneEffect(entry.SourceAnimation, entry.Speed, targetEntityId: entry.SourceId);

            return;
        }

        //ground-targeted effect
        if ((entry.TargetAnimation > 0) && entry.PointX.HasValue && entry.PointY.HasValue)
            CreateSceneEffect(entry.TargetAnimation, entry.Speed, tileX: entry.PointX.Value, tileY: entry.PointY.Value, isBackground: entry.IsBackground);

        //entity-targeted effect on target
        if ((entry.TargetId != 0) && (entry.TargetAnimation > 0))
            CreateSceneEffect(entry.TargetAnimation, entry.Speed, targetEntityId: entry.TargetId, isBackground: entry.IsBackground);

        //source-side effect (caster visual)
        if ((entry.SourceId != 0) && (entry.SourceAnimation > 0))
            CreateSceneEffect(entry.SourceAnimation, entry.Speed, targetEntityId: entry.SourceId);
    }

    private void CreateSceneEffect(
        int effectId,
        ushort speed,
        uint? targetEntityId = null,
        int? tileX = null,
        int? tileY = null,
        bool isBackground = false)
    {
        var info = EffectInfoSource(effectId);

        if (info is null)
            return;

        EffectSpawner.CreateEffect(Effects, effectId, speed, info.Value, targetEntityId, tileX, tileY, isBackground);
    }

    public void Update(double elapsedMs)
    {
        MsSinceFrame += elapsedMs;

        if (HasView && (MsSinceFrame > STALE_AFTER_MS))
        {
            Clear();

            return;
        }

        foreach (var entity in EntitiesById.Values)
        {
            AnimationSystem.Advance(entity, (float)elapsedMs);
            AnimationSystem.TickEmotes(entity, (float)elapsedMs);
        }

        EffectSpawner.Advance(Effects, Projectiles, (float)elapsedMs, GetEntity);
    }

    public void Clear()
    {
        EntitiesById.Clear();
        Effects.Clear();
        Projectiles.Clear();
        HasView = false;
        MapId = -1;
        Width = 0;
        Height = 0;
        CheckSum = 0;
        TargetId = 0;
    }

    public static Direction StepDirection(int dx, int dy)
        => (dx, dy) switch
        {
            (0, -1) => Direction.Up,
            (1, 0) => Direction.Right,
            (0, 1) => Direction.Down,
            _ => Direction.Left
        };

    private void ApplyEntity(ScryEntity scry)
    {
        if (!EntitiesById.TryGetValue(scry.Id, out var entity))
        {
            if (!scry.HasLook)
                return;

            entity = new WorldEntity { Id = scry.Id };
            ApplyLook(entity, scry);
            EntitiesById[scry.Id] = entity;

            return;
        }

        var moved = (entity.TileX != scry.X) || (entity.TileY != scry.Y);

        if (scry.HasLook && !moved && (entity.AnimState != EntityAnimState.Walking))
        {
            ApplyLook(entity, scry);

            return;
        }

        MoveTo(entity, scry.X, scry.Y, scry.Direction);
    }

    private static void ApplyLook(WorldEntity entity, ScryEntity scry)
    {
        if (scry.Aisling is { } aisling)
            WorldState.ApplyAislingLook(entity, aisling);
        else if (scry.Creature is { } creature)
            WorldState.ApplyVisibleEntityLook(entity, creature);
        else if (scry.GroundItem is { } item)
            WorldState.ApplyVisibleEntityLook(entity, item);

        entity.TileX = scry.X;
        entity.TileY = scry.Y;
        entity.Direction = scry.Direction;
    }

    private static void MoveTo(WorldEntity entity, int x, int y, Direction facing)
    {
        var dx = x - entity.TileX;
        var dy = y - entity.TileY;

        if ((dx == 0) && (dy == 0))
        {
            entity.Direction = facing;

            return;
        }

        entity.TileX = x;
        entity.TileY = y;

        if (Math.Abs(dx) + Math.Abs(dy) == 1)
        {
            var step = StepDirection(dx, dy);
            entity.Direction = step;
            AnimationSystem.StartWalk(entity, step, entity.UsesCreatureWalkTiming);

            return;
        }

        entity.Direction = facing;
        AnimationSystem.CancelAllAnimations(entity);
    }
}
