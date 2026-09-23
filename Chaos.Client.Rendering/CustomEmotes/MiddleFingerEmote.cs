#region
using Chaos.Client.Data;
using DALib.Drawing;
using Microsoft.Xna.Framework.Graphics;
using SkiaSharp;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     Geometry and timing for the middle finger emote — a gesture bubble above the head, in the same style as Rock On,
///     Peace and Stop, because it is made out of one of them.
/// </summary>
/// <remarks>
///     <para>
///         Nothing here is drawn by hand. The emote is emot01 frame 13 — the Stop gesture, an open palm — with every
///         finger but the middle one folded away. That frame is the only hand in the file with all five fingers drawn
///         separately, so erasing the raised part of the other four leaves their own fingertips behind as the knuckle
///         line, and the thumb comes along untouched.
///     </para>
///     <para>
///         Because the source frame is palettized through the player's body palette, the hand follows body colour for
///         free, exactly as the real gesture emotes do.
///     </para>
///     <para>
///         Coordinates are in aisling-composite space — the 111x85 canvas <c>AislingRenderer.Composite</c> builds. An
///         emot01 frame is composited into that canvas at an X offset of
///         <see cref="AislingRenderer.LAYER_OFFSET_PADDING" /> and a Y offset of zero.
///     </para>
/// </remarks>
public sealed class MiddleFingerEmote : ICustomEmote
{
    /// <summary>
    ///     The body animation the client sends and listens for. Byte 19 is a gap in <c>BodyAnimation</c> (Mouth is 17,
    ///     BlowKiss is 21) and sits inside the 1-44 range the server relays untouched, so this needs no server change.
    ///     18 is the sunglasses; 20 is Heart Eyes.
    /// </summary>
    public const int BODY_ANIMATION = 19;

    /// <summary>
    ///     Preview-frame sentinel for the emote wheel and catalog, past the end of emot01's 50 frames so it can never
    ///     collide with a real one. See <see cref="SunglassesEmote.PREVIEW_FRAME" /> for the mechanism.
    /// </summary>
    public const int PREVIEW_FRAME = 1001;

    /// <summary>The emot01 frame the hand is folded out of: Stop, the open palm.</summary>
    public const int SOURCE_FRAME = 13;

    /// <summary>
    ///     How long the bubble is held. Rock On, Peace and Stop are single frames shown for the default emote duration,
    ///     and this matches them.
    /// </summary>
    public const float DURATION_MS = 1500f;

    #region The fold
    /// <summary>Composite X of the middle finger's left edge in the source frame.</summary>
    public const int KEEP_LEFT_X = 63;

    /// <summary>Composite X of the middle finger's right edge in the source frame.</summary>
    public const int KEEP_RIGHT_X = 66;

    /// <summary>
    ///     The first composite row that survives the fold. Everything above it, outside the middle finger, is erased; the
    ///     rows from here down carry the folded fingertips, the thumb and the palm. Cutting lower flattens the knuckle
    ///     line into a straight edge.
    /// </summary>
    public const int FOLD_ROW = 8;

    /// <summary>
    ///     Last composite row of the bubble and its tail. The aisling head occupies rows 24-38, so cropping here keeps
    ///     the source frame's own face from being painted over the character's real one.
    /// </summary>
    public const int BUBBLE_BOTTOM_Y = 23;

    /// <summary>
    ///     True when the pixel at this composite position is part of a finger that should fold down out of sight.
    /// </summary>
    public static bool ShouldFoldAway(int compositeX, int compositeY)
        => (compositeY < FOLD_ROW) && ((compositeX < KEEP_LEFT_X) || (compositeX > KEEP_RIGHT_X));
    #endregion

    /// <summary>
    ///     Composite position of a pixel that is always bubble interior: inside the bubble, above the hand, left of the
    ///     middle finger. Sampling the fill from the frame rather than hardcoding white keeps it correct for every body
    ///     palette.
    /// </summary>
    public const int FILL_SAMPLE_X = 58;

    /// <inheritdoc cref="FILL_SAMPLE_X" />
    public const int FILL_SAMPLE_Y = 3;

    /// <summary>
    ///     Rows that contain only bubble — no hand — so the bubble's own colours can be told apart from the hand's at
    ///     runtime. Row 1 is the bubble's top edge; 21 to 23 are the tail.
    /// </summary>
    private static readonly int[] BubbleOnlyRows = [1, 21, 22, 23];

    /// <summary>
    ///     Folds the source frame down to a single raised finger, in place. Erased pixels become bubble fill, so the
    ///     bubble reads as empty where the fingers were.
    /// </summary>
    /// <remarks>
    ///     Only hand pixels are erased. The fold rectangle also covers the bubble's top edge and sides, and painting
    ///     those out would punch a hole in the outline — so the bubble's own colours are gathered from
    ///     <see cref="BubbleOnlyRows" /> first and skipped.
    /// </remarks>
    /// <param name="bitmap">The rendered source frame, positioned as it sits in the composite.</param>
    /// <param name="compositeOffsetX">
    ///     Where the frame's left edge sits in composite space — <see cref="AislingRenderer.LAYER_OFFSET_PADDING" />.
    /// </param>
    /// <returns>False when the frame is the wrong shape to fold, in which case nothing was changed.</returns>
    public static bool TryFold(SKBitmap bitmap, int compositeOffsetX)
    {
        if ((bitmap.Height <= FOLD_ROW) || (bitmap.Width + compositeOffsetX <= KEEP_RIGHT_X))
            return false;

        var fillX = FILL_SAMPLE_X - compositeOffsetX;

        if ((fillX < 0) || (fillX >= bitmap.Width) || (FILL_SAMPLE_Y >= bitmap.Height))
            return false;

        var fill = bitmap.GetPixel(fillX, FILL_SAMPLE_Y);

        if (fill.Alpha == 0)
            return false;

        var bubbleColors = new HashSet<uint>();

        foreach (var y in BubbleOnlyRows)
        {
            if (y >= bitmap.Height)
                continue;

            for (var x = 0; x < bitmap.Width; x++)
            {
                var c = bitmap.GetPixel(x, y);

                if (c.Alpha != 0)
                    bubbleColors.Add((uint)c);
            }
        }

        for (var y = 0; y < FOLD_ROW; y++)
        for (var x = 0; x < bitmap.Width; x++)
        {
            if (!ShouldFoldAway(x + compositeOffsetX, y))
                continue;

            var c = bitmap.GetPixel(x, y);

            if ((c.Alpha != 0) && !bubbleColors.Contains((uint)c))
                bitmap.SetPixel(x, y, fill);
        }

        return true;
    }

    /// <summary>
    ///     Composite X where the cropped bubble texture is drawn, mirrored about the sprite's own flip pivot when the
    ///     aisling faces down or left. Mirroring maps the texture's right edge onto its new left edge, which swings the
    ///     bubble to the character's other shoulder with the tail still pointing at the head — the same thing the real
    ///     gesture emotes do, since they flip as part of the composite.
    /// </summary>
    public static int ResolveLeftX(bool flip, int textureWidth)
        => flip
            ? AislingRenderer.MirrorX(AislingRenderer.LAYER_OFFSET_PADDING + textureWidth - 1)
            : AislingRenderer.LAYER_OFFSET_PADDING;

    #region ICustomEmote
    public static MiddleFingerEmote Instance { get; } = new();

    private MiddleFingerEmote() { }

    public int BodyAnimation => BODY_ANIMATION;
    public string Name => "Middle Finger";
    public int PreviewFrame => PREVIEW_FRAME;
    public int IconSourceFrame => SOURCE_FRAME;
    public float DurationMs => DURATION_MS;

    /// <summary>
    ///     Draws the bubble above the head. Draws nothing when the source art is missing or the wrong shape, so a
    ///     repacked emot01 makes the emote silent rather than garbled.
    /// </summary>
    public void Draw(in CustomEmoteDrawContext context, float elapsedMs)
    {
        if ((elapsedMs < 0f) || (elapsedMs >= DURATION_MS))
            return;

        //one texture per body colour: the source frame is palettized through the body palette, and a green aisling
        //needs a green hand
        var bodyColor = context.BodyColor;
        var texture = context.Textures.GetOrBuild($"middle-finger:{bodyColor}", () => BuildBubble(bodyColor));

        if (texture is null)
            return;

        //the bubble sits above the head rather than on the face, so unlike the glasses it ignores the walk bob —
        //the real gesture bubbles do not bob with the head either
        context.DrawTexture(texture, AislingRenderer.LAYER_OFFSET_PADDING, 0);
    }

    /// <summary>
    ///     Folds the Stop hand for the wheel and catalog icon — the same fold the world bubble gets, so the two cannot
    ///     drift apart. Returns null when the frame is the wrong shape, leaving the plain hand as the icon.
    /// </summary>
    public SKImage? BuildIcon(SKImage sourceFrame)
    {
        using var bitmap = new SKBitmap(sourceFrame.Width, sourceFrame.Height);

        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawImage(sourceFrame, 0, 0);
        }

        return TryFold(bitmap, AislingRenderer.LAYER_OFFSET_PADDING) ? SKImage.FromBitmap(bitmap) : null;
    }

    private static Texture2D? BuildBubble(int bodyColor)
    {
        var drawData = DataContext.AislingDrawData;
        var epf = drawData.EmotionsEpf;

        if (epf is null || (SOURCE_FRAME >= epf.Count))
            return null;

        if (!drawData.BodyPalettes.TryGetValue(bodyColor, out var palette) && !drawData.BodyPalettes.TryGetValue(0, out palette))
            return null;

        using var image = Graphics.RenderImage(epf[SOURCE_FRAME], palette);

        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (image is null)
            return null;

        var height = BUBBLE_BOTTOM_Y + 1;

        if (image.Height < height)
            return null;

        //crop to the bubble and its tail; rows 24 and below are the frame's own face, which would paint over
        //the character's real one
        using var bitmap = new SKBitmap(image.Width, height);

        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawImage(image, 0, 0);
        }

        //a repacked or resized frame would put the fold over the wrong pixels — draw nothing rather than garbage
        if (!TryFold(bitmap, AislingRenderer.LAYER_OFFSET_PADDING))
            return null;

        return TextureConverter.ToTexture2D(SKImage.FromBitmap(bitmap));
    }
    #endregion
}
