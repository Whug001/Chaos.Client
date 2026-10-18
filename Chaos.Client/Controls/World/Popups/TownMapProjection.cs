namespace Chaos.Client.Controls.World.Popups;

/// <summary>
///     Turns a map tile into a pixel on the town map panel. Shared by the player's marker and the group pins so both
///     land on the same spot for the same tile.
/// </summary>
/// <remarks>
///     The town image is an isometric picture of the map: tile X runs down and to the right, tile Y down and to the
///     left, from a pivot on the image's top edge. The integer division order is the original client's and is kept
///     as-is so the marker does not move by a pixel.
/// </remarks>
public static class TownMapProjection
{
    public static (int X, int Y)? TileToPixel(
        int imgX,
        int imgY,
        int imgW,
        int imgH,
        int tileW,
        int tileH,
        int tileX,
        int tileY)
    {
        if ((tileW == 0) || (tileH == 0))
            return null;

        var totalTiles = tileW + tileH;
        var pivotX = imgW * tileH / totalTiles + imgX;

        var pixelX = (imgX - 1 + imgW - pivotX) * tileX / tileW + pivotX + (imgX - pivotX) * tileY / tileH;
        var pixelY = imgH * tileW / totalTiles * tileX / tileW + imgY + imgH * tileH / totalTiles * tileY / tileH;

        return (pixelX, pixelY);
    }

    public static bool IsOnImage(int imgX, int imgY, int imgW, int imgH, (int X, int Y) point)
        => (point.X >= imgX) && (point.X < (imgX + imgW)) && (point.Y >= imgY) && (point.Y < (imgY + imgH));
}
