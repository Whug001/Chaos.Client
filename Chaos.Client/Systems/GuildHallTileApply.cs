using Chaos.Networking.Entities.Server;
using DALib.Data;

namespace Chaos.Client.Systems;

/// <summary>Writes a hall editor Tiles packet into the live map file.</summary>
public static class GuildHallTileApply
{
    /// <returns>How many tiles landed on the map.</returns>
    public static int Apply(MapFile mapFile, IReadOnlyList<GuildHallTileInfo> tiles)
    {
        var applied = 0;

        foreach (var tile in tiles)
        {
            if ((tile.X >= mapFile.Width) || (tile.Y >= mapFile.Height))
                continue;

            mapFile.Tiles[tile.X, tile.Y] = new MapTile
            {
                Background = unchecked((short)tile.Background),
                LeftForeground = unchecked((short)tile.Left),
                RightForeground = unchecked((short)tile.Right)
            };

            applied++;
        }

        return applied;
    }

}
