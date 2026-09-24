#region
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>One spotlight to draw this frame. <see cref="ScreenPosition" /> is the floor point in viewport pixels.</summary>
public readonly record struct SpotlightDraw(
    Vector2 ScreenPosition,
    Color Color,
    float Strength,
    float RadiusTiles,
    bool Beam);

/// <summary>
///     Draws Theatre spotlights' colour: a soft pool on the floor, a fainter tint over whoever stands in it, and an
///     optional beam from the top of the view. Additive, drawn right after the darkness layer. The three textures are
///     built once, white with an alpha falloff, and tinted per light.
/// </summary>
public sealed class SpotlightRenderer : IDisposable
{
    private const int POOL_WIDTH = 128;
    private const int POOL_HEIGHT = 64;
    private const int TINT_WIDTH = 64;
    private const int TINT_HEIGHT = 128;
    private const int BEAM_WIDTH = 64;
    private const int BEAM_HEIGHT = 256;
    private const float POOL_ALPHA = 0.85f;
    private const float TINT_ALPHA = 0.45f;
    private const float BEAM_ALPHA = 0.38f;

    /// <summary>Colour strength with the house lights full, so a spotlight reads as a wash in a lit room.</summary>
    private const float WASH_FLOOR = 0.35f;

    private readonly Texture2D Beam;
    private readonly Texture2D Pool;
    private readonly Texture2D Tint;

    public SpotlightRenderer(GraphicsDevice device)
    {
        Pool = BuildOval(device, POOL_WIDTH, POOL_HEIGHT, 1.4f);
        Tint = BuildOval(device, TINT_WIDTH, TINT_HEIGHT, 1.2f);
        Beam = BuildBeam(device);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Pool.Dispose();
        Tint.Dispose();
        Beam.Dispose();
    }

    public static float ColorScale(float strength, float houseDarkness)
        => strength * (WASH_FLOOR + ((1f - WASH_FLOOR) * Math.Clamp(houseDarkness, 0f, 1f)));

    /// <summary>Draws every light. Call inside a batch begun with <see cref="BlendState.Additive" />.</summary>
    public void Draw(SpriteBatch spriteBatch, Rectangle viewport, ReadOnlySpan<SpotlightDraw> lights, float houseDarkness)
    {
        foreach (var light in lights)
        {
            var scale = ColorScale(light.Strength, houseDarkness);

            if (scale <= 0.005f)
                continue;

            var x = viewport.X + light.ScreenPosition.X;
            var y = viewport.Y + light.ScreenPosition.Y;
            var poolWidth = light.RadiusTiles * SpotlightMasks.TILE_TO_PIXELS * 2f * 1.15f;
            var poolHeight = poolWidth / 2f;

            if (light.Beam && (y > viewport.Y))
            {
                var beamWidth = poolWidth * 0.9f;

                spriteBatch.Draw(
                    Beam,
                    new Rectangle((int)(x - (beamWidth / 2f)), viewport.Y, (int)beamWidth, (int)(y - viewport.Y)),
                    Tinted(light.Color, scale * BEAM_ALPHA));
            }

            spriteBatch.Draw(
                Pool,
                new Rectangle((int)(x - (poolWidth / 2f)), (int)(y - (poolHeight / 2f)), (int)poolWidth, (int)poolHeight),
                Tinted(light.Color, scale * POOL_ALPHA));

            var tintWidth = poolWidth * 0.55f;
            var tintHeight = SpotlightMasks.BODY_HEIGHT * 1.4f;

            spriteBatch.Draw(
                Tint,
                new Rectangle((int)(x - (tintWidth / 2f)), (int)(y - tintHeight + (poolHeight * 0.25f)), (int)tintWidth, (int)tintHeight),
                Tinted(light.Color, scale * TINT_ALPHA));
        }
    }

    //additive blend adds rgb × alpha, so the tint's alpha carries the strength and its rgb stays the pure colour
    private static Color Tinted(Color color, float alpha)
        => new(color.R, color.G, color.B, (byte)(Math.Clamp(alpha, 0f, 1f) * 255f));

    private static Texture2D BuildOval(GraphicsDevice device, int width, int height, float power)
    {
        var pixels = new Color[width * height];

        for (var py = 0; py < height; py++)
            for (var px = 0; px < width; px++)
            {
                var dx = (px + 0.5f - (width / 2f)) / (width / 2f);
                var dy = (py + 0.5f - (height / 2f)) / (height / 2f);
                var distance = MathF.Sqrt((dx * dx) + (dy * dy));
                var alpha = distance >= 1f ? 0f : MathF.Pow(1f - distance, power);

                pixels[(py * width) + px] = new Color((byte)255, (byte)255, (byte)255, (byte)(alpha * 255f));
            }

        var texture = new Texture2D(device, width, height);
        texture.SetData(pixels);

        return texture;
    }

    //narrow at the top (35% of the width), full width at the bottom, brighter toward the floor, soft sides
    private static Texture2D BuildBeam(GraphicsDevice device)
    {
        var pixels = new Color[BEAM_WIDTH * BEAM_HEIGHT];

        for (var py = 0; py < BEAM_HEIGHT; py++)
        {
            var down = py / (BEAM_HEIGHT - 1f);
            var halfWidth = (0.175f + (0.325f * down)) * BEAM_WIDTH;

            for (var px = 0; px < BEAM_WIDTH; px++)
            {
                var across = MathF.Abs(px + 0.5f - (BEAM_WIDTH / 2f)) / halfWidth;
                var side = across >= 1f ? 0f : 1f - (across * across);
                var alpha = side * (0.15f + (0.85f * down));

                pixels[(py * BEAM_WIDTH) + px] = new Color((byte)255, (byte)255, (byte)255, (byte)(alpha * 255f));
            }
        }

        var texture = new Texture2D(device, BEAM_WIDTH, BEAM_HEIGHT);
        texture.SetData(pixels);

        return texture;
    }
}
