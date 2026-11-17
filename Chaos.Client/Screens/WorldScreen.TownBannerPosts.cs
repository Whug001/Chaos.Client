#region
using Chaos.Client.Collections;
using Chaos.Client.Rendering;
using Chaos.Client.Systems;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
        //wall banner tiles are cut for one map's walls
        BannerPostTextures.ClearWallCells();
        WorldState.TownBannerPosts.Set(args);

        if (args.DesignId != 0)
            RequestGuildEmblem(args.DesignId);
    }

    /// <summary>
    ///     Draws the post standing on tile (<paramref name="x" />, <paramref name="y" />), if there is one and its banner
    ///     has downloaded. Called in the stripe pass after the tile's foreground, so it sorts like a foreground piece.
    /// </summary>
    private void DrawBannerPostTile(BatchBlendScope scope, int x, int y)
    {
        var posts = WorldState.TownBannerPosts;

        if (!posts.Blocks(x, y) || (BannerPostTextures.Get(Device, posts.DesignId) is not { } texture))
            return;

        scope.Require(BlendState.AlphaBlend);
        scope.Batch.Draw(texture, BannerPostTopLeft(x, y), Color.White);
    }

    /// <summary>
    ///     Repaints the wall banner on tile (<paramref name="x" />, <paramref name="y" />) with the town's banner, over the
    ///     wall pieces drawn just before it in the stripe pass.
    /// </summary>
    private void DrawTownWallBannerTile(BatchBlendScope scope, int x, int y)
    {
        var posts = WorldState.TownBannerPosts;

        if (!posts.InWallBanner(x, y) || MapFile is null || (x >= MapFile.Width) || (y >= MapFile.Height))
            return;

        var tile = MapFile.Tiles[x, y];

        if (BannerPostTextures.GetWallCell(Device, posts.DesignId, x, y, IsTileWall(tile.LeftForeground), IsTileWall(tile.RightForeground))
            is not { } texture)
            return;

        var smooth = ClientSettings.ScrollLevel > 0;
        var corner = Camera.WorldToScreen(Camera.TileToWorld(x, y, MapFile.Height));
        var top = corner.Y + TownWallBannerPainter.CELL_TOP;

        scope.Require(BlendState.AlphaBlend);
        scope.Batch.Draw(texture, smooth ? new Vector2(corner.X, top) : new Vector2(MathF.Floor(corner.X), MathF.Floor(top)), Color.White);
    }

    /// <summary>Where the post's image goes so its foot pixel stands on the tile's floor centre (viewport space).</summary>
    private Vector2 BannerPostTopLeft(int x, int y)
    {
        var smooth = ClientSettings.ScrollLevel > 0;
        var tile = Camera.WorldToScreen(Camera.TileToWorld(x, y, MapFile!.Height));
        var left = tile.X + HALF_TILE_WIDTH - BannerPostPainter.FOOT_X;
        var top = tile.Y + HALF_TILE_HEIGHT - BannerPostPainter.FOOT_Y;

        return smooth ? new Vector2(left, top) : new Vector2(MathF.Floor(left), MathF.Floor(top));
    }

    /// <summary>Shows the banner's credit when the click lands on a drawn post. True when it did.</summary>
    private bool TryClickBannerPost(int mouseX, int mouseY)
    {
        var posts = WorldState.TownBannerPosts;

        if ((MapFile is null) || (posts.DesignId == 0) || !BannerPostTextures.CanDraw(posts.DesignId))
            return false;

        var viewport = WorldHud.ViewportBounds;
        var localX = mouseX - viewport.X;
        var localY = mouseY - viewport.Y;

        foreach (var post in posts.Posts)
        {
            var topLeft = BannerPostTopLeft(post.X, post.Y);

            if (!BannerPostPainter.Hits((int)MathF.Floor(localX - topLeft.X), (int)MathF.Floor(localY - topLeft.Y)))
                continue;

            WorldState.Chat.AddOrangeBarMessage(posts.Credit());

            return true;
        }

        return false;
    }

    private const int HALF_TILE_WIDTH = 28;
    private const int HALF_TILE_HEIGHT = 14;
}
