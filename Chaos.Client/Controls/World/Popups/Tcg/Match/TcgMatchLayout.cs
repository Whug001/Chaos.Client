#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Tcg.Match;

/// <summary>Every fixed rectangle of the 640x480 match window, and the creature slot positions.</summary>
public static class TcgMatchLayout
{
    public const int SLOT_STEP = 64;
    public const int MAX_ROW_WIDTH = 400;
    public const int BOARD_CENTRE_X = 235;
    public const int YOUR_FEET_Y = 316;
    public const int THEIR_FEET_Y = 176;
    public const int HAND_STEP = 44;

    public static readonly Rectangle Board = new(0, 0, 470, 480);
    public static readonly Rectangle OpponentStrip = new(0, 0, 470, 72);
    public static readonly Rectangle Battlefield = new(0, 72, 470, 280);
    public static readonly Rectangle YourStrip = new(0, 352, 470, 128);
    public static readonly Rectangle Column = new(470, 0, 170, 480);

    public static readonly Rectangle OpponentSeal = new(8, 18, 36, 36);
    public static readonly Rectangle OpponentGems = new(52, 8, 200, 16);
    public static readonly Rectangle OpponentTraps = new(52, 30, 90, 34);
    public static readonly Rectangle OpponentCounts = new(300, 8, 160, 56);
    public static readonly Rectangle YourSeal = new(8, 356, 36, 36);
    public static readonly Rectangle YourGems = new(52, 358, 200, 16);
    public static readonly Rectangle YourTraps = new(260, 356, 90, 34);
    public static readonly Rectangle Hand = new(4, 388, 372, 84);
    public static readonly Rectangle Buttons = new(380, 356, 86, 120);
    public static readonly Rectangle Concede = new(450, 336, 16, 16);
    public static readonly Rectangle StopWatching = new(366, 336, 80, 16);
    public static readonly Rectangle WatchingLine = new(104, 336, 158, 14);

    public static readonly Rectangle StackRow = new(300, 74, 166, 84);

    public static readonly Rectangle BigCard = new(495, 8, 120, 168);
    public static readonly Rectangle Hourglass = new(478, 182, 24, 36);
    public static readonly Rectangle Timers = new(506, 186, 128, 28);
    public static readonly Rectangle PhaseLine = new(476, 222, 158, 14);
    public static readonly Rectangle Log = new(476, 238, 158, 120);
    public static readonly Rectangle Chat = new(476, 362, 158, 94);
    public static readonly Rectangle ChatBox = new(476, 458, 158, 18);

    public static readonly Rectangle Banner = new(85, 160, 300, 80);

    public static Point CreatureSlot(int index, int count, bool yours)
    {
        var step = count * SLOT_STEP > MAX_ROW_WIDTH + SLOT_STEP ? MAX_ROW_WIDTH / count : SLOT_STEP;
        var x = BOARD_CENTRE_X - ((count - 1) * step / 2) + (index * step);

        return new Point(x, yours ? YOUR_FEET_Y : THEIR_FEET_Y);
    }

    public static Point HandCard(int index, int count)
    {
        var step = count <= 1 ? HAND_STEP : Math.Min(HAND_STEP, (Hand.Width - 60) / (count - 1));

        return new Point(Hand.X + (index * step), Hand.Y);
    }

    /// <summary>Stack card slot for a display index (0 is the newest, leftmost).</summary>
    public static Rectangle StackSlot(int index, int count)
    {
        var step = count <= 3 ? 53 : (StackRow.Width - TcgCardFace.WIDTH) / (count - 1);

        return new Rectangle(StackRow.X + (index * step), StackRow.Y, TcgCardFace.WIDTH, TcgCardFace.HEIGHT);
    }

    /// <summary>Display index under a window-local point, or null. The newest (index 0, drawn on top) wins overlaps.</summary>
    public static int? StackHitAt(int x, int y, int count)
    {
        if (count <= 0)
            return null;

        for (var i = 0; i < count; i++)
        {
            if (StackSlot(i, count).Contains(x, y))
                return i;
        }

        return null;
    }

    public static Point Gem(Rectangle row, int index) => new(row.X + (index * 18), row.Y);

    public static Point Trap(Rectangle row, int index) => new(row.X + (index * 28), row.Y);
}
