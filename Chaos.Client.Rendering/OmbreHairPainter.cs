#region
using SkiaSharp;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Ombre hair: the hair dye slots (palette indexes 98-103) of a head layer fade row by row from the root palette
///     to the tip palette. Every other pixel draws from the root palette and index 0 stays transparent, as in DALib's
///     SimpleRender. Rows are in the sprite's frame space (frame top + row), so the h, e and f layers line up.
/// </summary>
public static class OmbreHairPainter
{
    public static SKColor Blend(SKColor root, SKColor tip, int row, int top, int bottom)
    {
        if (bottom <= top)
            return root;

        var t = Math.Clamp((row - top) / (float)(bottom - top), 0f, 1f);

        return new SKColor(Mix(root.Red, tip.Red, t), Mix(root.Green, tip.Green, t), Mix(root.Blue, tip.Blue, t), Mix(root.Alpha, tip.Alpha, t));
    }

    /// <summary>The first and last rows holding a hair dye pixel, or null when there are none.</summary>
    public static (int First, int Last)? HairRows(int width, int height, byte[] data)
    {
        var first = -1;
        var last = -1;

        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                if (!GuildCloakGrid.IsDye(data[(y * width) + x]))
                    continue;

                if (first < 0)
                    first = y;

                last = y;

                break;
            }

        return first < 0 ? null : (first, last);
    }

    /// <summary>Row-major pixels for <see cref="GuildCloakPainter.ToImage" />.</summary>
    public static SKColor[] Paint(
        int width,
        int height,
        int frameTop,
        byte[] data,
        IList<SKColor> root,
        IList<SKColor> tip,
        int fadeTop,
        int fadeBottom)
    {
        var pixels = new SKColor[width * height];

        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var i = (y * width) + x;
                var index = data[i];

                if (index == 0)
                    continue;

                pixels[i] = GuildCloakGrid.IsDye(index)
                    ? Blend(root[index], tip[index], frameTop + y, fadeTop, fadeBottom)
                    : root[index];
            }

        return pixels;
    }

    private static byte Mix(byte from, byte to, float t) => (byte)MathF.Round(from + ((to - from) * t));
}
