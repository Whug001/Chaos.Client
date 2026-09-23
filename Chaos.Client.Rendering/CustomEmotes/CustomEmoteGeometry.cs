namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     Where the aisling head sits in aisling-composite space — the 111x85 canvas <c>AislingRenderer.Composite</c>
///     builds — on the front-facing poses custom emotes are drawn on. Every position is unflipped.
/// </summary>
public static class CustomEmoteGeometry
{
    /// <summary>Top row of the head in every front-facing frame.</summary>
    public const int HEAD_TOP_Y = 24;

    /// <summary>Bottom row of the head in every front-facing frame.</summary>
    public const int HEAD_BOTTOM_Y = 38;

    /// <summary>Leftmost column of the head, measured off the rendered face EPF.</summary>
    public const int HEAD_LEFT_X = 50;

    /// <summary>Rightmost column of the head, measured off the rendered face EPF.</summary>
    public const int HEAD_RIGHT_X = 60;

    /// <summary>Top of the two eye rows.</summary>
    public const int EYE_TOP_Y = 32;

    /// <summary>
    ///     emot01 frame 0 (Smile): a plain head with no speech-bubble backdrop. Most custom emote icons are painted
    ///     onto it.
    /// </summary>
    public const int PLAIN_FACE_FRAME = 0;

    /// <summary>
    ///     Rows the head sits lower than its resting position on this animation frame. The walk EPF drops the head one row
    ///     on its second and fourth strides; every other front-facing frame keeps it at rows 24-38. Without this, art
    ///     drawn on the head floats a pixel above it for half of a walk cycle.
    /// </summary>
    public static int HeadBobOffset(int frameIndex, string animSuffix) => animSuffix == "01" && frameIndex is 7 or 9 ? 1 : 0;

    /// <summary>
    ///     Rows an art piece sits away from its resting place while it eases in: <paramref name="startRows" /> at the
    ///     start, 0 once <paramref name="durationMs" /> has passed. Uses ceiling, so the two directions round
    ///     differently: a rise (positive <paramref name="startRows" />) rounds away from rest and only reaches 0 on
    ///     the final frame, while a fall (negative <paramref name="startRows" />) rounds toward 0 and settles a
    ///     little before <paramref name="durationMs" /> is up — e.g. the party hat reaches row 0 at about 216ms of
    ///     240ms.
    /// </summary>
    public static int EaseInRows(float elapsedMs, float durationMs, int startRows)
    {
        if (elapsedMs >= durationMs)
            return 0;

        var progress = Math.Clamp(elapsedMs / durationMs, 0f, 1f);
        var offset = (int)Math.Ceiling(startRows * (1f - progress));

        return Math.Clamp(offset, Math.Min(startRows, 0), Math.Max(startRows, 0));
    }
}
