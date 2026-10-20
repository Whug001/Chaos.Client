#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Utility;
using Chaos.Client.Utilities;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SkiaSharp;
#endregion

namespace Chaos.Client.Controls.World.Popups.Effects;

/// <summary>
///     The Effects window's big preview: the player standing on a pedestal while the selected effect loops over them,
///     timed by <see cref="EffectPreviewTimeline" />. Drawn at 1x and clipped to the pedestal.
/// </summary>
public sealed class EffectPreview : UIElement
{
    //(frame, flip, isFront): Down, Right, Up, Left. Standing frames of the walk sheet, as MountPreview.Facings.
    private static readonly (int Frame, bool Flip, bool IsFront)[] Facings =
    [
        (5, true, true),
        (5, false, true),
        (0, false, false),
        (0, true, false)
    ];

    private readonly AislingRenderer Renderer;
    private readonly EffectRenderer Effects;
    private readonly Texture2D Pedestal;
    private AislingAppearance? Look;
    private int Facing;
    private Texture2D? Figure;
    private int FigureTopPadding;
    private bool FigureFailed;
    private EffectWindowKind Kind;
    private EffectWindowEntry? Entry;
    private double ElapsedMs;

    public EffectPreview(AislingRenderer renderer, EffectRenderer effects, int width, int height)
    {
        Renderer = renderer;
        Effects = effects;
        Width = width;
        Height = height;
        Pedestal = DialogFrame.BuildRecessedTexture(new SKColor(24, 22, 30), width, height);
        IsHitTestVisible = false;
    }

    public void SetLook(AislingAppearance look)
    {
        Look = look;
        ElapsedMs = 0;
        ReleaseFigure();
    }

    /// <param name="facing">Down=0, Right=1, Up=2, Left=3.</param>
    public void SetFacing(int facing)
    {
        if (facing == Facing)
            return;

        Facing = facing;
        ReleaseFigure();
    }

    /// <summary>Shows a new effect from the start of its loop. Null or None shows the player alone.</summary>
    public void SetEffect(EffectWindowKind kind, EffectWindowEntry? entry)
    {
        if ((kind == Kind) && Equals(entry, Entry))
            return;

        Kind = kind;
        Entry = entry;
        ElapsedMs = 0;
    }

    /// <summary>Drops the rendered figure. Called when the window hides, so a closed window holds no GPU memory.</summary>
    public void ReleaseFigure()
    {
        Figure?.Dispose();
        Figure = null;
        FigureFailed = false;
    }

    public override void Dispose()
    {
        ReleaseFigure();
        Pedestal.Dispose();
        base.Dispose();
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (Visible)
            ElapsedMs += gameTime.ElapsedGameTime.TotalMilliseconds;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);

        if (!Visible)
            return;

        DrawTexture(spriteBatch, Pedestal, new Vector2(ScreenX, ScreenY), Color.White);

        var bounds = new Rectangle(ScreenX, ScreenY, Width, Height);

        //the tile centre the world would anchor both sprites to, placed so the body centre is the pedestal centre
        var centerX = ScreenX + (Width / 2);
        var centerY = ScreenY + (Height / 2) + AislingRenderer.CANVAS_CENTER_Y - AislingRenderer.BODY_CENTER_Y;
        var frame = CurrentFrame();

        if (frame.ShowFigure && (EnsureFigure() is { } figure))
            DrawClipped(
                spriteBatch,
                figure,
                new Rectangle(
                    centerX - AislingRenderer.CANVAS_CENTER_X,
                    centerY - AislingRenderer.CANVAS_CENTER_Y - FigureTopPadding,
                    figure.Width,
                    figure.Height),
                bounds);

        //the frame belongs to EffectRenderer's cache: drawn, never disposed
        if ((frame.EffectId > 0) && (Effects.GetFrame(frame.EffectId, frame.EffectFrame) is { } effect))
            DrawClipped(
                spriteBatch,
                effect.Texture,
                new Rectangle(
                    centerX - effect.CenterX + Math.Min(0, (int)effect.Left),
                    centerY - effect.CenterY + Math.Min(0, (int)effect.Top),
                    effect.Texture.Width,
                    effect.Texture.Height),
                bounds);
    }

    private PreviewFrame CurrentFrame()
    {
        if (Entry is not { Effect: > 0 } entry)
            return new PreviewFrame(true, 0, 0);

        return Kind == EffectWindowKind.Death
            ? EffectPreviewTimeline.Death(ClipFor(entry.Animation), entry.KeyTimeMs, ElapsedMs)
            : EffectPreviewTimeline.LoginLogout(ClipFor(entry.Animation), ClipFor(entry.LogoutAnimation), entry.KeyTimeMs, ElapsedMs);
    }

    private EffectClip ClipFor(ushort effectId)
        => Effects.GetEffectInfo(effectId) is { } info ? new EffectClip(effectId, info.FrameCount, info.FrameIntervalMs) : new EffectClip(effectId, 0, 0);

    private Texture2D? EnsureFigure()
    {
        if ((Figure is not null) || FigureFailed || Look is not { } look)
            return Figure;

        (var frame, var flip, var isFront) = Facings[Facing];
        Figure = Renderer.Render(in look, frame, out _, out FigureTopPadding, AislingRenderer.WALK_ANIM, flip, isFront);
        FigureFailed = Figure is null;

        return Figure;
    }

    private void DrawClipped(SpriteBatch spriteBatch, Texture2D texture, Rectangle destination, Rectangle bounds)
    {
        if (DrawClip.Clip(destination, bounds, out var clipped, out var source) && clipped.Intersects(ClipRect))
            spriteBatch.Draw(texture, clipped, source, Color.White);
    }
}
