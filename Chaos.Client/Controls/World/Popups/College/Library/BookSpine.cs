using Chaos.Client.Rendering.Library;
using Chaos.Client.ViewModel.College.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Controls.World.Popups.College.Library;

/// <summary>Draws one library spine from its BookArt texture.</summary>
public static class BookSpine
{
    public const int WIDTH = 28;
    public const int HEIGHT = 120;

    /// <summary>The spine's box for a carousel place: centred on the place, its bottom on the shelf line.</summary>
    public static Rectangle SpineRect(float placeX, float scale, int bottom)
    {
        var width = Math.Max(1, (int)MathF.Round(WIDTH * scale));
        var height = Math.Max(1, (int)MathF.Round(HEIGHT * scale));

        return new Rectangle((int)MathF.Round(placeX) - (width / 2), bottom - height, width, height);
    }

    public static Color SpineColor(byte spine) => LibraryLabels.Spines[spine % LibraryLabels.Spines.Length];

    /// <summary>The book's spine texture, built once per book from <see cref="BookArt" />.</summary>
    public static Texture2D GetTexture(GraphicsDevice device, Dictionary<int, Texture2D> cache, int id, string title, Color fill)
    {
        if (cache.TryGetValue(id, out var texture))
            return texture;

        texture = new Texture2D(device, WIDTH, HEIGHT);
        texture.SetData(BookArt.PaintSpine(title, fill, FontGlyphBits.Instance));
        cache[id] = texture;

        return texture;
    }

    public static void Draw(SpriteBatch spriteBatch, Texture2D spine, Rectangle rect) => spriteBatch.Draw(spine, rect, Color.White);
}
