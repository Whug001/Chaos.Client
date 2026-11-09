using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Rendering;

public sealed record AuroraStyle(bool Curtain, int Step);

public sealed class AuroraRenderer : AmbientOverlayBase
{
    public static AuroraStyle Curtains { get; } = new(true, 4);
    public static AuroraStyle Glow { get; } = new(false, 6);

    private static readonly Color[] Colors = [new((byte)90, (byte)255, (byte)160), new((byte)70, (byte)220, (byte)230), new((byte)170, (byte)110, (byte)255)];
    private static readonly float[] Phases = [0f, 2.1f, 4.2f];
    private static readonly float[] Bases = [125f, 95f, 150f];
    private static readonly float[] Speeds = [0.3f, -0.22f, 0.17f];

    private readonly AuroraStyle Style;
    private Texture2D? Strip;

    public AuroraRenderer(AuroraStyle style) => Style = style;

    public override BlendState BlendState => BlendState.Additive;

    public bool HasAllocatedTextures => Strip is not null;

    public override void Draw(SpriteBatch spriteBatch, Rectangle viewport, Vector2 worldOrigin)
    {
        if (EffectAlpha <= 0f)
            return;

        Strip ??= Style.Curtain
            ? AmbientTextures.VerticalAlpha(spriteBatch.GraphicsDevice, 8, 128, (0f, 0f), (0.75f, 0.5f), (0.93f, 1f), (1f, 0f))
            : AmbientTextures.VerticalAlpha(spriteBatch.GraphicsDevice, 8, 128, (0f, 0f), (0.5f, 1f), (1f, 0f));

        var scale = ViewScale(viewport);
        var width = viewport.Width / scale;

        for (var ribbon = 0; ribbon < Colors.Length; ribbon++)
            for (var x = 0f; x < width; x += Style.Step)
                DrawStrip(spriteBatch, viewport, scale, ribbon, x);
    }

    private void DrawStrip(SpriteBatch spriteBatch, Rectangle viewport, float scale, int ribbon, float x)
    {
        var ph = Phases[ribbon];
        var t = Clock;
        float top, height, alpha;

        if (Style.Curtain)
        {
            var y = Bases[ribbon] + (22f * MathF.Sin((0.012f * x) + (0.35f * t) + ph)) + (10f * MathF.Sin((0.031f * x) - (0.6f * t) + (2f * ph)));
            height = 110f + (30f * MathF.Sin((0.02f * x) + (0.2f * t) + ph));
            top = y - height;
            var s = 0.5f + (0.5f * MathF.Sin((0.017f * x) + (0.5f * t) + ph));
            alpha = (s * s * 0.55f) + 0.1f;
        }
        else
        {
            var fullHeight = viewport.Height / scale;
            var sp = Speeds[ribbon];
            top = (0.5f * fullHeight) + (70f * MathF.Sin((0.008f * x) + (sp * t) + ph)) - (0.65f * fullHeight);
            height = 1.3f * fullHeight;
            var s = 0.5f + (0.5f * MathF.Sin((0.011f * x) + (1.7f * sp * t) + ph));
            alpha = (s * s * 0.26f) + 0.03f;
        }

        var left = (int)MathF.Round(x * scale);
        var right = (int)MathF.Round((x + Style.Step) * scale);
        var destination = new Rectangle(
            viewport.X + left,
            viewport.Y + (int)MathF.Round(top * scale),
            right - left,
            (int)MathF.Round(height * scale));

        var c = Colors[ribbon];
        spriteBatch.Draw(Strip!, destination, new Color(c.R, c.G, c.B, (byte)Math.Clamp((int)(255f * alpha * EffectAlpha), 0, 255)));
    }

    public override void ReleaseTextures()
    {
        Strip?.Dispose();
        Strip = null;
    }
}
