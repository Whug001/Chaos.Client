using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Rendering;

public sealed record StreakStyle(
    float GapMin,
    float GapMax,
    float Speed,
    float Life,
    float Tail,
    float Width,
    float StartXMin,
    float StartYMin,
    float StartYMax,
    (float At, Color Color)[] TailColors,
    bool Burning);

public sealed class StreakRenderer : AmbientOverlayBase
{
    public static StreakStyle ShootingStars { get; } = new(
        5f, 12f, 615f, 0.55f, 90f, 1.5f, 0.4f, -10f, 0.4f,
        [(0f, new Color((byte)235, (byte)245, (byte)255, (byte)255)), (1f, new Color((byte)160, (byte)190, (byte)255, (byte)0))],
        false);

    public static StreakStyle Meteors { get; } = new(
        3f, 6f, 295f, 1.3f, 130f, 3f, 0.5f, -20f, 0.25f,
        [(0f, new Color((byte)255, (byte)240, (byte)200, (byte)255)), (0.3f, new Color((byte)255, (byte)170, (byte)90, (byte)178)), (1f, new Color((byte)255, (byte)90, (byte)40, (byte)0))],
        true);

    private const float DIR_X = -0.86f;
    private const float DIR_Y = 0.5f;
    private const float SPARK_EVERY = 0.03f;
    private const int MAX_SPARKS = 80;

    private sealed class Streak
    {
        public float X, Y, Age, NextSpark;
    }

    private sealed class Spark
    {
        public float X, Y, VX, VY, Age, Life;
    }

    private readonly StreakStyle Style;
    private readonly Random Random = new();
    private readonly List<Streak> Streaks = [];
    private readonly List<Spark> Sparks = [];
    private Texture2D? Tail;
    private Texture2D? Head;
    private Texture2D? Pixel;
    private float NextSpawn;

    public StreakRenderer(StreakStyle style)
    {
        Style = style;
        NextSpawn = Roll(0.5f, style.GapMin);
    }

    public override BlendState BlendState => BlendState.Additive;

    public bool HasAllocatedTextures => Tail is not null;

    private float Roll(float min, float max) => min + ((max - min) * Random.NextSingle());

    protected override void ResetState()
    {
        Streaks.Clear();
        Sparks.Clear();
        NextSpawn = Roll(0.5f, Style.GapMin);
    }

    protected override void Step(float dt, Rectangle viewport, Vector2 worldOrigin)
    {
        NextSpawn -= dt;

        if ((NextSpawn <= 0f) && Spawning)
        {
            NextSpawn = Roll(Style.GapMin, Style.GapMax);
            Streaks.Add(new Streak
            {
                X = Roll(viewport.Width * Style.StartXMin, viewport.Width * 1.1f),
                Y = Roll(Style.StartYMin, viewport.Height * Style.StartYMax)
            });
        }

        for (var i = Streaks.Count - 1; i >= 0; i--)
        {
            var streak = Streaks[i];
            streak.Age += dt;

            if (streak.Age >= Style.Life)
            {
                Streaks.RemoveAt(i);

                continue;
            }

            streak.X += DIR_X * Style.Speed * dt;
            streak.Y += DIR_Y * Style.Speed * dt;
            streak.NextSpark -= dt;

            if (Style.Burning && (streak.NextSpark <= 0f) && (Sparks.Count < MAX_SPARKS))
            {
                streak.NextSpark = SPARK_EVERY;
                Sparks.Add(new Spark
                {
                    X = streak.X + Roll(-3f, 3f),
                    Y = streak.Y + Roll(-3f, 3f),
                    VX = Roll(-14f, 14f),
                    VY = Roll(20f, 55f),
                    Life = Roll(0.4f, 0.8f)
                });
            }
        }

        for (var i = Sparks.Count - 1; i >= 0; i--)
        {
            var spark = Sparks[i];
            spark.Age += dt;

            if (spark.Age >= spark.Life)
            {
                Sparks.RemoveAt(i);

                continue;
            }

            spark.X += spark.VX * dt;
            spark.Y += spark.VY * dt;
        }
    }

    public override void Draw(SpriteBatch spriteBatch, Rectangle viewport, Vector2 worldOrigin)
    {
        if (EffectAlpha <= 0f)
            return;

        var device = spriteBatch.GraphicsDevice;
        Tail ??= AmbientTextures.Gradient(device, 128, 4, Style.TailColors);
        Head ??= AmbientTextures.SoftDot(device, 32);
        Pixel ??= AmbientTextures.Pixel(device);

        var angle = MathF.Atan2(-DIR_Y, -DIR_X);
        var tailScale = new Vector2(Style.Tail / Tail.Width, Style.Width / Tail.Height);

        foreach (var streak in Streaks)
        {
            var fade = MathF.Sin(MathF.PI * streak.Age / Style.Life) * EffectAlpha;
            var position = new Vector2(viewport.X + streak.X, viewport.Y + streak.Y);

            spriteBatch.Draw(Tail, position, null, new Color((byte)255, (byte)255, (byte)255, (byte)Math.Clamp((int)(255f * fade), 0, 255)), angle, new Vector2(0f, Tail.Height / 2f), tailScale, SpriteEffects.None, 0f);

            if (Style.Burning)
                spriteBatch.Draw(Head, position - new Vector2(16f, 16f), Tint(255, 190, 110, fade * 0.8f));
        }

        foreach (var spark in Sparks)
            spriteBatch.Draw(
                Pixel,
                new Rectangle(viewport.X + (int)spark.X, viewport.Y + (int)spark.Y, 2, 2),
                Tint(255, 190, 110, (1f - (spark.Age / spark.Life)) * EffectAlpha));
    }

    public override void ReleaseTextures()
    {
        Tail?.Dispose();
        Tail = null;
        Head?.Dispose();
        Head = null;
        Pixel?.Dispose();
        Pixel = null;
    }
}
