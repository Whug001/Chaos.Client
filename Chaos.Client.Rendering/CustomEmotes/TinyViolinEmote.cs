#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     A tiny violin: a very small brown violin appears by the cheek with a thin bow sawing across it while little
///     blue notes rise out of it one after another, then everything fades.
/// </summary>
public sealed class TinyViolinEmote : PixelArtEmote
{
    /// <summary>Byte 52 is the fifth byte of emote batch three, inside the server's relayed range.</summary>
    public const int BODY_ANIMATION = 52;

    public const int PREVIEW_FRAME = 1016;
    public const float TOTAL_MS = 2600f;

    /// <summary>The violin fades in over this long.</summary>
    public const float VIOLIN_IN_MS = 300f;

    /// <summary>The bow starts sawing here...</summary>
    public const float PLAY_START_MS = 400f;

    /// <summary>...and lifts off here, with the violin fading after.</summary>
    public const float PLAY_END_MS = 2000f;

    /// <summary>Everything fades out from here to the end.</summary>
    public const float FADE_OUT_MS = 2200f;

    /// <summary>How often the bow changes direction: 3 columns out, 3 columns back.</summary>
    public const float SAW_MS = 300f;

    /// <summary>Columns the bow travels between its two sawing positions.</summary>
    public const int BOW_THROW_COLUMNS = 3;

    /// <summary>How long one note lives, from the violin to gone.</summary>
    public const float NOTE_LIFE_MS = 800f;

    /// <summary>Rows a note rises over its life.</summary>
    public const int NOTE_RISE_ROWS = 8;

    /// <summary>Columns a note drifts away from the head over its life.</summary>
    public const int NOTE_DRIFT_COLUMNS = 4;

    /// <summary>Fraction of its life after which a note starts to fade.</summary>
    public const float NOTE_FADE_FROM = 0.6f;

    /// <summary>The violin is 13 wide, x59-71, overlapping the cheek so its body reads against the head.</summary>
    public const int VIOLIN_LEFT_X = 59;

    /// <summary>The violin is 6 tall, rows 33-38, sitting by the chin with the eyes left clear above it.</summary>
    public const int VIOLIN_TOP_Y = 33;

    /// <summary>The bow starts here, crossing the violin's body on a slight diagonal, and saws outward from the head.</summary>
    public const int BOW_LEFT_X = 57;

    /// <summary>The bow crosses the violin's middle rows at a slight diagonal.</summary>
    public const int BOW_TOP_Y = VIOLIN_TOP_Y + 1;

    /// <summary>Where each note is born: just above the violin's top edge.</summary>
    public const int NOTE_ORIGIN_X = 66;

    public const int NOTE_ORIGIN_Y = 32;

    /// <summary>One note rises at each of these moments, while the bow plays.</summary>
    public static IReadOnlyList<float> NoteStartsMs { get; } = [700f, 1150f, 1600f];

    /// <summary>
    ///     The violin: a horizontal figure-eight body (two rounded bouts with a pinched waist) in mid brown with a
    ///     darker outline and a lighter highlight, two dark f-hole pixels, a thin dark neck and a tan scroll/peg pair.
    /// </summary>
    public static IReadOnlyList<string> ViolinPixels { get; } =
    [
        "...kkk.......",
        "kmmhhmk......",
        ".kfmknnnnSS..",
        ".kfmk........",
        "kmmmmmk......",
        "...kkk......."
    ];

    /// <summary>The bow: a light diagonal line with a dark frog at its upper end, crossing the body's own colours.</summary>
    public static IReadOnlyList<string> BowPixels { get; } =
    [
        "ffwww.....",
        ".....www..",
        "........ww"
    ];

    /// <summary>An eighth note: a 2x2 head at the bottom-left, a 1-pixel stem 4 rows tall, a 2-pixel flag at the top.</summary>
    public static IReadOnlyList<string> NotePixels { get; } =
    [
        ".NNN",
        ".N..",
        ".N..",
        ".N..",
        "NN..",
        "NN.."
    ];

    private static readonly Dictionary<char, Color> ViolinPalette = new()
    {
        ['k'] = new Color(70, 46, 30),
        ['m'] = new Color(156, 104, 62),
        ['h'] = new Color(232, 200, 150),
        ['f'] = new Color(40, 30, 22),
        ['n'] = new Color(58, 46, 38),
        ['S'] = new Color(214, 178, 120)
    };

    private static readonly Dictionary<char, Color> BowPalette = new()
    {
        ['w'] = new Color(232, 214, 180),
        ['f'] = new Color(70, 58, 44)
    };

    private static readonly Dictionary<char, Color> NotePalette = new() { ['N'] = new Color(36, 58, 136) };

    private static readonly PixelArt ViolinArt = PixelArt.FromPalette("tiny-violin:violin", ViolinPixels, ViolinPalette);
    private static readonly PixelArt BowArt = PixelArt.FromPalette("tiny-violin:bow", BowPixels, BowPalette);
    private static readonly PixelArt NoteArt = PixelArt.FromPalette("tiny-violin:note", NotePixels, NotePalette);

    public static TinyViolinEmote Instance { get; } = new();

    private TinyViolinEmote() { }

    public override int BodyAnimation => BODY_ANIMATION;
    public override bool RequiresUnlock => true;
    public override string Name => "Tiny Violin";
    public override int PreviewFrame => PREVIEW_FRAME;
    public override float DurationMs => TOTAL_MS;

    /// <summary>Violin in, bow across it, before the first note rises: everything opaque.</summary>
    public override float IconTimeMs => 600f;

    /// <summary>0 as the violin fades in, 1 while it plays, back to 0 as it fades out.</summary>
    public static float ViolinOpacity(float elapsedMs)
    {
        if (elapsedMs < VIOLIN_IN_MS)
            return Math.Clamp(elapsedMs / VIOLIN_IN_MS, 0f, 1f);

        if (elapsedMs < FADE_OUT_MS)
            return 1f;

        return Math.Clamp((TOTAL_MS - elapsedMs) / (TOTAL_MS - FADE_OUT_MS), 0f, 1f);
    }

    /// <summary>Columns the bow has travelled outward: 0 or <see cref="BOW_THROW_COLUMNS" />, swapping every <see cref="SAW_MS" />.</summary>
    public static int BowOffset(float elapsedMs)
    {
        if ((elapsedMs < PLAY_START_MS) || (elapsedMs >= PLAY_END_MS))
            return 0;

        return (int)((elapsedMs - PLAY_START_MS) / SAW_MS) % 2 == 0 ? 0 : BOW_THROW_COLUMNS;
    }

    /// <summary>True while the bow is sawing across the violin.</summary>
    public static bool IsPlaying(float elapsedMs) => (elapsedMs >= PLAY_START_MS) && (elapsedMs < PLAY_END_MS);

    /// <summary>One note mid-flight.</summary>
    /// <param name="Index">Which entry of <see cref="NoteStartsMs" /> this note rose from.</param>
    /// <param name="CenterX">Composite X of the note's centre, before head bob.</param>
    /// <param name="CenterY">Composite Y of the note's centre, before head bob.</param>
    /// <param name="Opacity">1 until the note starts to fade, then down to 0.</param>
    public readonly record struct Note(int Index, int CenterX, int CenterY, float Opacity);

    /// <summary>Every note alive at this moment, oldest first. Each drifts up and away from the head.</summary>
    public static IEnumerable<Note> Notes(float elapsedMs)
    {
        for (var i = 0; i < NoteStartsMs.Count; i++)
        {
            var age = elapsedMs - NoteStartsMs[i];

            if ((age < 0f) || (age >= NOTE_LIFE_MS))
                continue;

            var life = age / NOTE_LIFE_MS;
            var rise = (int)MathF.Round(NOTE_RISE_ROWS * life);
            var drift = (int)MathF.Round(NOTE_DRIFT_COLUMNS * life);
            var opacity = life < NOTE_FADE_FROM
                ? 1f
                : Math.Clamp(1f - (life - NOTE_FADE_FROM) / (1f - NOTE_FADE_FROM), 0f, 1f);

            yield return new Note(i, NOTE_ORIGIN_X + drift, NOTE_ORIGIN_Y - rise, opacity);
        }
    }

    public override IReadOnlyList<PixelLayer> Compose(float elapsedMs, int headBobRows)
    {
        if ((elapsedMs < 0f) || (elapsedMs >= TOTAL_MS))
            return [];

        var opacity = ViolinOpacity(elapsedMs);
        var layers = new List<PixelLayer>(3) { new(ViolinArt, VIOLIN_LEFT_X, VIOLIN_TOP_Y + headBobRows, opacity) };

        if (IsPlaying(elapsedMs))
            layers.Add(new PixelLayer(BowArt, BOW_LEFT_X + BowOffset(elapsedMs), BOW_TOP_Y + headBobRows, opacity));

        foreach (var note in Notes(elapsedMs))
            layers.Add(
                new PixelLayer(
                    NoteArt,
                    note.CenterX - NoteArt.Width / 2,
                    note.CenterY - NoteArt.Height / 2 + headBobRows,
                    note.Opacity * opacity));

        return layers;
    }
}
