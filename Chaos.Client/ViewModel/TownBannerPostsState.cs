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
    /// <summary>The orange bar's limit.</summary>
    public const int MAX_CREDIT = 45;

    //three dots rather than the one-character ellipsis, which the game font may not have
    private const string ELLIPSIS = "...";

    private readonly HashSet<Point> PostSet = [];
    private int? LastMapId;

    /// <summary>The town's approved banner design id, or 0 for none.</summary>
    public int DesignId { get; private set; }

    public string TownName { get; private set; } = string.Empty;
    public string Designer { get; private set; } = string.Empty;

    public IReadOnlyCollection<Point> Posts => PostSet;

    /// <summary>Wall-tile rectangles whose painted wall banner shows the town's banner; empty while there is none.</summary>
    public IReadOnlyList<TownBannerWallInfo> Walls { get; private set; } = [];

    /// <summary>True when tile (<paramref name="x" />, <paramref name="y" />) lies in a wall banner's rectangle and there is a banner.</summary>
    public bool InWallBanner(int x, int y)
    {
        if (DesignId == 0)
            return false;

        foreach (var wall in Walls)
            if ((x >= wall.X0) && (x <= wall.X1) && (y >= wall.Y0) && (y <= wall.Y1))
                return true;

        return false;
    }

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
        Walls = args.Walls.ToList();

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

    /// <summary>
    ///     The orange-bar credit shown when a post is clicked: "Banner of Mileth by Ayla", with the designer's name cut to
    ///     fit the bar's <see cref="MAX_CREDIT" /> characters.
    /// </summary>
    public string Credit()
    {
        var town = $"Banner of {TownName}";

        if (string.IsNullOrEmpty(Designer))
            return town;

        var full = $"{town} by {Designer}";

        if (full.Length <= MAX_CREDIT)
            return full;

        return full[..(MAX_CREDIT - ELLIPSIS.Length)] + ELLIPSIS;
    }

    public void Clear()
    {
        DesignId = 0;
        TownName = string.Empty;
        Designer = string.Empty;
        PostSet.Clear();
        Walls = [];
    }
}
