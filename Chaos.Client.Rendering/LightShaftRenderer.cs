using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Rendering;

public sealed record LightShaftStyle(
    Color Tint,
    int Count,
    float WidthMin,
    float WidthMax,
    float AlphaMin,
    float AlphaMax,
    float Base,
    float Depth,
    float FreqMin,
    float FreqMax,
    float DriftMin,
    float DriftMax);

public sealed class LightShaftRenderer : AmbientOverlayBase
{
    public static LightShaftStyle Sunbeams { get; } = new(new Color((byte)255, (byte)232, (byte)170), 6, 50f, 120f, 0.4f, 0.7f, 0.6f, 0.4f, 0.08f, 0.18f, 2f, 6f);
    public static LightShaftStyle Moonbeams { get; } = new(new Color((byte)170, (byte)200, (byte)255), 5, 50f, 110f, 0.35f, 0.6f, 0.65f, 0.35f, 0.06f, 0.14f, 1.5f, 4f);

    private const float ANGLE = 0.42f;
    private const float LENGTH = 430f;
    private const float WRAP_END = 840f;
    private const float WRAP_START = 40f;

    private sealed class Beam
    {
        public float X, Width, Alpha, Phase, Freq, Drift;
    }

    private readonly LightShaftStyle Style;
    private readonly Beam[] Beams;
    private Texture2D? Shaft;

    public LightShaftRenderer(LightShaftStyle style)
    {
        Style = style;
        var random = new Random();

        float Roll(float min, float max) => min + ((max - min) * random.NextSingle());

        Beams = Enumerable.Range(0, style.Count)
                          .Select(_ => new Beam
                          {
                              X = Roll(60f, 800f),
                              Width = Roll(style.WidthMin, style.WidthMax),
                              Alpha = Roll(style.AlphaMin, style.AlphaMax),
                              Phase = Roll(0f, MathHelper.TwoPi),
                              Freq = Roll(style.FreqMin, style.FreqMax),
                              Drift = Roll(style.DriftMin, style.DriftMax)
                          })
                          .ToArray();
    }

    public override BlendState BlendState => BlendState.Additive;

    public bool HasAllocatedTextures => Shaft is not null;

    protected override void Step(float dt, Rectangle viewport, Vector2 worldOrigin)
    {
        foreach (var beam in Beams)
        {
            beam.X += beam.Drift * dt;

            if (beam.X > WRAP_END)
                beam.X = WRAP_START;
        }
    }

    public override void Draw(SpriteBatch spriteBatch, Rectangle viewport, Vector2 worldOrigin)
    {
        if (EffectAlpha <= 0f)
            return;

        Shaft ??= AmbientTextures.Create(spriteBatch.GraphicsDevice, 64, 256, static (x, y) =>
        {
            var across = 1f - MathF.Abs((x / 63f * 2f) - 1f);

            return AmbientTextures.White(across * (1f - (y / 255f)));
        });

        var scale = ViewScale(viewport);

        foreach (var beam in Beams)
        {
            var alpha = EffectAlpha * beam.Alpha * (Style.Base + (Style.Depth * MathF.Sin((MathHelper.TwoPi * beam.Freq * Clock) + beam.Phase)));
            var position = new Vector2(viewport.X + (beam.X * scale), viewport.Y - (40f * scale));
            var size = new Vector2(beam.Width * scale / Shaft.Width, LENGTH * scale / Shaft.Height);

            spriteBatch.Draw(
                Shaft,
                position,
                null,
                new Color(Style.Tint.R, Style.Tint.G, Style.Tint.B, (byte)Math.Clamp((int)(255f * alpha), 0, 255)),
                ANGLE,
                new Vector2(Shaft.Width / 2f, 0f),
                size,
                SpriteEffects.None,
                0f);
        }
    }

    public override void ReleaseTextures()
    {
        Shaft?.Dispose();
        Shaft = null;
    }
}
