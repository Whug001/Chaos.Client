using Chaos.Geometry.Abstractions;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using GeoPoint = Chaos.Geometry.Point;

namespace Chaos.Client.ViewModel;

/// <summary>
///     The town banner posts on the current map, as last sent by the server (<c>TownBannerPosts</c>, opcode 185). Each
///     packet replaces the whole list. While <see cref="DesignId" /> is 0 the town has no banner, so no post is drawn or
///     blocks. Cleared on a change of map id and on logout; a same-map refresh keeps it.
/// </summary>
public sealed class TownBannerPostsState
{
    private readonly HashSet<Point> PostSet = [];
    private int? LastMapId;

    /// <summary>The town's approved banner design id, or 0 for none.</summary>
    public int DesignId { get; private set; }

    public string TownName { get; private set; } = string.Empty;
    public string Designer { get; private set; } = string.Empty;

    public IReadOnlyCollection<Point> Posts => PostSet;

    /// <summary>The post tiles as pathfinding blockers; empty while there is no banner.</summary>
    public IEnumerable<IPoint> BlockedPoints
        => DesignId == 0 ? [] : PostSet.Select(post => (IPoint)new GeoPoint(post.X, post.Y));

    /// <summary>True when (<paramref name="x" />, <paramref name="y" />) holds a post and the town has a banner.</summary>
    public bool Blocks(int x, int y) => (DesignId != 0) && PostSet.Contains(new Point(x, y));

    public void Set(TownBannerPostsArgs args)
    {
        DesignId = args.DesignId;
        TownName = args.TownName;
        Designer = args.Designer;
        PostSet.Clear();

        foreach (var post in args.Posts)
            PostSet.Add(new Point(post.X, post.Y));
    }

    /// <summary>Forgets the posts when the map id changes; a refresh of the same map keeps them.</summary>
    public void OnMapInfo(int mapId)
    {
        if (LastMapId != mapId)
            Clear();

        LastMapId = mapId;
    }

    public void Clear()
    {
        DesignId = 0;
        TownName = string.Empty;
        Designer = string.Empty;
        PostSet.Clear();
    }
}
