#region
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     The Glass Way overlay art: the gold rune, the faint trail mark, the gold tint on safe rows, the dark hole of a
///     broken pane, 6 shard frames and a 1x1 pixel. Loads embedded <c>glassway.*.png</c> when the art is packed in,
///     otherwise draws simple placeholders so the feature works before the art lands.
/// </summary>
public sealed class GlassWayFrames : IDisposable
{
    public const int SHARD_FRAMES = 6;
    private const int W = 56;
    private const int H = 27;

    private readonly List<Texture2D> Shards = [];

    public GlassWayFrames(GraphicsDevice device)
    {
        Rune = LoadEmbedded(device, "glassway.rune.png") ?? BuildRune(device);
        Mark = LoadEmbedded(device, "glassway.mark.png") ?? Make(device, (x, y) => DiamondDistance(x, y) < 0.25 ? Shade(255, 205, 90, 0.35) : Color.Transparent);
        Safe = LoadEmbedded(device, "glassway.safe.png") ?? Make(device, (_, _) => Shade(255, 205, 90, 0.18));
        Hole = LoadEmbedded(device, "glassway.hole.png") ?? Make(device, (x, y) => Shade(10, 5, 20, DiamondDistance(x, y) < 0.85 ? 0.9 : 0.5));

        for (var i = 0; i < SHARD_FRAMES; i++)
            Shards.Add(LoadEmbedded(device, $"glassway.shard_{i:D2}.png") ?? BuildShards(device, i / (double)(SHARD_FRAMES - 1)));

        Pixel = new Texture2D(device, 1, 1);
        Pixel.SetData([Color.White]);
    }

    public Texture2D Hole { get; }
    public Texture2D Mark { get; }
    public Texture2D Pixel { get; }
    public Texture2D Rune { get; }
    public Texture2D Safe { get; }

    public void Dispose()
    {
        foreach (var t in Shards)
            t.Dispose();

        Rune.Dispose();
        Mark.Dispose();
        Safe.Dispose();
        Hole.Dispose();
        Pixel.Dispose();
    }

    public Texture2D ShardFrame(double progress) => Shards[(int)Math.Round(Math.Clamp(progress, 0, 1) * (SHARD_FRAMES - 1))];

    private static Texture2D? LoadEmbedded(GraphicsDevice device, string name)
    {
        using var stream = typeof(GlassWayFrames).Assembly.GetManifestResourceStream(name);

        //the world batches draw with premultiplied AlphaBlend, and PNGs are straight alpha
        return stream is null ? null : Texture2D.FromStream(device, stream, DefaultColorProcessors.PremultiplyAlpha);
    }

    private static double DiamondDistance(int x, int y) => Math.Abs(x - 27.5) / 28.0 + Math.Abs(y - 13.0) / 13.5;

    private static Texture2D Make(GraphicsDevice device, Func<int, int, Color> pixel)
    {
        var data = new Color[W * H];

        for (var y = 0; y < H; y++)
            for (var x = 0; x < W; x++)
                data[y * W + x] = DiamondDistance(x, y) <= 1 ? pixel(x, y) : Color.Transparent;

        var texture = new Texture2D(device, W, H);
        texture.SetData(data);

        return texture;
    }

    //premultiplied colours: rgb already scaled by alpha
    private static Color Shade(int r, int g, int b, double alpha)
        => new((int)(r * alpha), (int)(g * alpha), (int)(b * alpha), (int)(255 * alpha));

    private static Texture2D BuildRune(GraphicsDevice device)
        => Make(device, (x, y) =>
        {
            var d = DiamondDistance(x, y);

            return d is > 0.45 and < 0.6 || (d < 0.12) ? Shade(255, 205, 90, 0.9) : d < 0.7 ? Shade(255, 205, 90, 0.15) : Color.Transparent;
        });

    private static Texture2D BuildShards(GraphicsDevice device, double p)
    {
        var fragments = Enumerable.Range(0, 7)
                                  .Select(i =>
                                  {
                                      var a = i * Math.Tau / 7 + 0.3;
                                      var r = 4 + 14 * p;

                                      return (X: 27.5 + Math.Cos(a) * r * 1.6, Y: 13 + Math.Sin(a) * r * 0.6 + 10 * p * p);
                                  })
                                  .ToList();

        var alpha = 1 - 0.8 * p;

        return Make(device, (x, y) =>
        {
            foreach (var f in fragments)
                if ((Math.Abs(x - f.X) <= 1.5) && (Math.Abs(y - f.Y) <= 1))
                    return Shade(170, 120, 255, alpha);

            return Color.Transparent;
        });
    }
}
