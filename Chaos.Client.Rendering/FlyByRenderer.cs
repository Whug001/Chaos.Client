#region
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Full-viewport overlay for things that fly past in groups now and then — the bats. The flight itself is a
///     <see cref="FlyByFlock" />; this class fades the whole effect in and out and draws each flyer's current frame as a
///     1:1 pixel sprite. Switched off, no new group starts, and the flyers already in the air fade with the effect.
///     Touched only on the game-loop thread.
/// </summary>
public sealed class FlyByRenderer : IAmbientOverlay
{
    private readonly FlyByStyle Style;
    private readonly FlyByFlock Flock;

    private Texture2D[]? FrameTextures;
    private Vector2? LastWorldOrigin;
    private bool Active;
    private float EffectAlpha; // 0..1 current fade level

    public FlyByRenderer(FlyByStyle style)
    {
        Style = style;
        Flock = new FlyByFlock(style, new Random());
    }

    /// <inheritdoc />
    public BlendState BlendState => BlendState.NonPremultiplied;

    /// <summary>How many flyers are in the air. Tests use it.</summary>
    public int FlyerCount => Flock.Flyers.Count;

    /// <inheritdoc />
    public bool IsActive => Active || (EffectAlpha > 0f);

    /// <inheritdoc />
    public void SetActive(bool on, bool immediate = false)
    {
        //clear the flyers when the effect starts from nothing (a map change, or switched on after a full fade-out, so a
        //stale group never resumes mid-screen) or is switched off immediately. An immediate "on" while already active
        //(an unrelated F4 re-apply) keeps the bats in flight.
        if ((on && !IsActive) || (immediate && !on))
        {
            Flock.Reset();
            LastWorldOrigin = null;
        }

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

        var step = dt / Style.FadeSeconds;
        EffectAlpha = Active ? MathF.Min(1f, EffectAlpha + step) : MathF.Max(0f, EffectAlpha - step);

        if (!IsActive || (viewport.Width <= 0) || (viewport.Height <= 0))
        {
            //forget the camera so the next activation doesn't shift everything by the distance walked meanwhile
            LastWorldOrigin = null;

            return;
        }

        var cameraShift = LastWorldOrigin is { } last ? worldOrigin - last : Vector2.Zero;
        LastWorldOrigin = worldOrigin;

        Flock.Update(dt, new Vector2(viewport.Width, viewport.Height), cameraShift, Active);
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch spriteBatch, Rectangle viewport, Vector2 worldOrigin)
    {
        if ((EffectAlpha <= 0f) || (Flock.Flyers.Count == 0))
            return;

        var device = spriteBatch.GraphicsDevice;

        FrameTextures ??= Style.Frames
                               .Select(rows => SpriteGrid.Build(device, rows, Color.Black))
                               .ToArray();

        var alpha = (byte)Math.Clamp((int)(255f * Style.Alpha * EffectAlpha), 0, 255);
        var color = new Color(Style.Color.R, Style.Color.G, Style.Color.B, alpha);

        foreach (var flyer in Flock.Flyers)
        {
            var texture = FrameTextures[Flock.FrameOf(flyer)];

            var topLeft = new Vector2(
                viewport.X + MathF.Round(flyer.Position.X - (texture.Width / 2f)),
                viewport.Y + MathF.Round(flyer.Position.Y - (texture.Height / 2f)));

            spriteBatch.Draw(texture, topLeft, color);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (FrameTextures is not null)
            foreach (var texture in FrameTextures)
                texture.Dispose();

        FrameTextures = null;
    }
}
