#region
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>
///     How far each entity is drawn along its raft, in tiles along x. An entity is anchored to the lane step at which it
///     reached its tile; the drift is how far the lane has moved since. A tile change from a walk blends from the old
///     drift with the walk's progress; a tile change from a raft step re-anchors to the nearest step, so a server walk
///     that lands a little before or after the client's own step does not jump. Pure.
/// </summary>
public sealed class CausewayRideTracker
{
    private const double MAX_LEAD = 2.0;

    private readonly Dictionary<uint, Entry> Entries = [];

    public void Clear() => Entries.Clear();

    public void Forget(uint id) => Entries.Remove(id);

    /// <summary>The drift, in tiles along x, to draw the entity with this frame.</summary>
    /// <param name="walkProgress">0 when a walk onto this tile just started, 1 when it is done or there is no walk.</param>
    /// <param name="lane">The river lane on the entity's row, or null off the river.</param>
    public double Offset(
        uint id,
        int x,
        int y,
        double walkProgress,
        CausewayLane? lane,
        CausewayShellTiming timing,
        long playMs)
    {
        if (!Entries.TryGetValue(id, out var e))
        {
            e = new Entry { X = x, Y = y, Anchor = lane is null ? 0 : CausewaySchedule.Steps(lane, playMs) };
            Entries[id] = e;
        } else if ((e.X != x) || (e.Y != y))
        {
            e.Carry = e.Last;
            e.X = x;
            e.Y = y;
            e.Anchor = lane is null ? 0 : CausewaySchedule.Steps(lane, playMs);
        }

        var target = 0.0;

        if (lane is not null)
        {
            var piece = CausewaySchedule.PieceAt(lane, x, e.Anchor);

            if ((piece >= 0)
                && ((lane.Kind != CausewayLaneKind.Shell)
                    || (CausewaySchedule.ShellState(timing, lane.Pieces[piece], playMs) != CausewayShellState.Sunk)))
            {
                var drift = CausewaySchedule.Steps(lane, playMs) + CausewaySchedule.Fraction(lane, playMs) - e.Anchor;
                target = lane.Direction * Math.Clamp(drift, -1, MAX_LEAD);
            }
        }

        var p = Math.Clamp(walkProgress, 0, 1);
        e.Last = e.Carry + (target - e.Carry) * p;

        return e.Last;
    }

    /// <summary>The entity was carried one tile by its raft (locally or by the server's ride walk).</summary>
    public void Shifted(uint id, int x, int y, CausewayLane lane, long playMs)
    {
        if (!Entries.TryGetValue(id, out var e))
        {
            e = new Entry();
            Entries[id] = e;
        } else
        {
            //the tile moved under a walk still blending in; move the blend start with it so the drawn spot does not jump
            var moved = x - e.X;
            e.Carry -= moved;
            e.Last -= moved;
        }

        e.X = x;
        e.Y = y;
        e.Anchor = (long)Math.Round(CausewaySchedule.Steps(lane, playMs) + CausewaySchedule.Fraction(lane, playMs));
    }

    private sealed class Entry
    {
        public long Anchor;
        public double Carry;
        public double Last;
        public int X;
        public int Y;
    }
}
