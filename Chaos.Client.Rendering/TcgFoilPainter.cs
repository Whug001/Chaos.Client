#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     The TCG foil look, painted on the CPU: a diagonal holo sheen that drifts through the spectrum over
///     <see cref="LoopSeconds" /> seconds, with ten fixed sparkles twinkling over it. Writes premultiplied bytes
///     (rgb * alpha), so the buffer uploads straight into a <see cref="Microsoft.Xna.Framework.Graphics.Texture2D" />.
///     Mirrors foil_mock.py (sheen strength 0.26, no glint).
/// </summary>
public static class TcgFoilPainter
{
    public const int Width = 60;
    public const int Height = 84;
    public const float LoopSeconds = 4f;

    private const double Strength = 0.26;
    private const double SparklePeriod = 1.6;
    private const double SparkleVisible = 0.8;
    private const double Diag = Width + (Height * 0.6);

    private static readonly (int X, int Y, double Offset)[] Sparkles =
    [
        (26, 27, 1.58),
        (9, 17, 3.29),
        (12, 54, 2.33),
        (38, 35, 0.15),
        (33, 61, 0.28),
        (11, 62, 0.24),
        (42, 23, 3.79),
        (46, 15, 2.31),
        (31, 14, 3.91),
        (8, 25, 1.16)
    ];

    /// <summary>
    ///     Fills <paramref name="pixels" /> (a <c>Width x Height</c> row-major buffer) with the foil frame for time
    ///     <paramref name="seconds" />. The result repeats every <see cref="LoopSeconds" />.
    /// </summary>
    public static void Paint(Color[] pixels, float seconds)
    {
        var t = (double)seconds % LoopSeconds;

        if (t < 0)
            t += LoopSeconds;

        for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
            {
                var i = (y * Width) + x;

                if (!Inside(x, y))
                {
                    pixels[i] = default;

                    continue;
                }

                var d = x + (y * 0.6);
                var hue = Math.Round((((d / Diag) * 1.3) - (t / LoopSeconds)) * 24.0) / 24.0;
                hue -= Math.Floor(hue);
                var (r, g, b) = HsvToRgb(hue, 0.45, 1.0);
                var wave = 0.5 + (0.5 * Math.Sin(Math.Tau * ((d / 70.0) - (t / LoopSeconds))));
                var alpha = Strength * (0.35 + (0.65 * wave));

                pixels[i] = Premultiplied(r, g, b, alpha);
            }

        foreach (var (sx, sy, offset) in Sparkles)
        {
            var phase = (t + offset) % SparklePeriod;

            if (phase > SparkleVisible)
                continue;

            var k = Math.Sin((phase / SparkleVisible) * Math.PI);

            SetSparkle(pixels, sx, sy, k);

            if (k > 0.45)
            {
                SetSparkle(pixels, sx + 1, sy, 0.85 * k);
                SetSparkle(pixels, sx - 1, sy, 0.85 * k);
                SetSparkle(pixels, sx, sy + 1, 0.85 * k);
                SetSparkle(pixels, sx, sy - 1, 0.85 * k);
            }

            if (k > 0.85)
            {
                SetSparkle(pixels, sx + 2, sy, 0.5 * k);
                SetSparkle(pixels, sx - 2, sy, 0.5 * k);
                SetSparkle(pixels, sx, sy + 2, 0.5 * k);
                SetSparkle(pixels, sx, sy - 2, 0.5 * k);
            }
        }
    }

    private static bool Inside(int x, int y, int r = 2)
    {
        var cx = Math.Min(Math.Max(x, r), (Width - 1) - r);
        var cy = Math.Min(Math.Max(y, r), (Height - 1) - r);
        var dx = x - cx;
        var dy = y - cy;

        return ((dx * dx) + (dy * dy)) <= ((r * r) + 1);
    }

    private static (int R, int G, int B) HsvToRgb(double h, double s, double v)
    {
        var sector = h * 6.0;
        var i = (int)sector % 6;
        var f = sector - Math.Floor(sector);
        var p = v * (1.0 - s);
        var q = v * (1.0 - (s * f));
        var t = v * (1.0 - (s * (1.0 - f)));

        (double R, double G, double B) rgb = i switch
        {
            0 => (v, t, p),
            1 => (q, v, p),
            2 => (p, v, t),
            3 => (p, q, v),
            4 => (t, p, v),
            _ => (v, p, q)
        };

        return ((int)Math.Round(rgb.R * 255), (int)Math.Round(rgb.G * 255), (int)Math.Round(rgb.B * 255));
    }

    private static Color Premultiplied(int r, int g, int b, double alpha)
        => new(
            (byte)Math.Clamp(Math.Round(r * alpha), 0, 255),
            (byte)Math.Clamp(Math.Round(g * alpha), 0, 255),
            (byte)Math.Clamp(Math.Round(b * alpha), 0, 255),
            (byte)Math.Clamp(Math.Round(alpha * 255), 0, 255));

    private static void SetSparkle(Color[] pixels, int x, int y, double alpha)
    {
        if ((x < 0) || (y < 0) || (x >= Width) || (y >= Height) || !Inside(x, y))
            return;

        pixels[(y * Width) + x] = Premultiplied(255, 255, 240, alpha);
    }
}
