namespace Chaos.Client.Systems.College;

/// <summary>Which map ids are College room layouts (the five subject rooms and the debate layout).</summary>
public static class CollegeRoomLayouts
{
    private static readonly short[] RoomIds = [763, 775, 776, 777, 778, DEBATE_ID];

    public const short DEBATE_ID = 779;

    public static bool IsLayout(short mapId) => Array.IndexOf(RoomIds, mapId) >= 0;

    /// <summary>
    ///     The room swapped its map in place (debate on or off): the player is still in the room, so an unsent board is still
    ///     theirs to send. Leaving the College, or entering it, is a room change instead.
    /// </summary>
    public static bool IsLayoutSwap(short oldMapId, short newMapId) => (oldMapId != newMapId) && IsLayout(oldMapId) && IsLayout(newMapId);
}
