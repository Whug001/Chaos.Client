#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     A keyboard keycap with a bold F floats in above the head; it is pressed twice, springs back each time, then
///     fades while rising.
/// </summary>
/// <remarks>
///     The F must read the right way round when the aisling is flipped, so both the resting and the pressed art carry
///     a pre-mirrored copy, exactly as the "OH YEAH!" bubble does.
/// </remarks>
public sealed class PressFEmote : PixelArtEmote
{
    /// <summary>Byte 49 is the second byte of emote batch three, inside the server's relayed range.</summary>
    public const int BODY_ANIMATION = 49;

    public const int PREVIEW_FRAME = 1013;
    public const float TOTAL_MS = 2000f;

    /// <summary>The first press starts here: the top face drops into the lip for <see cref="PRESS_MS" />.</summary>
    public const float PRESS1_START_MS = 700f;

    /// <summary>The second press starts here.</summary>
    public const float PRESS2_START_MS = 1000f;

    /// <summary>How long one press holds before the cap springs back.</summary>
    public const float PRESS_MS = 200f;

    /// <summary>The cap fades from here to the end, rising as it goes.</summary>
    public const float FADE_OUT_MS = 1700f;

    /// <summary>Rows the cap rises while it fades.</summary>
    public const int FADE_RISE_ROWS = 2;

    /// <summary>The cap is 11 wide, x50-60, centred on the head (x50-60).</summary>
    public const int LEFT_X = 50;

    /// <summary>The cap is 11 tall, rows 12-22, floating just above the head's top row (24).</summary>
    public const int REST_TOP_Y = 12;

    /// <summary>
    ///     The resting cap: a lighter top face with a bold dark F, over a darker side lip so it looks raised.
    /// </summary>
    public static IReadOnlyList<string> UpPixels { get; } =
    [
        "kkkkkkkkkkk",
        "kfffffffffk",
        "kfFFFFFfffk",
        "kfFFffffffk",
        "kfFFffffffk",
        "kfFFFFffffk",
        "kfFFffffffk",
        "kfFFffffffk",
        "kfFFffffffk",
        "ksssssssssk",
        "kkkkkkkkkkk"
    ];

    /// <summary>
    ///     The pressed cap: the top face sits two rows lower and the lip is squashed to its bottom outline.
    /// </summary>
    public static IReadOnlyList<string> PressedPixels { get; } =
    [
        "...........",
        "...........",
        "kkkkkkkkkkk",
        "kfffffffffk",
        "kfFFFFFfffk",
        "kfFFffffffk",
        "kfFFffffffk",
        "kfFFFFffffk",
        "kfFFffffffk",
        "kfFFffffffk",
        "kkkkkkkkkkk"
    ];

    private static readonly Dictionary<char, Color> Palette = new()
    {
        ['k'] = new Color(46, 42, 52),
        ['F'] = new Color(46, 42, 52),
        ['f'] = new Color(202, 198, 184),
        ['s'] = new Color(148, 144, 134)
    };

    private static readonly PixelArt UpArt = PixelArt.FromPalette("press-f:up", UpPixels, Palette);
    private static readonly PixelArt PressedArt = PixelArt.FromPalette("press-f:pressed", PressedPixels, Palette);

    /// <summary>The resting cap with its rows pre-reversed, so the F reads correctly once flipped.</summary>
    private static readonly PixelArt MirroredUpArt = PixelArt.FromPalette("press-f:up-mirrored", MirrorRows(UpPixels), Palette);

    /// <summary>The pressed cap with its rows pre-reversed, so the F reads correctly once flipped.</summary>
    private static readonly PixelArt MirroredPressedArt = PixelArt.FromPalette(
        "press-f:pressed-mirrored",
        MirrorRows(PressedPixels),
        Palette);

    public static PressFEmote Instance { get; } = new();

    private PressFEmote() { }

    public override int BodyAnimation => BODY_ANIMATION;
    public override bool RequiresUnlock => true;
    public override string Name => "Press F";
    public override int PreviewFrame => PREVIEW_FRAME;
    public override float DurationMs => TOTAL_MS;

    /// <summary>Resting above the head, before the first press.</summary>
    public override float IconTimeMs => 300f;

    /// <summary>True while the cap is pressed down: two <see cref="PRESS_MS" /> windows.</summary>
    public static bool IsPressed(float elapsedMs)
        => (elapsedMs >= PRESS1_START_MS) && (elapsedMs < PRESS1_START_MS + PRESS_MS)
            || (elapsedMs >= PRESS2_START_MS) && (elapsedMs < PRESS2_START_MS + PRESS_MS);

    /// <summary>Rows the cap has risen while fading: 0 until <see cref="FADE_OUT_MS" />, <see cref="FADE_RISE_ROWS" /> at the end.</summary>
    public static int RiseRows(float elapsedMs)
    {
        if (elapsedMs < FADE_OUT_MS)
            return 0;

        return (int)MathF.Round(FADE_RISE_ROWS * (elapsedMs - FADE_OUT_MS) / (TOTAL_MS - FADE_OUT_MS));
    }

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

        var pressed = IsPressed(elapsedMs);
        var art = pressed ? PressedArt : UpArt;
        var mirrored = pressed ? MirroredPressedArt : MirroredUpArt;

        return [new PixelLayer(art, LEFT_X, REST_TOP_Y - RiseRows(elapsedMs) + headBobRows, Opacity(elapsedMs), mirrored)];
    }
}
