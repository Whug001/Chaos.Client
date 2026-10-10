#region
using System.Diagnostics;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>Seconds on a steady clock, shared by the Tumble Tower state and its drawing.</summary>
public static class TumbleClock
{
    public static double Now => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
}

public readonly record struct TumbleTileView(TumbleTowerTileState State, double Progress, double SecondsSinceChange);

public readonly record struct TumbleFloorRect(int X, int Y, int Size)
{
    public bool Contains(int x, int y) => (x >= X) && (y >= Y) && (x < X + Size) && (y < Y + Size);
}

/// <summary>
///     The client's copy of a Tumble Tower match: floor rectangles, every tile that is not solid, recent falls, and the
///     label numbers. Pure; every time-dependent call takes the time in <see cref="TumbleClock" /> seconds.
/// </summary>
public sealed class TumbleTowerState
{
    private readonly Dictionary<uint, TumbleFall> Falls = [];
    private readonly List<TumbleFloorRect> FloorRects = [];
    private readonly Dictionary<(int X, int Y), (TumbleTowerTileState State, double ChangedAt)> Tiles = [];

    public bool IsActive => FloorRects.Count > 0;
    public int FloorsInUse => FloorRects.Count;
    public IReadOnlyList<TumbleFloorRect> Floors => FloorRects;
    public double MeltSeconds { get; private set; } = 1.5;
    public int PlayersLeft { get; private set; }

    /// <summary>Null when the rule has no timer.</summary>
    public int? SecondsLeft { get; private set; }

    public void Apply(TumbleTowerStateArgs args, double now)
    {
        switch (args.Kind)
        {
            case TumbleTowerMessageKind.Layout:
                FloorRects.Clear();
                Tiles.Clear();
                Falls.Clear();
                MeltSeconds = args.MeltMs / 1000.0;

                foreach (var f in args.Floors)
                    FloorRects.Add(new TumbleFloorRect(f.OriginX, f.OriginY, f.Size));

                ApplyTiles(args.Tiles, now);

                break;

            case TumbleTowerMessageKind.TileUpdates:
                ApplyTiles(args.Tiles, now);

                break;

            case TumbleTowerMessageKind.Fall:
                Falls[args.EntityId] = new TumbleFall(args.EntityId, args.FromX, args.FromY, args.ToX, args.ToY, args.FloorsDropped, args.Outcome, now);

                break;

            case TumbleTowerMessageKind.Status:
                PlayersLeft = args.PlayersLeft;
                SecondsLeft = args.SecondsLeft == ushort.MaxValue ? null : args.SecondsLeft;

                break;

            case TumbleTowerMessageKind.Clear:
                Clear();

                break;
        }
    }

    public void Clear()
    {
        FloorRects.Clear();
        Tiles.Clear();
        Falls.Clear();
        PlayersLeft = 0;
        SecondsLeft = null;
    }

    public int? FloorAt(int x, int y)
    {
        for (var i = 0; i < FloorRects.Count; i++)
            if (FloorRects[i].Contains(x, y))
                return i;

        return null;
    }

    public (int X, int Y) SameSpotOn(int floor, int x, int y, int otherFloor)
    {
        var from = FloorRects[floor];
        var to = FloorRects[otherFloor];

        return (to.X + (x - from.X), to.Y + (y - from.Y));
    }

    public TumbleTileView TileAt(int x, int y, double now)
    {
        if (!Tiles.TryGetValue((x, y), out var tile))
            return new TumbleTileView(TumbleTowerTileState.Solid, 0, 0);

        var since = Math.Max(0, now - tile.ChangedAt);
        var progress = tile.State == TumbleTowerTileState.Melting ? Math.Clamp(since / MeltSeconds, 0, 1) : 1;

        return new TumbleTileView(tile.State, progress, since);
    }

    public TumbleFall? ActiveFall(uint entityId, double now)
    {
        if (!Falls.TryGetValue(entityId, out var fall))
            return null;

        if (now - fall.StartedAt <= TumbleDiveTimeline.TotalSeconds(fall.FloorsDropped))
            return fall;

        Falls.Remove(entityId);

        return null;
    }

    /// <summary>Replaces the contents of <paramref name="into" /> with the falls still playing. Called every frame, so it allocates nothing.</summary>
    public void CollectActiveFalls(double now, List<TumbleFall> into)
    {
        into.Clear();

        foreach (var fall in Falls.Values)
            if (now - fall.StartedAt <= TumbleDiveTimeline.TotalSeconds(fall.FloorsDropped))
                into.Add(fall);
    }

    private void ApplyTiles(IEnumerable<TumbleTileInfo> tiles, double now)
    {
        foreach (var t in tiles)
            if (t.State == TumbleTowerTileState.Solid)
                Tiles.Remove((t.X, t.Y));
            else
                Tiles[(t.X, t.Y)] = (t.State, now - t.MsSinceChange / 1000.0);
    }
}
