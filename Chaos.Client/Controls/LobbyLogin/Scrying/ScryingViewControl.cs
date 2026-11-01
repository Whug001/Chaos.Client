#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Models;
using Chaos.Client.Screens;
using Chaos.Client.Systems;
using Chaos.DarkAges.Definitions;
using DALib.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.LobbyLogin.Scrying;

/// <summary>Live view of one player's surroundings, cut to the start screen's logo oval.</summary>
public sealed class ScryingViewControl : UIElement
{
    private const float FADE_IN_MS = 500f;

    //values below 1 show more of the map
    private const float ZOOM = 0.8f;

    private static readonly BlendState MaskBlend = new()
    {
        ColorSourceBlend = Blend.DestinationAlpha,
        ColorDestinationBlend = Blend.Zero,
        AlphaSourceBlend = Blend.DestinationAlpha,
        AlphaDestinationBlend = Blend.Zero
    };

    private readonly ChaosGame Game;
    private readonly BatchBlendScope BlendScope = new();
    private Camera Camera = null!;
    private float Fade;
    private MapFile? MapFile;
    private MapRenderer? MapRenderer;
    private Texture2D? Mask;
    private RenderTarget2D? OvalTarget;
    private RenderTarget2D? SceneTarget;
    private static readonly ClientEntityType[] EntityPaintOrder =
    [
        ClientEntityType.GroundItem,
        ClientEntityType.Aisling,
        ClientEntityType.Creature
    ];

    private int AnimationTick;

    public ScryScene Scene { get; } = new();

    private bool MapUsable => MapFile is not null;
    private bool Showing => Scene.HasView && MapUsable && Scene.Target is not null;

    private int SceneWidth => (int)Math.Round(Width / ZOOM);
    private int SceneHeight => (int)Math.Round(Height / ZOOM);

    public ScryingViewControl(ChaosGame game, UIElement logoSlot)
    {
        Game = game;
        Name = "ScryingView";
        X = logoSlot.X;
        Y = logoSlot.Y;
        Width = logoSlot.Width;
        Height = logoSlot.Height;

        //decorative overlay over the logo: never steal clicks from the start panel buttons
        IsHitTestVisible = false;

        //scene pixels are downscaled by ZOOM into the oval, so scale the offset up: the player
        //still lands at (-28, 24) oval pixels from center, the way WorldScreen centers it
        Camera = new Camera(SceneWidth, SceneHeight) { Offset = new Vector2(-28, 24) / ZOOM };
        Scene.MapChanged += OnMapChanged;
        Scene.TargetChanged += () => Fade = 0f;
    }

    private void OnMapChanged()
    {
        MapFile = WorldScreen.LoadMapFile(Scene.MapId, Scene.Width, Scene.Height, Scene.CheckSum);
        Fade = 0f;

        if (MapFile is null)
            return;

        //PreloadMapTiles accumulates on a reused renderer: it builds new atlases without
        //disposing the old ones and keeps every fallback texture and cycling variant. WorldScreen
        //avoids that by disposing and rebuilding its MapRenderer on every map change; do the same
        //here, since MapChanged fires every few seconds to minutes while watching.
        MapRenderer?.Dispose();
        MapRenderer = new MapRenderer();

        //same preload WorldScreen runs once it has a MapFile: atlas the map's tiles (plus door-swap
        //variants that can appear at runtime) so background/foreground draws hit the atlas path
        MapRenderer.PreloadMapTiles(
            ChaosGame.Device,
            MapFile,
            null,
            static id => DoorTable.GetVariants((short)id).Select(static v => (int)v));
    }

    public override void Update(GameTime gameTime)
    {
        var elapsed = gameTime.ElapsedGameTime.TotalMilliseconds;
        Scene.Update(elapsed);

        if (!Scene.HasView)
            MapFile = null;

        Fade = Showing ? Math.Min(1f, Fade + (float)(elapsed / FADE_IN_MS)) : 0f;

        //same 100 ms tile animation tick WorldScreen.Update advances, driving animated tiles
        AnimationTick = (int)(gameTime.TotalGameTime.TotalMilliseconds / 100);
        MapRenderer?.UpdatePaletteCycling(AnimationTick);
    }

    /// <summary>Call before the screen's SpriteBatch.Begin.</summary>
    public void RenderOffscreen(SpriteBatch spriteBatch)
    {
        if (!Showing || (Fade <= 0f))
            return;

        var device = spriteBatch.GraphicsDevice;
        EnsureTargets(device);

        var bindings = device.GetRenderTargets();
        var previous = bindings.Length > 0 ? bindings[0].RenderTarget as RenderTarget2D : null;

        device.SetRenderTarget(SceneTarget);
        device.Clear(Color.Black);
        DrawScene(spriteBatch);

        device.SetRenderTarget(OvalTarget);
        device.Clear(Color.Transparent);
        spriteBatch.Begin(blendState: BlendState.Opaque);
        spriteBatch.Draw(Mask, Vector2.Zero, Color.White);
        spriteBatch.End();
        spriteBatch.Begin(blendState: MaskBlend, samplerState: GlobalSettings.Sampler);
        spriteBatch.Draw(SceneTarget, new Rectangle(0, 0, Width, Height), Color.White);
        spriteBatch.End();

        device.SetRenderTarget(previous);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible || !Showing || (Fade <= 0f) || OvalTarget is null)
            return;

        spriteBatch.Draw(OvalTarget, new Rectangle(ScreenX, ScreenY, Width, Height), Color.White * Fade);
    }

    private void DrawScene(SpriteBatch spriteBatch)
    {
        var target = Scene.Target!;

        //center on the watched player's tile plus its walk-slide offset, so walking is followed smoothly
        Camera.Position = Camera.TileToWorld(target.TileX, target.TileY, MapFile!.Height) + target.VisualOffset;

        spriteBatch.Begin(samplerState: GlobalSettings.Sampler);
        MapRenderer!.DrawBackground(spriteBatch, MapFile, Camera, AnimationTick);
        spriteBatch.End();

        //foreground tiles and entities share one diagonal stripe pass (depth = tileX + tileY) so tall
        //foregrounds occlude entities behind them, the way WorldScreen.DrawForegroundAndEntities does
        var bounds = Camera.GetVisibleTileBounds(MapFile.Width, MapFile.Height, MapRenderer.ForegroundExtraMargin);
        var byTile = Scene.Entities.ToLookup(static e => (e.TileX, e.TileY));

        BlendScope.Begin(spriteBatch, BlendState.AlphaBlend, GlobalSettings.Sampler, RasterizerState.CullNone, Matrix.Identity);

        try
        {
            for (var depth = bounds.MinX + bounds.MinY; depth <= bounds.MaxX + bounds.MaxY; depth++)
                for (var tileX = bounds.MinX; tileX <= bounds.MaxX; tileX++)
                {
                    var tileY = depth - tileX;

                    if ((tileY < bounds.MinY) || (tileY > bounds.MaxY))
                        continue;

                    MapRenderer.DrawForegroundTile(BlendScope, MapFile, Camera, tileX, tileY, AnimationTick);

                    //ground items underneath, then creatures and aislings, in EntityPaintOrder.
                    //never names, name tags, chat bubbles, health bars or group boxes: frames carry none
                    foreach (var type in EntityPaintOrder)
                    foreach (var entity in byTile[(tileX, tileY)])
                    {
                        if (entity.Type != type)
                            continue;

                        var tileWorld = Camera.TileToWorld(tileX, tileY, MapFile.Height);
                        var tileCenterX = tileWorld.X + DaLibConstants.HALF_TILE_WIDTH;
                        var tileCenterY = tileWorld.Y + DaLibConstants.HALF_TILE_HEIGHT;
                        DrawScryEntity(BlendScope.Batch, entity, tileCenterX, tileCenterY);
                    }
                }
        } finally
        {
            BlendScope.End();
        }
    }

    private void DrawScryEntity(SpriteBatch spriteBatch, WorldEntity entity, float tileCenterX, float tileCenterY)
    {
        switch (entity.Type)
        {
            case ClientEntityType.GroundItem:
                DrawScryGroundItem(spriteBatch, entity, tileCenterX, tileCenterY);

                break;
            case ClientEntityType.Creature:
                DrawScryCreature(spriteBatch, entity, tileCenterX, tileCenterY);

                break;
            case ClientEntityType.Aisling:
                DrawScryAisling(spriteBatch, entity, tileCenterX, tileCenterY);

                break;
        }
    }

    private void DrawScryGroundItem(
        SpriteBatch spriteBatch,
        WorldEntity entity,
        float tileCenterX,
        float tileCenterY)
    {
        //same call WorldScreen.DrawGroundItem makes; swim tiles never host scry items, but keep the guard
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

    private void DrawScryCreature(
        SpriteBatch spriteBatch,
        WorldEntity entity,
        float tileCenterX,
        float tileCenterY)
    {
        //same body as WorldScreen.DrawCreature, but nothing is ever tinted, hovered or grouped here
        var creatureRenderer = Game.CreatureRenderer;
        var animInfo = creatureRenderer.GetAnimInfo(entity.SpriteId);

        if (animInfo is null)
            return;

        var info = animInfo.Value;
        (var frameIndex, var flip) = AnimationSystem.GetCreatureFrame(entity, in info);

        var alpha = entity.IsHidden ? 0f : entity.IsTransparent ? 1f / 3f : 1f;
        var groundPaintHeight = entity.IsOnSwimmingTile ? 0 : entity.GroundPaintHeight;

        creatureRenderer.Draw(
            spriteBatch,
            Camera,
            entity.SpriteId,
            frameIndex,
            flip,
            tileCenterX,
            tileCenterY,
            entity.VisualOffset,
            EntityTintType.None,
            default,
            groundPaintHeight,
            entity.GroundTintColor,
            alpha);
    }

    private void DrawScryAisling(
        SpriteBatch spriteBatch,
        WorldEntity entity,
        float tileCenterX,
        float tileCenterY)
    {
        if (entity.IsHidden)
            return;

        //morphed aislings (creature form) render as creatures
        if (entity.Appearance is null && entity is { SpriteId: > 0, IsOnSwimmingTile: false })
        {
            DrawScryCreature(spriteBatch, entity, tileCenterX, tileCenterY);

            return;
        }

        if (entity.Appearance is null && !entity.IsOnSwimmingTile)
            return;

        var appearance = entity.Appearance ?? default;
        (var frameIndex, var flip, var animSuffix, var isFrontFacing) = AnimationSystem.GetAislingFrame(entity);

        //swimming override: single sprite replaces all aisling layers
        if (entity.IsOnSwimmingTile)
        {
            var isFemale = entity.Appearance?.Gender == Gender.Female;
            var dirIndex = isFrontFacing ? 1 : 0;

            var swimFrameCount = Game.AislingRenderer.GetSwimFrameCount(isFemale);
            var framesPerDir = swimFrameCount / 2;

            if (framesPerDir <= 0)
                return;

            var walkFrames = framesPerDir - 1;

            var animIndex = walkFrames > 0
                ? 1 + (entity.AnimState == EntityAnimState.Walking ? entity.AnimFrameIndex % walkFrames : entity.IdleAnimTick % walkFrames)
                : 0;

            var swimFrame = dirIndex * framesPerDir + animIndex;

            Game.AislingRenderer.DrawSwimming(
                spriteBatch,
                Camera,
                isFemale,
                swimFrame,
                flip,
                tileCenterX,
                tileCenterY,
                entity.VisualOffset);

            return;
        }

        //rest position override: single spf sprite replaces all aisling layers
        if (entity.RestPosition != RestPosition.None)
        {
            Game.AislingRenderer.DrawResting(
                spriteBatch,
                Camera,
                entity.Appearance?.Gender == Gender.Female,
                entity.RestPosition,
                isFrontFacing,
                flip,
                tileCenterX,
                tileCenterY,
                entity.VisualOffset,
                entity.VisibleEmoteFrame);

            return;
        }

        //fishing poses and custom emotes are skipped: scry frames only carry look and position,
        //so those states never occur here
        var drawParams = new AislingDrawParams(
            entity.RenderCacheId,
            appearance,
            frameIndex,
            flip,
            isFrontFacing,
            animSuffix,
            entity.VisibleEmoteFrame,
            entity.GroundPaintHeight,
            entity.GroundTintColor,
            tileCenterX,
            tileCenterY,
            entity.VisualOffset,
            EntityTintType.None,
            default,
            entity.IsDead,
            entity is { IsTransparent: true, IsDead: false } ? 1f / 3f : 1f);

        Game.AislingRenderer.Draw(spriteBatch, Camera, in drawParams);
    }

    private void EnsureTargets(GraphicsDevice device)
    {
        Camera.Resize(SceneWidth, SceneHeight);
        SceneTarget ??= new RenderTarget2D(device, SceneWidth, SceneHeight);
        OvalTarget ??= new RenderTarget2D(device, Width, Height);
        Mask ??= BuildOvalMask(device, Width, Height);
    }

    //Mask matches _nslogo.spf frame 0: the transparent/art edge fits cx=147.5 cy=132.5
    //rx=148 ry=133 over 1003 edge points (rms 0.005), so the oval fills the 296x266
    //box and the full-size ellipse stands.
    internal static Color[] OvalMaskPixels(int width, int height)
    {
        var pixels = new Color[width * height];
        var rx = width / 2f;
        var ry = height / 2f;

        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var nx = (x + 0.5f - rx) / rx;
                var ny = (y + 0.5f - ry) / ry;
                var distance = MathF.Sqrt(nx * nx + ny * ny);
                var edge = Math.Clamp((1f - distance) * rx / 2f, 0f, 1f);
                pixels[y * width + x] = Color.White * edge;
            }

        return pixels;
    }

    private static Texture2D BuildOvalMask(GraphicsDevice device, int width, int height)
    {
        var texture = new Texture2D(device, width, height);
        texture.SetData(OvalMaskPixels(width, height));

        return texture;
    }

    public override void Dispose()
    {
        SceneTarget?.Dispose();
        OvalTarget?.Dispose();
        Mask?.Dispose();
        MapRenderer?.Dispose();
        base.Dispose();
    }
}
