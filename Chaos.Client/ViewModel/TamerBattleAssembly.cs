#region
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>What an assembly step found.</summary>
public enum TamerBattleAcceptance : byte
{
    Waiting = 0,
    Ready = 1,
    Broken = 2
}

/// <summary>The outcome of <see cref="TamerBattleAssembly.Accept" />: the status, and the fight when ready.</summary>
public sealed record TamerBattleAcceptResult(TamerBattleAcceptance Status, TamerBattleFight? Fight)
{
    public bool IsWaiting => Status == TamerBattleAcceptance.Waiting;
    public bool IsReady => Status == TamerBattleAcceptance.Ready;
    public bool IsBroken => Status == TamerBattleAcceptance.Broken;
}

/// <summary>One whole fight, reassembled in order: who fought, every event, and the result.</summary>
public sealed record TamerBattleFight(
    ushort FightNumber,
    string AttackerName,
    string DefenderName,
    TamerBattleViewerSide ViewerSide,
    string Title,
    IReadOnlyList<TamerBattleMonsterView> Monsters,
    IReadOnlyList<TamerBattleNameEntry> Names,
    IReadOnlyList<TamerBattleEventView> Events,
    TamerBattleSide Winner,
    TamerBattleEndReason Reason,
    ushort EndTick,
    IReadOnlyList<string> ResultLines);

/// <summary>
///     Collects a fight's Start, Events parts and End and reports when it is whole or broken.
///     The server sends all parts at once in order over TCP; out-of-order Events parts are buffered by part number
///     and duplicated parts are ignored. A Start for a new fight number discards any unfinished fight. An Events part
///     or an End with no active Start, or an End with parts missing, is broken: the fight is dropped.
///     On Ready the parts are released and the completed fight number is remembered; late or duplicate parts for that
///     number return Waiting. A broken fight is reported once: its number is remembered and its remaining parts
///     return Waiting.
///     Pure: no textures.
/// </summary>
public sealed class TamerBattleAssembly
{
    public const int MAX_MONSTERS = 10;
    public const int MAX_NAMES = 64;
    public const int MAX_EVENTS = 1000;
    public const int MAX_PARTS = 64;
    public const int MAX_RESULT_LINES = 4;

    private ushort? CurrentFight;
    private TamerBattleDisplayArgs? Start;
    private readonly Dictionary<byte, List<TamerBattleEventView>> Parts = [];
    private TamerBattleDisplayArgs? End;
    private readonly HashSet<ushort> IgnoredFights = [];

    public bool HasActive => CurrentFight is not null;

    public ushort? ActiveFightNumber => CurrentFight;

    public TamerBattleAcceptResult Accept(TamerBattleDisplayArgs? args)
    {
        if (args is null)
            return Broken();

        if (!Enum.IsDefined(args.Kind))
            return Broken();

        return args.Kind switch
        {
            TamerBattleDisplayKind.Start => AcceptStart(args),
            TamerBattleDisplayKind.Events => AcceptEvents(args),
            TamerBattleDisplayKind.End => AcceptEnd(args),
            _ => Broken()
        };
    }

    public void Clear()
    {
        CurrentFight = null;
        Start = null;
        Parts.Clear();
        End = null;
        IgnoredFights.Clear();
    }

    private void ClearActive()
    {
        CurrentFight = null;
        Start = null;
        Parts.Clear();
        End = null;
    }

    private TamerBattleAcceptResult AcceptStart(TamerBattleDisplayArgs args)
    {
        if (IgnoredFights.Contains(args.FightNumber))
            return Waiting();

        var monsters = args.Monsters ?? [];
        var names = args.Names ?? [];

        if ((monsters.Count > MAX_MONSTERS) || (names.Count > MAX_NAMES))
        {
            ClearActive();
            IgnoredFights.Add(args.FightNumber);
            return Broken();
        }

        if (CurrentFight is null || args.FightNumber != CurrentFight)
        {
            CurrentFight = args.FightNumber;
            Start = args;
            Parts.Clear();
            End = null;
        }
        else
        {
            //a repeated Start for the fight already assembling is a duplicate; keep what arrived
            if (Start is null)
                Start = args;
        }

        return Waiting();
    }

    private TamerBattleAcceptResult AcceptEvents(TamerBattleDisplayArgs args)
    {
        if (IgnoredFights.Contains(args.FightNumber))
            return Waiting();

        if (CurrentFight is null || args.FightNumber != CurrentFight)
        {
            if (CurrentFight is null)
                IgnoredFights.Add(args.FightNumber);

            return Broken();
        }

        var events = args.Events ?? [];

        if ((args.PartNumber >= MAX_PARTS) || (events.Count > MAX_EVENTS))
        {
            var dropped = CurrentFight.Value;
            ClearActive();
            IgnoredFights.Add(dropped);
            return Broken();
        }

        if (!Parts.ContainsKey(args.PartNumber))
            Parts[args.PartNumber] = new List<TamerBattleEventView>(events);

        return CheckReady();
    }

    private TamerBattleAcceptResult AcceptEnd(TamerBattleDisplayArgs args)
    {
        if (IgnoredFights.Contains(args.FightNumber))
            return Waiting();

        if (CurrentFight is null || args.FightNumber != CurrentFight)
        {
            if (CurrentFight is null)
                IgnoredFights.Add(args.FightNumber);

            return Broken();
        }

        var lines = args.ResultLines ?? [];

        if ((args.EventParts > MAX_PARTS) || (lines.Count > MAX_RESULT_LINES))
        {
            var dropped = CurrentFight.Value;
            ClearActive();
            IgnoredFights.Add(dropped);
            return Broken();
        }

        End ??= args;

        if (!IsComplete())
        {
            var dropped = CurrentFight.Value;
            ClearActive();
            IgnoredFights.Add(dropped);
            return Broken();
        }

        return Ready();
    }

    private TamerBattleAcceptResult CheckReady()
    {
        if (End is null)
            return Waiting();

        return IsComplete() ? Ready() : Waiting();
    }

    private bool IsComplete()
    {
        if ((Start is null) || (End is null))
            return false;

        if (CurrentFight is null || End.FightNumber != CurrentFight || Start.FightNumber != CurrentFight)
            return false;

        if (Parts.Count != End.EventParts)
            return false;

        for (var i = 0; i < End.EventParts; i++)
        {
            if (!Parts.ContainsKey((byte)i))
                return false;
        }

        return true;
    }

    private TamerBattleAcceptResult Ready()
    {
        //IsComplete has already established Start and End are present
        var start = Start!;
        var end = End!;

        var events = new List<TamerBattleEventView>();

        for (var i = 0; i < end.EventParts; i++)
            events.AddRange(Parts[(byte)i]);

        var fight = new TamerBattleFight(
            start.FightNumber,
            start.AttackerName ?? string.Empty,
            start.DefenderName ?? string.Empty,
            start.ViewerSide,
            start.Title ?? string.Empty,
            (start.Monsters ?? []).ToList(),
            (start.Names ?? []).ToList(),
            events,
            end.Winner,
            end.Reason,
            end.EndTick,
            (end.ResultLines ?? []).ToList());

        IgnoredFights.Add(fight.FightNumber);
        ClearActive();

        return new TamerBattleAcceptResult(TamerBattleAcceptance.Ready, fight);
    }

    private static TamerBattleAcceptResult Waiting() => new(TamerBattleAcceptance.Waiting, null);

    private static TamerBattleAcceptResult Broken() => new(TamerBattleAcceptance.Broken, null);
}
