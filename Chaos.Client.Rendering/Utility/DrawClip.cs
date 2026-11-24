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

    /// <summary>
    ///     The part of <paramref name="destination" /> inside <paramref name="bounds" />, and the matching rectangle of a
    ///     <paramref name="textureWidth" /> x <paramref name="textureHeight" /> texture stretched over the whole of
    ///     <paramref name="destination" />. False when nothing is visible. Integer maths stays exact when the scale
    ///     divides evenly (e.g. the 2x lock badge); otherwise the source edge rounds down by under a texel.
    /// </summary>
    public static bool ClipScaled(
        Rectangle destination,
        Rectangle bounds,
        int textureWidth,
        int textureHeight,
        out Rectangle clipped,
        out Rectangle source)
    {
        clipped = Rectangle.Intersect(destination, bounds);

        if ((clipped.Width <= 0)
            || (clipped.Height <= 0)
            || (destination.Width <= 0)
            || (destination.Height <= 0))
        {
            source = Rectangle.Empty;

            return false;
        }

        source = new Rectangle(
            (clipped.X - destination.X) * textureWidth / destination.Width,
            (clipped.Y - destination.Y) * textureHeight / destination.Height,
            clipped.Width * textureWidth / destination.Width,
            clipped.Height * textureHeight / destination.Height);

        if ((source.Width <= 0) || (source.Height <= 0))
        {
            clipped = Rectangle.Empty;
            source = Rectangle.Empty;

            return false;
        }

        return true;
    }
}
