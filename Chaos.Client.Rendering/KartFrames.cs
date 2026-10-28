#region
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Rendering;

public static class KartFrames
{
    public const int FRAMES = 16;

    private static readonly Dictionary<GraphicsDevice, Texture2D[]> Textures = [];

    public static int FrameFor(double heading)
    {
        var cos = Math.Cos(heading);
        var sin = Math.Sin(heading);
        var angle = Math.Atan2(14 * (cos + sin), 28 * (cos - sin));
        var turn = Math.PI * 2;
        var normal = (angle % turn + turn) % turn;

        return (int)Math.Round(normal / (turn / FRAMES)) % FRAMES;
    }

    public static (int TileX, int TileY, float OffsetX, float OffsetY) RideOffset(double x, double y)
    {
        var tileX = (int)Math.Floor(x);
        var tileY = (int)Math.Floor(y);
        var dx = x - (tileX + 0.5);
        var dy = y - (tileY + 0.5);

        return (tileX, tileY, (float)(28 * (dx - dy)), (float)(14 * (dx + dy)));
    }

    public static Texture2D Texture(GraphicsDevice device, int frame)
    {
        if (!Textures.TryGetValue(device, out var frames))
        {
            frames = new Texture2D[FRAMES];

            for (var i = 0; i < FRAMES; i++)
                frames[i] = Build(device, i);

            Textures[device] = frames;
        }

        return frames[frame % FRAMES];
    }

    private static Texture2D Build(GraphicsDevice device, int frame)
    {
        const int width = 40;
        const int height = 24;
        var pixels = new Color[width * height];
        var angle = frame * Math.PI * 2 / FRAMES;
        var noseX = Math.Cos(angle);
        var noseY = Math.Sin(angle);

        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var dx = x - 19.5;
                var dy = y - 11.5;

                if ((dx * dx / 361) + (dy * dy / 100) > 1)
                    continue;

                var projection = (dx * noseX) + (dy * noseY);
                var cross = Math.Abs((dx * noseY) - (dy * noseX));
                pixels[(y * width) + x] = (projection > 5) && (cross < (projection - 4) * 0.45) ? new Color(48, 48, 48) : Color.White;
            }

        var texture = new Texture2D(device, width, height);
        texture.SetData(pixels);

        return texture;
    }
}
