#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     A halo: a gold ring fades in just above the head, bobs a row, and a glint slides across its front once.
/// </summary>
public sealed class HaloEmote : PixelArtEmote
{
    /// <summary>Byte 2 is unused by <c>BodyAnimation</c> (Assail is 1, HandsUp is 6) and inside the relayed 1-44 range.</summary>
    public const int BODY_ANIMATION = 2;

    public const int PREVIEW_FRAME = 1003;
    public const float TOTAL_MS = 2000f;
    public const float FADE_IN_MS = 200f;

    /// <summary>How long each half of the bob lasts, after the fade-in.</summary>
    public const float BOB_HALF_MS = 300f;

    public const float GLINT_START_MS = 400f;
    public const float GLINT_END_MS = 1000f;

    /// <summary>The ring is 11 wide, x50-60 — the head's own width.</summary>
    public const int LEFT_X = CustomEmoteGeometry.HEAD_LEFT_X;

    /// <summary>The ring spans rows 19-21, leaving rows 22-23 clear above the head's top row (24).</summary>
    public const int TOP_Y = 19;

    /// <summary>First and last ring columns the glint crosses, along the front (bottom) row.</summary>
    private const int GLINT_FIRST_COLUMN = 2;

    private const int GLINT_LAST_COLUMN = 8;

    private static readonly Dictionary<char, Color> Palette = new()
    {
        ['G'] = new Color(255, 212, 72),
        ['g'] = new Color(206, 150, 36)
    };

    public static IReadOnlyList<string> RingPixels { get; } =
    [
        "..GGGGGGG..",
        "GG.......GG",
        "..ggggggg.."
    ];

    private static readonly PixelArt RingArt = PixelArt.FromPalette("halo:ring", RingPixels, Palette);
    private static readonly PixelArt GlintArt = PixelArt.Dot("halo:glint", new Color(255, 246, 196));

    public static HaloEmote Instance { get; } = new();

    private HaloEmote() { }

    public override int BodyAnimation => BODY_ANIMATION;
    public override string Name => "Halo";
    public override int PreviewFrame => PREVIEW_FRAME;
    public override float DurationMs => TOTAL_MS;

    /// <summary>Fully faded in, resting, glint gone.</summary>
    public override float IconTimeMs => GLINT_END_MS;

    /// <summary>The ring's opacity: 0 at the start, 1 once the fade-in is done.</summary>
    public static float Opacity(float elapsedMs) => Math.Clamp(elapsedMs / FADE_IN_MS, 0f, 1f);

    /// <summary>Rows the ring is lifted: 0 during the fade-in, then alternating 0 and -1.</summary>
    public static int BobRows(float elapsedMs)
    {
        if (elapsedMs < FADE_IN_MS)
            return 0;

        return (int)((elapsedMs - FADE_IN_MS) / BOB_HALF_MS) % 2 == 0 ? 0 : -1;
    }

    /// <summary>Ring column the glint is on, or -1 when it isn't showing.</summary>
    public static int GlintColumn(float elapsedMs)
    {
        if ((elapsedMs < GLINT_START_MS) || (elapsedMs >= GLINT_END_MS))
            return -1;

        const int STEPS = GLINT_LAST_COLUMN - GLINT_FIRST_COLUMN + 1;
        var step = (int)((elapsedMs - GLINT_START_MS) / ((GLINT_END_MS - GLINT_START_MS) / STEPS));

        return GLINT_FIRST_COLUMN + Math.Min(step, STEPS - 1);
    }

    public override IReadOnlyList<PixelLayer> Compose(float elapsedMs, int headBobRows)
    {
        if ((elapsedMs < 0f) || (elapsedMs >= TOTAL_MS))
            return [];

        var top = TOP_Y + BobRows(elapsedMs) + headBobRows;
        var ring = new PixelLayer(RingArt, LEFT_X, top, Opacity(elapsedMs));
        var glint = GlintColumn(elapsedMs);

        if (glint < 0)
            return [ring];

        return [ring, new PixelLayer(GlintArt, LEFT_X + glint, top + RingPixels.Count - 1)];
    }
}
