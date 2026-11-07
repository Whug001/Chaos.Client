using Chaos.DarkAges.Definitions;
using Chaos.DarkAges.TurkeyShoot;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using static Chaos.DarkAges.TurkeyShoot.TurkeyShootRules;

namespace Chaos.Client.Controls.World.Popups.TurkeyShoot;

/// <summary>What a display message means for Biscuit and the sounds.</summary>
public enum TurkeyShootCue
{
    None,
    FlockTakeoff,
    GoldenTakeoff,
    TurkeyHit,
    GoldenHit,
    Miss,
    FlockEscaped,
    FlockCleared,
    Results
}

/// <summary>One turkey to draw this frame, in field units, with its sheet frame.</summary>
public readonly record struct VisibleTurkey(int Index, bool Golden, double X, double Y, bool FacesRight, int Frame);

/// <summary>
///     What the Turkey Shoot window shows: roster, scores, the flock's flights and the local flock clock. Plain logic
///     with no drawing. The server judges every shot; this side only plans the same flights and draws them.
/// </summary>
public sealed class TurkeyShootState
{
    public const int FLAP_FRAMES = 4;
    public const int FLAP_FRAME_MS = 90;
    public const int HIT_FRAME = 4;
    public const int FALL_FRAME = 5;
    public const int HIT_PUFF_MS = 150;

    /// <summary>Field units per second.</summary>
    public const int FALL_SPEED = 160;

    public const int MISS_PUFF_MS = 250;

    private readonly Dictionary<int, int> HitTimes = [];
    private readonly HashSet<int> LeftSlots = [];
    private readonly List<(short X, short Y, int AtMs)> Misses = [];
    private readonly Dictionary<int, int> ScoreBySlot = [];

    public IReadOnlyList<TurkeyShootHunterEntry> Roster { get; private set; } = [];
    public int YourSlot { get; private set; }
    public int FlockCount { get; private set; } = FLOCK_COUNT;
    public int FlockIndex { get; private set; } = -1;
    public IReadOnlyList<TurkeyFlight> Flights { get; private set; } = [];
    public int StartsInMs { get; private set; }
    public int FlockClockMs { get; private set; }
    public bool ClockRunning { get; private set; }
    public bool FlockOver { get; private set; }
    public int BoltsLeft { get; private set; }
    public int ReloadMs { get; private set; }
    public TurkeyShootDisplayArgs? Results { get; private set; }

    public string Title => FlockIndex < 0 ? "Harvest Turkey Shoot" : $"Harvest Turkey Shoot - Flock {FlockIndex + 1} of {FlockCount}";

    public static bool InField(int x, int y) => (x >= 0) && (x < FIELD_WIDTH) && (y >= 0) && (y < FIELD_HEIGHT);

    public int ScoreOf(int slot) => ScoreBySlot.GetValueOrDefault(slot);

    public bool HasLeft(int slot) => LeftSlots.Contains(slot);

    public string NameOf(int slot) => Roster.FirstOrDefault(hunter => hunter.Slot == slot)?.Name ?? "?";

    public void Open(TurkeyShootDisplayArgs args)
    {
        HitTimes.Clear();
        LeftSlots.Clear();
        Misses.Clear();
        ScoreBySlot.Clear();
        Roster = args.Roster.ToList();
        YourSlot = args.YourSlot;
        FlockCount = args.FlockCount;
        FlockIndex = -1;
        Flights = [];
        StartsInMs = 0;
        FlockClockMs = 0;
        ClockRunning = false;
        FlockOver = false;
        BoltsLeft = 0;
        ReloadMs = 0;
        Results = null;

        foreach (var hunter in Roster)
            ScoreBySlot[hunter.Slot] = 0;
    }

    public TurkeyShootCue Apply(TurkeyShootDisplayArgs args)
    {
        switch (args.Type)
        {
            case TurkeyShootDisplayType.FlockStart:
                return StartFlock(args);
            case TurkeyShootDisplayType.ShotResult:
                return ApplyShot(args);
            case TurkeyShootDisplayType.HunterLeft:
                LeftSlots.Add(args.Slot);

                return TurkeyShootCue.None;
            case TurkeyShootDisplayType.FlockOver:
                if (args.FlockIndex != FlockIndex)
                    return TurkeyShootCue.None;

                FlockOver = true;

                //Biscuit's tail-chase/flop happens only when the whole flock escapes with no hits.
                if ((args.EscapedCount > 0) && (HitTimes.Count == 0))
                    return TurkeyShootCue.FlockEscaped;

                return TurkeyShootCue.FlockCleared;
            case TurkeyShootDisplayType.Results:
                Results = args;
                FlockOver = true;

                foreach (var standing in args.Ranking)
                    ScoreBySlot[standing.Slot] = standing.Points;

                return TurkeyShootCue.Results;
            default:
                return TurkeyShootCue.None;
        }
    }

    public void Tick(int elapsedMs)
    {
        if (elapsedMs <= 0)
            return;

        ReloadMs = Math.Max(0, ReloadMs - elapsedMs);

        if (ClockRunning)
        {
            FlockClockMs += elapsedMs;

            return;
        }

        if (FlockIndex < 0)
            return;

        StartsInMs -= elapsedMs;

        if (StartsInMs > 0)
            return;

        //the overshoot past the lead belongs to the flock clock
        ClockRunning = true;
        FlockClockMs = -StartsInMs;
        StartsInMs = 0;
    }

    /// <summary>The shot to send, or null when the rules forbid one now. A refused shot spends nothing.</summary>
    public TurkeyShootActionArgs? TryShoot(int x, int y)
    {
        if (!ClockRunning || FlockOver || (BoltsLeft <= 0) || (ReloadMs > 0) || !InField(x, y) || HasLeft(YourSlot) || AllTurkeysGone())
            return null;

        BoltsLeft--;
        ReloadMs = RELOAD_MS;

        return new TurkeyShootActionArgs
        {
            Type = TurkeyShootActionType.Shoot,
            FlockIndex = (byte)FlockIndex,
            FlockTimeMs = FlockClockMs,
            AimX = (short)x,
            AimY = (short)y
        };
    }

    public IEnumerable<VisibleTurkey> VisibleTurkeys()
    {
        foreach (var flight in Flights)
        {
            if (HitTimes.TryGetValue(flight.Index, out var hitAt))
            {
                //a hit can arrive a little after the flight's end on this clock, so clamp to where it was last seen
                flight.TryGetPosition(Math.Clamp(hitAt, flight.TakeoffDelayMs, flight.EndMs), out var from);

                var since = FlockClockMs - hitAt;
                var y = from.Y + FALL_SPEED * Math.Max(0, since - HIT_PUFF_MS) / 1000.0;

                if (y > FIELD_HEIGHT + EXIT_MARGIN)
                    continue;

                yield return new VisibleTurkey(flight.Index, flight.Golden, from.X, y, flight.FacesRight, since < HIT_PUFF_MS ? HIT_FRAME : FALL_FRAME);

                continue;
            }

            if (flight.TryGetPosition(FlockClockMs, out var at))
                yield return new VisibleTurkey(flight.Index, flight.Golden, at.X, at.Y, flight.FacesRight, FlockClockMs / FLAP_FRAME_MS % FLAP_FRAMES);
        }
    }

    public IEnumerable<(short X, short Y)> VisibleMisses()
        => Misses.Where(miss => FlockClockMs - miss.AtMs < MISS_PUFF_MS).Select(miss => (miss.X, miss.Y));

    /// <summary>One hunter line for the results window, in display order.</summary>
    public readonly record struct Standing(int Slot, string Name, int Points, bool Gone, int? Place);

    public IReadOnlyList<Standing> Standings()
    {
        if (Results is null)
            return Roster.Select(hunter => new Standing(hunter.Slot, hunter.Name, ScoreOf(hunter.Slot), HasLeft(hunter.Slot), null)).ToList();

        var serverOrder = new Dictionary<int, int>();
        var leftInRanking = new HashSet<int>();

        foreach (var line in Results.Ranking)
        {
            serverOrder.TryAdd(line.Slot, serverOrder.Count);

            if (line.Left)
                leftInRanking.Add(line.Slot);
        }

        var ordered = Roster.Select((hunter, index) => (Hunter: hunter, RosterIndex: index, Score: ScoreOf(hunter.Slot)))
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => serverOrder.TryGetValue(entry.Hunter.Slot, out var order) ? order : int.MaxValue)
            .ThenBy(entry => entry.RosterIndex)
            .ToList();

        var standings = new List<Standing>(ordered.Count);
        var place = 0;
        var lastScore = int.MinValue;

        foreach (var (hunter, _, score) in ordered)
        {
            if (score != lastScore)
            {
                place = standings.Count + 1;
                lastScore = score;
            }

            standings.Add(new Standing(hunter.Slot, hunter.Name, score, HasLeft(hunter.Slot) || leftInRanking.Contains(hunter.Slot), place));
        }

        return standings;
    }

    public string ResultHeadline()
    {
        if (Results is null)
            return string.Empty;

        var winners = Results.WinnerSlots.Select(slot => (int)slot).ToList();

        return winners.Count switch
        {
            0 => "No winner this time.",
            1 when Results.CountsAsWin is false => $"{NameOf(winners[0])} scored {Points(ScoreOf(winners[0]))}.",
            1 => $"{NameOf(winners[0])} wins with {Points(ScoreOf(winners[0]))}!",
            _ => $"A shared win: {string.Join(" and ", winners.Select(NameOf))}!"
        };
    }

    public string ResultNote() => Results is { CountsAsWin: false } ? "A lone hunt earns prize points, not wins." : string.Empty;

    private static string Points(int points) => points == 1 ? "1 point" : $"{points} points";

    private TurkeyShootCue StartFlock(TurkeyShootDisplayArgs args)
    {
        if ((Results is not null) || (args.FlockIndex <= FlockIndex))
            return TurkeyShootCue.None;

        FlockIndex = args.FlockIndex;
        Flights = TurkeyFlightPlanner.Plan(args.Seed, args.FlockIndex, args.HuntersAtStart);
        HitTimes.Clear();
        Misses.Clear();
        StartsInMs = args.StartsInMs;
        FlockClockMs = 0;
        ClockRunning = StartsInMs <= 0;
        FlockOver = false;
        BoltsLeft = HasLeft(YourSlot) ? 0 : BOLTS_PER_FLOCK;
        ReloadMs = 0;

        return Flights.Any(flight => flight.Golden) ? TurkeyShootCue.GoldenTakeoff : TurkeyShootCue.FlockTakeoff;
    }

    private TurkeyShootCue ApplyShot(TurkeyShootDisplayArgs args)
    {
        ScoreBySlot[args.Slot] = args.NewScore;

        if (args.FlockIndex != FlockIndex)
            return TurkeyShootCue.None;

        if (args.TurkeyIndex < 0)
        {
            Misses.Add((args.AimX, args.AimY, FlockClockMs));

            return TurkeyShootCue.Miss;
        }

        if (args.TurkeyIndex >= Flights.Count)
            return TurkeyShootCue.None;

        if (HitTimes.ContainsKey(args.TurkeyIndex))
            return TurkeyShootCue.None;

        HitTimes.TryAdd(args.TurkeyIndex, FlockClockMs);

        return Flights[args.TurkeyIndex].Golden ? TurkeyShootCue.GoldenHit : TurkeyShootCue.TurkeyHit;
    }

    private bool AllTurkeysGone() => Flights.All(flight => HitTimes.ContainsKey(flight.Index) || (FlockClockMs > flight.EndMs));
}
