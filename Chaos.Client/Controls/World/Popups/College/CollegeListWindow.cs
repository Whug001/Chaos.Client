#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.Definitions;
using Chaos.Client.Extensions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Chaos.Networking.Entities.Client;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>
///     The shared body of the College lists (500 x 360): column headers, a page of <see cref="CollegeListRow" />s, the
///     pager ("&lt;", "&gt;", "Page N of M"), the selection and the buttons that act on it (Read first). Each list gives its
///     columns, its current items, how an item fills a row and what opening one sends.
/// </summary>
public abstract class CollegeListWindow<TItem> : GuildCloakDialogBase
{
    protected const int WIDTH = 500;
    protected const int HEIGHT = 360;
    protected const int LEFT = 16;
    protected const int INNER_WIDTH = WIDTH - (2 * LEFT);
    protected const int TITLE_TOP = 10;
    protected const int TABS_TOP = 28;
    protected const int TABBED_HEADER_TOP = TABS_TOP + CustomButton.HEIGHT + 6;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;
    private const int BOTTOM = HEIGHT - BORDER_BOTTOM_HEIGHT - CustomButton.HEIGHT - 4;
    private const int ARROW_WIDTH = 28;
    private const int PAGE_X = LEFT + 66;
    private const int PAGE_WIDTH = 100;
    private const int ACTIONS_X = LEFT + 180;
    private const int BUTTON_GAP = 6;

    private readonly List<UILabel> Headers = [];
    private readonly CustomButton NextButton;
    private readonly UILabel PageLabel;
    private readonly CustomButton PrevButton;
    private readonly CollegeListRow[] Rows;
    private readonly List<CustomButton> SelectionButtons = [];

    private int NextActionX = ACTIONS_X;

    protected CollegeListWindow(
        string name,
        int headerTop,
        int rowCount,
        IReadOnlyList<(string Header, int X)> columns,
        string emptyText)
        : base("_nsett", false)
    {
        Name = name;
        Visible = false;
        UsesControlStack = true;
        Width = WIDTH;
        Height = HEIGHT;
        this.CenterOnScreen();
        OkButton = CreateCloseButton(Hide, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);

        var columnX = columns.Select(c => c.X)
                             .ToArray();

        for (var i = 0; i < columns.Count; i++)
        {
            var right = i + 1 < columns.Count ? columnX[i + 1] : INNER_WIDTH;
            Headers.Add(Caption(columns[i].Header, LEFT + columnX[i] + 4, headerTop, right - columnX[i] - 8, color: LegendColors.Gold));
        }

        var rowsTop = headerTop + TextRenderer.CHAR_HEIGHT + 4;
        RowsTop = rowsTop;
        Rows = new CollegeListRow[rowCount];

        for (var i = 0; i < rowCount; i++)
        {
            var row = new CollegeListRow(INNER_WIDTH, columnX)
            {
                X = LEFT,
                Y = rowsTop + (i * CollegeListRow.HEIGHT),
                Visible = false
            };

            row.Clicked += () => Select(row.Id);
            row.DoubleClicked += () => OpenItem(row.Id);
            AddChild(row);
            Rows[i] = row;
        }

        Empty = Caption(emptyText, LEFT, rowsTop + 40, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gray);

        PrevButton = AddButton("<", ARROW_WIDTH, LEFT, BOTTOM, () => Turn(-1));
        NextButton = AddButton(">", ARROW_WIDTH, LEFT + ARROW_WIDTH + 4, BOTTOM, () => Turn(1));

        PageLabel = Caption(
            string.Empty,
            PAGE_X,
            BOTTOM + ((CustomButton.HEIGHT - TextRenderer.CHAR_HEIGHT) / 2),
            PAGE_WIDTH,
            color: LegendColors.Gray);

        AddSelectionButton("Read", 80, OpenItem);
    }

    /// <summary>Raised with what a list sends: OpenPiece, and a list's own actions (GalleryPage, ShowToClass).</summary>
    public event Action<CollegeActionArgs>? ActionRequested;

    protected UILabel Empty { get; }

    /// <summary>The y of the first row, for controls laid over the rows.</summary>
    protected int RowsTop { get; }

    /// <summary>The page shown, from 0. Clamped to the items each time the rows are shown.</summary>
    protected int Page { get; set; }

    protected int SelectedId { get; private set; }

    /// <summary>The items the list shows now, in order (all pages).</summary>
    protected abstract IReadOnlyList<TItem> Items { get; }

    /// <summary>Items per page; the rows hold at most their own count.</summary>
    protected virtual int PageSize => Rows.Length;

    /// <summary>False while a subclass shows the page another way (the gallery's picture grid): rows and headers hide.</summary>
    protected virtual bool ShowsRows => true;

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

    /// <summary>Adds a button to the right of the last one on the bottom row; it acts on the selected item and is disabled without one.</summary>
    protected CustomButton AddSelectionButton(string caption, int width, Action<int> onClick)
    {
        var button = AddButton(
            caption,
            width,
            NextActionX,
            BOTTOM,
            () =>
            {
                if (SelectedId != 0)
                    onClick(SelectedId);
            });

        NextActionX += width + BUTTON_GAP;
        SelectionButtons.Add(button);

        return button;
    }

    /// <summary>Called with the page's items each time the rows are shown.</summary>
    protected virtual void PageShown(IReadOnlyList<TItem> shown) { }

    /// <summary>Called after the selection changes.</summary>
    protected virtual void SelectionChanged(int id) { }

    protected void SelectId(int id) => Select(id);

    protected void OpenId(int id) => OpenItem(id);

    protected abstract (string Text, Color Color)[] ColumnsOf(TItem item);

    protected abstract int IdOf(TItem item);

    protected abstract CollegeActionArgs OpenAction(int id);

    protected void Raise(CollegeActionArgs args) => ActionRequested?.Invoke(args);

    /// <summary>Fills the rows from <see cref="Items" /> for the current page, and keeps the selection if it is still shown.</summary>
    protected void ShowRows()
    {
        var items = Items;
        var pageSize = PageSize;
        var pages = Math.Max(1, (items.Count + pageSize - 1) / pageSize);
        Page = Math.Clamp(Page, 0, pages - 1);

        var shown = items.Skip(Page * pageSize)
                         .Take(pageSize)
                         .ToList();

        for (var i = 0; i < Rows.Length; i++)
        {
            Rows[i].Visible = ShowsRows && (i < shown.Count);

            if (Rows[i].Visible)
                Rows[i]
                    .Set(IdOf(shown[i]), ColumnsOf(shown[i]));
        }

        foreach (var header in Headers)
            header.Visible = ShowsRows;

        Empty.Visible = shown.Count == 0;
        PageLabel.Text = $"Page {Page + 1} of {pages}";
        PrevButton.Enabled = Page > 0;
        NextButton.Enabled = Page < (pages - 1);
        PageShown(shown);

        var ids = shown.Select(IdOf)
                       .ToList();

        Select(ids.Contains(SelectedId) ? SelectedId : ids.FirstOrDefault());
    }

    private void OpenItem(int id)
    {
        if (id != 0)
            Raise(OpenAction(id));
    }

    private void Select(int id)
    {
        SelectedId = id;

        foreach (var row in Rows)
            row.Selected = row.Visible && (row.Id == id);

        foreach (var button in SelectionButtons)
            button.Enabled = id != 0;

        SelectionChanged(id);
    }

    private void Turn(int by)
    {
        Page += by;
        ShowRows();
    }
}
