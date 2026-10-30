#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     A sleepy thought cloud: a small cloud puffs up beside the head, three Z's drift up out of it one after another,
///     then the cloud fades.
/// </summary>
public sealed class ZzzCloudEmote : PixelArtEmote
{
    /// <summary>Byte 7 is unused by <c>BodyAnimation</c> (HandsUp is 6, Smile is 9) and inside the relayed range.</summary>
    public const int BODY_ANIMATION = 7;

    public const int PREVIEW_FRAME = 1007;
    public const float TOTAL_MS = 2000f;

    /// <summary>How long each of the cloud's two growing sizes shows before it is full size.</summary>
    public const float GROW_STAGE_MS = 80f;

    /// <summary>The cloud is full size, and its trail of dots shows, from here.</summary>
    public const float FULL_MS = GROW_STAGE_MS * 2;

    /// <summary>The cloud and its trail fade out from here to the end.</summary>
    public const float FADE_OUT_MS = 1700f;

    /// <summary>
    ///     Centre of the cloud: up and to the right of the head (x50-60, rows 24-38), so it reads as a thought rather
    ///     than a hat.
    /// </summary>
    public const int CLOUD_CENTER_X = 64;

    public const int CLOUD_CENTER_Y = 18;

    /// <summary>Where each Z is born: the cloud's top-right shoulder.</summary>
    public const int Z_ORIGIN_X = 67;

    public const int Z_ORIGIN_Y = 15;

    /// <summary>How long one Z lives, from the cloud to gone.</summary>
    public const float Z_LIFE_MS = 800f;

    /// <summary>Rows a Z rises over its life.</summary>
    public const int Z_RISE_ROWS = 9;

    /// <summary>Columns a Z drifts away from the head over its life.</summary>
    public const int Z_DRIFT_COLUMNS = 5;

    /// <summary>Fraction of its life after which a Z is drawn big.</summary>
    public const float Z_GROW_AT = 0.4f;

    /// <summary>Fraction of its life after which a Z starts to fade.</summary>
    public const float Z_FADE_FROM = 0.6f;

    /// <summary>The trail sits between the head's top-right corner and the cloud's bottom-left.</summary>
    private const int TRAIL_LEFT_X = 61;

    private const int TRAIL_TOP_Y = 21;

    /// <summary>One Z starts at each of these moments. The last is gone by 1900ms, before the cloud finishes fading.</summary>
    private static readonly float[] ZStartsMs = [300f, 700f, 1100f];

    private static readonly Dictionary<char, Color> CloudPalette = new()
    {
        ['c'] = new Color(204, 208, 218),
        ['C'] = new Color(160, 166, 182)
    };

    private static readonly Dictionary<char, Color> ZPalette = new() { ['z'] = new Color(132, 160, 226) };

    public static IReadOnlyList<string> CloudPixels { get; } =
    [
        "..cc.cc..",
        ".ccccccc.",
        "ccccccccc",
        "cccccccCC",
        ".CCCCCCC."
    ];

    /// <summary>The cloud's sizes, smallest first; the last is <see cref="CloudPixels" />.</summary>
    private static readonly PixelArt[] CloudStages =
    [
        PixelArt.FromPalette("zzz-cloud:cloud-small", [".c.c.", "ccccc", ".CCC."], CloudPalette),
        PixelArt.FromPalette("zzz-cloud:cloud-medium", [".cc.cc.", "ccccccc", "ccccccC", ".CCCCC."], CloudPalette),
        PixelArt.FromPalette("zzz-cloud:cloud", CloudPixels, CloudPalette)
    ];

    /// <summary>Two thought dots climbing from the head to the cloud.</summary>
    private static readonly PixelArt TrailArt = PixelArt.FromPalette("zzz-cloud:trail", ["..cc", "c..."], CloudPalette);

    //a Z needs at least 4 rows: at 3 the slash collapses into one pixel and the letter reads as an I
    private static readonly PixelArt SmallZ = PixelArt.FromPalette(
        "zzz-cloud:z-small",
        [
            "zzzz",
            "..z.",
            ".z..",
            "zzzz"
        ],
        ZPalette);

    private static readonly PixelArt BigZ = PixelArt.FromPalette(
        "zzz-cloud:z-big",
        [
            "zzzzz",
            "...z.",
            "..z..",
            ".z...",
            "zzzzz"
        ],
        ZPalette);

    public static ZzzCloudEmote Instance { get; } = new();

    private ZzzCloudEmote() { }

    public override int BodyAnimation => BODY_ANIMATION;
    public override string Name => "Zzz Cloud";
    public override int PreviewFrame => PREVIEW_FRAME;
    public override float DurationMs => TOTAL_MS;

    /// <summary>The full cloud with the first Z just out of it, small and fully opaque.</summary>
    public override float IconTimeMs => 500f;

    /// <summary>Index into the cloud sizes: 0 small, 1 medium, 2 full.</summary>
    public static int CloudStage(float elapsedMs) => Math.Clamp((int)(Math.Max(0f, elapsedMs) / GROW_STAGE_MS), 0, 2);

    /// <summary>1 until <see cref="FADE_OUT_MS" />, then down to 0 at the end.</summary>
    public static float CloudOpacity(float elapsedMs)
        => elapsedMs < FADE_OUT_MS ? 1f : Math.Clamp((TOTAL_MS - elapsedMs) / (TOTAL_MS - FADE_OUT_MS), 0f, 1f);

    /// <summary>One Z at a moment in its life.</summary>
    /// <param name="CenterX">Composite X of the Z's centre, before head bob.</param>
    /// <param name="CenterY">Composite Y of the Z's centre, before head bob.</param>
    /// <param name="IsBig">True once the Z has grown.</param>
    /// <param name="Opacity">1 until the Z starts to fade, then down to 0.</param>
    public readonly record struct Z(int CenterX, int CenterY, bool IsBig, float Opacity);

    /// <summary>Every Z alive at this moment, oldest first.</summary>
    public static IEnumerable<Z> Zs(float elapsedMs)
    {
        foreach (var start in ZStartsMs)
        {
            var age = elapsedMs - start;

            if ((age < 0f) || (age >= Z_LIFE_MS))
                continue;

            var life = age / Z_LIFE_MS;
            var rise = (int)MathF.Round(Z_RISE_ROWS * life);
            var drift = (int)MathF.Round(Z_DRIFT_COLUMNS * life);
            var opacity = life < Z_FADE_FROM ? 1f : Math.Clamp(1f - (life - Z_FADE_FROM) / (1f - Z_FADE_FROM), 0f, 1f);

            yield return new Z(Z_ORIGIN_X + drift, Z_ORIGIN_Y - rise, life >= Z_GROW_AT, opacity);
        }
    }

    public override IReadOnlyList<PixelLayer> Compose(float elapsedMs, int headBobRows)
    {
        if ((elapsedMs < 0f) || (elapsedMs >= TOTAL_MS))
            return [];

        var cloud = CloudStages[CloudStage(elapsedMs)];
        var cloudOpacity = CloudOpacity(elapsedMs);

        var layers = new List<PixelLayer>(5)
        {
            new(
                cloud,
                CLOUD_CENTER_X - cloud.Width / 2,
                CLOUD_CENTER_Y - cloud.Height / 2 + headBobRows,
                cloudOpacity)
        };

        if (elapsedMs >= FULL_MS)
            layers.Add(new PixelLayer(TrailArt, TRAIL_LEFT_X, TRAIL_TOP_Y + headBobRows, cloudOpacity));

        foreach (var z in Zs(elapsedMs))
        {
            var art = z.IsBig ? BigZ : SmallZ;

            layers.Add(new PixelLayer(art, z.CenterX - art.Width / 2, z.CenterY - art.Height / 2 + headBobRows, z.Opacity));
        }

        return layers;
    }
}
