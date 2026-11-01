#region
using Chaos.Client.Collections;
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

    public WorldEntity? Target => EntitiesById.GetValueOrDefault(TargetId);

    public void Apply(ScryFrameArgs frame)
    {
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

    public void Update(double elapsedMs)
    {
        MsSinceFrame += elapsedMs;

        if (HasView && (MsSinceFrame > STALE_AFTER_MS))
        {
            Clear();

            return;
        }

        foreach (var entity in EntitiesById.Values)
            AnimationSystem.Advance(entity, (float)elapsedMs);
    }

    public void Clear()
    {
        EntitiesById.Clear();
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
