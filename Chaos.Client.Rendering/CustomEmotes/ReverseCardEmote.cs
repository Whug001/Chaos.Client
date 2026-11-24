#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     A reverse card: a green playing-card-sized card pops up beside the head, then flips twice like a spinning coin,
///     cycling green to blue to red, and fades.
/// </summary>
/// <remarks>
///     The two chasing arrows are rotationally symmetric (a 180-degree rotation leaves the face unchanged), so the card
///     reads the same mirrored and needs no <c>MirroredArt</c>.
/// </remarks>
public sealed class ReverseCardEmote : PixelArtEmote
{
    /// <summary>Byte 48 is the first byte of emote batch three, inside the server's relayed range.</summary>
    public const int BODY_ANIMATION = 48;

    public const int PREVIEW_FRAME = 1012;
    public const float TOTAL_MS = 2000f;

    /// <summary>How long the card takes to rise into place beside the head.</summary>
    public const float RISE_MS = 250f;

    /// <summary>Rows below its resting place the card starts from.</summary>
    public const int START_ROW_OFFSET = 4;

    /// <summary>The first flip starts here, once the card has landed.</summary>
    public const float FLIP1_START_MS = 600f;

    /// <summary>The second flip starts here.</summary>
    public const float FLIP2_START_MS = 1000f;

    /// <summary>How long one flip takes: face, side, edge, side, like the Money Eyes coin spin.</summary>
    public const float FLIP_MS = 300f;

    /// <summary>How long the face-on view holds inside a flip.</summary>
    public const float FLIP_FACE_MS = 120f;

    /// <summary>How long each turning view (side, edge, side) shows inside a flip.</summary>
    public const float FLIP_TURN_MS = 60f;

    /// <summary>The card fades out from here to the end.</summary>
    public const float FADE_OUT_MS = 1800f;

    /// <summary>The card is 11 wide, x62-72, clear of the head (x50-60) on the aisling's right side.</summary>
    public const int REST_LEFT_X = 62;

    /// <summary>The card is 15 tall, rows 20-34, sitting beside the head rather than over it.</summary>
    public const int REST_TOP_Y = 20;

    /// <summary>
    ///     The card face: a solid centre with a dark outline and a lighter inner border, two chunky bent arrows chasing
    ///     each other. The top arrow runs left-to-right then hooks down into a solid head; the bottom arrow is its
    ///     180-degree twin, running right-to-left then hooking up, with a clear row of face between them. 'f' is the
    ///     card colour, filled in per flip.
    /// </summary>
    public static IReadOnlyList<string> FacePixels { get; } =
    [
        "kkkkkkkkkkk",
        "klllllllllk",
        "klwwwwffflk",
        "klwwwwffflk",
        "klfffwwfflk",
        "klffwwwwflk",
        "klfffwwfflk",
        "klffffffflk",
        "klffwwffflk",
        "klfwwwwfflk",
        "klffwwffflk",
        "klfffwwwwlk",
        "klfffwwwwlk",
        "klllllllllk",
        "kkkkkkkkkkk"
    ];

    /// <summary>Turned halfway: the card narrows to three columns, keeping a hint of the arrows.</summary>
    public static IReadOnlyList<string> SidePixels { get; } =
    [
        "....kkk....",
        "....klk....",
        "....kfk....",
        "....kwk....",
        "....kwk....",
        "....kfk....",
        "....kfk....",
        "....kwk....",
        "....kfk....",
        "....kfk....",
        "....kwk....",
        "....kwk....",
        "....kfk....",
        "....klk....",
        "....kkk...."
    ];

    /// <summary>Edge-on: just the rim.</summary>
    public static IReadOnlyList<string> EdgePixels { get; } =
    [
        ".....k.....",
        ".....k.....",
        ".....k.....",
        ".....k.....",
        ".....k.....",
        ".....k.....",
        ".....k.....",
        ".....k.....",
        ".....k.....",
        ".....k.....",
        ".....k.....",
        ".....k.....",
        ".....k.....",
        ".....k.....",
        ".....k....."
    ];

    private static readonly Dictionary<char, Color> GreenPalette = new()
    {
        ['k'] = new Color(36, 34, 40),
        ['l'] = new Color(178, 198, 168),
        ['f'] = new Color(46, 148, 70),
        ['w'] = new Color(214, 214, 206)
    };

    private static readonly Dictionary<char, Color> BluePalette = new()
    {
        ['k'] = new Color(36, 34, 40),
        ['l'] = new Color(178, 198, 200),
        ['f'] = new Color(52, 120, 200),
        ['w'] = new Color(214, 214, 206)
    };

    private static readonly Dictionary<char, Color> RedPalette = new()
    {
        ['k'] = new Color(36, 34, 40),
        ['l'] = new Color(200, 190, 178),
        ['f'] = new Color(200, 52, 64),
        ['w'] = new Color(214, 214, 206)
    };

    private static readonly PixelArt[] FaceArts =
    [
        PixelArt.FromPalette("reverse-card:green-face", FacePixels, GreenPalette),
        PixelArt.FromPalette("reverse-card:blue-face", FacePixels, BluePalette),
        PixelArt.FromPalette("reverse-card:red-face", FacePixels, RedPalette)
    ];

    private static readonly PixelArt[] SideArts =
    [
        PixelArt.FromPalette("reverse-card:green-side", SidePixels, GreenPalette),
        PixelArt.FromPalette("reverse-card:blue-side", SidePixels, BluePalette),
        PixelArt.FromPalette("reverse-card:red-side", SidePixels, RedPalette)
    ];

    private static readonly PixelArt[] EdgeArts =
    [
        PixelArt.FromPalette("reverse-card:green-edge", EdgePixels, GreenPalette),
        PixelArt.FromPalette("reverse-card:blue-edge", EdgePixels, BluePalette),
        PixelArt.FromPalette("reverse-card:red-edge", EdgePixels, RedPalette)
    ];

    public static ReverseCardEmote Instance { get; } = new();

    private ReverseCardEmote() { }

    public override int BodyAnimation => BODY_ANIMATION;
    public override string Name => "Reverse Card";
    public override int PreviewFrame => PREVIEW_FRAME;
    public override float DurationMs => TOTAL_MS;

    /// <summary>Just landed, green and face-on, before the first flip.</summary>
    public override float IconTimeMs => RISE_MS;

    /// <summary>Rows below its resting place the card sits: <see cref="START_ROW_OFFSET" /> at the start, 0 once landed.</summary>
    public static int RowOffset(float elapsedMs) => CustomEmoteGeometry.EaseInRows(elapsedMs, RISE_MS, START_ROW_OFFSET);

    /// <summary>Which flip is running at this moment: 0, 1, or -1 when the card is not flipping.</summary>
    public static int FlipIndex(float elapsedMs)
    {
        if ((elapsedMs >= FLIP1_START_MS) && (elapsedMs < FLIP1_START_MS + FLIP_MS))
            return 0;

        if ((elapsedMs >= FLIP2_START_MS) && (elapsedMs < FLIP2_START_MS + FLIP_MS))
            return 1;

        return -1;
    }

    /// <summary>Which view of the card shows: 0 face-on, 1 side-on, 2 edge-on. Only turns while flipping.</summary>
    public static int View(float elapsedMs)
    {
        var flip = FlipIndex(elapsedMs);

        if (flip < 0)
            return 0;

        var start = flip == 0 ? FLIP1_START_MS : FLIP2_START_MS;
        var inFlip = elapsedMs - start;

        if (inFlip < FLIP_FACE_MS)
            return 0;

        return (int)((inFlip - FLIP_FACE_MS) / FLIP_TURN_MS) == 1 ? 2 : 1;
    }

    /// <summary>Which card colour shows: 0 green, 1 blue after the first flip, 2 red after the second.</summary>
    public static int CardColour(float elapsedMs)
    {
        if (elapsedMs < FLIP1_START_MS + FLIP_MS)
            return 0;

        return elapsedMs < FLIP2_START_MS + FLIP_MS ? 1 : 2;
    }

    /// <summary>1 until <see cref="FADE_OUT_MS" />, then down to 0 at the end.</summary>
    public static float Opacity(float elapsedMs)
        => elapsedMs < FADE_OUT_MS ? 1f : Math.Clamp((TOTAL_MS - elapsedMs) / (TOTAL_MS - FADE_OUT_MS), 0f, 1f);

    /// <summary>Rotates a pixel map 180 degrees: reversed row order with every row reversed.</summary>
    public static IReadOnlyList<string> Rotate180(IReadOnlyList<string> rows)
        => rows.Select(r => new string(r.Reverse().ToArray())).Reverse().ToList();

    public override IReadOnlyList<PixelLayer> Compose(float elapsedMs, int headBobRows)
    {
        if ((elapsedMs < 0f) || (elapsedMs >= TOTAL_MS))
            return [];

        var colour = CardColour(elapsedMs);
        var art = View(elapsedMs) switch
        {
            0 => FaceArts[colour],
            1 => SideArts[colour],
            _ => EdgeArts[colour]
        };

        return [new PixelLayer(art, REST_LEFT_X, REST_TOP_Y + RowOffset(elapsedMs) + headBobRows, Opacity(elapsedMs))];
    }
}
