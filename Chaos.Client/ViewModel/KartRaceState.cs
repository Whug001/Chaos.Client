#region
using Chaos.DarkAges.Karting;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

public sealed class KartRaceState
{
    private readonly Dictionary<byte, SnapshotPair> Snapshots = [];
    private readonly Dictionary<byte, List<uint>> RecordedLapTimes = [];
    private readonly Dictionary<byte, (uint TotalMs, uint BestLapMs)> FinishTimes = [];

    public bool Active { get; private set; }
    public string TrackKey { get; private set; } = string.Empty;
    public int Laps { get; private set; }
    public TrackSurface? Surface { get; private set; }
    public KartStats? Stats { get; private set; }
    public IReadOnlyList<KartGateInfo> Gates { get; private set; } = [];
    public IReadOnlyList<(double X, double Y)> CentreLine { get; private set; } = [];
    public IReadOnlyDictionary<byte, KartRacerInfo> Racers { get; private set; } = new Dictionary<byte, KartRacerInfo>();
    public byte? LocalIndex { get; private set; }
    public KartRaceStage Stage { get; private set; } = KartRaceStage.Waiting;
    public uint StartStep { get; private set; }
    public uint GoStep { get; private set; }
    public uint LatestTick { get; private set; }
    public ushort LatestAck { get; private set; }

    public byte? IndexOfEntity(uint entityId)
        => Racers.Values.FirstOrDefault(racer => racer.EntityId == entityId)?.Index;

    public KartRacerSnapshot? Latest(byte index) => Snapshots.TryGetValue(index, out var pair) ? pair.Latest : null;

    public KartBody? Interpolated(byte index, double now)
    {
        if (!Snapshots.TryGetValue(index, out var pair))
            return null;

        if (pair.Previous is null)
            return ToBody(pair.Latest);

        var t = Math.Clamp((now - pair.LatestAt) * 15, 0, 1);
        var previous = pair.Previous;
        var latest = pair.Latest;
        var headingDelta = ((latest.Heading - previous.Heading + Math.PI) % (Math.PI * 2) + Math.PI * 2) % (Math.PI * 2) - Math.PI;

        return new KartBody
        {
            X = previous.X + (latest.X - previous.X) * t,
            Y = previous.Y + (latest.Y - previous.Y) * t,
            Vx = previous.Vx + (latest.Vx - previous.Vx) * t,
            Vy = previous.Vy + (latest.Vy - previous.Vy) * t,
            Heading = KartPhysics.NormalizeHeading(previous.Heading + headingDelta * t)
        };
    }

    public IReadOnlyList<uint> LapTimes(byte index)
        => RecordedLapTimes.TryGetValue(index, out var times) ? times : [];

    public (uint TotalMs, uint BestLapMs)? Finish(byte index)
        => FinishTimes.TryGetValue(index, out var finish) ? finish : null;

    public void ApplyLayout(KartTrackLayoutArgs args, uint localEntityId)
    {
        TrackKey = args.TrackKey;
        Laps = args.Laps;
        Surface = TrackSurface.FromRuns(args.Width, args.Height, args.Runs.Select(run => (run.Kind, run.Length)), args.Pads);
        Stats = KartStats.FromValues(args.Stats);
        Gates = args.Gates.ToArray();
        CentreLine = args.CentreLine.ToArray();
        Racers = args.Racers.ToDictionary(racer => racer.Index);
        LocalIndex = args.Racers.FirstOrDefault(racer => racer.EntityId == localEntityId)?.Index;
        Snapshots.Clear();
        RecordedLapTimes.Clear();
        FinishTimes.Clear();
        LatestTick = 0;
        LatestAck = 0;
        StartStep = 0;
        GoStep = 0;
        Stage = KartRaceStage.Waiting;
        Active = true;
    }

    public void ApplySnapshot(KartSnapshotArgs args, double now)
    {
        LatestTick = args.ServerTick;
        LatestAck = args.AckSequence;

        foreach (var racer in args.Racers)
        {
            if (Snapshots.TryGetValue(racer.Index, out var pair))
                Snapshots[racer.Index] = new SnapshotPair(pair.Latest, racer, now);
            else
                Snapshots[racer.Index] = new SnapshotPair(null, racer, now);
        }
    }

    public void ApplyEvent(KartRaceEventArgs args)
    {
        switch (args.Kind)
        {
            case KartRaceEventKind.Countdown:
                StartStep = args.StartStep;
                GoStep = args.GoStep;
                Stage = KartRaceStage.Countdown;
                break;
            case KartRaceEventKind.LapDone:
                if (!RecordedLapTimes.TryGetValue(args.Index, out var times))
                    RecordedLapTimes[args.Index] = times = [];
                times.Add(args.LapMs);
                break;
            case KartRaceEventKind.Finished:
                FinishTimes[args.Index] = (args.TotalMs, args.BestLapMs);
                Stage = KartRaceStage.Finishing;
                break;
            case KartRaceEventKind.RaceOver:
                Stage = KartRaceStage.Results;
                break;
        }
    }

    public KartRaceStage StageAt(uint clockStep)
        => Stage == KartRaceStage.Countdown && clockStep >= GoStep ? KartRaceStage.Racing : Stage;

    public void Clear()
    {
        Active = false;
        TrackKey = string.Empty;
        Laps = 0;
        Surface = null;
        Stats = null;
        Gates = [];
        CentreLine = [];
        Racers = new Dictionary<byte, KartRacerInfo>();
        LocalIndex = null;
        Stage = KartRaceStage.Waiting;
        StartStep = 0;
        GoStep = 0;
        LatestTick = 0;
        LatestAck = 0;
        Snapshots.Clear();
        RecordedLapTimes.Clear();
        FinishTimes.Clear();
    }

    public static KartBody ToBody(KartRacerSnapshot snapshot)
        => new() { X = snapshot.X, Y = snapshot.Y, Vx = snapshot.Vx, Vy = snapshot.Vy, Heading = snapshot.Heading };

    public static KartModifiers ToMods(KartRacerSnapshot snapshot)
        => new()
        {
            BoostSeconds = KartFixed.Seconds(snapshot.BoostTicks),
            StallSeconds = KartFixed.Seconds(snapshot.StallTicks),
            PadCooldownSeconds = KartFixed.Seconds(snapshot.PadCooldownTicks),
            DriftCharge = KartFixed.Seconds(snapshot.DriftChargeTicks),
            Held = snapshot.Flags.HasFlag(KartRacerFlags.Held),
            DriftDirection = snapshot.DriftDirection,
            Drifting = snapshot.DriftDirection != 0,
            BoostLevel = snapshot.BoostLevel
        };

    private sealed record SnapshotPair(KartRacerSnapshot? Previous, KartRacerSnapshot Latest, double LatestAt);
}
