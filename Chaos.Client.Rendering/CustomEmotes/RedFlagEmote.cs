#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     A red flag: a thin pole rises from behind the head top on the aisling's right, flying a rippling red
///     pennant away from the head, then everything fades.
/// </summary>
public sealed class RedFlagEmote : PixelArtEmote
{
    /// <summary>Byte 56 is the ninth byte of emote batch three, inside the server's relayed range.</summary>
    public const int BODY_ANIMATION = 56;

    public const int PREVIEW_FRAME = 1020;
    public const float TOTAL_MS = 2200f;

    /// <summary>The pole rises over this long, easing into place.</summary>
    public const float RISE_MS = 250f;

    /// <summary>Rows below its resting place the pole starts from, hidden behind the head.</summary>
    public const int START_ROW_OFFSET = 8;

    /// <summary>How long one wave frame of the flag shows.</summary>
    public const float WAVE_MS = 150f;

    /// <summary>Everything fades out from here to the end.</summary>
    public const float FADE_OUT_MS = 1800f;

    /// <summary>The pole stands here: 1 wide, just right of the head (x50-60).</summary>
    public const int POLE_LEFT_X = 61;

    /// <summary>The pole is 14 tall, its base at row 26 inside the head's top rows.</summary>
    public const int POLE_HEIGHT = 14;

    public const int POLE_BASE_Y = 26;

    /// <summary>The pole's top once risen: rows 13-26.</summary>
    public const int POLE_REST_TOP_Y = 13;

    /// <summary>The flag flies here: 8 wide, x62-69, away from the head, its top level with the pole's.</summary>
    public const int FLAG_LEFT_X = 62;

    public const int FLAG_WIDTH = 8;
    public const int FLAG_HEIGHT = 6;

    /// <summary>The flag ripples through these three cloth frames while it flies.</summary>
    public static IReadOnlyList<string> Flag0Pixels { get; } =
    [
        "rrrrrrrr",
        "RRrrrrrr",
        "RRssrrrr",
        "RRssrrrr",
        "RRrrrrr.",
        "rrrrrr.."
    ];

    /// <summary>The fly end lifted one row.</summary>
    public static IReadOnlyList<string> Flag1Pixels { get; } =
    [
        "rrrrrrr.",
        "RRrrrrrr",
        "RRssrrrr",
        "RRssrrrr",
        "RRrrrrrr",
        "rrrrrrr."
    ];

    /// <summary>The fly end dipped one row.</summary>
    public static IReadOnlyList<string> Flag2Pixels { get; } =
    [
        "rrrrrr..",
        "RRrrrrr.",
        "RRssrrrr",
        "RRssrrrr",
        "RRrrrrrr",
        "rrrrrrrr"
    ];

    private static readonly Dictionary<char, Color> FlagPalette = new()
    {
        ['r'] = new Color(214, 48, 60),
        ['R'] = new Color(168, 32, 48),
        ['s'] = new Color(110, 22, 36)
    };

    private static IReadOnlyList<string> PolePixels { get; } =
    [
        "p", "p", "p", "p", "p", "p", "p",
        "p", "p", "p", "p", "p", "p", "p"
    ];

    private static readonly PixelArt PoleArt =
        PixelArt.FromPalette("red-flag:pole", PolePixels, new Dictionary<char, Color> { ['p'] = new Color(74, 74, 84) });

    private static readonly PixelArt[] FlagArts =
    [
        PixelArt.FromPalette("red-flag:flag-0", Flag0Pixels, FlagPalette),
        PixelArt.FromPalette("red-flag:flag-1", Flag1Pixels, FlagPalette),
        PixelArt.FromPalette("red-flag:flag-2", Flag2Pixels, FlagPalette)
    ];

    public static RedFlagEmote Instance { get; } = new();

    private RedFlagEmote() { }

    public override int BodyAnimation => BODY_ANIMATION;
    public override string Name => "Red Flag";
    public override int PreviewFrame => PREVIEW_FRAME;
    public override float DurationMs => TOTAL_MS;

    /// <summary>Risen, mid-wave, everything opaque.</summary>
    public override float IconTimeMs => 600f;

    /// <summary>Rows below its resting place the pole sits: <see cref="START_ROW_OFFSET" /> at the start, 0 once risen.</summary>
    public static int RowOffset(float elapsedMs) => CustomEmoteGeometry.EaseInRows(elapsedMs, RISE_MS, START_ROW_OFFSET);

    /// <summary>Which cloth frame flies: 0, 1 and 2 cycling every <see cref="WAVE_MS" />.</summary>
    public static int WaveIndex(float elapsedMs)
        => (int)(Math.Clamp(elapsedMs, 0f, TOTAL_MS - 1f) / WAVE_MS) % FlagArts.Length;

    /// <summary>1 until <see cref="FADE_OUT_MS" />, then down to 0 at the end.</summary>
    public static float Opacity(float elapsedMs)
        => elapsedMs < FADE_OUT_MS ? 1f : Math.Clamp((TOTAL_MS - elapsedMs) / (TOTAL_MS - FADE_OUT_MS), 0f, 1f);

    public override IReadOnlyList<PixelLayer> Compose(float elapsedMs, int headBobRows)
    {
        if ((elapsedMs < 0f) || (elapsedMs >= TOTAL_MS))
            return [];

        var opacity = Opacity(elapsedMs);
        var top = POLE_REST_TOP_Y + RowOffset(elapsedMs) + headBobRows;

        return
        [
            new PixelLayer(PoleArt, POLE_LEFT_X, top, opacity),
            new PixelLayer(FlagArts[WaveIndex(elapsedMs)], FLAG_LEFT_X, top, opacity)
        ];
    }
}
