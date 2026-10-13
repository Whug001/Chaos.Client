#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Controls.World.Popups.ArenaHost;

/// <summary>
///     The Arena Host Panel: the modes on the left, the chosen mode's settings in the middle (or the vote picker), the
///     Undercroft roster on the right, and the footer with the problem line, the server's answer and the Start button.
///     Opened by the server's ArenaHostDisplay Open; the click logic is <see cref="ArenaHostPanelState" />, and every rule
///     is checked again on the server.
/// </summary>
/// <remarks>
///     Every row and button is built once here; <see cref="RefreshAll" /> only sets text, colours and visibility. The
///     columns end at or above the footer line minus 4 px, so the window fits the 640 x 480 screen.
/// </remarks>
public sealed class ArenaHostPanelControl : GuildCloakDialogBase
{
    private const int PANEL_WIDTH = 616;
    private const int PANEL_HEIGHT = 464;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;

    private const int LEFT = 20;
    private const int GAP = 8;
    private const int TITLE_TOP = 8;
    private const int TOP = 28;
    private const int ROW = ArenaHostRow.HEIGHT;
    private const int BORDER_GAP = 4;

    private const int MODES_LEFT = LEFT;
    private const int MODES_WIDTH = 186;
    private const int MODE_ROWS = 11;

    private const int SETTINGS_LEFT = MODES_LEFT + MODES_WIDTH + GAP;
    private const int SETTINGS_WIDTH = 224;
    private const int SETTING_ROWS = 8;
    private const int OPTIONS_TOP = TOP + ROW + (4 * TextRenderer.CHAR_HEIGHT) + 4;
    private const int OPTION_STEP = CustomButton.HEIGHT + 2;
    private const int OPTION_LABEL_WIDTH = 84;
    private const int ARROW_WIDTH = 20;
    private const int VALUE_WIDTH = 94;

    private const int ROSTER_LEFT = SETTINGS_LEFT + SETTINGS_WIDTH + GAP;
    private const int ROSTER_WIDTH = 150;
    private const int ROSTER_PAGE_SIZE = 19;
    private const int ROSTER_BUTTON_WIDTH = 48;

    private const int FOOTER_TOP = PANEL_HEIGHT - BORDER_BOTTOM_HEIGHT - CustomButton.HEIGHT - 26;
    private const int MAIN_BUTTON_WIDTH = 130;

    private readonly CustomButton AdminPlayButton;
    private readonly CustomButton BackButton;
    private readonly UILabel DescriptionLabel;
    private readonly UILabel FreeForAllCaption;
    private readonly CustomButton MainButton;
    private readonly UILabel MessageLabel;
    private readonly byte[] ModeIds = new byte[2 * MODE_ROWS];
    private readonly UILabel ModeNameLabel;
    private readonly ArenaHostRow[] ModeRows = new ArenaHostRow[2 * MODE_ROWS];
    private readonly CustomButton[] NextOptionButtons = new CustomButton[SETTING_ROWS];
    private readonly CustomButton NextRosterPageButton;
    private readonly CustomButton OpenVoteButton;
    private readonly ArenaHostOption[] OptionIds = new ArenaHostOption[SETTING_ROWS];
    private readonly UILabel[] OptionLabels = new UILabel[SETTING_ROWS];
    private readonly ArenaHostRow[] OptionValueRows = new ArenaHostRow[SETTING_ROWS];
    private readonly UILabel[] PickedLabels = new UILabel[ArenaHostProtocol.MAX_VOTE_MODES];
    private readonly CustomButton[] PrevOptionButtons = new CustomButton[SETTING_ROWS];
    private readonly CustomButton PrevRosterPageButton;
    private readonly UILabel ProblemLabel;
    private readonly UILabel RosterCaption;
    private readonly string[] RosterNames = new string[ROSTER_PAGE_SIZE];
    private readonly UILabel RosterPageLabel;
    private readonly ArenaHostRow[] RosterRows = new ArenaHostRow[ROSTER_PAGE_SIZE];
    private readonly CustomButton SecondsButton;
    private readonly ArenaHostPanelState State = new();
    private readonly UILabel TeamCaption;
    private readonly UILabel VoteHintLabel;
    private readonly CustomButton VoteModeButton;

    //set while the server closes the window, so Hide does not answer with a Close action
    private bool ClosedByServer;
    private int RosterPage;

    public ArenaHostPanelControl()
        : base("_nsett", false)
    {
        Name = "ArenaHostPanel";
        Visible = false;
        UsesControlStack = true;

        Width = PANEL_WIDTH;
        Height = PANEL_HEIGHT;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(Hide, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);

        Caption("Arena Host", 0, TITLE_TOP, Width, HorizontalAlignment.Center, LegendColors.Gold);

        //modes
        FreeForAllCaption = Caption("Free-for-all", MODES_LEFT, TOP, MODES_WIDTH, color: LegendColors.Gold);
        TeamCaption = Caption("Team", MODES_LEFT, TOP + ((MODE_ROWS + 1) * ROW), MODES_WIDTH, color: LegendColors.Gold);

        for (var i = 0; i < ModeRows.Length; i++)
        {
            var slot = i;
            var section = i / MODE_ROWS;
            var y = TOP + ROW + (section * (MODE_ROWS + 1) * ROW) + ((i % MODE_ROWS) * ROW);

            var row = new ArenaHostRow(MODES_WIDTH)
            {
                X = MODES_LEFT,
                Y = y,
                Visible = false
            };

            row.Clicked += () => Raise(State.ClickMode(ModeIds[slot]));
            AddChild(row);
            ModeRows[i] = row;
        }

        //settings
        ModeNameLabel = Caption(string.Empty, SETTINGS_LEFT, TOP, SETTINGS_WIDTH, color: LegendColors.Gold);

        DescriptionLabel = new UILabel
        {
            X = SETTINGS_LEFT,
            Y = TOP + ROW,
            Width = SETTINGS_WIDTH,
            Height = 4 * TextRenderer.CHAR_HEIGHT,
            WordWrap = true,
            VerticalAlignment = VerticalAlignment.Top,
            ForegroundColor = LegendColors.Gray,
            IsHitTestVisible = false
        };

        AddChild(DescriptionLabel);

        for (var i = 0; i < SETTING_ROWS; i++)
        {
            var slot = i;
            var y = OPTIONS_TOP + (i * OPTION_STEP);

            OptionLabels[i] = Caption(
                string.Empty,
                SETTINGS_LEFT,
                y + ((CustomButton.HEIGHT - TextRenderer.CHAR_HEIGHT) / 2),
                OPTION_LABEL_WIDTH,
                color: LegendColors.Gray);

            PrevOptionButtons[i] = AddButton(
                "<",
                ARROW_WIDTH,
                SETTINGS_LEFT + OPTION_LABEL_WIDTH + 2,
                y,
                () => Raise(State.ClickOption(OptionIds[slot], false)));

            var value = new ArenaHostRow(VALUE_WIDTH)
            {
                X = SETTINGS_LEFT + OPTION_LABEL_WIDTH + 2 + ARROW_WIDTH + 2,
                Y = y + ((CustomButton.HEIGHT - ROW) / 2),
                Visible = false
            };

            value.Clicked += () => Raise(State.ClickOption(OptionIds[slot], true));
            AddChild(value);
            OptionValueRows[i] = value;

            NextOptionButtons[i] = AddButton(
                ">",
                ARROW_WIDTH,
                SETTINGS_LEFT + SETTINGS_WIDTH - ARROW_WIDTH,
                y,
                () => Raise(State.ClickOption(OptionIds[slot], true)));
        }

        var underOptions = OPTIONS_TOP + (SETTING_ROWS * OPTION_STEP) + 2;
        VoteModeButton = AddButton("Open vote...", 120, SETTINGS_LEFT, underOptions, OnVoteModeClicked);
        AdminPlayButton = AddButton(string.Empty, 160, SETTINGS_LEFT, underOptions + OPTION_STEP, () => Raise(State.ToggleAdminPlay()));

        //vote picker, in the settings column
        VoteHintLabel = Caption("Tick 2 to 5 rites on the left.", SETTINGS_LEFT, TOP, SETTINGS_WIDTH);

        for (var i = 0; i < PickedLabels.Length; i++)
            PickedLabels[i] = Caption(string.Empty, SETTINGS_LEFT, TOP + ROW + (i * ROW), SETTINGS_WIDTH, color: LegendColors.Gray);

        var voteButtonsTop = TOP + ROW + (PickedLabels.Length * ROW) + 6;
        SecondsButton = AddButton(string.Empty, 100, SETTINGS_LEFT, voteButtonsTop, OnSecondsClicked);
        OpenVoteButton = AddButton("Open vote", 100, SETTINGS_LEFT, voteButtonsTop + OPTION_STEP, () => Raise(State.OpenVote()));
        BackButton = AddButton("Back", 100, SETTINGS_LEFT + 104, voteButtonsTop + OPTION_STEP, OnVoteModeClicked);

        //roster
        RosterCaption = Caption(string.Empty, ROSTER_LEFT, TOP, ROSTER_WIDTH, color: LegendColors.Gold);

        for (var i = 0; i < ROSTER_PAGE_SIZE; i++)
        {
            var slot = i;

            var row = new ArenaHostRow(ROSTER_WIDTH)
            {
                X = ROSTER_LEFT,
                Y = TOP + ROW + (i * ROW),
                Visible = false
            };

            row.Clicked += () => Raise(State.ClickRosterName(RosterNames[slot]));
            AddChild(row);
            RosterRows[i] = row;
            RosterNames[i] = string.Empty;
        }

        var pageTop = TOP + ROW + (ROSTER_PAGE_SIZE * ROW) + 3;
        PrevRosterPageButton = AddButton("<", 24, ROSTER_LEFT, pageTop, () => TurnRosterPage(-1));
        NextRosterPageButton = AddButton(">", 24, ROSTER_LEFT + ROSTER_WIDTH - 24, pageTop, () => TurnRosterPage(1));

        RosterPageLabel = Caption(
            string.Empty,
            ROSTER_LEFT + 24,
            pageTop + ((CustomButton.HEIGHT - TextRenderer.CHAR_HEIGHT) / 2),
            ROSTER_WIDTH - 48,
            HorizontalAlignment.Center,
            LegendColors.Gray);

        var teamTop = pageTop + CustomButton.HEIGHT + 3;
        var teamStep = (ROSTER_WIDTH - ROSTER_BUTTON_WIDTH) / 2;
        AddButton("Split 2", ROSTER_BUTTON_WIDTH, ROSTER_LEFT, teamTop, () => Raise(State.Split(2)));
        AddButton("Split 4", ROSTER_BUTTON_WIDTH, ROSTER_LEFT + teamStep, teamTop, () => Raise(State.Split(4)));
        AddButton("Clear", ROSTER_BUTTON_WIDTH, ROSTER_LEFT + (2 * teamStep), teamTop, () => Raise(State.ClearTeams()));

        //footer
        var footerWidth = Width - (2 * LEFT) - MAIN_BUTTON_WIDTH - GAP;
        ProblemLabel = Caption(string.Empty, LEFT, FOOTER_TOP, footerWidth, color: LegendColors.Gray);
        MessageLabel = Caption(string.Empty, LEFT, FOOTER_TOP + ROW, footerWidth);

        MainButton = AddButton(
            "Start",
            MAIN_BUTTON_WIDTH,
            Width - LEFT - MAIN_BUTTON_WIDTH,
            Height - BORDER_BOTTOM_HEIGHT - BORDER_GAP - CustomButton.HEIGHT,
            () => Raise(State.ClickMainButton()));
    }

    /// <summary>Raised with each action to send to the server, including Close when the player hides the window.</summary>
    public event Action<ArenaHostActionArgs>? ActionRequested;

    public void Open(ArenaHostDisplayArgs args)
    {
        State.Reset();
        RosterPage = 0;
        State.Apply(args);
        RefreshAll();
        Show();
    }

    public void Refresh(ArenaHostDisplayArgs args)
    {
        if (!Visible)
            return;

        State.Apply(args);
        RefreshAll();
    }

    /// <summary>The server closed the panel; nothing is sent back.</summary>
    public void CloseFromServer()
    {
        ClosedByServer = true;
        Hide();
        ClosedByServer = false;
    }

    public override void Hide()
    {
        if (!Visible)
            return;

        base.Hide();

        if (!ClosedByServer)
            ActionRequested?.Invoke(ArenaHostPanelState.CloseAction());
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (e.Keycode == Keycode.Escape)
        {
            if (State.CancelPick())
                RefreshAll();
            else if (State.VoteMode)
            {
                State.ToggleVoteMode();
                RefreshAll();
            } else
                Hide();

            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    private void OnSecondsClicked()
    {
        State.CycleVoteSeconds();
        RefreshAll();
    }

    private void OnVoteModeClicked()
    {
        State.ToggleVoteMode();
        RefreshAll();
    }

    private void Raise(ArenaHostActionArgs? args)
    {
        if (args is not null)
            ActionRequested?.Invoke(args);

        RefreshAll();
    }

    private void RefreshAll()
    {
        if (State.Args is not { } args)
            return;

        RefreshModes(args);
        RefreshSettings(args);
        RefreshRoster(args);

        var hasProblem = !string.IsNullOrEmpty(args.Problem);
        ProblemLabel.Text = hasProblem ? args.Problem : args.StatusText;
        ProblemLabel.ForegroundColor = hasProblem ? LegendColors.Orange : LegendColors.Gray;
        MessageLabel.Text = State.Message;

        MainButton.Caption = State.MainButtonCaption;
        MainButton.Enabled = State.MainButtonEnabled;
    }

    private void RefreshModes(ArenaHostDisplayArgs args)
    {
        var freeForAll = args.Modes.Where(m => !m.IsTeam).ToList();
        var team = args.Modes.Where(m => m.IsTeam).ToList();

        FreeForAllCaption.Visible = freeForAll.Count > 0;
        TeamCaption.Visible = team.Count > 0;

        for (var i = 0; i < ModeRows.Length; i++)
        {
            var section = i < MODE_ROWS ? freeForAll : team;
            var index = i % MODE_ROWS;
            var row = ModeRows[i];

            if (index >= section.Count)
            {
                row.Visible = false;
                row.Selected = false;

                continue;
            }

            var mode = section[index];
            ModeIds[i] = mode.Id;
            row.Show(ArenaHostRow.ModeText(mode, State.VoteMode, State.VoteSelection.Contains(mode.Id)), LegendColors.White);
            row.Selected = mode.Id == args.SelectedMode;
        }
    }

    private void RefreshRoster(ArenaHostDisplayArgs args)
    {
        var pages = Math.Max(1, (args.Roster.Count + ROSTER_PAGE_SIZE - 1) / ROSTER_PAGE_SIZE);
        RosterPage = Math.Clamp(RosterPage, 0, pages - 1);

        RosterCaption.Text = State.PickingCaptain switch
        {
            ArenaHostOption.GreenCaptain => "Click the Green captain",
            ArenaHostOption.GoldCaptain  => "Click the Gold captain",
            _                            => $"Undercroft ({args.Roster.Count})"
        };

        for (var i = 0; i < ROSTER_PAGE_SIZE; i++)
        {
            var index = (RosterPage * ROSTER_PAGE_SIZE) + i;
            var row = RosterRows[i];

            if (index >= args.Roster.Count)
            {
                row.Visible = false;
                RosterNames[i] = string.Empty;

                continue;
            }

            var player = args.Roster[index];
            RosterNames[i] = player.Name;
            row.Show(ArenaHostRow.RosterText(player), ArenaHostRow.TeamColor(player.Team));
        }

        RosterPageLabel.Text = $"{RosterPage + 1}/{pages}";
        PrevRosterPageButton.Enabled = RosterPage > 0;
        NextRosterPageButton.Enabled = RosterPage < (pages - 1);
    }

    private void RefreshSettings(ArenaHostDisplayArgs args)
    {
        var voting = State.VoteMode;

        ModeNameLabel.Visible = !voting;
        ModeNameLabel.Text = args.ModeName;
        DescriptionLabel.Visible = !voting;
        DescriptionLabel.Text = args.ModeDescription;

        for (var i = 0; i < SETTING_ROWS; i++)
        {
            var shown = !voting && (i < args.Options.Count);
            OptionLabels[i].Visible = shown;
            OptionValueRows[i].Visible = shown;

            if (!shown)
            {
                PrevOptionButtons[i].Visible = false;
                NextOptionButtons[i].Visible = false;
                OptionValueRows[i].Selected = false;

                continue;
            }

            var option = args.Options[i];
            var isCaptain = option.Option is ArenaHostOption.GreenCaptain or ArenaHostOption.GoldCaptain;
            OptionIds[i] = option.Option;
            OptionLabels[i].Text = option.Label;
            OptionValueRows[i].Show(option.Value, LegendColors.White);
            OptionValueRows[i].Selected = isCaptain && (State.PickingCaptain == option.Option);
            PrevOptionButtons[i].Visible = !isCaptain;
            NextOptionButtons[i].Visible = !isCaptain;
        }

        VoteModeButton.Visible = !voting;
        AdminPlayButton.Visible = !voting && args.IsAdmin;
        AdminPlayButton.Caption = args.AdminPlays ? "Play in matches: on" : "Play in matches: off";

        VoteHintLabel.Visible = voting;
        var picked = State.VoteSelection.ToList();

        for (var i = 0; i < PickedLabels.Length; i++)
        {
            PickedLabels[i].Visible = voting && (i < picked.Count);
            PickedLabels[i].Text = i < picked.Count ? args.Modes.FirstOrDefault(m => m.Id == picked[i])?.Name ?? string.Empty : string.Empty;
        }

        SecondsButton.Visible = voting;
        SecondsButton.Caption = $"Seconds: {State.VoteSeconds}";
        OpenVoteButton.Visible = voting;
        OpenVoteButton.Enabled = picked.Count >= ArenaHostProtocol.MIN_VOTE_MODES;
        BackButton.Visible = voting;
    }

    private void TurnRosterPage(int delta)
    {
        RosterPage += delta;
        RefreshAll();
    }
}
