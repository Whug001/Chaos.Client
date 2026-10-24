using Microsoft.Xna.Framework;

namespace Chaos.Client.Systems.Cinema;

/// <summary>
///     Where the film screen sits on a map. The picture is drawn in world pixels at the anchor tile's top-left corner
///     plus the offset, in the draw stripe of <see cref="Depth" /> so walls and people in front cover it.
/// </summary>
public readonly record struct CinemaScreenSpot(int AnchorX, int AnchorY, int OffsetX, int OffsetY, int Width, int Height)
{
    public int Depth => AnchorX + AnchorY;
}

public static class CinemaScreenLayout
{
    public const int HALL_MAP_ID = 15;
    public const int VIDEO_WIDTH = 854;
    public const int VIDEO_HEIGHT = 480;

    //placeholder until Plan 3 paints the castle-wall screen and sets the real spot
    public static CinemaScreenSpot Hall { get; } = new(AnchorX: 8, AnchorY: 2, OffsetX: -180, OffsetY: -200, Width: 360, Height: 190);

    /// <summary>The largest rectangle with the picture's shape that fits centred in <paramref name="area" />.</summary>
    public static Rectangle FitPicture(Rectangle area, int pictureWidth, int pictureHeight)
    {
        if ((long)area.Width * pictureHeight > (long)area.Height * pictureWidth)
        {
            var width = area.Height * pictureWidth / pictureHeight;

            return new Rectangle(area.X + (area.Width - width) / 2, area.Y, width, area.Height);
        }

        var height = area.Width * pictureHeight / pictureWidth;

        return new Rectangle(area.X, area.Y + (area.Height - height) / 2, area.Width, height);
    }

    /// <summary>
    ///     The stripe to draw the screen in. The picture hangs above its anchor, so it can be on screen while the anchor
    ///     tile's stripe is not; then it is drawn in the nearest visible stripe.
    /// </summary>
    public static int DrawStripe(int spotDepth, int minDepth, int maxDepth) => Math.Clamp(spotDepth, minDepth, maxDepth);
}
