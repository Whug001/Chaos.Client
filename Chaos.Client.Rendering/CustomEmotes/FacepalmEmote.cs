#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     A facepalm: a hand rises from below the chin and slaps over the eyes with a starburst at the temple, drags
///     down in a weary rub while a sigh drifts out beside the mouth, then lowers and fades.
/// </summary>
public sealed class FacepalmEmote : PixelArtEmote
{
    /// <summary>Byte 57 is the tenth byte of emote batch three, inside the server's relayed range.</summary>
    public const int BODY_ANIMATION = 57;

    public const int PREVIEW_FRAME = 1021;
    public const float TOTAL_MS = 2200f;

    /// <summary>The hand rises over this long, easing into place.</summary>
    public const float RISE_MS = 180f;

    /// <summary>Rows below its resting place the hand starts from, under the chin.</summary>
    public const int START_ROW_OFFSET = 10;

    /// <summary>The impact starburst flashes, and the hand sits a row lower, for this long after landing.</summary>
    public const float SMACK_MS = 150f;

    /// <summary>The weary rub starts here: the hand drags down a row...</summary>
    public const float RUB_START_MS = 800f;

    /// <summary>...over this long.</summary>
    public const float RUB_MS = 600f;

    /// <summary>The sigh drifts out from beside the mouth here...</summary>
    public const float SIGH_START_MS = 1000f;

    /// <summary>...over this long.</summary>
    public const float SIGH_MS = 700f;

    /// <summary>The hand lowers and fades from here to the end.</summary>
    public const float FADE_OUT_MS = 1800f;

    /// <summary>Rows the hand sinks while it fades.</summary>
    public const int FADE_SINK_ROWS = 3;

    /// <summary>The hand sits here: 9 wide, x51-59, centred on the head (x50-60).</summary>
    public const int HAND_LEFT_X = 51;

    /// <summary>The hand is 12 tall, rows 26-37: fingertips up over the forehead, palm over the eyes and nose.</summary>
    public const int HAND_TOP_Y = 26;

    /// <summary>The impact starburst flashes here: at the temple, just right of the hand.</summary>
    public const int IMPACT_LEFT_X = 60;

    public const int IMPACT_TOP_Y = 30;

    /// <summary>The sigh drifts out from beside the chin, below the eye rows.</summary>
    public const int SIGH_ORIGIN_X = 61;

    public const int SIGH_ORIGIN_Y = 37;

    /// <summary>Columns the sigh drifts out while it fades.</summary>
    public const int SIGH_DRIFT_COLUMNS = 3;

    /// <summary>
    ///     The hand seen from the back, fingers pointing up: four separate fingers with rounded tips at slightly
    ///     different lengths and darker gap lines between them, the palm over the eyes, a thumb stub sticking out on
    ///     the left, and a wrist continuing down past the chin. A noticeably darker, warmer skin set than a typical
    ///     face, with a full outline all round — it cannot follow the body palette, which the emote never sees.
    /// </summary>
    public static IReadOnlyList<string> HandPixels { get; } =
    [
        "...k.....",
        "kkSkSk...",
        "kMSMSMSkk",
        "kMSMSMSMk",
        "kMSMSMSMk",
        "..kMMMMk.",
        ".kMSMMMk.",
        ".kMSMMMk.",
        "..kMMMMk.",
        "..kMMMk..",
        "..kSMSk..",
        "..kkkkk.."
    ];

    /// <summary>The impact mark: four short yellow lines bursting from the temple.</summary>
    public static IReadOnlyList<string> ImpactPixels { get; } =
    [
        "y...y",
        ".y.y.",
        ".....",
        ".y.y.",
        "y...y"
    ];

    /// <summary>The soft blue-grey sigh cloudlet.</summary>
    public static IReadOnlyList<string> SighPixels { get; } =
    [
        ".bbb.",
        "bBBBb",
        "bBBBb",
        ".bbb."
    ];

    private static readonly Dictionary<char, Color> HandPalette = new()
    {
        ['k'] = new Color(104, 60, 44),
        ['M'] = new Color(198, 134, 96),
        ['S'] = new Color(160, 100, 70)
    };

    private static readonly Dictionary<char, Color> SighPalette = new()
    {
        ['b'] = new Color(150, 168, 184),
        ['B'] = new Color(190, 205, 220)
    };

    private static readonly PixelArt HandArt = PixelArt.FromPalette("facepalm:hand", HandPixels, HandPalette);

    private static readonly PixelArt ImpactArt =
        PixelArt.FromPalette("facepalm:impact", ImpactPixels, new Dictionary<char, Color> { ['y'] = new Color(216, 196, 120) });

    private static readonly PixelArt SighArt = PixelArt.FromPalette("facepalm:sigh", SighPixels, SighPalette);

    public static FacepalmEmote Instance { get; } = new();

    private FacepalmEmote() { }

    public override int BodyAnimation => BODY_ANIMATION;
    public override bool RequiresUnlock => true;
    public override string Name => "Facepalm";
    public override int PreviewFrame => PREVIEW_FRAME;
    public override float DurationMs => TOTAL_MS;

    /// <summary>Holding still over the eyes: risen, smack over, rub not started, sigh not out.</summary>
    public override float IconTimeMs => 500f;

    /// <summary>Rows below its resting place the hand sits: <see cref="START_ROW_OFFSET" /> at the start, 0 once risen.</summary>
    public static int RiseOffset(float elapsedMs) => CustomEmoteGeometry.EaseInRows(elapsedMs, RISE_MS, START_ROW_OFFSET);

    /// <summary>True while the impact starburst flashes at the temple.</summary>
    public static bool IsImpact(float elapsedMs) => (elapsedMs >= RISE_MS) && (elapsedMs < RISE_MS + SMACK_MS);

    /// <summary>Rows the hand has smacked down: 1 while the impact flashes, 0 otherwise.</summary>
    public static int SmackRows(float elapsedMs) => IsImpact(elapsedMs) ? 1 : 0;

    /// <summary>Rows the hand has dragged down in its weary rub: 0, then 1 once the rub finishes.</summary>
    public static int RubRows(float elapsedMs)
    {
        if (elapsedMs < RUB_START_MS)
            return 0;

        return elapsedMs >= (RUB_START_MS + RUB_MS) ? 1 : 0;
    }

    /// <summary>1 until <see cref="FADE_OUT_MS" />, then down to 0 at the end.</summary>
    public static float Opacity(float elapsedMs)
        => elapsedMs < FADE_OUT_MS ? 1f : Math.Clamp((TOTAL_MS - elapsedMs) / (TOTAL_MS - FADE_OUT_MS), 0f, 1f);

    /// <summary>Rows the hand has sunk while fading.</summary>
    public static int LowerRows(float elapsedMs)
    {
        if (elapsedMs < FADE_OUT_MS)
            return 0;

        return (int)MathF.Round(FADE_SINK_ROWS * (elapsedMs - FADE_OUT_MS) / (TOTAL_MS - FADE_OUT_MS));
    }

    /// <summary>Top row of the hand: risen, smacked, rubbed and lowered.</summary>
    public static int HandTopY(float elapsedMs)
        => HAND_TOP_Y + RiseOffset(elapsedMs) + SmackRows(elapsedMs) + RubRows(elapsedMs) + LowerRows(elapsedMs);

    /// <summary>Where the sigh puff is, if it has drifted out and not yet faded. Null before and after.</summary>
    public static (int X, int Y, float Opacity)? Sigh(float elapsedMs)
    {
        var age = elapsedMs - SIGH_START_MS;

        if ((age < 0f) || (age >= SIGH_MS))
            return null;

        return (
            SIGH_ORIGIN_X + (int)MathF.Round(SIGH_DRIFT_COLUMNS * age / SIGH_MS),
            SIGH_ORIGIN_Y - (int)MathF.Round(1f * age / SIGH_MS),
            Math.Clamp(1f - age / SIGH_MS, 0f, 1f));
    }

    public override IReadOnlyList<PixelLayer> Compose(float elapsedMs, int headBobRows)
    {
        if ((elapsedMs < 0f) || (elapsedMs >= TOTAL_MS))
            return [];

        var opacity = Opacity(elapsedMs);
        var layers = new List<PixelLayer>(3)
        {
            new(HandArt, HAND_LEFT_X, HandTopY(elapsedMs) + headBobRows, opacity)
        };

        if (IsImpact(elapsedMs))
            layers.Add(new PixelLayer(ImpactArt, IMPACT_LEFT_X, IMPACT_TOP_Y + headBobRows, opacity));

        if (Sigh(elapsedMs) is { } sigh)
            layers.Add(new PixelLayer(SighArt, sigh.X, sigh.Y + headBobRows, sigh.Opacity * opacity));

        return layers;
    }
}
