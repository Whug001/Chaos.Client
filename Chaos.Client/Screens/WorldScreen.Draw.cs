#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Generic;
using Chaos.Client.Controls.World.Hud.Panel;
using Chaos.Client.Data;
using Chaos.Client.Models;
using Chaos.Client.Rendering.Models;
using Chaos.Client.Rendering.Utility;
using Chaos.Client.Systems;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Screens;

public sealed partial class WorldScreen
{
    public void Draw(SpriteBatch spriteBatch, GameTime gameTime)
    {
        //sort once per frame — cached via dirty flag, reused by all draw sub-passes
        var sortedEntities = WorldState.CurrentFrame.SortedEntities;

        //pre-render silhouettes of ALL visible entities (items, monsters, merchants, aislings) before world
        //drawing. each entity is redrawn at 50% over the finished world: invisible in the open (self-blend over
        //identical pixels) but shown through where a foreground tile occluded it. transparent entities compound
        //multiplicatively (~50% open, ~25% behind walls). extends retail's local-player overdraw (FUN_005d4360).
        if (MapFile is not null && MapPreloaded)
        {
            SilhouetteRenderer.Clear();

            //feed every visible entity in the stripe's paint order, EXCEPT the local player which is appended last
            //so its silhouette overdraws on top of all other entities (restores retail's always-see-yourself
            //behavior). order matches the world pass so non-player silhouettes self-blend to a no-op in the open.
            CollectSilhouetteEntities(sortedEntities);

            //pre-render silhouettes into a screen-sized rt (must happen before main rt drawing starts,
            //because rt switching discards the main rt's contents). DrawingForSilhouette routes transparent
            //entities through TRANSPARENT_SILHOUETTE_ALPHA so they compound correctly with the overlay.
            SilhouetteRenderer.PreRenderSilhouettes(batch =>
            {
                batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, GlobalSettings.Sampler);
                DrawingForSilhouette = true;

                try
                {
                    foreach (var entityId in SilhouetteRenderer.SilhouetteEntityIds)
                    {
                        var silEntity = WorldState.GetEntity(entityId);

                        if (silEntity is not null)
                            DrawEntity(batch, silEntity);
                    }
                } finally
                {
                    DrawingForSilhouette = false;
                    batch.End();
                }
            });
        }

        //pass 1: world rendering — clipped to the hud viewport area, camera transform
        if (MapFile is not null && MapPreloaded)
        {
            var viewportRect = WorldHud.ViewportBounds;
            Device.ScissorRectangle = viewportRect;

            var transform = Matrix.CreateTranslation(viewportRect.X, viewportRect.Y, 0);

            //background tiles + tile cursor: batched (many draws, no blend changes)
            spriteBatch.Begin(samplerState: GlobalSettings.Sampler, rasterizerState: ScissorRasterizerState, transformMatrix: transform);

            MapRenderer.DrawBackground(
                spriteBatch,
                MapFile,
                Camera,
                AnimationTick);
            DrawTileCursor(spriteBatch);
            spriteBatch.End();

            //background animations: drawn above the background tiles, beneath all entities/foreground.
            //use a BatchBlendScope (not a plain batch) so additive/screen effects can switch blend via Require.
            BlendScope.Begin(spriteBatch, BlendState.AlphaBlend, GlobalSettings.Sampler, ScissorRasterizerState, transform);

            try
            {
                DrawBackgroundEffects(BlendScope);
            } finally
            {
                BlendScope.End();
            }

            //foreground, entities, effects: batched (Deferred) by default; BatchBlendScope breaks the batch only for the
            //non-AlphaBlend draws (additive/screen effects, screen-blend foreground tiles). try/finally guarantees the
            //batch is always closed — leaving it open would make next frame's Begin throw and cascade every frame.
            BlendScope.Begin(spriteBatch, BlendState.AlphaBlend, GlobalSettings.Sampler, ScissorRasterizerState, transform);

            try
            {
                DrawForegroundAndEntities(BlendScope, sortedEntities);

                //effects/foreground tiles each restore AlphaBlend, but make the contract explicit before the silhouette
                //overlay so it always composites at AlphaBlend regardless of which blend the last stripe draw left.
                BlendScope.Require(BlendState.AlphaBlend);
                SilhouetteRenderer.DrawSilhouettes(BlendScope.Batch);
            } finally
            {
                BlendScope.End();
            }

            //ground-target outline — drawn after foreground/entities so a wall/tree on the target tile can't occlude
            //the targeting indicator. camera-space (same transform as the world passes). guarded to skip the batch
            //setup when not targeting.
            if (IsAimingAtTile)
            {
                spriteBatch.Begin(samplerState: GlobalSettings.Sampler, rasterizerState: ScissorRasterizerState, transformMatrix: transform);
                DrawGroundTargetHighlight(spriteBatch);
                spriteBatch.End();
            }

            //darkness overlay — drawn over the world in screen space (no camera transform)
            if (DarknessRenderer.IsActive)
            {
                spriteBatch.Begin(
                    blendState: BlendState.NonPremultiplied,
                    samplerState: GlobalSettings.Sampler,
                    rasterizerState: ScissorRasterizerState);
                var viewport = WorldHud.ViewportBounds;
                DarknessRenderer.Draw(spriteBatch, viewport);
                spriteBatch.End();
            }

            //weather overlay — drawn after darkness so snowflakes/rain remain visible on dark maps
            if (WeatherRenderer.IsActive)
            {
                spriteBatch.Begin(
                    blendState: BlendState.AlphaBlend,
                    samplerState: GlobalSettings.Sampler,
                    rasterizerState: ScissorRasterizerState);
                var weatherViewport = WorldHud.ViewportBounds;
                WeatherRenderer.Draw(spriteBatch, weatherViewport);
                spriteBatch.End();
            }

            //blind overlay — black out viewport, then redraw only the player character. drawn before
            //entity overlays so chat bubbles, name tags, chant text, etc. remain visible while blinded,
            //matching retail (which implements blind as a per-entity darkness mask rather than a
            //viewport fill, so its independent overlay panes are unaffected).
            if (WorldState.Attributes.Current?.Blind is true)
            {
                spriteBatch.Begin(
                    blendState: BlendState.AlphaBlend,
                    samplerState: GlobalSettings.Sampler,
                    rasterizerState: ScissorRasterizerState);
                RenderHelper.DrawRect(spriteBatch, WorldHud.ViewportBounds, Color.Black);
                spriteBatch.End();

                var player = WorldState.GetPlayerEntity();

                if (player is not null)
                {
                    spriteBatch.Begin(
                        SpriteSortMode.Deferred,
                        BlendState.AlphaBlend,
                        GlobalSettings.Sampler,
                        null,
                        ScissorRasterizerState,
                        null,
                        transform);
                    DrawEntity(spriteBatch, player);
                    spriteBatch.End();
                }
            }

            //entity overlays (chat bubbles, health bars, name tags, chant text) — drawn after darkness
            //so light level doesn't tint them, and after blind so they remain visible while blinded
            spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                GlobalSettings.Sampler,
                null,
                ScissorRasterizerState,
                null,
                transform);

            Overlays.Draw(spriteBatch, Camera, MapFile.Height);
            spriteBatch.End();

            //snapshot draw count before debug draws so the reported count excludes debug visualizations
            DebugOverlay.SnapshotDrawCount();

            //debug overlay: entity hitboxes, tile grid, etc.
            if (DebugOverlay.IsActive)
            {
                spriteBatch.Begin(
                    SpriteSortMode.Deferred,
                    BlendState.AlphaBlend,
                    GlobalSettings.Sampler,
                    null,
                    ScissorRasterizerState,
                    null,
                    transform);

                DebugRenderer.Draw(
                    spriteBatch,
                    Camera,
                    MapFile,
                    MapRenderer.ForegroundExtraMargin,
                    sortedEntities,
                    WorldState.GetPlayerEntity(),
                    EntityHitBoxes,
                    WorldState.CurrentFrame.HoveredTile);
                spriteBatch.End();
            }
        }

        //tab map overlay — drawn on top of world, under hud
        //tabmaprenderer manages its own spritebatch begin/end blocks (stencil passes for entity overlap)
        //NoTabMap map flag (0x40) suppresses both the toggle (InputHandlers) and the render
        if (TabMapVisible && MapFile is not null && !CurrentMapFlags.HasFlag(MapFlags.NoTabMap))
        {
            var player = WorldState.GetPlayerEntity();

            //no player → no tab map this frame (avoids stamping baseline at (0,0) during transitions)
            if (player is not null)
            {
                var viewport = WorldHud.ViewportBounds;
                var px = player.TileX;
                var py = player.TileY;

                var sourceCount = sortedEntities.Count;

                if (TabMapEntities.Length < sourceCount)
                    TabMapEntities = new TabMapEntity[sourceCount];

                var entityCount = 0;

                for (var i = 0; i < sourceCount; i++)
                {
                    var e = sortedEntities[i];

                    if (e.IsHidden)
                        continue;

                    TabMapEntities[entityCount++] = new TabMapEntity(
                        e.TileX,
                        e.TileY,
                        e.Type,
                        e.Id,
                        e.CreatureType);
                }

                TabMapRenderer.Draw(
                    spriteBatch,
                    Device,
                    viewport,
                    px,
                    py,
                    TabMapEntities,
                    entityCount,
                    WorldState.PlayerEntityId,
                    DarknessRenderer.IsFullBlackDark,
                    Lighting.Sources,
                    LightingSystem.BaselineVisibilityOffsets);
            }
        }

        //pass 2: ui overlay — full screen, no transform
        spriteBatch.Begin(samplerState: GlobalSettings.Sampler);
        Root!.Draw(spriteBatch);
        DrawDragIcon(spriteBatch);
        spriteBatch.End();
    }

    #region Swimming
    /// <summary>
    ///     Updates the entity's water tile state from the current map tile's gndattr data.
    /// </summary>
    private void UpdateEntityWaterState(WorldEntity entity)
    {
        if (MapFile is null
            || (entity.TileX < 0)
            || (entity.TileX >= MapFile.Width)
            || (entity.TileY < 0)
            || (entity.TileY >= MapFile.Height))
        {
            entity.GroundPaintHeight = 0;

            return;
        }

        var bgTileId = MapFile.Tiles[entity.TileX, entity.TileY].Background;

        if (DataContext.Tiles.GroundAttributes.TryGetValue(bgTileId, out var gndAttr))
        {
            entity.IsOnSwimmingTile = gndAttr.IsWalkBlocking;
            entity.GroundPaintHeight = gndAttr.PaintHeight;

            entity.GroundTintColor = new Color(
                gndAttr.R,
                gndAttr.G,
                gndAttr.B,
                gndAttr.A);

            //cache swim walk frame count for animation timing
            if (gndAttr.IsWalkBlocking)
            {
                var isFemale = entity.Appearance?.Gender == Gender.Female;
                var swimFrameCount = Game.AislingRenderer.GetSwimFrameCount(isFemale);
                var framesPerDir = swimFrameCount / 2;
                entity.SwimWalkFrames = Math.Max(framesPerDir - 1, 1);
            }
        } else
        {
            entity.IsOnSwimmingTile = false;
            entity.GroundPaintHeight = 0;
            entity.SwimWalkFrames = 0;
        }
    }
    #endregion

    #region Diagonal Stripe Rendering
    /// <summary>
    ///     Iterates foreground tiles, entities, and effects in diagonal stripe order (depth = x+y ascending). Per stripe draw
    ///     order: ground items → aislings → creatures → ground effects → entity effects → foreground tiles. Within each
    ///     category, entities draw in list order (arrival order — later arrivals on top).
    /// </summary>
    private void DrawForegroundAndEntities(BatchBlendScope scope, IReadOnlyList<WorldEntity> sortedEntities)
    {
        if (MapFile is null)
            return;

        EntityHitBoxes.Clear();

        (var fgMinX, var fgMinY, var fgMaxX, var fgMaxY) = Camera.GetVisibleTileBounds(
            MapFile.Width,
            MapFile.Height,
            MapRenderer.ForegroundExtraMargin);

        var minDepth = fgMinX + fgMinY;
        var maxDepth = fgMaxX + fgMaxY;
        var entityIndex = 0;
        var entityCount = sortedEntities.Count;

        //skip entities before the visible depth range
        while ((entityIndex < entityCount) && (sortedEntities[entityIndex].SortDepth < minDepth))
            entityIndex++;

        for (var depth = minDepth; depth <= maxDepth; depth++)
        {
            //collect entities at this depth stripe
            var stripeStart = entityIndex;

            while ((entityIndex < entityCount) && (sortedEntities[entityIndex].SortDepth == depth))
                entityIndex++;

            var stripeEnd = entityIndex;

            //1. ground items
            for (var i = stripeStart; i < stripeEnd; i++)
                if (sortedEntities[i].Type == ClientEntityType.GroundItem)
                    DrawEntity(scope.Batch, sortedEntities[i]);

            //2. aislings
            for (var i = stripeStart; i < stripeEnd; i++)
                if (sortedEntities[i].Type == ClientEntityType.Aisling)
                    DrawEntity(scope.Batch, sortedEntities[i]);

            //3. creatures
            for (var i = stripeStart; i < stripeEnd; i++)
                if (sortedEntities[i].Type == ClientEntityType.Creature)
                    DrawEntity(scope.Batch, sortedEntities[i]);

            //4. dying creature dissolves
            DrawDyingEffectsAtDepth(scope.Batch, depth);

            //5. ground-targeted effects
            DrawGroundEffectsAtDepth(scope, depth);

            //6. projectiles in flight
            DrawProjectilesAtDepth(scope.Batch, depth);

            //7. entity-attached effects
            for (var i = stripeStart; i < stripeEnd; i++)
                DrawEntityEffects(scope, sortedEntities[i]);

            //8. foreground tiles (on top — trees, buildings occlude entities behind them)
            var tileXStart = Math.Max(fgMinX, depth - fgMaxY);
            var tileXEnd = Math.Min(fgMaxX, depth - fgMinY);

            for (var tileX = tileXStart; tileX <= tileXEnd; tileX++)
                MapRenderer.DrawForegroundTile(
                    scope,
                    MapFile,
                    Camera,
                    tileX,
                    depth - tileX,
                    AnimationTick);
        }
    }

    /// <summary>
    ///     Populates the silhouette renderer with every visible entity, in the exact paint order the stripe pass
    ///     uses. Matching that order is what keeps the 50% silhouette overlay invisible in the open (each entity is
    ///     redrawn over its own identical pixels — a no-op) while still showing entities through foreground
    ///     occlusion. Extends retail's local-player overdraw (FUN_005d4360) to all entities. The local player is
    ///     appended LAST (after the depth loop) so its silhouette composites on top of every other entity.
    ///     Order MUST mirror <see cref="DrawForegroundAndEntities" />: depth-major, and within a depth ground items →
    ///     aislings → creatures. If it diverges, overlapping entities in the open self-blend imperfectly and ghost.
    /// </summary>
    private void CollectSilhouetteEntities(IReadOnlyList<WorldEntity> sortedEntities)
    {
        if (MapFile is null)
            return;

        (var fgMinX, var fgMinY, var fgMaxX, var fgMaxY) = Camera.GetVisibleTileBounds(
            MapFile.Width,
            MapFile.Height,
            MapRenderer.ForegroundExtraMargin);

        var minDepth = fgMinX + fgMinY;
        var maxDepth = fgMaxX + fgMaxY;
        var entityIndex = 0;
        var entityCount = sortedEntities.Count;
        var playerId = WorldState.PlayerEntityId;

        //skip entities before the visible depth range (mirrors the stripe)
        while ((entityIndex < entityCount) && (sortedEntities[entityIndex].SortDepth < minDepth))
            entityIndex++;

        for (var depth = minDepth; depth <= maxDepth; depth++)
        {
            var stripeStart = entityIndex;

            while ((entityIndex < entityCount) && (sortedEntities[entityIndex].SortDepth == depth))
                entityIndex++;

            var stripeEnd = entityIndex;

            //order MUST match DrawForegroundAndEntities: ground items, then aislings, then creatures
            for (var i = stripeStart; i < stripeEnd; i++)
                if (sortedEntities[i].Type == ClientEntityType.GroundItem)
                    SilhouetteRenderer.AddSilhouette(sortedEntities[i].Id);

            //skip the local player here; it is appended last (after the depth loop) so it overdraws on top of all others
            for (var i = stripeStart; i < stripeEnd; i++)
                if ((sortedEntities[i].Type == ClientEntityType.Aisling) && (sortedEntities[i].Id != playerId))
                    SilhouetteRenderer.AddSilhouette(sortedEntities[i].Id);

            for (var i = stripeStart; i < stripeEnd; i++)
                if (sortedEntities[i].Type == ClientEntityType.Creature)
                    SilhouetteRenderer.AddSilhouette(sortedEntities[i].Id);
        }

        //local player last → its silhouette composites on top of every other entity (retail's local-player overdraw).
        //intentional paint-order divergence: only the player overdraws; all other entities stay in stripe order so
        //they still self-blend to a no-op in the open.
        if (WorldState.GetEntity(playerId) is not null)
            SilhouetteRenderer.AddSilhouette(playerId);
    }

    private void DrawDyingEffectsAtDepth(SpriteBatch spriteBatch, int depth)
    {
        if (MapFile is null)
            return;

        foreach (var dying in WorldState.DyingEffects)
        {
            if (dying.IsComplete || ((dying.TileX + dying.TileY) != depth))
                continue;

            var tileWorld = Camera.TileToWorld(dying.TileX, dying.TileY, MapFile.Height);
            var tileCenterX = tileWorld.X + DaLibConstants.HALF_TILE_WIDTH;
            var tileCenterY = tileWorld.Y + DaLibConstants.HALF_TILE_HEIGHT;

            var texCenterX = dying.CenterX - Math.Min(0, (int)dying.Left);
            var texCenterY = dying.CenterY - Math.Min(0, (int)dying.Top);

            var anchorX = dying.Flip
                ? dying.SourceWidth - texCenterX - dying.CenterXOffset
                : texCenterX + dying.CenterXOffset;

            var drawX = tileCenterX - anchorX;
            var drawY = tileCenterY - texCenterY;
            var screenPos = Camera.WorldToScreen(new Vector2(drawX, drawY));

            var effects = dying.Flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            var sourceRect = new Rectangle(0, 0, dying.SourceWidth, dying.TextureHeight);

            spriteBatch.Draw(
                dying.Texture,
                screenPos,
                sourceRect,
                Color.White * dying.Alpha,
                0f,
                Vector2.Zero,
                1f,
                effects,
                0f);
        }
    }

    private void DrawBackgroundEffects(BatchBlendScope scope)
    {
        if (MapFile is null)
            return;

        foreach (var effect in WorldState.ActiveEffects)
        {
            if (!effect.IsBackground || effect.IsComplete)
                continue;

            if (!effect.TileX.HasValue || !effect.TileY.HasValue)
                continue;

            var tileWorld = Camera.TileToWorld(effect.TileX.Value, effect.TileY.Value, MapFile.Height);

            DrawSingleEffect(
                scope,
                effect,
                tileWorld.X + DaLibConstants.HALF_TILE_WIDTH,
                tileWorld.Y + DaLibConstants.HALF_TILE_HEIGHT,
                Vector2.Zero);
        }
    }

    private void DrawGroundEffectsAtDepth(BatchBlendScope scope, int depth)
    {
        foreach (var effect in WorldState.ActiveEffects)
        {
            if (effect.TargetEntityId.HasValue || effect.IsComplete || effect.IsBackground)
                continue;

            if (!effect.TileX.HasValue || !effect.TileY.HasValue)
                continue;

            if ((effect.TileX.Value + effect.TileY.Value) != depth)
                continue;

            var tileWorld = Camera.TileToWorld(effect.TileX.Value, effect.TileY.Value, MapFile!.Height);

            DrawSingleEffect(
                scope,
                effect,
                tileWorld.X + DaLibConstants.HALF_TILE_WIDTH,
                tileWorld.Y + DaLibConstants.HALF_TILE_HEIGHT,
                Vector2.Zero);
        }
    }

    private EntityTintType ResolveEntityTint(WorldEntity entity)
    {
        if (entity.HitTintExpiryMs > 0)
            return EntityTintType.HitTint;

        if (WorldState.CurrentFrame.ShowTintHighlight && (WorldState.CurrentFrame.HoveredEntityId == entity.Id))
            return EntityTintType.Highlight;

        if ((byte)entity.TintColor != 0)
            return EntityTintType.Status;

        if (GroupHighlightedIds.Contains(entity.Id))
            return EntityTintType.Group;

        return EntityTintType.None;
    }

    private void DrawProjectilesAtDepth(SpriteBatch spriteBatch, int depth)
    {
        if (MapFile is null)
            return;

        foreach (var proj in WorldState.ActiveProjectiles)
        {
            if (proj.IsComplete)
                continue;

            var tile = Camera.WorldToTile(proj.CurrentX, proj.CurrentY, MapFile.Height);
            var projDepth = tile.X + tile.Y;

            if (projDepth != depth)
                continue;

            var frameIndex = proj.Direction * proj.FramesPerDirection + proj.CurrentFrameCycle;

            Game.EffectRenderer.DrawProjectile(
                spriteBatch,
                Camera,
                proj.MeffectId,
                frameIndex,
                proj.CurrentX + proj.ArcOffsetX,
                proj.CurrentY + proj.ArcOffsetY);
        }
    }

    private void DrawSingleEffect(
        BatchBlendScope scope,
        Animation effect,
        float tileCenterX,
        float tileCenterY,
        Vector2 visualOffset)
        => Game.EffectRenderer.Draw(
            scope,
            Camera,
            effect.EffectId,
            effect.CurrentFrame,
            effect.BlendMode,
            tileCenterX,
            tileCenterY,
            visualOffset);
    #endregion

    #region Entity Rendering
    private void DrawEntity(SpriteBatch spriteBatch, WorldEntity entity)
    {
        if (MapFile is null)
            return;

        var tileWorldPos = Camera.TileToWorld(entity.TileX, entity.TileY, MapFile.Height);
        var tileCenterX = tileWorldPos.X + DaLibConstants.HALF_TILE_WIDTH;
        var tileCenterY = tileWorldPos.Y + DaLibConstants.HALF_TILE_HEIGHT;

        var entityTextureBottom = 0;

        switch (entity.Type)
        {
            case ClientEntityType.Aisling:
                entityTextureBottom = DrawAisling(
                    spriteBatch,
                    entity,
                    tileCenterX,
                    tileCenterY);

                break;

            case ClientEntityType.Creature:
                entityTextureBottom = DrawCreature(
                    spriteBatch,
                    entity,
                    tileCenterX,
                    tileCenterY);

                break;

            case ClientEntityType.GroundItem:
                DrawGroundItem(
                    spriteBatch,
                    entity,
                    tileCenterX,
                    tileCenterY);

                return; //ground items don't get hitboxes
        }

        if (entityTextureBottom <= 0)
            return;

        //hitbox: 28px wide centered on tile screen x, 60px tall bottom-aligned to texture bottom
        var tileScreenPos = Camera.WorldToScreen(new Vector2(tileCenterX + entity.VisualOffset.X, tileCenterY + entity.VisualOffset.Y));
        var hitboxX = (int)tileScreenPos.X - HITBOX_WIDTH / 2;
        var hitboxY = entityTextureBottom - HITBOX_HEIGHT;

        EntityHitBoxes.Add(
            new EntityHitBox(
                entity.Id,
                new Rectangle(
                    hitboxX,
                    hitboxY,
                    HITBOX_WIDTH,
                    HITBOX_HEIGHT)));
    }

    /// <summary>
    ///     Draws a creature entity. Returns the screen-space Y of the texture bottom edge, or 0 if not drawn.
    /// </summary>
    private int DrawCreature(
        SpriteBatch spriteBatch,
        WorldEntity entity,
        float tileCenterX,
        float tileCenterY)
    {
        var creatureRenderer = Game.CreatureRenderer;
        var animInfo = creatureRenderer.GetAnimInfo(entity.SpriteId);

        if (animInfo is null)
            return 0;

        var info = animInfo.Value;
        (var frameIndex, var flip) = AnimationSystem.GetCreatureFrame(entity, in info);

        //transparent entities draw faded in both passes so they compound multiplicatively with occlusion:
        //stripe at TRANSPARENT_ALPHA + silhouette RT at TRANSPARENT_SILHOUETTE_ALPHA → ~50% open, ~25% behind FG.
        //non-transparent entities draw opaque in both passes → 100% open, ~50% behind FG.
        var alpha = entity.IsHidden
            ? 0f
            : entity.IsTransparent
                ? DrawingForSilhouette ? TRANSPARENT_SILHOUETTE_ALPHA : TRANSPARENT_ALPHA
                : 1f;

        var tint = ResolveEntityTint(entity);

        //mirror the aisling convention — swimming tiles replace the normal sprite path and must not double-tint the creature.
        var groundPaintHeight = entity.IsOnSwimmingTile ? 0 : entity.GroundPaintHeight;

        var statusTint = tint == EntityTintType.Status ? LegendColors.Get(entity.TintColor) : default;

        return creatureRenderer.Draw(
            spriteBatch,
            Camera,
            entity.SpriteId,
            frameIndex,
            flip,
            tileCenterX,
            tileCenterY,
            entity.VisualOffset,
            tint,
            statusTint,
            groundPaintHeight,
            entity.GroundTintColor,
            alpha);
    }

    private int DrawAisling(
        SpriteBatch spriteBatch,
        WorldEntity entity,
        float tileCenterX,
        float tileCenterY)
    {
        //hidden aislings have no visual (body sprite 0, all equipment 0) but are still present for
        //hit-testing — skip the draw and anchor the hitbox bottom to the tile center (feet position).
        if (entity.IsHidden)
        {
            var tileScreenPos = Camera.WorldToScreen(new Vector2(tileCenterX + entity.VisualOffset.X, tileCenterY + entity.VisualOffset.Y));

            return (int)tileScreenPos.Y;
        }

        //morphed aislings (creature form) render as creatures — swimming overrides morphs too
        if (entity.Appearance is null && entity is { SpriteId: > 0, IsOnSwimmingTile: false })
            return DrawCreature(
                spriteBatch,
                entity,
                tileCenterX,
                tileCenterY);

        if (entity.Appearance is null && !entity.IsOnSwimmingTile)
            return 0;

        var appearance = entity.Appearance ?? default;
        (var frameIndex, var flip, var animSuffix, var isFrontFacing) = AnimationSystem.GetAislingFrame(entity);

        //swimming override — single sprite replaces all aisling layers, driven by existing animation state
        if (entity.IsOnSwimmingTile)
        {
            var isFemale = entity.Appearance?.Gender == Gender.Female;
            var dirIndex = isFrontFacing ? 1 : 0;

            var swimFrameCount = Game.AislingRenderer.GetSwimFrameCount(isFemale);
            var framesPerDir = swimFrameCount / 2;

            if (framesPerDir <= 0)
                return 0;

            //walking: use walk frame index directly. idle: use idleanimtick for continuous cycling.
            //frame 0 is the idle/standing pose — skip it so the swim animation only cycles walk frames (1..n).
            var walkFrames = framesPerDir - 1;

            var animIndex = walkFrames > 0
                ? 1 + (entity.AnimState == EntityAnimState.Walking ? entity.AnimFrameIndex % walkFrames : entity.IdleAnimTick % walkFrames)
                : 0;

            var swimFrame = dirIndex * framesPerDir + animIndex;

            return Game.AislingRenderer.DrawSwimming(
                spriteBatch,
                Camera,
                isFemale,
                swimFrame,
                flip,
                tileCenterX,
                tileCenterY,
                entity.VisualOffset);
        }

        //rest position override — single spf sprite replaces all aisling layers
        if (entity.RestPosition != RestPosition.None)
            return Game.AislingRenderer.DrawResting(
                spriteBatch,
                Camera,
                entity.Appearance?.Gender == Gender.Female,
                entity.RestPosition,
                isFrontFacing,
                flip,
                tileCenterX,
                tileCenterY,
                entity.VisualOffset,
                entity.ActiveEmoteFrame);

        var emotionFrame = entity.ActiveEmoteFrame;
        var groundPaintHeight = entity.IsOnSwimmingTile ? 0 : entity.GroundPaintHeight;

        var tint = ResolveEntityTint(entity);

        //dead wins over transparent: a ghost uses the opaque base alpha so AislingRenderer's GHOST_ALPHA isn't
        //stacked with TRANSPARENT_ALPHA into an effectively-invisible result. Living transparent aislings draw faded
        //in both passes (stripe TRANSPARENT_ALPHA + silhouette TRANSPARENT_SILHOUETTE_ALPHA → ~50% open, ~25% behind FG).
        var alpha = entity is { IsTransparent: true, IsDead: false }
            ? DrawingForSilhouette ? TRANSPARENT_SILHOUETTE_ALPHA : TRANSPARENT_ALPHA
            : 1f;

        var drawParams = new AislingDrawParams(
            entity.Id,
            appearance,
            frameIndex,
            flip,
            isFrontFacing,
            animSuffix,
            emotionFrame,
            groundPaintHeight,
            entity.GroundTintColor,
            tileCenterX,
            tileCenterY,
            entity.VisualOffset,
            tint,
            tint == EntityTintType.Status ? LegendColors.Get(entity.TintColor) : default,
            entity.IsDead,
            alpha);

        return Game.AislingRenderer.Draw(spriteBatch, Camera, in drawParams);
    }

    private void DrawEntityEffects(BatchBlendScope scope, WorldEntity entity)
    {
        if (MapFile is null)
            return;

        var tileWorldPos = Camera.TileToWorld(entity.TileX, entity.TileY, MapFile.Height);
        var tileCenterX = tileWorldPos.X + DaLibConstants.HALF_TILE_WIDTH;
        var tileCenterY = tileWorldPos.Y + DaLibConstants.HALF_TILE_HEIGHT;

        foreach (var effect in WorldState.ActiveEffects)
        {
            if ((effect.TargetEntityId != entity.Id) || effect.IsComplete)
                continue;

            DrawSingleEffect(
                scope,
                effect,
                tileCenterX,
                tileCenterY,
                entity.VisualOffset);
        }
    }

    private void DrawGroundItem(
        SpriteBatch spriteBatch,
        WorldEntity entity,
        float tileCenterX,
        float tileCenterY)
    {
        //swim tiles don't normally host ground items, but mirror the aisling/creature convention for safety.
        var groundPaintHeight = entity.IsOnSwimmingTile ? 0 : entity.GroundPaintHeight;

        Game.ItemRenderer.Draw(
            spriteBatch,
            Camera,
            entity.SpriteId,
            entity.ItemColor,
            tileCenterX,
            tileCenterY,
            groundPaintHeight,
            entity.GroundTintColor);
    }

    /// <summary>
    ///     Creates a texture containing a dashed ellipse inscribed in the isometric tile diamond. Gaps at the 4 cardinal
    ///     directions (top, right, bottom, left of the ellipse).
    /// </summary>
    private static Texture2D CreateTileCursorTexture(GraphicsDevice device, Color color)
    {
        const int WIDTH = DaLibConstants.HALF_TILE_WIDTH * 2; //56
        const int HEIGHT = DaLibConstants.HALF_TILE_HEIGHT * 2; //28

        var pixels = new Color[WIDTH * HEIGHT];

        var cx = WIDTH / 2;
        var cy = HEIGHT / 2;

        //top-right quarter only.
        //these are offsets from the center.
        //tweak these until the shape matches exactly how you want.
        Span<Point> quarter =
        [
            new(-6, -8),
            new(-7, -8),
            new(-8, -8),
            new(-9, -8),
            new(-10, -8),
            new(-11, -7),
            new(-12, -7),
            new(-13, -6),
            new(-14, -6),
            new(-15, -5),
            new(-16, -5),
            new(-17, -4),
            new(-17, -3)
        ];

        ImageUtil.DrawProjectedQuadrants(
            pixels,
            WIDTH,
            HEIGHT,
            cx,
            cy,
            quarter,
            color);

        var texture = new Texture2D(device, WIDTH, HEIGHT);
        texture.SetData(pixels);

        return texture;
    }

    private void DrawDragIcon(SpriteBatch spriteBatch)
    {
        //the payload owns its ghost, so a drag source needs no PanelBase behind it. The cursor is the same virtual-space
        //value the dispatcher measured the drag threshold against, and the UI pass applies no camera transform.
        if (Game.Dispatcher.ActiveDragPayload is not IDragGhost { GhostTexture: { } icon })
            return;

        spriteBatch.Draw(
            icon,
            new Vector2(InputBuffer.MouseX - icon.Width / 2, InputBuffer.MouseY - icon.Height / 2),
            Color.White * 0.7f);
    }

    private void DrawTileCursor(SpriteBatch spriteBatch)
    {
        if (MapFile is null || TileCursorTexture is null)
            return;

        //ground-targeting replaces the dashed cursor with the target-tile treatment (see DrawGroundTargetHighlight)
        if (IsAimingAtTile)
            return;

        if (WorldState.CurrentFrame.HoveredTile is not { } hoverTile)
            return;

        var tileWorld = Camera.TileToWorld(hoverTile.X, hoverTile.Y, MapFile.Height);
        var tileScreen = Camera.WorldToScreen(new Vector2(tileWorld.X, tileWorld.Y));

        var cursorTexture = WorldState.CurrentFrame.UseDragCursor ? TileCursorDragTexture : TileCursorTexture;
        spriteBatch.Draw(cursorTexture!, new Vector2((int)tileScreen.X, (int)tileScreen.Y), Color.White);
    }

    /// <summary>
    ///     GROUND-TARGET TILE TREATMENT — SWAP SEAM. The single place that decides how a ground-targeted spell's target
    ///     tile is conveyed. Drawn over the world (after foreground/entities, so walls can't occlude it), replacing the
    ///     dashed cursor while a ground spell is armed. To try a different treatment (glow, pulse, sprite, etc.), rewrite
    ///     this method body — nothing else in the targeting flow changes.
    ///     Current: a cyan tile diamond — brighter perimeter border over a very slight interior fill (fill/border ratio
    ///     baked into the texture; <see cref="GroundTargetHighlightColor" /> is the premultiplied tint for overall
    ///     color/opacity). ponytail: single hovered tile now. For the multi-tile AOE stencil, reuse TabMapRenderer's
    ///     border-collapse — a 16-variant atlas keyed by a 4-bit neighbor mask so a region shows only its outer perimeter;
    ///     this single tile is that scheme's mask=0 (all-borders) case.
    /// </summary>
    private void DrawGroundTargetHighlight(SpriteBatch spriteBatch)
    {
        if (MapFile is null || TileHighlightTexture is null)
            return;

        if (!IsAimingAtTile)
            return;

        //HoveredTile gates on the viewport and map bounds — an aim over the HUD draws nothing
        if (WorldState.CurrentFrame.HoveredTile is not { } tile)
            return;

        //the reticle has to sit on the tile the cast will actually use, entity snap included
        if (SnapTargetTileAt(InputBuffer.MouseX, InputBuffer.MouseY) is { } snapped)
            tile = new Point(snapped.X, snapped.Y);

        var tileWorld = Camera.TileToWorld(tile.X, tile.Y, MapFile.Height);
        var tileScreen = Camera.WorldToScreen(new Vector2(tileWorld.X, tileWorld.Y));

        spriteBatch.Draw(TileHighlightTexture, new Vector2((int)tileScreen.X, (int)tileScreen.Y), GroundTargetHighlightColor);
    }

    /// <summary>
    ///     Creates the tile-diamond highlight texture: a faint interior fill with a brighter perimeter border, mirroring
    ///     TabMapRenderer's fill+border tile so the world highlight and tab map read consistently. Pixels are white with
    ///     the fill/border opacity ratio baked in; the caller applies color and overall opacity via the draw tint.
    /// </summary>
    private static Texture2D CreateTileHighlightTexture(GraphicsDevice device)
    {
        const int WIDTH = DaLibConstants.TILE_WIDTH; //56
        const int HEIGHT = DaLibConstants.TILE_HEIGHT; //27

        var fill = Color.White * 0.25f; //interior weight relative to the opaque border (further scaled by the draw tint)

        var pixels = new Color[WIDTH * HEIGHT];

        var cx = WIDTH / 2; //28
        var cy = HEIGHT / 2; //13
        var hw = WIDTH / 2 - 1; //27 — left/right tips land at x=1/x=55, inside the buffer (no tip clipping)
        var hh = HEIGHT / 2; //13

        //interior fill: solid span per row (every pixel set, no gaps). half-width is 0 at the tips, full hw at center.
        for (var y = 0; y < HEIGHT; y++)
        {
            var halfW = (int)MathF.Round(hw * (1f - Math.Abs(y - cy) / (float)hh));

            for (var x = cx - halfW; x <= cx + halfW; x++)
                pixels[y * WIDTH + x] = fill;
        }

        //perimeter border: the diamond edge is shallow (~2px across per 1px down), so it must be walked x-major (one
        //pixel per COLUMN) to stay connected — drawing it per-row would leave ~2px gaps and read as a dotted line.
        //DrawProjectedQuadrants mirrors this one quarter onto all four edges, overwriting the fill's edge pixels.
        Span<Point> quarter = stackalloc Point[hw + 1];

        for (var dx = 0; dx <= hw; dx++)
            quarter[dx] = new Point(dx, -hh + (int)MathF.Round(dx * (float)hh / hw));

        ImageUtil.DrawProjectedQuadrants(
            pixels,
            WIDTH,
            HEIGHT,
            cx,
            cy,
            quarter,
            Color.White);

        var texture = new Texture2D(device, WIDTH, HEIGHT);
        texture.SetData(pixels);

        return texture;
    }
    #endregion
}