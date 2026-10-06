#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
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
///     The gallery: one subject's awarded pieces, in the order the server sent them (tier, then newest). The Art tab shows a picture grid of 8; the other tabs show 10 rows, and on the Music tab each row has a Play button.
///     The subject tabs ask the server for that subject's list.
/// </summary>
public sealed class CollegeGalleryControl : CollegeListWindow<CollegeGalleryRowInfo>
{
    private const int ART_COLUMNS = 4;
    private const int ART_PAGE = 8;
    private const int MUSIC_ROWS = 10;
    private const int CELL_GAP = 4;
    private const int TAB_WIDTH = 72;
    private const int TAB_GAP = 4;

    private static readonly CollegeSubjectCode[] Subjects =
    [
        CollegeSubjectCode.Art,
        CollegeSubjectCode.Music,
        CollegeSubjectCode.Literature,
        CollegeSubjectCode.History,
        CollegeSubjectCode.Lore,
        CollegeSubjectCode.Philosophy
    ];

    private readonly GalleryArtCell[] Cells;
    private readonly CollegeDrawings Drawings;
    private readonly TuneRowButton[] PlayButtons;
    private readonly TunePlayer Player;
    private readonly CustomButton[] SubjectTabs;
    private readonly Dictionary<int, object> TuneOwners = new();
    private readonly GalleryTunePlay TunePlay = new();
    private readonly CollegeTunes Tunes;
    private readonly UILabel TitleLabel;

    private List<CollegeGalleryRowInfo> All = [];
    private CollegeSubjectCode Subject;

    public CollegeGalleryControl(CollegeDrawings drawings, CollegeTunes tunes, TunePlayer player)
        : base(
            "CollegeGallery",
            TABBED_HEADER_TOP,
            MUSIC_ROWS,
            [("Title", 0), ("Author", 236), ("Award", 356)],
            "No pieces yet.")
    {
        TitleLabel = Caption(string.Empty, LEFT, TITLE_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gold);

        SubjectTabs = Subjects.Select((subject, i) => AddButton(
                                  subject.ToString(),
                                  TAB_WIDTH,
                                  LEFT + (i * (TAB_WIDTH + TAB_GAP)),
                                  TABS_TOP,
                                  () => Raise(
                                      new CollegeActionArgs
                                      {
                                          Type = CollegeActionType.GalleryPage,
                                          Subject = subject
                                      })))
                              .ToArray();

        Drawings = drawings;
        Drawings.DrawingReady += OnDrawingReady;

        Cells = new GalleryArtCell[ART_PAGE];

        for (var i = 0; i < ART_PAGE; i++)
        {
            var cell = new GalleryArtCell
            {
                X = LEFT + ((i % ART_COLUMNS) * GalleryArtCell.WIDTH),
                Y = TABBED_HEADER_TOP + ((i / ART_COLUMNS) * (GalleryArtCell.HEIGHT + CELL_GAP))
            };

            cell.Clicked += () => SelectId(cell.Id);
            cell.DoubleClicked += () => OpenId(cell.Id);
            AddChild(cell);
            Cells[i] = cell;
        }

        Tunes = tunes;
        Player = player;
        Tunes.TuneReady += OnTuneReady;
        PlayButtons = new TuneRowButton[MUSIC_ROWS];

        for (var i = 0; i < MUSIC_ROWS; i++)
        {
            var button = new TuneRowButton
            {
                X = LEFT + INNER_WIDTH - TuneRowButton.WIDTH - 2,
                Y = RowsTop + (i * CollegeListRow.HEIGHT) + 1,
                Visible = false
            };

            button.Pressed += OnPlayPressed;
            AddChild(button);
            PlayButtons[i] = button;
        }
    }

    private bool IsArt => Subject == CollegeSubjectCode.Art;

    private bool IsMusic => Subject == CollegeSubjectCode.Music;

    protected override int PageSize => IsArt ? ART_PAGE : base.PageSize;

    protected override bool ShowsRows => !IsArt;

    protected override IReadOnlyList<CollegeGalleryRowInfo> Items => All;

    public override void Dispose()
    {
        Drawings.DrawingReady -= OnDrawingReady;
        Tunes.TuneReady -= OnTuneReady;

        //the thumbnails belong to CollegeDrawings
        foreach (var cell in Cells)
            cell.Picture = null;

        base.Dispose();
    }

    /// <summary>Shows a subject's list. A new subject, or a gallery that was closed, starts on the first page.</summary>
    public void Open(CollegeDisplayArgs args)
    {
        if (!Visible || (args.Subject != Subject))
            Page = 0;

        if (args.Subject != Subject)
        {
            StopGalleryTune();
            TunePlay.Cancel();
        }

        Subject = args.Subject;
        All = args.GalleryRows;
        TitleLabel.Text = $"Gallery of {Subject}";

        for (var i = 0; i < SubjectTabs.Length; i++)
            SubjectTabs[i].Selected = Subjects[i] == Subject;

        ShowRows();

        if (!Visible)
            Show();
    }

    protected override (string Text, Color Color)[] ColumnsOf(CollegeGalleryRowInfo item)
        =>
        [
            (item.Title, LegendColors.White),
            (item.Author, LegendColors.Gray),
            (CollegeTiers.Name(item.Tier), CollegeTiers.Badge(item.Tier))
        ];

    protected override int IdOf(CollegeGalleryRowInfo item) => item.Id;

    protected override CollegeActionArgs OpenAction(int id)
        => new()
        {
            Type = CollegeActionType.OpenPiece,
            Source = CollegePieceSource.Gallery,
            Id = id
        };

    protected override void PageShown(IReadOnlyList<CollegeGalleryRowInfo> shown)
    {
        if (Cells is not null)
            for (var i = 0; i < Cells.Length; i++)
            {
                var cell = Cells[i];
                cell.Visible = IsArt && (i < shown.Count);

                if (!cell.Visible)
                    continue;

                var row = shown[i];
                cell.Set(row.Id, row.Title, row.Author, CollegeTiers.Badge(row.Tier));
                cell.Picture = Drawings.TryGetTexture(row.Id, out var texture) ? texture : null;
            }

        FetchMissingThumbnails();

        if (PlayButtons is null)
            return;

        for (var i = 0; i < PlayButtons.Length; i++)
        {
            var button = PlayButtons[i];
            button.Visible = IsMusic && (i < shown.Count);

            if (button.Visible)
                button.Id = shown[i].Id;
        }

        RefreshPlayButtons();
    }

    protected override void SelectionChanged(int id)
    {
        if (Cells is null)
            return;

        foreach (var cell in Cells)
            cell.Selected = cell.Visible && (cell.Id == id);
    }

    public override void Hide()
    {
        if (!Visible)
            return;

        StopGalleryTune();
        TunePlay.Cancel();
        base.Hide();
    }

    public override void Update(GameTime gameTime)
    {
        if (Visible && IsMusic)
        {
            TunePlay.Tick(DateTime.UtcNow);
            RefreshPlayButtons();
        }

        //a fetch the server's per-minute limit dropped gets no reply, so the shown page asks again until it is filled
        if (Visible && IsArt)
            FetchMissingThumbnails();

        base.Update(gameTime);
    }

    private void FetchMissingThumbnails()
    {
        if (Cells is null)
            return;

        var now = DateTime.UtcNow;

        foreach (var cell in Cells)
            if (cell.Visible && (cell.Picture is null) && Drawings.NeedsFetch(cell.Id, now))
                Raise(
                    new CollegeActionArgs
                    {
                        Type = CollegeActionType.DrawingFetch,
                        Id = cell.Id
                    });
    }

    private void StopGalleryTune()
    {
        if (Player.Owner is { } owner && TuneOwners.ContainsValue(owner))
            Player.Stop();
    }

    //one owner object per entry, so a tune keeps its row's Stop across page turns
    private object TuneOwner(int id)
    {
        if (!TuneOwners.TryGetValue(id, out var owner))
            TuneOwners[id] = owner = new object();

        return owner;
    }

    private bool IsPlaying(int id)
        => ReferenceEquals(Player.Owner, TuneOwner(id)) && (Player.IsPlaying || Player.IsRendering);

    private void OnPlayPressed(int id)
    {
        if (id == 0)
            return;

        var now = DateTime.UtcNow;
        var cached = Tunes.TryGet(id, out var tune);

        switch (TunePlay.Press(id, IsPlaying(id), cached, !cached && Tunes.NeedsFetch(id, now), now))
        {
            case TuneRowAction.Stop:
                Player.Stop();

                break;
            case TuneRowAction.Play:
                Player.Play(tune!, TuneOwner(id));

                break;
            case TuneRowAction.Fetch:
                Raise(
                    new CollegeActionArgs
                    {
                        Type = CollegeActionType.TuneFetch,
                        Id = id
                    });

                break;
        }

        RefreshPlayButtons();
    }

    private void OnTuneReady(int id)
    {
        if (Visible && TunePlay.Arrived(id) && Tunes.TryGet(id, out var tune))
            Player.Play(tune, TuneOwner(id));

        RefreshPlayButtons();
    }

    private void RefreshPlayButtons()
    {
        foreach (var button in PlayButtons)
        {
            if (!button.Visible)
                continue;

            var caption = GalleryTunePlay.Caption(IsPlaying(button.Id), TunePlay.Pending == button.Id);

            if (button.Caption != caption)
                button.Caption = caption;
        }
    }

    private void OnDrawingReady(int id)
    {
        foreach (var cell in Cells)
            if (cell.Visible && (cell.Id == id) && Drawings.TryGetTexture(id, out var texture))
                cell.Picture = texture;
    }
}
