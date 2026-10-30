#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     Money eyes: a gold coin over each eye, spinning face-on, side-on, edge-on and back while it glints.
/// </summary>
public sealed class MoneyEyesEmote : PixelArtEmote
{
    /// <summary>Byte 8 is unused by <c>BodyAnimation</c> (HandsUp is 6, Smile is 9) and inside the relayed range.</summary>
    public const int BODY_ANIMATION = 8;

    public const int PREVIEW_FRAME = 1008;
    public const float TOTAL_MS = 1500f;

    /// <summary>How long the coins show face-on in each spin. The glint is only on this face.</summary>
    public const float FACE_MS = 160f;

    /// <summary>How long each of the three turning views (side, edge, side) shows.</summary>
    public const float TURN_MS = 80f;

    /// <summary>One full spin: face, side, edge, side.</summary>
    public const float SPIN_MS = FACE_MS + TURN_MS * 3;

    /// <summary>
    ///     Composite X of both maps' left column. Same placement as Heart Eyes: the coins centre on x54 and x60 with x57
    ///     clear between them.
    /// </summary>
    public const int LEFT_X = 52;

    /// <summary>The coins span rows 31-35, centred on the eyes (32-33) and reaching just below them.</summary>
    public const int TOP_Y = 31;

    private static readonly Dictionary<char, Color> Palette = new()
    {
        ['w'] = new Color(255, 242, 168),
        ['Y'] = new Color(255, 212, 84),
        ['g'] = new Color(230, 172, 38),
        ['d'] = new Color(184, 128, 20),
        ['G'] = new Color(150, 98, 18)
    };

    /// <summary>Face-on: a round coin lit from the top left, with a stamped centre and a glint.</summary>
    public static IReadOnlyList<string> FacePixels { get; } =
    [
        ".YYg...YYg.",
        "YwggG.YwggG",
        "YgdgG.YgdgG",
        "YggGG.YggGG",
        ".GGG...GGG."
    ];

    /// <summary>Turned halfway: the coin narrows to three columns.</summary>
    public static IReadOnlyList<string> SidePixels { get; } =
    [
        "..g.....g..",
        ".YgG...YgG.",
        ".YdG...YdG.",
        ".YgG...YgG.",
        "..G.....G.."
    ];

    /// <summary>Edge-on: just the rim.</summary>
    public static IReadOnlyList<string> EdgePixels { get; } =
    [
        "..Y.....Y..",
        "..g.....g..",
        "..g.....g..",
        "..G.....G..",
        "..G.....G.."
    ];

    private static readonly PixelArt FaceArt = PixelArt.FromPalette("money-eyes:face", FacePixels, Palette);
    private static readonly PixelArt SideArt = PixelArt.FromPalette("money-eyes:side", SidePixels, Palette);
    private static readonly PixelArt EdgeArt = PixelArt.FromPalette("money-eyes:edge", EdgePixels, Palette);

    public static MoneyEyesEmote Instance { get; } = new();

    private MoneyEyesEmote() { }

    public override int BodyAnimation => BODY_ANIMATION;
    public override string Name => "Money Eyes";
    public override int PreviewFrame => PREVIEW_FRAME;
    public override float DurationMs => TOTAL_MS;

    /// <summary>The first face-on view.</summary>
    public override float IconTimeMs => 0f;

    /// <summary>Which view of the coin shows: 0 face-on, 1 side-on, 2 edge-on.</summary>
    public static int View(float elapsedMs)
    {
        var inSpin = Math.Max(0f, elapsedMs) % SPIN_MS;

        if (inSpin < FACE_MS)
            return 0;

        return (int)((inSpin - FACE_MS) / TURN_MS) == 1 ? 2 : 1;
    }

    public override IReadOnlyList<PixelLayer> Compose(float elapsedMs, int headBobRows)
    {
        if ((elapsedMs < 0f) || (elapsedMs >= TOTAL_MS))
            return [];

        var art = View(elapsedMs) switch
        {
            0 => FaceArt,
            1 => SideArt,
            _ => EdgeArt
        };

        return [new PixelLayer(art, LEFT_X, TOP_Y + headBobRows)];
    }
}
