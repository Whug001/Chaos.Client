#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     "Oh Yeah!": a stone wall appears beside the aisling, cracks and bursts, and a grinning potion flask full of green
///     brew charges through the hole. It lands, throws its arms up shouting "OH YEAH!" while its brew fizzes, then backs
///     out through the hole and the broken wall fades away.
/// </summary>
/// <remarks>
///     The scene stands on the ground beside the aisling rather than on the head, so it ignores the head bob and ends as
///     soon as the aisling walks (<see cref="EndsOnWalk" />).
/// </remarks>
public sealed class PotionFlaskEmote : PixelArtEmote
{
    /// <summary>
    ///     Byte 47 is past the last <c>BodyAnimation</c> emote (Confused, 44) and below the attack animations (128+).
    ///     Chaos-Server relays it from the build that raised OnEmote's limit to 47.
    /// </summary>
    public const int BODY_ANIMATION = 47;

    public const int PREVIEW_FRAME = 1011;
    public const float TOTAL_MS = 3000f;

    /// <summary>The wall fades in over this long.</summary>
    public const float WALL_IN_MS = 150f;

    /// <summary>The first crack shows, and the wall starts to shake, from here.</summary>
    public const float CRACK_MS = 250f;

    /// <summary>The cracks spread from here.</summary>
    public const float CRACK_SPREAD_MS = 420f;

    /// <summary>The wall bursts: the hole opens, the rubble flies and the flask leaps out.</summary>
    public const float BURST_MS = 600f;

    /// <summary>The flask lands beside the aisling and starts shouting.</summary>
    public const float LAND_MS = 800f;

    /// <summary>The shout ends and the flask starts backing out.</summary>
    public const float SHOUT_END_MS = 2200f;

    /// <summary>The flask is back at the hole and starts to fade into it.</summary>
    public const float BACK_AT_HOLE_MS = 2600f;

    /// <summary>The flask is gone, and the wall and rubble start to fade.</summary>
    public const float FADE_OUT_MS = 2700f;

    /// <summary>How long the wall holds each side of its shake.</summary>
    public const float SHAKE_MS = 50f;

    /// <summary>How long each beat of the flask's bounce lasts.</summary>
    public const float BOUNCE_MS = 200f;

    /// <summary>Rows the flask rises at the top of its leap out of the wall.</summary>
    public const int LEAP_ROWS = 6;

    /// <summary>
    ///     The last row the scene stands on: a little above the bottom of the 85-row composite, about where the aisling's
    ///     feet are.
    /// </summary>
    public const int GROUND_Y = 77;

    /// <summary>The wall stands clear of the aisling's body, x80-101.</summary>
    public const int WALL_LEFT_X = 80;

    public const int WALL_WIDTH = 22;
    public const int WALL_HEIGHT = 30;
    public const int WALL_TOP_Y = GROUND_Y - WALL_HEIGHT + 1;

    /// <summary>Centre of the hole, in wall coordinates. The cracks start here too.</summary>
    public const int HOLE_CENTER_X = 11;

    public const int HOLE_CENTER_Y = 19;

    /// <summary>Where the flask stands once it has landed: right beside the aisling, overlapping the wall's edge.</summary>
    public const int LAND_LEFT_X = 67;

    /// <summary>Where the flask stands inside the hole, as it bursts out and when it backs in.</summary>
    public const int HOLE_LEFT_X = WALL_LEFT_X + HOLE_CENTER_X - 7;

    /// <summary>
    ///     The speech bubble sits up and to the right of the landed flask, its tail pointing down at the flask's right
    ///     shoulder, so the brew fizzing straight up out of the neck (x73-75) stays in view.
    /// </summary>
    public const int BUBBLE_LEFT_X = 76;

    public const string SHOUT = "OH YEAH!";

    /// <summary>Where the icon puts the flask: right of the head (x50-60), level with it.</summary>
    public const int ICON_FLASK_LEFT_X = 62;

    public const int ICON_FLASK_TOP_Y = 22;

    /// <summary>How long one brew bubble takes to rise out of the flask's neck and pop.</summary>
    public const float FIZZ_LIFE_MS = 450f;

    /// <summary>A new brew bubble rises this often while the flask shouts.</summary>
    public const float FIZZ_EVERY_MS = 150f;

    public const int FIZZ_RISE_ROWS = 7;

    /// <summary>Downward pull on the rubble, in pixels per second squared.</summary>
    private const float GRAVITY = 600f;

    private static readonly Color Edge = new(150, 140, 122);

    private static readonly Dictionary<char, Color> StonePalette = new()
    {
        ['h'] = new Color(168, 164, 156),
        ['s'] = new Color(136, 132, 126),
        ['S'] = new Color(104, 100, 96),
        ['m'] = new Color(70, 66, 64),
        ['d'] = new Color(34, 30, 32),
        ['k'] = new Color(52, 48, 46)
    };

    private static readonly Dictionary<char, Color> FlaskPalette = new()
    {
        ['k'] = new Color(150, 100, 60),
        ['K'] = new Color(108, 70, 40),
        ['e'] = new Color(176, 206, 214),
        ['g'] = new Color(150, 182, 190),
        ['f'] = new Color(112, 140, 152),
        ['a'] = new Color(112, 140, 152),
        ['l'] = new Color(88, 196, 72),
        ['L'] = new Color(46, 138, 52),
        ['b'] = new Color(176, 240, 150),
        ['o'] = new Color(26, 40, 28),
        ['m'] = new Color(26, 40, 28),
        ['t'] = new Color(236, 230, 196)
    };

    private static readonly Dictionary<char, Color> BubblePalette = new()
    {
        ['q'] = Edge,
        ['c'] = new Color(240, 232, 196),
        ['t'] = new Color(44, 36, 40)
    };

    /// <summary>The flask with its arms hanging at its sides. Cork, glass neck, brew-filled bulb with a grin, cork feet.</summary>
    public static IReadOnlyList<string> FlaskArmsDownPixels { get; } =
    [
        "......kkK......",
        ".....kkkkK.....",
        "......egf......",
        "......egf......",
        ".....eggff.....",
        "....eglllff....",
        "...ellbllllf...",
        "..elllllllllf..",
        "..ellllllllLf..",
        "..ellolllolLf..",
        "aaellolllolLfaa",
        "a.elllllllllf.a",
        "a.elmlllllmLf.a",
        "..ellmtttmlLf..",
        "..elllmmmllLf..",
        "..eLllllllLLf..",
        "...eLLLLLLLf...",
        "....effffff....",
        "....kk...kk....",
        "...kkk...kkk..."
    ];

    /// <summary>The same flask with both arms thrown up.</summary>
    public static IReadOnlyList<string> FlaskArmsUpPixels { get; } =
    [
        "......kkK......",
        ".....kkkkK.....",
        "......egf......",
        "......egf......",
        "a....eggff....a",
        "a...eglllff...a",
        ".a.ellbllllf.a.",
        "..elllllllllf..",
        "..ellllllllLf..",
        "..ellolllolLf..",
        "..ellolllolLf..",
        "..elllllllllf..",
        "..elmlllllmLf..",
        "..ellmtttmlLf..",
        "..elllmmmllLf..",
        "..eLllllllLLf..",
        "...eLLLLLLLf...",
        "....effffff....",
        "....kk...kk....",
        "...kkk...kkk..."
    ];

    /// <summary>A 3x5 pixel font, just the letters the shout needs. A space is two blank columns.</summary>
    private static readonly Dictionary<char, string[]> Font = new()
    {
        ['O'] = ["ttt", "t.t", "t.t", "t.t", "ttt"],
        ['H'] = ["t.t", "t.t", "ttt", "t.t", "t.t"],
        ['Y'] = ["t.t", "t.t", ".t.", ".t.", ".t."],
        ['E'] = ["ttt", "t..", "tt.", "t..", "ttt"],
        ['A'] = [".t.", "t.t", "ttt", "t.t", "t.t"],
        ['!'] = ["t", "t", "t", ".", "t"],
        [' '] = ["..", "..", "..", "..", ".."]
    };

    /// <summary>The crack that shows first, in wall coordinates, spreading up-right and down-left from the hole's centre.</summary>
    private static readonly (int X, int Y)[] FirstCrack =
    [
        (11, 19), (12, 18), (12, 17), (13, 16), (11, 20), (10, 21), (10, 22), (9, 23)
    ];

    /// <summary>What the crack grows into just before the burst.</summary>
    private static readonly (int X, int Y)[] SpreadCrack =
    [
        .. FirstCrack,
        (13, 15), (14, 14), (14, 13), (15, 12), (16, 11),
        (9, 24), (8, 25), (8, 26),
        (12, 20), (13, 21), (14, 21), (15, 22), (16, 23),
        (10, 18), (9, 17), (8, 17), (7, 16), (6, 15)
    ];

    /// <summary>Each chunk of rubble: launch velocity in pixels per second, and whether it is a big chunk.</summary>
    private static readonly (float Vx, float Vy, bool Big)[] Rubble =
    [
        (-90f, -120f, true),
        (-60f, -150f, false),
        (-120f, -60f, false),
        (-30f, -170f, true),
        (40f, -130f, false),
        (70f, -90f, true),
        (-140f, -100f, false),
        (-20f, -80f, false),
        (100f, -140f, false)
    ];

    private static readonly PixelArt WallArt = PixelArt.FromPalette("potion-flask:wall", BuildWall(false), StonePalette);
    private static readonly PixelArt BrokenWallArt = PixelArt.FromPalette("potion-flask:wall-broken", BuildWall(true), StonePalette);
    private static readonly PixelArt FirstCrackArt = PixelArt.FromPalette("potion-flask:crack-1", BuildCrack(FirstCrack), StonePalette);
    private static readonly PixelArt SpreadCrackArt = PixelArt.FromPalette("potion-flask:crack-2", BuildCrack(SpreadCrack), StonePalette);
    private static readonly PixelArt SmallChunk = PixelArt.FromPalette("potion-flask:chunk-small", ["hs", "sS"], StonePalette);
    private static readonly PixelArt BigChunk = PixelArt.FromPalette("potion-flask:chunk-big", ["hss", "sSS"], StonePalette);
    private static readonly PixelArt FlaskArmsDown = PixelArt.FromPalette("potion-flask:flask-down", FlaskArmsDownPixels, FlaskPalette);
    private static readonly PixelArt FlaskArmsUp = PixelArt.FromPalette("potion-flask:flask-up", FlaskArmsUpPixels, FlaskPalette);
    private static readonly PixelArt FizzArt = PixelArt.Dot("potion-flask:fizz", new Color(176, 240, 150));
    private static readonly PixelArt BubbleArt = PixelArt.FromPalette("potion-flask:bubble", BuildSpeechBubble(SHOUT), BubblePalette);

    /// <summary>The bubble with its text pre-reversed, so the text reads correctly once a flipped aisling mirrors it.</summary>
    private static readonly PixelArt MirroredBubbleArt = PixelArt.FromPalette(
        "potion-flask:bubble-mirrored",
        BuildSpeechBubble(SHOUT, true),
        BubblePalette);

    public static PotionFlaskEmote Instance { get; } = new();

    private PotionFlaskEmote() { }

    public static int FlaskWidth => FlaskArmsDownPixels[0].Length;
    public static int FlaskHeight => FlaskArmsDownPixels.Count;
    public static int FlaskRestTopY => GROUND_Y - FlaskHeight + 1;

    public override int BodyAnimation => BODY_ANIMATION;
    public override string Name => "Oh Yeah!";
    public override int PreviewFrame => PREVIEW_FRAME;
    public override float DurationMs => TOTAL_MS;
    public override bool EndsOnWalk => true;

    /// <summary>Mid-shout: landed, arms up, everything opaque.</summary>
    public override float IconTimeMs => LAND_MS;

    /// <summary>The whole scene is far too wide for a wheel slot, so the icon is just the cheering flask beside the head.</summary>
    public override IReadOnlyList<PixelLayer> IconLayers() => [new PixelLayer(FlaskArmsUp, ICON_FLASK_LEFT_X, ICON_FLASK_TOP_Y)];

    /// <summary>0 to 1 as the wall fades in, 1 while it stands, back to 0 as it fades out.</summary>
    public static float WallOpacity(float elapsedMs)
    {
        if (elapsedMs < WALL_IN_MS)
            return Math.Clamp(elapsedMs / WALL_IN_MS, 0f, 1f);

        if (elapsedMs < FADE_OUT_MS)
            return 1f;

        return Math.Clamp((TOTAL_MS - elapsedMs) / (TOTAL_MS - FADE_OUT_MS), 0f, 1f);
    }

    /// <summary>Columns the wall is pushed sideways while it shakes before the burst: 0, 1 or -1.</summary>
    public static int ShakeColumns(float elapsedMs)
    {
        if ((elapsedMs < CRACK_MS) || (elapsedMs >= BURST_MS))
            return 0;

        return (int)((elapsedMs - CRACK_MS) / SHAKE_MS) % 2 == 0 ? 1 : -1;
    }

    /// <summary>
    ///     Where the flask is: its top-left corner, whether its arms are up, and its opacity. Null before the burst and
    ///     once it has gone back into the hole.
    /// </summary>
    public static (int X, int Y, bool ArmsUp, float Opacity)? Flask(float elapsedMs)
    {
        if ((elapsedMs < BURST_MS) || (elapsedMs >= FADE_OUT_MS))
            return null;

        if (elapsedMs < LAND_MS)
        {
            var progress = (elapsedMs - BURST_MS) / (LAND_MS - BURST_MS);
            var x = HOLE_LEFT_X + (int)MathF.Round((LAND_LEFT_X - HOLE_LEFT_X) * progress);
            var lift = (int)MathF.Round(LEAP_ROWS * MathF.Sin(MathF.PI * progress));

            return (x, FlaskRestTopY - lift, false, 1f);
        }

        if (elapsedMs < SHOUT_END_MS)
        {
            var up = (int)((elapsedMs - LAND_MS) / BOUNCE_MS) % 2 == 1;

            return (LAND_LEFT_X, FlaskRestTopY - (up ? 1 : 0), true, 1f);
        }

        if (elapsedMs < BACK_AT_HOLE_MS)
        {
            var progress = (elapsedMs - SHOUT_END_MS) / (BACK_AT_HOLE_MS - SHOUT_END_MS);
            var x = LAND_LEFT_X + (int)MathF.Round((HOLE_LEFT_X - LAND_LEFT_X) * progress);

            return (x, FlaskRestTopY, false, 1f);
        }

        return (HOLE_LEFT_X, FlaskRestTopY, false, Math.Clamp((FADE_OUT_MS - elapsedMs) / (FADE_OUT_MS - BACK_AT_HOLE_MS), 0f, 1f));
    }

    /// <summary>Top-left of each rubble chunk, flying from the hole and then lying where it lands. Empty before the burst.</summary>
    public static IEnumerable<(int X, int Y, bool Big)> RubbleChunks(float elapsedMs)
    {
        if (elapsedMs < BURST_MS)
            yield break;

        var seconds = (elapsedMs - BURST_MS) / 1000f;
        var originX = WALL_LEFT_X + HOLE_CENTER_X;
        var originY = WALL_TOP_Y + HOLE_CENTER_Y;

        foreach (var (vx, vy, big) in Rubble)
        {
            var art = big ? BigChunk : SmallChunk;
            var restY = GROUND_Y - art.Height + 1;

            //when the chunk's arc comes back down to the ground: originY + vy*t + g*t^2/2 = restY
            var landing = (-vy + MathF.Sqrt(vy * vy + 2f * GRAVITY * (restY - originY))) / GRAVITY;
            var t = Math.Min(seconds, landing);
            var y = originY + vy * t + 0.5f * GRAVITY * t * t;

            yield return (originX + (int)MathF.Round(vx * t), Math.Min(restY, (int)MathF.Round(y)), big);
        }
    }

    /// <summary>Each brew bubble fizzing out of the flask's neck while it shouts: offset from the neck, and opacity.</summary>
    public static IEnumerable<(int Dx, int Dy, float Opacity)> Fizz(float elapsedMs)
    {
        if ((elapsedMs < LAND_MS) || (elapsedMs >= SHOUT_END_MS + FIZZ_LIFE_MS))
            yield break;

        for (var start = LAND_MS; (start <= elapsedMs) && (start < SHOUT_END_MS); start += FIZZ_EVERY_MS)
        {
            var age = elapsedMs - start;

            if (age >= FIZZ_LIFE_MS)
                continue;

            var life = age / FIZZ_LIFE_MS;
            var index = (int)((start - LAND_MS) / FIZZ_EVERY_MS);

            //alternate bubbles lean left and right; every bubble wobbles a column every other row it rises
            var rise = (int)MathF.Round(FIZZ_RISE_ROWS * life);
            var dx = (index % 2 == 0 ? -1 : 1) * ((rise / 2) % 2);
            var opacity = life < 0.6f ? 1f : Math.Clamp(1f - (life - 0.6f) / 0.4f, 0f, 1f);

            yield return (dx, -rise, opacity);
        }
    }

    /// <summary>True while the "OH YEAH!" speech bubble shows.</summary>
    public static bool IsShouting(float elapsedMs) => (elapsedMs >= LAND_MS) && (elapsedMs < SHOUT_END_MS);

    public override IReadOnlyList<PixelLayer> Compose(float elapsedMs, int headBobRows)
    {
        if ((elapsedMs < 0f) || (elapsedMs >= TOTAL_MS))
            return [];

        var wallOpacity = WallOpacity(elapsedMs);
        var wallX = WALL_LEFT_X + ShakeColumns(elapsedMs);
        var layers = new List<PixelLayer>(24) { new(elapsedMs < BURST_MS ? WallArt : BrokenWallArt, wallX, WALL_TOP_Y, wallOpacity) };

        if (elapsedMs is >= CRACK_MS and < BURST_MS)
            layers.Add(new PixelLayer(elapsedMs < CRACK_SPREAD_MS ? FirstCrackArt : SpreadCrackArt, wallX, WALL_TOP_Y));

        foreach (var (x, y, big) in RubbleChunks(elapsedMs))
            layers.Add(new PixelLayer(big ? BigChunk : SmallChunk, x, y, wallOpacity));

        if (Flask(elapsedMs) is not { } flask)
            return layers;

        layers.Add(new PixelLayer(flask.ArmsUp ? FlaskArmsUp : FlaskArmsDown, flask.X, flask.Y, flask.Opacity));

        var neckX = flask.X + FlaskWidth / 2;

        foreach (var (dx, dy, opacity) in Fizz(elapsedMs))
            layers.Add(new PixelLayer(FizzArt, neckX + dx, flask.Y + dy, opacity));

        if (IsShouting(elapsedMs))
            layers.Add(
                new PixelLayer(
                    BubbleArt,
                    BUBBLE_LEFT_X,
                    FlaskRestTopY - BubbleArt.Height - 1,
                    MirroredArt: MirroredBubbleArt));

        return layers;
    }

    /// <summary>
    ///     Stone courses 5 rows tall (highlight, two stone, shade, mortar), bricks 7 wide, every other course offset by
    ///     half a brick. With <paramref name="broken" />, a ragged dark hole is punched through around the hole's centre.
    /// </summary>
    private static IReadOnlyList<string> BuildWall(bool broken)
    {
        var rows = new string[WALL_HEIGHT];

        for (var y = 0; y < WALL_HEIGHT; y++)
        {
            var row = new char[WALL_WIDTH];
            var course = y / 5;
            var inCourse = y % 5;
            var offset = course % 2 == 0 ? 0 : 3;

            for (var x = 0; x < WALL_WIDTH; x++)
            {
                if (broken && HoleShade(x, y) is { } hole)
                {
                    row[x] = hole;

                    continue;
                }

                if (y == WALL_HEIGHT - 1)
                    row[x] = 'S';
                else if ((inCourse == 4) || ((x + offset) % 7 == 6))
                    row[x] = 'm';
                else
                    row[x] = inCourse switch
                    {
                        0 => 'h',
                        3 => 'S',
                        _ => 's'
                    };
            }

            rows[y] = new string(row);
        }

        return rows;
    }

    /// <summary>
    ///     'd' inside the hole, 'S' on its broken rim, null for untouched wall. The hole's radius wobbles with the angle
    ///     so its edge looks smashed rather than drilled.
    /// </summary>
    private static char? HoleShade(int x, int y)
    {
        var dx = (x - HOLE_CENTER_X) / 6f;
        var dy = (y - HOLE_CENTER_Y) / 8.5f;
        var angle = MathF.Atan2(dy, dx);
        var reach = 1f + 0.18f * MathF.Sin(5f * angle) + 0.08f * MathF.Sin(11f * angle);
        var distance = MathF.Sqrt(dx * dx + dy * dy) / reach;

        if (distance < 1f)
            return 'd';

        return distance < 1.18f ? 'S' : null;
    }

    private static IReadOnlyList<string> BuildCrack((int X, int Y)[] points)
    {
        var grid = new char[WALL_HEIGHT][];

        for (var y = 0; y < WALL_HEIGHT; y++)
            grid[y] = new string(PixelArt.TRANSPARENT, WALL_WIDTH).ToCharArray();

        foreach (var (x, y) in points)
            grid[y][x] = 'k';

        return grid.Select(r => new string(r)).ToList();
    }

    /// <summary>
    ///     A cream speech bubble with rounded corners, the text in the 3x5 font with a column between letters, and a
    ///     two-row tail under its sixth column pointing down at the flask's neck. With <paramref name="reverseText" />, only
    ///     the text is mirrored, so the tail still swings to the flask's side when the whole bubble is flipped.
    /// </summary>
    public static IReadOnlyList<string> BuildSpeechBubble(string text, bool reverseText = false)
    {
        var glyphs = text.Select(c => Font[c]).ToList();
        var textWidth = glyphs.Sum(g => g[0].Length) + glyphs.Count - 1;
        var width = textWidth + 4;
        var rows = new List<string>
        {
            "." + new string('q', width - 2) + ".",
            "q" + new string('c', width - 2) + "q"
        };

        for (var line = 0; line < 5; line++)
        {
            var letters = string.Join(".", glyphs.Select(g => g[line]));

            if (reverseText)
                letters = new string(letters.Reverse().ToArray());

            rows.Add("qc" + letters.Replace('.', 'c') + "cq");
        }

        rows.Add("q" + new string('c', width - 2) + "q");
        rows.Add("." + new string('q', width - 2) + ".");

        const int TAIL_X = 5;
        rows.Add(new string('.', TAIL_X) + "qq" + new string('.', width - TAIL_X - 2));
        rows.Add(new string('.', TAIL_X) + "q" + new string('.', width - TAIL_X - 1));

        return rows;
    }
}
