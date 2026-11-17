#region
using Chaos.Client.Collections;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Screens;

/// <summary>
///     Town banner posts: standing poles flying the town's approved banner on the tiles the server lists for this map.
///     They block their tile like a wall while the town has a banner.
/// </summary>
public sealed partial class WorldScreen
{
    private void WireTownBannerPosts() => Game.Connection.OnTownBannerPosts += HandleTownBannerPosts;

    private void UnwireTownBannerPosts() => Game.Connection.OnTownBannerPosts -= HandleTownBannerPosts;

    private void HandleTownBannerPosts(TownBannerPostsArgs args)
    {
        WorldState.TownBannerPosts.Set(args);

        if (args.DesignId != 0)
            RequestGuildEmblem(args.DesignId);
    }
}
