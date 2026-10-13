#region
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>The College wall images (frame and board) and their slanted strips, as premultiplied colours.</summary>
public static class WallDecalPainter
{
    public static readonly Color FrameOuter = new(120, 80, 44);
    public static readonly Color FrameInner = new(166, 118, 68);
    public static readonly Color FrameLine = new(58, 36, 20);
    public static readonly Color PaneFill = new Color(40, 120, 190) * 0.55f;
    public static readonly Color PaneEdge = new Color(150, 220, 255) * 0.85f;

    /// <summary>A 96 x 72 drawing inside its wooden frame, 112 x 88.</summary>
    public static Color[] Framed(IReadOnlyList<Color> palette, byte[] pixels)
    {
        const int WIDTH = WallDecalGeometry.FRAME_IMAGE_WIDTH;
        const int HEIGHT = WallDecalGeometry.PICTURE_HEIGHT;
        const int BORDER = WallDecalGeometry.FRAME_BORDER;
        var image = new Color[WIDTH * HEIGHT];

        for (var y = 0; y < HEIGHT; y++)
            for (var x = 0; x < WIDTH; x++)
            {
                var edge = Math.Min(Math.Min(x, y), Math.Min(WIDTH - 1 - x, HEIGHT - 1 - y));

                image[(y * WIDTH) + x] = edge switch
                {
                    < 4 => FrameOuter,
                    < 7 => FrameInner,
                    < BORDER => FrameLine,
                    _ => palette[pixels[((y - BORDER) * CollegeProtocol.DRAWING_WIDTH) + (x - BORDER)]]
                };
            }

        return image;
    }

    /// <summary>The 168 x 88 board: a bright edge round the 160 x 80 pane; swatch 0 (or no drawing) shows the pane.</summary>
    public static Color[] Board(IReadOnlyList<Color>? palette, byte[]? pixels)
    {
        const int WIDTH = WallDecalGeometry.BOARD_IMAGE_WIDTH;
        const int HEIGHT = WallDecalGeometry.PICTURE_HEIGHT;
        const int EDGE = WallDecalGeometry.PANE_EDGE;
        var image = new Color[WIDTH * HEIGHT];

        for (var y = 0; y < HEIGHT; y++)
            for (var x = 0; x < WIDTH; x++)
            {
                var edge = Math.Min(Math.Min(x, y), Math.Min(WIDTH - 1 - x, HEIGHT - 1 - y));

                if (edge < EDGE)
                {
                    image[(y * WIDTH) + x] = PaneEdge;

                    continue;
                }

                var swatch = pixels?[((y - EDGE) * CollegeProtocol.BOARD_WIDTH) + (x - EDGE)] ?? 0;
                image[(y * WIDTH) + x] = (swatch == 0) || (palette is null) ? PaneFill : palette[swatch];
            }

        return image;
    }

    /// <summary>Cuts an image <paramref name="spot" />.Width wide into one slanted strip per wall tile, left to right on screen.</summary>
    public static Color[][] Strips(Color[] image, int height, WallSpot spot)
    {
        const int FACE = MirrorGeometry.FACE_WIDTH;
        var stripHeight = WallDecalGeometry.StripHeight(height);
        var strips = new Color[spot.Tiles][];

        for (var s = 0; s < spot.Tiles; s++)
        {
            var strip = new Color[FACE * stripHeight];

            for (var column = 0; column < FACE; column++)
                for (var row = 0; row < height; row++)
                    strip[(WallDecalGeometry.StripRow(spot.Side, column, row) * FACE) + column] = image[(row * spot.Width) + (s * FACE) + column];

            strips[s] = strip;
        }

        return strips;
    }
}
