#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.Custom;
using Chaos.Client.Rendering.Definitions;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>
///     The gallery: one subject's awarded pieces, in the order the server sent them (tier, then newest), 10 to a page.
///     The subject tabs ask the server for that subject's list.
/// </summary>
public sealed class CollegeGalleryControl : CollegeListWindow<CollegeGalleryRowInfo>
{
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

    private readonly CustomButton[] SubjectTabs;
    private readonly UILabel TitleLabel;

    private List<CollegeGalleryRowInfo> All = [];
    private CollegeSubjectCode Subject;

    public CollegeGalleryControl()
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
    }

    protected override IReadOnlyList<CollegeGalleryRowInfo> Items => All;

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
}
