#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.ViewModel;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.Controls.World.Popups.TowerLeaderboard;

/// <summary>
///     The Endless Tower leaderboard window: one season's top lineups, three per page, each with its rank, floor and time
///     and every member's walking figure, name and class. Opened by the server's TowerLeaderboard packet from the base's
///     leaderboard board; the season buttons ask the server for another season, and its answer replaces the data.
/// </summary>
/// <remarks>
///     Layout, top to bottom: title, subtitle, the season buttons (older, this season, newer), the three rows, then the page
///     row and the frame's bottom border with Close.
/// </remarks>
public sealed class TowerLeaderboardControl : GuildCloakDialogBase
{
    private const int LEFT = 16;
    private const int TITLE_TOP = 10;
    private const int SUBTITLE_TOP = 28;
    private const int TABS_TOP = 46;
    private const int ROWS_TOP = 76;
    private const int ROW_WIDTH = 26 + 74 + (6 * 76);
    private const int SMALL_BUTTON = 28;
    private const int TAB_WIDTH = 110;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;
    private const int BORDER_GAP = 4;
    private const int BUTTON_GAP = 4;

    private readonly UILabel EmptyLabel;
    private readonly CustomButton NewerButton;
    private readonly CustomButton NextPageButton;
    private readonly CustomButton OlderButton;
    private readonly UILabel PageLabel;
    private readonly CustomButton PrevPageButton;
    private readonly TowerLeaderboardRow[] Rows = new TowerLeaderboardRow[TowerLeaderboardState.PAGE_SIZE];
    private readonly TowerLeaderboardState State = new();
    private readonly UILabel SubtitleLabel;
    private readonly UILabel TitleLabel;

    public TowerLeaderboardControl(AislingRenderer renderer)
        : base("_nsett", false)
    {
        Name = "TowerLeaderboard";
        Visible = false;
        UsesControlStack = true;

        var rowsHeight = TowerLeaderboardState.PAGE_SIZE * TowerLeaderboardRow.HEIGHT;
        var pageTop = ROWS_TOP + rowsHeight + 4;

        Width = LEFT + ROW_WIDTH + LEFT;
        Height = pageTop + CustomButton.HEIGHT + BORDER_GAP + BORDER_BOTTOM_HEIGHT;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(Hide, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);

        TitleLabel = Caption(string.Empty, 0, TITLE_TOP, Width, HorizontalAlignment.Center, LegendColors.Gold);
        SubtitleLabel = Caption(string.Empty, LEFT, SUBTITLE_TOP, ROW_WIDTH, HorizontalAlignment.Center, LegendColors.Gray);

        var tabsLeft = (Width - (TAB_WIDTH + (2 * SMALL_BUTTON) + (2 * BUTTON_GAP))) / 2;
        OlderButton = AddButton("<", SMALL_BUTTON, tabsLeft, TABS_TOP, () => RequestSeason(State.Args.OlderSeason));
        AddButton("This season", TAB_WIDTH, tabsLeft + SMALL_BUTTON + BUTTON_GAP, TABS_TOP, () => RequestSeason(0));

        NewerButton = AddButton(
            ">",
            SMALL_BUTTON,
            tabsLeft + SMALL_BUTTON + TAB_WIDTH + (2 * BUTTON_GAP),
            TABS_TOP,
            () => RequestSeason(State.Args.NewerSeason));

        for (var i = 0; i < TowerLeaderboardState.PAGE_SIZE; i++)
        {
            var row = new TowerLeaderboardRow(renderer, i % 2 == 0)
            {
                X = LEFT,
                Y = ROWS_TOP + (i * TowerLeaderboardRow.HEIGHT),
                Visible = false
            };

            AddChild(row);
            Rows[i] = row;
        }

        EmptyLabel = Caption(
            string.Empty,
            LEFT,
            ROWS_TOP + ((rowsHeight - TextRenderer.CHAR_HEIGHT) / 2),
            ROW_WIDTH,
            HorizontalAlignment.Center,
            LegendColors.Gray);

        PrevPageButton = AddButton("<", SMALL_BUTTON, LEFT, pageTop, () => TurnPage(-1));
        NextPageButton = AddButton(">", SMALL_BUTTON, LEFT + SMALL_BUTTON + BUTTON_GAP, pageTop, () => TurnPage(1));

        PageLabel = Caption(
            string.Empty,
            LEFT + (2 * SMALL_BUTTON) + 10,
            pageTop + 5,
            TAB_WIDTH,
            color: LegendColors.Gray);
    }

    /// <summary>Raised by the season buttons with the season to ask the server for (0 is the current one).</summary>
    public event Action<TowerLeaderboardRequestArgs>? SeasonRequested;

    public override void Hide()
    {
        if (!Visible)
            return;

        foreach (var row in Rows)
            row.ReleaseFrames();

        base.Hide();
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

    /// <summary>Shows a season's board on its first page, opening the window if it is closed.</summary>
    public void Show(TowerLeaderboardArgs args)
    {
        State.Open(args);
        RefreshAll();

        if (!Visible)
            Show();
    }

    private void RefreshAll()
    {
        var args = State.Args;
        TitleLabel.Text = State.Title;
        SubtitleLabel.Text = State.Subtitle;

        OlderButton.Enabled = args.OlderSeason != 0;
        NewerButton.Enabled = args.NewerSeason != 0;

        var items = State.PageItems;

        for (var i = 0; i < TowerLeaderboardState.PAGE_SIZE; i++)
            Rows[i].SetEntry(i < items.Count ? items[i] : null);

        EmptyLabel.Text = State.Empty;
        EmptyLabel.Visible = args.Entries.Count == 0;

        PageLabel.Text = $"Page {State.Page + 1}/{State.Pages}";
        PrevPageButton.Enabled = State.Page > 0;
        NextPageButton.Enabled = State.Page < (State.Pages - 1);
    }

    private void RequestSeason(ushort season)
        => SeasonRequested?.Invoke(
            new TowerLeaderboardRequestArgs
            {
                Season = season
            });

    private void TurnPage(int delta)
    {
        State.TurnPage(delta);
        RefreshAll();
    }
}
