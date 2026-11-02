#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     A skull puff: dark smoke rises off the head and gathers into a small skull, which chatters its jaw three times
///     and then breaks back into smoke that drifts apart and fades.
/// </summary>
public sealed class SkullPuffEmote : PixelArtEmote
{
    /// <summary>
    ///     Byte 46 is past the last <c>BodyAnimation</c> emote (Confused, 44) and below the attack animations (128+).
    ///     Chaos-Server relays it from the build that raised OnEmote's limit to 47.
    /// </summary>
    public const int BODY_ANIMATION = 46;

    public const int PREVIEW_FRAME = 1010;
    public const float TOTAL_MS = 2000f;

    /// <summary>The smoke rises and grows until here, then the skull takes its place.</summary>
    public const float GATHER_MS = 450f;

    /// <summary>The skull breaks back into smoke here.</summary>
    public const float BREAK_MS = 1500f;

    /// <summary>The skull is 7 wide, x52-58, centred on the head.</summary>
    public const int LEFT_X = 52;

    /// <summary>The skull is 8 tall, rows 13-20, leaving rows 21-23 for the wisp of smoke beneath it.</summary>
    public const int TOP_Y = 13;

    /// <summary>Centre column of the smoke, the skull and the wisp.</summary>
    public const int CENTER_X = 55;

    /// <summary>Where the gathering smoke starts: just above the head's top row (24).</summary>
    public const int GATHER_START_Y = 22;

    /// <summary>Where the smoke ends up as it turns into the skull: the skull's middle.</summary>
    public const int SKULL_CENTER_Y = 17;

    /// <summary>Centre row of the wisp joining the skull to the head.</summary>
    public const int WISP_CENTER_Y = 22;

    /// <summary>The jaw hangs open from Start to End.</summary>
    private static readonly (float Start, float End)[] JawOpenings = [(650f, 780f), (910f, 1040f), (1170f, 1300f)];

    /// <summary>Where each puff of the broken skull ends up, relative to the skull's centre.</summary>
    private static readonly (int Dx, int Dy)[] BreakDrift = [(-6, -5), (0, -7), (6, -5)];

    private static readonly Dictionary<char, Color> SmokePalette = new()
    {
        ['s'] = new Color(112, 106, 126),
        ['S'] = new Color(78, 72, 92)
    };

    private static readonly Dictionary<char, Color> SkullPalette = new()
    {
        ['b'] = new Color(204, 198, 182),
        ['B'] = new Color(150, 144, 132),
        ['k'] = new Color(36, 30, 42)
    };

    /// <summary>A puff's three sizes, smallest first. The bottom row is shaded.</summary>
    private static readonly PixelArt[] SmokeStages =
    [
        PixelArt.FromPalette("skull-puff:smoke-small", [".s.", "sss", ".S."], SmokePalette),
        PixelArt.FromPalette("skull-puff:smoke-medium", [".ss.", "ssss", "ssss", ".SS."], SmokePalette),
        PixelArt.FromPalette("skull-puff:smoke-large", [".sss.", "sssss", "sssss", ".SSS."], SmokePalette)
    ];

    public static IReadOnlyList<string> SkullClosedPixels { get; } =
    [
        ".bbbbb.",
        "bbbbbbB",
        "bkkbkkB",
        "bkkbkkB",
        ".bbkbB.",
        ".bkbkB.",
        "..bbB..",
        "......."
    ];

    /// <summary>The same skull with the jaw dropped a row and a dark mouth showing.</summary>
    public static IReadOnlyList<string> SkullOpenPixels { get; } =
    [
        ".bbbbb.",
        "bbbbbbB",
        "bkkbkkB",
        "bkkbkkB",
        ".bbkbB.",
        ".bkbkB.",
        ".kkkkk.",
        "..bbB.."
    ];

    private static readonly PixelArt SkullClosedArt = PixelArt.FromPalette("skull-puff:skull-closed", SkullClosedPixels, SkullPalette);
    private static readonly PixelArt SkullOpenArt = PixelArt.FromPalette("skull-puff:skull-open", SkullOpenPixels, SkullPalette);

    public static SkullPuffEmote Instance { get; } = new();

    private SkullPuffEmote() { }

    public override int BodyAnimation => BODY_ANIMATION;
    public override string Name => "Skull Puff";
    public override int PreviewFrame => PREVIEW_FRAME;
    public override float DurationMs => TOTAL_MS;

    /// <summary>The skull's first moment, jaw shut.</summary>
    public override float IconTimeMs => GATHER_MS;

    /// <summary>True while the skull's jaw hangs open.</summary>
    public static bool IsJawOpen(float elapsedMs)
    {
        foreach (var (start, end) in JawOpenings)
            if ((elapsedMs >= start) && (elapsedMs < end))
                return true;

        return false;
    }

    /// <summary>True while the skull itself shows, rather than smoke.</summary>
    public static bool IsSkull(float elapsedMs) => (elapsedMs >= GATHER_MS) && (elapsedMs < BREAK_MS);

    /// <summary>The gathering smoke's size (0 small to 2 large) and centre row, before head bob.</summary>
    public static (int Stage, int CenterY) Gathering(float elapsedMs)
    {
        var progress = Math.Clamp(elapsedMs / GATHER_MS, 0f, 1f);
        var stage = Math.Min(SmokeStages.Length - 1, (int)(progress * SmokeStages.Length));
        var centerY = GATHER_START_Y - (int)MathF.Round((GATHER_START_Y - SKULL_CENTER_Y) * progress);

        return (stage, centerY);
    }

    public override IReadOnlyList<PixelLayer> Compose(float elapsedMs, int headBobRows)
    {
        if ((elapsedMs < 0f) || (elapsedMs >= TOTAL_MS))
            return [];

        if (elapsedMs < GATHER_MS)
        {
            var (stage, centerY) = Gathering(elapsedMs);

            return [Centered(SmokeStages[stage], CENTER_X, centerY + headBobRows, 1f)];
        }

        if (elapsedMs < BREAK_MS)
            return
            [
                Centered(SmokeStages[0], CENTER_X, WISP_CENTER_Y + headBobRows, 1f),
                new PixelLayer(IsJawOpen(elapsedMs) ? SkullOpenArt : SkullClosedArt, LEFT_X, TOP_Y + headBobRows)
            ];

        var life = (elapsedMs - BREAK_MS) / (TOTAL_MS - BREAK_MS);
        var puff = SmokeStages[life < 0.5f ? 2 : 1];
        var opacity = Math.Clamp(1f - life, 0f, 1f);
        var layers = new List<PixelLayer>(BreakDrift.Length);

        foreach (var (dx, dy) in BreakDrift)
            layers.Add(
                Centered(
                    puff,
                    CENTER_X + (int)MathF.Round(dx * life),
                    SKULL_CENTER_Y + (int)MathF.Round(dy * life) + headBobRows,
                    opacity));

        return layers;
    }

    private static PixelLayer Centered(PixelArt art, int centerX, int centerY, float opacity)
        => new(art, centerX - art.Width / 2, centerY - art.Height / 2, opacity);
}
