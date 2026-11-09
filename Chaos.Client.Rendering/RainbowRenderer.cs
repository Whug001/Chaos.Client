using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Rendering;

public sealed class RainbowRenderer : AmbientOverlayBase
{
    private const float INNER = 300f;
    private const float OUTER = 345f;

    private static readonly Color[] Bands =
    [
        new((byte)255, (byte)60, (byte)60),
        new((byte)255, (byte)150, (byte)40),
        new((byte)255, (byte)240, (byte)70),
        new((byte)80, (byte)230, (byte)90),
        new((byte)70, (byte)150, (byte)255),
        new((byte)150, (byte)80, (byte)240)
    ];

    private static readonly (float At, float Alpha)[] Falloff = [(0f, 1f), (0.6f, 0.7f), (1f, 0f)];

    private Texture2D? Arc;

    public override BlendState BlendState => BlendState.Additive;

    public bool HasAllocatedTextures => Arc is not null;

    public override void Draw(SpriteBatch spriteBatch, Rectangle viewport, Vector2 worldOrigin)
    {
        if (EffectAlpha <= 0f)
            return;

        if ((viewport.Width <= 0) || (viewport.Height <= 0))
            return;

        if ((Arc is null) || (Arc.Width != viewport.Width) || (Arc.Height != viewport.Height))
        {
            ReleaseTextures();
            Arc = Build(spriteBatch.GraphicsDevice, viewport.Width, viewport.Height);
        }

        var alpha = EffectAlpha * (0.34f + (0.12f * MathF.Sin(0.5f * Clock)));

        spriteBatch.Draw(Arc, viewport, new Color((byte)255, (byte)255, (byte)255, (byte)Math.Clamp((int)(255f * alpha), 0, 255)));
    }

    private static Texture2D Build(GraphicsDevice device, int width, int height)
    {
        var scale = MathF.Max(1f, width / 640f);
        var inner = INNER * scale;
        var outer = OUTER * scale;
        var cx = width * 0.55f;
        var cy = height * 1.25f;

        return AmbientTextures.Create(device, width, height, (x, y) =>
        {
            var d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
            var at = (d - inner) / (outer - inner);

            if (at is < 0f or > 1f)
                return Color.Transparent;

            var color = BandColor(at, out var edge);
            var alpha = edge * AmbientTextures.Sample(Falloff, y / (height - 1f));

            return new Color(color.R, color.G, color.B, (byte)Math.Clamp((int)(255f * alpha), 0, 255));
        });
    }

    private static Color BandColor(float at, out float edge)
    {
        edge = at < 0.1f ? at / 0.1f : at > 0.9f ? (1f - at) / 0.1f : 1f;

        var band = MathHelper.Clamp((at - 0.1f) / 0.8f, 0f, 1f) * (Bands.Length - 1);
        var index = Math.Min((int)band, Bands.Length - 2);

        return Color.Lerp(Bands[index], Bands[index + 1], band - index);
    }

    public override void ReleaseTextures()
    {
        Arc?.Dispose();
        Arc = null;
    }
}
