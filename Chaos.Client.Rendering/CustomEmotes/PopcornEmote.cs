#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     Popcorn: a striped tub heaped with popcorn rises in front of the chin while kernels pop out of the heap on
///     little arcs, then the tub lowers and fades.
/// </summary>
public sealed class PopcornEmote : PixelArtEmote
{
    /// <summary>Byte 50 is the third byte of emote batch three, inside the server's relayed range.</summary>
    public const int BODY_ANIMATION = 50;

    public const int PREVIEW_FRAME = 1014;
    public const float TOTAL_MS = 2500f;

    /// <summary>How long the tub takes to rise into place in front of the chin.</summary>
    public const float RISE_MS = 300f;

    /// <summary>Rows below its resting place the tub starts from.</summary>
    public const int START_ROW_OFFSET = 6;

    /// <summary>The tub lowers and fades from here to the end.</summary>
    public const float FADE_OUT_MS = 2100f;

    /// <summary>Rows the tub sinks while it fades.</summary>
    public const int FADE_SINK_ROWS = 4;

    /// <summary>How long one kernel lives, from the heap to gone.</summary>
    public const float KERNEL_LIFE_MS = 600f;

    /// <summary>Rows a kernel rises over its life.</summary>
    public const int KERNEL_RISE_ROWS = 6;

    /// <summary>Columns a kernel drifts away from the heap over its life.</summary>
    public const int KERNEL_DRIFT_COLUMNS = 4;

    /// <summary>Fraction of its life after which a kernel starts to fade.</summary>
    public const float KERNEL_FADE_FROM = 0.6f;

    /// <summary>The tub is 9 wide, x52-60, centred a little to the aisling's right of the head (x50-60).</summary>
    public const int LEFT_X = 52;

    /// <summary>The tub's rim sits at row 37, over the chin, leaving the eyes (row 32) visible above it.</summary>
    public const int REST_TOP_Y = 34;

    /// <summary>Centre of the heap kernels pop out of.</summary>
    public const int HEAP_CENTER_X = 56;

    public const int HEAP_CENTER_Y = 35;

    /// <summary>One kernel pops out at each of these moments, alternating sides.</summary>
    public static IReadOnlyList<float> KernelStartsMs { get; } = [400f, 650f, 900f, 1150f, 1400f, 1650f];

    /// <summary>
    ///     The tub: a pale-yellow heap with butter bits over red-and-off-white vertical stripes, slightly wider at
    ///     the top.
    /// </summary>
    public static IReadOnlyList<string> TubPixels { get; } =
    [
        "..pp.pp..",
        ".ppppppp.",
        ".ppbpbpp.",
        "kRwRwRwRk",
        "kRwRwRwRk",
        "kRwRwRwRk",
        "kRwRwRwRk",
        ".kRwRwRk.",
        ".kRwRwRk.",
        ".kRwRwRk.",
        "..kRwRk..",
        "..kkkkk.."
    ];

    /// <summary>A popped kernel: a pale-yellow blob with a darker butter pixel.</summary>
    public static IReadOnlyList<string> KernelPixels { get; } =
    [
        ".pp",
        "pBp",
        "pp."
    ];

    private static readonly Dictionary<char, Color> TubPalette = new()
    {
        ['k'] = new Color(90, 40, 44),
        ['R'] = new Color(196, 44, 64),
        ['w'] = new Color(214, 214, 206),
        ['p'] = new Color(240, 216, 148),
        ['b'] = new Color(214, 150, 44)
    };

    private static readonly Dictionary<char, Color> KernelPalette = new()
    {
        ['p'] = new Color(240, 216, 148),
        ['B'] = new Color(214, 150, 44)
    };

    private static readonly PixelArt TubArt = PixelArt.FromPalette("popcorn:tub", TubPixels, TubPalette);
    private static readonly PixelArt KernelArt = PixelArt.FromPalette("popcorn:kernel", KernelPixels, KernelPalette);

    public static PopcornEmote Instance { get; } = new();

    private PopcornEmote() { }

    public override int BodyAnimation => BODY_ANIMATION;
    public override bool RequiresUnlock => true;
    public override string Name => "Popcorn";
    public override int PreviewFrame => PREVIEW_FRAME;
    public override float DurationMs => TOTAL_MS;

    /// <summary>Just risen, before the first kernel pops.</summary>
    public override float IconTimeMs => 350f;

    /// <summary>Rows away from its resting place the tub sits: up from below, then sinking while it fades.</summary>
    public static int TubOffset(float elapsedMs)
    {
        if (elapsedMs < RISE_MS)
            return CustomEmoteGeometry.EaseInRows(elapsedMs, RISE_MS, START_ROW_OFFSET);

        if (elapsedMs < FADE_OUT_MS)
            return 0;

        return (int)MathF.Round(FADE_SINK_ROWS * (elapsedMs - FADE_OUT_MS) / (TOTAL_MS - FADE_OUT_MS));
    }

    /// <summary>1 until <see cref="FADE_OUT_MS" />, then down to 0 at the end.</summary>
    public static float TubOpacity(float elapsedMs)
        => elapsedMs < FADE_OUT_MS ? 1f : Math.Clamp((TOTAL_MS - elapsedMs) / (TOTAL_MS - FADE_OUT_MS), 0f, 1f);

    /// <summary>One kernel mid-pop.</summary>
    /// <param name="Index">Which entry of <see cref="KernelStartsMs" /> this kernel popped from.</param>
    /// <param name="CenterX">Composite X of the kernel's centre, before head bob.</param>
    /// <param name="CenterY">Composite Y of the kernel's centre, before head bob.</param>
    /// <param name="Opacity">1 until the kernel starts to fade, then down to 0.</param>
    public readonly record struct Kernel(int Index, int CenterX, int CenterY, float Opacity);

    /// <summary>Every kernel alive at this moment, oldest first. Even kernels pop left, odd ones pop right.</summary>
    public static IEnumerable<Kernel> Kernels(float elapsedMs)
    {
        for (var i = 0; i < KernelStartsMs.Count; i++)
        {
            var age = elapsedMs - KernelStartsMs[i];

            if ((age < 0f) || (age >= KERNEL_LIFE_MS))
                continue;

            var life = age / KERNEL_LIFE_MS;
            var rise = (int)MathF.Round(KERNEL_RISE_ROWS * life);
            var drift = (int)MathF.Round(KERNEL_DRIFT_COLUMNS * life);
            var side = i % 2 == 0 ? -1 : 1;
            var opacity = life < KERNEL_FADE_FROM
                ? 1f
                : Math.Clamp(1f - (life - KERNEL_FADE_FROM) / (1f - KERNEL_FADE_FROM), 0f, 1f);

            yield return new Kernel(i, HEAP_CENTER_X + side * drift, HEAP_CENTER_Y - rise, opacity);
        }
    }

    public override IReadOnlyList<PixelLayer> Compose(float elapsedMs, int headBobRows)
    {
        if ((elapsedMs < 0f) || (elapsedMs >= TOTAL_MS))
            return [];

        var offset = TubOffset(elapsedMs);
        var layers = new List<PixelLayer>(3) { new(TubArt, LEFT_X, REST_TOP_Y + offset + headBobRows, TubOpacity(elapsedMs)) };

        foreach (var kernel in Kernels(elapsedMs))
            layers.Add(
                new PixelLayer(
                    KernelArt,
                    kernel.CenterX - KernelArt.Width / 2,
                    kernel.CenterY - KernelArt.Height / 2 + headBobRows,
                    kernel.Opacity));

        return layers;
    }
}
