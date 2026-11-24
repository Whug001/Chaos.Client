#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     A tumbleweed: a round tangle of twigs rolls along the ground in front of the aisling's feet, hopping as it
///     goes and kicking up dust where it lands, then fades.
/// </summary>
/// <remarks>
///     The scene stands on the ground beside the aisling rather than on the head, so it ignores the head bob and ends as
///     soon as the aisling walks (<see cref="EndsOnWalk" />).
/// </remarks>
public sealed class TumbleweedEmote : PixelArtEmote
{
    /// <summary>Byte 55 is the eighth byte of emote batch three, inside the server's relayed range.</summary>
    public const int BODY_ANIMATION = 55;

    public const int PREVIEW_FRAME = 1019;
    public const float TOTAL_MS = 3000f;

    /// <summary>The weed fades in over this long.</summary>
    public const float FADE_IN_MS = 200f;

    /// <summary>The weed fades out from here to the end.</summary>
    public const float FADE_OUT_MS = 2700f;

    /// <summary>How many hops carry the weed across the ground.</summary>
    public const int HOP_COUNT = 4;

    /// <summary>How long one hop lasts.</summary>
    public const float HOP_MS = 750f;

    /// <summary>Rows the weed rises at the top of each hop.</summary>
    public const int HOP_APEX_ROWS = 3;

    /// <summary>How long one rotation frame shows while the weed rolls.</summary>
    public const float FRAME_MS = 120f;

    /// <summary>How long each landing's dust hangs around while fading.</summary>
    public const float PUFF_MS = 300f;

    /// <summary>The weed rolls from here, far left of the aisling...</summary>
    public const int START_X = 8;

    /// <summary>...to here, far right of the aisling.</summary>
    public const int END_X = 92;

    /// <summary>The weed is 13 wide and 13 tall, an irregular tangle with holes showing through.</summary>
    public const int WEED_WIDTH = 13;

    public const int WEED_HEIGHT = 13;

    /// <summary>Where the icon puts one weed frame: overlapping the cheek (x50-60), level with it.</summary>
    public const int ICON_LEFT_X = 59;

    public const int ICON_TOP_Y = 24;

    /// <summary>The weed at rest stands here, its bottom on the ground.</summary>
    public static int RestTopY => PotionFlaskEmote.GROUND_Y - WEED_HEIGHT + 1;

    /// <summary>Each hop ends here: the moments dust kicks up.</summary>
    public static IReadOnlyList<float> LandingTimesMs { get; } = [HOP_MS, 2f * HOP_MS, 3f * HOP_MS];

    /// <summary>
    ///     The first rotation frame: an irregular ball of long criss-crossing twig strokes with a broken silhouette
    ///     and holes through the middle. 'd' is dark back twigs, 'm' mid twigs, 'p' pale front highlights.
    /// </summary>
    public static IReadOnlyList<string> Frame0Pixels { get; } =
    [
        "......m......",
        "...mm....d...",
        "..m...p...d.p",
        "...m.ppp.d...",
        ".dm.m...d..m.",
        "p.mmmm.d..mm.",
        "...d.....p...",
        "..mm.d.m.pp..",
        ".d..d...mmm..",
        "m..d.ppp.m...",
        "..d...m...p..",
        "....m...d....",
        "......p......"
    ];

    /// <summary>The second rotation frame: the same tangle turned a quarter turn, mid-roll.</summary>
    public static IReadOnlyList<string> Frame1Pixels { get; } =
    [
        "...m...p.....",
        "....d...d....",
        "..d..m.mm.m..",
        "...d.mdm.m.m.",
        ".m..d..mm..m.",
        "...p.d.m.p...",
        "p.mp.....pp.m",
        "...p.m.d.p...",
        ".d..m...d....",
        "...mmpp..d.d.",
        "..p.mp.m..d..",
        ".......mm....",
        "..........p.."
    ];

    /// <summary>The third rotation frame: the same tangle turned half a turn, mid-roll.</summary>
    public static IReadOnlyList<string> Frame2Pixels { get; } =
    [
        "......p......",
        "....d...m....",
        "..p...m...d..",
        "...m.ppp.d..m",
        "..mmm...d..d.",
        "..pp.m.d.mm..",
        "...p.....d...",
        ".mm..d.mmmm.p",
        ".m..d...m.md.",
        "...d.ppp.m...",
        "p.d...p...m..",
        "...d....mm...",
        "......m......"
    ];

    private static readonly Dictionary<char, Color> WeedPalette = new()
    {
        ['d'] = new Color(90, 62, 34),
        ['m'] = new Color(176, 136, 80),
        ['p'] = new Color(214, 186, 120)
    };

    private static readonly PixelArt[] FrameArts =
    [
        PixelArt.FromPalette("tumbleweed:frame-0", Frame0Pixels, WeedPalette),
        PixelArt.FromPalette("tumbleweed:frame-1", Frame1Pixels, WeedPalette),
        PixelArt.FromPalette("tumbleweed:frame-2", Frame2Pixels, WeedPalette)
    ];

    private static readonly PixelArt DustArt = PixelArt.Dot("tumbleweed:dust", new Color(190, 180, 160));

    public static TumbleweedEmote Instance { get; } = new();

    private TumbleweedEmote() { }

    public override int BodyAnimation => BODY_ANIMATION;
    public override bool RequiresUnlock => true;
    public override string Name => "Tumbleweed";
    public override int PreviewFrame => PREVIEW_FRAME;
    public override float DurationMs => TOTAL_MS;
    public override bool EndsOnWalk => true;

    /// <summary>Mid-roll: fully opaque, mid-hop.</summary>
    public override float IconTimeMs => 1350f;

    /// <summary>The whole scene is far too wide for a wheel slot, so the icon is one weed frame alone beside the head.</summary>
    public override IReadOnlyList<PixelLayer> IconLayers() => [new PixelLayer(FrameArts[0], ICON_LEFT_X, ICON_TOP_Y)];

    /// <summary>Composite X of the weed's left edge: far left at the start, far right at the end.</summary>
    public static int WeedX(float elapsedMs)
    {
        var progress = Math.Clamp(elapsedMs / TOTAL_MS, 0f, 1f);

        return START_X + (int)MathF.Round((END_X - START_X) * progress);
    }

    /// <summary>Rows the weed has risen off the ground: 0 at each landing, <see cref="HOP_APEX_ROWS" /> mid-hop.</summary>
    public static int HopLift(float elapsedMs)
    {
        var hop = Math.Clamp(elapsedMs, 0f, TOTAL_MS - 1f) % HOP_MS / HOP_MS;

        return (int)MathF.Round(HOP_APEX_ROWS * 4f * hop * (1f - hop));
    }

    /// <summary>Which rotation frame shows: 0, 1 and 2 cycling every <see cref="FRAME_MS" />.</summary>
    public static int FrameIndex(float elapsedMs)
        => (int)(Math.Clamp(elapsedMs, 0f, TOTAL_MS - 1f) / FRAME_MS) % FrameArts.Length;

    /// <summary>0 as the weed fades in, 1 while it rolls, back to 0 as it fades out.</summary>
    public static float Opacity(float elapsedMs)
    {
        if (elapsedMs < FADE_IN_MS)
            return Math.Clamp(elapsedMs / FADE_IN_MS, 0f, 1f);

        if (elapsedMs < FADE_OUT_MS)
            return 1f;

        return Math.Clamp((TOTAL_MS - elapsedMs) / (TOTAL_MS - FADE_OUT_MS), 0f, 1f);
    }

    /// <summary>One dust puff kicked up where the weed lands from a hop.</summary>
    /// <param name="X">Composite X of the puff.</param>
    /// <param name="Y">Composite Y of the puff.</param>
    /// <param name="Opacity">1 at the landing, then down to 0 over <see cref="PUFF_MS" />.</param>
    public readonly record struct Puff(int X, int Y, float Opacity);

    /// <summary>Every dust puff alive at this moment: two at each hop's landing, at the weed's edges.</summary>
    public static IEnumerable<Puff> Puffs(float elapsedMs)
    {
        foreach (var landing in LandingTimesMs)
        {
            var age = elapsedMs - landing;

            if ((age < 0f) || (age >= PUFF_MS))
                continue;

            var landX = WeedX(landing);
            var rise = (int)MathF.Round(2f * age / PUFF_MS);
            var opacity = Math.Clamp(1f - age / PUFF_MS, 0f, 1f);

            yield return new Puff(landX, PotionFlaskEmote.GROUND_Y - 1 - rise, opacity);
            yield return new Puff(landX + WEED_WIDTH - 1, PotionFlaskEmote.GROUND_Y - 1 - rise, opacity);
        }
    }

    public override IReadOnlyList<PixelLayer> Compose(float elapsedMs, int headBobRows)
    {
        if ((elapsedMs < 0f) || (elapsedMs >= TOTAL_MS))
            return [];

        var opacity = Opacity(elapsedMs);
        var layers = new List<PixelLayer>(3)
        {
            new(FrameArts[FrameIndex(elapsedMs)], WeedX(elapsedMs), RestTopY - HopLift(elapsedMs), opacity)
        };

        foreach (var puff in Puffs(elapsedMs))
            layers.Add(new PixelLayer(DustArt, puff.X, puff.Y, puff.Opacity * opacity));

        return layers;
    }
}
