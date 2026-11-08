#region
using Chaos.CannonFair;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>
///     The client's copy of a Cannon Fair match: roster, aiming state, the volley recording and replay clock,
///     standings and results. Plain C# with no MonoGame types; every time-dependent call takes the time in milliseconds.
/// </summary>
public sealed class CannonFairSession
{
    private RecordingAssembler Assembler = new();
    private List<CannonFairPieceInfo> PieceList = [];
    private List<CannonFairRosterEntry> RosterList = [];
    private List<CannonFairStandingInfo> StandingList = [];
    private List<CannonFairStandingInfo> TopThreeList = [];
    private long AimEndMs;
    private long ReplayStartMs;
    private long ResultsEndMs;
    private int? RecordingVolleyId;
    private int? ReplayStartVolleyId;

    public Action<int>? Confirm { get; set; }
    public Action<float, float>? LockIn { get; set; }
    public bool IsOpen { get; private set; }
    public int OwnSlot { get; private set; } = -1;
    public bool CanAim { get; private set; }
    public bool Locked { get; private set; }
    public MatchPhase Phase { get; private set; } = MatchPhase.Waiting;
    public int FortNumber { get; private set; }
    public int VolleyNumber { get; private set; }
    public ShotKind Shot { get; private set; } = ShotKind.Plain;
    public int Cannon { get; private set; }
    public int CannonCount { get; private set; }
    public int LockedCount { get; private set; }
    public int ActiveCount { get; private set; }
    public IReadOnlyList<CannonFairPieceInfo> Pieces => PieceList;
    public IReadOnlyList<CannonFairRosterEntry> Roster => RosterList;
    public IReadOnlyList<CannonFairStandingInfo> Standings => StandingList;
    public IReadOnlyList<CannonFairStandingInfo> TopThree => TopThreeList;
    public CannonFairStandingInfo? Own { get; private set; }
    public VolleyResult? Volley { get; private set; }
    public RecordingPlayer? Player { get; private set; }
    public bool IsReplaying => RecordingVolleyId.HasValue && (RecordingVolleyId == ReplayStartVolleyId);
    public bool ShowingResults { get; private set; }
    public bool IsFinal { get; private set; }

    public int AimMsLeft(long nowMs) => (int)Math.Max(0, AimEndMs - nowMs);

    public int ReplayMs(long nowMs) => ReplayStartVolleyId.HasValue ? (int)Math.Max(0, nowMs - ReplayStartMs) : 0;

    public int ResultsMsLeft(long nowMs) => (int)Math.Max(0, ResultsEndMs - nowMs);

    public void Apply(CannonFairDisplayArgs args, long nowMs)
    {
        switch (args.Kind)
        {
            case CannonFairMessageKind.Open:
                //a new match restarts volley ids, so a fresh assembler keeps old pieces from blocking the new ones
                Clear();
                IsOpen = true;
                OwnSlot = args.OwnSlot;
                RosterList = [.. args.Roster];
                FortNumber = args.Fort;
                VolleyNumber = args.Volley;
                PieceList = [.. args.Pieces];
                Phase = MatchPhase.Aiming;

                break;

            case CannonFairMessageKind.Aiming:
                FortNumber = args.Fort;
                VolleyNumber = args.Volley;
                PieceList = [.. args.Pieces];
                Shot = Enum.IsDefined((ShotKind)args.Shot) ? (ShotKind)args.Shot : ShotKind.Plain;
                Cannon = args.Cannon;
                CannonCount = args.CannonCount;
                AimEndMs = nowMs + args.AimMs;
                Locked = false;
                CanAim = (OwnSlot >= 0) && (args.Cannon > 0);
                Volley = null;
                Player = null;
                RecordingVolleyId = null;
                ReplayStartVolleyId = null;
                ShowingResults = false;
                IsFinal = false;
                Phase = MatchPhase.Aiming;

                break;

            case CannonFairMessageKind.LockProgress:
                LockedCount = args.Locked;
                ActiveCount = args.ActivePlayers;

                break;

            case CannonFairMessageKind.RecordingPiece:
                if (!Assembler.Add(args.VolleyId, args.PieceIndex, args.PieceCount, args.Data))
                    break;

                try
                {
                    Volley = RecordingCodec.Decode(Assembler.Complete!);
                }
                catch (FormatException)
                {
                    Volley = null;
                    Player = null;
                    RecordingVolleyId = null;

                    break;
                }

                Player = new RecordingPlayer(Volley);
                RecordingVolleyId = args.VolleyId;
                Confirm?.Invoke(args.VolleyId);

                break;

            case CannonFairMessageKind.ReplayStart:
                ReplayStartVolleyId = args.VolleyId;
                ReplayStartMs = nowMs;
                Phase = MatchPhase.Replay;

                break;

            case CannonFairMessageKind.Standings:
                SetStandings(args.Standings);

                break;

            case CannonFairMessageKind.FortResults:
                FortNumber = args.Fort;
                VolleyNumber = args.Volley;
                SetStandings(args.Standings);
                ResultsEndMs = nowMs + args.HoldMs;
                ShowingResults = true;
                IsFinal = false;
                Phase = MatchPhase.Results;

                break;

            case CannonFairMessageKind.MatchResults:
                SetStandings(args.Standings);
                ResultsEndMs = nowMs + args.HoldMs;
                ShowingResults = true;
                IsFinal = true;
                Phase = MatchPhase.Results;

                break;

            case CannonFairMessageKind.Close:
                Clear();

                break;

            default:
                break;
        }
    }

    public bool TryLockIn(float angle, float power)
    {
        if (!CanAim || Locked)
            return false;

        Locked = true;
        LockIn?.Invoke(angle, power);

        return true;
    }

    public void Clear()
    {
        IsOpen = false;
        OwnSlot = -1;
        CanAim = false;
        Locked = false;
        Phase = MatchPhase.Waiting;
        FortNumber = 0;
        VolleyNumber = 0;
        AimEndMs = 0;
        Shot = ShotKind.Plain;
        Cannon = 0;
        CannonCount = 0;
        LockedCount = 0;
        ActiveCount = 0;
        PieceList = [];
        RosterList = [];
        StandingList = [];
        TopThreeList = [];
        Own = null;
        Volley = null;
        Player = null;
        RecordingVolleyId = null;
        ReplayStartVolleyId = null;
        ReplayStartMs = 0;
        ShowingResults = false;
        IsFinal = false;
        ResultsEndMs = 0;
        Assembler = new();
    }

    private void SetStandings(List<CannonFairStandingInfo> standings)
    {
        StandingList = [.. standings];
        TopThreeList = [.. standings.OrderBy(s => s.Place).Take(3)];
        Own = standings.FirstOrDefault(s => s.Slot == OwnSlot);
    }
}
