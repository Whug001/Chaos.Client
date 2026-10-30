#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     A crown: a gold crown with a red centre gem drops onto the head, then a glint sweeps across its band once.
/// </summary>
public sealed class CrownEmote : PixelArtEmote
{
    /// <summary>
    ///     Byte 45 is past the last <c>BodyAnimation</c> emote (Confused, 44) and below the attack animations (128+).
    ///     Chaos-Server relays it from the build that raised OnEmote's limit to 47.
    /// </summary>
    public const int BODY_ANIMATION = 45;

    public const int PREVIEW_FRAME = 1009;
    public const float TOTAL_MS = 1800f;

    /// <summary>How long the crown takes to fall — the same as the party hat.</summary>
    public const float DROP_MS = 240f;

    /// <summary>Rows above its resting place the crown starts from.</summary>
    public const int START_ROW_OFFSET = -10;

    /// <summary>The glint sweeps the band from here...</summary>
    public const float GLINT_START_MS = 420f;

    /// <summary>...to here, one column per step.</summary>
    public const float GLINT_END_MS = 690f;

    /// <summary>The crown is 9 wide, x51-59, centred on the head.</summary>
    public const int LEFT_X = 51;

    /// <summary>The crown is 5 tall, rows 21-25. Its base sits over the head's top rows (24-25), like the party hat's brim.</summary>
    public const int TOP_Y = 21;

    /// <summary>The band row inside the crown art, where the gems sit and the glint runs.</summary>
    public const int BAND_ROW = 3;

    public static IReadOnlyList<string> CrownPixels { get; } =
    [
        "p...p...p",
        "g..ggg..G",
        "gggggggGG",
        "YbYgrgYbG",
        "GGGGGGGGG"
    ];

    private static readonly PixelArt CrownArt = PixelArt.FromPalette(
        "crown:crown",
        CrownPixels,
        new Dictionary<char, Color>
        {
            ['p'] = new(255, 232, 140),
            ['Y'] = new(255, 210, 80),
            ['g'] = new(228, 170, 36),
            ['G'] = new(160, 106, 20),
            ['r'] = new(200, 28, 48),
            ['b'] = new(52, 104, 212)
        });

    private static readonly PixelArt GlintArt = PixelArt.Dot("crown:glint", new Color(255, 246, 186));

    public static CrownEmote Instance { get; } = new();

    private CrownEmote() { }

    public override int BodyAnimation => BODY_ANIMATION;
    public override string Name => "Crown";
    public override int PreviewFrame => PREVIEW_FRAME;
    public override float DurationMs => TOTAL_MS;

    /// <summary>Landed, before the glint.</summary>
    public override float IconTimeMs => DROP_MS;

    /// <summary>Rows above its resting place the crown sits: <see cref="START_ROW_OFFSET" /> at the start, 0 once landed.</summary>
    public static int RowOffset(float elapsedMs) => CustomEmoteGeometry.EaseInRows(elapsedMs, DROP_MS, START_ROW_OFFSET);

    /// <summary>Crown column the glint is on, or -1 when it isn't showing.</summary>
    public static int GlintColumn(float elapsedMs)
    {
        if ((elapsedMs < GLINT_START_MS) || (elapsedMs >= GLINT_END_MS))
            return -1;

        var width = CrownPixels[0].Length;
        var step = (int)((elapsedMs - GLINT_START_MS) / ((GLINT_END_MS - GLINT_START_MS) / width));

        return Math.Min(step, width - 1);
    }

    public override IReadOnlyList<PixelLayer> Compose(float elapsedMs, int headBobRows)
    {
        if ((elapsedMs < 0f) || (elapsedMs >= TOTAL_MS))
            return [];

        var top = TOP_Y + RowOffset(elapsedMs) + headBobRows;
        var crown = new PixelLayer(CrownArt, LEFT_X, top);
        var glint = GlintColumn(elapsedMs);

        if (glint < 0)
            return [crown];

        return [crown, new PixelLayer(GlintArt, LEFT_X + glint, top + BAND_ROW)];
    }
}
