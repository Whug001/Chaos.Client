#region
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.Screens;

/// <summary>
///     Maps whose tiles always come from the server. The client neither loads them from the maps folder nor saves them
///     there, because the same map number holds a different layout every time (tower floors, guild halls, houses).
/// </summary>
public static class MapStreaming
{
    /// <summary>Player houses are private copies of map 32002 (guild halls are 32001).</summary>
    public const int HOUSE_MAP_ID = 32002;

    public static bool IsStreamed(int mapId) => mapId is CONSTANTS.STREAMED_MAP_ID or CONSTANTS.GUILD_HALL_MAP_ID or HOUSE_MAP_ID;

    /// <summary>Maps that take live guild-hall tile packets; streamed tower floors (32000) do not.</summary>
    public static bool IsEditableMap(int mapId) => mapId is CONSTANTS.GUILD_HALL_MAP_ID or HOUSE_MAP_ID;
}
