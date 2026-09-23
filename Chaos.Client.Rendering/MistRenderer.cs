#region
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Full-viewport mist overlay — the renderer behind Fog, Blood Moon, Sandstorm, Miasma and Underwater, each a
///     <see cref="MistStyle" /> preset. Draws a translucent color wash (optionally pulsing), then drifting parallax
///     layers of a seamless (toroidal) value-noise cloud texture, then an optional dark edge vignette. The layer tile
///     grid is anchored to the world so the clouds sit still over the MAP while the camera pans; only the time-based
///     drift moves them. The whole effect fades in/out over <see cref="MistStyle.FadeSeconds" />. Touched only on the
///     game-loop thread.
/// </summary>
public sealed class MistRenderer : IAmbientOverlay
{
    // Cloud texture size — must be multiples of 16x8 (see CloudNoiseTexture.Build).
    private const int TEX_W = 256;
    private const int TEX_H = 128;

    private const int VIGNETTE_TEX = 256; // square gradient texture, stretched to the viewport

    private readonly MistStyle Style;
    private readonly Vector2[] Scroll; // accumulated drift per layer, px

    private Texture2D? CloudTexture;
    private Texture2D? VignetteTexture;
    private bool Active;
    private float EffectAlpha; // 0..1 current fade level
    private float PulseTime;   // pulse clock, wrapped to PulsePeriod

    public MistRenderer(MistStyle style)
    {
        Style = style;
        Scroll = new Vector2[style.Layers.Length];
    }

    /// <inheritdoc />
    public BlendState BlendState => BlendState.NonPremultiplied;

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

        //don't accumulate clocks while fully idle — keeps offsets from drifting when unused
        if (!IsActive)
            return;

        PulseTime = (PulseTime + dt) % Style.PulsePeriod;

        for (var i = 0; i < Scroll.Length; i++)
        {
            //modulo a large constant so the float never loses precision over a long session
            var next = Scroll[i] + Style.Layers[i].Velocity * dt;
            Scroll[i] = new Vector2(next.X % 100000f, next.Y % 100000f);
        }
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, Rectangle viewport, Vector2 worldOrigin)
    {
        if ((EffectAlpha <= 0f) || (viewport.Width <= 0) || (viewport.Height <= 0))
            return;

        CloudTexture ??= CloudNoiseTexture.Build(
            spriteBatch.GraphicsDevice,
            TEX_W,
            TEX_H,
            Style.Seed,
            Style.NoiseKnee,
            Style.NoiseRange);

        //wash first — dims/tints the scene, swinging around its resting strength if the style pulses
        var washAlpha = Style.WashAlpha * ((1f - Style.PulseDepth) + (2f * Style.PulseDepth * PulseLevel()));
        var washA = ToByte(washAlpha * EffectAlpha);
        RenderHelper.DrawRect(spriteBatch, viewport, new Color(Style.WashColor.R, Style.WashColor.G, Style.WashColor.B, washA));

        //then the parallax cloud layers
        for (var l = 0; l < Style.Layers.Length; l++)
        {
            var layer = Style.Layers[l];
            var tileW = Math.Max(1, (int)(TEX_W * layer.Scale));
            var tileH = Math.Max(1, (int)(TEX_H * layer.Scale));
            var layerA = ToByte(layer.Alpha * EffectAlpha);

            if (layerA == 0)
                continue;

            var tint = new Color(Style.LayerTint.R, Style.LayerTint.G, Style.LayerTint.B, layerA);

            //anchor the tile grid to the world (drift shifts it within world space) so the pattern is
            //static relative to the map, not the screen — panning the camera must not drag the clouds
            var startX = GridStart(viewport.X, worldOrigin.X + Scroll[l].X, tileW);
            var startY = GridStart(viewport.Y, worldOrigin.Y + Scroll[l].Y, tileH);

            for (var y = startY; y < viewport.Bottom; y += tileH)
                for (var x = startX; x < viewport.Right; x += tileW)
                    spriteBatch.Draw(CloudTexture, new Rectangle(x, y, tileW, tileH), tint);
        }

        if (Style.VignetteAlpha <= 0f)
            return;

        //vignette last so it darkens wash + clouds evenly at the edges (screen-space by design). the
        //texture bakes the gradient alpha; the tint alpha scales the whole thing by fade + peak.
        VignetteTexture ??= BuildVignetteTexture(spriteBatch.GraphicsDevice, Style.VignetteColor, Style.VignetteInner);
        spriteBatch.Draw(VignetteTexture, viewport, new Color((byte)255, (byte)255, (byte)255, ToByte(Style.VignetteAlpha * EffectAlpha)));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        CloudTexture?.Dispose();
        CloudTexture = null;
        VignetteTexture?.Dispose();
        VignetteTexture = null;
    }

    // ============================================================

    private static byte ToByte(float unit) => (byte)Math.Clamp((int)(255f * unit), 0, 255);

    //current pulse strength [0..1] for the style's pulse shape
    private float PulseLevel()
    {
        var phase = PulseTime / Style.PulsePeriod; // [0..1)

        return Style.Pulse switch
        {
            MistPulse.Sine      => 0.5f + (0.5f * MathF.Sin(phase * MathF.Tau)),
            MistPulse.Heartbeat => MathF.Min(1f, Bump(phase, 0.10f, 0.10f) + (0.6f * Bump(phase, 0.30f, 0.10f))),
            _                   => 0.5f
        };
    }

    //raised-cosine bump: 1 at center, falling smoothly to 0 at ±width — no discontinuities
    private static float Bump(float phase, float center, float width)
    {
        var d = MathF.Abs(phase - center);

        if (d >= width)
            return 0f;

        return 0.5f * (1f + MathF.Cos(MathF.PI * d / width));
    }

    //largest tile-grid line ≤ viewEdge, for a grid whose lines sit at screen coords ≡ phase (mod tileSize) —
    //guarantees the first tile covers the viewport edge for any camera position or drift value
    private static int GridStart(int viewEdge, float phase, int tileSize)
    {
        var rem = (((viewEdge - (int)MathF.Floor(phase)) % tileSize) + tileSize) % tileSize;

        return viewEdge - rem;
    }

    //Builds a VIGNETTE_TEX² radial gradient: transparent center, colored edges. Alpha ramps from 0 at `inner`
    //(normalized radius; 1.0 = edge midpoints, ~1.41 = corners) up to 255 with a smoothstep so there is no
    //visible ring. Stretched over the viewport at draw time.
    private static Texture2D BuildVignetteTexture(GraphicsDevice device, Color color, float inner)
    {
        var pixels = new Color[VIGNETTE_TEX * VIGNETTE_TEX];

        for (var py = 0; py < VIGNETTE_TEX; py++)
            for (var px = 0; px < VIGNETTE_TEX; px++)
            {
                var u = ((px + 0.5f) / VIGNETTE_TEX * 2f) - 1f;
                var v = ((py + 0.5f) / VIGNETTE_TEX * 2f) - 1f;
                var r = MathF.Sqrt((u * u) + (v * v));

                var t = Math.Clamp((r - inner) / (1f - inner), 0f, 1f);
                t = t * t * (3f - (2f * t)); // smoothstep — no hard ring

                pixels[(py * VIGNETTE_TEX) + px] = new Color(color.R, color.G, color.B, (byte)(t * 255f));
            }

        var tex = new Texture2D(device, VIGNETTE_TEX, VIGNETTE_TEX);
        tex.SetData(pixels);

        return tex;
    }
}
