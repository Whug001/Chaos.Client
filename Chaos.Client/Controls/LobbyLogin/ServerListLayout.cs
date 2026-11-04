using Microsoft.Xna.Framework;

namespace Chaos.Client.Controls.LobbyLogin;

/// <summary>
///     Arithmetic for the retail server box (lsvr): a top piece with five row slots, one middle strip per extra row, and a
///     bottom border.
/// </summary>
public static class ServerListLayout
{
    public const int ROWS_IN_TOP = 5;

    //the retail layout always stacks one middle strip, so the box never has fewer than six slots
    public static int StripCount(int serverCount) => Math.Max(1, serverCount - ROWS_IN_TOP);

    public static int BoxHeight(int topHeight, int stripHeight, int bottomHeight, int strips)
        => topHeight + stripHeight * strips + bottomHeight;

    public static Point BoxOrigin(int screenWidth, int screenHeight, int boxWidth, int boxHeight)
        => new((screenWidth - boxWidth) / 2, (screenHeight - boxHeight) / 2);

    public static Rectangle Row(Rectangle firstRow, int pitch, int row)
        => new(firstRow.X, firstRow.Y + row * pitch, firstRow.Width, firstRow.Height);

    public static Rectangle HitRow(Rectangle firstName, Rectangle firstDescription, int pitch, int row)
        => new(firstName.X, firstName.Y + row * pitch, firstDescription.Right - firstName.X, pitch);
}
