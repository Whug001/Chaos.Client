#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     Party time: a striped cone hat falls onto the head, and a burst of confetti flies off its tip when it lands.
/// </summary>
public sealed class PartyHatEmote : PixelArtEmote
{
    /// <summary>Byte 5 is unused by <c>BodyAnimation</c> and inside the relayed 1-44 range.</summary>
    public const int BODY_ANIMATION = 5;

    public const int PREVIEW_FRAME = 1006;
    public const float TOTAL_MS = 1800f;

    /// <summary>How long the hat takes to fall — the same as the sunglasses.</summary>
    public const float DROP_MS = 240f;

    /// <summary>Rows above its resting place the hat starts from.</summary>
    public const int START_ROW_OFFSET = -10;

    /// <summary>How long the confetti flies, from the landing.</summary>
    public const float CONFETTI_MS = 400f;

    /// <summary>The hat is 9 wide, x51-59, centred on the head.</summary>
    public const int LEFT_X = 51;

    /// <summary>The hat is 10 tall, rows 16-25. Its brim sits over the head's top rows (24-25).</summary>
    public const int TOP_Y = 16;

    /// <summary>Downward pull on the confetti, in pixels per second squared.</summary>
    private const float GRAVITY = 420f;

    /// <summary>Where the confetti comes from: the cone's tip, relative to the hat's top-left.</summary>
    private const int TIP_OFFSET_X = 4;

    private const int TIP_OFFSET_Y = 2;

    private static readonly Color Pink = new(236, 82, 152);
    private static readonly Color Blue = new(78, 146, 236);

    public static IReadOnlyList<string> HatPixels { get; } =
    [
        "....y....",
        "...yYy...",
        "....a....",
        "...aaa...",
        "...bbb...",
        "..bbbbb..",
        "..aaaaa..",
        ".aaaaaaa.",
        ".bbbbbbb.",
        "eeeeeeeee"
    ];

    private static readonly PixelArt HatArt = PixelArt.FromPalette(
        "party-hat:hat",
        HatPixels,
        new Dictionary<char, Color>
        {
            ['y'] = new(255, 226, 92),
            ['Y'] = new(232, 178, 40),
            ['a'] = Pink,
            ['b'] = Blue,
            ['e'] = new(250, 200, 60)
        });

    //one instance per colour: an art key must never name two different PixelArt objects
    private static readonly PixelArt PinkDot = PixelArt.Dot("party-hat:confetti-pink", Pink);
    private static readonly PixelArt BlueDot = PixelArt.Dot("party-hat:confetti-blue", Blue);

    /// <summary>Each confetti piece: launch velocity in pixels per second, and its colour.</summary>
    private static readonly (float Vx, float Vy, PixelArt Art)[] Confetti =
    [
        (-55f, -70f, PinkDot),
        (55f, -70f, BlueDot),
        (-30f, -95f, PixelArt.Dot("party-hat:confetti-yellow", new Color(255, 214, 64))),
        (30f, -95f, PixelArt.Dot("party-hat:confetti-green", new Color(86, 200, 112))),
        (-75f, -30f, PixelArt.Dot("party-hat:confetti-orange", new Color(250, 146, 52))),
        (75f, -30f, PixelArt.Dot("party-hat:confetti-purple", new Color(164, 96, 224))),
        (-12f, -110f, BlueDot),
        (12f, -110f, PinkDot)
    ];

    public static PartyHatEmote Instance { get; } = new();

    private PartyHatEmote() { }

    public override int BodyAnimation => BODY_ANIMATION;
    public override string Name => "Party Hat";
    public override int PreviewFrame => PREVIEW_FRAME;
    public override float DurationMs => TOTAL_MS;

    /// <summary>The hat on the head, the confetti gone.</summary>
    public override float IconTimeMs => DROP_MS + CONFETTI_MS;

    /// <summary>Rows above its resting place the hat sits: <see cref="START_ROW_OFFSET" /> at the start, 0 once landed.</summary>
    public static int RowOffset(float elapsedMs) => CustomEmoteGeometry.EaseInRows(elapsedMs, DROP_MS, START_ROW_OFFSET);

    public override IReadOnlyList<PixelLayer> Compose(float elapsedMs, int headBobRows)
    {
        if ((elapsedMs < 0f) || (elapsedMs >= TOTAL_MS))
            return [];

        var top = TOP_Y + RowOffset(elapsedMs) + headBobRows;
        var layers = new List<PixelLayer>(1 + Confetti.Length) { new(HatArt, LEFT_X, top) };
        var sinceLanding = elapsedMs - DROP_MS;

        if ((sinceLanding < 0f) || (sinceLanding >= CONFETTI_MS))
            return layers;

        var seconds = sinceLanding / 1000f;

        //the last 30% of the flight fades out
        var opacity = Math.Clamp((CONFETTI_MS - sinceLanding) / (CONFETTI_MS * 0.3f), 0f, 1f);
        var tipX = LEFT_X + TIP_OFFSET_X;
        var tipY = TOP_Y + TIP_OFFSET_Y + headBobRows;

        foreach (var (vx, vy, art) in Confetti)
        {
            var x = tipX + (int)MathF.Round(vx * seconds);
            var y = tipY + (int)MathF.Round(vy * seconds + 0.5f * GRAVITY * seconds * seconds);

            layers.Add(new PixelLayer(art, x, y, opacity));
        }

        return layers;
    }
}
