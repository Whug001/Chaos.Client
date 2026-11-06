using System.Collections.Concurrent;
using Microsoft.Xna.Framework;
using SkiaSharp;

namespace Chaos.Client.Rendering.Utility;

/// <summary>
///     Chat bubble frames embedded in this assembly as "bubbleframe.&lt;n&gt;.png" (21 x 21, a 3 x 3 grid of 7 px cells).
///     A missing or wrong-sized frame reads as none, so the bubble still draws without it.
///     Frames 9 and 10 also have "bubbleframe.&lt;n&gt;-head.png" and "bubbleframe.&lt;n&gt;-tail.png" decor of any size.
/// </summary>
public static class ChatBubbleFrames
{
    /// <summary>Decor pixels with their size, since decor is drawn as its own texture rather than tiled.</summary>
    public sealed record DecorImage(Color[] Pixels, int Width, int Height);

    private static readonly ConcurrentDictionary<byte, Color[]?> Cache = new();
    private static readonly ConcurrentDictionary<(byte Frame, string Part), DecorImage?> DecorCache = new();

    public static Color[]? Get(byte frame)
    {
        if (frame == 0)
            return null;

        return Cache.GetOrAdd(
            frame,
            static frame =>
            {
                using var stream = typeof(ChatBubbleFrames).Assembly.GetManifestResourceStream($"bubbleframe.{frame}.png");
                return stream is null ? null : Decode(stream);
            });
    }

    /// <summary>
    ///     The "bubbleframe.&lt;frame&gt;-&lt;part&gt;.png" decor ("head" or "tail") at its own size, or null when the
    ///     frame has none. Only frames keep the 21 x 21 rule; decor may be any size.
    /// </summary>
    public static DecorImage? GetDecor(byte frame, string part)
    {
        if (frame == 0)
            return null;

        return DecorCache.GetOrAdd(
            (frame, part),
            static key =>
            {
                using var stream = typeof(ChatBubbleFrames).Assembly.GetManifestResourceStream($"bubbleframe.{key.Frame}-{key.Part}.png");
                return stream is null ? null : DecodeAnySize(stream);
            });
    }

    public static DecorImage? DecodeAnySize(Stream stream)
    {
        using var bitmap = SKBitmap.Decode(stream);

        if (bitmap is null || (bitmap.Width <= 0) || (bitmap.Height <= 0))
            return null;

        var pixels = new Color[bitmap.Width * bitmap.Height];

        for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
            {
                var p = bitmap.GetPixel(x, y);
                pixels[(y * bitmap.Width) + x] = Color.FromNonPremultiplied(p.Red, p.Green, p.Blue, p.Alpha);
            }

        return new DecorImage(pixels, bitmap.Width, bitmap.Height);
    }

    public static Color[]? Decode(Stream stream)
    {
        using var bitmap = SKBitmap.Decode(stream);

        if (bitmap is null || (bitmap.Width != ChatBubblePainter.FRAME_SIZE) || (bitmap.Height != ChatBubblePainter.FRAME_SIZE))
            return null;

        var pixels = new Color[ChatBubblePainter.FRAME_SIZE * ChatBubblePainter.FRAME_SIZE];

        for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
            {
                var p = bitmap.GetPixel(x, y);
                pixels[(y * bitmap.Width) + x] = Color.FromNonPremultiplied(p.Red, p.Green, p.Blue, p.Alpha);
            }

        return pixels;
    }
}
