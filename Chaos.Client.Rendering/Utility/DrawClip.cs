using Microsoft.Xna.Framework;

namespace Chaos.Client.Rendering.Utility;

/// <summary>Clipping for 1:1 sprite draws inside a panel.</summary>
public static class DrawClip
{
    /// <summary>
    ///     The part of <paramref name="destination" /> inside <paramref name="bounds" />, and the matching rectangle of a
    ///     texture drawn unscaled at <paramref name="destination" />. False when nothing is visible.
    /// </summary>
    public static bool Clip(Rectangle destination, Rectangle bounds, out Rectangle clipped, out Rectangle source)
    {
        clipped = Rectangle.Intersect(destination, bounds);

        if ((clipped.Width <= 0) || (clipped.Height <= 0))
        {
            source = Rectangle.Empty;

            return false;
        }

        source = new Rectangle(clipped.X - destination.X, clipped.Y - destination.Y, clipped.Width, clipped.Height);

        return true;
    }
}
