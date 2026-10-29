#region
using Chaos.DarkAges.Karting;
using Chaos.Networking.Entities.Server;
using System.Globalization;
#endregion

namespace Chaos.Client.ViewModel;

public sealed class KartRaceState
{
    private const uint RACER_SNAPSHOT_TIMEOUT_STEPS = 30;
    private readonly Dictionary<byte, KartRacerSnapshot> Snapshots = [];
    private readonly Dictionary<byte, uint> LastSnapshotTicks = [];
    private readonly Dictionary<byte, List<uint>> RecordedLapTimes = [];
    private readonly Dictionary<byte, (uint TotalMs, uint BestLapMs)> FinishTimes = [];

    public IReadOnlyList<KartTimedHazard> Hazards { get; private set; } = [];
    public IReadOnlyList<(double X, double Y)> Caches { get; private set; } = [];
    public IReadOnlyList<(double X, double Y)> WispSpots { get; private set; } = [];
    public uint CacheBits { get; private set; }
    public ulong WispBits { get; private set; }
    public IReadOnlyList<KartLooseWispInfo> Loose { get; private set; } = [];
    public IReadOnlyList<KartItemObjectInfo> Objects { get; private set; } = [];
    public KartItemKind HeldItem { get; private set; }
    public KartItemKind RollItem { get; private set; }
    public byte RollTicks { get; private set; }
    public byte CooldownTicks { get; private set; }

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
    public IReadOnlyList<KartResultEntry> Results { get; private set; } = [];

    public IReadOnlyList<KartRacerSnapshot> Standings
        => Snapshots.Values.Where(racer => racer.Place > 0).OrderBy(racer => racer.Place).ThenBy(racer => racer.Index).ToList();

    public bool LocalFinished
        => LocalIndex is { } index
           && (Finish(index) is not null || Latest(index)?.Status is KartRacerStatus.Finished or KartRacerStatus.Dnf);

    public bool Spectating => Active && (LocalIndex is null || LocalFinished);

    public bool ShouldHideEntity(byte index)
        => Snapshots.TryGetValue(index, out var racer) && racer.Status != KartRacerStatus.Dnf;

    public byte? IndexOfEntity(uint entityId)
        => Racers.Values.FirstOrDefault(racer => racer.EntityId == entityId)?.Index;

    public KartRacerSnapshot? Latest(byte index) => Snapshots.GetValueOrDefault(index);

    public IReadOnlyList<uint> LapTimes(byte index)
        => RecordedLapTimes.TryGetValue(index, out var times) ? times : [];

    public (uint TotalMs, uint BestLapMs)? Finish(byte index)
        => FinishTimes.TryGetValue(index, out var finish) ? finish : null;

    public (KartRacerSnapshot? Ahead, KartRacerSnapshot? Behind) Neighbours(byte index)
        => Neighbours(index, Standings);

    public (KartRacerSnapshot? Ahead, KartRacerSnapshot? Behind) Neighbours(byte index, IReadOnlyList<KartRacerSnapshot> standings)
    {
        var activeStandings = standings.Where(racer => racer.Status != KartRacerStatus.Dnf).ToArray();

        for (var i = 0; i < activeStandings.Length; i++)
            if (activeStandings[i].Index == index)
                return (i > 0 ? activeStandings[i - 1] : null, i < activeStandings.Length - 1 ? activeStandings[i + 1] : null);

        return (null, null);
    }

    public bool ShouldCaptureFinish(KartRaceEventArgs args)
        => !LocalFinished
           && args.Kind == KartRaceEventKind.Finished
           && LocalIndex == args.Index;

    public bool ShouldCaptureFinish(KartSnapshotArgs args)
        => !LocalFinished
           && LocalIndex is { } index
           && args.Racers.FirstOrDefault(racer => racer.Index == index)?.Status is KartRacerStatus.Finished or KartRacerStatus.Dnf;

    public IReadOnlyList<byte> DrawableIndexes()
        => Snapshots.Values.Where(racer => racer.Status != KartRacerStatus.Dnf).Select(racer => racer.Index).Order().ToList();

    public IReadOnlyList<byte> SpectateCandidates()
        => Snapshots.Values.Where(racer => racer.Status != KartRacerStatus.Dnf)
                           .OrderBy(racer => racer.Place == 0 ? int.MaxValue : racer.Place)
                           .ThenBy(racer => racer.Index)
                           .Select(racer => racer.Index)
                           .ToList();

    public bool ApplyLayout(KartTrackLayoutArgs args, uint localEntityId)
    {
        var sameRace = Active && TrackKey == args.TrackKey;
        TrackKey = args.TrackKey;
        Laps = args.Laps;
        Surface = TrackSurface.FromRuns(args.Width, args.Height, args.Runs.Select(run => (run.Kind, run.Length)), args.Pads);
        Stats = KartStats.FromValues(args.Stats);
        Gates = args.Gates.ToArray();
        CentreLine = args.CentreLine.ToArray();
        Hazards = args.Hazards.ToArray();
        Caches = args.Caches.ToArray();
        WispSpots = args.WispSpots.ToArray();
        ClearItems();
        Racers = args.Racers.ToDictionary(racer => racer.Index);
        LocalIndex = args.Racers.FirstOrDefault(racer => racer.EntityId == localEntityId)?.Index;
        Snapshots.Clear();
        LastSnapshotTicks.Clear();
        LatestTick = 0;
        LatestAck = 0;

        if (!sameRace)
        {
            RecordedLapTimes.Clear();
            FinishTimes.Clear();
            Results = [];
            StartStep = 0;
            GoStep = 0;
            Stage = KartRaceStage.Waiting;
        }

        Active = true;

        return !sameRace;
    }

    public void ApplySnapshot(KartSnapshotArgs args)
    {
        LatestTick = args.ServerTick;
        LatestAck = args.AckSequence;
        CacheBits = args.CacheBits;
        WispBits = args.WispBits;
        Loose = args.Loose.ToArray();
        Objects = args.Objects.ToArray();
        HeldItem = args.HeldItem;
        RollItem = args.RollItem;
        RollTicks = args.RollTicks;
        CooldownTicks = args.CooldownTicks;

        var received = args.Racers.Select(racer => racer.Index).ToHashSet();

        foreach (var index in Snapshots.Keys.ToArray())
            if (!received.Contains(index)
                && LastSnapshotTicks.TryGetValue(index, out var lastSeen)
                && TickElapsed(args.ServerTick, lastSeen, RACER_SNAPSHOT_TIMEOUT_STEPS))
            {
                Snapshots.Remove(index);
                LastSnapshotTicks.Remove(index);
            }

        foreach (var racer in args.Racers)
        {
            Snapshots[racer.Index] = racer;
            LastSnapshotTicks[racer.Index] = args.ServerTick;
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
            case KartRaceEventKind.Results:
                Results = args.Results.ToArray();
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
        Hazards = [];
        Caches = [];
        WispSpots = [];
        ClearItems();
        Racers = new Dictionary<byte, KartRacerInfo>();
        LocalIndex = null;
        Stage = KartRaceStage.Waiting;
        StartStep = 0;
        GoStep = 0;
        LatestTick = 0;
        LatestAck = 0;
        Results = [];
        Snapshots.Clear();
        LastSnapshotTicks.Clear();
        RecordedLapTimes.Clear();
        FinishTimes.Clear();
    }

    private static bool TickElapsed(uint current, uint previous, uint threshold)
    {
        var elapsed = unchecked(current - previous);

        return (elapsed >= threshold) && (elapsed < 0x80000000);
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
            BoostLevel = snapshot.BoostLevel,
            SpinSeconds = KartFixed.Seconds(snapshot.SpinTicks),
            MantleSeconds = KartFixed.Seconds(snapshot.MantleTicks),
            SlowSeconds = KartFixed.Seconds(snapshot.SlowTicks),
            Wisps = snapshot.Wisps
        };

    public static string PlaceText(byte place, int count)
        => place == 0 ? string.Empty : $"{KartTime.Ordinal(place)} / {count}";

    public static string GapText(ushort fromCs, ushort toCs)
    {
        var difference = toCs - fromCs;

        return string.Format(CultureInfo.InvariantCulture, "{0}{1:0.00}", difference < 0 ? "-" : "+", Math.Abs(difference) / 100.0);
    }

    public static string? BannerFor(KartRaceEventArgs args, byte? localIndex, int laps)
    {
        if (localIndex is not { } index || args.Index != index)
            return null;

        return args.Kind switch
        {
            KartRaceEventKind.LapDone when laps >= 2 && args.Lap == laps - 1 => "FINAL LAP",
            KartRaceEventKind.Finished => "FINISHED",
            _ => null
        };
    }

    public static double BannerSeconds(string banner) => banner == "FINISHED" ? 3 : 2;
    private void ClearItems()
    {
        CacheBits = 0;
        WispBits = 0;
        Loose = [];
        Objects = [];
        HeldItem = KartItemKind.None;
        RollItem = KartItemKind.None;
        RollTicks = 0;
        CooldownTicks = 0;
    }

    public bool IsLocalGloamingHit(KartRaceEventArgs args)
        => (args.Kind == KartRaceEventKind.Hit) && (args.Item == KartItemKind.Gloaming) && (args.Index == LocalIndex);

    /// <summary>The Gloaming's black overlay: alpha 0.5 at the hit, fading to 0 at <paramref name="until" />.</summary>
    public static float GloamingAlpha(double until, double now) => (float)Math.Clamp(0.5 * (until - now), 0, 0.5);
}
