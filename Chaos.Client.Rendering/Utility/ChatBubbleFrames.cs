using Microsoft.Xna.Framework;
using SkiaSharp;

namespace Chaos.Client.Rendering.Utility;

/// <summary>
///     Chat bubble frames embedded in this assembly as "bubbleframe.&lt;n&gt;.png" (21 x 21, a 3 x 3 grid of 7 px cells).
///     A missing or wrong-sized frame reads as none, so the bubble still draws without it.
/// </summary>
public static class ChatBubbleFrames
{
    private static readonly Dictionary<byte, Color[]?> Cache = [];

    public static Color[]? Get(byte frame)
    {
        if (frame == 0)
            return null;

        if (Cache.TryGetValue(frame, out var cached))
            return cached;

        using var stream = typeof(ChatBubbleFrames).Assembly.GetManifestResourceStream($"bubbleframe.{frame}.png");
        var pixels = stream is null ? null : Decode(stream);
        Cache[frame] = pixels;

        return pixels;
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
