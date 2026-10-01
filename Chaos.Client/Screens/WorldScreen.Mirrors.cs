#region
using Chaos.Client.Collections;
using Chaos.Client.Models;
using Chaos.Client.Rendering;
using Chaos.Client.Systems;
using Chaos.DarkAges.Definitions;
using Chaos.Geometry.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Screens;

/// <summary>
///     Mirrors: the layout and doubles from the server, and their drawing. Before the world pass,
///     <see cref="PreRenderMirrors" /> paints the characters the mirrors need into the atlas and composes all visible glass
///     into the layer; during the stripe pass, <see cref="DrawMirrorTile" /> pastes each wall tile's slice and frame.
/// </summary>
public sealed partial class WorldScreen
{
    private static readonly Color GlassTint = new(190, 205, 255);
    private static readonly Color FunhouseTallTint = new(255, 200, 235);
    private static readonly Color FunhouseWideTint = new(200, 255, 225);
    private static readonly Color FunhouseWaveTint = new(230, 210, 255);
    private static readonly Color HauntedTint = new(185, 225, 215);
    private static readonly Color GhostTint = new(170, 255, 190);
    private static readonly Color WindowTint = new(220, 220, 235);

    private static readonly Color DoubleTint = new(200, 230, 255);
    private const float DARK_STRETCH_OPACITY = 0.95f;

    private readonly List<(ulong Key, Vector2 Tile, float Alpha)> MirrorDoubleDraws = [];
    private readonly List<WorldEntity> MirrorCandidates = [];
    private readonly Dictionary<ulong, WorldEntity> MirrorCellEntities = [];
    private readonly List<MirrorPlacement> MirrorPlacements = [];
    private readonly EntityTrail MirrorTrail = new();
    private readonly List<int> VisibleMirrorSegments = [];
    private MirrorRenderer MirrorRenderer = null!;

    private void WireMirrors()
    {
        Game.Connection.OnMirrorLayout += HandleMirrorLayout;
        Game.Connection.OnMirrorDouble += HandleMirrorDouble;
    }

    private void UnwireMirrors()
    {
        Game.Connection.OnMirrorLayout -= HandleMirrorLayout;
        Game.Connection.OnMirrorDouble -= HandleMirrorDouble;
    }

    private static void HandleMirrorLayout(MirrorLayoutArgs args) => WorldState.Mirrors.Apply(args);

    private static void HandleMirrorDouble(MirrorDoubleArgs args)
        => WorldState.Mirrors.AddDouble(args.EntityId, args.SegmentIndex, Environment.TickCount64);

    /// <summary>Forgets the mirrors. Called on a real map change only; maps without mirrors send no layout at all.</summary>
    private void ResetMirrors()
    {
        WorldState.Mirrors.Clear();
        MirrorTrail.Clear();
    }

    private static Vector2 EntityTile(WorldEntity entity) => new Vector2(entity.TileX, entity.TileY) + MirrorMath.OffsetToTiles(entity.VisualOffset);

    private static ulong MirrorCellKey(uint entityId, Direction facing, bool idle)
        => ((ulong)entityId << 8) | ((ulong)facing << 1) | (idle ? 1UL : 0UL);

    /// <summary>Screen-local top-left of a face's 28x89 canvas, on whole pixels so the layer slice and frame line up.</summary>
    private Vector2 MirrorFaceOrigin(Point wall, MirrorSide side)
    {
        var tile = Camera.WorldToScreen(Camera.TileToWorld(wall.X, wall.Y, MapFile!.Height));

        return new Vector2(
            MathF.Floor(tile.X) + MirrorGeometry.FaceLocalX(side),
            MathF.Floor(tile.Y) + MirrorGeometry.CANVAS_TOP);
    }

    /// <summary>Once a frame, before the world pass: records positions, places reflections, paints the atlas and the layer.</summary>
    private void PreRenderMirrors(IReadOnlyList<WorldEntity> sortedEntities)
    {
        var mirrors = WorldState.Mirrors;

        if (!mirrors.HasMirrors || MapFile is null)
            return;

        var nowMs = Environment.TickCount64;
        var seconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;

        MirrorCandidates.Clear();

        foreach (var entity in sortedEntities)
            if (entity.Type is ClientEntityType.Aisling or ClientEntityType.Creature && !entity.IsHidden)
            {
                MirrorCandidates.Add(entity);
                MirrorTrail.Record(entity.Id, nowMs, EntityTile(entity), entity.Direction);
            }

        MirrorTrail.Prune(nowMs);
        CollectVisibleMirrorSegments();
        CollectMirrorPlacements(nowMs, seconds);

        MirrorRenderer.BeginFrame();
        MirrorCellEntities.Clear();

        foreach (var placement in MirrorPlacements)
            ReserveMirrorCell(placement.EntityId, placement.Facing, placement.Idle);

        CollectMirrorDoubles(nowMs);

        MirrorRenderer.RenderAtlas(PaintMirrorCells);
        MirrorRenderer.RenderLayer(DrawMirrorGlass, batch => DrawMirrorReflections(batch, seconds));
    }

    private void CollectVisibleMirrorSegments()
    {
        VisibleMirrorSegments.Clear();

        (var minX, var minY, var maxX, var maxY) = Camera.GetVisibleTileBounds(MapFile!.Width, MapFile.Height, MapRenderer.ForegroundExtraMargin);
        var segments = WorldState.Mirrors.Segments;

        for (var i = 0; i < segments.Count; i++)
            for (var k = 0; k < segments[i].Length; k++)
            {
                var wall = MirrorMath.WallTile(segments[i], k);

                if ((wall.X >= minX - 2) && (wall.X <= maxX + 2) && (wall.Y >= minY - 2) && (wall.Y <= maxY + 2))
                {
                    VisibleMirrorSegments.Add(i);

                    break;
                }
            }
    }

    private void CollectMirrorPlacements(long nowMs, double seconds)
    {
        MirrorPlacements.Clear();

        var player = WorldState.GetPlayerEntity();
        var centre = player is null ? Vector2.Zero : EntityTile(player);
        var segments = WorldState.Mirrors.Segments;

        void Add(WorldEntity entity, Direction facing, bool idle, Vector2 tile, float scaleX, float scaleY, bool ripple, Color tint, float alpha)
            => MirrorPlacements.Add(
                new MirrorPlacement(
                    entity.Id,
                    facing,
                    idle,
                    tile,
                    scaleX,
                    scaleY,
                    ripple,
                    tint,
                    alpha,
                    Vector2.Distance(EntityTile(entity), centre)));

        foreach (var index in VisibleMirrorSegments)
        {
            var segment = segments[index];

            if (segment.Style == MirrorStyle.Window)
            {
                if (segment.PartnerIndex >= segments.Count)
                    continue;

                var partner = segments[segment.PartnerIndex];

                foreach (var entity in MirrorCandidates)
                    if (MirrorMath.IsInFront(partner, entity.TileX, entity.TileY, MirrorMath.REFLECT_DEPTH, MirrorMath.REFLECT_MARGIN))
                        Add(
                            entity,
                            MirrorMath.ReflectFacing(partner.Side, entity.Direction),
                            false,
                            MirrorMath.WindowPoint(segment, partner, EntityTile(entity)),
                            1,
                            1,
                            false,
                            WindowTint,
                            MirrorMath.WINDOW_ALPHA);

                continue;
            }

            var slip = segment.Style == MirrorStyle.Haunted ? MirrorMath.HauntedSlipAt(segment.Id, seconds) : (HauntedSlip.None, 0d);

            foreach (var entity in MirrorCandidates)
            {
                if (!MirrorMath.IsInFront(segment, entity.TileX, entity.TileY, MirrorMath.REFLECT_DEPTH, MirrorMath.REFLECT_MARGIN))
                    continue;

                var tile = EntityTile(entity);
                var facing = MirrorMath.ReflectFacing(segment.Side, entity.Direction);

                switch (segment.Style)
                {
                    case MirrorStyle.Funhouse:
                    {
                        (var sx, var sy) = MirrorMath.FunhouseScale(segment.Funhouse);

                        var tint = segment.Funhouse switch
                        {
                            MirrorFunhouse.Tall => FunhouseTallTint,
                            MirrorFunhouse.Wide => FunhouseWideTint,
                            _                   => FunhouseWaveTint
                        };

                        Add(entity, facing, false, MirrorMath.ReflectPoint(segment, tile), sx, sy, true, tint, MirrorMath.GLASS_ALPHA);

                        break;
                    }
                    case MirrorStyle.Endless:
                    {
                        var reflected = MirrorMath.ReflectPoint(segment, tile);

                        for (var copy = 0; copy < MirrorMath.ENDLESS_COPIES; copy++)
                        {
                            var scale = MirrorMath.EndlessScale(copy);

                            Add(
                                entity,
                                MirrorMath.EndlessFacing(copy, entity.Direction, facing),
                                false,
                                MirrorMath.EndlessCopy(segment.Side, reflected, copy),
                                scale,
                                scale,
                                false,
                                GlassTint,
                                MirrorMath.EndlessAlpha(copy));
                        }

                        break;
                    }
                    case MirrorStyle.Haunted:
                    {
                        var at = tile;
                        var idle = false;
                        var tint = HauntedTint;
                        var alpha = MirrorMath.GLASS_ALPHA;

                        switch (slip.Item1)
                        {
                            case HauntedSlip.Lag when MirrorTrail.TryGet(
                                entity.Id,
                                nowMs - (long)(MirrorMath.HAUNTED_LAG_SECONDS * 1000),
                                out var past,
                                out var pastFacing):
                                at = past;
                                facing = MirrorMath.ReflectFacing(segment.Side, pastFacing);

                                break;
                            case HauntedSlip.Stare:
                                if (MirrorTrail.TryGet(entity.Id, nowMs - (long)(slip.Item2 * 1000), out var then, out _))
                                    at = then;

                                facing = MirrorMath.FacingOutOf(segment.Side);
                                idle = true;

                                break;
                            case HauntedSlip.Ghost:
                                tint = GhostTint;
                                alpha = MirrorMath.GhostAlpha(seconds);

                                break;
                        }

                        Add(entity, facing, idle, MirrorMath.ReflectPoint(segment, at), 1, 1, false, tint, alpha);

                        break;
                    }
                    default:
                        Add(entity, facing, false, MirrorMath.ReflectPoint(segment, tile), 1, 1, false, GlassTint, MirrorMath.GLASS_ALPHA);

                        break;
                }
            }
        }

        //the cap keeps the nearest; then paint far to near so nearer reflections overlap farther ones
        if (MirrorPlacements.Count > MirrorMath.SPRITE_CAP)
        {
            MirrorPlacements.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            MirrorPlacements.RemoveRange(MirrorMath.SPRITE_CAP, MirrorPlacements.Count - MirrorMath.SPRITE_CAP);
        }

        MirrorPlacements.Sort((a, b) => (a.Tile.X + a.Tile.Y).CompareTo(b.Tile.X + b.Tile.Y));
    }

    /// <summary>Works out where each double is now and reserves its atlas cell. Drawn later by <see cref="DrawMirrorDoubles" />.</summary>
    private void CollectMirrorDoubles(long nowMs)
    {
        MirrorDoubleDraws.Clear();

        var mirrors = WorldState.Mirrors;
        mirrors.PruneDoubles(nowMs);

        foreach (var d in mirrors.Doubles.ToList())
        {
            var owner = WorldState.GetEntity(d.EntityId);

            if (owner is null)
            {
                mirrors.RemoveDoublesOf(d.EntityId);

                continue;
            }

            (var phase, var progress, var alpha) = MirrorMath.DoubleAt((nowMs - d.StartMs) / 1000.0);

            if (phase == DoublePhase.Done)
                continue;

            var segment = mirrors.Segments[d.SegmentIndex];

            if (!MirrorTrail.TryGet(owner.Id, nowMs - (long)(MirrorMath.DOUBLE_LAG_SECONDS * 1000), out var follow, out var followFacing))
            {
                follow = EntityTile(owner);
                followFacing = owner.Direction;
            }

            var tile = follow;
            var facing = followFacing;

            if (phase == DoublePhase.Climbing)
            {
                if (!MirrorTrail.TryGet(owner.Id, d.StartMs, out var startTile, out _))
                    startTile = EntityTile(owner);

                tile = Vector2.Lerp(MirrorMath.ReflectPoint(segment, startTile), follow, progress);
                facing = MirrorMath.FacingOutOf(segment.Side);
            }

            ReserveMirrorCell(owner.Id, facing, false);
            MirrorDoubleDraws.Add((MirrorCellKey(owner.Id, facing, false), tile, alpha));
        }
    }

    /// <summary>After the silhouettes: the doubles, faint and pale, over the world.</summary>
    private void DrawMirrorDoubles(BatchBlendScope scope)
    {
        var atlas = MirrorRenderer.AtlasTexture;

        if ((MirrorDoubleDraws.Count == 0) || atlas is null || !WorldState.Mirrors.HasMirrors)
            return;

        scope.Require(BlendState.AlphaBlend);
        var anchor = new Vector2(MirrorRenderer.CELL_ANCHOR_X, MirrorRenderer.CELL_ANCHOR_Y);

        foreach ((var key, var tile, var alpha) in MirrorDoubleDraws)
        {
            if (!MirrorRenderer.TryGetCell(key, out var cell))
                continue;

            var feet = Camera.WorldToScreen(MirrorMath.TileCenterWorld(tile, MapFile!.Height));
            scope.Batch.Draw(atlas, feet, cell, DoubleTint * alpha, 0f, anchor, 1f, SpriteEffects.None, 0f);
        }
    }

    /// <summary>
    ///     After the doubles: each dark stretch (and one tile north and west of it, where heads and walls rise) nearly
    ///     black, a warm glow at each player's feet inside, then the stretch's mirrors again at full brightness.
    /// </summary>
    private void DrawDarkStretches(BatchBlendScope scope)
    {
        var mirrors = WorldState.Mirrors;

        if ((mirrors.DarkStretches.Count == 0) || MapFile is null)
            return;

        scope.Require(BlendState.AlphaBlend);
        var shade = Color.Black * DARK_STRETCH_OPACITY;

        foreach (var s in mirrors.DarkStretches)
            for (var y = s.Y - 1; y < s.Y + s.Height; y++)
                for (var x = s.X - 1; x < s.X + s.Width; x++)
                {
                    if ((x < 0) || (y < 0) || (x >= MapFile.Width) || (y >= MapFile.Height))
                        continue;

                    scope.Batch.Draw(MirrorRenderer.Diamond, Camera.WorldToScreen(Camera.TileToWorld(x, y, MapFile.Height)), shade);
                }

        scope.Require(BlendState.Additive);
        var glowOrigin = new Vector2(32, 16);

        foreach (var entity in MirrorCandidates)
            if ((entity.Type == ClientEntityType.Aisling) && mirrors.IsInDarkStretch(entity.TileX, entity.TileY))
                scope.Batch.Draw(
                    MirrorRenderer.Glow,
                    Camera.WorldToScreen(MirrorMath.TileCenterWorld(EntityTile(entity), MapFile.Height)),
                    null,
                    new Color(90, 70, 50) * 0.6f,
                    0f,
                    glowOrigin,
                    1f,
                    SpriteEffects.None,
                    0f);

        scope.Require(BlendState.AlphaBlend);

        if (!MirrorRenderer.LayerReady)
            return;

        foreach (var index in VisibleMirrorSegments)
        {
            var segment = mirrors.Segments[index];

            for (var k = 0; k < segment.Length; k++)
            {
                var wall = MirrorMath.WallTile(segment, k);
                var front = segment.Side == MirrorSide.North ? new Point(wall.X, wall.Y + 1) : new Point(wall.X + 1, wall.Y);

                if (mirrors.IsInDarkStretch(front.X, front.Y))
                    DrawMirrorFace(scope.Batch, wall, segment.Side);
            }
        }
    }

    private void ReserveMirrorCell(uint entityId, Direction facing, bool idle)
    {
        var key = MirrorCellKey(entityId, facing, idle);

        if (!MirrorRenderer.TryReserveCell(key, out _, out var isNew) || !isNew)
            return;

        var entity = WorldState.GetEntity(entityId);

        if (entity is null)
            return;

        var copy = entity.CopyForMirror(MirrorMath.MirrorCacheId(entityId, facing, idle));
        copy.Direction = facing;
        copy.VisualOffset = Vector2.Zero;

        if (idle)
        {
            copy.AnimState = EntityAnimState.Idle;
            copy.ActiveBodyAnimation = null;
        }

        MirrorCellEntities[key] = copy;
    }

    private void PaintMirrorCells()
    {
        foreach ((var key, var copy) in MirrorCellEntities)
        {
            if (!MirrorRenderer.TryGetCell(key, out var cell))
                continue;

            var feet = Camera.WorldToScreen(
                Camera.TileToWorld(copy.TileX, copy.TileY, MapFile!.Height)
                + new Vector2(DaLibConstants.HALF_TILE_WIDTH, DaLibConstants.HALF_TILE_HEIGHT));

            MirrorRenderer.PaintCell(cell, feet, batch => DrawEntity(batch, copy));
        }
    }

    private void DrawMirrorGlass(SpriteBatch batch)
    {
        var segments = WorldState.Mirrors.Segments;

        foreach (var index in VisibleMirrorSegments)
        {
            var segment = segments[index];

            var colour = segment.Style switch
            {
                MirrorStyle.Funhouse => new Color(74, 58, 96),
                MirrorStyle.Haunted  => new Color(47, 64, 64),
                MirrorStyle.Endless  => new Color(52, 66, 100),
                MirrorStyle.Window   => new Color(60, 60, 70),
                _                    => new Color(58, 70, 96)
            };

            var glass = MirrorRenderer.Glass(segment.Side);

            for (var k = 0; k < segment.Length; k++)
                batch.Draw(glass, MirrorFaceOrigin(MirrorMath.WallTile(segment, k), segment.Side), colour);
        }
    }

    private void DrawMirrorReflections(SpriteBatch batch, double seconds)
    {
        var atlas = MirrorRenderer.AtlasTexture;

        if (atlas is null)
            return;

        var anchor = new Vector2(MirrorRenderer.CELL_ANCHOR_X, MirrorRenderer.CELL_ANCHOR_Y);

        foreach (var p in MirrorPlacements)
        {
            if (!MirrorRenderer.TryGetCell(MirrorCellKey(p.EntityId, p.Facing, p.Idle), out var cell))
                continue;

            var feet = Camera.WorldToScreen(MirrorMath.TileCenterWorld(p.Tile, MapFile!.Height));
            var colour = p.Tint * p.Alpha;

            if (!p.Ripple)
            {
                batch.Draw(atlas, feet, cell, colour, 0f, anchor, new Vector2(p.ScaleX, p.ScaleY), SpriteEffects.None, 0f);

                continue;
            }

            for (var row = 0; row < MirrorRenderer.CELL_SIZE; row += 2)
            {
                var source = new Rectangle(cell.X, cell.Y + row, MirrorRenderer.CELL_SIZE, 2);

                var at = new Vector2(
                    feet.X - MirrorRenderer.CELL_ANCHOR_X * p.ScaleX + MirrorMath.RippleOffset(seconds, row),
                    feet.Y + (row - MirrorRenderer.CELL_ANCHOR_Y) * p.ScaleY);

                batch.Draw(atlas, at, source, colour, 0f, Vector2.Zero, new Vector2(p.ScaleX, p.ScaleY), SpriteEffects.None, 0f);
            }
        }

        //glints: a faint streak sweeping along each glass, endless or window run
        var segments = WorldState.Mirrors.Segments;

        foreach (var index in VisibleMirrorSegments)
        {
            var segment = segments[index];

            if (segment.Style is not (MirrorStyle.Glass or MirrorStyle.Endless or MirrorStyle.Window))
                continue;

            var fraction = MirrorMath.GlintFraction(seconds + MirrorMath.Fnv1a(segment.Id) % 9);

            if (fraction is null)
                continue;

            var step = segment.Side == MirrorSide.North ? new Vector2(28, 14) : new Vector2(-28, 14);
            var start = MirrorFaceOrigin(MirrorMath.WallTile(segment, 0), segment.Side) + new Vector2(14, 82);
            var at = start + step * (fraction.Value * segment.Length);

            batch.Draw(MirrorRenderer.Pixel, at, null, Color.White * 0.18f, 0.35f, new Vector2(0.5f, 1f), new Vector2(3, 70), SpriteEffects.None, 0f);
        }
    }

    /// <summary>During the stripe pass, right after wall tile (x, y)'s foreground: its faces' glass slices and frames.</summary>
    private void DrawMirrorTile(BatchBlendScope scope, int x, int y)
    {
        var faces = WorldState.Mirrors.FacesAt(x, y);

        if (faces.Count == 0)
            return;

        scope.Require(BlendState.AlphaBlend);

        foreach (var face in faces)
            DrawMirrorFace(scope.Batch, new Point(x, y), face.Side);
    }

    private void DrawMirrorFace(SpriteBatch batch, Point wall, MirrorSide side)
    {
        var layer = MirrorRenderer.LayerTexture;

        if (layer is null)
            return;

        var origin = MirrorFaceOrigin(wall, side);
        var source = new Rectangle((int)origin.X, (int)origin.Y, MirrorGeometry.FACE_WIDTH, MirrorGeometry.CANVAS_HEIGHT);
        var clipped = Rectangle.Intersect(source, layer.Bounds);

        if (clipped.Width > 0 && clipped.Height > 0)
            batch.Draw(layer, new Vector2(clipped.X, clipped.Y), clipped, Color.White);

        var ui = UiRenderer.Instance;
        var frame = ui?.GetSpfTexture(side == MirrorSide.North ? "mirpnl01.spf" : "mirpnl02.spf");

        if (frame is not null && !ReferenceEquals(frame, ui!.MissingTexture))
            batch.Draw(frame, origin, Color.White);
    }

    private readonly record struct MirrorPlacement(
        uint EntityId,
        Direction Facing,
        bool Idle,
        Vector2 Tile,
        float ScaleX,
        float ScaleY,
        bool Ripple,
        Color Tint,
        float Alpha,
        float Distance);
}
