#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Utilities;

/// <summary>Hue/saturation/value conversions. Hue is 0-360 degrees; saturation and value are 0-1.</summary>
public static class HsvColor
{
    public static (float Hue, float Saturation, float Value) ToHsv(Color color)
    {
        var r = color.R / 255f;
        var g = color.G / 255f;
        var b = color.B / 255f;
        var max = MathF.Max(r, MathF.Max(g, b));
        var min = MathF.Min(r, MathF.Min(g, b));
        var delta = max - min;
        var saturation = max <= 0f ? 0f : delta / max;

        if (delta <= 0f)
            return (0f, saturation, max);

        float hue;

        if (max == r)
            hue = (g - b) / delta % 6f;
        else if (max == g)
            hue = ((b - r) / delta) + 2f;
        else
            hue = ((r - g) / delta) + 4f;

        hue *= 60f;

        return (hue < 0f ? hue + 360f : hue, saturation, max);
    }

    public static Color FromHsv(float hue, float saturation, float value)
    {
        hue = ((hue % 360f) + 360f) % 360f;
        saturation = Math.Clamp(saturation, 0f, 1f);
        value = Math.Clamp(value, 0f, 1f);

        var chroma = value * saturation;
        var x = chroma * (1f - MathF.Abs((hue / 60f % 2f) - 1f));
        var m = value - chroma;

        var (r, g, b) = (int)(hue / 60f) switch
        {
            0 => (chroma, x, 0f),
            1 => (x, chroma, 0f),
            2 => (0f, chroma, x),
            3 => (0f, x, chroma),
            4 => (x, 0f, chroma),
            _ => (chroma, 0f, x)
        };

        return new Color(r + m, g + m, b + m);
    }
}
