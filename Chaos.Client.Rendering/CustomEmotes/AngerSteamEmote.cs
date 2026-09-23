#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     Fuming: puffs of steam rise from both sides of the head at ear height, drifting outward, growing and fading.
/// </summary>
public sealed class AngerSteamEmote : PixelArtEmote
{
    /// <summary>Byte 4 is unused by <c>BodyAnimation</c> and inside the relayed 1-44 range.</summary>
    public const int BODY_ANIMATION = 4;

    public const int PREVIEW_FRAME = 1005;
    public const float TOTAL_MS = 1600f;

    /// <summary>How long one puff lives, from ear to gone.</summary>
    public const float PUFF_LIFE_MS = 700f;

    /// <summary>Where the left-hand puffs are born: two columns outside the head (x50-60), at ear height.</summary>
    public const int LEFT_ORIGIN_X = CustomEmoteGeometry.HEAD_LEFT_X - 2;

    /// <summary>Where the right-hand puffs are born.</summary>
    public const int RIGHT_ORIGIN_X = CustomEmoteGeometry.HEAD_RIGHT_X + 2;

    public const int ORIGIN_Y = 30;

    /// <summary>Rows a puff rises over its life.</summary>
    public const int RISE_ROWS = 8;

    /// <summary>Columns a puff drifts away from the head over its life.</summary>
    public const int DRIFT_COLUMNS = 4;

    /// <summary>Fraction of its life after which a puff starts to fade.</summary>
    public const float FADE_FROM = 0.6f;

    /// <summary>One puff per side starts at each of these moments.</summary>
    private static readonly float[] PuffStartsMs = [0f, 400f, 800f];

    private static readonly Dictionary<char, Color> Palette = new()
    {
        ['p'] = new Color(212, 212, 218),
        ['q'] = new Color(168, 168, 180)
    };

    /// <summary>A puff's three sizes, smallest first. The bottom row is shaded.</summary>
    private static readonly PixelArt[] Stages =
    [
        PixelArt.FromPalette("anger-steam:small", [".p.", "ppp", ".q."], Palette),
        PixelArt.FromPalette("anger-steam:medium", [".pp.", "pppp", "pppp", ".qq."], Palette),
        PixelArt.FromPalette("anger-steam:large", [".ppp.", "ppppp", "ppppp", ".qqq."], Palette)
    ];

    public static AngerSteamEmote Instance { get; } = new();

    private AngerSteamEmote() { }

    public override int BodyAnimation => BODY_ANIMATION;
    public override string Name => "Anger Steam";
    public override int PreviewFrame => PREVIEW_FRAME;
    public override float DurationMs => TOTAL_MS;

    /// <summary>The first pair of puffs at medium size, still fully opaque.</summary>
    public override float IconTimeMs => 250f;

    /// <summary>One puff at a moment in its life.</summary>
    /// <param name="IsLeft">True for the puffs on the left of the unflipped sprite.</param>
    /// <param name="CenterX">Composite X of the puff's centre, before head bob.</param>
    /// <param name="CenterY">Composite Y of the puff's centre, before head bob.</param>
    /// <param name="Stage">Index into the puff sizes: 0 small, 1 medium, 2 large.</param>
    /// <param name="Opacity">1 until the puff starts to fade, then down to 0.</param>
    public readonly record struct Puff(bool IsLeft, int CenterX, int CenterY, int Stage, float Opacity);

    /// <summary>Every puff alive at this moment, left and right of each pair together.</summary>
    public static IEnumerable<Puff> Puffs(float elapsedMs)
    {
        foreach (var start in PuffStartsMs)
        {
            var age = elapsedMs - start;

            if ((age < 0f) || (age >= PUFF_LIFE_MS))
                continue;

            var life = age / PUFF_LIFE_MS;
            var stage = Math.Min(Stages.Length - 1, (int)(life * Stages.Length));
            var rise = (int)MathF.Round(RISE_ROWS * life);
            var drift = (int)MathF.Round(DRIFT_COLUMNS * life);
            var opacity = life < FADE_FROM ? 1f : Math.Clamp(1f - (life - FADE_FROM) / (1f - FADE_FROM), 0f, 1f);

            yield return new Puff(true, LEFT_ORIGIN_X - drift, ORIGIN_Y - rise, stage, opacity);
            yield return new Puff(false, RIGHT_ORIGIN_X + drift, ORIGIN_Y - rise, stage, opacity);
        }
    }

    public override IReadOnlyList<PixelLayer> Compose(float elapsedMs, int headBobRows)
    {
        if ((elapsedMs < 0f) || (elapsedMs >= TOTAL_MS))
            return [];

        var layers = new List<PixelLayer>(6);

        foreach (var puff in Puffs(elapsedMs))
        {
            var art = Stages[puff.Stage];

            layers.Add(
                new PixelLayer(art, puff.CenterX - art.Width / 2, puff.CenterY - art.Height / 2 + headBobRows, puff.Opacity));
        }

        return layers;
    }
}
