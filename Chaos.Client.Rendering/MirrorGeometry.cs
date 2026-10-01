#region
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Where a mirror face's panel and glass sit inside a wall tile, in tile-local pixels (origin at the tile's
///     <see cref="Camera.TileToWorld" /> corner). The Unora art script (Tools/MirrorMaze/make_art.py) uses the same
///     numbers; change both together.
/// </summary>
public static class MirrorGeometry
{
    public const int FACE_WIDTH = 28;
    public const int PANEL_ROWS = 76;
    public const int GLASS_LEFT = 3;
    public const int GLASS_RIGHT = 24;
    public const int GLASS_TOP = 69;
    public const int GLASS_BOTTOM = 6;

    /// <summary>The frame sprite's canvas: 28 wide, 89 tall, drawn at tile-local y -62.</summary>
    public const int CANVAS_HEIGHT = 89;

    public const int CANVAS_TOP = -62;

    /// <summary>The face's base row for column 0-27 of the face.</summary>
    public static int BaseRow(MirrorSide side, int column) => side == MirrorSide.North ? 13 + column / 2 : 26 - column / 2;

    /// <summary>The face's left edge within the tile: 0 for north (left half), 28 for west (right half).</summary>
    public static int FaceLocalX(MirrorSide side) => side == MirrorSide.North ? 0 : FACE_WIDTH;

    /// <summary>True when tile-local row <paramref name="localRow" /> of face column <paramref name="column" /> is glass.</summary>
    public static bool IsGlassPixel(MirrorSide side, int column, int localRow)
    {
        if ((column < GLASS_LEFT) || (column > GLASS_RIGHT))
            return false;

        var above = BaseRow(side, column) - localRow;

        return (above >= GLASS_BOTTOM) && (above <= GLASS_TOP);
    }

    /// <summary>How far up the glass a pixel is, 0 at its bottom edge and 1 at its top. Only meaningful for glass pixels.</summary>
    public static float GlassHeightFraction(MirrorSide side, int column, int localRow)
        => (BaseRow(side, column) - localRow - GLASS_BOTTOM) / (float)(GLASS_TOP - GLASS_BOTTOM);
}
