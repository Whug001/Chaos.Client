using Microsoft.Xna.Framework;

namespace Chaos.Client.Rendering;

/// <summary>
///     Where the play area lands on the back buffer when the world is drawn at full resolution, and the matrix that
///     maps 640-space play-area coordinates into the world target.
/// </summary>
public readonly record struct WorldSurfaceLayout(Rectangle Viewport, Rectangle Destination, float ScaleX, float ScaleY, Matrix SurfaceMatrix)
{
    public static WorldSurfaceLayout From(Rectangle presentRect, Rectangle viewport, int virtualWidth, int virtualHeight)
    {
        var fx = (float)presentRect.Width / virtualWidth;
        var fy = (float)presentRect.Height / virtualHeight;

        //edges are rounded, not the size, so the play area meets the HUD on the same back-buffer pixel
        var left = presentRect.X + (int)MathF.Round(viewport.Left * fx);
        var top = presentRect.Y + (int)MathF.Round(viewport.Top * fy);
        var right = presentRect.X + (int)MathF.Round(viewport.Right * fx);
        var bottom = presentRect.Y + (int)MathF.Round(viewport.Bottom * fy);
        var destination = new Rectangle(left, top, right - left, bottom - top);

        var scaleX = (float)destination.Width / viewport.Width;
        var scaleY = (float)destination.Height / viewport.Height;
        var matrix = Matrix.CreateTranslation(-viewport.X, -viewport.Y, 0) * Matrix.CreateScale(scaleX, scaleY, 1);

        return new WorldSurfaceLayout(viewport, destination, scaleX, scaleY, matrix);
    }
}
