#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     Salty: a salt shaker appears above the head, tips over to pour, shakes three times scattering salt grains
///     onto the head top, then tips back upright and fades.
/// </summary>
public sealed class SaltyEmote : PixelArtEmote
{
    /// <summary>Byte 54 is the seventh byte of emote batch three, inside the server's relayed range.</summary>
    public const int BODY_ANIMATION = 54;

    public const int PREVIEW_FRAME = 1018;
    public const float TOTAL_MS = 2400f;

    /// <summary>The shaker fades in over this long.</summary>
    public const float APPEAR_MS = 150f;

    /// <summary>The shaker tips over to pour here, swapping to its tilted piece.</summary>
    public const float TILT_MS = 500f;

    /// <summary>The first shake jolt starts here; two more follow every <see cref="JOLT_MS" />.</summary>
    public const float SHAKE_START_MS = 700f;

    /// <summary>How long one down-up jolt lasts: dipped for the first half, back for the second.</summary>
    public const float JOLT_MS = 180f;

    /// <summary>How many shake jolts scatter salt.</summary>
    public const int JOLT_COUNT = 3;

    /// <summary>Rows the shaker dips during the first half of each jolt.</summary>
    public const int JOLT_ROWS = 2;

    /// <summary>The shaker tips back upright here.</summary>
    public const float UNTILT_MS = 1400f;

    /// <summary>Everything fades out from here to the end.</summary>
    public const float FADE_OUT_MS = 2000f;

    /// <summary>How long one jolt's salt grains fall before they are gone: a visible stream from cap to head top.</summary>
    public const float GRAIN_LIFE_MS = 250f;

    /// <summary>How many grains each jolt scatters onto the head top.</summary>
    public const int GRAINS_PER_JOLT = 7;

    /// <summary>The upright shaker stands here: 7 wide, x58-64, just right of the head's centre.</summary>
    public const int UPRIGHT_LEFT_X = 58;

    /// <summary>The upright shaker is 10 tall, rows 12-21, well above the head.</summary>
    public const int UPRIGHT_TOP_Y = 12;

    /// <summary>The tilted shaker sits here: 7 wide, x54-60, its cap down-left toward the head.</summary>
    public const int TILTED_LEFT_X = 54;

    public const int TILTED_TOP_Y = 12;

    /// <summary>Where the salt pours from: just under the tilted cap's tip.</summary>
    public const int POUR_X = 55;

    public const int POUR_Y = 21;

    /// <summary>Columns each grain lands away from the pour point, so the salt scatters either side of the stream.</summary>
    public static IReadOnlyList<int> GrainDx { get; } = [-3, -2, -1, 0, 1, 2, 3];

    /// <summary>
    ///     The upright shaker: a distinctly darker metal cap with two visible hole pixels over a clearly rectangular
    ///     glass body in light blue-grey, its lower half filled with off-white salt, all in a strong dark outline.
    /// </summary>
    public static IReadOnlyList<string> UprightPixels { get; } =
    [
        "..kkk..",
        ".kccck.",
        ".kHcHk.",
        ".kkkkk.",
        "kgggggk",
        "kgggggk",
        "kgwwwgk",
        "kgwwwgk",
        "kgwwwgk",
        "kkkkkkk"
    ];

    /// <summary>
    ///     The same blocky shaker tipped to pour, rotated roughly 135 degrees: a stair-stepped diagonal rectangle
    ///     running from the top-right down to the metal cap at the bottom-left, pointing at the head top.
    /// </summary>
    public static IReadOnlyList<string> TiltedPixels { get; } =
    [
        "....kkk",
        "...kggk",
        "...kggk",
        "..kggk.",
        "..kggk.",
        "..kwwk.",
        ".kkkk..",
        ".kHHk..",
        ".kcck..",
        ".kk...."
    ];

    private static readonly Dictionary<char, Color> ShakerPalette = new()
    {
        ['k'] = new Color(40, 40, 48),
        ['g'] = new Color(170, 180, 190),
        ['w'] = new Color(214, 214, 206),
        ['c'] = new Color(110, 116, 128),
        ['H'] = new Color(30, 32, 40)
    };

    private static readonly PixelArt UprightArt = PixelArt.FromPalette("salty:shaker", UprightPixels, ShakerPalette);
    private static readonly PixelArt TiltedArt = PixelArt.FromPalette("salty:shaker-tilted", TiltedPixels, ShakerPalette);
    private static readonly PixelArt GrainArt = PixelArt.Dot("salty:grain", new Color(214, 214, 206));
    private static readonly PixelArt WideGrainArt =
        PixelArt.FromPalette("salty:grain-wide", ["xx"], new Dictionary<char, Color> { ['x'] = new Color(214, 214, 206) });
    private static readonly PixelArt ShadowArt = PixelArt.Dot("salty:grain-shadow", new Color(150, 150, 142));

    public static SaltyEmote Instance { get; } = new();

    private SaltyEmote() { }

    public override int BodyAnimation => BODY_ANIMATION;
    public override string Name => "Salty";
    public override int PreviewFrame => PREVIEW_FRAME;
    public override float DurationMs => TOTAL_MS;

    /// <summary>Upright and fully opaque, before the tip-over.</summary>
    public override float IconTimeMs => 300f;

    /// <summary>True while the shaker is tipped over pouring.</summary>
    public static bool IsTilted(float elapsedMs) => (elapsedMs >= TILT_MS) && (elapsedMs < UNTILT_MS);

    /// <summary>Rows the pouring shaker has dipped: <see cref="JOLT_ROWS" /> for the first half of each jolt, 0 otherwise.</summary>
    public static int JoltOffset(float elapsedMs)
    {
        var sinceShake = elapsedMs - SHAKE_START_MS;

        if (sinceShake < 0f)
            return 0;

        if (sinceShake >= JOLT_COUNT * JOLT_MS)
            return 0;

        return (sinceShake % JOLT_MS) < (JOLT_MS / 2f) ? JOLT_ROWS : 0;
    }

    /// <summary>0 as the shaker fades in, 1 while it pours, back to 0 as it fades out.</summary>
    public static float ShakerOpacity(float elapsedMs)
    {
        if (elapsedMs < APPEAR_MS)
            return Math.Clamp(elapsedMs / APPEAR_MS, 0f, 1f);

        if (elapsedMs < FADE_OUT_MS)
            return 1f;

        return Math.Clamp((TOTAL_MS - elapsedMs) / (TOTAL_MS - FADE_OUT_MS), 0f, 1f);
    }

    /// <summary>One salt grain falling from the cap onto the head top.</summary>
    /// <param name="Index">Which jolt this grain fell from.</param>
    /// <param name="X">Composite X of the grain, before head bob.</param>
    /// <param name="Y">Composite Y of the grain, before head bob.</param>
    /// <param name="Opacity">1 as it falls, then down to 0 as it lands.</param>
    /// <param name="IsWide">True for the grains drawn 2 pixels wide.</param>
    /// <param name="HasShadow">True for the grains with a mid-grey shadow dot beside them.</param>
    public readonly record struct Grain(int Index, int X, int Y, float Opacity, bool IsWide, bool HasShadow);

    /// <summary>Every salt grain alive at this moment: one cluster of <see cref="GRAINS_PER_JOLT" /> per jolt.</summary>
    public static IEnumerable<Grain> Grains(float elapsedMs)
    {
        for (var jolt = 0; jolt < JOLT_COUNT; jolt++)
        {
            var age = elapsedMs - (SHAKE_START_MS + jolt * JOLT_MS);

            if ((age < 0f) || (age >= GRAIN_LIFE_MS))
                continue;

            for (var grain = 0; grain < GRAINS_PER_JOLT; grain++)
            {
                var landRow = CustomEmoteGeometry.HEAD_TOP_Y + (grain % 3);
                var progress = age / GRAIN_LIFE_MS;
                var opacity = progress < 0.6f ? 1f : Math.Clamp(1f - ((progress - 0.6f) / 0.4f), 0f, 1f);

                yield return new Grain(
                    jolt,
                    POUR_X + GrainDx[grain],
                    POUR_Y + (int)MathF.Round((landRow - POUR_Y) * progress),
                    opacity,
                    grain % 3 == 0,
                    grain % 3 == 1);
            }
        }
    }

    public override IReadOnlyList<PixelLayer> Compose(float elapsedMs, int headBobRows)
    {
        if ((elapsedMs < 0f) || (elapsedMs >= TOTAL_MS))
            return [];

        var opacity = ShakerOpacity(elapsedMs);
        var tilted = IsTilted(elapsedMs);
        var layers = new List<PixelLayer>(1 + GRAINS_PER_JOLT)
        {
            new(
                tilted ? TiltedArt : UprightArt,
                tilted ? TILTED_LEFT_X : UPRIGHT_LEFT_X,
                (tilted ? TILTED_TOP_Y : UPRIGHT_TOP_Y) + (tilted ? JoltOffset(elapsedMs) : 0) + headBobRows,
                opacity)
        };

        foreach (var grain in Grains(elapsedMs))
        {
            layers.Add(
                new PixelLayer(
                    grain.IsWide ? WideGrainArt : GrainArt,
                    grain.X,
                    grain.Y + headBobRows,
                    grain.Opacity * opacity));

            if (grain.HasShadow)
                layers.Add(
                    new PixelLayer(
                        ShadowArt,
                        grain.X + (grain.IsWide ? 2 : 1),
                        grain.Y + headBobRows,
                        grain.Opacity * opacity));
        }

        return layers;
    }
}
