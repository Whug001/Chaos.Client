#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Where a racer's own head sits on each of the 16 kart frames, and which of the aisling's four facings it shows.
/// </summary>
public static class KartHead
{
    //neck points from the kart anchor, printed by Unora Tools/KartTracks/art/pack_assets.py when it cuts the helmet off kart_driver_body
    private static readonly Point[] NECKS =
    [
        new(-5, -30), new(0, -30), new(0, -30), new(-1, -30),
        new(-1, -30), new(-1, -30), new(1, -30), new(0, -30),
        new(5, -30), new(-1, -30), new(-3, -30), new(-1, -30),
        new(0, -30), new(-2, -30), new(-3, -30), new(-2, -29)
    ];

    public static Vector2 NeckOffset(int frame) => NECKS[Wrap(frame)].ToVector2();

    /// <summary>
    ///     Frame 0 faces screen east and the frames turn clockwise. Aisling sprites face the four diagonals: east to south
    ///     shows the front, south to west the front mirrored, west to north the back mirrored, and north to east the back.
    /// </summary>
    public static (bool Front, bool Flip) Facing(int frame)
        => Wrap(frame) switch
        {
            <= 4  => (true, false),
            <= 8  => (true, true),
            <= 12 => (false, true),
            _     => (false, false)
        };

    private static int Wrap(int frame) => ((frame % KartFrames.FRAMES) + KartFrames.FRAMES) % KartFrames.FRAMES;
}
