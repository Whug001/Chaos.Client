#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Systems.College;
using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>
///     Picks the picture to hang in a wall frame: the Gallery and Hand-ins tabs, a 3 x 2 grid of pictures, a pager, the
///     server's status line and Hang here. The server sends each page; nothing is cached across pages.
/// </summary>
public sealed class WallPickerControl : GuildCloakDialogBase
{
    private const int WIDTH = 392;
    private const int HEIGHT = 330;
    private const int LEFT = 16;
    private const int COLUMNS = 3;
    private const int PAGE_SIZE = 6;
    private const int CELL_GAP = 4;
    private const int TAB_WIDTH = 90;
    private const int TAB_GAP = 4;
    private const int TABS_TOP = 8;
    private const int GRID_TOP = TABS_TOP + CustomButton.HEIGHT + 4;
    private const int STATUS_TOP = GRID_TOP + (2 * GalleryArtCell.HEIGHT) + CELL_GAP + 2;
    private const int BOTTOM = HEIGHT - BORDER_BOTTOM_HEIGHT - CustomButton.HEIGHT - 4;
    private const int ARROW_WIDTH = 28;
    private const int PAGE_X = LEFT + 66;
    private const int PAGE_WIDTH = 100;
    private const int HANG_X = LEFT + 180;
    private const int HANG_WIDTH = 90;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;

    private static readonly CollegePieceSource[] Sources = [CollegePieceSource.Gallery, CollegePieceSource.HandIn];

    private readonly GalleryArtCell[] Cells = new GalleryArtCell[PAGE_SIZE];
    private readonly CustomButton HangButton;
    private readonly CustomButton NextButton;
    private readonly UILabel PageLabel;
    private readonly CustomButton PrevButton;
    private readonly UILabel StatusLabel;
    private readonly CustomButton[] Tabs;

    private int Page;
    private int PageCount = 1;
    private int SelectedId;
    private int Slot;
    private CollegePieceSource Source = CollegePieceSource.Gallery;

    public WallPickerControl()
        : base("_nsett", false)
    {
        Name = "WallPicker";
        Visible = false;
        UsesControlStack = true;
        Width = WIDTH;
        Height = HEIGHT;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(Hide, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);

        Tabs =
        [
            AddButton("Gallery", TAB_WIDTH, LEFT, TABS_TOP, () => RequestPage(CollegePieceSource.Gallery, 0)),
            AddButton("Hand-ins", TAB_WIDTH, LEFT + TAB_WIDTH + TAB_GAP, TABS_TOP, () => RequestPage(CollegePieceSource.HandIn, 0))
        ];

        for (var i = 0; i < PAGE_SIZE; i++)
        {
            var cell = new GalleryArtCell
            {
                X = LEFT + ((i % COLUMNS) * GalleryArtCell.WIDTH),
                Y = GRID_TOP + ((i / COLUMNS) * (GalleryArtCell.HEIGHT + CELL_GAP))
            };

            cell.Clicked += () => Select(cell.Id);

            cell.DoubleClicked += () =>
            {
                Select(cell.Id);
                HangSelected();
            };

            AddChild(cell);
            Cells[i] = cell;
        }

        StatusLabel = Caption(string.Empty, LEFT, STATUS_TOP, WIDTH - (2 * LEFT), HorizontalAlignment.Center, LegendColors.Gray);
        PrevButton = AddButton("<", ARROW_WIDTH, LEFT, BOTTOM, () => TurnPage(-1));
        NextButton = AddButton(">", ARROW_WIDTH, LEFT + ARROW_WIDTH + 4, BOTTOM, () => TurnPage(1));

        PageLabel = Caption(
            string.Empty,
            PAGE_X,
            BOTTOM + ((CustomButton.HEIGHT - TextRenderer.CHAR_HEIGHT) / 2),
            PAGE_WIDTH,
            color: LegendColors.Gray);

        HangButton = AddButton("Hang here", HANG_WIDTH, HANG_X, BOTTOM, HangSelected);
        HangButton.Enabled = false;
    }

    public event Action<CollegeActionArgs>? ActionRequested;

    public void Open(int slot)
    {
        Slot = slot;
        Source = CollegePieceSource.Gallery;
        Page = 0;
        PageCount = 1;
        ClearCells();
        StatusLabel.Text = "Loading...";
        ShowTabsAndPager();

        if (!Visible)
            Show();

        RequestPage(Source, 0);
    }

    public void Show(CollegeDisplayArgs args)
    {
        Source = args.Source;
        Page = args.Page;
        PageCount = Math.Max(1, (int)args.PageCount);
        ClearCells();

        var frame = CollegeTiers.Badge(CollegeTierCode.None);

        for (var i = 0; (i < PAGE_SIZE) && (i < args.WallPickRows.Count); i++)
        {
            var row = args.WallPickRows[i];
            var cell = Cells[i];
            cell.Set(row.Id, row.Title, row.Artist, frame);

            if (row.Pixels.Length == CollegeProtocol.DRAWING_PIXELS)
                cell.Picture = DrawingTextures.Build(PixelDrawing.From(row.Palette, row.Pixels), 1);

            cell.Visible = true;
        }

        StatusLabel.Text = args.Message;
        ShowTabsAndPager();

        if (!Visible)
            Show();
    }

    public override void Hide()
    {
        ClearCells();
        base.Hide();
    }

    public override void Dispose()
    {
        ClearCells();
        base.Dispose();
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

    private void ClearCells()
    {
        SelectedId = 0;
        HangButton.Enabled = false;

        foreach (var cell in Cells)
        {
            cell.Picture?.Dispose();
            cell.Picture = null;
            cell.Visible = false;
            cell.Selected = false;
        }
    }

    private void HangSelected()
    {
        if (SelectedId == 0)
            return;

        var id = SelectedId;
        var source = Source;
        Hide();

        ActionRequested?.Invoke(
            new CollegeActionArgs
            {
                Type = CollegeActionType.WallHang,
                Number = (byte)Slot,
                Source = source,
                Id = id
            });
    }

    private void RequestPage(CollegePieceSource source, int page)
        => ActionRequested?.Invoke(
            new CollegeActionArgs
            {
                Type = CollegeActionType.WallPicker,
                Source = source,
                Page = (byte)page
            });

    private void Select(int id)
    {
        SelectedId = id;
        HangButton.Enabled = id != 0;

        foreach (var cell in Cells)
            cell.Selected = cell.Visible && (cell.Id == id);
    }

    private void ShowTabsAndPager()
    {
        for (var i = 0; i < Tabs.Length; i++)
            Tabs[i].Selected = Sources[i] == Source;

        PageLabel.Text = $"Page {Page + 1} of {PageCount}";
        PrevButton.Enabled = Page > 0;
        NextButton.Enabled = Page < (PageCount - 1);
    }

    private void TurnPage(int by)
    {
        var page = Page + by;

        if ((page >= 0) && (page < PageCount))
            RequestPage(Source, page);
    }
}
