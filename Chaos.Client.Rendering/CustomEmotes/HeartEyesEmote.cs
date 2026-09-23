#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     Heart eyes: two red hearts cover the eyes and pulse between a big and a small size.
/// </summary>
public sealed class HeartEyesEmote : PixelArtEmote
{
    /// <summary>Byte 20 is the last gap between Mouth (17) and BlowKiss (21); 18 and 19 are the sunglasses and middle finger.</summary>
    public const int BODY_ANIMATION = 20;

    public const int PREVIEW_FRAME = 1002;
    public const float TOTAL_MS = 1500f;

    /// <summary>How long each heart size is held. Big, small, big… — four full pulses a second.</summary>
    public const float PULSE_MS = 125f;

    /// <summary>
    ///     Composite X of both maps' left column. The hearts centre on x54 and x60 and leave x57 clear between them, so
    ///     they read as two hearts rather than one blob.
    /// </summary>
    public const int LEFT_X = 52;

    /// <summary>The big hearts span rows 31-34, covering the eyes (32-33) and the brow above them.</summary>
    public const int BIG_TOP_Y = 31;

    /// <summary>The small hearts span rows 32-34, centred on the eyes.</summary>
    public const int SMALL_TOP_Y = 32;

    private static readonly Dictionary<char, Color> Palette = new()
    {
        ['r'] = new Color(214, 36, 64),
        ['h'] = new Color(255, 122, 150)
    };

    public static IReadOnlyList<string> BigPixels { get; } =
    [
        "rr.rr.rr.rr",
        "rhrrr.rhrrr",
        ".rrr...rrr.",
        "..r.....r.."
    ];

    public static IReadOnlyList<string> SmallPixels { get; } =
    [
        ".r.r...r.r.",
        ".hrr...hrr.",
        "..r.....r.."
    ];

    private static readonly PixelArt BigArt = PixelArt.FromPalette("heart-eyes:big", BigPixels, Palette);
    private static readonly PixelArt SmallArt = PixelArt.FromPalette("heart-eyes:small", SmallPixels, Palette);

    public static HeartEyesEmote Instance { get; } = new();

    private HeartEyesEmote() { }

    public override int BodyAnimation => BODY_ANIMATION;
    public override string Name => "Heart Eyes";
    public override int PreviewFrame => PREVIEW_FRAME;
    public override float DurationMs => TOTAL_MS;

    /// <summary>The first beat: big hearts.</summary>
    public override float IconTimeMs => 0f;

    /// <summary>True while the big hearts are showing. The emote opens on a big beat.</summary>
    public static bool IsBig(float elapsedMs) => (int)(Math.Max(0f, elapsedMs) / PULSE_MS) % 2 == 0;

    public override IReadOnlyList<PixelLayer> Compose(float elapsedMs, int headBobRows)
    {
        if ((elapsedMs < 0f) || (elapsedMs >= TOTAL_MS))
            return [];

        if (IsBig(elapsedMs))
            return [new PixelLayer(BigArt, LEFT_X, BIG_TOP_Y + headBobRows)];

        return [new PixelLayer(SmallArt, LEFT_X, SMALL_TOP_Y + headBobRows)];
    }
}
