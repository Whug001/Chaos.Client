#region
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Karting;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Systems.Karting;

/// <summary>
///     Buffers race snapshots and gives each other kart's body a fixed delay behind the estimated server step, so karts
///     move smoothly between snapshots that arrive every second step.
/// </summary>
public sealed class KartInterpolator
{
    public const double DELAY_STEPS = 5;
    public const double CARRY_STEPS = 3;
    public const int BUFFER = 8;
    public const double SNAP_TILES = 3;

    private readonly List<(uint Tick, Dictionary<byte, KartBody> Bodies, Dictionary<ushort, KartItemObjectInfo> Objects)> Frames = [];

    public void Add(uint serverTick, IEnumerable<KartRacerSnapshot> racers, IEnumerable<KartItemObjectInfo>? objects = null)
    {
        if (Frames.Count > 0 && serverTick <= Frames[^1].Tick)
            return;

        Frames.Add((serverTick, racers.ToDictionary(racer => racer.Index, KartRaceState.ToBody), (objects ?? []).ToDictionary(o => o.Id)));

        if (Frames.Count > BUFFER)
            Frames.RemoveAt(0);
    }

    /// <returns>The object's position and heading <see cref="DELAY_STEPS" /> behind, or null when the newest frame no longer has it.</returns>
    public (double X, double Y, double Heading)? SampleObject(ushort id, double clockStep)
    {
        if ((Frames.Count == 0) || !Frames[^1].Objects.ContainsKey(id))
            return null;

        var target = clockStep - DELAY_STEPS;
        (uint Tick, KartItemObjectInfo Item)? previous = null;

        foreach (var frame in Frames)
        {
            if (!frame.Objects.TryGetValue(id, out var item))
                continue;

            if (frame.Tick > target)
            {
                if (previous is not { } from)
                    return (item.X, item.Y, item.Heading);

                var t = (target - from.Tick) / (frame.Tick - from.Tick);

                return (from.Item.X + (item.X - from.Item.X) * t,
                        from.Item.Y + (item.Y - from.Item.Y) * t,
                        from.Item.Heading + Math.IEEERemainder(item.Heading - from.Item.Heading, 2 * Math.PI) * t);
            }

            previous = (frame.Tick, item);
        }

        return previous is { } last ? (last.Item.X, last.Item.Y, last.Item.Heading) : null;
    }

    public KartBody? Sample(byte index, double clockStep)
    {
        var target = clockStep - DELAY_STEPS;
        (uint Tick, KartBody Body)? previous = null;

        foreach (var frame in Frames)
        {
            if (!frame.Bodies.TryGetValue(index, out var body))
                continue;

            if (frame.Tick > target)
            {
                if (previous is not { } from)
                    return body;

                return Blend(from.Body, body, (target - from.Tick) / (frame.Tick - from.Tick));
            }

            previous = (frame.Tick, body);
        }

        if (previous is not { } last)
            return null;

        return Carry(last.Body, target - last.Tick);
    }

    public void Clear() => Frames.Clear();

    private static KartBody Blend(KartBody from, KartBody to, double fraction)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;

        //a respawn moves the kart across the track; sliding it there would draw it over the grass
        if (Math.Sqrt(dx * dx + dy * dy) > SNAP_TILES)
            return to;

        var headingDelta = Math.IEEERemainder(to.Heading - from.Heading, 2 * Math.PI);

        return new KartBody
        {
            X = from.X + dx * fraction,
            Y = from.Y + dy * fraction,
            Vx = from.Vx + (to.Vx - from.Vx) * fraction,
            Vy = from.Vy + (to.Vy - from.Vy) * fraction,
            Heading = KartPhysics.NormalizeHeading(from.Heading + headingDelta * fraction)
        };
    }

    private static KartBody Carry(KartBody body, double steps)
    {
        var seconds = Math.Clamp(steps, 0, CARRY_STEPS) / KartPhysics.STEPS_PER_SECOND;

        return body with { X = body.X + body.Vx * seconds, Y = body.Y + body.Vy * seconds };
    }
}
