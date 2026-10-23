#region
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Controls.World.Popups.HallBattle;

public enum WarRoomPage
{
    Board,
    Team
}

/// <summary>
///     What the war room shows: the guild list, the picked guild's detail and the leader's challenge form on the board
///     page, and both rosters plus the candidates on the team page. Pure state, so every rule is testable without a
///     window; the server checks each action again.
/// </summary>
public sealed class WarRoomState
{
    public const int FORMING_HAND_PICK = 0;
    public const int FORMING_OPEN_INVITE = 1;

    public static readonly byte[] TeamSizes = [3, 5, 8];

    private DateTime TeamReceivedAt = DateTime.UtcNow;
    private ushort TeamSeconds;

    public List<string> Candidates { get; private set; } = [];
    public int CandidateOffset { get; private set; }
    public bool CanChallenge { get; private set; }
    public string BlockedReason { get; private set; } = string.Empty;
    public int DetailDraws { get; private set; }
    public string DetailGuild { get; private set; } = string.Empty;
    public int DetailLosses { get; private set; }
    public int DetailRating { get; private set; }
    public int DetailWins { get; private set; }
    public bool FormingOpenInvite { get; set; }
    public List<HallBattleHistoryRow> History { get; private set; } = [];
    public bool HasDetail { get; private set; }
    public List<string> Invited { get; private set; } = [];
    public string LastMeeting { get; private set; } = string.Empty;
    public List<string> OurMembers { get; private set; } = [];
    public string OurGuild { get; private set; } = string.Empty;
    public WarRoomPage Page { get; private set; }
    public bool Ranked { get; set; }
    public List<HallBattleBoardRow> Rows { get; private set; } = [];
    public string SelectedGuild { get; set; } = string.Empty;
    public byte TeamSize { get; private set; }
    public string TheirGuild { get; private set; } = string.Empty;
    public List<string> TheirMembers { get; private set; } = [];
    public string ViewerGuild { get; private set; } = string.Empty;
    public bool ViewerIsLeader { get; private set; }
    public byte ChosenSize { get; set; } = TeamSizes[0];
    public string WagerText { get; set; } = string.Empty;

    public string OurCountText => $"{OurMembers.Count}/{TeamSize}";
    public string TheirCountText => $"{TheirMembers.Count}/{TeamSize}";

    public bool ShowBlockedReason => ShowChallengeForm && (BlockedReason.Length > 0);
    public bool ShowChallengeButton => ShowChallengeForm && (BlockedReason.Length == 0);
    public bool ShowChallengeForm => ViewerIsLeader && HasDetail && CanChallenge;
    public bool WagerEnabled => Ranked;

    public uint Wager => Ranked && uint.TryParse(WagerText, out var wager) ? wager : 0;

    public void Apply(HallBattleDisplayArgs args)
    {
        switch (args.Kind)
        {
            case HallBattleDisplayKind.Board:
                Page = WarRoomPage.Board;
                ViewerGuild = args.ViewerGuild;
                ViewerIsLeader = args.ViewerIsLeader;

                Rows = args.BoardRows
                           .OrderByDescending(r => r.Rating)
                           .ThenBy(r => r.GuildName, StringComparer.OrdinalIgnoreCase)
                           .ToList();

                break;

            case HallBattleDisplayKind.GuildDetail:
                Page = WarRoomPage.Board;

                if (args.ViewerGuild.Length > 0)
                {
                    ViewerGuild = args.ViewerGuild;
                    ViewerIsLeader = args.ViewerIsLeader;
                }

                HasDetail = true;
                DetailGuild = args.DetailGuild;
                SelectedGuild = args.DetailGuild;
                DetailRating = args.DetailRating;
                DetailWins = args.DetailWins;
                DetailLosses = args.DetailLosses;
                DetailDraws = args.DetailDraws;
                LastMeeting = args.LastMeeting;
                CanChallenge = args.CanChallenge;
                BlockedReason = args.ChallengeBlockedReason;
                History = args.History;

                break;

            case HallBattleDisplayKind.Team:
                Page = WarRoomPage.Team;
                var newBattle = !OurGuild.Equals(args.OurGuild, StringComparison.OrdinalIgnoreCase)
                    || !TheirGuild.Equals(args.TheirGuild, StringComparison.OrdinalIgnoreCase);
                OurGuild = args.OurGuild;
                TheirGuild = args.TheirGuild;
                TeamSize = args.TeamSize;
                FormingOpenInvite = args.Mode == FORMING_OPEN_INVITE;
                OurMembers = args.OurMembers;
                TheirMembers = args.TheirMembers;
                Candidates = args.Candidates;
                Invited = args.Invited;
                TeamSeconds = args.SecondsLeft;
                TeamReceivedAt = DateTime.UtcNow;
                if (newBattle)
                    CandidateOffset = 0;

                break;
        }
    }

    public HallBattleActionArgs ChallengeAction()
        => new()
        {
            Action = HallBattleActionKind.Challenge,
            GuildName = DetailGuild,
            TeamSize = ChosenSize,
            Ranked = Ranked,
            Wager = Wager,
            Mode = (byte)(FormingOpenInvite ? FORMING_OPEN_INVITE : FORMING_HAND_PICK)
        };

    public bool IsInvited(string name) => Invited.Contains(name, StringComparer.OrdinalIgnoreCase);
    public int CandidateMaxOffset(int pageSize) => Candidates.Count == 0 ? 0 : ((Candidates.Count - 1) / pageSize * pageSize);
    public int CandidatePages(int pageSize) => Math.Max(1, (Candidates.Count + pageSize - 1) / pageSize);
    public void PageCandidates(int step, int pageSize)
        => CandidateOffset = Math.Clamp(CandidateOffset + (step * pageSize), 0, CandidateMaxOffset(pageSize));
    public void ClampCandidateOffset(int pageSize) => CandidateOffset = Math.Min(CandidateOffset, CandidateMaxOffset(pageSize));

    public HallBattleActionArgs PickAction(string name)
        => new()
        {
            Action = HallBattleActionKind.Pick,
            PlayerName = name,
            Picked = !IsInvited(name)
        };

    public int SecondsLeft(DateTime now) => Math.Max(0, TeamSeconds - (int)(now - TeamReceivedAt).TotalSeconds);

    public static HallBattleActionArgs SelectAction(string guild)
        => new()
        {
            Action = HallBattleActionKind.SelectGuild,
            GuildName = guild
        };

    public static string ClockText(int seconds) => $"{seconds / 60}:{seconds % 60:00}";
}
