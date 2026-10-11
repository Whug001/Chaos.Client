#region
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Drowned Causeway river art, one 56x27 ground diamond per tile: raft ends and middle, a risen shell, four bubbling
///     frames and the ripple a sunk shell leaves. Loads embedded <c>causeway.&lt;name&gt;.png</c> files when the art is
///     packed in, otherwise draws simple placeholders so the mode works before the art lands.
/// </summary>
public sealed class CausewayFrames : IDisposable
{
    public const int BUBBLE_FRAMES = 4;
    private const int H = 27;
    private const int W = 56;

    private readonly List<Texture2D> Bubbling = [];

    public CausewayFrames(GraphicsDevice device)
    {
        RaftEndA = LoadEmbedded(device, "causeway.raft_end_a.png") ?? BuildRaft(device, 1);
        RaftMiddle = LoadEmbedded(device, "causeway.raft_middle.png") ?? BuildRaft(device, 0);
        RaftEndB = LoadEmbedded(device, "causeway.raft_end_b.png") ?? BuildRaft(device, -1);
        ShellRisen = LoadEmbedded(device, "causeway.shell_risen.png") ?? BuildShell(device, 0);

        for (var i = 0; i < BUBBLE_FRAMES; i++)
            Bubbling.Add(LoadEmbedded(device, $"causeway.shell_bubbling_{i:D2}.png") ?? BuildShell(device, i + 1));

        ShellSunk = LoadEmbedded(device, "causeway.shell_sunk.png") ?? BuildRipple(device);
    }

    public Texture2D RaftEndA { get; }
    public Texture2D RaftEndB { get; }
    public Texture2D RaftMiddle { get; }
    public Texture2D ShellRisen { get; }
    public Texture2D ShellSunk { get; }

    public void Dispose()
    {
        foreach (var t in Bubbling.Concat([RaftEndA, RaftEndB, RaftMiddle, ShellRisen, ShellSunk]))
            t.Dispose();
    }

    /// <param name="tile">The tile's index within its piece, 0 at the low-x end.</param>
    public Texture2D For(CausewayLaneKind kind, CausewayShellState state, int tile, int length, long playMs)
    {
        if (kind == CausewayLaneKind.Shell)
            return state switch
            {
                CausewayShellState.Risen    => ShellRisen,
                CausewayShellState.Bubbling => Bubbling[(int)(Math.Max(0, playMs) / 150 % BUBBLE_FRAMES)],
                _                           => ShellSunk
            };

        if ((length == 1) || ((tile > 0) && (tile < length - 1)))
            return RaftMiddle;

        return tile == 0 ? RaftEndA : RaftEndB;
    }

    private static Texture2D Build(GraphicsDevice device, Func<int, int, Color> paint)
    {
        var pixels = new Color[W * H];

        for (var y = 0; y < H; y++)
            for (var x = 0; x < W; x++)
                pixels[y * W + x] = paint(x, y);

        var texture = new Texture2D(device, W, H);
        texture.SetData(pixels);

        return texture;
    }

    private static Texture2D BuildRaft(GraphicsDevice device, int end)
        => Build(
            device,
            (x, y) =>
            {
                var d = DiamondDistance(x, y);

                if (d > 0.9)
                    return Color.Transparent;

                var plank = ((x + 2 * y) / 7 % 2) == 0;
                var color = plank ? new Color(122, 86, 50) : new Color(98, 68, 40);
                var rim = (d > 0.78) || ((end == 1) && (x < 16)) || ((end == -1) && (x > 40));

                return rim ? new Color(color.R * 3 / 5, color.G * 3 / 5, color.B * 3 / 5) : color;
            });

    private static Texture2D BuildRipple(GraphicsDevice device)
        => Build(
            device,
            (x, y) =>
            {
                var d = DiamondDistance(x, y);

                return d is > 0.62 and < 0.72 ? new Color(200, 230, 255) * 0.35f : Color.Transparent;
            });

    private static Texture2D BuildShell(GraphicsDevice device, int bubbles)
    {
        var dots = new HashSet<(int, int)>();

        for (var k = 0; k < bubbles * 3; k++)
            dots.Add(((k * 37 + bubbles * 11) % 30 + 13, (k * 17 + bubbles * 5) % 12 + 7));

        return Build(
            device,
            (x, y) =>
            {
                var d = DiamondDistance(x, y);

                if (d > 0.72)
                    return Color.Transparent;

                if (dots.Contains((x, y)))
                    return Color.White;

                var shade = (float)(1 - d * 0.6);

                return new Color((int)(70 * shade), (int)(118 * shade), (int)(100 * shade));
            });
    }

    private static double DiamondDistance(int x, int y) => Math.Abs(x - 27.5) / 28.0 + Math.Abs(y - 13.0) / 13.5;

    private static Texture2D? LoadEmbedded(GraphicsDevice device, string name)
    {
        using var stream = typeof(CausewayFrames).Assembly.GetManifestResourceStream(name);

        //the world batches draw with premultiplied AlphaBlend, and PNGs are straight alpha
        return stream is null ? null : Texture2D.FromStream(device, stream, DefaultColorProcessors.PremultiplyAlpha);
    }
}
