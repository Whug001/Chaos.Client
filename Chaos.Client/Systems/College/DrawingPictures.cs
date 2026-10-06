using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using SkiaSharp;

namespace Chaos.Client.Systems.College;

/// <summary>
///     A drawing as a picture for a writing piece: a 384 x 288 PNG, each canvas pixel a 4 x 4 block. Reading one back
///     recognises only pictures made that way (exact size, solid blocks, at most 32 colours).
/// </summary>
public static class DrawingPictures
{
    private const int ZOOM = CollegeProtocol.DRAWING_ZOOM;
    public const int WIDTH = PixelDrawing.WIDTH * ZOOM;
    public const int HEIGHT = PixelDrawing.HEIGHT * ZOOM;

    public static PreparedPicture ToPicture(PixelDrawing drawing)
    {
        var colours = DrawingTextures.ToColors(drawing.Palette, drawing.Pixels, ZOOM);

        using var bitmap = new SKBitmap(WIDTH, HEIGHT, SKColorType.Rgba8888, SKAlphaType.Opaque);
        bitmap.Pixels = colours.Select(c => new SKColor(c.R, c.G, c.B)).ToArray();

        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        var bytes = png.ToArray();

        return new PreparedPicture(bytes, PicturePrep.Hash(bytes), WIDTH, HEIGHT);
    }

    /// <summary>
    ///     The drawing a picture was made from, or null. The palette starts with the background, which the eraser paints:
    ///     the parchment if the picture has it, otherwise its most-used colour. Then come the other colours as first seen,
    ///     then unused defaults.
    /// </summary>
    public static PixelDrawing? TryRead(byte[] bytes)
    {
        using var codec = SKCodec.Create(new SKMemoryStream(bytes));

        if (codec is null || (codec.Info.Width != WIDTH) || (codec.Info.Height != HEIGHT))
            return null;

        using var bitmap = SKBitmap.Decode(codec);

        if (bitmap is null || (bitmap.Width != WIDTH) || (bitmap.Height != HEIGHT))
            return null;

        var source = bitmap.Pixels;
        var colours = new List<Color>();
        var swatches = new Dictionary<SKColor, byte>();
        var pixels = new byte[PixelDrawing.WIDTH * PixelDrawing.HEIGHT];

        for (var y = 0; y < PixelDrawing.HEIGHT; y++)
            for (var x = 0; x < PixelDrawing.WIDTH; x++)
            {
                var colour = source[(y * ZOOM * WIDTH) + (x * ZOOM)];

                if (colour.Alpha != 255)
                    return null;

                for (var dy = 0; dy < ZOOM; dy++)
                    for (var dx = 0; dx < ZOOM; dx++)
                        if (source[(((y * ZOOM) + dy) * WIDTH) + (x * ZOOM) + dx] != colour)
                            return null;

                if (!swatches.TryGetValue(colour, out var swatch))
                {
                    if (colours.Count == PixelDrawing.COLOURS)
                        return null;

                    swatch = (byte)colours.Count;
                    swatches[colour] = swatch;
                    colours.Add(new Color(colour.Red, colour.Green, colour.Blue));
                }

                pixels[(y * PixelDrawing.WIDTH) + x] = swatch;
            }

        MoveToFront(colours, pixels, Background(colours, pixels));

        foreach (var fallback in ArtPalette.Default)
            if ((colours.Count < PixelDrawing.COLOURS) && !colours.Contains(fallback))
                colours.Add(fallback);

        var palette = new byte[PixelDrawing.COLOURS * 3];

        for (var i = 0; i < colours.Count; i++)
        {
            palette[i * 3] = colours[i].R;
            palette[(i * 3) + 1] = colours[i].G;
            palette[(i * 3) + 2] = colours[i].B;
        }

        return PixelDrawing.From(palette, pixels);
    }

    private static int Background(List<Color> colours, byte[] pixels)
    {
        var parchment = colours.IndexOf(ArtPalette.Default[0]);

        if (parchment >= 0)
            return parchment;

        var counts = new int[colours.Count];

        foreach (var pixel in pixels)
            counts[pixel]++;

        var most = 0;

        for (var i = 1; i < counts.Length; i++)
            if (counts[i] > counts[most])
                most = i;

        return most;
    }

    private static void MoveToFront(List<Color> colours, byte[] pixels, int swatch)
    {
        if (swatch == 0)
            return;

        var colour = colours[swatch];
        colours.RemoveAt(swatch);
        colours.Insert(0, colour);

        for (var i = 0; i < pixels.Length; i++)
            pixels[i] = pixels[i] == swatch ? (byte)0 : pixels[i] < swatch ? (byte)(pixels[i] + 1) : pixels[i];
    }
}
