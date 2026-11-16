#region
using Chaos.DarkAges.Definitions;
using Chaos.Geometry.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>What a battle unit is doing at the clock: sliding, leaping, attacking or fading.</summary>
public enum TamerBattleAction : byte
{
    Idle = 0,
    Walk = 1,
    Attack = 2,
    Leap = 3,
    Fading = 4
}

/// <summary>The flash over a battle unit: gold for a trait or set bonus.</summary>
public enum TamerBattleFlash : byte
{
    None = 0,
    Gold = 1
}

/// <summary>The colour kind of a floating battle number: red, green or white.</summary>
public enum TamerBattleFloaterKind : byte
{
    Damage = 0,
    Heal = 1,
    Shield = 2
}

/// <summary>One battle unit at the playback clock. Draw positions and facings are in view coordinates.</summary>
public sealed record TamerBattleUnitState(
    byte Id,
    TamerBattleSide Side,
    string Name,
    byte Level,
    ushort Sprite,
    TamerWireElement Element,
    bool Alive,
    int Health,
    int MaxHealth,
    int Shield,
    int Energy,
    IReadOnlyList<string> StatusKeys,
    int EngineColumn,
    int EngineRow,
    int ViewColumn,
    int ViewRow,
    double DrawX,
    double DrawY,
    Direction Facing,
    TamerBattleAction Action,
    double ActionProgress,
    TamerBattleFlash Flash,
    byte Range,
    string? AbilityName,
    double AbilityProgress)
{
    public bool IsDown => !Alive;

    public bool IsRanged => Range > 1;
}

/// <summary>A number floating up from a target, in view cell coordinates, with its age in seconds.</summary>
public sealed record TamerBattleFloater(
    int Amount,
    TamerBattleFloaterKind Kind,
    int ViewColumn,
    int ViewRow,
    double Age);

/// <summary>A ranged attack dot travelling from one view cell to another, with 0-to-1 progress.</summary>
public sealed record TamerBattleProjectile(
    double FromX,
    double FromY,
    double ToX,
    double ToY,
    double Progress);

/// <summary>
///     The state of every monster at the playback clock, the log, the clock, pause, speed, skip, replay and the
///     board turn. Events apply incrementally from the last applied index while the clock moves forward; a full
///     rebuild happens only on Replay, Skip and RebuildFromScratch. After any sequence of Advance, Skip and
///     Replay the state equals a fresh playback advanced to the same clock. Never touches a texture. Pure.
/// </summary>
public sealed class TamerBattlePlayback
{
    public const int BOARD_WIDTH = 7;
    public const int BOARD_DEPTH = 6;
    public const int TICKS_PER_SECOND = 10;
    public const int RISING_START_TICK = 450;
    public const int RISING_PER_SECOND = 10;
    public const int ANIMATION_TAIL_TICKS = 10;

    private const int WALK_MAX_TICKS = 4;
    private const int LEAP_TICKS = 2;
    private const int ATTACK_TICKS = 4;
    private const int FADE_TICKS = 5;
    private const int ABILITY_TICKS = 10;
    private const int FLOATER_TICKS = 10;
    private const int PROJECTILE_TICKS = 3;
    private const int FLASH_TICKS = 5;

    private readonly Dictionary<string, string> NameMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly int[] StepDurations;
    private readonly int? risingStartTick;
    private readonly int risingPerSecond;
    private List<TamerBattleUnitState> UnitList = [];
    private List<TamerBattleFloater> FloaterList = [];
    private List<TamerBattleProjectile> ProjectileList = [];
    private List<string> LogList = [];
    private double animationClock;

    //the incremental simulation: mutable units plus the candidates, log lines and bonus keys built so far, and the
    //count of events already applied. events arrive in tick order, so a forward Advance resumes from the last
    //applied index instead of replaying every event from tick 0.
    private readonly Dictionary<byte, MutableUnit> AppliedUnits = new();
    private readonly List<byte> AppliedOrder = [];
    private readonly List<FloaterCandidate> AppliedFloaters = [];
    private readonly List<ProjectileCandidate> AppliedProjectiles = [];
    private readonly List<string> AppliedLogs = [];
    private readonly HashSet<(TamerBattleSide Side, string Key)> AppliedBonuses = new();
    private int AppliedEventCount;

    public TamerBattlePlayback(TamerBattleFight fight)
    {
        Fight = fight ?? throw new ArgumentNullException(nameof(fight));
        ClockTicks = 0;
        animationClock = 0;
        Speed = 1;

        var names = fight.Names ?? [];
        foreach (var entry in names)
        {
            if ((entry is not null) && !string.IsNullOrEmpty(entry.Key) && !NameMap.ContainsKey(entry.Key))
                NameMap[entry.Key] = entry.Name ?? entry.Key;
        }

        var events = fight.Events ?? [];
        StepDurations = ComputeStepDurations(events);
        risingStartTick = FindRisingStart(events);
        risingPerSecond = RISING_PER_SECOND;

        RebuildFromScratch();
    }

    public TamerBattleFight Fight { get; }

    public double ClockTicks { get; private set; }

    /// <summary>The animation clock: runs for a short tail past <see cref="ClockTicks" /> so animations starting on the end tick can finish. Event application stays clamped to <see cref="ClockTicks" />.</summary>
    public double AnimationTicks => animationClock;

    public bool Paused { get; set; }

    public int Speed { get; private set; }

    public bool Finished => ClockTicks >= (Fight.EndTick - 1e-9);

    /// <summary>
    ///     The fight is over and the animation tail after the end tick has run out too, so the last hit and fade have
    ///     been seen. False at the end tick itself; true at once after <see cref="Skip" />.
    /// </summary>
    public bool TailFinished => Finished && (animationClock >= (Fight.EndTick + ANIMATION_TAIL_TICKS - 1e-9));

    public IReadOnlyList<TamerBattleUnitState> Units => UnitList;

    public IReadOnlyList<TamerBattleFloater> Floaters => FloaterList;

    public IReadOnlyList<TamerBattleProjectile> Projectiles => ProjectileList;

    public IReadOnlyList<string> LogLines => LogList;

    public int RisingPercent => ComputeRising(ClockTicks);

    public string ResultTitle => TamerBattleText.ResultTitle(Fight.ViewerSide, Fight.Winner, Fight.AttackerName, Fight.DefenderName);

    public string ResultReason => TamerBattleText.ResultReason(Fight.Reason);

    public IReadOnlyList<string> ResultLines => Fight.ResultLines ?? [];

    /// <summary>Advances the clock by real elapsed time multiplied by speed. The displayed clock clamps to the end tick, while the animation clock keeps running for a short tail so animations starting on the end tick can finish. No-op while paused or zero.</summary>
    public void Advance(TimeSpan realElapsed)
    {
        if (Paused || (realElapsed <= TimeSpan.Zero))
            return;

        var tailEnd = Fight.EndTick + ANIMATION_TAIL_TICKS;

        if ((ClockTicks >= (Fight.EndTick - 1e-9)) && (animationClock >= (tailEnd - 1e-9)))
            return;

        var deltaTicks = (realElapsed.TotalSeconds * TICKS_PER_SECOND * Speed);
        ClockTicks = Math.Min(Fight.EndTick, ClockTicks + deltaTicks);
        animationClock = Math.Min(tailEnd, animationClock + deltaTicks);

        ApplyPendingEvents();
    }

    /// <summary>Cycles speed through 1x, 2x, 4x, 1x. Returns the new speed.</summary>
    public int CycleSpeed()
    {
        Speed = Speed switch { 1 => 2, 2 => 4, _ => 1 };
        return Speed;
    }

    /// <summary>Jumps to the final state at once, including the end of the animation tail.</summary>
    public void Skip()
    {
        ClockTicks = Fight.EndTick;
        animationClock = Fight.EndTick + ANIMATION_TAIL_TICKS;
        RebuildFromScratch();
    }

    /// <summary>Starts again from tick 0, unpaused.</summary>
    public void Replay()
    {
        ClockTicks = 0;
        animationClock = 0;
        Paused = false;
        RebuildFromScratch();
    }

    /// <summary>The one board-turn mapping, used for every position and facing. Defender turns 180; others identity.</summary>
    public (int Column, int Row) ViewCell(int column, int row)
    {
        return Fight.ViewerSide == TamerBattleViewerSide.Defender
            ? (BOARD_WIDTH - 1 - column, BOARD_DEPTH - 1 - row)
            : (column, row);
    }

    /// <summary>
    ///     True for the side drawn at the bottom of the board and in the left roster: the viewer's own squad, or the
    ///     attacker when the viewer took no part.
    /// </summary>
    public bool IsNearSide(TamerBattleSide side)
        => side == (Fight.ViewerSide == TamerBattleViewerSide.Defender ? TamerBattleSide.Defender : TamerBattleSide.Attacker);

    private int ComputeRising(double clockTicks)
    {
        if (risingStartTick is not { } start)
            return 100;

        if (clockTicks < start)
            return 100;

        var steps = (int)Math.Floor((clockTicks - start) / 10.0);
        return 100 + (risingPerSecond * steps);
    }

    private static int? FindRisingStart(IReadOnlyList<TamerBattleEventView> events)
    {
        //The engine emits one RisingDamage at the start tick with Amount equal to the percent at that tick
        //(100), not the per-second rise, so the start comes from Tick and the rate stays the default.
        foreach (var ev in events)
        {
            if ((ev is not null) && (ev.Type == TamerBattleEventType.RisingDamage))
                return ev.Tick;
        }

        return null;
    }

    private static int[] ComputeStepDurations(IReadOnlyList<TamerBattleEventView> events)
    {
        var durations = new int[events.Count];

        for (var i = 0; i < events.Count; i++)
        {
            var ev = events[i];

            if ((ev is null) || (ev.Type != TamerBattleEventType.Step))
            {
                durations[i] = 1;
                continue;
            }

            ushort? next = null;

            for (var j = i + 1; j < events.Count; j++)
            {
                var later = events[j];

                if (later is not null && later.UnitId == ev.UnitId)
                {
                    next = later.Tick;
                    break;
                }
            }

            if (next is null)
                durations[i] = WALK_MAX_TICKS;
            else
            {
                var gap = (int)next.Value - ev.Tick;
                durations[i] = gap <= 0 ? 1 : Math.Min(gap, WALK_MAX_TICKS);
            }
        }

        return durations;
    }

    private sealed class MutableUnit
    {
        public required byte Id;
        public required TamerBattleSide Side;
        public required string Name;
        public required byte Level;
        public required ushort Sprite;
        public required TamerWireElement Element;
        public required int MaxHealth;
        public required byte Range;
        public int EngineColumn;
        public int EngineRow;
        public int Health;
        public int Shield;
        public int Energy;
        public HashSet<string> Statuses = new(StringComparer.OrdinalIgnoreCase);
        public bool Alive = true;
        public Direction Facing;
        public TamerBattleAction Action = TamerBattleAction.Idle;
        public int ActionStart;
        public int ActionDuration = 1;
        public int FromColumn;
        public int FromRow;
        public int? AbilityTick;
        public string? AbilityLabel;
        public int? FlashTick;
    }

    private sealed record FloaterCandidate(int Tick, int Amount, TamerBattleFloaterKind Kind, int ViewColumn, int ViewRow);

    private sealed record ProjectileCandidate(int Tick, double FromX, double FromY, double ToX, double ToY);

    /// <summary>
    ///     Reapplies every event from tick 0 up to the clock, rebuilding the simulation from scratch. Used after any
    ///     clock discontinuity (<see cref="Replay" />, <see cref="Skip" />), and by tests as the full-rebuild oracle
    ///     for the incremental path: only a continuous forward <see cref="Advance" /> may reuse applied state.
    /// </summary>
    public void RebuildFromScratch()
    {
        ResetApplied();
        ApplyPendingEvents();
    }

    private void ResetApplied()
    {
        AppliedUnits.Clear();
        AppliedOrder.Clear();
        AppliedFloaters.Clear();
        AppliedProjectiles.Clear();
        AppliedLogs.Clear();
        AppliedBonuses.Clear();
        AppliedEventCount = 0;

        foreach (var monster in Fight.Monsters ?? [])
        {
            if ((monster is null) || AppliedUnits.ContainsKey(monster.UnitId))
                continue;

            var maxHealth = Math.Max(0, monster.MaxHealth);

            //the start's cells are engine board cells; they are a fallback only.
            //every board position comes from the Spawn events.
            var mutable = new MutableUnit
            {
                Id = monster.UnitId,
                Side = monster.Side,
                Name = monster.DisplayName ?? string.Empty,
                Level = monster.Level,
                Sprite = monster.Sprite,
                Element = monster.Element,
                MaxHealth = maxHealth,
                Range = monster.Range,
                EngineColumn = Math.Clamp((int)monster.StartColumn, 0, BOARD_WIDTH - 1),
                EngineRow = Math.Clamp((int)monster.StartRow, 0, BOARD_DEPTH - 1),
                Health = maxHealth,
                Shield = 0,
                Energy = 0,
                Facing = InitialFacing(monster.Side)
            };

            AppliedUnits[monster.UnitId] = mutable;
            AppliedOrder.Add(monster.UnitId);
        }
    }

    /// <summary>
    ///     Applies every not-yet-applied event up to the clock, then re-derives the visible state. Events arrive in
    ///     tick order, so application resumes from the last applied index and stops at the first future event.
    /// </summary>
    private void ApplyPendingEvents()
    {
        var events = Fight.Events ?? [];

        for (; AppliedEventCount < events.Count; AppliedEventCount++)
        {
            var ev = events[AppliedEventCount];

            if (ev is null)
                continue;

            if (((int)ev.Tick) > ClockTicks + 1e-9)
                break;

            if (!Enum.IsDefined(ev.Type))
                continue;

            ApplyEvent(ev, AppliedEventCount);
        }

        Derive();
    }

    private void ApplyEvent(TamerBattleEventView ev, int index)
    {
        //aliases so the per-event application below reads as the simulation it is
        var units = AppliedUnits;
        var logs = AppliedLogs;
        var floaters = AppliedFloaters;
        var projectiles = AppliedProjectiles;
        var loggedBonuses = AppliedBonuses;

        switch (ev.Type)
            {
                case TamerBattleEventType.Spawn:
                {
                    if (!units.TryGetValue(ev.UnitId, out var unit))
                        break;

                    if ((ev.X < 0) || (ev.X > BOARD_WIDTH - 1) || (ev.Y < 0) || (ev.Y > BOARD_DEPTH - 1))
                        break;

                    unit.EngineColumn = ev.X;
                    unit.EngineRow = ev.Y;

                    if (ev.Amount > 0)
                        unit.Health = Math.Min(ev.Amount, unit.MaxHealth);

                    break;
                }
                case TamerBattleEventType.Step:
                {
                    if (!units.TryGetValue(ev.UnitId, out var unit))
                        break;

                    if ((ev.X < 0) || (ev.X > BOARD_WIDTH - 1) || (ev.Y < 0) || (ev.Y > BOARD_DEPTH - 1))
                        break;

                    if (!unit.Alive)
                        break;

                    unit.FromColumn = unit.EngineColumn;
                    unit.FromRow = unit.EngineRow;
                    unit.EngineColumn = ev.X;
                    unit.EngineRow = ev.Y;
                    unit.Action = TamerBattleAction.Walk;
                    unit.ActionStart = ev.Tick;
                    unit.ActionDuration = StepDurations[index];
                    unit.Facing = MovementFacing(unit.FromColumn, unit.FromRow, ev.X, ev.Y);
                    break;
                }
                case TamerBattleEventType.Leap:
                {
                    if (!units.TryGetValue(ev.UnitId, out var unit))
                        break;

                    if ((ev.X < 0) || (ev.X > BOARD_WIDTH - 1) || (ev.Y < 0) || (ev.Y > BOARD_DEPTH - 1))
                        break;

                    if (!unit.Alive)
                        break;

                    unit.FromColumn = unit.EngineColumn;
                    unit.FromRow = unit.EngineRow;
                    unit.EngineColumn = ev.X;
                    unit.EngineRow = ev.Y;
                    unit.Action = TamerBattleAction.Leap;
                    unit.ActionStart = ev.Tick;
                    unit.ActionDuration = LEAP_TICKS;
                    unit.Facing = MovementFacing(unit.FromColumn, unit.FromRow, ev.X, ev.Y);
                    break;
                }
                case TamerBattleEventType.Attack:
                {
                    if (!units.TryGetValue(ev.UnitId, out var attacker))
                        break;

                    if ((ev.TargetId == 0) || !units.TryGetValue(ev.TargetId, out var target))
                        break;

                    if (!attacker.Alive)
                        break;

                    if (ev.Energy != -1)
                        attacker.Energy = Math.Clamp((int)ev.Energy, 0, 100);

                    attacker.Facing = TargetFacing(attacker, target);
                    attacker.Action = TamerBattleAction.Attack;
                    attacker.ActionStart = ev.Tick;
                    attacker.ActionDuration = ATTACK_TICKS;

                    if (attacker.Range > 1)
                    {
                        var (fromColumn, fromRow) = ViewCell(attacker.EngineColumn, attacker.EngineRow);
                        var (toColumn, toRow) = ViewCell(target.EngineColumn, target.EngineRow);
                        projectiles.Add(new ProjectileCandidate(ev.Tick, fromColumn, fromRow, toColumn, toRow));
                    }

                    break;
                }
                case TamerBattleEventType.Ability:
                {
                    if (!units.TryGetValue(ev.UnitId, out var caster))
                        break;

                    if (!caster.Alive)
                        break;

                    if (ev.Energy != -1)
                        caster.Energy = Math.Clamp((int)ev.Energy, 0, 100);

                    if ((ev.TargetId != 0) && units.TryGetValue(ev.TargetId, out var abilityTarget))
                        caster.Facing = TargetFacing(caster, abilityTarget);

                    caster.Action = TamerBattleAction.Attack;
                    caster.ActionStart = ev.Tick;
                    caster.ActionDuration = ATTACK_TICKS;

                    var abilityName = string.IsNullOrEmpty(ev.Key) ? "ability" : TamerBattleText.DisplayName(ev.Key, NameMap);
                    caster.AbilityTick = ev.Tick;
                    caster.AbilityLabel = abilityName;
                    logs.Add(TamerBattleText.AbilityLine(caster.Name, abilityName));
                    break;
                }
                case TamerBattleEventType.Damage:
                {
                    if ((ev.TargetId == 0) || !units.TryGetValue(ev.TargetId, out var target))
                        break;

                    if ((ev.UnitId != 0) && !units.TryGetValue(ev.UnitId, out _))
                        break;

                    if (!target.Alive || (ev.Amount < 0))
                        break;

                    var absorbed = Math.Min(target.Shield, ev.Amount);
                    target.Shield -= absorbed;
                    var remaining = ev.Amount - absorbed;
                    target.Health = Math.Max(0, target.Health - remaining);

                    if (target.Shield <= 0)
                    {
                        target.Shield = 0;
                        target.Statuses.Remove("Shield");
                    }

                    if (ev.Energy != -1)
                        target.Energy = Math.Clamp((int)ev.Energy, 0, 100);

                    if (ev.Amount != 0)
                    {
                        var (viewColumn, viewRow) = ViewCell(target.EngineColumn, target.EngineRow);
                        floaters.Add(new FloaterCandidate(ev.Tick, ev.Amount, TamerBattleFloaterKind.Damage, viewColumn, viewRow));
                    }

                    break;
                }
                case TamerBattleEventType.Heal:
                {
                    if ((ev.TargetId == 0) || !units.TryGetValue(ev.TargetId, out var target))
                        break;

                    if ((ev.UnitId != 0) && !units.TryGetValue(ev.UnitId, out _))
                        break;

                    if (!target.Alive || (ev.Amount < 0))
                        break;

                    target.Health = Math.Min(target.MaxHealth, target.Health + ev.Amount);

                    if (ev.Amount != 0)
                    {
                        var (viewColumn, viewRow) = ViewCell(target.EngineColumn, target.EngineRow);
                        floaters.Add(new FloaterCandidate(ev.Tick, ev.Amount, TamerBattleFloaterKind.Heal, viewColumn, viewRow));
                    }

                    break;
                }
                case TamerBattleEventType.Shield:
                {
                    if ((ev.TargetId == 0) || !units.TryGetValue(ev.TargetId, out var target))
                        break;

                    if ((ev.UnitId != 0) && !units.TryGetValue(ev.UnitId, out _))
                        break;

                    if (!target.Alive || (ev.Amount < 0))
                        break;

                    target.Shield = ev.Amount;

                    if (ev.Amount > 0)
                        target.Statuses.Add("Shield");
                    else
                        target.Statuses.Remove("Shield");

                    if (ev.Amount != 0)
                    {
                        var (viewColumn, viewRow) = ViewCell(target.EngineColumn, target.EngineRow);
                        floaters.Add(new FloaterCandidate(ev.Tick, ev.Amount, TamerBattleFloaterKind.Shield, viewColumn, viewRow));
                    }

                    break;
                }
                case TamerBattleEventType.StatusOn:
                {
                    if ((ev.TargetId == 0) || !units.TryGetValue(ev.TargetId, out var target))
                        break;

                    if (string.IsNullOrEmpty(ev.Key))
                        break;

                    if (!target.Alive && !ev.Key.Equals("Shield", StringComparison.OrdinalIgnoreCase))
                        break;

                    target.Statuses.Add(ev.Key);

                    if (ev.Key.Equals("Shield", StringComparison.OrdinalIgnoreCase) && (ev.Amount > 0))
                        target.Shield = ev.Amount;

                    break;
                }
                case TamerBattleEventType.StatusOff:
                {
                    if ((ev.TargetId == 0) || !units.TryGetValue(ev.TargetId, out var target))
                        break;

                    if (string.IsNullOrEmpty(ev.Key))
                        break;

                    target.Statuses.Remove(ev.Key);

                    if (ev.Key.Equals("Shield", StringComparison.OrdinalIgnoreCase))
                        target.Shield = 0;

                    break;
                }
                case TamerBattleEventType.Trait:
                {
                    if (!units.TryGetValue(ev.UnitId, out var owner))
                        break;

                    if (!owner.Alive || string.IsNullOrEmpty(ev.Key))
                        break;

                    owner.FlashTick = ev.Tick;
                    logs.Add(TamerBattleText.TraitLine(owner.Name, TamerBattleText.DisplayName(ev.Key, NameMap)));
                    break;
                }
                case TamerBattleEventType.SetBonus:
                {
                    if (!units.TryGetValue(ev.UnitId, out var unit))
                        break;

                    if (!unit.Alive || string.IsNullOrEmpty(ev.Key))
                        break;

                    unit.FlashTick = ev.Tick;

                    //The engine emits one SetBonus per affected unit; the log says it once per side and bonus.
                    if (loggedBonuses.Add((unit.Side, ev.Key)))
                    {
                        var squadName = unit.Side == TamerBattleSide.Attacker ? Fight.AttackerName : Fight.DefenderName;
                        logs.Add(TamerBattleText.SetBonusLine(squadName, TamerBattleText.DisplayName(ev.Key, NameMap)));
                    }

                    break;
                }
                case TamerBattleEventType.Down:
                {
                    if (!units.TryGetValue(ev.UnitId, out var unit))
                        break;

                    if (!unit.Alive)
                        break;

                    unit.Alive = false;
                    unit.Health = 0;
                    unit.Shield = 0;
                    unit.Statuses.Clear();
                    unit.Action = TamerBattleAction.Fading;
                    unit.ActionStart = ev.Tick;
                    unit.ActionDuration = FADE_TICKS;
                    logs.Add(TamerBattleText.DownLine(unit.Name));
                    break;
                }
                case TamerBattleEventType.RisingDamage:
                case TamerBattleEventType.End:
                {
                    break;
                }
                default:
                {
                    break;
                }
            }
    }

    private void Derive()
    {
        var anim = animationClock;
        var unitStates = new List<TamerBattleUnitState>(AppliedOrder.Count);

        foreach (var id in AppliedOrder)
        {
            var unit = AppliedUnits[id];
            var (viewColumn, viewRow) = ViewCell(unit.EngineColumn, unit.EngineRow);

            var elapsed = anim - unit.ActionStart;
            var progress = unit.Action switch
            {
                TamerBattleAction.Idle => 1.0,
                _ => unit.ActionDuration <= 0 ? 1.0 : Math.Clamp(elapsed / unit.ActionDuration, 0, 1)
            };

            var action = unit.Action;

            if (action is TamerBattleAction.Walk or TamerBattleAction.Leap or TamerBattleAction.Attack or TamerBattleAction.Fading)
            {
                if ((elapsed >= unit.ActionDuration) || (progress >= 1.0))
                {
                    action = TamerBattleAction.Idle;
                    progress = 1.0;
                }
            }

            double drawX;
            double drawY;

            if ((action == TamerBattleAction.Walk) || (action == TamerBattleAction.Leap))
            {
                var (fromColumn, fromRow) = ViewCell(unit.FromColumn, unit.FromRow);
                drawX = fromColumn + ((viewColumn - fromColumn) * progress);
                drawY = fromRow + ((viewRow - fromRow) * progress);
            }
            else
            {
                drawX = viewColumn;
                drawY = viewRow;
            }

            var flash = unit.FlashTick is { } flashStart && ((anim - flashStart) >= 0) && ((anim - flashStart) < FLASH_TICKS)
                ? TamerBattleFlash.Gold
                : TamerBattleFlash.None;

            string? abilityName = null;
            var abilityProgress = 1.0;

            if (unit.AbilityTick is { } abilityStart && !string.IsNullOrEmpty(unit.AbilityLabel))
            {
                var abilityElapsed = anim - abilityStart;

                if ((abilityElapsed >= 0) && (abilityElapsed < ABILITY_TICKS))
                {
                    abilityName = unit.AbilityLabel;
                    abilityProgress = abilityElapsed / (double)ABILITY_TICKS;
                }
            }

            var statuses = unit.Statuses.OrderBy(s => s, StringComparer.Ordinal).ToList();

            unitStates.Add(new TamerBattleUnitState(
                unit.Id,
                unit.Side,
                unit.Name,
                unit.Level,
                unit.Sprite,
                unit.Element,
                unit.Alive,
                Math.Clamp(unit.Health, 0, unit.MaxHealth),
                unit.MaxHealth,
                Math.Max(0, unit.Shield),
                Math.Clamp(unit.Energy, 0, 100),
                statuses,
                unit.EngineColumn,
                unit.EngineRow,
                viewColumn,
                viewRow,
                drawX,
                drawY,
                unit.Facing,
                action,
                progress,
                flash,
                unit.Range,
                abilityName,
                abilityProgress));
        }

        UnitList = unitStates;

        FloaterList = AppliedFloaters
            .Where(f => ((anim - f.Tick) >= 0) && ((anim - f.Tick) < FLOATER_TICKS))
            .Select(f => new TamerBattleFloater(f.Amount, f.Kind, f.ViewColumn, f.ViewRow, (anim - f.Tick) / (double)TICKS_PER_SECOND))
            .ToList();

        ProjectileList = AppliedProjectiles
            .Where(p => ((anim - p.Tick) >= 0) && ((anim - p.Tick) < PROJECTILE_TICKS))
            .Select(p => new TamerBattleProjectile(p.FromX, p.FromY, p.ToX, p.ToY, (anim - p.Tick) / (double)PROJECTILE_TICKS))
            .ToList();

        LogList = AppliedLogs.Count <= 4 ? new List<string>(AppliedLogs) : AppliedLogs.GetRange(AppliedLogs.Count - 4, 4);
    }

    private Direction InitialFacing(TamerBattleSide side)
    {
        var engine = side == TamerBattleSide.Attacker ? Direction.Up : Direction.Down;
        return MapDirection(engine);
    }

    private Direction MapDirection(Direction engine)
    {
        if (Fight.ViewerSide != TamerBattleViewerSide.Defender)
            return engine;

        return engine switch
        {
            Direction.Up => Direction.Down,
            Direction.Down => Direction.Up,
            Direction.Left => Direction.Right,
            Direction.Right => Direction.Left,
            _ => engine
        };
    }

    private Direction MovementFacing(int fromColumn, int fromRow, int toColumn, int toRow)
    {
        var (fromViewColumn, fromViewRow) = ViewCell(fromColumn, fromRow);
        var (toViewColumn, toViewRow) = ViewCell(toColumn, toRow);
        var dx = toViewColumn - fromViewColumn;
        var dy = toViewRow - fromViewRow;

        if (dx == 0 && dy == 0)
            return Direction.Down;

        if (Math.Abs(dx) >= Math.Abs(dy))
            return dx > 0 ? Direction.Right : Direction.Left;

        return dy > 0 ? Direction.Down : Direction.Up;
    }

    private Direction TargetFacing(MutableUnit from, MutableUnit to)
    {
        var (fromViewColumn, fromViewRow) = ViewCell(from.EngineColumn, from.EngineRow);
        var (toViewColumn, toViewRow) = ViewCell(to.EngineColumn, to.EngineRow);
        var dx = toViewColumn - fromViewColumn;
        var dy = toViewRow - fromViewRow;

        if (dx == 0 && dy == 0)
            return from.Facing;

        if (Math.Abs(dx) >= Math.Abs(dy))
            return dx > 0 ? Direction.Right : Direction.Left;

        return dy > 0 ? Direction.Down : Direction.Up;
    }
}
