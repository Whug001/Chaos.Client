#region
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Paints a pumpkin's carving: lit (candle glow, brightest in the middle, with a slight flicker) or not yet lit (dark
///     holes). Works on a plain pixel buffer, so it is tested without a graphics device.
/// </summary>
public static class PumpkinPainter
{
    public const int FLICKER_MS = 150;
    public const int FLICKER_PHASES = 4;

    private static readonly float[] Flicker = [1f, 0.94f, 1f, 0.97f];

    /// <summary>Must match glow_rgb in make_pumpkin.py at phase 0.</summary>
    public static Color GlowColor(int glow, int phase)
    {
        var flicker = Flicker[phase % FLICKER_PHASES];

        return new Color(
            255,
            (int)Math.Clamp(MathF.Round((244 - (0.63f * glow)) * flicker), 0, 255),
            (int)Math.Clamp(MathF.Round((150 - glow) * flicker), 0, 255),
            255);
    }

    /// <summary>A carved pixel before the reveal: a dark hole, no candle yet.</summary>
    public static readonly Color HoleColor = new(58, 24, 8, 255);

    /// <summary>
    ///     Paints <paramref name="map" /> into a frame's pixels. The buffer's (0,0) is the frame's (<paramref name="left" />,
    ///     <paramref name="top" />).
    /// </summary>
    public static void Paint(
        Span<Color> pixels,
        int width,
        int height,
        int left,
        int top,
        IReadOnlyList<PumpkinFacePixel> map,
        ReadOnlySpan<byte> grid,
        int phase)
        => PaintCut(pixels, width, height, left, top, map, grid, glow => GlowColor(glow, phase));

    /// <summary>Paints a carving that is not lit yet: every pixel with a cut cell becomes <see cref="HoleColor" />.</summary>
    public static void PaintHoles(
        Span<Color> pixels,
        int width,
        int height,
        int left,
        int top,
        IReadOnlyList<PumpkinFacePixel> map,
        ReadOnlySpan<byte> grid)
        => PaintCut(pixels, width, height, left, top, map, grid, _ => HoleColor);

    private static void PaintCut(
        Span<Color> pixels,
        int width,
        int height,
        int left,
        int top,
        IReadOnlyList<PumpkinFacePixel> map,
        ReadOnlySpan<byte> grid,
        Func<int, Color> colorForGlow)
    {
        foreach (var pixel in map)
        {
            var x = pixel.X - left;
            var y = pixel.Y - top;

            if (((uint)x >= (uint)width) || ((uint)y >= (uint)height) || !AnyCut(pixel.Cells, grid))
                continue;

            pixels[(y * width) + x] = colorForGlow(pixel.Glow);
        }
    }

    public static int PhaseAt(long tickMs) => (int)(tickMs / FLICKER_MS % FLICKER_PHASES);

    private static bool AnyCut(int[] cells, ReadOnlySpan<byte> grid)
    {
        foreach (var cell in cells)
            if (PumpkinGrid.IsCut(grid, cell % PumpkinGrid.WIDTH, cell / PumpkinGrid.WIDTH))
                return true;

        return false;
    }
}
