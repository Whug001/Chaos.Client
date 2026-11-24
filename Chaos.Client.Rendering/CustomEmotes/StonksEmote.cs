#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     A stonks chart: a jagged bright-green line draws itself upward beside the head, segment by segment, ending in an
///     arrowhead pointing up-right; the finished arrow pulses once, then fades.
/// </summary>
/// <remarks>
///     The chart must rise from lower-left to upper-right on screen in both facings, so every stage carries a
///     pre-mirrored copy, exactly as the "OH YEAH!" bubble does. The renderer mirrors the layer's position too, so a
///     flipped chart sits on the other side of the head but still rises left-to-right.
/// </remarks>
public sealed class StonksEmote : PixelArtEmote
{
    /// <summary>Byte 53 is the sixth byte of emote batch three, inside the server's relayed range.</summary>
    public const int BODY_ANIMATION = 53;

    public const int PREVIEW_FRAME = 1017;
    public const float TOTAL_MS = 2400f;

    /// <summary>The line draws itself over this long: one more segment every <see cref="STAGE_MS" />.</summary>
    public const float REVEAL_MS = 900f;

    /// <summary>How long each reveal stage shows before the next segment appears.</summary>
    public const float STAGE_MS = REVEAL_MS / 5f;

    /// <summary>The finished arrow shifts up a row here...</summary>
    public const float PULSE_UP_MS = 1300f;

    /// <summary>...and comes back down here.</summary>
    public const float PULSE_DOWN_MS = 1450f;

    /// <summary>The finished arrow fades out from here to the end.</summary>
    public const float FADE_OUT_MS = 2000f;

    /// <summary>The chart canvas: 18 wide, x54-71, rising from the head's right side past it.</summary>
    public const int CHART_WIDTH = 18;

    /// <summary>The chart canvas: 16 tall, rows 12-27, its tip above the head and its tail by it.</summary>
    public const int CHART_HEIGHT = 16;

    public const int CHART_LEFT_X = 54;
    public const int CHART_TOP_Y = 12;

    /// <summary>
    ///     The line's spine in chart coordinates, drawn in order: up, a small dip, up, a small dip, then a big run up
    ///     into the arrowhead. Two pixels thick once rasterised, with a darker shadow under it.
    /// </summary>
    public static IReadOnlyList<(int X, int Y)> Spine { get; } =
    [
        (1, 14), (2, 13), (3, 12), (4, 11), (5, 10), (6, 9),
        (7, 10), (8, 11),
        (9, 10), (10, 9), (11, 8), (12, 7),
        (13, 8), (14, 9),
        (14, 7), (15, 6), (16, 4), (17, 3)
    ];

    /// <summary>
    ///     The arrowhead in chart coordinates: a solid filled triangle pointing up-right, capping the shaft where it
    ///     arrives from below-left. A single-pixel tip with a clean diagonal hypotenuse, five wide at its base,
    ///     clearly wider than the line.
    /// </summary>
    public static IReadOnlyList<(int X, int Y)> Arrowhead { get; } =
    [
        (17, 2),
        (16, 3), (17, 3),
        (15, 4), (16, 4), (17, 4),
        (14, 5), (15, 5), (16, 5), (17, 5),
        (13, 6), (14, 6), (15, 6), (16, 6), (17, 6)
    ];

    /// <summary>How many spine points each reveal stage shows: four segments, then the whole run.</summary>
    public static IReadOnlyList<int> StageCuts { get; } = [6, 8, 12, 14, 18];

    private static readonly Dictionary<char, Color> ChartPalette = new()
    {
        ['g'] = new Color(78, 208, 108),
        ['G'] = new Color(34, 122, 62)
    };

    private static readonly PixelArt[] StageArts =
    [
        PixelArt.FromPalette("stonks:chart-0", BuildStage(0, false), ChartPalette),
        PixelArt.FromPalette("stonks:chart-1", BuildStage(1, false), ChartPalette),
        PixelArt.FromPalette("stonks:chart-2", BuildStage(2, false), ChartPalette),
        PixelArt.FromPalette("stonks:chart-3", BuildStage(3, false), ChartPalette),
        PixelArt.FromPalette("stonks:chart-4", BuildStage(4, false), ChartPalette)
    ];

    private static readonly PixelArt[] MirroredStageArts =
    [
        PixelArt.FromPalette("stonks:chart-0-mirrored", BuildStage(0, true), ChartPalette),
        PixelArt.FromPalette("stonks:chart-1-mirrored", BuildStage(1, true), ChartPalette),
        PixelArt.FromPalette("stonks:chart-2-mirrored", BuildStage(2, true), ChartPalette),
        PixelArt.FromPalette("stonks:chart-3-mirrored", BuildStage(3, true), ChartPalette),
        PixelArt.FromPalette("stonks:chart-4-mirrored", BuildStage(4, true), ChartPalette)
    ];

    public static StonksEmote Instance { get; } = new();

    private StonksEmote() { }

    public override int BodyAnimation => BODY_ANIMATION;
    public override bool RequiresUnlock => true;
    public override string Name => "Stonks";
    public override int PreviewFrame => PREVIEW_FRAME;
    public override float DurationMs => TOTAL_MS;

    /// <summary>Finished arrow, before the pulse: everything opaque.</summary>
    public override float IconTimeMs => 1000f;

    /// <summary>Which reveal stage shows: 0 to 3 for the four segments, 4 for the whole run with the arrowhead.</summary>
    public static int StageIndex(float elapsedMs)
        => Math.Clamp((int)(Math.Max(0f, elapsedMs) / STAGE_MS), 0, 4);

    /// <summary>Rows the finished arrow has risen: -1 in the middle of its pulse, 0 otherwise.</summary>
    public static int PulseRows(float elapsedMs)
        => (elapsedMs >= PULSE_UP_MS) && (elapsedMs < PULSE_DOWN_MS) ? -1 : 0;

    /// <summary>1 until <see cref="FADE_OUT_MS" />, then down to 0 at the end.</summary>
    public static float Opacity(float elapsedMs)
        => elapsedMs < FADE_OUT_MS ? 1f : Math.Clamp((TOTAL_MS - elapsedMs) / (TOTAL_MS - FADE_OUT_MS), 0f, 1f);

    /// <summary>Mirrors a pixel map horizontally, so it reads correctly once the renderer mirrors it back.</summary>
    public static IReadOnlyList<string> MirrorRows(IReadOnlyList<string> rows)
        => rows.Select(r => new string(r.Reverse().ToArray())).ToList();

    public override IReadOnlyList<PixelLayer> Compose(float elapsedMs, int headBobRows)
    {
        if ((elapsedMs < 0f) || (elapsedMs >= TOTAL_MS))
            return [];

        var stage = StageIndex(elapsedMs);

        return
        [
            new PixelLayer(
                StageArts[stage],
                CHART_LEFT_X,
                CHART_TOP_Y + PulseRows(elapsedMs) + headBobRows,
                Opacity(elapsedMs),
                MirroredArt: MirroredStageArts[stage])
        ];
    }

    /// <summary>
    ///     Rasterises one reveal stage into chart rows: the spine's shown points two pixels thick in both directions
    ///     with a darker shadow under the whole shape, the filled arrowhead only on the last stage. With
    ///     <paramref name="mirror" />, the rows come out pre-reversed for a flipped aisling.
    /// </summary>
    private static IReadOnlyList<string> BuildStage(int stage, bool mirror)
    {
        var grid = new char[CHART_HEIGHT][];

        for (var y = 0; y < CHART_HEIGHT; y++)
            grid[y] = new string(PixelArt.TRANSPARENT, CHART_WIDTH).ToCharArray();

        foreach (var (x, y) in Spine.Take(StageCuts[stage]).Concat(stage == 4 ? Arrowhead : []))
            for (var dy = 0; dy < 2; dy++)
                for (var dx = 0; dx < 2; dx++)
                {
                    var px = x + dx;
                    var py = y + dy;

                    if ((px < CHART_WIDTH) && (py < CHART_HEIGHT))
                        grid[py][px] = 'g';
                }

        for (var y = 0; y < CHART_HEIGHT; y++)
            for (var x = 0; x < CHART_WIDTH; x++)
                if ((grid[y][x] == 'g') && (y + 1 < CHART_HEIGHT) && (grid[y + 1][x] == PixelArt.TRANSPARENT))
                    grid[y + 1][x] = 'G';

        var rows = grid.Select(r => new string(r)).ToList();

        return mirror ? MirrorRows(rows) : rows;
    }
}
