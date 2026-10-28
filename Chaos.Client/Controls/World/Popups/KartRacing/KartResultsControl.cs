#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.ViewModel;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.KartRacing;

public sealed class KartResultsControl : GuildCloakDialogBase
{
    private const int LEFT = 16;
    private const int TITLE_TOP = 10;
    private const int SUBTITLE_TOP = 28;
    private const int HEADER_TOP = 46;
    private const int ROWS_TOP = 64;
    private static readonly int ROW_HEIGHT = TextRenderer.CHAR_HEIGHT + 4;
    private const int PLACE_WIDTH = 40;
    private const int NAME_WIDTH = 160;
    private const int TIME_WIDTH = 80;
    private const int BEST_WIDTH = 80;
    private const int ROW_WIDTH = PLACE_WIDTH + NAME_WIDTH + TIME_WIDTH + BEST_WIDTH;
    private const int SMALL_BUTTON = 28;
    private const int PAGE_LABEL_WIDTH = 110;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;
    private const int BORDER_GAP = 4;
    private const int BUTTON_GAP = 4;

    private readonly UILabel[] BestLabels = new UILabel[KartResultsView.PAGE_SIZE];
    private readonly UILabel[] NameLabels = new UILabel[KartResultsView.PAGE_SIZE];
    private readonly CustomButton NextPageButton;
    private readonly UILabel PageLabel;
    private readonly UILabel[] PlaceLabels = new UILabel[KartResultsView.PAGE_SIZE];
    private readonly CustomButton PrevPageButton;
    private readonly UILabel SubtitleLabel;
    private readonly UILabel[] TimeLabels = new UILabel[KartResultsView.PAGE_SIZE];
    private IReadOnlyList<KartResultLine> Lines = [];
    private int Page;

    public KartResultsControl()
        : base("_nsett", false)
    {
        Name = "KartResults";
        Visible = false;
        UsesControlStack = true;

        var pageTop = ROWS_TOP + (KartResultsView.PAGE_SIZE * ROW_HEIGHT) + 4;

        Width = LEFT + ROW_WIDTH + LEFT;
        Height = pageTop + CustomButton.HEIGHT + BORDER_GAP + BORDER_BOTTOM_HEIGHT;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(Hide, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);

        Caption("Race Results", 0, TITLE_TOP, Width, HorizontalAlignment.Center, LegendColors.Gold);
        SubtitleLabel = Caption(string.Empty, LEFT, SUBTITLE_TOP, ROW_WIDTH, HorizontalAlignment.Center, LegendColors.Gray);

        Caption("Place", LEFT, HEADER_TOP, PLACE_WIDTH, color: LegendColors.Gray);
        Caption("Racer", LEFT + PLACE_WIDTH, HEADER_TOP, NAME_WIDTH, color: LegendColors.Gray);
        Caption("Time", LEFT + PLACE_WIDTH + NAME_WIDTH, HEADER_TOP, TIME_WIDTH, color: LegendColors.Gray);
        Caption("Best lap", LEFT + PLACE_WIDTH + NAME_WIDTH + TIME_WIDTH, HEADER_TOP, BEST_WIDTH, color: LegendColors.Gray);

        for (var i = 0; i < KartResultsView.PAGE_SIZE; i++)
        {
            var y = ROWS_TOP + (i * ROW_HEIGHT);
            PlaceLabels[i] = Caption(string.Empty, LEFT, y, PLACE_WIDTH);
            NameLabels[i] = Caption(string.Empty, LEFT + PLACE_WIDTH, y, NAME_WIDTH);
            TimeLabels[i] = Caption(string.Empty, LEFT + PLACE_WIDTH + NAME_WIDTH, y, TIME_WIDTH);
            BestLabels[i] = Caption(string.Empty, LEFT + PLACE_WIDTH + NAME_WIDTH + TIME_WIDTH, y, BEST_WIDTH);
        }

        PrevPageButton = AddButton("<", SMALL_BUTTON, LEFT, pageTop, () => TurnPage(-1));
        NextPageButton = AddButton(">", SMALL_BUTTON, LEFT + SMALL_BUTTON + BUTTON_GAP, pageTop, () => TurnPage(1));
        PageLabel = Caption(string.Empty, LEFT + (2 * SMALL_BUTTON) + 10, pageTop + 5, PAGE_LABEL_WIDTH, color: LegendColors.Gray);
    }

    public static Color MedalColor(int medal)
        => medal switch
        {
            1 => LegendColors.Gold,
            2 => new Color(192, 192, 192),
            3 => new Color(205, 127, 50),
            _ => Color.White
        };

    public static string LapsText(int laps) => laps == 1 ? "1 lap" : $"{laps} laps";

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

    public void Show(IReadOnlyList<KartResultLine> lines, int laps)
    {
        Lines = lines;
        Page = 0;
        SubtitleLabel.Text = LapsText(laps);
        Refresh();

        if (!Visible)
            Show();
    }

    private void Refresh()
    {
        var pages = KartResultsView.Pages(Lines.Count);

        for (var i = 0; i < KartResultsView.PAGE_SIZE; i++)
        {
            var lineIndex = (Page * KartResultsView.PAGE_SIZE) + i;
            var line = lineIndex < Lines.Count ? Lines[lineIndex] : null;
            var color = MedalColor(line?.Medal ?? 0);

            PlaceLabels[i].Text = line?.Place ?? string.Empty;
            PlaceLabels[i].ForegroundColor = color;
            NameLabels[i].Text = line?.Name ?? string.Empty;
            TimeLabels[i].Text = line?.Time ?? string.Empty;
            BestLabels[i].Text = line?.BestLap ?? string.Empty;
        }

        PageLabel.Text = $"Page {Page + 1}/{pages}";
        PrevPageButton.Enabled = Page > 0;
        NextPageButton.Enabled = Page < (pages - 1);
    }

    private void TurnPage(int delta)
    {
        Page = Math.Clamp(Page + delta, 0, KartResultsView.Pages(Lines.Count) - 1);
        Refresh();
    }
}
