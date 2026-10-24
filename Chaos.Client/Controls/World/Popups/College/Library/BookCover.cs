using System.Globalization;
using Chaos.Client.Controls.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Controls.World.Popups.College.Library;

/// <summary>Draws the front book's cover: a fetched Picture or Drawing texture, or a plain spine-coloured one.</summary>
public static class BookCover
{
    public const int WIDTH = 120;
    public const int HEIGHT = 170;

    private const int TEXT_MARGIN = 8;
    private const int TITLE_TOP_PAD = 24;
    private const int MAX_PLAIN_TITLE_LINES = 3;

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

    public static void DrawTexture(SpriteBatch spriteBatch, Texture2D cover, Rectangle rect, float progress)
    {
        var turned = TurnRect(FitTexture(cover.Width, cover.Height, rect), progress);

        if (turned.Width <= 0)
            return;

        spriteBatch.Draw(cover, turned, new Rectangle(0, 0, cover.Width, cover.Height), Color.White);
    }

    public static void DrawPlain(
        SpriteBatch spriteBatch,
        string title,
        string author,
        Color fill,
        Rectangle rect,
        float progress)
    {
        var turned = TurnRect(rect, progress);

        if (turned.Width <= 0)
            return;

        UIElement.DrawRect(spriteBatch, turned, fill);

        if (turned.Width < 2 * TEXT_MARGIN + TextRenderer.CHAR_HEIGHT)
            return;

        var x = turned.X + TEXT_MARGIN;
        var width = turned.Width - (2 * TEXT_MARGIN);
        var y = turned.Y + TITLE_TOP_PAD;

        foreach (var line in TextRenderer.WrapLines(title, width)
                                          .Take(MAX_PLAIN_TITLE_LINES))
        {
            TextRenderer.DrawText(
                spriteBatch,
                new Vector2(x + ((width - TextRenderer.MeasureWidth(line)) / 2), y),
                line,
                Color.White,
                false);

            y += TextRenderer.CHAR_HEIGHT;
        }

        var byline = string.IsNullOrEmpty(author) ? string.Empty : $"by {author}";

        while ((byline.Length > 0) && (TextRenderer.MeasureWidth(byline) > width))
            byline = byline[..^1];

        if (byline.Length > 0)
            TextRenderer.DrawText(
                spriteBatch,
                new Vector2(x + ((width - TextRenderer.MeasureWidth(byline)) / 2), turned.Bottom - TEXT_MARGIN - TextRenderer.CHAR_HEIGHT),
                byline,
                LegendColors.Gray,
                false);
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
