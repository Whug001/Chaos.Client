#region
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Models;
using Chaos.Client.Rendering.Utility;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Effects;

/// <summary>Builds an Effects window tile: the player with one still frame of an effect, on one square texture.</summary>
public static class EffectTileRenderer
{
    public const int SIZE = 96;

    //the tile centre inside the canvas, placed so the body centre is the canvas centre
    public const int CENTER_X = SIZE / 2;
    public const int CENTER_Y = (SIZE / 2) + AislingRenderer.CANVAS_CENTER_Y - AislingRenderer.BODY_CENTER_Y;

    /// <summary>
    ///     A new texture the caller owns. <paramref name="effect" /> belongs to EffectRenderer's cache and is only read.
    ///     Null when there is nothing to draw.
    /// </summary>
    public static Texture2D? Compose(Texture2D? figure, int figureTopPadding, SpriteFrame? effect)
    {
        if ((figure is null) && (effect is null))
            return null;

        using var canvas = new PixelBufferScope(SIZE, SIZE);
        Array.Clear(canvas.Pixels, 0, canvas.Count);

        if (figure is not null)
        {
            using var source = new PixelBufferScope(figure);

            PixelBlend.SourceOver(
                canvas.AsSpan(),
                SIZE,
                SIZE,
                source.AsSpan(),
                source.Width,
                source.Height,
                CENTER_X - AislingRenderer.CANVAS_CENTER_X,
                CENTER_Y - AislingRenderer.CANVAS_CENTER_Y - figureTopPadding);
        }

        if (effect is { } frame)
        {
            using var source = new PixelBufferScope(frame.Texture);

            PixelBlend.SourceOver(
                canvas.AsSpan(),
                SIZE,
                SIZE,
                source.AsSpan(),
                source.Width,
                source.Height,
                CENTER_X - frame.CenterX + Math.Min(0, (int)frame.Left),
                CENTER_Y - frame.CenterY + Math.Min(0, (int)frame.Top));
        }

        var texture = new Texture2D(TextureConverter.Device, SIZE, SIZE);
        canvas.CommitTo(texture);

        return texture;
    }
}
