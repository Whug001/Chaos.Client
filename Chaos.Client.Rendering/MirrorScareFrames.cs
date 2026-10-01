#region
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     The four fullscreen jumpscare frames: a pale face lunging, then black. Drawn chunky and scaled up to the view.
/// </summary>
public sealed class MirrorScareFrames : IDisposable
{
    public const int WIDTH = 160;
    public const int HEIGHT = 120;

    private readonly Texture2D[] Frames;

    public MirrorScareFrames(GraphicsDevice device)
    {
        Frames = new Texture2D[FRAME_COUNT];

        for (var frame = 0; frame < Frames.Length; frame++)
        {
            var texture = new Texture2D(device, WIDTH, HEIGHT);
            texture.SetData(Pixels(frame));
            Frames[frame] = texture;
        }
    }

    public const int FRAME_COUNT = 4;

    public Texture2D this[int frame] => Frames[frame];

    public void Dispose()
    {
        foreach (var frame in Frames)
            frame.Dispose();
    }

    /// <summary>Frame 0 is far, 1 is closer, 2 fills the glass, 3 is black.</summary>
    public static Color[] Pixels(int frame)
    {
        var pixels = new Color[WIDTH * HEIGHT];

        if (frame >= 3)
        {
            Array.Fill(pixels, Color.Black);

            return pixels;
        }

        var grow = frame switch
        {
            0 => 0.34f,
            1 => 0.62f,
            _ => 1.08f
        };

        var cx = WIDTH / 2f;
        var cy = HEIGHT / 2f + 4;
        var rx = 46f * grow;
        var ry = 58f * grow;

        for (var y = 0; y < HEIGHT; y++)
            for (var x = 0; x < WIDTH; x++)
            {
                var nx = (x + 0.5f - cx) / rx;
                var ny = (y + 0.5f - cy) / ry;
                var face = nx * nx + ny * ny;

                Color color;

                if (face > 1f)
                {
                    var edge = Math.Clamp((Math.Abs(x - cx) / cx + Math.Abs(y - cy) / cy) * 0.5f, 0f, 1f);
                    color = new Color((byte)(12 * (1 - edge)), (byte)(4 * (1 - edge)), (byte)(16 * (1 - edge)));
                }
                else
                {
                    var shade = 0.72f + 0.28f * (1f - ny);
                    color = new Color((byte)(214 * shade), (byte)(196 * shade), (byte)(176 * shade));

                    if (Eye(nx, ny, -0.38f, grow) || Eye(nx, ny, 0.38f, grow))
                        color = new Color(8, 6, 10);
                    else if (Mouth(nx, ny, frame))
                        color = frame == 2 ? new Color(90, 8, 18) : new Color(40, 12, 18);
                }

                pixels[y * WIDTH + x] = color;
            }

        return pixels;
    }

    private static bool Eye(float nx, float ny, float side, float grow)
    {
        var ex = (nx - side) / (0.16f + grow * 0.04f);
        var ey = (ny + 0.18f) / 0.22f;

        return ex * ex + ey * ey <= 1f;
    }

    private static bool Mouth(float nx, float ny, int frame)
    {
        var open = frame switch
        {
            0 => 0.06f,
            1 => 0.14f,
            _ => 0.28f
        };

        var mx = nx / 0.42f;
        var my = (ny - 0.38f) / open;

        return mx * mx + my * my <= 1f;
    }
}
