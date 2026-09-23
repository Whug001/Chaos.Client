#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     An idea: a grey bulb pops up above the head, flickers on twice, then stays lit yellow with short rays.
/// </summary>
public sealed class LightbulbEmote : PixelArtEmote
{
    /// <summary>Byte 3 is unused by <c>BodyAnimation</c> and inside the relayed 1-44 range.</summary>
    public const int BODY_ANIMATION = 3;

    public const int PREVIEW_FRAME = 1004;
    public const float TOTAL_MS = 1600f;

    /// <summary>How long the bulb takes to rise into place.</summary>
    public const float RISE_MS = 150f;

    /// <summary>Rows below its resting place the bulb starts from. More than 3 would put it on the scalp.</summary>
    public const int RISE_ROWS = 3;

    /// <summary>From here on the bulb is lit and its rays show.</summary>
    public const float STEADY_MS = 620f;

    /// <summary>The bulb is 7 wide, x52-58, centred on the head.</summary>
    public const int LEFT_X = 52;

    /// <summary>The bulb is 10 tall, rows 12-21, leaving rows 22-23 clear above the head.</summary>
    public const int TOP_Y = 12;

    /// <summary>The rays map is 3 wider than the bulb on each side and starts 3 rows above it.</summary>
    private const int RAYS_MARGIN = 3;

    /// <summary>The two flickers before the bulb holds steady: lit from Start to End.</summary>
    private static readonly (float Start, float End)[] Flickers = [(300f, 380f), (460f, 540f)];

    public static IReadOnlyList<string> BulbPixels { get; } =
    [
        "..ggg..",
        ".ghggg.",
        "ghggggg",
        "ggggggg",
        "ggggggg",
        ".ggggg.",
        "..ggg..",
        "..mmm..",
        "..MMM..",
        "...m..."
    ];

    public static IReadOnlyList<string> RaysPixels { get; } =
    [
        "......y......",
        ".y....y....y.",
        "..y.......y..",
        ".............",
        ".............",
        "yy.........yy",
        "............."
    ];

    private static readonly Color Metal = new(118, 118, 128);
    private static readonly Color MetalDark = new(84, 84, 94);

    private static readonly PixelArt UnlitArt = PixelArt.FromPalette(
        "lightbulb:unlit",
        BulbPixels,
        new Dictionary<char, Color>
        {
            ['g'] = new(150, 152, 164),
            ['h'] = new(196, 198, 210),
            ['m'] = Metal,
            ['M'] = MetalDark
        });

    private static readonly PixelArt LitArt = PixelArt.FromPalette(
        "lightbulb:lit",
        BulbPixels,
        new Dictionary<char, Color>
        {
            ['g'] = new(255, 222, 82),
            ['h'] = new(255, 244, 176),
            ['m'] = Metal,
            ['M'] = MetalDark
        });

    private static readonly PixelArt RaysArt = PixelArt.FromPalette(
        "lightbulb:rays",
        RaysPixels,
        new Dictionary<char, Color> { ['y'] = new(255, 236, 120) });

    public static LightbulbEmote Instance { get; } = new();

    private LightbulbEmote() { }

    public override int BodyAnimation => BODY_ANIMATION;
    public override string Name => "Lightbulb";
    public override int PreviewFrame => PREVIEW_FRAME;
    public override float DurationMs => TOTAL_MS;

    /// <summary>Lit, with rays.</summary>
    public override float IconTimeMs => STEADY_MS;

    /// <summary>Rows below its resting place the bulb sits: <see cref="RISE_ROWS" /> at the start, 0 once risen.</summary>
    public static int RiseOffset(float elapsedMs) => CustomEmoteGeometry.EaseInRows(elapsedMs, RISE_MS, RISE_ROWS);

    /// <summary>True while the bulb is lit: during each flicker, and steadily from <see cref="STEADY_MS" />.</summary>
    public static bool IsLit(float elapsedMs)
    {
        if (elapsedMs >= STEADY_MS)
            return true;

        foreach (var (start, end) in Flickers)
            if ((elapsedMs >= start) && (elapsedMs < end))
                return true;

        return false;
    }

    public override IReadOnlyList<PixelLayer> Compose(float elapsedMs, int headBobRows)
    {
        if ((elapsedMs < 0f) || (elapsedMs >= TOTAL_MS))
            return [];

        var top = TOP_Y + RiseOffset(elapsedMs) + headBobRows;
        var bulb = new PixelLayer(IsLit(elapsedMs) ? LitArt : UnlitArt, LEFT_X, top);

        if (elapsedMs < STEADY_MS)
            return [bulb];

        return [bulb, new PixelLayer(RaysArt, LEFT_X - RAYS_MARGIN, top - RAYS_MARGIN)];
    }
}
