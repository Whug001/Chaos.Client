#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Systems.Karting;

/// <summary>The vertical hair crop and chin origin for a rendered head.</summary>
public readonly record struct KartHeadCrop(int Top, int Bottom, Vector2 Origin);

/// <summary>Pixel-only crop and chin calculations for kart driver heads.</summary>
public static class KartHeadPixelMath
{
    public const int CROP_WIDTH = 44;
    private const int CHIN_ROWS = 3;
    private const byte VISIBLE_ALPHA = 16;

    /// <summary>Measures the haired crop and anchors it to the bald render's chin when available.</summary>
    public static KartHeadCrop? Measure(Color[] hairPixels, Color[]? baldPixels, int canvasWidth, int canvasHeight, int cropLeft, int cropWidth)
    {
        ArgumentNullException.ThrowIfNull(hairPixels);

        if ((canvasWidth <= 0) || (canvasHeight <= 0) || (hairPixels.Length != canvasWidth * canvasHeight))
            throw new ArgumentOutOfRangeException(nameof(canvasWidth));

        if (baldPixels is not null && baldPixels.Length != hairPixels.Length)
            throw new ArgumentException("The bald and haired renders must have matching dimensions.", nameof(baldPixels));

        var left = Math.Clamp(cropLeft, 0, canvasWidth);
        var right = Math.Clamp(cropLeft + cropWidth, left, canvasWidth);
        int? top = null;
        var bottom = -1;

        for (var y = 0; y < canvasHeight; y++)
            for (var x = left; x < right; x++)
                if (hairPixels[y * canvasWidth + x].A > VISIBLE_ALPHA)
                {
                    top ??= y;
                    bottom = y;

                    break;
                }

        if (top is not { } cropTop)
            return null;

        var chinPixels = baldPixels ?? hairPixels;
        var chinBottom = LastVisibleRow(chinPixels, canvasWidth, canvasHeight, left, right) ?? bottom;
        var minX = int.MaxValue;
        var maxX = -1;

        for (var y = Math.Max(0, chinBottom - CHIN_ROWS + 1); y <= chinBottom; y++)
            for (var x = left; x < right; x++)
                if (chinPixels[y * canvasWidth + x].A > VISIBLE_ALPHA)
                {
                    minX = Math.Min(minX, x);
                    maxX = Math.Max(maxX, x);
                }

        var center = maxX < 0 ? canvasWidth / 2f : (minX + maxX + 1) / 2f;

        return new KartHeadCrop(cropTop, bottom, new Vector2(center - left, chinBottom - cropTop + 1));
    }

    private static int? LastVisibleRow(Color[] pixels, int width, int height, int left, int right)
    {
        for (var y = height - 1; y >= 0; y--)
            for (var x = left; x < right; x++)
                if (pixels[y * width + x].A > VISIBLE_ALPHA)
                    return y;

        return null;
    }
}
