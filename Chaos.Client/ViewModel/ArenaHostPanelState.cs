#region
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>
///     The Arena Host Panel's client state: the server's last packet, plus what only the client knows (captain pick mode,
///     the vote picker, the End match confirm). Each click returns the action to send, or null.
/// </summary>
public sealed class ArenaHostPanelState
{
    public readonly record struct PageSlice(int Start, int Count, bool HasPrevious, bool HasNext);

    public const int MODE_PAGE_SIZE = 11;

    public static readonly IReadOnlyList<byte> VoteDurations = [30, 60, 120];

    private readonly List<byte> VotePicks = [];

    public ArenaHostDisplayArgs? Args { get; private set; }
    public int FreeForAllPage { get; private set; }
    public int TeamPage { get; private set; }

    /// <summary>GreenCaptain or GoldCaptain while the next roster click picks a captain.</summary>
    public ArenaHostOption? PickingCaptain { get; private set; }

    public bool VoteMode { get; private set; }
    public byte VoteSeconds { get; private set; } = 60;
    public bool ConfirmingEnd { get; private set; }

    /// <summary>The last server answer, kept until the next click.</summary>
    public string Message { get; private set; } = string.Empty;

    public IReadOnlyCollection<byte> VoteSelection => VotePicks;

    public void Apply(ArenaHostDisplayArgs args)
    {
        var previousSelectedMode = Args?.SelectedMode;
        Args = args;

        var freeForAll = args.Modes.Count(m => !m.IsTeam);
        var team = args.Modes.Count(m => m.IsTeam);
        FreeForAllPage = ClampPage(freeForAll, FreeForAllPage);
        TeamPage = ClampPage(team, TeamPage);

        if (previousSelectedMode != args.SelectedMode)
        {
            var selected = args.Modes.FirstOrDefault(m => m.Id == args.SelectedMode);

            if (selected is not null)
            {
                var section = args.Modes.Where(m => m.IsTeam == selected.IsTeam).ToList();
                var selectedIndex = section.FindIndex(m => m.Id == selected.Id);
                var page = selectedIndex / MODE_PAGE_SIZE;

                if (selected.IsTeam)
                    TeamPage = page;
                else
                    FreeForAllPage = page;
            }
        }

        if (!string.IsNullOrEmpty(args.Message))
            Message = args.Message;

        if (args.Options.All(o => o.Option is not (ArenaHostOption.GreenCaptain or ArenaHostOption.GoldCaptain)))
            PickingCaptain = null;

        if (args.Status != ArenaHostStatus.MatchRunning)
            ConfirmingEnd = false;
    }

    public void Reset()
    {
        Args = null;
        FreeForAllPage = 0;
        TeamPage = 0;
        PickingCaptain = null;
        VoteMode = false;
        VotePicks.Clear();
        VoteSeconds = 60;
        ConfirmingEnd = false;
        Message = string.Empty;
    }

    public static PageSlice GetPageSlice(int count, int page, int pageSize)
    {
        var pages = Math.Max(1, (count + pageSize - 1) / pageSize);
        var clampedPage = Math.Clamp(page, 0, pages - 1);
        var start = clampedPage * pageSize;
        var shown = Math.Min(pageSize, Math.Max(0, count - start));

        return new PageSlice(start, shown, clampedPage > 0, clampedPage < pages - 1);
    }

    private static int ClampPage(int count, int page) => GetPageSlice(count, page, MODE_PAGE_SIZE).Start / MODE_PAGE_SIZE;

    public void PageModes(bool isTeam, bool forward)
    {
        if (Args is null)
            return;

        var count = Args.Modes.Count(m => m.IsTeam == isTeam);
        var page = isTeam ? TeamPage : FreeForAllPage;
        page = ClampPage(count, page + (forward ? 1 : -1));

        if (isTeam)
            TeamPage = page;
        else
            FreeForAllPage = page;
    }

    /// <summary>Leaves captain pick mode. Returns whether it was active, so Escape can tell if it used the key.</summary>
    public bool CancelPick()
    {
        var wasPicking = PickingCaptain is not null;
        PickingCaptain = null;

        return wasPicking;
    }

    private ArenaHostActionArgs Send(ArenaHostActionArgs args)
    {
        Message = string.Empty;

        return args;
    }

    public ArenaHostActionArgs? ClickMode(byte id)
    {
        if (VoteMode)
        {
            if (!VotePicks.Remove(id) && (VotePicks.Count < ArenaHostProtocol.MAX_VOTE_MODES) && (id != ArenaHostProtocol.COURT_MODE_ID))
                VotePicks.Add(id);

            return null;
        }

        return Send(new ArenaHostActionArgs { Type = ArenaHostActionType.SelectMode, Mode = id });
    }

    public ArenaHostActionArgs? ClickOption(ArenaHostOption option, bool forward)
    {
        if (option is ArenaHostOption.GreenCaptain or ArenaHostOption.GoldCaptain)
        {
            PickingCaptain = PickingCaptain == option ? null : option;

            return null;
        }

        return Send(new ArenaHostActionArgs { Type = ArenaHostActionType.StepOption, Option = option, Forward = forward });
    }

    public ArenaHostActionArgs ClickRosterName(string name)
    {
        if (PickingCaptain is { } side)
        {
            PickingCaptain = null;

            return Send(new ArenaHostActionArgs { Type = ArenaHostActionType.PickCaptain, Option = side, Name = name });
        }

        return Send(new ArenaHostActionArgs { Type = ArenaHostActionType.CycleTeam, Name = name });
    }

    public ArenaHostActionArgs Split(byte teamCount) => Send(new ArenaHostActionArgs { Type = ArenaHostActionType.Split, TeamCount = teamCount });

    public ArenaHostActionArgs ClearTeams() => Send(new ArenaHostActionArgs { Type = ArenaHostActionType.ClearTeams });

    public ArenaHostActionArgs ToggleAdminPlay() => Send(new ArenaHostActionArgs { Type = ArenaHostActionType.ToggleAdminPlay });

    public void ToggleVoteMode()
    {
        VoteMode = !VoteMode;
        VotePicks.Clear();
    }

    public void CycleVoteSeconds()
        => VoteSeconds = VoteDurations[(VoteDurations.ToList().IndexOf(VoteSeconds) + 1) % VoteDurations.Count];

    public ArenaHostActionArgs? OpenVote()
    {
        if (VotePicks.Count < ArenaHostProtocol.MIN_VOTE_MODES)
            return null;

        var args = new ArenaHostActionArgs
        {
            Type = ArenaHostActionType.OpenVote,
            VoteModes = VotePicks.ToList(),
            VoteSeconds = VoteSeconds
        };

        VoteMode = false;
        VotePicks.Clear();

        return Send(args);
    }

    public string MainButtonCaption
        => Args?.Status switch
        {
            ArenaHostStatus.Countdown    => "Cancel countdown",
            ArenaHostStatus.MatchRunning => ConfirmingEnd ? "Are you sure?" : "End match",
            _                            => "Start"
        };

    public bool MainButtonEnabled
        => Args is { } a && ((a.Status != ArenaHostStatus.Idle) || a.CanStart);

    public ArenaHostActionArgs? ClickMainButton()
    {
        if (Args is not { } a)
            return null;

        switch (a.Status)
        {
            case ArenaHostStatus.Countdown:
                return Send(new ArenaHostActionArgs { Type = ArenaHostActionType.Cancel });
            case ArenaHostStatus.MatchRunning when !ConfirmingEnd:
                ConfirmingEnd = true;

                return null;
            case ArenaHostStatus.MatchRunning:
                ConfirmingEnd = false;

                return Send(new ArenaHostActionArgs { Type = ArenaHostActionType.EndMatch });
            default:
                return a.CanStart ? Send(new ArenaHostActionArgs { Type = ArenaHostActionType.Start }) : null;
        }
    }

    public static ArenaHostActionArgs CloseAction() => new() { Type = ArenaHostActionType.Close };
}
