#region
using Chaos.Client.Rendering.Definitions;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.College.Library;

/// <summary>The viewer's College pieces that can be copied to the library. Rows carry a local index id.</summary>
public sealed class LibraryCollegePiecesControl : CollegeListWindow<LibraryCollegeRowInfo>
{
    private List<LibraryCollegeRowInfo> All = [];
    private readonly Dictionary<int, (LibraryCopySource Source, int Id)> Targets = new();

    public LibraryCollegePiecesControl()
        : base(
            "LibraryCollegePieces",
            32,
            12,
            [("Title", 0), ("Subject", 320)],
            "No College pieces to copy.")
    {
        Caption("Copy from the College", LEFT, TITLE_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gold);
    }

    protected override IReadOnlyList<LibraryCollegeRowInfo> Items => All;

    protected override string OpenCaption => "Copy";

    /// <summary>Shows the College pieces the server sent. A list that was closed starts on the first page.</summary>
    public void Open(CollegeDisplayArgs args)
    {
        if (!Visible)
            Page = 0;

        All = args.CollegeRows;
        Targets.Clear();

        for (var i = 0; i < All.Count; i++)
            Targets[i + 1] = (All[i].Source, All[i].Id);

        ShowRows();

        if (!Visible)
            Show();
    }

    protected override (string Text, Color Color)[] ColumnsOf(LibraryCollegeRowInfo item)
        =>
        [
            (item.Title, LegendColors.White),
            (item.Subject.ToString(), LegendColors.Gray)
        ];

    protected override int IdOf(LibraryCollegeRowInfo item) => All.IndexOf(item) + 1;

    protected override CollegeActionArgs OpenAction(int id)
    {
        Targets.TryGetValue(id, out var target);

        return new CollegeActionArgs
        {
            Type = CollegeActionType.LibraryCopyCollege,
            CopySource = target.Source,
            Id = target.Id
        };
    }
}
