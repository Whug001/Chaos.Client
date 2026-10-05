#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.Systems.College;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>
///     The gallery: one subject's awarded pieces, in the order the server sent them (tier, then newest). The Art tab shows a picture grid of 8 and the other tabs show 10 rows.
///     The subject tabs ask the server for that subject's list.
/// </summary>
public sealed class CollegeGalleryControl : CollegeListWindow<CollegeGalleryRowInfo>
{
    private const int ART_COLUMNS = 4;
    private const int ART_PAGE = 8;
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
    private readonly CustomButton[] SubjectTabs;
    private readonly UILabel TitleLabel;

    private List<CollegeGalleryRowInfo> All = [];
    private CollegeSubjectCode Subject;

    public CollegeGalleryControl(CollegeDrawings drawings)
        : base(
            "CollegeGallery",
            TABBED_HEADER_TOP,
            10,
            [("Title", 0), ("Author", 260), ("Award", 380)],
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
    }

    private bool IsArt => Subject == CollegeSubjectCode.Art;

    protected override int PageSize => IsArt ? ART_PAGE : base.PageSize;

    protected override bool ShowsRows => !IsArt;

    protected override IReadOnlyList<CollegeGalleryRowInfo> Items => All;

    public override void Dispose()
    {
        Drawings.DrawingReady -= OnDrawingReady;

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
        if (Cells is null)
            return;

        for (var i = 0; i < Cells.Length; i++)
        {
            var cell = Cells[i];
            cell.Visible = IsArt && (i < shown.Count);

            if (!cell.Visible)
                continue;

            var row = shown[i];
            cell.Set(row.Id, row.Title, row.Author, CollegeTiers.Badge(row.Tier));
            cell.Picture = Drawings.TryGetTexture(row.Id, out var texture) ? texture : null;

            if ((cell.Picture is null) && Drawings.NeedsFetch(row.Id))
                Raise(
                    new CollegeActionArgs
                    {
                        Type = CollegeActionType.DrawingFetch,
                        Id = row.Id
                    });
        }
    }

    protected override void SelectionChanged(int id)
    {
        if (Cells is null)
            return;

        foreach (var cell in Cells)
            cell.Selected = cell.Visible && (cell.Id == id);
    }

    private void OnDrawingReady(int id)
    {
        foreach (var cell in Cells)
            if (cell.Visible && (cell.Id == id) && Drawings.TryGetTexture(id, out var texture))
                cell.Picture = texture;
    }
}
