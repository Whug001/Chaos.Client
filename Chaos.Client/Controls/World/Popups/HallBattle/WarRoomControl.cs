#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.ArenaHost;
using Chaos.Client.Rendering.Definitions;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.HallBattle;

/// <summary>Where every war room control sits, relative to the window. The control and its layout test both read this.</summary>
public static class WarRoomLayout
{
    public const int WIDTH = 600;
    public const int HEIGHT = 420;
    public const int LIST_ROWS = 18;
    public const int HISTORY_ROWS = 6;
    public const int TEAM_ROWS = 8;
    public const int CANDIDATE_ROWS = 14;
    public const int ROW = ArenaHostRow.HEIGHT;
    public const int LABEL = 14;
    public const int BUTTON = CustomButton.HEIGHT;

    public static readonly Rectangle Title = new(10, 8, 580, LABEL);
    public static readonly Rectangle Subtitle = new(10, 24, 580, LABEL);
    public static readonly Rectangle CloseButton = new(480, 388, 110, BUTTON);

    //board page
    public static readonly Rectangle ListPrev = new(10, 288, 56, BUTTON);
    public static readonly Rectangle ListNext = new(70, 288, 56, BUTTON);
    public static readonly Rectangle ListPageLabel = new(132, 294, 100, LABEL);
    public static readonly Rectangle DetailName = new(252, 30, 338, LABEL);
    public static readonly Rectangle DetailRecord = new(252, 46, 338, LABEL);
    public static readonly Rectangle DetailLast = new(252, 62, 338, LABEL);
    public static readonly Rectangle HistoryHeader = new(252, 80, 338, LABEL);
    public static readonly Rectangle SizeLabel = new(252, 190, 50, LABEL);
    public static readonly Rectangle RankedButton = new(252, 212, 100, BUTTON);
    public static readonly Rectangle FriendlyButton = new(358, 212, 100, BUTTON);
    public static readonly Rectangle WagerLabel = new(252, 244, 50, LABEL);
    public static readonly Rectangle WagerBox = new(306, 238, 100, BUTTON);
    public static readonly Rectangle HandPickButton = new(252, 266, 150, BUTTON);
    public static readonly Rectangle OpenInviteButton = new(408, 266, 150, BUTTON);
    public static readonly Rectangle ChallengeButton = new(252, 294, 120, BUTTON);
    public static readonly Rectangle BlockedReason = new(252, 298, 338, 2 * LABEL);

    //team page
    public static readonly Rectangle OurHeader = new(10, 44, 180, LABEL);
    public static readonly Rectangle TheirHeader = new(200, 44, 180, LABEL);
    public static readonly Rectangle CandidateHeader = new(390, 44, 200, LABEL);

    public static Rectangle ListRow(int index) => new(10, 30 + (index * ROW), 230, ROW);
    public static Rectangle HistoryRow(int index) => new(252, 94 + (index * ROW), 338, ROW);
    public static Rectangle SizeButton(int index) => new(306 + (index * 52), 184, 48, BUTTON);
    public static Rectangle OurRow(int index) => new(10, 60 + (index * ROW), 180, ROW);
    public static Rectangle TheirRow(int index) => new(200, 60 + (index * ROW), 180, ROW);
    public static Rectangle CandidateRow(int index) => new(390, 60 + (index * ROW), 200, ROW);

    /// <summary>Every control the page can show, relative to the window.</summary>
    public static IEnumerable<Rectangle> VisibleBounds(WarRoomPage page)
    {
        yield return Title;
        yield return CloseButton;

        if (page == WarRoomPage.Team)
        {
            yield return Subtitle;
            yield return OurHeader;
            yield return TheirHeader;
            yield return CandidateHeader;

            for (var i = 0; i < TEAM_ROWS; i++)
            {
                yield return OurRow(i);
                yield return TheirRow(i);
            }

            for (var i = 0; i < CANDIDATE_ROWS; i++)
                yield return CandidateRow(i);

            yield break;
        }

        foreach (var rect in new[]
                 {
                     ListPrev, ListNext, ListPageLabel, DetailName, DetailRecord, DetailLast, HistoryHeader, SizeLabel, RankedButton,
                     FriendlyButton, WagerLabel, WagerBox, HandPickButton, OpenInviteButton, ChallengeButton, BlockedReason
                 })
            yield return rect;

        for (var i = 0; i < LIST_ROWS; i++)
            yield return ListRow(i);

        for (var i = 0; i < HISTORY_ROWS; i++)
            yield return HistoryRow(i);

        for (var i = 0; i < WarRoomState.TeamSizes.Length; i++)
            yield return SizeButton(i);
    }
}

/// <summary>
///     The hall battle war room. Board page: guilds by rating on the left, the picked guild's record and history on the
///     right and, for a leader who can challenge, the challenge form. Team page: both rosters and the candidates to pick.
///     Every button only asks; the server answers with a fresh display.
/// </summary>
public sealed class WarRoomControl : UIPanel
{
    private readonly UILabel BlockedLabel;
    private readonly string?[] CandidateNames = new string[WarRoomLayout.CANDIDATE_ROWS];
    private readonly ArenaHostRow[] CandidateRows = new ArenaHostRow[WarRoomLayout.CANDIDATE_ROWS];
    private readonly CustomButton ChallengeButton;
    private readonly UILabel CandidateHeader;
    private readonly UILabel DetailLast;
    private readonly UILabel DetailName;
    private readonly UILabel DetailRecord;
    private readonly CustomButton FriendlyButton;
    private readonly CustomButton HandPickButton;
    private readonly UILabel HistoryHeader;
    private readonly UILabel[] HistoryLabels = new UILabel[WarRoomLayout.HISTORY_ROWS];
    private readonly string?[] ListNames = new string[WarRoomLayout.LIST_ROWS];
    private readonly UILabel ListPageLabel;
    private readonly ArenaHostRow[] ListRows = new ArenaHostRow[WarRoomLayout.LIST_ROWS];
    private readonly CustomButton NextButton;
    private readonly CustomButton OpenInviteButton;
    private readonly ArenaHostRow[] OurRows = new ArenaHostRow[WarRoomLayout.TEAM_ROWS];
    private readonly UILabel OurHeader;
    private readonly CustomButton PrevButton;
    private readonly CustomButton RankedButton;
    private readonly CustomButton[] SizeButtons = new CustomButton[WarRoomState.TeamSizes.Length];
    private readonly UILabel SizeLabel;
    private readonly WarRoomState State = new();
    private readonly UILabel Subtitle;
    private readonly ArenaHostRow[] TheirRows = new ArenaHostRow[WarRoomLayout.TEAM_ROWS];
    private readonly UILabel TheirHeader;
    private readonly UILabel Title;
    private readonly UILabel WagerLabel;
    private readonly CustomTextBox WagerBox;
    private int ListOffset;

    public WarRoomControl(int screenWidth, int screenHeight)
    {
        Name = "WarRoom";
        Width = WarRoomLayout.WIDTH;
        Height = WarRoomLayout.HEIGHT;
        X = (screenWidth - WarRoomLayout.WIDTH) / 2;
        Y = (screenHeight - WarRoomLayout.HEIGHT) / 2;
        Visible = false;
        UsesControlStack = true;
        BackgroundColor = new Color(28, 20, 12);
        BorderColor = LegendColors.Gold;

        Title = Label(WarRoomLayout.Title, LegendColors.Gold);
        Subtitle = Label(WarRoomLayout.Subtitle, LegendColors.White);
        DetailName = Label(WarRoomLayout.DetailName, LegendColors.Gold);
        DetailRecord = Label(WarRoomLayout.DetailRecord, LegendColors.White);
        DetailLast = Label(WarRoomLayout.DetailLast, LegendColors.White);
        HistoryHeader = Label(WarRoomLayout.HistoryHeader, LegendColors.Gold);
        SizeLabel = Label(WarRoomLayout.SizeLabel, LegendColors.White);
        WagerLabel = Label(WarRoomLayout.WagerLabel, LegendColors.White);
        ListPageLabel = Label(WarRoomLayout.ListPageLabel, LegendColors.White);
        OurHeader = Label(WarRoomLayout.OurHeader, LegendColors.Gold);
        TheirHeader = Label(WarRoomLayout.TheirHeader, LegendColors.Gold);
        CandidateHeader = Label(WarRoomLayout.CandidateHeader, LegendColors.Gold);
        BlockedLabel = Label(WarRoomLayout.BlockedReason, LegendColors.Red);
        BlockedLabel.WordWrap = true;
        SizeLabel.Text = "Size";
        WagerLabel.Text = "Wager";
        HistoryHeader.Text = "Recent meetings";
        CandidateHeader.Text = "Pick members";

        for (var i = 0; i < ListRows.Length; i++)
        {
            var slot = i;
            ListRows[i] = Row(WarRoomLayout.ListRow(i));
            ListRows[i].Clicked += () => SelectGuild(slot);
        }

        for (var i = 0; i < HistoryLabels.Length; i++)
            HistoryLabels[i] = Label(WarRoomLayout.HistoryRow(i), LegendColors.White);

        for (var i = 0; i < OurRows.Length; i++)
        {
            OurRows[i] = Row(WarRoomLayout.OurRow(i));
            TheirRows[i] = Row(WarRoomLayout.TheirRow(i));
        }

        for (var i = 0; i < CandidateRows.Length; i++)
        {
            var slot = i;
            CandidateRows[i] = Row(WarRoomLayout.CandidateRow(i));
            CandidateRows[i].Clicked += () => Pick(slot);
        }

        for (var i = 0; i < SizeButtons.Length; i++)
        {
            var size = WarRoomState.TeamSizes[i];
            var rect = WarRoomLayout.SizeButton(i);
            SizeButtons[i] = Button(size.ToString(), rect);
            SizeButtons[i].Clicked += () =>
            {
                State.ChosenSize = size;
                Refresh();
            };
        }

        RankedButton = Button("Ranked", WarRoomLayout.RankedButton);
        FriendlyButton = Button("Friendly", WarRoomLayout.FriendlyButton);
        HandPickButton = Button("Hand pick", WarRoomLayout.HandPickButton);
        OpenInviteButton = Button("Open invite", WarRoomLayout.OpenInviteButton);
        ChallengeButton = Button("Challenge", WarRoomLayout.ChallengeButton);
        PrevButton = Button("Prev", WarRoomLayout.ListPrev);
        NextButton = Button("Next", WarRoomLayout.ListNext);
        var close = Button("Close", WarRoomLayout.CloseButton);

        WagerBox = new CustomTextBox
        {
            X = WarRoomLayout.WagerBox.X,
            Y = WarRoomLayout.WagerBox.Y,
            Width = WarRoomLayout.WagerBox.Width,
            Height = WarRoomLayout.WagerBox.Height,
            DigitsOnly = true,
            MaxLength = 9,
            HintText = "gold"
        };

        AddChild(WagerBox);

        RankedButton.Clicked += () => SetRanked(true);
        FriendlyButton.Clicked += () => SetRanked(false);
        HandPickButton.Clicked += () => SetOpenInvite(false);
        OpenInviteButton.Clicked += () => SetOpenInvite(true);
        PrevButton.Clicked += () => PageList(-1);
        NextButton.Clicked += () => PageList(1);
        close.Clicked += Hide;

        ChallengeButton.Clicked += () =>
        {
            State.WagerText = WagerBox.Text;
            Action?.Invoke(State.ChallengeAction());
        };

        Refresh();
    }

    public event Action<HallBattleActionArgs>? Action;

    public WarRoomPage Page => State.Page;

    public void Apply(HallBattleDisplayArgs args)
    {
        State.Apply(args);
        Refresh();

        if (!Visible)
            Show();
    }

    public void Hide()
    {
        if (UsesControlStack)
            InputDispatcher.Instance?.RemoveControl(this);

        Visible = false;
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            Hide();
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    public void Show()
    {
        if (UsesControlStack)
            InputDispatcher.Instance?.PushControl(this);

        Visible = true;
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (Visible && (State.Page == WarRoomPage.Team))
            Subtitle.Text = TeamSubtitle();
    }

    private ArenaHostRow Row(Rectangle rect)
    {
        var row = new ArenaHostRow(rect.Width) { X = rect.X, Y = rect.Y, Visible = false };
        AddChild(row);

        return row;
    }

    private UILabel Label(Rectangle rect, Color color)
    {
        var label = new UILabel
        {
            X = rect.X,
            Y = rect.Y,
            Width = rect.Width,
            Height = rect.Height,
            ForegroundColor = color,
            IsHitTestVisible = false
        };

        AddChild(label);

        return label;
    }

    private CustomButton Button(string caption, Rectangle rect)
    {
        var button = new CustomButton(caption, rect.Width) { X = rect.X, Y = rect.Y };
        AddChild(button);

        return button;
    }

    private void PageList(int step)
    {
        var maxOffset = Math.Max(0, State.Rows.Count - WarRoomLayout.LIST_ROWS);
        ListOffset = Math.Clamp(ListOffset + (step * WarRoomLayout.LIST_ROWS), 0, maxOffset);
        Refresh();
    }

    private void Pick(int slot)
    {
        if (CandidateNames[slot] is { } name)
            Action?.Invoke(State.PickAction(name));
    }

    private void SelectGuild(int slot)
    {
        if (ListNames[slot] is not { } name)
            return;

        State.SelectedGuild = name;
        Refresh();
        Action?.Invoke(WarRoomState.SelectAction(name));
    }

    private void SetOpenInvite(bool openInvite)
    {
        State.FormingOpenInvite = openInvite;
        Refresh();
    }

    private void SetRanked(bool ranked)
    {
        State.Ranked = ranked;
        Refresh();
    }

    private string TeamSubtitle()
        => $"{State.OurGuild} vs {State.TheirGuild}  -  {(State.FormingOpenInvite ? "Open invite" : "Hand pick")}  -  {WarRoomState.ClockText(State.SecondsLeft(DateTime.UtcNow))} left";

    private void Refresh()
    {
        var team = State.Page == WarRoomPage.Team;

        Title.Text = team ? "Hall Battle - Pick your team" : "War Room";
        Subtitle.Visible = team;
        OurHeader.Visible = team;
        TheirHeader.Visible = team;
        CandidateHeader.Visible = team;

        foreach (var row in OurRows.Concat(TheirRows).Concat(CandidateRows))
            row.Visible = false;

        SetBoardVisible(!team);

        if (team)
            RefreshTeam();
        else
            RefreshBoard();
    }

    private void SetBoardVisible(bool visible)
    {
        foreach (var control in new UIElement[]
                 {
                     PrevButton, NextButton, ListPageLabel, DetailName, DetailRecord, DetailLast, HistoryHeader, SizeLabel, RankedButton,
                     FriendlyButton, WagerLabel, WagerBox, HandPickButton, OpenInviteButton, ChallengeButton, BlockedLabel
                 }.Concat(SizeButtons)
                  .Concat(HistoryLabels)
                  .Concat(ListRows))
            control.Visible = visible;
    }

    private void RefreshBoard()
    {
        var maxOffset = Math.Max(0, State.Rows.Count - WarRoomLayout.LIST_ROWS);
        ListOffset = Math.Min(ListOffset, maxOffset);
        var pages = Math.Max(1, (State.Rows.Count + WarRoomLayout.LIST_ROWS - 1) / WarRoomLayout.LIST_ROWS);
        ListPageLabel.Text = $"Page {(ListOffset / WarRoomLayout.LIST_ROWS) + 1}/{pages}";
        PrevButton.Enabled = ListOffset > 0;
        NextButton.Enabled = ListOffset < maxOffset;

        for (var i = 0; i < ListRows.Length; i++)
        {
            var index = ListOffset + i;
            var row = ListRows[i];

            if (index >= State.Rows.Count)
            {
                ListNames[i] = null;
                row.Visible = false;
                row.Selected = false;

                continue;
            }

            var entry = State.Rows[index];
            ListNames[i] = entry.GuildName;
            row.Show(RowText(entry), entry.GuildName.Equals(State.ViewerGuild, StringComparison.OrdinalIgnoreCase) ? LegendColors.Gold : LegendColors.White);
            row.Selected = entry.GuildName.Equals(State.SelectedGuild, StringComparison.OrdinalIgnoreCase);
        }

        var detail = State.HasDetail;
        DetailName.Text = detail ? State.DetailGuild : "Pick a guild";
        DetailRecord.Text = detail ? $"Rating {State.DetailRating}   Won {State.DetailWins}  Lost {State.DetailLosses}  Drew {State.DetailDraws}" : string.Empty;
        DetailLast.Text = detail ? $"Last meeting: {(State.LastMeeting.Length > 0 ? State.LastMeeting : "never")}" : string.Empty;
        HistoryHeader.Visible = detail;

        for (var i = 0; i < HistoryLabels.Length; i++)
        {
            HistoryLabels[i].Visible = detail && (i < State.History.Count);

            if (HistoryLabels[i].Visible)
                HistoryLabels[i].Text = HistoryText(State.History[i]);
        }

        var form = State.ShowChallengeForm;

        foreach (var control in new UIElement[] { SizeLabel, RankedButton, FriendlyButton, WagerLabel, WagerBox, HandPickButton, OpenInviteButton }.Concat(SizeButtons))
            control.Visible = form;

        ChallengeButton.Visible = State.ShowChallengeButton;
        BlockedLabel.Visible = State.ShowBlockedReason;
        BlockedLabel.Text = State.BlockedReason;

        for (var i = 0; i < SizeButtons.Length; i++)
            SizeButtons[i].Selected = WarRoomState.TeamSizes[i] == State.ChosenSize;

        RankedButton.Selected = State.Ranked;
        FriendlyButton.Selected = !State.Ranked;
        WagerBox.Enabled = State.WagerEnabled;
        HandPickButton.Selected = !State.FormingOpenInvite;
        OpenInviteButton.Selected = State.FormingOpenInvite;
    }

    private void RefreshTeam()
    {
        Subtitle.Text = TeamSubtitle();
        OurHeader.Text = $"{State.OurGuild} {State.OurCountText}";
        TheirHeader.Text = $"{State.TheirGuild} {State.TheirCountText}";

        for (var i = 0; i < WarRoomLayout.TEAM_ROWS; i++)
        {
            if (i < State.OurMembers.Count)
                OurRows[i].Show(State.OurMembers[i], LegendColors.White);

            if (i < State.TheirMembers.Count)
                TheirRows[i].Show(State.TheirMembers[i], LegendColors.White);
        }

        for (var i = 0; i < CandidateRows.Length; i++)
        {
            if (i >= State.Candidates.Count)
            {
                CandidateNames[i] = null;

                continue;
            }

            var name = State.Candidates[i];
            var invited = State.IsInvited(name);
            CandidateNames[i] = name;
            CandidateRows[i].Show(invited ? $"[x] {name}" : $"[ ] {name}", invited ? LegendColors.Lime : LegendColors.White);
        }
    }

    public static string RowText(HallBattleBoardRow row)
        => $"{row.GuildName}  {row.Rating}  {row.Wins}/{row.Losses}/{row.Draws}";

    public static string HistoryText(HallBattleHistoryRow row)
    {
        var result = row.Result switch
        {
            0 => "Won",
            1 => "Lost",
            _ => "Drew"
        };

        return $"{row.Date}  {result} vs {row.Opponent} ({(row.Ranked ? "ranked" : "friendly")})";
    }
}
