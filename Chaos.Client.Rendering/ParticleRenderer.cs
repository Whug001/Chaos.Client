#region
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Full-viewport ambient particle overlay — the renderer behind ash, embers, leaves, petals, fireflies, bubbles
///     and blowing sand, each a <see cref="ParticleStyle" /> preset. Particles are drawn from small procedural
///     textures (no sprite assets), move with a base velocity plus a sine sway, and wrap around the viewport edges.
///     Camera movement shifts every particle by the same amount, so they drift over the MAP rather than being glued
///     to the screen. The whole effect fades in/out over <see cref="ParticleStyle.FadeSeconds" />. Touched only on
///     the game-loop thread.
///     A style can also give each particle a pair (or two) of flapping wings.
/// </summary>
public sealed class ParticleRenderer : IAmbientOverlay
{
    private const float REFERENCE_AREA = 640f * 480f; // viewport area ParticleStyle.Count is tuned for

    /// <summary>
    ///     How far the reference-area density may scale up on very large viewports. Normal window sizes keep
    ///     their current density; anything larger is capped at this multiple instead of scaling without limit.
    /// </summary>
    internal const int MAX_AREA_SCALE = 4;

    internal static int CappedCount(ParticleStyle style, int width, int height)
    {
        var scaled = (int)MathF.Round(style.Count * (width * height / REFERENCE_AREA));

        return Math.Max(1, Math.Min(scaled, style.Count * MAX_AREA_SCALE));
    }
    internal const float WRAP_MARGIN = 24f;            // px past each edge before a particle wraps, so it never pops in view
    private const int SOFT_TEX = 32;                  // soft dot / bubble texture size
    private const int LEAF_TEX_W = 16;
    private const int LEAF_TEX_H = 8;
    private const int WING_TEX_W = 32;
    private const int WING_TEX_H = 16;
    internal const int BALLOON_TEX_W = 16;         // balloon body + string texture; the body fills the top half
    internal const int BALLOON_TEX_H = 40;
    private static readonly Color BalloonDetailColor = new(240, 240, 240);
    private const float UPPER_WING_ANGLE = -0.45f; // radians from horizontal for the right wing; negative tilts up
    private const float LOWER_WING_ANGLE = 0.55f;
    private const float LOWER_WING_LENGTH = 0.7f;  // lower wings are this fraction of the upper ones
    private const float LOWER_WING_WIDTH = 0.8f;
    private const float WING_PALE = 0.55f;         // how far the wing color moves from the particle color to white
    private const float GHOST_FRAME_RATE = 2.2f;   // ghost hem frames per second

    private static readonly Color GhostDetail = new(36, 34, 58); // ghost eyes and mouth
    private static readonly Color FeatherBarDetail = new(110, 100, 95); // multiplied by the feather color: darker bars
    private static readonly Color FeatherTipColor = new(232, 222, 198); // cream quill and tip
    private static readonly Color WheatStalkDetail = new(150, 120, 60);
    private const float GUST_GATE_FADE = 0.15f; // gust level above GustGate over which gated particles fade in

    private readonly ParticleStyle Style;
    private readonly Random Rng = new();

    private Particle[] Particles = [];
    private int ParticlesForWidth;
    private int ParticlesForHeight;
    private Vector2? LastWorldOrigin;

    private Texture2D? PixelTexture;
    private Texture2D? SoftDotTexture;
    private Texture2D? LeafTexture;
    private Texture2D? BubbleTexture;
    private Texture2D? WingTexture;
    private Texture2D? BalloonTexture;
    private Texture2D? BalloonDetailTexture;
    private Texture2D[]? GhostTextures;
    private Texture2D[]? FeatherTextures; // body, then tip
    private Texture2D? WheatHeadTexture;

    private bool Active;
    private float EffectAlpha; // 0..1 current fade level
    private float Clock;       // seconds, drives sway / twinkle / tumble
    private float GustLevel;   // 0..1, read from total game time so every renderer gusts together

    private struct Particle
    {
        public Vector2 Position; // relative to the viewport's top-left
        public Vector2 Velocity;
        public float Size;
        public Color Color;
        public float Alpha;
        public float SwayFreq;
        public float SwayPhase;
        public float Rotation;
        public float SpinRate;
        public float TwinkleFreq;
        public float TwinklePhase;
        public float FlapFreq;
        public float FlapPhase;
    }

    public ParticleRenderer(ParticleStyle style) => Style = style;

    /// <inheritdoc />
    public BlendState BlendState => Style.Additive ? BlendState.Additive : BlendState.NonPremultiplied;

    /// <inheritdoc />
    public bool IsActive => Active || (EffectAlpha > 0f);

    /// <inheritdoc />
    public void SetActive(bool on, bool immediate = false)
    {
        Active = on;

        if (immediate)
            EffectAlpha = on ? 1f : 0f;
    }

    /// <inheritdoc />
    public void Update(GameTime gameTime, Rectangle viewport, Vector2 worldOrigin)
    {
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (dt <= 0f)
            return;

        var target = Active ? 1f : 0f;
        var step = dt / Style.FadeSeconds;

        if (EffectAlpha < target)
            EffectAlpha = MathF.Min(target, EffectAlpha + step);
        else if (EffectAlpha > target)
            EffectAlpha = MathF.Max(target, EffectAlpha - step);

        if (!IsActive || (viewport.Width <= 0) || (viewport.Height <= 0))
        {
            //forget the camera so the next activation doesn't shift everything by the distance walked meanwhile
            LastWorldOrigin = null;

            return;
        }

        if ((viewport.Width != ParticlesForWidth) || (viewport.Height != ParticlesForHeight))
            Spawn(viewport.Width, viewport.Height);

        //camera pan: move particles with the world. a jump bigger than the viewport is a teleport or map change —
        //skip it, the wrap below would only scramble positions anyway
        var cameraShift = LastWorldOrigin is { } last ? worldOrigin - last : Vector2.Zero;

        if ((MathF.Abs(cameraShift.X) > viewport.Width) || (MathF.Abs(cameraShift.Y) > viewport.Height))
            cameraShift = Vector2.Zero;

        LastWorldOrigin = worldOrigin;
        Clock = (Clock + dt) % 10000f;
        GustLevel = Gust(gameTime.TotalGameTime.TotalSeconds);

        var spanW = viewport.Width + (2f * WRAP_MARGIN);
        var spanH = viewport.Height + (2f * WRAP_MARGIN);

        for (var i = 0; i < Particles.Length; i++)
        {
            ref var p = ref Particles[i];

            p.Position += cameraShift + (CurrentVelocity(in p) * dt);
            p.Rotation = Style.SeeSaw > 0f
                ? SeeSawTilt(Style.SeeSaw, SwayAngle(in p))
                : (p.Rotation + (p.SpinRate * dt)) % MathF.Tau;

            p.Position.X = Wrap(p.Position.X, spanW);
            p.Position.Y = Wrap(p.Position.Y, spanH);
        }
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, Rectangle viewport, Vector2 worldOrigin)
    {
        if ((EffectAlpha <= 0f) || (Particles.Length == 0) || (viewport.Width <= 0) || (viewport.Height <= 0))
            return;

        var device = spriteBatch.GraphicsDevice;
        var texture = GetShapeTexture(device);
        var textureCenter = new Vector2(texture.Width / 2f, texture.Height / 2f);
        var origin = new Vector2(viewport.X, viewport.Y);

        foreach (var p in Particles)
        {
            var alpha = p.Alpha * TwinkleLevel(in p) * EffectAlpha * GustAlphaFactor(Style, GustLevel);

            if (alpha <= 0.004f)
                continue;

            var position = origin + p.Position;

            if (Style.HaloScale > 0f)
            {
                SoftDotTexture ??= BuildSoftDotTexture(device);

                spriteBatch.Draw(
                    SoftDotTexture,
                    position,
                    null,
                    WithAlpha(p.Color, alpha * Style.HaloAlpha),
                    0f,
                    new Vector2(SOFT_TEX / 2f),
                    p.Size * Style.HaloScale / SOFT_TEX,
                    SpriteEffects.None,
                    0f);
            }

            if (Style.WingScale > 0f)
            {
                WingTexture ??= BuildWingTexture(device);
                DrawWings(spriteBatch, in p, position, alpha);
            }

            if (Style.Shape == ParticleShape.Ghost)
            {
                DrawGhost(spriteBatch, in p, position, alpha);

                continue;
            }

            //whole-pixel positions keep the feather's bars from shimmering as it drifts
            if (Style.Shape == ParticleShape.Feather)
                position = new Vector2(MathF.Round(position.X), MathF.Round(position.Y));

            var (rotation, scale) = ShapeTransform(in p, texture);

            spriteBatch.Draw(
                texture,
                position,
                null,
                WithAlpha(p.Color, alpha),
                rotation,
                textureCenter,
                scale,
                SpriteEffects.None,
                0f);
            if (Style.Shape == ParticleShape.Balloon)
            {
                BalloonDetailTexture ??= BuildBalloonDetailTexture(device);

                spriteBatch.Draw(
                    BalloonDetailTexture,
                    position,
                    null,
                    WithAlpha(BalloonDetailColor, alpha),
                    rotation,
                    textureCenter,
                    scale,
                    SpriteEffects.None,
                    0f);
            }

            if (Style.Shape == ParticleShape.Feather)
                spriteBatch.Draw(
                    FeatherTextures![1],
                    position,
                    null,
                    WithAlpha(FeatherTipColor, alpha),
                    rotation,
                    textureCenter,
                    scale,
                    SpriteEffects.None,
                    0f);
        }
    }

    /// <inheritdoc />
    /// <summary>
    ///     Releases the cached GPU textures without deactivating the effect; they rebuild lazily on the next
    ///     draw. Called for effects no longer active after a map change, so the old map's textures don't linger.
    /// </summary>
    public void ReleaseTextures()
    {
        PixelTexture?.Dispose();
        PixelTexture = null;
        SoftDotTexture?.Dispose();
        SoftDotTexture = null;
        LeafTexture?.Dispose();
        LeafTexture = null;
        BubbleTexture?.Dispose();
        BubbleTexture = null;
        WingTexture?.Dispose();
        WingTexture = null;
        BalloonTexture?.Dispose();
        BalloonTexture = null;
        BalloonDetailTexture?.Dispose();
        BalloonDetailTexture = null;

        if (GhostTextures is not null)
            foreach (var ghostTexture in GhostTextures)
                ghostTexture.Dispose();

        GhostTextures = null;

        if (FeatherTextures is not null)
            foreach (var featherTexture in FeatherTextures)
                featherTexture.Dispose();

        FeatherTextures = null;
        WheatHeadTexture?.Dispose();
        WheatHeadTexture = null;
    }

    internal bool HasAllocatedTextures => (PixelTexture is not null)
                                          || (SoftDotTexture is not null)
                                          || (LeafTexture is not null)
                                          || (BubbleTexture is not null)
                                          || (WingTexture is not null)
                                          || (BalloonTexture is not null)
                                          || (BalloonDetailTexture is not null)
                                          || (GhostTextures is not null)
                                          || (FeatherTextures is not null)
                                          || (WheatHeadTexture is not null);

    public void Dispose()
    {
        PixelTexture?.Dispose();
        PixelTexture = null;
        SoftDotTexture?.Dispose();
        SoftDotTexture = null;
        LeafTexture?.Dispose();
        LeafTexture = null;
        BubbleTexture?.Dispose();
        BubbleTexture = null;
        WingTexture?.Dispose();
        WingTexture = null;
        BalloonTexture?.Dispose();
        BalloonTexture = null;
        BalloonDetailTexture?.Dispose();
        BalloonDetailTexture = null;

        if (GhostTextures is not null)
            foreach (var ghostTexture in GhostTextures)
                ghostTexture.Dispose();

        GhostTextures = null;

        if (FeatherTextures is not null)
            foreach (var featherTexture in FeatherTextures)
                featherTexture.Dispose();

        FeatherTextures = null;
        WheatHeadTexture?.Dispose();
        WheatHeadTexture = null;
    }

    // ============================================================

    //(re)creates particles scattered over the whole viewport, count scaled by viewport area
    private void Spawn(int width, int height)
    {
        ParticlesForWidth = width;
        ParticlesForHeight = height;

        var count = CappedCount(Style, width, height);
        Particles = new Particle[count];

        for (var i = 0; i < count; i++)
            Particles[i] = new Particle
            {
                Position = new Vector2(
                    Roll(-WRAP_MARGIN, width + WRAP_MARGIN),
                    Roll(-WRAP_MARGIN, height + WRAP_MARGIN)),
                Velocity = new Vector2(
                    Roll(Style.VelocityMin.X, Style.VelocityMax.X),
                    Roll(Style.VelocityMin.Y, Style.VelocityMax.Y)),
                Size = Roll(Style.SizeMin, Style.SizeMax),
                Color = Style.Colors[Rng.Next(Style.Colors.Length)],
                Alpha = Roll(Style.AlphaMin, Style.AlphaMax),
                SwayFreq = Roll(Style.SwayFreqMin, Style.SwayFreqMax),
                SwayPhase = Roll(0f, MathF.Tau),
                Rotation = Roll(0f, MathF.Tau),
                SpinRate = Roll(-Style.Spin, Style.Spin),
                TwinkleFreq = Roll(Style.TwinkleFreqMin, Style.TwinkleFreqMax),
                TwinklePhase = Roll(0f, MathF.Tau),
                FlapFreq = Roll(Style.WingFlapMin, Style.WingFlapMax),
                FlapPhase = Roll(0f, MathF.Tau)
            };
    }

    /// <summary>
    ///     The tilt, in radians, of a see-sawing particle at <paramref name="swayAngle" />. It follows the sideways sway
    ///     speed: level at each end of the swing and tilted most mid-swing, toward the way it is moving.
    /// </summary>
    internal static float SeeSawTilt(float seeSaw, float swayAngle) => seeSaw * MathF.Sin(swayAngle);

    /// <summary>
    ///     The fall-speed multiplier of a see-sawing particle at <paramref name="swayAngle" />: 0.5 at each end of the
    ///     swing, where it nearly hangs, and 1.4 mid-swing.
    /// </summary>
    internal static float SeeSawDrop(float swayAngle) => 0.5f + (0.9f * MathF.Abs(MathF.Sin(swayAngle)));

    /// <summary>
    ///     The strength of the breeze, 0 (lull) to 1 (strongest gust), at <paramref name="seconds" /> of total game
    ///     time. Two slow waves of different lengths, so gusts come unevenly. Every renderer reads the same value.
    /// </summary>
    public static float Gust(double seconds)
        => (float)Math.Clamp(0.5 + (0.3 * Math.Sin(0.83 * seconds)) + (0.2 * Math.Sin((1.9 * seconds) + 1.3)), 0.0, 1.0);

    /// <summary>The sideways speed multiplier for <paramref name="style" /> at gust level <paramref name="gust" />.</summary>
    internal static float GustSpeedFactor(ParticleStyle style, float gust) => MathHelper.Lerp(style.GustLow, style.GustHigh, gust);

    /// <summary>
    ///     The opacity multiplier for <paramref name="style" /> at gust level <paramref name="gust" />: 1 unless the
    ///     style has a <see cref="ParticleStyle.GustGate" />, and then 0 below the gate rising smoothly to 1.
    /// </summary>
    internal static float GustAlphaFactor(ParticleStyle style, float gust)
    {
        if (style.GustGate <= 0f)
            return 1f;

        var t = Math.Clamp((gust - style.GustGate) / GUST_GATE_FADE, 0f, 1f);

        return t * t * (3f - (2f * t));
    }

    private float SwayAngle(in Particle p) => (MathF.Tau * p.SwayFreq * Clock) + p.SwayPhase;

    //base velocity plus sway. Y sways at a different rate from X so two-axis sway traces a wandering loop
    private Vector2 CurrentVelocity(in Particle p)
    {
        var angle = SwayAngle(in p);
        var velocity = p.Velocity;

        if (Style.SeeSaw > 0f)
            velocity.Y *= SeeSawDrop(angle);

        velocity.X *= GustSpeedFactor(Style, GustLevel);

        return velocity + new Vector2(Style.Sway.X * MathF.Sin(angle), Style.Sway.Y * MathF.Sin((angle * 0.77f) + 1.3f));
    }

    //brightness multiplier [0..1] from the style's blink settings
    private float TwinkleLevel(in Particle p)
    {
        if (Style.Twinkle <= 0f)
            return 1f;

        var wave = 0.5f + (0.5f * MathF.Sin((MathF.Tau * p.TwinkleFreq * Clock) + p.TwinklePhase));

        return 1f - (Style.Twinkle * (1f - MathF.Pow(wave, Style.TwinkleSharpness)));
    }

    //rotation and scale for the particle's shape. leaves and petals squash on one axis as they spin, so they read
    //as flat things tumbling rather than stickers turning
    private (float Rotation, Vector2 Scale) ShapeTransform(in Particle p, Texture2D texture)
    {
        var tumble = 0.25f + (0.75f * MathF.Abs(MathF.Cos((Clock * 1.7f) + p.SwayPhase)));

        return Style.Shape switch
        {
            ParticleShape.Square => (0f, new Vector2(p.Size)),
            ParticleShape.Streak => (MathF.Atan2(CurrentVelocity(in p).Y, CurrentVelocity(in p).X), new Vector2(p.Size, 1f)),
            ParticleShape.Leaf   => (p.Rotation, new Vector2(p.Size / texture.Width, p.Size / texture.Width * tumble)),
            ParticleShape.Petal  => (p.Rotation, new Vector2(p.Size / texture.Width, p.Size * 0.6f / texture.Width * tumble)),
            ParticleShape.Confetti => (p.Rotation, new Vector2(p.Size * 0.55f, p.Size * tumble)),
            ParticleShape.Feather => (p.Rotation, new Vector2(p.Size / texture.Width)),
            ParticleShape.WheatHead => (p.Rotation, Vector2.One),
            _                    => (0f, new Vector2(p.Size / texture.Width))
        };
    }

    //wings sit on the body and sweep between nearly edge-on (0.2) and fully open (1). the texture's narrow end is the
    //draw origin, so each wing grows outward from the particle
    private void DrawWings(SpriteBatch spriteBatch, in Particle p, Vector2 position, float alpha)
    {
        var flap = 0.2f + (0.8f * MathF.Abs(MathF.Sin((MathF.Tau * p.FlapFreq * Clock) + p.FlapPhase)));
        var length = p.Size * Style.WingScale * flap;
        var width = p.Size * Style.WingWidth;
        var color = WithAlpha(Color.Lerp(p.Color, Color.White, WING_PALE), alpha * Style.WingAlpha);

        DrawWingPair(spriteBatch, position, color, UPPER_WING_ANGLE, length, width);

        if (Style.WingPairs > 1)
            DrawWingPair(spriteBatch, position, color, LOWER_WING_ANGLE, length * LOWER_WING_LENGTH, width * LOWER_WING_WIDTH);
    }

    //right wing at the given angle, left wing mirrored across the vertical. the wing is symmetric along its length, so
    //turning it to PI - angle mirrors it exactly
    private void DrawWingPair(SpriteBatch spriteBatch, Vector2 position, Color color, float angle, float length, float width)
    {
        var origin = new Vector2(0f, WING_TEX_H / 2f);
        var scale = new Vector2(length / WING_TEX_W, width / WING_TEX_H);

        spriteBatch.Draw(WingTexture!, position, null, color, angle, origin, scale, SpriteEffects.None, 0f);
        spriteBatch.Draw(WingTexture!, position, null, color, MathF.PI - angle, origin, scale, SpriteEffects.None, 0f);
    }

    /// <summary>
    ///     The ghost frame shown at <paramref name="clock" /> seconds for a particle with sway phase
    ///     <paramref name="phase" />. The phase shifts each ghost's hem so they don't ripple in step.
    /// </summary>
    public static int GhostFrameAt(float clock, float phase)
        => (int)((clock * GHOST_FRAME_RATE) + phase) % SpriteGrid.GhostFrames.Count;

    //1:1 pixel sprite at a whole-pixel position, so it stays crisp
    private void DrawGhost(SpriteBatch spriteBatch, in Particle p, Vector2 position, float alpha)
    {
        var frame = GhostTextures![GhostFrameAt(Clock, p.SwayPhase)];
        var topLeft = new Vector2(MathF.Round(position.X - (frame.Width / 2f)), MathF.Round(position.Y - (frame.Height / 2f)));

        spriteBatch.Draw(frame, topLeft, WithAlpha(p.Color, alpha));
    }

    private static Texture2D[] BuildGhostTextures(GraphicsDevice device)
        => SpriteGrid.GhostFrames
                      .Select(rows => SpriteGrid.Build(device, rows, GhostDetail))
                      .ToArray();

    //the body takes the particle color, with darker bars; the tip is drawn over it in cream
    private static Texture2D[] BuildFeatherTextures(GraphicsDevice device)
        =>
        [
            SpriteGrid.Build(device, SpriteGrid.FeatherBody, FeatherBarDetail),
            SpriteGrid.Build(device, SpriteGrid.FeatherTip, Color.White)
        ];

    private Texture2D GetShapeTexture(GraphicsDevice device)
        => Style.Shape switch
        {
            ParticleShape.Square or ParticleShape.Streak or ParticleShape.Confetti => PixelTexture ??= BuildPixelTexture(device),
            ParticleShape.Leaf                           => LeafTexture ??= BuildLeafTexture(device),
            ParticleShape.Ghost                          => (GhostTextures ??= BuildGhostTextures(device))[0],
            ParticleShape.Bubble                         => BubbleTexture ??= BuildBubbleTexture(device),
            ParticleShape.Balloon                        => BalloonTexture ??= BuildBalloonTexture(device),
            ParticleShape.Feather                        => (FeatherTextures ??= BuildFeatherTextures(device))[0],
            ParticleShape.WheatHead                      => WheatHeadTexture ??= SpriteGrid.Build(device, SpriteGrid.WheatHead, WheatStalkDetail),
            _                                            => SoftDotTexture ??= BuildSoftDotTexture(device)
        };

    private float Roll(float min, float max) => min + ((float)Rng.NextDouble() * (max - min));

    //wraps a viewport-relative coordinate into [-WRAP_MARGIN, span - WRAP_MARGIN)
    private static float Wrap(float value, float span) => ((((value + WRAP_MARGIN) % span) + span) % span) - WRAP_MARGIN;

    private static Color WithAlpha(Color color, float alpha)
        => new(color.R, color.G, color.B, (byte)Math.Clamp((int)(255f * alpha), 0, 255));

    // ============================================================
    // Procedural textures — white RGB, shape carried in alpha, tinted per particle at draw time
    // ============================================================

    private static Texture2D BuildPixelTexture(GraphicsDevice device)
    {
        var tex = new Texture2D(device, 1, 1);
        tex.SetData([Color.White]);

        return tex;
    }

    //radial falloff: opaque center fading to clear at the edge
    private static Texture2D BuildSoftDotTexture(GraphicsDevice device)
        => BuildTexture(
            device,
            SOFT_TEX,
            SOFT_TEX,
            (u, v) =>
            {
                var t = Math.Clamp(1f - MathF.Sqrt((u * u) + (v * v)), 0f, 1f);

                return t * t;
            });

    //thin bright rim, faint fill, and a small highlight up-left
    private static Texture2D BuildBubbleTexture(GraphicsDevice device)
        => BuildTexture(
            device,
            SOFT_TEX,
            SOFT_TEX,
            (u, v) =>
            {
                var r = MathF.Sqrt((u * u) + (v * v));

                if (r > 1f)
                    return 0f;

                var rim = Math.Clamp(1f - (MathF.Abs(r - 0.82f) / 0.16f), 0f, 1f);
                var hu = u + 0.35f;
                var hv = v + 0.35f;
                var highlight = Math.Clamp(1f - (MathF.Sqrt((hu * hu) + (hv * hv)) / 0.22f), 0f, 1f);

                return MathF.Max(0.08f, MathF.Max(rim, highlight));
            });

    //hard-edged pointed lens (leaf outline): inside where |v| <= 1 - u²
    private static Texture2D BuildLeafTexture(GraphicsDevice device)
        => BuildTexture(device, LEAF_TEX_W, LEAF_TEX_H, (u, v) => MathF.Abs(v) <= 1f - (u * u) ? 1f : 0f);

    //soft-edged teardrop: narrow where it meets the body (left edge), round at the tip, a little brighter toward the tip
    private static Texture2D BuildWingTexture(GraphicsDevice device)
        => BuildTexture(
            device,
            WING_TEX_W,
            WING_TEX_H,
            (u, v) =>
            {
                var along = (u + 1f) / 2f; // 0 at the body end, 1 at the tip
                var half = MathF.Sin(MathF.PI * MathF.Pow(along, 0.7f));

                if (half <= 0f)
                    return 0f;

                var edge = 1f - (MathF.Abs(v) / half);

                return Math.Clamp(edge * 2.5f, 0f, 1f) * (0.55f + (0.45f * along));
            });

    //body: an oval in the top half, 1.2 times taller than wide, with a soft one-pixel edge. tinted by the particle colour
    private static Texture2D BuildBalloonTexture(GraphicsDevice device)
        => BuildTexture(
            device,
            BALLOON_TEX_W,
            BALLOON_TEX_H,
            (u, v) =>
            {
                var bv = (v + 0.52f) / 0.48f;
                var r = MathF.Sqrt((u * u) + (bv * bv));

                return Math.Clamp((1f - r) * 6f, 0f, 1f);
            });

    //drawn pale over the body: a small shine up-left on the body and a thin wavy string from the knot to the bottom
    private static Texture2D BuildBalloonDetailTexture(GraphicsDevice device)
        => BuildTexture(
            device,
            BALLOON_TEX_W,
            BALLOON_TEX_H,
            (u, v) =>
            {
                var hu = (u + 0.35f) / 0.25f;
                var hv = (v + 0.72f) / 0.1f;
                var shine = Math.Clamp(1f - MathF.Sqrt((hu * hu) + (hv * hv)), 0f, 1f) * 0.8f;

                if (v < -0.04f)
                    return shine;

                var stringX = 0.18f * MathF.Sin((v + 0.04f) * 7f);

                return Math.Clamp(1f - (MathF.Abs(u - stringX) / 0.13f), 0f, 1f) * 0.7f;
            });

    //builds a white texture whose alpha is shape(u, v), with u and v the pixel center mapped to [-1, 1]
    private static Texture2D BuildTexture(GraphicsDevice device, int width, int height, Func<float, float, float> shape)
    {
        var pixels = new Color[width * height];

        for (var py = 0; py < height; py++)
            for (var px = 0; px < width; px++)
            {
                var u = ((px + 0.5f) / width * 2f) - 1f;
                var v = ((py + 0.5f) / height * 2f) - 1f;
                var a = (byte)Math.Clamp((int)(shape(u, v) * 255f), 0, 255);

                pixels[(py * width) + px] = new Color((byte)255, (byte)255, (byte)255, a);
            }

        var tex = new Texture2D(device, width, height);
        tex.SetData(pixels);

        return tex;
    }
}
