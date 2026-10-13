using Chaos.Client.Rendering;
using Chaos.Client.ViewModel.College;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Systems.College;

/// <summary>Turns a drawing into colours, each canvas pixel a zoom x zoom block, and into a texture the caller owns.</summary>
public static class DrawingTextures
{
    public static Color[] ToColors(IReadOnlyList<Color> palette, byte[] pixels, int zoom)
        => ToColors(palette, pixels, zoom, PixelDrawing.WIDTH, PixelDrawing.HEIGHT);

    public static Color[] ToColors(IReadOnlyList<Color> palette, byte[] pixels, int zoom, int width, int height)
        => ToColors(palette, pixels, zoom, width, height, new Color[width * zoom * height * zoom]);

    /// <summary>Fills <paramref name="colours" />, which must hold the whole zoomed drawing, and returns it.</summary>
    public static Color[] ToColors(IReadOnlyList<Color> palette, byte[] pixels, int zoom, Color[] colours)
        => ToColors(palette, pixels, zoom, PixelDrawing.WIDTH, PixelDrawing.HEIGHT, colours);

    public static Color[] ToColors(
        IReadOnlyList<Color> palette,
        byte[] pixels,
        int zoom,
        int width,
        int height,
        Color[] colours)
    {
        var rowWidth = width * zoom;

        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var c = palette[pixels[(y * width) + x]];
                var colour = new Color(c.R, c.G, c.B);

                for (var dy = 0; dy < zoom; dy++)
                    Array.Fill(colours, colour, (((y * zoom) + dy) * rowWidth) + (x * zoom), zoom);
            }

        return colours;
    }

    public static Texture2D Build(PixelDrawing drawing, int zoom)
    {
        var texture = new Texture2D(TextureConverter.Device, drawing.Width * zoom, drawing.Height * zoom);
        texture.SetData(ToColors(drawing.Palette, drawing.Pixels, zoom, drawing.Width, drawing.Height));

        return texture;
    }

    /// <summary>A live board at <paramref name="zoom" />: swatch 0 shows <see cref="BoardState.Pane" />.</summary>
    public static Texture2D BuildBoard(PixelDrawing? drawing, int zoom)
    {
        drawing ??= BoardState.NewBoard();
        var palette = drawing.Palette.ToArray();
        palette[0] = BoardState.Pane;
        var texture = new Texture2D(TextureConverter.Device, drawing.Width * zoom, drawing.Height * zoom);
        texture.SetData(ToColors(palette, drawing.Pixels, zoom, drawing.Width, drawing.Height));

        return texture;
    }
}
