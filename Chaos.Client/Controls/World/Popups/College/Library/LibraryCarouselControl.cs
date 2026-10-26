#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Controls.World.Popups.GuildCloak;
using Chaos.Client.Extensions;
using Chaos.Client.Systems.College;
using Chaos.Client.ViewModel.College.Library;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.College.Library;

/// <summary>
///     The library browsing window: five shelf tabs, the curved row of spines, the turning front cover with its
///     title, author, approval date and Open button, plus search, sort and pages. Covers come from
///     <see cref="CollegePictureTransfers" /> (Picture), a second <see cref="CollegeDrawings" /> instance the world
///     screen owns (Drawing), or a plain spine-coloured cover. Held cover textures are released on page change and
///     on hide, and no book is ever held twice.
/// </summary>
public sealed class LibraryCarouselControl : GuildCloakDialogBase
{
    private const int WIDTH = 560;
    private const int HEIGHT = 440;
    private const int LEFT = 16;
    private const int HEADER_TOP = 10;
    private const int TABS_TOP = 28;
    private const int TAB_WIDTH = 100;
    private const int TAB_GAP = 4;
    private const int COVER_TOP = 56;
    private const int SIDE_TOP = 80;
    private const int SIDE_X = 30;
    private const int SIDE_WIDTH = 170;
    private const int SORT_TOP = 108;
    private const int SPIN_TOP = 134;
    private const int SPIN_WIDTH = 83;
    private const int INFO_X = 350;
    private const int INFO_WIDTH = 180;
    private const int TITLE_HEIGHT = 2 * TextRenderer.CHAR_HEIGHT;
    private const int AUTHOR_TOP = 108;
    private const int DATE_TOP = 124;
    private const int OPEN_TOP = 144;
    private const int OPEN_WIDTH = 110;
    private const int BAND_X = 30;
    private const int BAND_TOP = 232;
    private const int BAND_WIDTH = 500;
    private const int BAND_HEIGHT = 150;
    private const int BAND_CENTER_X = BAND_X + (BAND_WIDTH / 2);
    private const int BAND_RADIUS = 230;
    private const int SHELF_BASE = BAND_TOP + BAND_HEIGHT - 4;
    private const int PAGER_TOP = 388;
    private const int ARROW_WIDTH = 28;
    private const int PAGE_X = SIDE_X + (2 * (ARROW_WIDTH + 4));
    private const int PAGE_WIDTH = 160;
    private const int OK_RIGHT_MARGIN = 20;
    private const int OK_BOTTOM_MARGIN = 3;
    private const float TURN_SECONDS = 0.2f;

    private static readonly LibraryShelfCode[] Shelves =
    [
        LibraryShelfCode.Guides,
        LibraryShelfCode.Lore,
        LibraryShelfCode.ShortStories,
        LibraryShelfCode.PoemsAndSongs,
        LibraryShelfCode.Artwork
    ];

    private readonly UILabel AuthorLabel;
    private readonly UILabel DateLabel;
    private readonly CollegeDrawings Drawings;
    private readonly UILabel EmptyLabel;
    private readonly UILabel HeaderLabel;
    private readonly Dictionary<int, (CoverKind Kind, string Hash)> Held = new();
    private readonly CustomButton NextPageButton;
    private readonly CustomButton OpenButton;
    private readonly UILabel PageLabel;
    private readonly CustomButton PrevPageButton;
    private readonly CustomTextBox SearchBox;
    private readonly CustomButton SortButton;
    private readonly Dictionary<int, CustomButton> TabButtons = new();
    private readonly UILabel TitleLabel;
    private readonly Dictionary<int, Texture2D> Spines = [];
    private readonly Dictionary<int, (CoverShown Shown, Texture2D Texture)> Covers = [];
    private readonly CollegePictureTransfers Transfers;

    private CarouselLayout Layout = new(0, BAND_CENTER_X, BAND_RADIUS);
    private byte Page;
    private byte PageCount;
    private List<LibraryShelfRowInfo> Rows = [];
    private LibraryShelfCode Shelf;
    private LibrarySortCode Sort;
    private int TurnIndex = -1;
    private float TurnProgress = 1f;
    private ushort Total;

    public LibraryCarouselControl(CollegePictureTransfers transfers, CollegeDrawings drawings)
        : base("_nsett", false)
    {
        ArgumentNullException.ThrowIfNull(transfers);
        ArgumentNullException.ThrowIfNull(drawings);

        Transfers = transfers;
        Drawings = drawings;

        Name = "LibraryCarousel";
        Visible = false;
        UsesControlStack = true;
        Width = WIDTH;
        Height = HEIGHT;
        this.CenterOnScreen();

        OkButton = CreateCloseButton(Hide, OK_RIGHT_MARGIN, OK_BOTTOM_MARGIN);

        HeaderLabel = Caption(string.Empty, LEFT, HEADER_TOP, WIDTH - (2 * LEFT), HorizontalAlignment.Center, LegendColors.Gold);

        var tabsWidth = (Shelves.Length * TAB_WIDTH) + ((Shelves.Length - 1) * TAB_GAP);
        var tabsX = (WIDTH - tabsWidth) / 2;

        for (var i = 0; i < Shelves.Length; i++)
        {
            var shelf = Shelves[i];
            var button = AddButton(LibraryLabels.ShelfName(shelf), TAB_WIDTH, tabsX + (i * (TAB_WIDTH + TAB_GAP)), TABS_TOP, () => RequestPage(shelf, 0));
            TabButtons[(int)shelf] = button;
        }

        SearchBox = new CustomTextBox
        {
            X = SIDE_X,
            Y = SIDE_TOP,
            Width = SIDE_WIDTH,
            Height = CustomButton.HEIGHT,
            MaxLength = CollegeProtocol.LIBRARY_MAX_SEARCH_CHARS,
            IsSelectable = true,
            IsTabStop = true,
            HintText = "Search title or author"
        };

        AddChild(SearchBox);

        SortButton = AddButton("Newest", SIDE_WIDTH, SIDE_X, SORT_TOP, ToggleSort);
        AddButton("<", SPIN_WIDTH, SIDE_X, SPIN_TOP, () => Layout.Spin(-1));
        AddButton(">", SPIN_WIDTH, SIDE_X + SPIN_WIDTH + 4, SPIN_TOP, () => Layout.Spin(1));

        TitleLabel = new UILabel
        {
            X = INFO_X,
            Y = SIDE_TOP,
            Width = INFO_WIDTH,
            Height = TITLE_HEIGHT,
            WordWrap = true,
            ForegroundColor = LegendColors.White,
            IsHitTestVisible = false
        };

        AddChild(TitleLabel);

        AuthorLabel = Caption(string.Empty, INFO_X, AUTHOR_TOP, INFO_WIDTH, color: LegendColors.Gray);
        DateLabel = Caption(string.Empty, INFO_X, DATE_TOP, INFO_WIDTH, color: LegendColors.Gray);
        OpenButton = AddButton("Open", OPEN_WIDTH, INFO_X, OPEN_TOP, OpenFront);

        PrevPageButton = AddButton("<", ARROW_WIDTH, SIDE_X, PAGER_TOP, () => RequestPage(Shelf, (byte)Math.Max(0, (int)Page - 1)));
        NextPageButton = AddButton(">", ARROW_WIDTH, SIDE_X + ARROW_WIDTH + 4, PAGER_TOP, () => RequestPage(Shelf, (byte)(Page + 1)));
        PageLabel = Caption(string.Empty, PAGE_X, PAGER_TOP + ((CustomButton.HEIGHT - TextRenderer.CHAR_HEIGHT) / 2), PAGE_WIDTH, color: LegendColors.Gray);

        EmptyLabel = Caption("No books on this shelf yet.", BAND_X, BAND_TOP + 60, BAND_WIDTH, HorizontalAlignment.Center, LegendColors.Gray);
    }

    private string AppliedSearch = string.Empty;

    public event Action<CollegeActionArgs>? ActionRequested;

    /// <summary>Shows one shelf page the server sent.</summary>
    public void Open(CollegeDisplayArgs args)
    {
        if (args.Type is not CollegeDisplayType.LibraryShelf)
            return;

        ReleaseCovers();
        ClearArt();

        Shelf = args.Shelf;
        Sort = args.Sort;
        Page = args.Page;
        PageCount = args.PageCount;
        Total = args.Total;
        Rows = new List<LibraryShelfRowInfo>(args.ShelfRows);

        Layout = new CarouselLayout(Rows.Count, BAND_CENTER_X, BAND_RADIUS);
        Layout.JumpTo(0);
        TurnIndex = Layout.Front;
        TurnProgress = 0f;

        AppliedSearch = args.Search;

        if (SearchBox.Text != args.Search)
            SearchBox.Text = args.Search;

        RefreshChrome();
        RefreshFront();
        Show();
    }

    public override void Hide()
    {
        if (!Visible)
            return;

        SearchBox.IsFocused = false;
        ReleaseCovers();
        ClearArt();
        base.Hide();
    }

    public override void Dispose()
    {
        ReleaseCovers();
        ClearArt();
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

        if ((e.Keycode == Keycode.Enter) && SearchBox.IsFocused)
        {
            SearchBox.IsFocused = false;
            AppliedSearch = SearchBox.Text.Trim();
            RequestPage(Shelf, 0);
            e.Handled = true;

            return;
        }

        if (!SearchBox.IsFocused && (e.Keycode is Keycode.Left or Keycode.Right))
        {
            Layout.Spin(e.Keycode == Keycode.Left ? -1 : 1);
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
    }

    public override void OnMouseScroll(MouseScrollEvent e)
    {
        Layout.Spin(-Math.Sign(e.Delta));
        e.Handled = true;
    }

    public override void OnClick(ClickEvent e)
    {
        var localX = e.ScreenX - ScreenX;
        var localY = e.ScreenY - ScreenY;

        foreach (var index in Layout.DrawOrder()
                                     .Reverse())
        {
            var place = Layout.Place(index);

            if (!place.Visible)
                continue;

            if (BookSpine.SpineRect(place.X, place.Scale, ShelfBottom(place.Lift))
                          .Contains(localX, localY))
            {
                Layout.BringToFront(index);
                e.Handled = true;

                return;
            }
        }

        base.OnClick(e);
    }

    public override void OnDoubleClick(DoubleClickEvent e)
    {
        if ((Rows.Count > 0) && FrontRect().Contains(e.ScreenX - ScreenX, e.ScreenY - ScreenY))
        {
            OpenFront();
            e.Handled = true;

            return;
        }

        base.OnDoubleClick(e);
    }

    public override void Update(GameTime gameTime)
    {
        if (Visible)
        {
            var seconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
            Layout.Step(seconds);

            if (TurnProgress < 1f)
                TurnProgress = Math.Min(1f, TurnProgress + (seconds / TURN_SECONDS));

            if (Layout.Front != TurnIndex)
            {
                TurnIndex = Layout.Front;
                TurnProgress = 0f;
                RefreshFront();
            }

            FetchCovers();
        }

        base.Update(gameTime);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        base.Draw(spriteBatch);
        DrawShelf(spriteBatch);
        DrawSpines(spriteBatch);
        DrawFrontCover(spriteBatch);
    }

    private void ToggleSort()
    {
        Sort = Sort == LibrarySortCode.Newest ? LibrarySortCode.AToZ : LibrarySortCode.Newest;
        SortButton.Caption = Sort == LibrarySortCode.Newest ? "Newest" : "A-Z";
        RequestPage(Shelf, 0);
    }

    private void RequestPage(LibraryShelfCode shelf, byte page)
        => ActionRequested?.Invoke(
            new CollegeActionArgs
            {
                Type = CollegeActionType.LibraryShelfPage,
                Shelf = shelf,
                Sort = Sort,
                Page = page,
                Text = AppliedSearch
            });

    private void OpenFront()
    {
        if ((Layout.Front >= 0) && (Layout.Front < Rows.Count))
            ActionRequested?.Invoke(
                new CollegeActionArgs
                {
                    Type = CollegeActionType.LibraryOpenBook,
                    Id = Rows[Layout.Front].Id,
                    OpenMode = LibraryOpenMode.Shelf
                });
    }

    private void RefreshChrome()
    {
        HeaderLabel.Text = $"{LibraryLabels.ShelfName(Shelf)} - {Total} books";

        foreach (var (shelf, button) in TabButtons)
            button.Selected = shelf == (int)Shelf;

        SortButton.Caption = Sort == LibrarySortCode.Newest ? "Newest" : "A-Z";

        var pages = Math.Max(1, (int)PageCount);
        Page = Math.Clamp(Page, (byte)0, (byte)(pages - 1));
        PageLabel.Text = $"Page {Page + 1} of {pages}";
        PrevPageButton.Enabled = Page > 0;
        NextPageButton.Enabled = Page < (pages - 1);

        EmptyLabel.Visible = Rows.Count == 0;
        OpenButton.Enabled = Rows.Count > 0;
    }

    private void RefreshFront()
    {
        if ((Layout.Front < 0) || (Layout.Front >= Rows.Count))
        {
            TitleLabel.Text = string.Empty;
            AuthorLabel.Text = string.Empty;
            DateLabel.Text = string.Empty;

            return;
        }

        var row = Rows[Layout.Front];
        TitleLabel.Text = row.Title;
        AuthorLabel.Text = string.IsNullOrEmpty(row.Author) ? string.Empty : $"by {row.Author}";
        DateLabel.Text = BookCover.FormatDate(row.ApprovedUnix);
    }

    private void FetchCovers()
    {
        var now = DateTime.UtcNow;

        for (var i = 0; i < Rows.Count; i++)
        {
            if (!Layout.ShouldLoadCover(i))
                continue;

            var row = Rows[i];

            if (Held.ContainsKey(row.Id))
                continue;

            switch (CoverChoice.Of(row.CoverHash, row.DrawingCover))
            {
                case CoverKind.Picture:
                    if (Transfers.TryGetTexture(row.CoverHash, out _))
                    {
                        Transfers.Hold(row.CoverHash);
                        Held[row.Id] = (CoverKind.Picture, row.CoverHash);
                    }

                    break;
                case CoverKind.Drawing:
                    if (Drawings.TryGetTexture(row.Id, out _))
                    {
                        Drawings.Hold(row.Id);
                        Held[row.Id] = (CoverKind.Drawing, string.Empty);
                    } else if (Drawings.NeedsFetch(row.Id, now))
                        ActionRequested?.Invoke(new CollegeActionArgs { Type = CollegeActionType.LibraryDrawingFetch, Id = row.Id });

                    break;
            }
        }
    }

    private void ReleaseCovers()
    {
        foreach (var (id, held) in Held)
            if (held.Kind == CoverKind.Picture)
                Transfers.Release(held.Hash);
            else
                Drawings.Release(id);

        Held.Clear();
    }

    private void ClearArt()
    {
        foreach (var texture in Spines.Values)
            texture.Dispose();

        Spines.Clear();

        foreach (var (_, cover) in Covers.Values)
            cover.Dispose();

        Covers.Clear();
    }

    private static int ShelfBottom(float lift) => SHELF_BASE - (int)MathF.Round(lift);

    private Rectangle FrontRect()
    {
        var rect = BookCover.CoverRect(BAND_CENTER_X, COVER_TOP);
        rect.Offset(ScreenX, ScreenY);

        return rect;
    }

    private void DrawShelf(SpriteBatch spriteBatch)
        => UIElement.DrawRect(
            spriteBatch,
            new Rectangle(ScreenX + BAND_X, ScreenY + BAND_TOP + BAND_HEIGHT - 2, BAND_WIDTH, 2),
            new Color(40, 28, 16));

    private void DrawSpines(SpriteBatch spriteBatch)
    {
        foreach (var index in Layout.DrawOrder())
        {
            var place = Layout.Place(index);

            if (!place.Visible)
                continue;

            var row = Rows[index];
            var rect = BookSpine.SpineRect(place.X, place.Scale, ShelfBottom(place.Lift));
            rect.Offset(ScreenX, ScreenY);

            BookSpine.Draw(spriteBatch, BookSpine.GetTexture(ChaosGame.Device, Spines, row.Id, row.Title, BookSpine.SpineColor(row.Spine)), rect);
        }
    }

    private void DrawFrontCover(SpriteBatch spriteBatch)
    {
        if ((Layout.Front < 0) || (Layout.Front >= Rows.Count))
            return;

        var row = Rows[Layout.Front];
        var rect = FrontRect();
        var kind = CoverChoice.Of(row.CoverHash, row.DrawingCover);
        Texture2D? art = null;

        if ((kind == CoverKind.Picture) && Held.ContainsKey(row.Id) && Transfers.TryGetTexture(row.CoverHash, out var picture))
            art = picture;
        else if ((kind == CoverKind.Drawing) && Held.ContainsKey(row.Id) && Drawings.TryGetTexture(row.Id, out var drawing))
            art = drawing;

        var failed = (kind == CoverKind.Picture && Transfers.HasFailed(row.CoverHash))
                     || (kind == CoverKind.Drawing && Drawings.HasGivenUp(row.Id, DateTime.UtcNow));
        var shown = CoverChoice.Shows(kind, art is not null, failed);

        if (shown == CoverShown.Loading)
        {
            BookCover.DrawLoading(spriteBatch, rect, TurnProgress);

            return;
        }

        if (!Covers.TryGetValue(row.Id, out var cached) || (cached.Shown != shown))
        {
            if (cached.Texture is not null)
                cached.Texture.Dispose();

            cached = (shown, BookCover.Build(ChaosGame.Device, row.Title, row.Author, BookSpine.SpineColor(row.Spine), shown == CoverShown.Art ? art : null, row.CoverStyle));
            Covers[row.Id] = cached;
        }

        BookCover.DrawTexture(spriteBatch, cached.Texture, rect, TurnProgress);
    }
}
