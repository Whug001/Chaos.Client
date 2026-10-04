#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Rendering;
using Chaos.Client.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SkiaSharp;
#endregion

namespace Chaos.Client.Controls.World.Popups.Beauty;

/// <summary>
///     A figure on a recessed pedestal. Owns exactly one composited texture at a time --
///     <see cref="AislingRenderer.Render" /> allocates a fresh texture per call and caches only per world entity, so this
///     view caches by (appearance, facing) and disposes the previous texture on every re-render (see
///     PokerTableControl's PortraitView for the same reasoning). Zoom is a pure draw-time scale -- it never invalidates
///     the cache.
/// </summary>
public sealed class MirrorPreview : UIElement
{
    public const int FACING_COUNT = 4;

    //(frame, flip, isFront): Down, Right, Up, Left. Epfs hold up (0-4) and right (5-9); down = right flipped, left = up flipped.
    private static readonly (int Frame, bool Flip, bool IsFront)[] FACINGS =
    [
        (5, true, true),
        (5, false, true),
        (0, false, false),
        (0, true, false)
    ];

    private readonly AislingRenderer Renderer;
    private readonly Texture2D Pedestal;

    private Texture2D? Figure;
    private AislingAppearance? RenderedAppearance;
    private int RenderedFacing = -1;

    public bool Zoomed { get; set; } = true;

    public MirrorPreview(AislingRenderer renderer, int width, int height)
    {
        Renderer = renderer;
        Width = width;
        Height = height;
        Pedestal = DialogFrame.BuildRecessedTexture(new SKColor(24, 22, 30), width, height);
    }

    /// <summary>The "Show gear" checkbox both the preview column and the review page put under their figures. It does not toggle itself.</summary>
    public static CustomCheckBox CreateGearToggle(int x, int y)
        => new()
        {
            X = x,
            Y = y,
            Width = CustomCheckBox.CHECKBOX_SIZE + CustomCheckBox.CAPTION_GAP + (9 * TextRenderer.CHAR_WIDTH),
            Height = CustomCheckBox.CHECKBOX_SIZE,
            Text = "Show gear",
            Checked = false
        };

    public void Refresh(AislingAppearance appearance, int facingIndex)
    {
        if (Nullable.Equals(appearance, RenderedAppearance) && (facingIndex == RenderedFacing))
            return;

        RenderedAppearance = appearance;
        RenderedFacing = facingIndex;

        Figure?.Dispose();
        var (frame, flip, isFront) = FACINGS[facingIndex];
        Figure = Renderer.Render(in appearance, frame, AislingRenderer.IDLE_ANIM, flip, isFront);

        if (Figure is null)
            RenderedFacing = -1;
    }

    /// <summary>Drops the texture on hide so a closed panel holds no GPU memory; the next Refresh re-renders.</summary>
    public void Release()
    {
        Figure?.Dispose();
        Figure = null;
        RenderedAppearance = null;
        RenderedFacing = -1;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);

        if (!Visible)
            return;

        DrawTexture(spriteBatch, Pedestal, new Vector2(ScreenX, ScreenY), Color.White);

        if (Figure is null)
            return;

        var scale = Zoomed ? 2 : 1;

        //DrawTextureFitted culls when the destination rect doesn't intersect ClipRect at all -- it does not
        //clip to it -- so an oversized composite (e.g. Show gear on with a tall equip layer) can paint outside
        //the pedestal. Fall back to 1x for this draw alone (the toggle state itself is untouched) when 2x
        //wouldn't fit.
        if (((Figure.Height * scale) > (Height - 12)) || ((Figure.Width * scale) > Width))
            scale = 1;

        var w = Figure.Width * scale;
        var h = Figure.Height * scale;

        //anchor on the body centre so the figure stays centred in the pedestal at 1x and 2x and doesn't drift
        //sideways between poses whose padded canvases differ in width
        var x = ScreenX + (Width / 2) - (AislingRenderer.CANVAS_CENTER_X * scale);
        var y = ScreenY + (Height / 2) - (AislingRenderer.BODY_CENTER_Y * scale);

        DrawTextureFitted(spriteBatch, Figure, new Rectangle(x, y, w, h), Color.White);
    }

    public override void Dispose()
    {
        Release();
        Pedestal.Dispose();
        base.Dispose();
    }
}
