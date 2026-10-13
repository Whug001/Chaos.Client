#region
using System.Collections;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Which pixels of a sprite frame are drawn, one bit each, so a click can be tested against the art itself rather than
///     the frame's whole rectangle.
/// </summary>
public sealed class OpaqueMask
{
    private readonly BitArray Bits;

    public int Width { get; }
    public int Height { get; }

    /// <param name="pixels">Row-major, at least <paramref name="width" /> x <paramref name="height" />.</param>
    public OpaqueMask(ReadOnlySpan<Color> pixels, int width, int height)
    {
        Width = width;
        Height = height;
        Bits = new BitArray(width * height);

        for (var i = 0; i < Bits.Length; i++)
            Bits[i] = pixels[i].A > 0;
    }

    /// <summary>
    ///     True when the pixel at (<paramref name="x" />, <paramref name="y" />) of the frame as drawn is not transparent.
    ///     <paramref name="flip" /> is the draw's horizontal flip; a point outside the frame is never opaque.
    /// </summary>
    public bool IsOpaque(int x, int y, bool flip)
    {
        if ((x < 0) || (y < 0) || (x >= Width) || (y >= Height))
            return false;

        if (flip)
            x = Width - 1 - x;

        return Bits[(y * Width) + x];
    }
}
