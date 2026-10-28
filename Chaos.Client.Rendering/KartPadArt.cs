using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Chaos.DarkAges.Karting;

namespace Chaos.Client.Rendering;

public static class KartPadArt
{
    private const int TILE_WIDTH = 56;
    private const int TILE_HEIGHT = 28;
    private const int CHEVRON_WIDTH = 28;
    private const int CHEVRON_HEIGHT = 20;

    private static readonly Dictionary<GraphicsDevice, Texture2D> TileTextures = [];
    private static readonly Dictionary<GraphicsDevice, Texture2D[]> ChevronTextures = [];

    public static IEnumerable<(int X, int Y)> PadTiles(KartBoostPad pad)
    {
        for (var y = pad.Y0; y <= pad.Y1; y++)
            for (var x = pad.X0; x <= pad.X1; x++)
                yield return (x, y);
    }

    public static int FrameForHeading(double heading) => KartFrames.FrameFor(heading);

    public static Texture2D TileTexture(GraphicsDevice device)
    {
        if (TileTextures.TryGetValue(device, out var texture))
            return texture;

        var pixels = new Color[TILE_WIDTH * TILE_HEIGHT];

        for (var y = 0; y < TILE_HEIGHT; y++)
            for (var x = 0; x < TILE_WIDTH; x++)
            {
                var dx = Math.Abs(x - (TILE_WIDTH - 1) / 2f);
                var dy = Math.Abs(y - (TILE_HEIGHT - 1) / 2f);

                if ((dx / 28f) + (dy / 14f) <= 1f)
                    pixels[(y * TILE_WIDTH) + x] = Color.FromNonPremultiplied(255, 205, 0, 110);
            }

        texture = new Texture2D(device, TILE_WIDTH, TILE_HEIGHT);
        texture.SetData(pixels);
        TileTextures[device] = texture;

        return texture;
    }

    public static Texture2D ChevronTexture(GraphicsDevice device, int frame)
    {
        if (!ChevronTextures.TryGetValue(device, out var frames))
        {
            frames = new Texture2D[KartFrames.FRAMES];

            for (var i = 0; i < frames.Length; i++)
                frames[i] = BuildChevron(device, i);

            ChevronTextures[device] = frames;
        }

        return frames[frame % KartFrames.FRAMES];
    }

    private static Texture2D BuildChevron(GraphicsDevice device, int frame)
    {
        var pixels = new Color[CHEVRON_WIDTH * CHEVRON_HEIGHT];
        var angle = frame * Math.PI * 2 / KartFrames.FRAMES;
        var dirX = Math.Cos(angle);
        var dirY = Math.Sin(angle);
        var sideX = -dirY;
        var sideY = dirX;

        for (var y = 0; y < CHEVRON_HEIGHT; y++)
            for (var x = 0; x < CHEVRON_WIDTH; x++)
            {
                var dx = x - (CHEVRON_WIDTH - 1) / 2.0;
                var dy = y - (CHEVRON_HEIGHT - 1) / 2.0;
                var along = (dx * dirX) + (dy * dirY);
                var across = Math.Abs((dx * sideX) + (dy * sideY));
                var armDistance = Math.Abs(across - (8 - along) * 0.65);

                if ((along >= -7) && (along <= 8) && (armDistance <= 1.5) && (across <= 9))
                    pixels[(y * CHEVRON_WIDTH) + x] = new Color(255, 255, 80, 255);
            }

        var texture = new Texture2D(device, CHEVRON_WIDTH, CHEVRON_HEIGHT);
        texture.SetData(pixels);

        return texture;
    }
}