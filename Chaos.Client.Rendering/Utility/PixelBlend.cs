using Microsoft.Xna.Framework;

namespace Chaos.Client.Rendering.Utility;

/// <summary>CPU compositing of premultiplied pixel buffers.</summary>
public static class PixelBlend
{
    /// <summary>
    ///     Draws <paramref name="src" /> over <paramref name="dst" /> with its top-left at (<paramref name="x" />,
    ///     <paramref name="y" />). Both buffers are premultiplied, as TextureConverter produces. Pixels that land outside
    ///     <paramref name="dst" /> are skipped.
    /// </summary>
    public static void SourceOver(
        Span<Color> dst,
        int dstWidth,
        int dstHeight,
        ReadOnlySpan<Color> src,
        int srcWidth,
        int srcHeight,
        int x,
        int y)
    {
        for (var sy = 0; sy < srcHeight; sy++)
        {
            var dy = y + sy;

            if ((dy < 0) || (dy >= dstHeight))
                continue;

            for (var sx = 0; sx < srcWidth; sx++)
            {
                var dx = x + sx;

                if ((dx < 0) || (dx >= dstWidth))
                    continue;

                var s = src[(sy * srcWidth) + sx];

                if (s.A == 0)
                    continue;

                ref var d = ref dst[(dy * dstWidth) + dx];

                if (s.A == 255)
                {
                    d = s;

                    continue;
                }

                var keep = 255 - s.A;

                d = new Color(
                    (byte)(s.R + (d.R * keep / 255)),
                    (byte)(s.G + (d.G * keep / 255)),
                    (byte)(s.B + (d.B * keep / 255)),
                    (byte)(s.A + (d.A * keep / 255)));
            }
        }
    }
}
