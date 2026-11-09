using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Rendering;

public sealed class WetWallRenderer : AmbientOverlayBase, IMapAwareOverlay
{
    internal WetWallSimulation Simulation { get; } = new(new Random());
    private MapSurface? Surface;
    private Texture2D? Pixel;
    private Texture2D? RingTexture;
    private Texture2D? Patch;

    public override BlendState BlendState => BlendState.NonPremultiplied;

    public bool HasAllocatedTextures => Pixel is not null;

    public void SetSurface(MapSurface? surface)
    {
        Simulation.Clear();
        Surface = surface;
    }

    protected override void ResetState() => Simulation.Clear();

    protected override void Step(float dt, Rectangle viewport, Vector2 worldOrigin)
    {
        var visible = new Rectangle(viewport.X - (int)worldOrigin.X, viewport.Y - (int)worldOrigin.Y, viewport.Width, viewport.Height);

        Simulation.Step(dt, visible, Surface, Spawning);
    }

    public override void Draw(SpriteBatch spriteBatch, Rectangle viewport, Vector2 worldOrigin)
    {
        if (EffectAlpha <= 0f)
            return;

        var device = spriteBatch.GraphicsDevice;
        Pixel ??= AmbientTextures.Pixel(device);
        RingTexture ??= AmbientTextures.Ring(device);
        Patch ??= AmbientTextures.SoftDot(device);

        var ox = (int)worldOrigin.X;
        var oy = (int)worldOrigin.Y;

        foreach (var p in Simulation.Patches)
        {
            var k = p.Age / p.Life;
            spriteBatch.Draw(Patch, new Rectangle((int)(p.X - p.Size) + ox, (int)(p.Y - (p.Size / 2f)) + oy, (int)(p.Size * 2f), (int)p.Size), Tint(4, 10, 30, p.Alpha * (1f - k) * EffectAlpha));
        }

        foreach (var s in Simulation.Streaks)
            spriteBatch.Draw(Pixel, new Rectangle((int)s.X + ox, (int)s.Y + oy, 2, (int)s.Size), Tint(4, 10, 30, s.Alpha * (1f - (s.Age / s.Life)) * EffectAlpha));

        foreach (var r in Simulation.Rings)
        {
            var k = r.Age / r.Life;
            DrawRing(spriteBatch, r.X + ox, r.Y + oy, r.Size * k, 0.55f * (1f - k));

            if (k > 0.3f)
                DrawRing(spriteBatch, r.X + ox, r.Y + oy, r.Size * (k - 0.3f), 0.3f * (1f - k));
        }

        foreach (var b in Simulation.Beads)
            spriteBatch.Draw(Pixel, new Rectangle((int)b.X + ox, (int)b.Y + oy, 1, 2), Tint(226, 238, 252, b.Alpha * (1f - (b.Age / b.Life)) * EffectAlpha));

        foreach (var drop in Simulation.Drops)
            DrawDrop(spriteBatch, drop, ox, oy);

        foreach (var s in Simulation.Splashes)
            spriteBatch.Draw(Pixel, new Rectangle((int)s.X + ox, (int)s.Y + oy, 1, 1), Tint(226, 238, 252, 0.85f * (1f - (s.Age / s.Life)) * EffectAlpha));
    }

    private void DrawRing(SpriteBatch spriteBatch, float x, float y, float radius, float alpha)
    {
        if (radius < 1f)
            return;

        spriteBatch.Draw(RingTexture!, new Rectangle((int)(x - radius), (int)(y - (radius / 2f)), (int)(radius * 2f), (int)radius), Tint(205, 225, 245, alpha * EffectAlpha));
    }

    private void DrawDrop(SpriteBatch spriteBatch, WetWallSimulation.Drop drop, int ox, int oy)
    {
        var x = (int)drop.X + ox;
        var y = (int)drop.Y + oy;
        var big = drop.Size >= 1.4f;

        if (drop.Hold <= 0f)
        {
            var tail = MathF.Min(18f, drop.V * 0.12f);

            for (var n = 1; n < tail; n += 2)
                spriteBatch.Draw(Pixel!, new Rectangle(x, y - n - 2, 1, 2), Tint(214, 232, 250, 0.55f * (1f - (n / tail)) * EffectAlpha));
        }

        var body = Tint(176, 205, 235, 0.8f * EffectAlpha);
        spriteBatch.Draw(Pixel!, new Rectangle(x, y - 3, 1, 2), body);
        spriteBatch.Draw(Pixel!, new Rectangle(x - (big ? 1 : 0), y - 1, big ? 3 : 2, 2), body);

        if (big)
            spriteBatch.Draw(Pixel!, new Rectangle(x, y + 1, 1, 1), body);

        spriteBatch.Draw(Pixel!, new Rectangle(x, y - 1, 1, 1), Tint(248, 252, 255, 0.95f * EffectAlpha));
    }

    public override void ReleaseTextures()
    {
        Pixel?.Dispose();
        Pixel = null;
        RingTexture?.Dispose();
        RingTexture = null;
        Patch?.Dispose();
        Patch = null;
    }
}
