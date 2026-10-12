#region
using System.Collections.Frozen;
#endregion

namespace Chaos.Client.Definitions;

/// <summary>
///     Creature sprites used by ore and tree gather nodes. The server sends these as ordinary creatures, so the tab map
///     tells them apart from monsters by sprite.
/// </summary>
public static class GatherNodeSprites
{
    private static readonly FrozenSet<ushort> Sprites = new ushort[]
    {
        1276, //bronze ore, tree
        1277, //iron ore
        1278, //mythril ore
        1279, //hy-brasyl ore
        1402, //large azurium ore
        1403, //large crimsonite ore
        1404, //small azurium ore
        1405, //small crimsonite ore
        1439, //large aerium ore
        1440, //small aerium ore
        1441, //large emberite ore
        1442, //small emberite ore
        1443, //large hydrillium ore
        1444, //small hydrillium ore
        1445, //large petramite ore
        1446  //small petramite ore
    }.ToFrozenSet();

    public static bool Contains(ushort spriteId) => Sprites.Contains(spriteId);
}
