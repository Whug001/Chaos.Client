using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Rendering;

public sealed class SteamRenderer : AmbientOverlayBase, IMapAwareOverlay
{
    private const float EMIT_EVERY = 0.27f;
    private const int MAX_VENTS = 16;
    private const int MAX_PUFFS = 150;
    private const float VENT_MARGIN = 80f;

    private sealed class Puff
    {
        public float X, Y, VX, VY, Age, Life, Start;
    }

    private readonly Random Random = new();
    private readonly List<Puff> Puffs = [];
    private MapSurface? Surface;
    private Texture2D? Dot;
    private float NextEmit;

    public override BlendState BlendState => BlendState.NonPremultiplied;

    public bool HasAllocatedTextures => Dot is not null;

    private float Roll(float min, float max) => min + ((max - min) * Random.NextSingle());

    public void SetSurface(MapSurface? surface)
    {
        Puffs.Clear();
        Surface = surface;
    }

    protected override void ResetState() => Puffs.Clear();

    protected override void Step(float dt, Rectangle viewport, Vector2 worldOrigin)
    {
        NextEmit -= dt;

        if ((NextEmit <= 0f) && Spawning && Surface is not null)
        {
            NextEmit = EMIT_EVERY;
            Emit(viewport, worldOrigin, Surface);
        }

        for (var i = Puffs.Count - 1; i >= 0; i--)
        {
            var puff = Puffs[i];
            puff.Age += dt;

            if (puff.Age >= puff.Life)
            {
                Puffs.RemoveAt(i);

                continue;
            }

            puff.X += puff.VX * dt;
            puff.Y += puff.VY * dt;
        }
    }

    private void Emit(Rectangle viewport, Vector2 worldOrigin, MapSurface surface)
    {
        var mapHeight = surface.Height;
        var left = viewport.Left - worldOrigin.X - VENT_MARGIN;
        var right = viewport.Right - worldOrigin.X + VENT_MARGIN;
        var top = viewport.Top - worldOrigin.Y - VENT_MARGIN;
        var bottom = viewport.Bottom - worldOrigin.Y + VENT_MARGIN;
        var emitted = 0;

        foreach (var vent in surface.SteamVents)
        {
            var world = Camera.TileToWorld(vent.X, vent.Y, mapHeight) + new Vector2(28f, 14f);

            if ((world.X < left) || (world.X > right) || (world.Y < top) || (world.Y > bottom))
                continue;

            if (Puffs.Count >= MAX_PUFFS)
                return;

            Puffs.Add(new Puff
            {
                X = world.X + Roll(-8f, 8f),
                Y = world.Y,
                VX = Roll(-6f, 9f),
                VY = Roll(-42f, -26f),
                Life = Roll(1.6f, 2.8f),
                Start = Roll(10f, 18f)
            });

            if (++emitted >= MAX_VENTS)
                return;
        }
    }

    public override void Draw(SpriteBatch spriteBatch, Rectangle viewport, Vector2 worldOrigin)
    {
        if (EffectAlpha <= 0f)
            return;

        Dot ??= AmbientTextures.SoftDot(spriteBatch.GraphicsDevice);

        foreach (var puff in Puffs)
        {
            var k = puff.Age / puff.Life;
            var size = puff.Start * (1f + (3.2f * k));
            var alpha = 0.42f * MathF.Sin(MathF.PI * MathF.Min(1f, 1.15f * k)) * EffectAlpha;
            var x = puff.X + worldOrigin.X - (size / 2f);
            var y = puff.Y + worldOrigin.Y - (size / 2f);

            spriteBatch.Draw(Dot, new Rectangle((int)x, (int)y, (int)size, (int)size), Tint(240, 240, 245, alpha));
        }
    }

    public override void ReleaseTextures()
    {
        Dot?.Dispose();
        Dot = null;
    }
}
