#region
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Paints a town banner post in the isometric retail style: a stone plinth on the tile, a round wooden pole with a gold
///     finial, and a crossbar running along the map's iso axis (rising to the right, two pixels across for one up). The
///     swallowtail cloth hangs from the crossbar in that same plane, so its columns are sheared one pixel up for every two
///     across, and the town's 11 × 11 banner is painted on it at 2×. Light comes from the upper left. Works on a plain
///     pixel buffer, so it is tested without a graphics device. The foot pixel (<see cref="FOOT_X" />,
///     <see cref="FOOT_Y" />) stands on the tile's floor centre.
/// </summary>
public static class BannerPostPainter
{
    public const int WIDTH = 48;
    public const int HEIGHT = 100;
    public const int FOOT_X = 24;
    public const int FOOT_Y = 95;
    public const int SCALE = 2;

    //cloth columns, and the crossbar's centre line: y = BAR_LEFT_Y - (x - CLOTH_LEFT) / 2
    private const int CLOTH_LEFT = 9;
    private const int CLOTH_WIDTH = 30;
    private const int BAR_LEFT_Y = 21;
    private const int BORDER = 1;
    private const int MARGIN = 3;
    private const int EMBLEM_SIZE = GuildEmblemProtocol.SIZE * SCALE;
    private const int BODY_ROWS = (2 * (BORDER + MARGIN)) + EMBLEM_SIZE;
    private const int TAIL_ROWS = 8;
    private const int POLE_LEFT = FOOT_X - 2;
    private const int POLE_TOP = 8;
    private const int PLINTH_Y = 91;
    private const int PLINTH_HALF_WIDTH = 8;
    private const int PLINTH_HALF_HEIGHT = 4;
    private const int PLINTH_DEPTH = 3;
    private const float FIELD_SHADE = 0.45f;

    private static readonly Color WoodOutline = new(42, 28, 18);
    private static readonly Color WoodLight = new(176, 128, 78);
    private static readonly Color Wood = new(128, 86, 48);
    private static readonly Color WoodDark = new(82, 54, 30);
    private static readonly Color GoldLight = new(250, 226, 128);
    private static readonly Color Gold = new(212, 175, 55);
    private static readonly Color GoldDark = new(140, 106, 30);
    private static readonly Color GoldOutline = new(70, 50, 15);
    private static readonly Color StoneLight = new(156, 150, 138);
    private static readonly Color Stone = new(132, 127, 117);
    private static readonly Color StoneLeft = new(106, 101, 94);
    private static readonly Color StoneRight = new(76, 72, 68);
    private static readonly Color StoneOutline = new(38, 36, 34);
    private static readonly Color DefaultField = new(70, 40, 30);

    /// <summary>The top-left pixel of the emblem's first (top-left) 2 × 2 block.</summary>
    public static Point EmblemOrigin => EmblemBlock(0, 0);

    /// <summary>
    ///     The top-left image pixel of the 2 × 2 block that shows emblem pixel (<paramref name="ex" />,
    ///     <paramref name="ey" />). Each emblem column sits one pixel higher than the one to its left (the iso shear).
    /// </summary>
    public static Point EmblemBlock(int ex, int ey)
    {
        var x = CLOTH_LEFT + BORDER + MARGIN + (ex * SCALE);

        return new Point(x, ClothTop(x) + BORDER + MARGIN + (ey * SCALE));
    }

    /// <summary>The emblem's most used colour, darkened; a dark brown when the emblem has no paint.</summary>
    public static Color FieldColor(GuildEmblemDesign design)
    {
        var counts = new int[design.Colors.Count + 1];

        foreach (var number in design.Pixels)
            if ((number != 0) && (number <= design.Colors.Count))
                counts[number]++;

        var best = 0;

        for (var number = 1; number < counts.Length; number++)
            if (counts[number] > counts[best])
                best = number;

        if (best == 0)
            return DefaultField;

        var color = design.Colors[best - 1];

        return new Color((int)(color.R * FIELD_SHADE), (int)(color.G * FIELD_SHADE), (int)(color.B * FIELD_SHADE));
    }

    /// <summary>
    ///     True when image pixel (<paramref name="x" />, <paramref name="y" />) is on the post: the plinth, the pole (with a
    ///     pixel of slack each side), the crossbar or the cloth. The air beside the pole is not, so floor clicks there land.
    /// </summary>
    public static bool Hits(int x, int y)
    {
        if ((x < 0) || (y < 0) || (x >= WIDTH) || (y >= HEIGHT))
            return false;

        if (InPlinth(x, y) || InCloth(x, y) || InBar(x, y))
            return true;

        return (x >= (POLE_LEFT - 1)) && (x <= (POLE_LEFT + 4)) && (y >= 1) && (y <= PLINTH_Y);
    }

    /// <summary>The post, row-major, <see cref="WIDTH" /> × <see cref="HEIGHT" />; everything else is transparent.</summary>
    public static Color[] Paint(GuildEmblemDesign design)
    {
        var image = new Color[WIDTH * HEIGHT];

        PaintPlinth(image);
        PaintPole(image);
        PaintBar(image);
        PaintCloth(image, design);

        return image;
    }

    private static int BarY(int x) => BAR_LEFT_Y - ((x - CLOTH_LEFT) / 2);

    private static int ClothTop(int x) => BarY(x) + 2;

    private static bool InBar(int x, int y)
        => (x >= (CLOTH_LEFT - 2)) && (x < (CLOTH_LEFT + CLOTH_WIDTH + 2)) && (y >= (BarY(x) - 1)) && (y <= (BarY(x) + 1));

    /// <summary>The cloth in its own plane: local column c (0 at the left edge) and row r (0 under the crossbar).</summary>
    private static bool InClothLocal(int c, int r)
    {
        if ((c < 0) || (c >= CLOTH_WIDTH) || (r < 0))
            return false;

        if (r < BODY_ROWS)
            return true;

        var tailRow = r - BODY_ROWS;

        if (tailRow >= TAIL_ROWS)
            return false;

        //the swallowtail notch widens by a pixel on each side per row, from the centre line
        return MathF.Abs(c - ((CLOTH_WIDTH / 2f) - 0.5f)) > (tailRow + 0.5f);
    }

    private static bool InCloth(int x, int y) => InClothLocal(x - CLOTH_LEFT, y - ClothTop(x));

    private static bool InPlinth(int x, int y)
    {
        var dx = MathF.Abs(x - FOOT_X + 0.5f) / PLINTH_HALF_WIDTH;
        var top = PLINTH_Y + ((dx - 1) * PLINTH_HALF_HEIGHT);
        var bottom = PLINTH_Y + ((1 - dx) * PLINTH_HALF_HEIGHT) + PLINTH_DEPTH;

        return (dx <= 1) && (y >= top) && (y <= bottom);
    }

    private static Color Shade(Color color, float factor)
        => new(
            (int)Math.Clamp(MathF.Round(color.R * factor), 0, 255),
            (int)Math.Clamp(MathF.Round(color.G * factor), 0, 255),
            (int)Math.Clamp(MathF.Round(color.B * factor), 0, 255));

    private static void PaintPlinth(Color[] image)
    {
        for (var y = 0; y < HEIGHT; y++)
            for (var x = 0; x < WIDTH; x++)
            {
                if (!InPlinth(x, y))
                    continue;

                var dx = MathF.Abs(x - FOOT_X + 0.5f) / PLINTH_HALF_WIDTH;
                var topFaceBottom = PLINTH_Y + ((1 - dx) * PLINTH_HALF_HEIGHT);
                Color color;

                if (!InPlinth(x - 1, y) || !InPlinth(x + 1, y) || !InPlinth(x, y - 1) || !InPlinth(x, y + 1))
                    color = StoneOutline;
                else if (y <= topFaceBottom)
                    //the top face, a little lighter toward the upper left, with a faint checker for texture
                    color = Shade(
                        ((x + y) % 2) == 0 ? StoneLight : Stone,
                        1.04f - (0.08f * ((x - (FOOT_X - PLINTH_HALF_WIDTH)) / (2f * PLINTH_HALF_WIDTH))));
                else
                    color = x < FOOT_X ? StoneLeft : StoneRight;

                Set(image, x, y, color);
            }
    }

    private static void PaintPole(Color[] image)
    {
        for (var y = POLE_TOP; y < PLINTH_Y; y++)
        {
            //a little grain: every few rows the middle of the pole darkens a shade
            var grain = (y % 7) == 3 ? 0.88f : 1f;

            Set(image, POLE_LEFT, y, WoodOutline);
            Set(image, POLE_LEFT + 1, y, Shade(WoodLight, grain));
            Set(image, POLE_LEFT + 2, y, Shade(Wood, grain));
            Set(image, POLE_LEFT + 3, y, WoodDark);
            Set(image, POLE_LEFT + 4, y, WoodOutline);
        }

        //the finial: a shaded gold ball sitting on a collar
        const int CX = FOOT_X;
        const int CY = 5;

        for (var y = CY - 4; y <= (CY + 3); y++)
            for (var x = CX - 4; x <= (CX + 3); x++)
            {
                var d = MathF.Sqrt(MathF.Pow(x - CX + 0.5f, 2) + MathF.Pow(y - CY + 0.5f, 2));

                if (d > 3.6f)
                    continue;

                var light = ((x - CX) + (y - CY)) / 6f;
                Color color;

                if (d > 2.8f)
                    color = GoldOutline;
                else if (light < -0.5f)
                    color = GoldLight;
                else if (light < 0.2f)
                    color = Gold;
                else
                    color = GoldDark;

                Set(image, x, y, color);
            }
    }

    private static void PaintBar(Color[] image)
    {
        for (var x = CLOTH_LEFT - 2; x < (CLOTH_LEFT + CLOTH_WIDTH + 2); x++)
        {
            var y = BarY(x);
            var cap = (x < CLOTH_LEFT) || (x >= (CLOTH_LEFT + CLOTH_WIDTH));

            Set(image, x, y - 1, cap ? GoldLight : WoodLight);
            Set(image, x, y, cap ? Gold : Wood);
            Set(image, x, y + 1, cap ? GoldOutline : WoodOutline);
        }
    }

    private static void PaintCloth(Color[] image, GuildEmblemDesign design)
    {
        var field = FieldColor(design);
        var border = Shade(field, 0.4f);

        for (var x = CLOTH_LEFT; x < (CLOTH_LEFT + CLOTH_WIDTH); x++)
        {
            var c = x - CLOTH_LEFT;
            var top = ClothTop(x);

            for (var r = 0; r < (BODY_ROWS + TAIL_ROWS); r++)
            {
                if (!InClothLocal(c, r))
                    continue;

                var y = top + r;

                if (!InClothLocal(c - 1, r) || !InClothLocal(c + 1, r) || !InClothLocal(c, r - 1) || !InClothLocal(c, r + 1))
                {
                    Set(image, x, y, border);

                    continue;
                }

                var color = field;
                var ec = c - BORDER - MARGIN;
                var er = r - BORDER - MARGIN;

                if ((ec >= 0) && (er >= 0) && (ec < EMBLEM_SIZE) && (er < EMBLEM_SIZE))
                {
                    var index = ((er / SCALE) * GuildEmblemProtocol.SIZE) + (ec / SCALE);

                    if ((index < design.Pixels.Length) && design.Pixels[index] is var number and > 0 && (number <= design.Colors.Count))
                    {
                        var paint = design.Colors[number - 1];
                        color = new Color(paint.R, paint.G, paint.B);
                    }
                }

                Set(image, x, y, Shade(color, ClothLight(c, r)));
            }
        }
    }

    /// <summary>
    ///     How lit the cloth is at local (c, r): brighter on the left, two soft folds, the crossbar's shadow along the top
    ///     and the tails darkening toward their tips.
    /// </summary>
    public static float ClothLight(int c, int r)
    {
        var light = 1.1f - (0.22f * c / (CLOTH_WIDTH - 1));

        if (c is 9 or 19)
            light += 0.06f;
        else if (c is 11 or 21)
            light -= 0.1f;

        if (r <= 2)
            light -= 0.18f;

        if (r >= BODY_ROWS)
            light -= 0.05f * (r - BODY_ROWS + 1);

        return light;
    }

    private static void Set(Color[] image, int x, int y, Color color)
    {
        if ((x >= 0) && (y >= 0) && (x < WIDTH) && (y < HEIGHT))
            image[(y * WIDTH) + x] = color;
    }
}
