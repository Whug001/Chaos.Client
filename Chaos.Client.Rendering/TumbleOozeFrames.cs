#region
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Tumble Tower tile art: 12 melt frames, 6 funnel frames, the shaft edge drawn over holes, and a 1x1 pixel.
///     Loads embedded <c>tumbleooze.melt_NN.png</c> / <c>tumbleooze.funnel_NN.png</c> when the art is packed in,
///     otherwise draws simple placeholder frames so the feature works before the art lands.
/// </summary>
public sealed class TumbleOozeFrames : IDisposable
{
    public const int MELT_FRAMES = 12;
    public const int FUNNEL_FRAMES = 6;
    private const int W = 56;
    private const int H = 27;

    private readonly List<Texture2D> Funnel = [];
    private readonly List<Texture2D> Melt = [];

    public TumbleOozeFrames(GraphicsDevice device)
    {
        for (var i = 0; i < MELT_FRAMES; i++)
            Melt.Add(LoadEmbedded(device, $"tumbleooze.melt_{i:D2}.png") ?? BuildMelt(device, i / (double)(MELT_FRAMES - 1)));

        for (var i = 0; i < FUNNEL_FRAMES; i++)
            Funnel.Add(LoadEmbedded(device, $"tumbleooze.funnel_{i:D2}.png") ?? BuildFunnel(device, i / (double)(FUNNEL_FRAMES - 1)));

        Shaft = BuildShaft(device);
        Pixel = new Texture2D(device, 1, 1);
        Pixel.SetData([Color.White]);
    }

    public Texture2D Pixel { get; }
    public Texture2D Shaft { get; }

    public void Dispose()
    {
        foreach (var t in Melt.Concat(Funnel))
            t.Dispose();

        Shaft.Dispose();
        Pixel.Dispose();
    }

    public Texture2D MeltFrame(double progress) => Melt[(int)Math.Round(Math.Clamp(progress, 0, 1) * (MELT_FRAMES - 1))];

    public Texture2D FunnelFrame(double progress) => Funnel[(int)Math.Round(Math.Clamp(progress, 0, 1) * (FUNNEL_FRAMES - 1))];

    private static Texture2D? LoadEmbedded(GraphicsDevice device, string name)
    {
        using var stream = typeof(TumbleOozeFrames).Assembly.GetManifestResourceStream(name);

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

    private static Texture2D BuildMelt(GraphicsDevice device, double p)
    {
        var rng = new Random(1234);
        var bubbles = Enumerable.Range(0, 14).Select(_ => (X: rng.Next(10, 46), Y: rng.Next(5, 22), At: rng.NextDouble())).ToList();

        return Make(device, (x, y) =>
        {
            var d = DiamondDistance(x, y);
            //darken toward a murky green, and deepen the middle as the tile sinks
            var darkness = 0.25 + 0.55 * p + (p > 0.5 ? (1 - d) * (p - 0.5) * 0.6 : 0);

            foreach (var b in bubbles)
                if ((b.At < p) && (Math.Abs(x - b.X) <= 1) && (Math.Abs(y - b.Y) <= 1))
                    return Shade(110, 160, 110, 0.55);

            return Shade(12, 26, 18, Math.Min(0.95, darkness));
        });
    }

    private static Texture2D BuildFunnel(GraphicsDevice device, double p)
        => Make(device, (x, y) =>
        {
            var d = DiamondDistance(x, y);
            var ring = Math.Abs(d - (1 - p)) < 0.12;

            return ring ? Shade(60, 90, 70, 0.8 * (1 - p)) : Color.Transparent;
        });

    private static Texture2D BuildShaft(GraphicsDevice device)
        => Make(device, (_, y) =>
        {
            //dark at the two back (upper) edges, fading toward the front, so the hole reads as a shaft
            var back = y < 13 ? 1 - y / 13.0 : 0;

            return Shade(0, 0, 0, Math.Clamp(0.25 + back * 0.6, 0, 0.85));
        });
}
