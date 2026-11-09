using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Rendering;

internal static class AmbientTextures
{
    public static Texture2D Create(GraphicsDevice device, int width, int height, Func<int, int, Color> pixel)
    {
        var data = new Color[width * height];

        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                data[(y * width) + x] = pixel(x, y);

        var texture = new Texture2D(device, width, height);
        texture.SetData(data);

        return texture;
    }

    public static Texture2D Pixel(GraphicsDevice device) => Create(device, 1, 1, static (_, _) => Color.White);

    public static Texture2D SoftDot(GraphicsDevice device, int size = 64)
        => Create(device, size, size, (x, y) =>
        {
            var dx = (x + 0.5f - (size / 2f)) / (size / 2f);
            var dy = (y + 0.5f - (size / 2f)) / (size / 2f);
            var d = MathF.Sqrt((dx * dx) + (dy * dy));
            var a = d < 0.5f ? MathHelper.Lerp(1f, 0.45f, d / 0.5f) : d < 1f ? MathHelper.Lerp(0.45f, 0f, (d - 0.5f) / 0.5f) : 0f;

            return White(a);
        });

    public static Texture2D Ring(GraphicsDevice device)
        => Create(device, 64, 32, static (x, y) =>
        {
            var dx = (x + 0.5f - 32f) / 32f;
            var dy = (y + 0.5f - 16f) / 16f;
            var d = MathF.Sqrt((dx * dx) + (dy * dy));

            return White(MathHelper.Clamp(1f - (MathF.Abs(d - 0.94f) / 0.06f), 0f, 1f));
        });

    public static Texture2D VerticalAlpha(GraphicsDevice device, int width, int height, params (float At, float Alpha)[] stops)
        => Create(device, width, height, (_, y) => White(Sample(stops, y / (height - 1f))));

    public static Texture2D Gradient(GraphicsDevice device, int length, int thickness, params (float At, Color Color)[] stops)
        => Create(device, length, thickness, (x, _) =>
        {
            var at = x / (length - 1f);

            for (var i = 1; i < stops.Length; i++)
                if (at <= stops[i].At)
                    return Color.Lerp(stops[i - 1].Color, stops[i].Color, (at - stops[i - 1].At) / (stops[i].At - stops[i - 1].At));

            return stops[^1].Color;
        });

    public static float Sample((float At, float Alpha)[] stops, float at)
    {
        for (var i = 1; i < stops.Length; i++)
            if (at <= stops[i].At)
                return MathHelper.Lerp(stops[i - 1].Alpha, stops[i].Alpha, (at - stops[i - 1].At) / (stops[i].At - stops[i - 1].At));

        return stops[^1].Alpha;
    }

    public static Color White(float alpha) => new((byte)255, (byte)255, (byte)255, (byte)Math.Clamp((int)(255f * alpha), 0, 255));
}
