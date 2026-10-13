#region
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>A picture's place on a College room wall: which wall, the first tile along it, and how many tiles wide.</summary>
public readonly record struct WallSpot(MirrorSide Side, int Start, int Tiles)
{
    public int Width => Tiles * MirrorGeometry.FACE_WIDTH;

    /// <summary>The wall tile holding strip <paramref name="index" />, counting strips from the left on screen.</summary>
    public (int X, int Y) TileOf(int index) => Side == MirrorSide.North ? (Start + index, 0) : (0, Start + Tiles - 1 - index);
}

/// <summary>
///     Where College wall art sits on a wall tile. Each picture is cut into 28 px strips, one per wall tile, drawn on the
///     same face of the tile the mirror maze uses (left half on a north wall, right half on a west wall), slanted with
///     <see cref="MirrorGeometry.BaseRow" />. The spots match Unora's Tools/College/college_maps.py; change both together.
/// </summary>
public static class WallDecalGeometry
{
    public const int PICTURE_HEIGHT = 88;
    public const int FRAME_IMAGE_WIDTH = 112;
    public const int BOARD_IMAGE_WIDTH = 168;
    public const int FRAME_BORDER = 8;
    public const int PANE_EDGE = 4;

    /// <summary>Rows between the picture's bottom edge and the wall's base line. Tune against a real room screenshot.</summary>
    public const int BOTTOM_GAP = 4;

    //BaseRow's smallest value: column 0 of a north face, column 27 of a west face
    private const int LOWEST_BASE = 13;
    private const int SLANT = 13;

    public static readonly WallSpot[] Frames =
    [
        new(MirrorSide.North, 1, 4),
        new(MirrorSide.North, 15, 4),
        new(MirrorSide.West, 1, 4),
        new(MirrorSide.West, 6, 4),
        new(MirrorSide.West, 11, 4),
        new(MirrorSide.West, 16, 4)
    ];

    public static readonly WallSpot Board = new(MirrorSide.North, 7, 6);

    public static int StripHeight(int height) => height + SLANT;

    public static int StripRow(MirrorSide side, int column, int row) => MirrorGeometry.BaseRow(side, column) - LOWEST_BASE + row;

    /// <summary>Tile-local y of a strip's top row, so the picture's bottom sits <see cref="BOTTOM_GAP" /> above the base.</summary>
    public static int StripTop(int height) => LOWEST_BASE + 1 - BOTTOM_GAP - height;
}
