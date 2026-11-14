#region
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>
///     The stable window's Ranked tab: the last <c>Ranked</c> display from the server, the selected defence log entry
///     (kept by id), which buttons are on and why not, the questions, and the request builders. Pure: no textures.
///     The server decides everything; this only saves the player a request that would be refused.
/// </summary>
public sealed class TamerRankedState
{
    public const int MAX_DEFENCE = 5;
    public const int MAX_OFFERS = 3;
    public const int MAX_LOG = 10;
    public const int MAX_TOP = 10;
    public const int SQUAD_COUNT = 3;
    public const int DEFENCE_SIZE = 5;

    private readonly List<string> DefenceList = [];
    private readonly List<TamerRankedOfferView> OfferList = [];
    private readonly List<TamerRankedLogView> LogList = [];
    private readonly List<TamerRankedTopView> TopList = [];
    private Guid? SelectedId;

    /// <summary>True once a <c>Ranked</c> display has arrived; until then the tab shows "Reading the ladder...".</summary>
    public bool HasView { get; private set; }

    public ushort Season { get; private set; }

    /// <summary>Seconds until the season ends, as of the last display.</summary>
    public uint SeasonSecondsLeft { get; private set; }

    public byte Step { get; private set; }
    public byte Points { get; private set; }
    public byte BestStep { get; private set; }
    public int Rating { get; private set; }

    /// <summary>The viewer's place in the ordered list, or 0 when not on it.</summary>
    public ushort LadderPosition { get; private set; }

    public byte AttacksLeft { get; private set; }
    public byte AttacksPerDay { get; private set; }
    public byte RefreshLeft { get; private set; }
    public bool DailyWinOpen { get; private set; }
    public TamerWireDefenceState DefenceState { get; private set; }

    /// <summary>The saved defence's species keys in squad order.</summary>
    public IReadOnlyList<string> Defence => DefenceList;

    /// <summary>The offered opponents; the index is the offer slot.</summary>
    public IReadOnlyList<TamerRankedOfferView> Offers => OfferList;

    /// <summary>The defence log, newest first.</summary>
    public IReadOnlyList<TamerRankedLogView> Log => LogList;

    public IReadOnlyList<TamerRankedTopView> Top => TopList;

    /// <summary>Why the last ranked request was refused, or empty.</summary>
    public string LastMessage { get; private set; } = string.Empty;

    /// <summary>A defence is saved, whether or not the data can still build it.</summary>
    public bool HasDefence => HasView && (DefenceState != TamerWireDefenceState.None);

    /// <summary>The saved defence uses a creature the data no longer has.</summary>
    public bool DefenceNeedsSaving => HasView && (DefenceState == TamerWireDefenceState.NeedsSaving);

    public TamerRankedLogView? SelectedEntry => SelectedId is { } id ? LogList.FirstOrDefault(entry => entry.Id == id) : null;

    /// <summary>The selected entry's row in <see cref="Log" />, or -1.</summary>
    public int SelectedIndex => SelectedId is { } id ? LogList.FindIndex(entry => entry.Id == id) : -1;

    /// <summary>The viewer and the selected entry's attacker have already fought today (log flag 32).</summary>
    public bool SelectedFoughtToday => SelectedEntry is { } entry && entry.Flags.HasFlag(TamerRankedLogFlags.FoughtToday);

    /// <summary>The viewer has already taken their revenge on the selected entry (log flag 64).</summary>
    public bool SelectedRevengeUsed => SelectedEntry is { } entry && entry.Flags.HasFlag(TamerRankedLogFlags.RevengeUsed);

    /// <summary>One refresh is left today, and there is a defence to be offered opponents for.</summary>
    public bool CanRefresh => HasDefence && (RefreshLeft > 0);

    /// <summary>The selected entry keeps a replay.</summary>
    public bool CanWatch => SelectedEntry is { } entry && entry.Flags.HasFlag(TamerRankedLogFlags.ReplayKept);

    /// <summary>Takes a <c>Ranked</c> display; other display kinds leave the state alone.</summary>
    public void Apply(TamerDisplayArgs args)
    {
        if ((args is null) || (args.Kind != TamerDisplayKind.Ranked))
            return;

        Season = args.Season;
        SeasonSecondsLeft = args.SeasonSecondsLeft;
        Step = args.Step;
        Points = args.Points;
        BestStep = args.BestStep;
        Rating = args.Rating;
        LadderPosition = args.LadderPosition;
        AttacksLeft = args.AttacksLeft;
        AttacksPerDay = args.AttacksPerDay;
        RefreshLeft = args.RefreshLeft;
        DailyWinOpen = args.DailyWinOpen;
        DefenceState = args.DefenceState;
        LastMessage = args.Message ?? string.Empty;

        Replace(DefenceList, args.Defence, MAX_DEFENCE);
        Replace(OfferList, args.Offers, MAX_OFFERS);
        Replace(LogList, args.Log, MAX_LOG);
        Replace(TopList, args.Top, MAX_TOP);

        HasView = true;
        FixSelection();
    }

    public void Clear()
    {
        HasView = false;
        Season = 0;
        SeasonSecondsLeft = 0;
        Step = 0;
        Points = 0;
        BestStep = 0;
        Rating = 0;
        LadderPosition = 0;
        AttacksLeft = 0;
        AttacksPerDay = 0;
        RefreshLeft = 0;
        DailyWinOpen = false;
        DefenceState = TamerWireDefenceState.None;
        LastMessage = string.Empty;
        DefenceList.Clear();
        OfferList.Clear();
        LogList.Clear();
        TopList.Clear();
        SelectedId = null;
    }

    /// <summary>Selects a log entry by id; an id not in the log changes nothing.</summary>
    public void Select(Guid entryId)
    {
        if ((entryId == Guid.Empty) || LogList.All(entry => entry.Id != entryId))
            return;

        SelectedId = entryId;
    }

    /// <summary>The opponent in an offer slot, or null for an empty or unknown slot.</summary>
    public TamerRankedOfferView? OfferAt(int slot)
    {
        if ((slot < 0) || (slot >= OfferList.Count))
            return null;

        var offer = OfferList[slot];

        return string.IsNullOrEmpty(offer.Name) ? null : offer;
    }

    /// <summary>Attack is on with a defence, an attack left and a non-empty active squad.</summary>
    public bool CanAttack(int slot, TamerStableState stable, out string? reason)
    {
        if (!HasView)
            reason = TamerRankedText.Reading;
        else if (!HasDefence)
            reason = TamerRankedText.NeedDefence;
        else if (OfferAt(slot) is null)
            reason = TamerRankedText.NoOpponentThere;
        else if (AttacksLeft == 0)
            reason = TamerRankedText.NoAttacksLeft;
        else if (!HasActiveSquad(stable))
            reason = TamerRankedText.EmptyActiveSquad;
        else
            reason = null;

        return reason is null;
    }

    /// <summary>Set from squad is on for a squad with exactly five members.</summary>
    public bool CanSetDefence(int squad, TamerStableState stable, out string? reason)
    {
        if (!HasView)
            reason = TamerRankedText.Reading;
        else if (SquadSize(stable, squad) != DEFENCE_SIZE)
            reason = TamerRankedText.NeedFive;
        else
            reason = null;

        return reason is null;
    }

    /// <summary>Revenge is on when the selected entry's revenge flag is set and the active squad is not empty.</summary>
    public bool CanRevenge(TamerStableState stable, out string? reason)
    {
        if (SelectedEntry is not { } entry)
            reason = TamerRankedText.NoEntryChosen;
        else if (entry.Flags.HasFlag(TamerRankedLogFlags.RevengeUsed))
            reason = TamerRankedText.AlreadyRevenged;
        else if (!entry.Flags.HasFlag(TamerRankedLogFlags.RevengeAvailable))
            reason = TamerRankedText.NoRevengeThere;
        else if (!HasActiveSquad(stable))
            reason = TamerRankedText.EmptyActiveSquad;
        else
            reason = null;

        return reason is null;
    }

    /// <summary>The question for the selected entry, the warning one when the pair has fought today; null with no entry.</summary>
    public string? RevengeQuestion()
        => SelectedEntry is { } entry ? TamerRankedText.RevengeQuestion(entry.Attacker, SelectedFoughtToday) : null;

    /// <summary>The question for an offer slot; null for an empty slot.</summary>
    public string? AttackQuestion(int slot)
        => OfferAt(slot) is { } offer ? TamerRankedText.AttackQuestion(offer.Name, AttacksLeft) : null;

    public static TamerActionArgs InfoAction() => new() { Type = TamerActionType.RankedInfo };

    public static TamerActionArgs RefreshAction() => new() { Type = TamerActionType.RankedRefresh };

    /// <summary>Null for a squad number over 2.</summary>
    public static TamerActionArgs? SetDefenceAction(byte squad)
        => squad >= SQUAD_COUNT ? null : new TamerActionArgs { Type = TamerActionType.RankedSetDefence, Squad = squad };

    /// <summary>The slot travels in <c>Squad</c>, the name as it was shown in <c>Name</c>. Null for a slot over 2 or no name.</summary>
    public static TamerActionArgs? AttackAction(byte slot, string name)
        => (slot >= MAX_OFFERS) || string.IsNullOrEmpty(name)
            ? null
            : new TamerActionArgs { Type = TamerActionType.RankedAttack, Squad = slot, Name = name };

    /// <summary>The log entry's id travels in <c>MonsterId</c>. Null for an empty id.</summary>
    public static TamerActionArgs? RevengeAction(Guid entryId)
        => entryId == Guid.Empty ? null : new TamerActionArgs { Type = TamerActionType.RankedRevenge, MonsterId = entryId };

    /// <summary>The log entry's id travels in <c>MonsterId</c>. Null for an empty id.</summary>
    public static TamerActionArgs? WatchAction(Guid entryId)
        => entryId == Guid.Empty ? null : new TamerActionArgs { Type = TamerActionType.RankedWatch, MonsterId = entryId };

    /// <summary>A friendly bout against the named player. Null for no name.</summary>
    public static TamerActionArgs? FriendlyAction(string name)
    {
        var cleaned = name is null ? string.Empty : name.Trim();

        return cleaned.Length == 0 ? null : new TamerActionArgs { Type = TamerActionType.Friendly, Name = cleaned };
    }

    private static void Replace<T>(List<T> target, List<T>? source, int cap) where T: class
    {
        target.Clear();

        if (source is null)
            return;

        foreach (var item in source)
        {
            if (target.Count >= cap)
                break;

            if (item is not null)
                target.Add(item);
        }
    }

    private static int SquadSize(TamerStableState? stable, int squad)
        => (stable is null) || (squad < 0) || (squad >= SQUAD_COUNT) || (squad >= stable.Squads.Count) ? 0 : stable.Squads[squad].Count;

    private static bool HasActiveSquad(TamerStableState? stable) => (stable is not null) && (SquadSize(stable, stable.ActiveSquad) > 0);

    //the same entry stays selected while it is in the log; when it is gone the newest takes its place
    private void FixSelection()
    {
        if (LogList.Count == 0)
        {
            SelectedId = null;

            return;
        }

        if ((SelectedId is null) || LogList.All(entry => entry.Id != SelectedId))
            SelectedId = LogList[0].Id;
    }
}
