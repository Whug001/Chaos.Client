#region
using Chaos.Client.Collections;
using Chaos.Client.Controls.Components;
using Chaos.Client.Data.Utilities;
using Chaos.Client.Definitions;
using Chaos.Client.Models;
using Chaos.Client.Rendering;
using Chaos.Client.Systems;
using Chaos.DarkAges.Definitions;
using Chaos.Geometry.Abstractions.Definitions;
using Microsoft.Xna.Framework;
using Pathfinder = Chaos.Client.Systems.Pathfinder;
#endregion

namespace Chaos.Client.Screens;

public sealed partial class WorldScreen
{
    public void Update(GameTime gameTime)
    {
        if (PendingLoginSwitch)
        {
            PendingLoginSwitch = false;
            Game.Screens.Switch(new LobbyLoginScreen(true));

            return;
        }

        if (!_avatarCaptured
            && Chaos.Client.Systems.AvatarCapture.IsEnabled
            && WorldState.GetPlayerEntity()?.Appearance is { } selfAppearance)
        {
            _avatarCaptured = true;
            Chaos.Client.Systems.AvatarCapture.CaptureAndSave(Game.AislingRenderer, in selfAppearance);
        }

        var elapsedMs = (float)gameTime.ElapsedGameTime.TotalMilliseconds;

        //global tile animation tick — 100ms resolution (matches tile animation table format)
        AnimationTick = (int)(gameTime.TotalGameTime.TotalMilliseconds / 100);
        MapRenderer.UpdatePaletteCycling(AnimationTick);

        //advance entity animations and active effects
        var smoothScroll = ClientSettings.ScrollLevel > 0;
        var player = WorldState.GetPlayerEntity();

        //animation advancement doesn't depend on sort order — iterate unordered to avoid a stale sort
        //(SortDepth is position-derived; movement later in Update would invalidate any sort taken here).
        foreach (var entity in WorldState.GetEntities())
        {
            //update water tile state before animation so swimming idle tick advances
            UpdateEntityWaterState(entity);

            //all entities step discretely by default. player gets smooth lerp only if setting enabled.
            var isSmooth = (entity == player) && smoothScroll;
            AnimationSystem.Advance(entity, elapsedMs, isSmooth);

            //update creature optional standing animation cycle — covers native creatures AND aislings
            //in monster form (both render as creature sprites), so gate on IsRenderedAsCreatureSprite
            //rather than Type. Otherwise a player-form's IdleOptionalActive/IdleFrameIntervalMs never
            //update, freezing forms whose idle motion lives in the optional loop (StandingFrameCount<=1).
            if (entity.IsRenderedAsCreatureSprite)
            {
                var animInfo = Game.CreatureRenderer.GetAnimInfo(entity.SpriteId);

                if (animInfo.HasValue)
                {
                    var info = animInfo.Value;
                    AnimationSystem.UpdateCreatureIdleCycle(entity, in info);
                }
            } else
                //leaving a form stops UpdateCreatureIdleCycle from running, but the creature's fast frame
                //interval would otherwise stick — clear it so the aisling idle falls back to DEFAULT_IDLE_FRAME_MS.
                entity.IdleFrameIntervalMs = 0f;

            //tick emote overlay timer and cycle animated emote frames
            if (entity.ActiveEmoteFrame >= 0)
            {
                entity.EmoteElapsedMs += elapsedMs;
                entity.EmoteRemainingMs -= elapsedMs;

                if (entity.EmoteRemainingMs <= 0)
                {
                    entity.ActiveEmoteFrame = -1;
                    entity.EmoteFrameCount = 0;
                } else if (entity.EmoteFrameCount > 1)
                {
                    var frameDuration = entity.EmoteDurationMs / entity.EmoteFrameCount;
                    var frameIndex = (int)(entity.EmoteElapsedMs / frameDuration) % entity.EmoteFrameCount;
                    entity.ActiveEmoteFrame = entity.EmoteStartFrame + frameIndex;
                }
            }

            //tick the sunglasses emote and retire it once it has run its course
            if (entity.IsWearingSunglasses)
            {
                entity.SunglassesElapsedMs += elapsedMs;

                if (SunglassesEmote.Resolve(entity.SunglassesElapsedMs)
                                   .IsFinished)
                    entity.SunglassesElapsedMs = -1f;
            }

            //tick the middle finger bubble; it is static, so it only needs a countdown
            if (entity.IsFlippingOff)
                entity.MiddleFingerRemainingMs = Math.Max(0f, entity.MiddleFingerRemainingMs - elapsedMs);

            if (entity.HitTintExpiryMs > 0)
                entity.HitTintExpiryMs = Math.Max(0, entity.HitTintExpiryMs - elapsedMs);
        }

        WorldState.UpdateEffects(elapsedMs);
        AdvanceProjectiles(elapsedMs);

        //group highlight auto-expire (1000ms flash)
        if (GroupHighlightedIds.Count > 0)
        {
            GroupHighlightTimer -= elapsedMs;

            if (GroupHighlightTimer <= 0)
            {
                GroupHighlightedIds.Clear();
                Game.AislingRenderer.ClearGroupTintCache();
                Game.CreatureRenderer.ClearTintCaches();
            }
        }

        //execute queued walk when player becomes idle after walk animation.
        var movementHandled = false;

        if (player is not null && player.IsAtRest && QueuedWalkDirection.HasValue)
        {
            var queuedDir = QueuedWalkDirection.Value;
            QueuedWalkDirection = null;

            if (player.Direction != queuedDir)
            {
                Game.Connection.Turn(queuedDir);
                player.Direction = queuedDir;
            } else
                PredictAndWalk(player, queuedDir);

            movementHandled = true;
        }

        //execute next pathfinding step when player becomes idle
        if (!movementHandled && player is not null && player.IsAtRest)
        {
            if (Pathfinding.Path is { Count: > 0 })
            {
                //if chasing an entity that no longer exists, stop
                if (Pathfinding.TargetEntityId.HasValue && WorldState.GetEntity(Pathfinding.TargetEntityId.Value) is null)
                    Pathfinding.Clear();
                else
                {
                    var nextPoint = Pathfinding.Path.Pop();
                    var dx = nextPoint.X - player.TileX;
                    var dy = nextPoint.Y - player.TileY;

                    var pathDir = (dx, dy) switch
                    {
                        (0, -1) => Direction.Up,
                        (1, 0)  => Direction.Right,
                        (0, 1)  => Direction.Down,
                        (-1, 0) => Direction.Left,
                        _       => (Direction?)null
                    };

                    if (pathDir.HasValue)
                    {
                        if (player.Direction != pathDir.Value)
                        {
                            Game.Connection.Turn(pathDir.Value);
                            player.Direction = pathDir.Value;
                        }

                        PredictAndWalk(player, pathDir.Value);
                    } else
                        Pathfinding.Clear();
                }
            } else if (Pathfinding.TargetEntityId.HasValue)
            {
                //path exhausted with entity target — check if adjacent and assail, or re-pathfind
                var target = WorldState.GetEntity(Pathfinding.TargetEntityId.Value);

                if (target is null)
                    Pathfinding.Clear();
                else if (Pathfinder.IsAdjacent(
                             player.TileX,
                             player.TileY,
                             target.TileX,
                             target.TileY))
                {
                    //adjacent — turn toward target and assail
                    var faceDir = Pathfinder.DirectionToward(
                        player.TileX,
                        player.TileY,
                        target.TileX,
                        target.TileY);

                    if (faceDir.HasValue && (player.Direction != faceDir.Value))
                    {
                        Game.Connection.Turn(faceDir.Value);
                        player.Direction = faceDir.Value;
                    }

                    Game.Connection.Spacebar();
                } else
                {
                    //entity moved — re-pathfind on 100ms timer
                    Pathfinding.RetargetTimer += elapsedMs;

                    if (Pathfinding.RetargetTimer >= 100f)
                    {
                        Pathfinding.RetargetTimer = 0;
                        PathfindToEntity(player, target);

                        if (Pathfinding.Path is null)
                            Pathfinding.Clear();
                    }
                }
            }
        }

        //tick re-pathfind timer while walking toward an entity target
        if (Pathfinding.TargetEntityId.HasValue && player is not null && (player.AnimState == EntityAnimState.Walking))
            Pathfinding.RetargetTimer += elapsedMs;

        //camera follows player's visual position (tile + walk interpolation offset)
        FollowPlayerCamera();

        //viewport-layer updates — must always run regardless of which ui panel has input focus
        //so that the world keeps animating visually behind open windows.
        if (MapFile is not null)
            Overlays.Update(
                Camera,
                MapFile.Height,
                Game.CreatureRenderer,
                gameTime);

        //bard song call countdown — driven from the update tick (not draw) so a live call expires in real
        //time even when draws are skipped or catch-up ticks run update multiple times per draw.
        //if the window expired before all four notes were entered, SongState hands back the partial
        //answer (0 for unentered notes) and has already cleared the call; send it exactly once here.
        var expiredSongAnswer = WorldState.Song.Update(elapsedMs);

        if (expiredSongAnswer is { } songAnswer)
            Game.Connection.SendSongAnswer(
                songAnswer.CallId,
                songAnswer.N1,
                songAnswer.N2,
                songAnswer.N3,
                songAnswer.N4);

        //gather light sources for this frame and feed them to consumers
        Lighting.Gather(MapFile, CurrentMapFlags, Camera);

        if (DarknessRenderer.IsActive)
            DarknessRenderer.Update(Camera, WorldHud.ViewportBounds, Lighting.Sources);

        WeatherRenderer.Update(gameTime, WorldHud.ViewportBounds);

        //tooltip follows cursor — always reposition regardless of active panel
        if (ItemTooltip.Visible)
            ItemTooltip.UpdatePosition(InputBuffer.MouseX, InputBuffer.MouseY);

        //── keyword hover — explain the tag or status the cursor is resting on ──
        KeywordTooltip.TrackHover(InputBuffer.MouseX, InputBuffer.MouseY, AbilityMetadataDetails.HoverableText);

        //── tooltip dismissal — clear hovered slot when blocking panels are visible ──
        if (HoveredInventorySlot is not null
            && (OtherProfile.Visible || NpcSession.Visible || FindVisibleModal() is not null))
        {
            HoveredInventorySlot = null;
            ItemTooltip.Hide();
        }

        //── track which entity the mouse is hovering over ──
        var hoverEntity = GetEntityAtScreen(InputBuffer.MouseX, InputBuffer.MouseY);

        var isItemDrag = Game.Dispatcher.ActiveDragPayload is SlotDragPayload slotDrag && (slotDrag.Source.Parent == WorldHud.Inventory);

        var newHoveredId = hoverEntity?.Type is ClientEntityType.Aisling or ClientEntityType.Creature
                           && !hoverEntity.IsHidden
                           && !(isItemDrag && (hoverEntity.Id == Game.Connection.AislingId))
            ? hoverEntity.Id
            : (uint?)null;

        Game.UseHandCursor = newHoveredId.HasValue;

        //tick casting timer (chant lines are sent on a 1-second interval)
        CastingSystem.Update(elapsedMs, Game.Connection);

        //slot machine reels: Slots.Update(float) is a distinct method from the inherited GameTime-based Update, so
        //Root!.Update(gameTime) below would never reach it -- it must be ticked explicitly, in seconds (ReelControl's
        //SPIN_SPEED/SETTLE_SECONDS are both second-based), hence the /1000f conversion from this method's millisecond elapsed.
        Slots.Update(elapsedMs / 1000f);

        //gilded spindle wheels: same reasoning as Slots.Update above -- Spindle.Update(float) is a distinct method
        //from the inherited GameTime-based Update and must be ticked explicitly, in seconds.
        Spindle.Update(elapsedMs / 1000f);

        //spacebar assail is handled in OnRootKeyDown — the dispatcher delivers both the
        //initial press and os key-repeat keydowns through the event pipeline, so dialogs
        //that consume spacebar (via e.Handled = true) naturally block it.

        var skipDispatch = false;

        foreach (var evt in InputBuffer.Events)
        {
            if (evt is not { Kind: BufferedInputKind.MouseButton, Button: MouseButton.Left, IsPress: true })
                continue;

            var mx = evt.X;
            var my = evt.Y;

            if (AislingContext.Visible && !AislingContext.ContainsPoint(mx, my))
            {
                AislingContext.Hide();
                skipDispatch = true;
            } else if (AbilityMetadataDetails.Visible && !AbilityMetadataDetails.ContainsPoint(mx, my))
            {
                AbilityMetadataDetails.Hide();
                skipDispatch = true;
            } else if (EventMetadataDetails.Visible && !EventMetadataDetails.ContainsPoint(mx, my))
            {
                EventMetadataDetails.Hide();
                skipDispatch = true;
            }

            break;
        }

        if (!skipDispatch)
            Game.Dispatcher.ProcessInput(Root!, gameTime);

        UpdateEmoteWheel();

        //all movement has been processed at this point — sort once and publish the frame state.
        PopulateFrameState(newHoveredId);

        Root!.Update(gameTime);
    }

    private void UpdateEmoteWheel()
    {
        if (_suppressWorldListUntilERelease && !InputBuffer.IsScancodeHeld(Scancode.E))
            _suppressWorldListUntilERelease = false;

        if (Game.Dispatcher.ControlStackCount > 0 && !EmoteWheel.IsOpen)
            return;

        var held = InputBuffer.IsMiddleButtonHeld && InputBuffer.IsScancodeHeld(Scancode.E);

        if (held)
        {
            _suppressWorldListUntilERelease = true;

            if (!EmoteWheel.IsOpen)
            {
                var bodyColor = WorldState.GetPlayerEntity()?.Appearance?.BodyColor ?? (int)BodyColor.White;
                EmoteWheel.ShowAt(InputBuffer.MouseX, InputBuffer.MouseY, bodyColor);
            }

            _emoteWheelHeld = true;
        }

        if (!EmoteWheel.IsOpen)
            return;

        if (!EmoteWheel.IsCatalogOpen)
            EmoteWheel.UpdateHover(InputBuffer.MouseX, InputBuffer.MouseY);

        if (!held && _emoteWheelHeld)
        {
            if (EmoteWheel.IsCatalogOpen)
            {
                //chord release while configuring — keep the catalog open
                _emoteWheelHeld = false;

                return;
            }

            if (EmoteWheel.GetHighlightedEmote() is { } emote)
            {
                var player = WorldState.GetPlayerEntity();

                if (player is not null && player.IsAtRest)
                {
                    Game.Connection.SendEmote(emote);
                    TryPlayLocalEmote(emote);
                }
            }

            EmoteWheel.Hide();
            _emoteWheelHeld = false;
        }
    }

    private void TryPlayLocalEmote(BodyAnimation anim)
    {
        var entity = WorldState.GetPlayerEntity();

        if (entity is null || !entity.IsAtRest)
            return;

        if ((entity.AnimState == EntityAnimState.BodyAnim)
            || (entity.ActiveEmoteFrame >= 0)
            || entity.IsWearingSunglasses
            || entity.IsFlippingOff)
            return;

        //the client-side emotes have no emot01 frame of their own, so they never reach the overlay path below
        switch ((int)anim)
        {
            case SunglassesEmote.BODY_ANIMATION:
                entity.SunglassesElapsedMs = 0f;

                return;

            case MiddleFingerEmote.BODY_ANIMATION:
                entity.MiddleFingerRemainingMs = MiddleFingerEmote.DURATION_MS;

                return;
        }

        (_, var framesPerDir, _, _) = AnimationSystem.ResolveBodyAnimParams(anim);

        if (framesPerDir > 0 || !DataUtilities.IsEmote(anim))
            return;

        (var startFrame, var frameCount, var durationMs) = AnimationSystem.ResolveEmoteFrames(anim);

        if (startFrame < 0)
            return;

        entity.EmoteStartFrame = startFrame;
        entity.EmoteFrameCount = frameCount;
        entity.ActiveEmoteFrame = startFrame;
        entity.EmoteDurationMs = durationMs;
        entity.EmoteElapsedMs = 0;
        entity.EmoteRemainingMs = durationMs;
    }

    /// <summary>
    ///     Publishes derived per-frame state (sort order, hover, tile under cursor) to <see cref="WorldState.CurrentFrame" /> after
    ///     all movement has been processed for the frame. Called once at the end of Update; Draw and overlay systems read
    ///     from <c>WorldState.Frame</c> rather than recomputing.
    /// </summary>
    private void PopulateFrameState(uint? newHoveredId)
    {
        //capture prev before Clear wipes it so the dirty-check still has last frame's value.
        var prevHoveredId = WorldState.CurrentFrame.HoveredEntityId;
        WorldState.CurrentFrame.Reset();

        if (newHoveredId != prevHoveredId)
        {
            Game.AislingRenderer.ClearTintedCache();
            Game.CreatureRenderer.ClearTintCaches();
        }

        //GetSortedEntities is self-caching via dirty flag, so this call is free when the sort is still valid.
        WorldState.CurrentFrame.SortedEntities = WorldState.GetSortedEntities();
        WorldState.CurrentFrame.HoveredEntityId = newHoveredId;
        //aiming a ground spell tints the tile (see WorldScreen.Draw), not entities — so suppress the entity hover-tint,
        //whether the spell was armed in cast mode or is being dragged. The two terms stay independent: a ground spell
        //being armed must not kill the drop-target tint of an unrelated drag (e.g. an inventory item onto a creature).
        WorldState.CurrentFrame.ShowTintHighlight =
            CastingSystem is { IsTargeting: true, IsGroundTargeting: false }
            || (Game.Dispatcher.IsDragging && !IsDraggingGroundSpell);
        WorldState.CurrentFrame.UseDragCursor = Game.Dispatcher.IsDragging;

        var worldViewport = WorldHud.ViewportBounds;

        WorldState.CurrentFrame.HoveredGroupBoxId = Overlays
                                             .GetGroupBoxAtScreen(
                                                 InputBuffer.MouseX - worldViewport.X,
                                                 InputBuffer.MouseY - worldViewport.Y)
                                             ?.EntityId;

        //mirror DrawTileCursor bounds-check logic exactly: viewport rect, then ScreenToTile, then map bounds.
        //HoveredTile is already null from Clear() — we only assign when all checks pass.
        if (MapFile is null
            || (InputBuffer.MouseX < worldViewport.X)
            || (InputBuffer.MouseX >= (worldViewport.X + worldViewport.Width))
            || (InputBuffer.MouseY < worldViewport.Y)
            || (InputBuffer.MouseY >= (worldViewport.Y + worldViewport.Height)))
            return;

        (var tileX, var tileY) = ScreenToTile(InputBuffer.MouseX, InputBuffer.MouseY);

        if ((tileX < 0) || (tileX >= MapFile.Width) || (tileY < 0) || (tileY >= MapFile.Height))
            return;

        WorldState.CurrentFrame.HoveredTile = new Point(tileX, tileY);
    }

    #region Projectile Advancement
    private void AdvanceProjectiles(float elapsedMs)
    {
        if (MapFile is null)
            return;

        for (var i = WorldState.ActiveProjectiles.Count - 1; i >= 0; i--)
        {
            var proj = WorldState.ActiveProjectiles[i];
            proj.ElapsedMs += elapsedMs;

            //defensive: a malformed projectile spec (StepDelayMs <= 0 or Step <= 0) would never
            //drain ElapsedMs or never reach the target, freezing the game thread inside this loop.
            if (proj.StepDelayMs <= 0f || proj.Step <= 0)
            {
                proj.IsComplete = true;
                WorldState.ActiveProjectiles.RemoveAt(i);

                continue;
            }

            //iteration cap defends against pathological cases where the projectile genuinely
            //can't catch a moving target within one frame's worth of elapsed time.
            const int MAX_STEPS_PER_FRAME = 64;
            var steps = 0;

            while ((proj.ElapsedMs >= proj.StepDelayMs) && !proj.IsComplete && (steps < MAX_STEPS_PER_FRAME))
            {
                proj.ElapsedMs -= proj.StepDelayMs;
                AdvanceProjectileStep(proj);
                steps++;
            }

            if (proj.IsComplete)
                WorldState.ActiveProjectiles.RemoveAt(i);
        }
    }

    private const float HIT_TINT_FLASH_MS = 100f;

    private void AdvanceProjectileStep(Projectile proj)
    {
        var targetEntity = WorldState.GetEntity(proj.TargetEntityId);

        if (targetEntity is not null)
        {
            var targetWorld = Camera.TileToWorld(targetEntity.TileX, targetEntity.TileY, MapFile!.Height);
            proj.LastKnownTargetX = targetWorld.X + DaLibConstants.HALF_TILE_WIDTH;
            proj.LastKnownTargetY = targetWorld.Y + DaLibConstants.HALF_TILE_HEIGHT;
        }

        var dx = proj.LastKnownTargetX - proj.CurrentX;
        var dy = proj.LastKnownTargetY - proj.CurrentY;
        var distSq = dx * dx + dy * dy;
        var stepSq = (float)proj.Step * proj.Step;

        if (distSq <= stepSq)
        {
            proj.IsComplete = true;

            targetEntity?.HitTintExpiryMs = HIT_TINT_FLASH_MS;

            return;
        }

        var remainingDistance = MathF.Sqrt(distSq);
        var unitX = dx / remainingDistance;
        var unitY = dy / remainingDistance;

        proj.CurrentX += unitX * proj.Step;
        proj.CurrentY += unitY * proj.Step;
        proj.DistanceTraveled += proj.Step;

        if (proj is { ArcRatioV: not null, ArcRatioH: not null, InitialDistance: > 0 })
        {
            var progress = Math.Clamp(proj.DistanceTraveled / proj.InitialDistance, 0f, 1f);
            var arcHeight = proj.InitialDistance * proj.ArcRatioV.Value / proj.ArcRatioH.Value / 2f;
            var arcOffset = MathF.Sin(MathF.PI * progress) * arcHeight;

            //perpendicular to heading (rotate 90°)
            proj.ArcOffsetX = -unitY * arcOffset;
            proj.ArcOffsetY = unitX * arcOffset;
        }

        if (targetEntity is not null)
        {
            var projTile = Camera.WorldToTile(proj.CurrentX, proj.CurrentY, MapFile!.Height);

            proj.Direction = GetProjectileDirection(
                targetEntity.TileX - projTile.X,
                targetEntity.TileY - projTile.Y);
        }

        if (proj.FramesPerDirection > 1)
            proj.CurrentFrameCycle = (proj.CurrentFrameCycle + 1) % proj.FramesPerDirection;
    }
    #endregion

    /// <summary>
    ///     Returns the first visible modal panel among Root's children (highest ZIndex first), or null.
    /// </summary>
    private UIPanel? FindVisibleModal()
    {
        if (Root is null)
            return null;

        UIPanel? best = null;

        foreach (var child in Root.Children)
            if (child is UIPanel { Visible: true, IsModal: true } panel && (best is null || (panel.ZIndex > best.ZIndex)))
                best = panel;

        return best;
    }

}