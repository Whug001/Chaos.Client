#region
using Chaos.DarkAges.Definitions;
using DALib.Drawing;
using SkiaSharp;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Paints a guild cloak design onto one frame of the guild cloak sprite. Each pixel takes its color from the reference
///     grid's cell that <see cref="GuildCloakGrid.CellFor" /> picks, and keeps the frame's own light and shadow. The shading
///     numbers come from the design session's mapping test
///     (<c>Unora/.superpowers/brainstorm/345065-1790308407/spike/fullpaint.py</c>); the mapping rule is in
///     <c>docs/superpowers/specs/2026-09-25-guild-cloak-stretch-design.md</c>.
/// </summary>
public static class GuildCloakPainter
{
    /// <summary>Shade of an outline pixel (one with an empty neighbour above, below, left or right).</summary>
    public const float EDGE_SHADE = 0.42f;

    /// <summary>Shade of the darkest cloth. The frame pixel's brightness adds up to <see cref="LIGHT_SHADE" /> more.</summary>
    public const float BASE_SHADE = 0.78f;

    public const float LIGHT_SHADE = 0.75f;

    /// <summary>The brightness of the lightest cloth color in the cape's palette (slot 25).</summary>
    public const float FULL_BRIGHTNESS = 99f;

    /// <summary>The brightness a dye pixel gets when none of its neighbours is cloth.</summary>
    private const int DARK_BRIGHTNESS = 31;

    /// <summary>
    ///     The brightness a pixel's shade comes from: the largest channel of its palette color. The old rune and hem are drawn
    ///     in the dye slots, so a dye pixel borrows the middle brightness of the cloth around it and the rune disappears.
    /// </summary>
    public static int Brightness(byte[] data, IList<SKColor> palette, int width, int height, int x, int y)
    {
        var index = data[(y * width) + x];

        if (!GuildCloakGrid.IsDye(index))
            return MaxChannel(palette[index]);

        Span<int> near = stackalloc int[4];
        var count = 0;
        ReadOnlySpan<int> dx = [1, -1, 0, 0];
        ReadOnlySpan<int> dy = [0, 0, 1, -1];

        for (var i = 0; i < 4; i++)
        {
            var nx = x + dx[i];
            var ny = y + dy[i];

            if ((nx < 0) || (ny < 0) || (nx >= width) || (ny >= height))
                continue;

            var neighbour = data[(ny * width) + nx];

            if ((neighbour == 0) || GuildCloakGrid.IsDye(neighbour))
                continue;

            near[count++] = MaxChannel(palette[neighbour]);
        }

        if (count == 0)
            return DARK_BRIGHTNESS;

        near[..count].Sort();

        return near[count / 2];
    }

    /// <summary>The color number at a cell. An unpainted cell takes the nearest painted cell in its row; an empty row gives 1.</summary>
    public static int ColorNumberAt(GuildCloakGrid reference, ReadOnlySpan<byte> cells, int x, int y, int colorCount)
    {
        var width = reference.Width;

        if (cells.Length != width * reference.Height)
            return 1;

        for (var d = 0; d < width; d++)
        {
            var left = x - d;
            var right = x + d;

            if ((left >= 0) && (left < width) && IsColor(cells[(y * width) + left], colorCount))
                return cells[(y * width) + left];

            if ((right >= 0) && (right < width) && IsColor(cells[(y * width) + right], colorCount))
                return cells[(y * width) + right];
        }

        return 1;
    }

    public static SKColor[] Paint(
        EpfFrame frame,
        IList<SKColor> palette,
        GuildCloakGrid reference,
        ReadOnlySpan<byte> cells,
        IReadOnlyList<SKColor> colors,
        bool flip,
        bool centreOnRune = false)
        => Paint(
            frame.PixelWidth,
            frame.PixelHeight,
            frame.Data,
            palette,
            reference,
            cells,
            colors,
            flip,
            centreOnRune);

    /// <summary>
    ///     The painted pixels of a frame, row-major, the frame's size. Empty pixels stay transparent. For a draw the renderer
    ///     will flip, <paramref name="flip" /> reads the design mirrored, so the flip turns it the right way round.
    ///     <paramref name="centreOnRune" /> centres each row on the old rune (<see cref="GuildCloakGrid.RuneCentre" />); the
    ///     renderer sets it for the Back part only.
    /// </summary>
    public static SKColor[] Paint(
        int width,
        int height,
        byte[] data,
        IList<SKColor> palette,
        GuildCloakGrid reference,
        ReadOnlySpan<byte> cells,
        IReadOnlyList<SKColor> colors,
        bool flip,
        bool centreOnRune = false)
    {
        var result = new SKColor[width * height];
        var target = new GuildCloakGrid(width, height, data);

        if (target.IsEmpty || reference.IsEmpty || (colors.Count == 0))
            return result;

        for (var y = target.TopRow; y <= target.BottomRow; y++)
        {
            (var first, var last) = target.Row(y);

            if (first < 0)
                continue;

            for (var x = first; x <= last; x++)
            {
                if (data[(y * width) + x] == 0)
                    continue;

                (var cellX, var cellY) = reference.CellFor(target, x, y, flip, centreOnRune);
                var color = colors[ColorNumberAt(reference, cells, cellX, cellY, colors.Count) - 1];

                var shade = IsEdge(target, x, y)
                    ? EDGE_SHADE
                    : BASE_SHADE + (LIGHT_SHADE * Brightness(data, palette, width, height, x, y) / FULL_BRIGHTNESS);

                result[(y * width) + x] = Shade(color, shade);
            }
        }

        return result;
    }

    public static SKColor Shade(SKColor color, float shade)
        => new(
            Channel(color.Red, shade),
            Channel(color.Green, shade),
            Channel(color.Blue, shade),
            255);

    public static SKColor[] ToColors(IReadOnlyList<GuildCloakColor> colors)
    {
        var result = new SKColor[colors.Count];

        for (var i = 0; i < colors.Count; i++)
            result[i] = new SKColor(colors[i].R, colors[i].G, colors[i].B);

        return result;
    }

    /// <summary>Turns painted pixels into a layer image placed the way <c>Graphics.RenderImage</c> places a frame.</summary>
    public static SKImage ToImage(EpfFrame frame, SKColor[] pixels)
    {
        var offsetX = Math.Max(0, (int)frame.Left);
        var offsetY = Math.Max(0, (int)frame.Top);
        var width = frame.PixelWidth;
        var height = frame.PixelHeight;
        var bitmapWidth = width + offsetX;

        using var bitmap = new SKBitmap(
            bitmapWidth,
            height + offsetY,
            SKColorType.Bgra8888,
            SKAlphaType.Premul);

        using var pixmap = bitmap.PeekPixels();
        var buffer = pixmap.GetPixelSpan<SKColor>();
        buffer.Clear();

        for (var y = 0; y < height; y++)
            pixels.AsSpan(y * width, width)
                  .CopyTo(buffer.Slice(((y + offsetY) * bitmapWidth) + offsetX, width));

        return SKImage.FromBitmap(bitmap);
    }

    private static byte Channel(byte value, float shade) => (byte)Math.Clamp((int)(value * shade), 0, 255);

    private static bool IsColor(byte value, int colorCount) => (value >= 1) && (value <= colorCount);

    private static bool IsEdge(GuildCloakGrid target, int x, int y)
        => !target.IsFilled(x + 1, y) || !target.IsFilled(x - 1, y) || !target.IsFilled(x, y + 1) || !target.IsFilled(x, y - 1);

    private static int MaxChannel(SKColor color) => Math.Max(color.Red, Math.Max(color.Green, color.Blue));
}
