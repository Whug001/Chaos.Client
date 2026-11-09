using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Rendering;

/// <summary>Fade handling shared by the procedural ambient overlays.</summary>
public abstract class AmbientOverlayBase : IAmbientOverlay
{
    private bool Active;

    protected float EffectAlpha { get; private set; }
    protected float Clock { get; private set; }
    protected bool Spawning => Active;
    protected virtual float FadeSeconds => 2f;

    public abstract BlendState BlendState { get; }

    public bool IsActive => Active || (EffectAlpha > 0f);

    public void SetActive(bool on, bool immediate = false)
    {
        if ((on && !IsActive) || (immediate && !on))
            ResetState();

        Active = on;

        if (immediate)
            EffectAlpha = on ? 1f : 0f;
    }

    public void Update(GameTime gameTime, Rectangle viewport, Vector2 worldOrigin)
    {
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (dt <= 0f)
            return;

        var step = dt / FadeSeconds;
        EffectAlpha = Active ? MathF.Min(1f, EffectAlpha + step) : MathF.Max(0f, EffectAlpha - step);

        if (!IsActive || (viewport.Width <= 0) || (viewport.Height <= 0))
            return;

        Clock += dt;
        Step(dt, viewport, worldOrigin);
    }

    public abstract void Draw(SpriteBatch spriteBatch, Rectangle viewport, Vector2 worldOrigin);

    public abstract void ReleaseTextures();

    public void Dispose()
    {
        ReleaseTextures();
        GC.SuppressFinalize(this);
    }

    protected virtual void ResetState() { }

    protected virtual void Step(float dt, Rectangle viewport, Vector2 worldOrigin) { }

    protected static float ViewScale(Rectangle viewport) => MathF.Max(1f, viewport.Width / 640f);

    protected static Color Tint(int r, int g, int b, float alpha)
        => new(r, g, b, (byte)Math.Clamp((int)(255f * alpha), 0, 255));
}
