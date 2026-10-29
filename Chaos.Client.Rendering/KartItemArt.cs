#region
using Chaos.DarkAges.Karting;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Rendering;

public enum KartItemShape
{
    Cache,
    Wisp,
    Bolt,
    Snare,
    Ward
}

/// <summary>Stage 3 placeholder art for caches, wisps and item objects. Stage 4 replaces it.</summary>
public static class KartItemArt
{
    public static string Key(KartItemShape shape)
        => shape switch
        {
            KartItemShape.Cache => "cache",
            KartItemShape.Wisp => "wisp",
            KartItemShape.Bolt => "bolt",
            KartItemShape.Snare => "snare",
            KartItemShape.Ward => "ward",
            _ => throw new ArgumentOutOfRangeException(nameof(shape))
        };

    public static string? IconKey(KartItemKind kind)
        => kind == KartItemKind.None ? null : $"icon_{KartItems.Key(kind)}";

    public static string? MedalKey(int medal)
        => medal switch { 1 => "medal_gold", 2 => "medal_silver", 3 => "medal_bronze", _ => null };

    private const int RAVEN_SIZE = 16;

    private static readonly Dictionary<(GraphicsDevice Device, KartItemShape Shape), Texture2D> ShapeTextures = [];
    private static readonly Dictionary<GraphicsDevice, Texture2D[]> RavenTextures = [];

    public static int SpinFrame(double heading, int spinTicks) => KartFrames.FrameFor(heading + spinTicks * 2 * Math.PI / 15);

    public static (int Width, int Height, Color[] Pixels) Pixels(KartItemShape shape)
        => shape switch
        {
            KartItemShape.Cache => Draw(20, 20, (dx, dy) => Math.Abs(dx) + Math.Abs(dy) <= 9 ? Color.FromNonPremultiplied(0, 220, 255, 220) : Color.Transparent),
            KartItemShape.Wisp  => Draw(8, 8, (dx, dy) => dx * dx + dy * dy <= 12 ? Color.FromNonPremultiplied(255, 240, 150, 255) : Color.Transparent),
            KartItemShape.Bolt  => Draw(10, 10, (dx, dy) => dx * dx + dy * dy <= 20 ? Color.FromNonPremultiplied(255, 120, 20, 255) : Color.Transparent),
            KartItemShape.Snare => Draw(14, 14, (dx, dy) => Math.Abs(Math.Abs(dx) - Math.Abs(dy)) <= 1.5 ? Color.FromNonPremultiplied(40, 160, 40, 255) : Color.Transparent),
            KartItemShape.Ward  => Draw(18, 18, (dx, dy) => Math.Abs(Math.Sqrt(dx * dx + dy * dy) - 7) <= 1.5 ? Color.FromNonPremultiplied(80, 140, 255, 230) : Color.Transparent),
            _                   => throw new ArgumentOutOfRangeException(nameof(shape))
        };

    /// <summary>A dark arrow pointing along the screen direction of <paramref name="frame" />.</summary>
    public static Color[] RavenPixels(int frame)
    {
        var angle = frame * 2 * Math.PI / KartFrames.FRAMES;
        var (fx, fy) = (Math.Cos(angle), Math.Sin(angle));

        return Draw(RAVEN_SIZE, RAVEN_SIZE, (dx, dy) =>
        {
            var along = dx * fx + dy * fy;
            var across = Math.Abs(-dx * fy + dy * fx);

            return (along >= -6) && (along <= 7) && (across <= (7 - along) * 0.5) ? Color.FromNonPremultiplied(40, 40, 48, 255) : Color.Transparent;
        }).Pixels;
    }

    public static Texture2D Texture(GraphicsDevice device, KartItemShape shape)
    {
        if (ShapeTextures.TryGetValue((device, shape), out var texture))
            return texture;

        var (width, height, pixels) = Pixels(shape);
        texture = new Texture2D(device, width, height);
        texture.SetData(pixels);
        ShapeTextures[(device, shape)] = texture;

        return texture;
    }

    public static Texture2D RavenTexture(GraphicsDevice device, int frame)
    {
        if (!RavenTextures.TryGetValue(device, out var textures))
        {
            textures = new Texture2D[KartFrames.FRAMES];
            RavenTextures[device] = textures;
        }

        if (textures[frame] is { } texture)
            return texture;

        texture = new Texture2D(device, RAVEN_SIZE, RAVEN_SIZE);
        texture.SetData(RavenPixels(frame));
        textures[frame] = texture;

        return texture;
    }

    private static (int Width, int Height, Color[] Pixels) Draw(int width, int height, Func<double, double, Color> shade)
    {
        var pixels = new Color[width * height];

        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                pixels[y * width + x] = shade(x - (width - 1) / 2.0, y - (height - 1) / 2.0);

        return (width, height, pixels);
    }
}
