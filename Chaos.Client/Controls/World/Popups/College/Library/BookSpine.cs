using Chaos.Client.Controls.Components;
using Chaos.Client.ViewModel.College.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Controls.World.Popups.College.Library;

/// <summary>Draws one library spine: the spine-coloured fill with a darker bottom edge and the title down it.</summary>
public static class BookSpine
{
    public const int WIDTH = 28;
    public const int HEIGHT = 120;
    public const int MAX_FALLBACK_CHARS = 12;

    //a rotated 6px small-text row needs this much spine width to sit inside it
    private const int ROTATED_MIN_WIDTH = 10;

    private static readonly Color TitleTint = new(235, 225, 200);

    /// <summary>The spine's box for a carousel place: centred on the place, its bottom on the shelf line.</summary>
    public static Rectangle SpineRect(float placeX, float scale, int bottom)
    {
        var width = Math.Max(1, (int)MathF.Round(WIDTH * scale));
        var height = Math.Max(1, (int)MathF.Round(HEIGHT * scale));

        return new Rectangle((int)MathF.Round(placeX) - (width / 2), bottom - height, width, height);
    }

    public static Color SpineColor(byte spine) => LibraryLabels.Spines[spine % LibraryLabels.Spines.Length];

    public static Color EdgeColor(Color fill) => new((byte)(fill.R / 2), (byte)(fill.G / 2), (byte)(fill.B / 2), fill.A);

    public static bool FitsRotatedTitle(int spineWidth) => spineWidth >= ROTATED_MIN_WIDTH;

    public static string FallbackTitle(string title)
        => title.Length <= MAX_FALLBACK_CHARS ? title : title[..MAX_FALLBACK_CHARS];

    /// <summary>The book's cached small-text title, built once per book; null when the title is empty.</summary>
    public static Texture2D? GetTitleTexture(GraphicsDevice device, Dictionary<int, Texture2D> cache, int id, string title)
    {
        if (cache.TryGetValue(id, out var texture))
            return texture;

        texture = TextRenderer.BuildSmallText(device, title);

        if (texture is not null)
            cache[id] = texture;

        return texture;
    }

    public static void Draw(
        SpriteBatch spriteBatch,
        GraphicsDevice device,
        Dictionary<int, Texture2D> titles,
        int id,
        string title,
        Rectangle rect,
        Color fill)
    {
        UIElement.DrawRect(spriteBatch, rect, fill);
        UIElement.DrawRect(spriteBatch, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), EdgeColor(fill));

        if (FitsRotatedTitle(rect.Width))
        {
            var texture = GetTitleTexture(device, titles, id, title);

            if (texture is null)
                return;

            var scale = Math.Min(1f, (rect.Height - 8) / (float)texture.Width);

            spriteBatch.Draw(
                texture,
                new Vector2(rect.Center.X, rect.Center.Y),
                null,
                TitleTint,
                MathF.PI / 2f,
                new Vector2(texture.Width / 2f, texture.Height / 2f),
                scale,
                SpriteEffects.None,
                0f);
        } else
            //too thin for the rotated row, so the start of the title reads horizontally instead;
            //spines nearer the front draw later and cover the overflow
            TextRenderer.DrawText(
                spriteBatch,
                new Vector2(rect.X + 2, rect.Y + 4),
                FallbackTitle(title),
                TitleTint,
                false);
    }
}
