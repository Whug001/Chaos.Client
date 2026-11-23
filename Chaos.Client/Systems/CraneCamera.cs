namespace Chaos.Client.Systems;

/// <summary>
///     While a player stands on the crane arcade's control plate, the camera centres on the claw pit instead of on them,
///     so the operator sees the whole machine. The coordinates match the server's CraneOptions.Machines.arcade and
///     Unora's Tools/Crane/dress_arcade.py (map 32004: pit x 4-11, y 4-11, plate (12, 12)); move them together.
/// </summary>
public static class CraneCamera
{
    public const int ARCADE_MAP_ID = 32004;
    private const int PLATE_X = 12;
    private const int PLATE_Y = 12;
    private const float PIT_CENTER = 7.5f;

    /// <summary>
    ///     The tile the camera should centre on, when the player at (<paramref name="tileX" />, <paramref name="tileY" />)
    ///     on map <paramref name="mapId" /> is operating the crane.
    /// </summary>
    public static bool TryTarget(int mapId, int tileX, int tileY, out float centerTileX, out float centerTileY)
    {
        centerTileX = centerTileY = 0;

        if ((mapId != ARCADE_MAP_ID) || (tileX != PLATE_X) || (tileY != PLATE_Y))
            return false;

        centerTileX = centerTileY = PIT_CENTER;

        return true;
    }
}
