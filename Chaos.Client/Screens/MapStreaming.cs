#region
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.Screens;

/// <summary>
///     Maps whose tiles always come from the server. The client neither loads them from the maps folder nor saves them
///     there, because the same map number holds a different layout every time (tower floors, guild halls).
/// </summary>
public static class MapStreaming
{
    public static bool IsStreamed(int mapId) => mapId is CONSTANTS.STREAMED_MAP_ID or CONSTANTS.GUILD_HALL_MAP_ID;
}
