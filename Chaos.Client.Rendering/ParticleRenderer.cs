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
/// </summary>
public sealed class ParticleRenderer : IAmbientOverlay
{
    private const float REFERENCE_AREA = 640f * 480f; // viewport area ParticleStyle.Count is tuned for
    private const float WRAP_MARGIN = 24f;            // px past each edge before a particle wraps, so it never pops in view
    private const int SOFT_TEX = 32;                  // soft dot / bubble texture size
    private const int LEAF_TEX_W = 16;
    private const int LEAF_TEX_H = 8;

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

    private bool Active;
    private float EffectAlpha; // 0..1 current fade level
    private float Clock;       // seconds, drives sway / twinkle / tumble

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

        var spanW = viewport.Width + (2f * WRAP_MARGIN);
        var spanH = viewport.Height + (2f * WRAP_MARGIN);

        for (var i = 0; i < Particles.Length; i++)
        {
            ref var p = ref Particles[i];

            p.Position += cameraShift + (CurrentVelocity(in p) * dt);
            p.Rotation = (p.Rotation + (p.SpinRate * dt)) % MathF.Tau;

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
            var alpha = p.Alpha * TwinkleLevel(in p) * EffectAlpha;

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
        }
    }

    /// <inheritdoc />
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
    }

    // ============================================================

    //(re)creates particles scattered over the whole viewport, count scaled by viewport area
    private void Spawn(int width, int height)
    {
        ParticlesForWidth = width;
        ParticlesForHeight = height;

        var count = Math.Max(1, (int)MathF.Round(Style.Count * (width * height / REFERENCE_AREA)));
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
                TwinklePhase = Roll(0f, MathF.Tau)
            };
    }

    //base velocity plus sway. Y sways at a different rate from X so two-axis sway traces a wandering loop
    private Vector2 CurrentVelocity(in Particle p)
    {
        var angle = (MathF.Tau * p.SwayFreq * Clock) + p.SwayPhase;

        return p.Velocity + new Vector2(Style.Sway.X * MathF.Sin(angle), Style.Sway.Y * MathF.Sin((angle * 0.77f) + 1.3f));
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
            _                    => (0f, new Vector2(p.Size / texture.Width))
        };
    }

    private Texture2D GetShapeTexture(GraphicsDevice device)
        => Style.Shape switch
        {
            ParticleShape.Square or ParticleShape.Streak => PixelTexture ??= BuildPixelTexture(device),
            ParticleShape.Leaf                           => LeafTexture ??= BuildLeafTexture(device),
            ParticleShape.Bubble                         => BubbleTexture ??= BuildBubbleTexture(device),
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
