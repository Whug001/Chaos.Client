using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Rendering;

public sealed class DappleRenderer : AmbientOverlayBase
{
    private const int COUNT = 26;
    private const float AREA_W = 900f;
    private const float AREA_H = 600f;
    private const float DRIFT_X = 5f;
    private const float DRIFT_Y = 2f;

    private sealed class Spot
    {
        public float X, Y, Radius, Alpha, Phase, Freq;
    }

    private readonly Spot[] Spots;
    private Texture2D? Dot;
    private float Offset;

    public DappleRenderer()
    {
        var random = new Random();

        float Roll(float min, float max) => min + ((max - min) * random.NextSingle());

        Spots = Enumerable.Range(0, COUNT)
                          .Select(_ => new Spot
                          {
                              X = Roll(0f, AREA_W),
                              Y = Roll(0f, AREA_H),
                              Radius = Roll(30f, 80f),
                              Alpha = Roll(0.35f, 0.65f),
                              Phase = Roll(0f, MathHelper.TwoPi),
                              Freq = Roll(0.25f, 0.7f)
                          })
                          .ToArray();
    }

    public override BlendState BlendState => BlendState.Additive;

    public bool HasAllocatedTextures => Dot is not null;

    protected override void Step(float dt, Rectangle viewport, Vector2 worldOrigin) => Offset += dt;

    public override void Draw(SpriteBatch spriteBatch, Rectangle viewport, Vector2 worldOrigin)
    {
        if (EffectAlpha <= 0f)
            return;

        Dot ??= AmbientTextures.SoftDot(spriteBatch.GraphicsDevice);

        var originX = worldOrigin.X - viewport.X + (Offset * DRIFT_X);
        var originY = worldOrigin.Y - viewport.Y + (Offset * DRIFT_Y);

        foreach (var spot in Spots)
        {
            var pulse = 0.55f
                        + (0.3f * MathF.Sin((MathHelper.TwoPi * spot.Freq * Clock) + spot.Phase))
                        + (0.15f * MathF.Sin((MathHelper.TwoPi * spot.Freq * 2.7f * Clock) + spot.Phase));
            var alpha = EffectAlpha * spot.Alpha * pulse;
            var x = viewport.X + Wrap(spot.X + originX, AREA_W) - 100f;
            var y = viewport.Y + Wrap(spot.Y + originY, AREA_H) - 100f;

            var destination = new Rectangle((int)(x - spot.Radius), (int)(y - (spot.Radius / 2f)), (int)(spot.Radius * 2f), (int)spot.Radius);

            if (destination.Intersects(viewport))
                spriteBatch.Draw(Dot, destination, new Color((byte)255, (byte)226, (byte)150, (byte)Math.Clamp((int)(255f * alpha), 0, 255)));
        }
    }

    private static float Wrap(float value, float size) => ((value % size) + size) % size;

    public override void ReleaseTextures()
    {
        Dot?.Dispose();
        Dot = null;
    }
}
