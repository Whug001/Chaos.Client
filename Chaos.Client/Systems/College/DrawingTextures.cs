using Chaos.Client.Rendering;
using Chaos.Client.ViewModel.College;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Systems.College;

/// <summary>Turns a drawing into colours, each canvas pixel a zoom x zoom block, and into a texture the caller owns.</summary>
public static class DrawingTextures
{
    public static Color[] ToColors(IReadOnlyList<Color> palette, byte[] pixels, int zoom)
    {
        var width = PixelDrawing.WIDTH * zoom;
        var colours = new Color[width * PixelDrawing.HEIGHT * zoom];

        for (var y = 0; y < PixelDrawing.HEIGHT; y++)
            for (var x = 0; x < PixelDrawing.WIDTH; x++)
            {
                var c = palette[pixels[(y * PixelDrawing.WIDTH) + x]];
                var colour = new Color(c.R, c.G, c.B);

                for (var dy = 0; dy < zoom; dy++)
                    Array.Fill(colours, colour, (((y * zoom) + dy) * width) + (x * zoom), zoom);
            }

        return colours;
    }

    public static Texture2D Build(PixelDrawing drawing, int zoom)
    {
        var texture = new Texture2D(TextureConverter.Device, PixelDrawing.WIDTH * zoom, PixelDrawing.HEIGHT * zoom);
        texture.SetData(ToColors(drawing.Palette, drawing.Pixels, zoom));

        return texture;
    }
}
