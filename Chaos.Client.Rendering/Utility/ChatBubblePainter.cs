using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Rendering.Utility;

/// <summary>
///     Draws a chat bubble's body, tail, effect and frame into a cleared pixel buffer. No graphics device, so it is
///     testable. The buffer is <see cref="BufferSize" />; a glow adds <see cref="GLOW_MARGIN" /> on every side.
/// </summary>
public static class ChatBubblePainter
{
    public const int FRAME_CELL = 7;
    public const int FRAME_SIZE = FRAME_CELL * 3;
    public const int GLOW_MARGIN = 2;
    public const int TAIL_HEIGHT = 4;

    private static readonly Color DefaultFill = new(0, 0, 0, 85);

    public static int MarginFor(ChatBubbleEffect effect) => effect == ChatBubbleEffect.Glow ? GLOW_MARGIN : 0;

    public static (int Width, int Height) BufferSize(int bodyWidth, int bodyHeight, ChatBubbleEffect effect)
    {
        var margin = MarginFor(effect);

        return (bodyWidth + (2 * margin), bodyHeight + TAIL_HEIGHT + (2 * margin));
    }

    public static Color ToColor(uint rgba)
    {
        var (r, g, b, a) = ChatBubbleColors.Unpack(rgba);

        return Color.FromNonPremultiplied(r, g, b, a);
    }

    public static void Paint(Color[] pixels, int bodyWidth, int bodyHeight, ChatBubbleLook look, Color[]? frame)
    {
        var margin = MarginFor(look.Effect);
        var (bufferWidth, bufferHeight) = BufferSize(bodyWidth, bodyHeight, look.Effect);

        //the default keeps today's exact colors; FromNonPremultiplied would round them differently
        var (border, fill, accent) = look.IsDefault
            ? (Color.White, DefaultFill, Color.White)
            : (ToColor(look.Border), ToColor(look.Fill), ToColor(look.Accent));

        var last = Math.Max(1, bodyHeight - 1);

        Func<int, Color> borderAt = look.Effect == ChatBubbleEffect.GradientBorder
            ? row => Color.Lerp(border, accent, row / (float)last)
            : _ => border;

        Func<int, Color> fillAt = look.Effect == ChatBubbleEffect.GradientFill
            ? row => Color.Lerp(fill, accent, row / (float)last)
            : _ => fill;

        //the default keeps the legacy order (body, then tail) for pixel parity; styled looks draw the tail
        //first so the bottom edge keeps its effect color where the tail base overlaps it
        if (!look.IsDefault)
            DrawTail(pixels, bufferWidth, margin, bodyWidth, bodyHeight, borderAt, fillAt);

        ImageUtil.DrawBubbleBody(pixels, bufferWidth, margin, margin, bodyWidth, bodyHeight, borderAt, fillAt);

        if (look.Effect == ChatBubbleEffect.TwoToneBorder)
            ImageUtil.DrawBubbleBody(pixels, bufferWidth, margin + 1, margin + 1, bodyWidth - 2, bodyHeight - 2, _ => accent, fillAt);

        if (look.IsDefault)
            DrawTail(pixels, bufferWidth, margin, bodyWidth, bodyHeight, borderAt, fillAt);

        if (frame is { Length: FRAME_SIZE * FRAME_SIZE })
            DrawFrame(pixels, bufferWidth, bufferHeight, margin, margin, bodyWidth, bodyHeight, frame);

        if (look.Effect == ChatBubbleEffect.Glow)
            DrawGlow(pixels, bufferWidth, bufferHeight, accent);
    }

    private static void DrawTail(
        Color[] pixels,
        int bufferWidth,
        int margin,
        int bodyWidth,
        int bodyHeight,
        Func<int, Color> borderAt,
        Func<int, Color> fillAt)
        => ImageUtil.DrawBubbleTail(
            pixels,
            bufferWidth,
            margin + (bodyWidth / 2),
            margin + bodyHeight - 1,
            borderAt(bodyHeight - 1),
            fillAt(bodyHeight - 1));

    private static void DrawFrame(Color[] pixels, int bufferWidth, int bufferHeight, int x, int y, int width, int height, Color[] frame)
    {
        var right = x + width - FRAME_CELL;
        var bottom = y + height - FRAME_CELL;

        for (var cx = x + FRAME_CELL; cx < right; cx += FRAME_CELL)
        {
            var w = Math.Min(FRAME_CELL, right - cx);
            Blit(pixels, bufferWidth, bufferHeight, frame, 1, 0, cx, y, w, FRAME_CELL);
            Blit(pixels, bufferWidth, bufferHeight, frame, 1, 2, cx, bottom, w, FRAME_CELL);
        }

        for (var cy = y + FRAME_CELL; cy < bottom; cy += FRAME_CELL)
        {
            var h = Math.Min(FRAME_CELL, bottom - cy);
            Blit(pixels, bufferWidth, bufferHeight, frame, 0, 1, x, cy, FRAME_CELL, h);
            Blit(pixels, bufferWidth, bufferHeight, frame, 2, 1, right, cy, FRAME_CELL, h);
        }

        Blit(pixels, bufferWidth, bufferHeight, frame, 0, 0, x, y, FRAME_CELL, FRAME_CELL);
        Blit(pixels, bufferWidth, bufferHeight, frame, 2, 0, right, y, FRAME_CELL, FRAME_CELL);
        Blit(pixels, bufferWidth, bufferHeight, frame, 0, 2, x, bottom, FRAME_CELL, FRAME_CELL);
        Blit(pixels, bufferWidth, bufferHeight, frame, 2, 2, right, bottom, FRAME_CELL, FRAME_CELL);
    }

    //alpha-over of one premultiplied frame cell, clipped to w x h
    private static void Blit(
        Color[] pixels,
        int bufferWidth,
        int bufferHeight,
        Color[] frame,
        int cellX,
        int cellY,
        int dx,
        int dy,
        int w,
        int h)
    {
        for (var row = 0; row < h; row++)
            for (var col = 0; col < w; col++)
            {
                var src = frame[(((cellY * FRAME_CELL) + row) * FRAME_SIZE) + (cellX * FRAME_CELL) + col];
                var px = dx + col;
                var py = dy + row;

                if ((src.A == 0) || (px < 0) || (py < 0) || (px >= bufferWidth) || (py >= bufferHeight))
                    continue;

                var index = (py * bufferWidth) + px;
                pixels[index] = Over(src, pixels[index]);
            }
    }

    private static Color Over(Color src, Color dst)
    {
        var keep = 255 - src.A;

        return new Color(
            (byte)(src.R + ((dst.R * keep) / 255)),
            (byte)(src.G + ((dst.G * keep) / 255)),
            (byte)(src.B + ((dst.B * keep) / 255)),
            (byte)(src.A + ((dst.A * keep) / 255)));
    }

    //two rings around everything drawn so far: half strength, then a quarter
    private static void DrawGlow(Color[] pixels, int bufferWidth, int bufferHeight, Color accent)
    {
        Ring(pixels, bufferWidth, bufferHeight, accent * 0.5f);
        Ring(pixels, bufferWidth, bufferHeight, accent * 0.25f);
    }

    private static void Ring(Color[] pixels, int bufferWidth, int bufferHeight, Color color)
    {
        var count = bufferWidth * bufferHeight;
        var solid = new bool[count];

        for (var i = 0; i < count; i++)
            solid[i] = pixels[i].A > 0;

        for (var y = 0; y < bufferHeight; y++)
            for (var x = 0; x < bufferWidth; x++)
            {
                var index = (y * bufferWidth) + x;

                if (solid[index])
                    continue;

                var touches = ((x > 0) && solid[index - 1])
                              || ((x < (bufferWidth - 1)) && solid[index + 1])
                              || ((y > 0) && solid[index - bufferWidth])
                              || ((y < (bufferHeight - 1)) && solid[index + bufferWidth]);

                if (touches)
                    pixels[index] = color;
            }
    }
}
