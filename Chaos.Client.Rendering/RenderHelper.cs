#region
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Provides primitive drawing helpers (filled rectangles) using a shared 1x1 white pixel texture.
/// </summary>
public static class RenderHelper
{
    private static Texture2D? SharedPixel;

    /// <summary>
    ///     Draws a filled rectangle using a shared 1x1 white pixel texture.
    /// </summary>
    public static void DrawRect(SpriteBatch spriteBatch, Rectangle bounds, Color color)
        => spriteBatch.Draw(GetPixel(spriteBatch.GraphicsDevice), bounds, color);

    /// <summary>
    ///     Draws a straight line segment of the given thickness by stretching and rotating the shared pixel.
    /// </summary>
    public static void DrawLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, float thickness = 1f)
    {
        var delta = end - start;
        var length = delta.Length();

        if (length < 0.5f)
            return;

        var angle = MathF.Atan2(delta.Y, delta.X);

        spriteBatch.Draw(
            GetPixel(spriteBatch.GraphicsDevice),
            start,
            null,
            color,
            angle,
            Vector2.Zero,
            new Vector2(length, thickness),
            SpriteEffects.None,
            0f);
    }

    private static Texture2D GetPixel(GraphicsDevice device)
    {
        if (SharedPixel is null || SharedPixel.IsDisposed)
        {
            SharedPixel = new Texture2D(device, 1, 1);
            SharedPixel.SetData([Color.White]);
        }

        return SharedPixel;
    }
}