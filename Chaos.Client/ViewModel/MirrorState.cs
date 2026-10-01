#region
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>One mirror face on a wall tile: its run, how far along the run it is, and which face of the tile.</summary>
public readonly record struct MirrorFace(int SegmentIndex, int IndexInRun, MirrorSide Side);

/// <summary>A reflection climbing out of a haunted mirror, from the server's MirrorDouble.</summary>
public readonly record struct MirrorDouble(uint EntityId, int SegmentIndex, long StartMs);

/// <summary>
///     The current map's mirrors, from the server's MirrorLayout, indexed by wall tile, plus the doubles now showing.
///     Not cleared by <c>WorldState.Clear</c>: a same-map refresh keeps the mirrors. WorldScreen clears it on a real
///     map change; <c>WorldState.ResetAll</c> clears it on logout.
/// </summary>
public sealed class MirrorState
{
    /// <summary>A double climbs for 0.6 s, follows for 6 s and fades for 1 s.</summary>
    public const long DOUBLE_LIFETIME_MS = 7600;

    private static readonly List<MirrorFace> NoFaces = [];

    private readonly List<MirrorDouble> DoubleList = [];
    private readonly Dictionary<(int X, int Y), List<MirrorFace>> FacesByTile = [];

    public IReadOnlyList<MirrorStretchInfo> DarkStretches { get; private set; } = [];
    public IReadOnlyList<MirrorDouble> Doubles => DoubleList;
    public bool HasMirrors => Segments.Count > 0;
    public IReadOnlyList<MirrorSegmentInfo> Segments { get; private set; } = [];

    public void AddDouble(uint entityId, int segmentIndex, long nowMs)
    {
        if ((segmentIndex < 0) || (segmentIndex >= Segments.Count))
            return;

        DoubleList.RemoveAll(d => d.EntityId == entityId);
        DoubleList.Add(new MirrorDouble(entityId, segmentIndex, nowMs));
    }

    public void Apply(MirrorLayoutArgs args)
    {
        Clear();
        Segments = args.Segments ?? [];
        DarkStretches = args.DarkStretches ?? [];

        for (var i = 0; i < Segments.Count; i++)
        {
            var segment = Segments[i];

            for (var k = 0; k < segment.Length; k++)
            {
                var tile = segment.Side == MirrorSide.North ? ((int)segment.X + k, (int)segment.Y) : ((int)segment.X, (int)segment.Y + k);

                if (!FacesByTile.TryGetValue(tile, out var faces))
                    FacesByTile[tile] = faces = [];

                faces.Add(new MirrorFace(i, k, segment.Side));
            }
        }
    }

    public void Clear()
    {
        Segments = [];
        DarkStretches = [];
        FacesByTile.Clear();
        DoubleList.Clear();
    }

    /// <summary>The mirror faces on wall tile (x, y): none, one, or a north and a west face.</summary>
    public IReadOnlyList<MirrorFace> FacesAt(int x, int y) => FacesByTile.TryGetValue((x, y), out var faces) ? faces : NoFaces;

    public bool IsInDarkStretch(int x, int y)
    {
        foreach (var s in DarkStretches)
            if ((x >= s.X) && (x < s.X + s.Width) && (y >= s.Y) && (y < s.Y + s.Height))
                return true;

        return false;
    }

    public void PruneDoubles(long nowMs) => DoubleList.RemoveAll(d => nowMs - d.StartMs >= DOUBLE_LIFETIME_MS);

    public void RemoveDoublesOf(uint entityId) => DoubleList.RemoveAll(d => d.EntityId == entityId);
}
