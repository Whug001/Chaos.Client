#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.Dialog;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.Tamer;

/// <summary>
///     The battle window: a framed 620 by 440 panel titled with the fight's title line, playing the finished fight
///     back on the 7 by 6 board. <see cref="Update" /> feeds real elapsed time to the playback, capped at 250 ms so
///     one hitch cannot skip the fight. Escape closes; a new fight replaces the one on screen. Borrows the
///     <c>_nsett</c> prefab for its OK button only, as the stable window does.
/// </summary>
public sealed class TamerBattleControl : FramedDialogPanelBase
{
    public const int PANEL_HEIGHT = 440;
    public const int PANEL_WIDTH = 620;
    public const int PANEL_X = 10;
    public const int PANEL_Y = 6;

    private const int BANNER_HEIGHT = 136;
    private const int BANNER_LEFT = 190;
    private const int BANNER_TOP = 98;
    private const int BANNER_WIDTH = 240;
    private const int BOARD_HEIGHT = TamerBoardView.BOARD_HEIGHT;
    private const int BOARD_LEFT = 170;
    private const int BOARD_TOP = 46;
    private const int BOARD_WIDTH = TamerBoardView.BOARD_WIDTH;
    private const int BUTTONS_TOP = 348;
    private const int CLOCK_LEFT = 390;
    private const int CLOCK_WIDTH = 60;
    private const int LEFT = 20;
    private const int LOG_HEIGHT = TextRenderer.CHAR_HEIGHT * 4;
    private const int LOG_LEFT = 170;
    private const int LOG_TOP = 292;
    private const int LOG_WIDTH = 280;
    private const int PAUSE_LEFT = 20;
    private const int PAUSE_WIDTH = 90;
    private const int REPLAY_LEFT = 284;
    private const int REPLAY_WIDTH = 90;
    private const int RISING_LEFT = 460;
    private const int RISING_WIDTH = 140;
    private const int ROSTER_HEIGHT = 296;
    private const int ROSTER_LEFT_X = 20;
    private const int ROSTER_RIGHT_X = 460;
    private const int ROSTER_TOP = 46;
    private const int ROSTER_WIDTH = 140;
    private const int ROW_COUNT = 5;
    private const int ROW_HEIGHT = TamerBattleRosterRow.HEIGHT;
    private const int ROWS_TOP_OFFSET = 16;
    private const int SKIP_LEFT = 186;
    private const int SKIP_WIDTH = 90;
    private const int SPEED_LEFT = 118;
    private const int SPEED_WIDTH = 60;
    private const int TITLE_LEFT = 20;
    private const int TITLE_TOP = 8;
    private const int TITLE_WIDTH = 580;
    private const int TOPLINE_HEIGHT = TextRenderer.CHAR_HEIGHT;
    private const int TOPLINE_TOP = 26;
    private const int VERSUS_LEFT = 20;
    private const int VERSUS_WIDTH = 360;

    private static readonly Color PanelFill = new(28, 20, 12);
    private static readonly Color BannerFill = new(10, 8, 5, 230);

    private readonly UILabel TitleLabel;
    private readonly UILabel VersusLabel;
    private readonly UILabel ClockLabel;
    private readonly UILabel RisingLabel;
    private readonly UIPanel LeftPanel;
    private readonly UIPanel RightPanel;
    private readonly UILabel LeftHeaderLabel;
    private readonly UILabel RightHeaderLabel;
    private readonly TamerBattleRosterRow[] LeftRows = new TamerBattleRosterRow[ROW_COUNT];
    private readonly TamerBattleRosterRow[] RightRows = new TamerBattleRosterRow[ROW_COUNT];
    private readonly TamerBoardView BoardView;
    private readonly UILabel LogLabel;
    private readonly CustomButton PauseButton;
    private readonly CustomButton SpeedButton;
    private readonly CustomButton SkipButton;
    private readonly CustomButton ReplayButton;
    private readonly UIPanel BannerPanel;
    private readonly UILabel ResultTitleLabel;
    private readonly UILabel ResultReasonLabel;
    private readonly UILabel ResultLinesLabel;

    private TamerBattlePlayback? Playback;

    /// <summary>The window was hidden, by the player. Closing tells the server nothing.</summary>
    public event Action? Closed;

    public TamerBattleControl(CreatureRenderer creatures)
        : base("_nsett", false)
    {
        ArgumentNullException.ThrowIfNull(creatures);

        Name = "Battle";
        Visible = false;
        UsesControlStack = true;
        Width = PANEL_WIDTH;
        Height = PANEL_HEIGHT;
        X = PANEL_X;
        Y = PANEL_Y;

        OkButton = CreateCloseButton(Hide, 20, 3);

        TitleLabel = Label(TITLE_LEFT, TITLE_TOP, TITLE_WIDTH, string.Empty, LegendColors.Gold, HorizontalAlignment.Center);
        AddChild(TitleLabel);

        VersusLabel = Label(VERSUS_LEFT, TOPLINE_TOP, VERSUS_WIDTH, string.Empty, LegendColors.Gold, HorizontalAlignment.Left);
        ClockLabel = Label(CLOCK_LEFT, TOPLINE_TOP, CLOCK_WIDTH, string.Empty, LegendColors.White, HorizontalAlignment.Right);
        RisingLabel = Label(RISING_LEFT, TOPLINE_TOP, RISING_WIDTH, string.Empty, LegendColors.Red, HorizontalAlignment.Right);
        AddChild(VersusLabel);
        AddChild(ClockLabel);
        AddChild(RisingLabel);

        LeftPanel = RosterPanel(ROSTER_LEFT_X);
        RightPanel = RosterPanel(ROSTER_RIGHT_X);
        LeftHeaderLabel = Label(2, 0, ROSTER_WIDTH - 4, string.Empty, LegendColors.Gold, HorizontalAlignment.Center);
        RightHeaderLabel = Label(2, 0, ROSTER_WIDTH - 4, string.Empty, LegendColors.Gold, HorizontalAlignment.Center);
        LeftPanel.AddChild(LeftHeaderLabel);
        RightPanel.AddChild(RightHeaderLabel);

        for (var i = 0; i < ROW_COUNT; i++)
        {
            LeftRows[i] = new TamerBattleRosterRow { X = 0, Y = ROWS_TOP_OFFSET + (i * ROW_HEIGHT) };
            RightRows[i] = new TamerBattleRosterRow { X = 0, Y = ROWS_TOP_OFFSET + (i * ROW_HEIGHT) };
            LeftPanel.AddChild(LeftRows[i]);
            RightPanel.AddChild(RightRows[i]);
        }

        AddChild(LeftPanel);
        AddChild(RightPanel);

        BoardView = new TamerBoardView(creatures) { X = BOARD_LEFT, Y = BOARD_TOP };
        AddChild(BoardView);

        LogLabel = new UILabel
        {
            X = LOG_LEFT,
            Y = LOG_TOP,
            Width = LOG_WIDTH,
            Height = LOG_HEIGHT,
            ForegroundColor = LegendColors.Silver,
            WordWrap = true,
            ColorCodesEnabled = false,
            IsHitTestVisible = false
        };
        AddChild(LogLabel);

        PauseButton = Button(PAUSE_LEFT, PAUSE_WIDTH, TamerBattleText.Pause, OnPauseClicked);
        SpeedButton = Button(SPEED_LEFT, SPEED_WIDTH, TamerBattleText.SpeedText(1), OnSpeedClicked);
        SkipButton = Button(SKIP_LEFT, SKIP_WIDTH, TamerBattleText.Skip, OnSkipClicked);
        ReplayButton = Button(REPLAY_LEFT, REPLAY_WIDTH, TamerBattleText.Replay, OnReplayClicked);

        BannerPanel = new UIPanel
        {
            X = BANNER_LEFT,
            Y = BANNER_TOP,
            Width = BANNER_WIDTH,
            Height = BANNER_HEIGHT,
            BackgroundColor = BannerFill,
            BorderColor = LegendColors.Gold,
            IsHitTestVisible = false,
            Visible = false
        };

        ResultTitleLabel = Label(8, 8, BANNER_WIDTH - 16, string.Empty, LegendColors.Gold, HorizontalAlignment.Center);
        ResultReasonLabel = Label(8, 24, BANNER_WIDTH - 16, string.Empty, LegendColors.White, HorizontalAlignment.Center);
        ResultLinesLabel = new UILabel
        {
            X = 8,
            Y = 40,
            Width = BANNER_WIDTH - 16,
            Height = BANNER_HEIGHT - 48,
            ForegroundColor = LegendColors.Silver,
            HorizontalAlignment = HorizontalAlignment.Center,
            WordWrap = true,
            ColorCodesEnabled = false,
            IsHitTestVisible = false
        };
        BannerPanel.AddChild(ResultTitleLabel);
        BannerPanel.AddChild(ResultReasonLabel);
        BannerPanel.AddChild(ResultLinesLabel);
        AddChild(BannerPanel);
    }

    /// <summary>Shows the fight with a fresh playback.</summary>
    public void Open(TamerBattlePlayback playback)
    {
        Playback = playback ?? throw new ArgumentNullException(nameof(playback));
        BoardView.Playback = playback;
        TitleLabel.Text = TamerStableText.FitLines(playback.Fight.Title, TITLE_WIDTH - 2, 1);
        Show();
        Refresh();
    }

    /// <summary>Repaints every label, roster row and button from the playback. Called every update while visible.</summary>
    public void Refresh()
    {
        if (!Visible || (Playback is null))
            return;

        var playback = Playback;
        var fight = playback.Fight;

        VersusLabel.Text = TamerStableText.FitLines(
            TamerBattleText.VersusText(fight.AttackerName, fight.DefenderName),
            VERSUS_WIDTH - 2,
            1);
        ClockLabel.Text = $"{TamerBattleText.ClockText(playback.ClockTicks)}s";

        var rising = TamerBattleText.RisingText(playback.RisingPercent);
        RisingLabel.Text = rising;
        RisingLabel.Visible = rising.Length > 0;

        var leftSide = fight.ViewerSide == TamerBattleViewerSide.Defender
            ? TamerBattleSide.Defender
            : TamerBattleSide.Attacker;
        var rightSide = leftSide == TamerBattleSide.Attacker ? TamerBattleSide.Defender : TamerBattleSide.Attacker;

        LeftHeaderLabel.Text = TamerStableText.FitLines(
            SideName(fight, leftSide),
            ROSTER_WIDTH - 6,
            1);
        RightHeaderLabel.Text = TamerStableText.FitLines(
            SideName(fight, rightSide),
            ROSTER_WIDTH - 6,
            1);

        var leftUnits = playback.Units.Where(unit => unit.Side == leftSide).ToList();
        var rightUnits = playback.Units.Where(unit => unit.Side == rightSide).ToList();

        for (var i = 0; i < ROW_COUNT; i++)
        {
            if (i < leftUnits.Count)
            {
                LeftRows[i].Visible = true;
                LeftRows[i].Bind(leftUnits[i]);
            } else
                LeftRows[i].Visible = false;

            if (i < rightUnits.Count)
            {
                RightRows[i].Visible = true;
                RightRows[i].Bind(rightUnits[i]);
            } else
                RightRows[i].Visible = false;
        }

        var logLines = playback.LogLines.Select(line => TamerStableText.FitLines(line, LOG_WIDTH - 2, 1)).ToList();
        LogLabel.Text = string.Join("\n", logLines);

        PauseButton.Caption = playback.Paused ? TamerBattleText.Play : TamerBattleText.Pause;
        SpeedButton.Caption = TamerBattleText.SpeedText(playback.Speed);
        PauseButton.Enabled = !playback.Finished;
        SpeedButton.Enabled = !playback.Finished;
        SkipButton.Enabled = !playback.Finished;
        ReplayButton.Enabled = playback.Finished;

        BannerPanel.Visible = playback.Finished;

        if (playback.Finished)
        {
            ResultTitleLabel.Text = TamerStableText.FitLines(playback.ResultTitle, BANNER_WIDTH - 18, 1);
            ResultReasonLabel.Text = TamerStableText.FitLines(playback.ResultReason, BANNER_WIDTH - 18, 1);
            ResultLinesLabel.Text = TamerStableText.FitLines(
                string.Join("\n", playback.ResultLines),
                BANNER_WIDTH - 18,
                7);
        }
    }

    public override void Update(GameTime gameTime)
    {
        if (Visible && Playback is not null)
        {
            var cappedMs = Math.Min(250.0, gameTime.ElapsedGameTime.TotalMilliseconds);
            Playback.Advance(TimeSpan.FromMilliseconds(cappedMs));
            Refresh();
        }

        base.Update(gameTime);
    }

    public override void Hide()
    {
        if (!Visible)
            return;

        base.Hide();
        Closed?.Invoke();
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

    private static string SideName(TamerBattleFight fight, TamerBattleSide side)
    {
        var name = side == TamerBattleSide.Attacker ? fight.AttackerName : fight.DefenderName;
        var fallback = side == TamerBattleSide.Attacker ? "Attacker" : "Defender";

        return string.IsNullOrWhiteSpace(name) ? fallback : name.Trim();
    }

    private static UIPanel RosterPanel(int x)
        => new()
        {
            X = x,
            Y = ROSTER_TOP,
            Width = ROSTER_WIDTH,
            Height = ROSTER_HEIGHT,
            BackgroundColor = PanelFill,
            BorderColor = LegendColors.DimGray,
            IsHitTestVisible = false
        };

    private CustomButton Button(int x, int width, string caption, ClickedHandler onClick)
    {
        var button = new CustomButton(caption, width) { X = x, Y = BUTTONS_TOP };
        button.Clicked += onClick;
        AddChild(button);

        return button;
    }

    private void OnPauseClicked()
    {
        if (Playback is null)
            return;

        Playback.Paused = !Playback.Paused;
        Refresh();
    }

    private void OnSpeedClicked()
    {
        if (Playback is null)
            return;

        Playback.CycleSpeed();
        Refresh();
    }

    private void OnSkipClicked()
    {
        if (Playback is null)
            return;

        Playback.Skip();
        Refresh();
    }

    private void OnReplayClicked()
    {
        if (Playback is null)
            return;

        Playback.Replay();
        Refresh();
    }

    private static UILabel Label(int x, int y, int width, string text, Color color, HorizontalAlignment alignment)
        => new()
        {
            X = x,
            Y = y,
            Width = width,
            Height = TextRenderer.CHAR_HEIGHT,
            Text = text,
            ForegroundColor = color,
            HorizontalAlignment = alignment,
            ColorCodesEnabled = false,
            IsHitTestVisible = false
        };
}
