using System.Globalization;
using Chaos.Client.Controls.Components;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Library;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Controls.World.Popups.College.Library;

/// <summary>Draws the front book's cover: a fetched Picture or Drawing texture, or a plain spine-coloured one.</summary>
public static class BookCover
{
    public const int WIDTH = 120;
    public const int HEIGHT = 170;

    /// <summary>The cover's box: half the full cover size, centred on the window.</summary>
    public static Rectangle CoverRect(int centerX, int top) => new(centerX - (WIDTH / 2), top, WIDTH, HEIGHT);

    /// <summary>The cover narrowed by the turn animation, kept centred; zero wide at the start of the turn.</summary>
    public static Rectangle TurnRect(Rectangle full, float progress)
    {
        var width = Math.Clamp((int)MathF.Round(full.Width * progress), 0, full.Width);

        return new Rectangle(full.X + ((full.Width - width) / 2), full.Y, width, full.Height);
    }

    public static string FormatDate(uint approvedUnix)
        => DateTimeOffset.FromUnixTimeSeconds(approvedUnix)
                        .UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Largest rect with the texture's aspect ratio that fits inside the box, centred.</summary>
    public static Rectangle FitTexture(int textureWidth, int textureHeight, Rectangle box)
    {
        if ((textureWidth <= 0) || (textureHeight <= 0))
            return box;

        var scale = Math.Min(box.Width / (float)textureWidth, box.Height / (float)textureHeight);
        var width = Math.Max(1, (int)MathF.Round(textureWidth * scale));
        var height = Math.Max(1, (int)MathF.Round(textureHeight * scale));

        return new Rectangle(box.X + ((box.Width - width) / 2), box.Y + ((box.Height - height) / 2), width, height);
    }

    /// <summary>Builds the front cover texture: plain, or the art in the author's chosen style.</summary>
    public static Texture2D Build(
        GraphicsDevice device,
        string title,
        string author,
        Color fill,
        Texture2D? art,
        LibraryCoverStyle style)
    {
        Color[] pixels;

        if (art is null)
            pixels = BookArt.PaintPlainCover(title, author, fill, FontGlyphBits.Instance);
        else
        {
            using var source = new PixelBufferScope(art);
            var artPixels = source.AsSpan().ToArray();

            pixels = style == LibraryCoverStyle.Full
                ? BookArt.PaintFullCover(title, author, fill, artPixels, art.Width, art.Height, FontGlyphBits.Instance)
                : BookArt.PaintFramedCover(title, author, fill, artPixels, art.Width, art.Height, FontGlyphBits.Instance);
        }

        var texture = new Texture2D(device, WIDTH, HEIGHT);
        texture.SetData(pixels);

        return texture;
    }

    public static void DrawTexture(SpriteBatch spriteBatch, Texture2D cover, Rectangle rect, float progress)
    {
        var turned = TurnRect(FitTexture(cover.Width, cover.Height, rect), progress);

        if (turned.Width <= 0)
            return;

        spriteBatch.Draw(cover, turned, new Rectangle(0, 0, cover.Width, cover.Height), Color.White);
    }

    public static void DrawLoading(SpriteBatch spriteBatch, Rectangle rect, float progress)
    {
        var turned = TurnRect(rect, progress);

        if (turned.Width <= 0)
            return;

        UIElement.DrawRect(spriteBatch, turned, Color.Black);

        const string loading = "Loading...";
        var x = turned.X + ((turned.Width - TextRenderer.MeasureWidth(loading)) / 2);

        TextRenderer.DrawText(
            spriteBatch,
            new Vector2(x, turned.Y + ((turned.Height - TextRenderer.CHAR_HEIGHT) / 2)),
            loading,
            LegendColors.Gray,
            false);
    }
}
