#region
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Repaints the green wall banner baked into the Town Hall's walls (<see cref="TownWallBannerArt" />) with a town's
///     approved banner: the green cloth takes the banner's main colour, keeping the baked folds and light, and the 11 × 11
///     emblem replaces the castle crest at 2×. The gold edging, rod and hem stay as painted. Works in the wall texture's
///     own space (112 × 112, one 28-column slice per wall tile), then lays a wall tile's slices onto its wall faces the
///     way Unora's wall-art script does (Tools/ArenaArt/preview/retex.py with --face-from-floor): face column c of a
///     tile's left piece starts at tile-local row 13 + c / 2 - 87, its right piece at 26 - c / 2 - 87, and texture row v
///     sits v rows below that. Pure pixels, so it is tested without a graphics device.
/// </summary>
public static class TownWallBannerPainter
{
    /// <summary>Each wall tile's image: both faces (56 px), from tile-local row <see cref="CELL_TOP" /> down.</summary>
    public const int CELL_WIDTH = 56;
    public const int CELL_HEIGHT = 64;
    public const int CELL_TOP = -72;

    /// <summary>Where the emblem goes in texture space: over the baked crest, inside the gold edging.</summary>
    public const int EMBLEM_U = 45;
    public const int EMBLEM_V = 12;

    private const int SLICES = 4;
    private const int FACE_HEIGHT = 88;
    private const int SLICE_WIDTH = 28;
    private const float CLOTH_SHADE = 0.85f;

    private static readonly float GreenMeanLight = MeanGreenLight();

    /// <summary>The banner's main (most used) colour, a little darkened for cloth; a dark brown when it has no paint.</summary>
    public static Color ClothColor(GuildEmblemDesign design)
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
            return new Color(70, 40, 30);

        var color = design.Colors[best - 1];

        return new Color((int)(color.R * CLOTH_SHADE), (int)(color.G * CLOTH_SHADE), (int)(color.B * CLOTH_SHADE));
    }

    /// <summary>The wall texture slice (0-3) on tile (x, y)'s left (side 0) or right (side 1) piece.</summary>
    public static int Slice(int x, int y, int side) => (((x - y + side) % SLICES) + SLICES) % SLICES;

    /// <summary>Tile-local row of texture row 0 in face column <paramref name="column" /> of a side's piece.</summary>
    public static int FaceTop(int side, int column) => (side == 0 ? 13 + (column / 2) : 26 - (column / 2)) - (FACE_HEIGHT - 1);

    /// <summary>
    ///     The repainted banner in wall-texture space, row-major <see cref="TownWallBannerArt.TEXTURE_SIZE" /> square:
    ///     transparent wherever the baked wall art should show through unchanged.
    /// </summary>
    public static Color[] Texels(GuildEmblemDesign design)
    {
        const int SIZE = TownWallBannerArt.TEXTURE_SIZE;
        var texels = new Color[SIZE * SIZE];
        var cloth = ClothColor(design);
        var art = TownWallBannerArt.Rgba;

        for (var row = 0; row < TownWallBannerArt.HEIGHT; row++)
            for (var col = 0; col < TownWallBannerArt.WIDTH; col++)
            {
                var i = ((row * TownWallBannerArt.WIDTH) + col) * 4;

                if (art[i + 3] == 0)
                    continue;

                var baked = new Color(art[i], art[i + 1], art[i + 2]);
                var u = TownWallBannerArt.U0 + col;
                var v = TownWallBannerArt.V0 + row;
                var green = IsGreen(baked);
                var eu = u - EMBLEM_U;
                var ev = v - EMBLEM_V;
                var inEmblem = (eu >= 0) && (ev >= 0) && (eu < (GuildEmblemProtocol.SIZE * 2)) && (ev < (GuildEmblemProtocol.SIZE * 2));

                if (!green && !inEmblem)
                    continue;

                //the baked cloth's light and folds, carried over onto the new colours
                var light = green ? Light(baked) / GreenMeanLight : 1f;
                var color = cloth;

                if (inEmblem)
                {
                    var index = ((ev / 2) * GuildEmblemProtocol.SIZE) + (eu / 2);

                    if ((index < design.Pixels.Length) && design.Pixels[index] is var number and > 0 && (number <= design.Colors.Count))
                    {
                        var paint = design.Colors[number - 1];
                        color = new Color(paint.R, paint.G, paint.B);
                    }
                }

                texels[(v * SIZE) + u] = Shade(color, light);
            }

        return texels;
    }

    /// <summary>
    ///     Tile (x, y)'s share of <paramref name="texels" />, laid onto the faces that have a wall piece, row-major
    ///     <see cref="CELL_WIDTH" /> × <see cref="CELL_HEIGHT" />, top row at tile-local <see cref="CELL_TOP" />.
    /// </summary>
    public static Color[] Cell(Color[] texels, int x, int y, bool leftWall, bool rightWall)
    {
        const int SIZE = TownWallBannerArt.TEXTURE_SIZE;
        var image = new Color[CELL_WIDTH * CELL_HEIGHT];

        for (var side = 0; side < 2; side++)
        {
            if ((side == 0) ? !leftWall : !rightWall)
                continue;

            var slice = Slice(x, y, side);

            for (var column = 0; column < SLICE_WIDTH; column++)
            {
                var u = ((slice * SLICE_WIDTH) + column) % SIZE;
                var top = FaceTop(side, column) - CELL_TOP;

                for (var v = 0; v < SIZE; v++)
                {
                    var texel = texels[(v * SIZE) + u];

                    if (texel.A == 0)
                        continue;

                    var row = top + v;

                    if ((row >= 0) && (row < CELL_HEIGHT))
                        image[(row * CELL_WIDTH) + (side * SLICE_WIDTH) + column] = texel;
                }
            }
        }

        return image;
    }

    /// <summary>True when the tile shows any of the banner: its pieces' slices cross the banner's columns.</summary>
    public static bool Touches(Color[] cell)
    {
        foreach (var pixel in cell)
            if (pixel.A != 0)
                return true;

        return false;
    }

    private static bool IsGreen(Color c) => (c.G > (c.R + 15)) && (c.G > (c.B + 15));

    private static float Light(Color c) => (0.3f * c.R) + (0.59f * c.G) + (0.11f * c.B);

    private static float MeanGreenLight()
    {
        var art = TownWallBannerArt.Rgba;
        var sum = 0f;
        var count = 0;

        for (var i = 0; i < art.Length; i += 4)
        {
            var c = new Color(art[i], art[i + 1], art[i + 2]);

            if ((art[i + 3] != 0) && IsGreen(c))
            {
                sum += Light(c);
                count++;
            }
        }

        return count == 0 ? 1f : sum / count;
    }

    private static Color Shade(Color color, float factor)
        => new(
            (int)Math.Clamp(MathF.Round(color.R * factor), 0, 255),
            (int)Math.Clamp(MathF.Round(color.G * factor), 0, 255),
            (int)Math.Clamp(MathF.Round(color.B * factor), 0, 255));
}
