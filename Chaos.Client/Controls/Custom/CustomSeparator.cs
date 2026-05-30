#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Utilities;
using Microsoft.Xna.Framework.Graphics;
using SkiaSharp;
#endregion

namespace Chaos.Client.Controls.Custom;

/// <summary>Orientation of a <see cref="CustomSeparator" />.</summary>
public enum SeparatorOrientation
{
    Horizontal,
    Vertical
}

/// <summary>
///     A thin divider that tiles a single dlgframe.epf edge piece along its length, so dividers share the same border
///     art as <see cref="CustomTextBox" /> / <see cref="CustomComboBox" />. A horizontal separator tiles the top (or
///     bottom) edge piece across its width; a vertical separator tiles the left (or right) edge piece down its height.
///     The cross-axis size is <b>cropped to the edge piece's actual visible bevel</b> (a few px) rather than the full
///     16px border tile — the surrounding tile is transparent padding — so the control's footprint matches the line you
///     see. The texture is baked at construction; create a new instance to change size/orientation.
/// </summary>
public sealed class CustomSeparator : UIPanel
{
    /// <param name="orientation">Whether the divider runs horizontally or vertically.</param>
    /// <param name="length">The length along the divider's axis (width if horizontal, height if vertical).</param>
    /// <param name="useFarEdge">Use the bottom/right edge piece instead of the top/left one.</param>
    public CustomSeparator(SeparatorOrientation orientation, int length, bool useFarEdge = false)
    {
        var vertical = orientation == SeparatorOrientation.Vertical;

        var raw = vertical
            ? DialogFrame.BuildVerticalSeparator(length, useFarEdge)
            : DialogFrame.BuildHorizontalSeparator(length, useFarEdge);

        SKImage? cropped = null;

        if (raw is not null)
        {
            cropped = CropCrossAxis(raw, vertical);
            raw.Dispose();
        }

        Background = ToTexture(cropped); //disposes the cropped image

        if (vertical)
        {
            Width = Background?.Width ?? DialogFrame.BORDER_SIZE; //cross-axis shrinks to the visible bevel
            Height = length;
        } else
        {
            Width = length;
            Height = Background?.Height ?? DialogFrame.BORDER_SIZE;
        }

        IsHitTestVisible = false; //a divider is purely decorative
    }

    //the separator owns a unique Background texture; UIPanel.Draw paints it and UIPanel.Dispose frees it.
    private static Texture2D? ToTexture(SKImage? image)
    {
        if (image is null)
            return null;

        using (image)
            return TextureConverter.ToTexture2D(image);
    }

    /// <summary>
    ///     Crops the baked strip to its opaque bevel along the cross-axis (columns for a vertical divider, rows for a
    ///     horizontal one), so the control's thickness matches the visible line instead of the full 16px tile. Always
    ///     returns a fresh image — the cropped region, or a full copy as a fallback when nothing opaque is found.
    /// </summary>
    private static SKImage CropCrossAxis(SKImage image, bool vertical)
    {
        var w = image.Width;
        var h = image.Height;

        using var pixmap = image.PeekPixels();

        if (pixmap is null)
            return CopyRegion(image, new SKRectI(0, 0, w, h));

        const byte ALPHA_THRESHOLD = 8;
        var lo = int.MaxValue;
        var hi = -1;

        if (vertical)
        {
            //find the first/last column that contains any opaque pixel.
            for (var x = 0; x < w; x++)
                for (var y = 0; y < h; y++)
                    if (pixmap.GetPixelColor(x, y).Alpha > ALPHA_THRESHOLD)
                    {
                        lo = System.Math.Min(lo, x);
                        hi = x;

                        break;
                    }

            return hi < 0 ? CopyRegion(image, new SKRectI(0, 0, w, h)) : CopyRegion(image, new SKRectI(lo, 0, hi + 1, h));
        }

        //horizontal: find the first/last row that contains any opaque pixel.
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
                if (pixmap.GetPixelColor(x, y).Alpha > ALPHA_THRESHOLD)
                {
                    lo = System.Math.Min(lo, y);
                    hi = y;

                    break;
                }

        return hi < 0 ? CopyRegion(image, new SKRectI(0, 0, w, h)) : CopyRegion(image, new SKRectI(0, lo, w, hi + 1));
    }

    private static SKImage CopyRegion(SKImage src, SKRectI rect)
    {
        var info = new SKImageInfo(rect.Width, rect.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        surface.Canvas.Clear(SKColors.Transparent);
        surface.Canvas.DrawImage(src, -rect.Left, -rect.Top);

        return surface.Snapshot();
    }
}
