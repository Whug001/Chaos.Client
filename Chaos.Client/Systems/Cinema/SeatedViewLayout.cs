using Microsoft.Xna.Framework;

namespace Chaos.Client.Systems.Cinema;

public static class SeatedViewLayout
{
    public const int WIDTH = 640;
    public const int HEIGHT = 480;
    public const int BACKDROP_WIDTH = 800;
    public const int BACKDROP_HEIGHT = 600;
    public const int OPENING_X = 188;
    public const int OPENING_Y = 154;
    public const int OPENING_WIDTH = 422;
    public const int OPENING_HEIGHT = 232;
    public const int SEAT_WIDTH = 110;
    public const int SEAT_HEIGHT = 70;
    public const int SEAT_SPACING = 120;
    public const int HEAD_CROP = 30;
    public const int HEAD_SCALE = 3;
    public const int HEAD_VISIBLE_ROWS = 16; // Crop rows of the head shown above the seat back.
    public const int SEAT_TOP_INSET = 6; // Transparent rows above the seat-back art's top edge.
    public const int NEAR_TOP = 370;
    public const int FAR_TOP = 330;
    public const float FAR_SCALE = 0.7f;
    public const int CINEMA_PANEL_WIDTH = 640;
    public const int CINEMA_PANEL_HEIGHT = 96;
    // How far below the seat-back top the panel's top edge sits, so the seat tops show above it.
    public const int CINEMA_PANEL_DROP = 40;

    public static Rectangle Screen(int row)
    {
        var width = 480 - 40 * row;
        var height = width * OPENING_HEIGHT / OPENING_WIDTH;
        var x = (WIDTH - width) / 2;
        var y = 60 + 12 * row;

        return new Rectangle(x, y, width, height);
    }

    public static Rectangle Backdrop(Rectangle screen)
    {
        var s = screen.Width / (float)OPENING_WIDTH;

        return new Rectangle(
            (int)MathF.Floor(screen.X - OPENING_X * s),
            (int)MathF.Floor(screen.Y - OPENING_Y * s),
            (int)MathF.Ceiling(BACKDROP_WIDTH * s) + 1,
            (int)MathF.Ceiling(BACKDROP_HEIGHT * s) + 1);
    }

    //row 0 sits nearest the screen, so only rows 0-2 can be in front; shared, never mutated by callers
    private static readonly int[][] RowsInFrontCache = [[], [0], [0, 1]];

    public static int[] RowsInFront(int viewerRow)
        => (viewerRow >= 0) && (viewerRow < RowsInFrontCache.Length) ? RowsInFrontCache[viewerRow] : Enumerable.Range(0, viewerRow).ToArray();

    public static float Scale(int distance) => distance == 1 ? 1f : FAR_SCALE;

    public static Rectangle SeatBack(int viewerRow, int viewerCol, int row, int col)
    {
        var distance = viewerRow - row;
        var s = Scale(distance);
        var w = (int)MathF.Round(SEAT_WIDTH * s);
        var h = (int)MathF.Round(SEAT_HEIGHT * s);
        var centerX = WIDTH / 2 + (col - viewerCol) * (int)MathF.Round(SEAT_SPACING * s);
        var x = centerX - w / 2;
        var y = distance == 1 ? NEAR_TOP : FAR_TOP;

        return new Rectangle(x, y, w, h);
    }

    //row panels only ever sit one or two rows ahead; shared, never mutated by callers
    private static readonly Rectangle[] PanelNear = BuildPanel(1);
    private static readonly Rectangle[] PanelFar = BuildPanel(2);

    public static Rectangle[] Panel(int distance)
        => distance == 1 ? PanelNear : distance == 2 ? PanelFar : BuildPanel(distance);

    private static Rectangle[] BuildPanel(int distance)
    {
        var s = Scale(distance);
        var w = (int)MathF.Round(CINEMA_PANEL_WIDTH * s);
        var h = (int)MathF.Round(CINEMA_PANEL_HEIGHT * s);
        var y = (distance == 1 ? NEAR_TOP : FAR_TOP) + (int)MathF.Round(CINEMA_PANEL_DROP * s);
        var left = WIDTH / 2 - w / 2;

        while (left > 0)
            left -= w;

        var tiles = new List<Rectangle>();

        while (left < WIDTH)
        {
            tiles.Add(new Rectangle(left, y, w, h));
            left += w;
        }

        return [.. tiles];
    }

    public static Rectangle Head(Rectangle seatBack, int distance)
    {
        var size = (int)MathF.Round(HEAD_CROP * HEAD_SCALE * Scale(distance));
        var x = seatBack.Center.X - size / 2;
        var s = Scale(distance);
        var visible = (int)MathF.Round(HEAD_VISIBLE_ROWS * HEAD_SCALE * s);
        var top = seatBack.Y + (int)MathF.Round(SEAT_TOP_INSET * s) - visible;

        return new Rectangle(x, top, size, size);
    }
}
