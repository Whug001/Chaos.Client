#region
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Paints a town banner post: a wooden pole with a gold finial and a crossbar, and a swallowtail cloth hanging from
///     the crossbar with the town's 11 × 11 banner at 2×. Works on a plain pixel buffer, so it is tested without a graphics
///     device. The pole's foot pixel (<see cref="FOOT_X" />, <see cref="FOOT_Y" />) stands on the tile's floor centre.
/// </summary>
public static class BannerPostPainter
{
    public const int WIDTH = 40;
    public const int HEIGHT = 96;
    public const int FOOT_X = 20;
    public const int FOOT_Y = 95;
    public const int SCALE = 2;

    private const int POLE_LEFT = FOOT_X - 1;
    private const int POLE_TOP = 6;
    private const int BAR_TOP = 9;
    private const int CLOTH_LEFT = 5;
    private const int CLOTH_WIDTH = 30;
    private const int CLOTH_TOP = 12;
    private const int BORDER = 1;
    private const int MARGIN = 3;
    private const int EMBLEM_SIZE = GuildEmblemProtocol.SIZE * SCALE;
    private const int CLOTH_BODY_HEIGHT = (2 * (BORDER + MARGIN)) + EMBLEM_SIZE;
    private const int TAIL_HEIGHT = 8;
    private const float FIELD_SHADE = 0.45f;

    private static readonly Color Outline = new(58, 36, 20);
    private static readonly Color Wood = new(120, 78, 42);
    private static readonly Color WoodLight = new(160, 110, 62);
    private static readonly Color Gold = new(212, 175, 55);
    private static readonly Color ClothBorder = new(30, 20, 16);
    private static readonly Color DefaultField = new(70, 40, 30);

    /// <summary>The top-left pixel of the 2× emblem inside the image.</summary>
    public static Point EmblemOrigin { get; } = new(CLOTH_LEFT + BORDER + MARGIN, CLOTH_TOP + BORDER + MARGIN);

    /// <summary>The post, row-major, <see cref="WIDTH" /> × <see cref="HEIGHT" />; everything else is transparent.</summary>
    public static Color[] Paint(GuildEmblemDesign design)
    {
        var image = new Color[WIDTH * HEIGHT];

        PaintPole(image);
        PaintCloth(image, design);

        return image;
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
    ///     True when image pixel (<paramref name="x" />, <paramref name="y" />) is on the post: the pole (with a pixel of
    ///     slack each side) or the cloth and crossbar. The air beside the pole is not, so floor clicks there still land.
    /// </summary>
    public static bool Hits(int x, int y)
    {
        if ((x < 0) || (y < 0) || (x >= WIDTH) || (y >= HEIGHT))
            return false;

        if ((y >= BAR_TOP) && InClothArea(x, y))
            return true;

        return (Math.Abs(x - FOOT_X) <= 2) && (y >= 1);
    }

    private static bool InClothArea(int x, int y)
        => (x >= CLOTH_LEFT) && (x < (CLOTH_LEFT + CLOTH_WIDTH)) && (y < (CLOTH_TOP + CLOTH_BODY_HEIGHT + TAIL_HEIGHT));

    private static void PaintPole(Color[] image)
    {
        //finial: one pixel at the tip, then the pole's width
        Set(image, FOOT_X, 1, Gold);

        for (var y = 2; y < POLE_TOP; y++)
            for (var x = POLE_LEFT; x < (POLE_LEFT + 3); x++)
                Set(image, x, y, Gold);

        for (var y = POLE_TOP; y <= FOOT_Y; y++)
        {
            Set(image, POLE_LEFT, y, Outline);
            Set(image, POLE_LEFT + 1, y, Wood);
            Set(image, POLE_LEFT + 2, y, WoodLight);
        }

        for (var x = CLOTH_LEFT; x < (CLOTH_LEFT + CLOTH_WIDTH); x++)
        {
            Set(image, x, BAR_TOP, WoodLight);
            Set(image, x, BAR_TOP + 1, Wood);
            Set(image, x, BAR_TOP + 2, Outline);
        }
    }

    private static void PaintCloth(Color[] image, GuildEmblemDesign design)
    {
        var field = FieldColor(design);
        var origin = EmblemOrigin;

        for (var y = CLOTH_TOP; y < (CLOTH_TOP + CLOTH_BODY_HEIGHT + TAIL_HEIGHT); y++)
            for (var x = CLOTH_LEFT; x < (CLOTH_LEFT + CLOTH_WIDTH); x++)
            {
                if (!InCloth(x, y))
                    continue;

                if (!InCloth(x - 1, y) || !InCloth(x + 1, y) || !InCloth(x, y - 1) || !InCloth(x, y + 1))
                {
                    Set(image, x, y, ClothBorder);

                    continue;
                }

                var ex = x - origin.X;
                var ey = y - origin.Y;
                var color = field;

                if ((ex >= 0) && (ey >= 0) && (ex < EMBLEM_SIZE) && (ey < EMBLEM_SIZE))
                {
                    var index = ((ey / SCALE) * GuildEmblemProtocol.SIZE) + (ex / SCALE);

                    if ((index < design.Pixels.Length) && design.Pixels[index] is var number and > 0
                        && (number <= design.Colors.Count))
                    {
                        var paint = design.Colors[number - 1];
                        color = new Color(paint.R, paint.G, paint.B);
                    }
                }

                Set(image, x, y, color);
            }
    }

    /// <summary>The cloth's shape: a rectangle with a swallowtail notch cut up into its bottom, centred.</summary>
    private static bool InCloth(int x, int y)
    {
        if ((x < CLOTH_LEFT) || (x >= (CLOTH_LEFT + CLOTH_WIDTH)) || (y < CLOTH_TOP))
            return false;

        var tailRow = y - (CLOTH_TOP + CLOTH_BODY_HEIGHT);

        if (tailRow < 0)
            return true;

        if (tailRow >= TAIL_HEIGHT)
            return false;

        //the notch widens by a pixel on each side per row, from the centre line between the middle two columns
        var centre = CLOTH_LEFT + (CLOTH_WIDTH / 2f) - 0.5f;

        return MathF.Abs(x - centre) > (tailRow + 0.5f);
    }

    private static void Set(Color[] image, int x, int y, Color color) => image[(y * WIDTH) + x] = color;
}
