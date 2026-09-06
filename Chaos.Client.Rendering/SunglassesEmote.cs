#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Pixel art, geometry and timing for the sunglasses emote — a pair of shades that drops onto an aisling's face and
///     sparkles when it lands. Everything here is pure data and arithmetic; <see cref="SunglassesRenderer" /> turns it into
///     textures and draws it.
/// </summary>
/// <remarks>
///     Coordinates are in aisling-composite space: the 111x85 canvas <c>AislingRenderer.Composite</c> builds, whose origin
///     is the top-left of the composited texture. That canvas is drawn at
///     <c>tileCenter - (CANVAS_CENTER_X, CANVAS_CENTER_Y)</c>, so composite coordinates map to screen pixels one-to-one.
/// </remarks>
public static class SunglassesEmote
{
    /// <summary>
    ///     The body animation the client sends and listens for. Byte 18 is a gap in <c>BodyAnimation</c> (Mouth is 17,
    ///     BlowKiss is 21) and sits inside the 1-44 range the server relays untouched, so this needs no server change.
    ///     Clients that don't know the emote find no frames for it and draw nothing.
    /// </summary>
    public const int BODY_ANIMATION = 18;

    /// <summary>
    ///     Preview-frame sentinel for the emote wheel and catalog. Every other catalog entry's preview frame indexes
    ///     emot01 directly; the sunglasses have no emot01 frame of their own, so this value tells
    ///     <see cref="UiRenderer.GetEmoteFaceTexture" /> to build the icon by stamping the glasses onto
    ///     <see cref="PREVIEW_FACE_FRAME" /> instead. It sits past emot01's 50 frames so it can never collide with one.
    /// </summary>
    public const int PREVIEW_FRAME = 1000;

    /// <summary>
    ///     The emot01 frame the wheel icon stamps the glasses onto. Frame 0 (Smile) is a plain head with no speech-bubble
    ///     backdrop, and a grin under shades is the right face for this emote anyway.
    /// </summary>
    public const int PREVIEW_FACE_FRAME = 0;

    #region Timing
    /// <summary>Milliseconds the glasses spend falling before they reach the eyes.</summary>
    public const float DROP_MS = 240f;

    /// <summary>Milliseconds the landing sparkle lasts, measured from the moment the glasses land.</summary>
    public const float SPARKLE_MS = 160f;

    /// <summary>Total length of the emote, after which nothing is drawn.</summary>
    public const float TOTAL_MS = 1400f;

    /// <summary>How many distinct pictures the sparkle cycles through.</summary>
    public const int SPARKLE_STAGES = 5;

    /// <summary><see cref="Frame.SparkleStage" /> when no sparkle should be drawn.</summary>
    public const int NO_SPARKLE = -1;
    #endregion

    #region Geometry
    /// <summary>
    ///     Rows above the settled position where the glasses start. The head's top row is 24 and the glasses are
    ///     <see cref="GLASSES_HEIGHT" /> tall settling at row <see cref="SETTLED_TOP_Y" />, so anything shallower than -11
    ///     starts them already overlapping the scalp instead of falling in from above it.
    /// </summary>
    public const int START_ROW_OFFSET = -11;

    /// <summary>
    ///     Composite Y of the glasses' top row once settled. The aisling face occupies rows 24-38 in every front-facing
    ///     frame; the eyes are on rows 32-33, so the brow bar sits one row above them.
    /// </summary>
    public const int SETTLED_TOP_Y = 31;

    /// <summary>
    ///     Composite X of the pixel map's left column when the sprite is not flipped. The visible face runs from about x53
    ///     to x61 in the three-quarter front pose; the extra column to the left is the temple arm along the side of the head.
    /// </summary>
    public const int SETTLED_LEFT_X = 52;

    /// <summary>
    ///     The column <c>AislingRenderer.Composite</c> mirrors about when it flips a sprite
    ///     (<c>BODY_CENTER_X + LAYER_OFFSET_PADDING</c>, which is also <c>CANVAS_CENTER_X</c>). The glasses have to mirror
    ///     about the same column or they slide off the face when the aisling faces the other way.
    /// </summary>
    public const int FLIP_PIVOT_X = AislingRenderer.FLIP_PIVOT_X;

    /// <summary>Composite offset of the sparkle's top-left pixel relative to the glasses' top-left pixel.</summary>
    public const int SPARKLE_OFFSET_X = -2;

    /// <summary>Composite offset of the sparkle's top-left pixel relative to the glasses' top-left pixel.</summary>
    public const int SPARKLE_OFFSET_Y = -3;
    #endregion

    #region Pixel art
    // f = frame, F = frame highlight, l = lens, L = lens highlight, w = glint, . = transparent
    private const char TRANSPARENT = '.';

    /// <summary>
    ///     The glasses, 10 wide by 4 tall. Row 0 is the brow bar, rows 1-2 are the lenses, row 3 is the bottom rim. The
    ///     leftmost column is the temple arm running back along the side of the head.
    /// </summary>
    public static IReadOnlyList<string> GlassesPixels { get; } =
    [
        ".fffffffff",
        "ffLllfllLf",
        "..fllffllf",
        "...ff..ff."
    ];

    /// <summary>Width of <see cref="GlassesPixels" /> in pixels.</summary>
    public static int GLASSES_WIDTH => GlassesPixels[0].Length;

    /// <summary>Height of <see cref="GlassesPixels" /> in pixels.</summary>
    public static int GLASSES_HEIGHT => GlassesPixels.Count;

    /// <summary>
    ///     The landing sparkle, one 5x5 pixel map per stage: a point that opens into a four-pointed star, peaks, then
    ///     collapses back to a point.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<string>> SparklePixels { get; } =
    [
        [
            ".....",
            ".....",
            "..w..",
            ".....",
            "....."
        ],
        [
            ".....",
            "..w..",
            ".www.",
            "..w..",
            "....."
        ],
        [
            "..W..",
            "..w..",
            "WwwwW",
            "..w..",
            "..W.."
        ],
        [
            ".....",
            "..w..",
            ".wWw.",
            "..w..",
            "....."
        ],
        [
            ".....",
            ".....",
            "..W..",
            ".....",
            "....."
        ]
    ];
    #endregion

    #region Palette
    private static readonly Color FrameColor = new(20, 20, 22);
    private static readonly Color FrameHighlight = new(58, 58, 64);
    private static readonly Color LensColor = new(27, 42, 68);
    private static readonly Color LensHighlight = new(51, 80, 127);
    private static readonly Color Glint = new(255, 255, 255);
    private static readonly Color GlintSoft = new(188, 212, 255);

    /// <summary>
    ///     Maps a pixel-map character to the colour it paints. Returns false for the transparent character and for anything
    ///     not in the palette.
    /// </summary>
    public static bool TryGetColor(char c, out Color color)
    {
        switch (c)
        {
            case 'f':
                color = FrameColor;

                return true;
            case 'F':
                color = FrameHighlight;

                return true;
            case 'l':
                color = LensColor;

                return true;
            case 'L':
                color = LensHighlight;

                return true;
            case 'w':
                color = Glint;

                return true;
            case 'W':
                color = GlintSoft;

                return true;
            case TRANSPARENT:
                color = Color.Transparent;

                return true;
            default:
                color = default;

                return false;
        }
    }
    #endregion

    /// <summary>
    ///     Where the glasses are, and what the sparkle is doing, at a point in the emote.
    /// </summary>
    /// <param name="RowOffset">
    ///     Rows above <see cref="SETTLED_TOP_Y" />. Negative while falling, zero from the landing onward.
    /// </param>
    /// <param name="SparkleStage">
    ///     Index into <see cref="SparklePixels" />, or <see cref="NO_SPARKLE" /> when the sparkle isn't showing.
    /// </param>
    /// <param name="IsFinished">True once the emote has run its course and nothing should be drawn.</param>
    public readonly record struct Frame(int RowOffset, int SparkleStage, bool IsFinished);

    /// <summary>
    ///     Resolves the emote's state at <paramref name="elapsedMs" /> milliseconds after it started.
    /// </summary>
    public static Frame Resolve(float elapsedMs)
    {
        if (elapsedMs >= TOTAL_MS)
            return new Frame(0, NO_SPARKLE, true);

        if (elapsedMs < DROP_MS)
        {
            var progress = Math.Clamp(elapsedMs / DROP_MS, 0f, 1f);

            //ceil rather than round: the glasses only reach row offset 0 at the landing itself, so the
            //fall never appears to settle a frame early and then sit still.
            var offset = (int)Math.Ceiling(START_ROW_OFFSET * (1f - progress));

            return new Frame(Math.Clamp(offset, START_ROW_OFFSET, 0), NO_SPARKLE, false);
        }

        var sinceLanding = elapsedMs - DROP_MS;

        if (sinceLanding >= SPARKLE_MS)
            return new Frame(0, NO_SPARKLE, false);

        var stage = (int)(sinceLanding / (SPARKLE_MS / SPARKLE_STAGES));

        return new Frame(0, Math.Clamp(stage, 0, SPARKLE_STAGES - 1), false);
    }

    /// <summary>
    ///     Where composite column <paramref name="x" /> ends up once the sprite is flipped. Delegates to
    ///     <see cref="AislingRenderer.MirrorX" /> so there is only ever one copy of this arithmetic.
    /// </summary>
    public static int MirrorX(int x) => AislingRenderer.MirrorX(x);

    /// <summary>
    ///     Composite X of the glasses' left column, mirrored about <see cref="FLIP_PIVOT_X" /> when the aisling sprite is
    ///     drawn flipped. Mirroring maps the sprite's right edge onto its new left edge.
    /// </summary>
    public static int ResolveLeftX(bool flip) => flip ? MirrorX(SETTLED_LEFT_X + GLASSES_WIDTH - 1) : SETTLED_LEFT_X;

    /// <summary>
    ///     Rows the head sits lower than its resting position on this animation frame. The walk EPF drops the head one row
    ///     on its second and fourth strides; every other front-facing frame keeps it at rows 24-38. Without this the glasses
    ///     float a pixel above the eyes for half of a walk cycle.
    /// </summary>
    public static int HeadBobOffset(int frameIndex, string animSuffix) => animSuffix == "01" && frameIndex is 7 or 9 ? 1 : 0;
}
