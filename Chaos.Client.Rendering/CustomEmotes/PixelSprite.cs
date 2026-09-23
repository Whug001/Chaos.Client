#region
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SkiaSharp;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     Turns <see cref="PixelArt" /> into pixels: a MonoGame texture for the world, and a SkiaSharp bitmap for icons and
///     previews.
/// </summary>
public static class PixelSprite
{
    /// <summary>Renders the art at 1:1 into a new bitmap. The caller owns the bitmap.</summary>
    public static SKBitmap ToBitmap(PixelArt art)
    {
        var bitmap = new SKBitmap(art.Width, art.Height);
        bitmap.Erase(SKColors.Transparent);

        for (var y = 0; y < art.Height; y++)
        for (var x = 0; x < art.Width; x++)
        {
            if (!art.Lookup(art.Rows[y][x], out var color) || (color.A == 0))
                continue;

            bitmap.SetPixel(x, y, new SKColor(color.R, color.G, color.B, color.A));
        }

        return bitmap;
    }

    /// <summary>Renders the art at 1:1 into a new texture. Returns null before the graphics device exists.</summary>
    public static Texture2D? ToTexture(PixelArt art)
    {
        var device = TextureConverter.Device;

        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (device is null)
            return null;

        var pixels = new Color[art.Width * art.Height];

        for (var y = 0; y < art.Height; y++)
        for (var x = 0; x < art.Width; x++)
        {
            if (!art.Lookup(art.Rows[y][x], out var color) || (color.A == 0))
                continue;

            pixels[y * art.Width + x] = Color.FromNonPremultiplied(color.R, color.G, color.B, color.A);
        }

        var texture = new Texture2D(device, art.Width, art.Height);
        texture.SetData(pixels);

        return texture;
    }

    /// <summary>
    ///     Returns a copy of <paramref name="source" /> with the layers painted over it. The source's top-left corner sits
    ///     at composite (<paramref name="sourceX" />, <paramref name="sourceY" />), and the layers are in composite
    ///     coordinates. The canvas grows in any direction to fit everything, so art above or beside the head is kept.
    /// </summary>
    public static SKImage Stamp(SKImage source, int sourceX, int sourceY, IReadOnlyList<PixelLayer> layers)
    {
        var minX = sourceX;
        var minY = sourceY;
        var maxX = sourceX + source.Width;
        var maxY = sourceY + source.Height;

        foreach (var layer in layers)
        {
            minX = Math.Min(minX, layer.X);
            minY = Math.Min(minY, layer.Y);
            maxX = Math.Max(maxX, layer.X + layer.Art.Width);
            maxY = Math.Max(maxY, layer.Y + layer.Art.Height);
        }

        using var bitmap = new SKBitmap(maxX - minX, maxY - minY);

        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawImage(source, sourceX - minX, sourceY - minY);

            foreach (var layer in layers)
            {
                using var art = ToBitmap(layer.Art);

                using var paint = new SKPaint
                {
                    Color = SKColors.White.WithAlpha((byte)Math.Clamp(layer.Opacity * 255f, 0f, 255f))
                };

                canvas.DrawBitmap(art, layer.X - minX, layer.Y - minY, paint);
            }
        }

        return SKImage.FromBitmap(bitmap);
    }
}
