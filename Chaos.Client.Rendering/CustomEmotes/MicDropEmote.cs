#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     A mic drop: a handheld microphone hangs at the aisling's side, falls to the ground with a bounce, lies there
///     while dust settles, and fades.
/// </summary>
/// <remarks>
///     The scene stands on the ground beside the aisling rather than on the head, so it ignores the head bob and ends as
///     soon as the aisling walks (<see cref="EndsOnWalk" />).
/// </remarks>
public sealed class MicDropEmote : PixelArtEmote
{
    /// <summary>Byte 51 is the fourth byte of emote batch three, inside the server's relayed range.</summary>
    public const int BODY_ANIMATION = 51;

    public const int PREVIEW_FRAME = 1015;
    public const float TOTAL_MS = 2400f;

    /// <summary>The mic fades in over this long.</summary>
    public const float APPEAR_MS = 150f;

    /// <summary>The mic hangs at hand height until here, then starts to fall.</summary>
    public const float DROP_START_MS = 450f;

    /// <summary>The mic hits the ground here, switching to its lying piece, and bounces once...</summary>
    public const float LAND_MS = 900f;

    /// <summary>...landing for good here, after a smaller arc.</summary>
    public const float BOUNCE_MS = 250f;

    /// <summary>The mic is back on the ground here and rests until the fade.</summary>
    public const float REST_MS = LAND_MS + BOUNCE_MS;

    /// <summary>The resting mic fades out from here to the end.</summary>
    public const float FADE_OUT_MS = 1800f;

    /// <summary>Rows the mic rises at the top of its bounce.</summary>
    public const int BOUNCE_APEX_ROWS = 4;

    /// <summary>How long each ground hit's impact marks hang around while flicking outward and fading.</summary>
    public const float IMPACT_MS = 250f;

    /// <summary>The upright mic is 4 wide and 9 tall: mesh head over a dark handle.</summary>
    public const int UPRIGHT_WIDTH = 4;

    public const int UPRIGHT_HEIGHT = 9;

    /// <summary>The lying mic is 9 wide and 4 tall: handle flat with the head at its right end.</summary>
    public const int LYING_WIDTH = 9;

    public const int LYING_HEIGHT = 4;

    /// <summary>The mic hangs at hand height, about row 50, a few columns right of the body.</summary>
    public const int HAND_TOP_Y = 50;

    /// <summary>The hanging and falling mic stands here: x68-71.</summary>
    public const int MIC_X = 68;

    /// <summary>The mic's top once it has fallen straight down: standing on the ground.</summary>
    public const int UPRIGHT_REST_TOP_Y = PotionFlaskEmote.GROUND_Y - UPRIGHT_HEIGHT + 1;

    /// <summary>The lying mic rests here: x66-74, on the same centre as the fall.</summary>
    public const int LYING_LEFT_X = 66;

    /// <summary>The lying mic's top at rest: standing on the ground.</summary>
    public const int LYING_REST_TOP_Y = PotionFlaskEmote.GROUND_Y - LYING_HEIGHT + 1;

    /// <summary>Composite X the impact marks spread from: the mic's centre.</summary>
    public const int IMPACT_CENTER_X = 70;

    /// <summary>Where the icon puts the upright mic: right of the head (x50-60), level with it.</summary>
    public const int ICON_LEFT_X = 62;

    public const int ICON_TOP_Y = 22;

    /// <summary>The upright mic: a round silver mesh head over a dark grey handle with a shaded base.</summary>
    public static IReadOnlyList<string> UprightPixels { get; } =
    [
        ".MM.",
        "MmMm",
        "mMMm",
        "MmMm",
        ".hh.",
        ".hh.",
        ".hh.",
        ".hh.",
        ".hH."
    ];

    /// <summary>The same mic lying on its side: handle flat, head a ring at its right end.</summary>
    public static IReadOnlyList<string> LyingPixels { get; } =
    [
        "......MM.",
        "hhhhhMmmM",
        "hhhhhMmmM",
        "......MM."
    ];

    /// <summary>A short horizontal impact line that flicks out sideways from each ground hit.</summary>
    public static IReadOnlyList<string> ImpactLinePixels { get; } = ["iii"];

    private static readonly Dictionary<char, Color> MicPalette = new()
    {
        ['M'] = new Color(188, 188, 198),
        ['m'] = new Color(130, 130, 144),
        ['h'] = new Color(86, 84, 94),
        ['H'] = new Color(62, 60, 70)
    };

    private static readonly Dictionary<char, Color> ImpactPalette = new() { ['i'] = new Color(200, 200, 190) };

    private static readonly PixelArt UprightArt = PixelArt.FromPalette("mic-drop:mic-upright", UprightPixels, MicPalette);
    private static readonly PixelArt LyingArt = PixelArt.FromPalette("mic-drop:mic-lying", LyingPixels, MicPalette);
    private static readonly PixelArt ImpactLineArt = PixelArt.FromPalette("mic-drop:impact-line", ImpactLinePixels, ImpactPalette);
    private static readonly PixelArt DustArt = PixelArt.Dot("mic-drop:dust", new Color(190, 180, 160));

    public static MicDropEmote Instance { get; } = new();

    private MicDropEmote() { }

    public override int BodyAnimation => BODY_ANIMATION;
    public override string Name => "Mic Drop";
    public override int PreviewFrame => PREVIEW_FRAME;
    public override float DurationMs => TOTAL_MS;
    public override bool EndsOnWalk => true;

    /// <summary>Mid-rest: lying on the ground, everything opaque, no impact marks left.</summary>
    public override float IconTimeMs => 1600f;

    /// <summary>The whole scene is far too wide for a wheel slot, so the icon is the upright mic alone beside the head.</summary>
    public override IReadOnlyList<PixelLayer> IconLayers() => [new PixelLayer(UprightArt, ICON_LEFT_X, ICON_TOP_Y)];

    /// <summary>0 as the mic fades in, 1 while it hangs, falls and rests, back to 0 as it fades out.</summary>
    public static float Opacity(float elapsedMs)
    {
        if (elapsedMs < APPEAR_MS)
            return Math.Clamp(elapsedMs / APPEAR_MS, 0f, 1f);

        if (elapsedMs < FADE_OUT_MS)
            return 1f;

        return Math.Clamp((TOTAL_MS - elapsedMs) / (TOTAL_MS - FADE_OUT_MS), 0f, 1f);
    }

    /// <summary>True once the mic has hit the ground and switched to its lying piece.</summary>
    public static bool IsLying(float elapsedMs) => elapsedMs >= LAND_MS;

    /// <summary>
    ///     Top row of the mic: hanging at hand height, falling with accelerating speed, arcing through one bounce,
    ///     then resting on the ground.
    /// </summary>
    public static int MicTopY(float elapsedMs)
    {
        if (elapsedMs < DROP_START_MS)
            return HAND_TOP_Y;

        if (elapsedMs < LAND_MS)
        {
            var progress = (elapsedMs - DROP_START_MS) / (LAND_MS - DROP_START_MS);

            return HAND_TOP_Y + (int)MathF.Round((UPRIGHT_REST_TOP_Y - HAND_TOP_Y) * progress * progress);
        }

        if (elapsedMs < REST_MS)
        {
            var progress = (elapsedMs - LAND_MS) / BOUNCE_MS;
            var lift = (int)MathF.Round(BOUNCE_APEX_ROWS * 4f * progress * (1f - progress));

            return LYING_REST_TOP_Y - lift;
        }

        return LYING_REST_TOP_Y;
    }

    /// <summary>One impact mark flicking out from a ground hit.</summary>
    /// <param name="X">Composite X of the mark's top-left.</param>
    /// <param name="Y">Composite Y of the mark's top-left.</param>
    /// <param name="IsLine">True for a sideways impact line, false for a rising dust dot.</param>
    /// <param name="Opacity">1 at the hit, then down to 0 over <see cref="IMPACT_MS" />.</param>
    public readonly record struct Impact(int X, int Y, bool IsLine, float Opacity);

    /// <summary>Every impact mark alive at this moment: two hits (the landing and the bounce), three marks each.</summary>
    public static IEnumerable<Impact> Impacts(float elapsedMs)
    {
        var hits = new[] { LAND_MS, REST_MS };

        for (var hit = 0; hit < hits.Length; hit++)
        {
            var age = elapsedMs - hits[hit];

            if ((age < 0f) || (age >= IMPACT_MS))
                continue;

            var spread = (int)MathF.Round(4f * age / IMPACT_MS);
            var opacity = Math.Clamp(1f - age / IMPACT_MS, 0f, 1f);
            var groundRow = PotionFlaskEmote.GROUND_Y - 1;

            yield return new Impact(IMPACT_CENTER_X - 5 - spread, groundRow, true, opacity);
            yield return new Impact(IMPACT_CENTER_X + 2 + spread, groundRow, true, opacity);
            yield return new Impact(
                IMPACT_CENTER_X + (hit == 0 ? -3 : 3),
                groundRow - (int)MathF.Round(3f * age / IMPACT_MS),
                false,
                opacity);
        }
    }

    public override IReadOnlyList<PixelLayer> Compose(float elapsedMs, int headBobRows)
    {
        if ((elapsedMs < 0f) || (elapsedMs >= TOTAL_MS))
            return [];

        var opacity = Opacity(elapsedMs);
        var lying = IsLying(elapsedMs);
        var layers = new List<PixelLayer>(5)
        {
            new(
                lying ? LyingArt : UprightArt,
                lying ? LYING_LEFT_X : MIC_X,
                MicTopY(elapsedMs),
                opacity)
        };

        foreach (var impact in Impacts(elapsedMs))
            layers.Add(
                new PixelLayer(
                    impact.IsLine ? ImpactLineArt : DustArt,
                    impact.X,
                    impact.Y,
                    impact.Opacity * opacity));

        return layers;
    }
}
