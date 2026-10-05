#region
using System.Globalization;
using Chaos.Client.Rendering.Definitions;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>A Teacher's list of the class's hand-ins: read one, or show it to the class.</summary>
public sealed class CollegeHandInsControl : CollegeListWindow<CollegeHandInRowInfo>
{
    private List<CollegeHandInRowInfo> All = [];

    public CollegeHandInsControl()
        : base(
            "CollegeHandIns",
            32,
            12,
            [("Student", 0), ("Title", 110), ("Handed in", 370)],
            "No hand-ins yet.")
    {
        Caption("Class hand-ins", LEFT, TITLE_TOP, INNER_WIDTH, HorizontalAlignment.Center, LegendColors.Gold);

        AddSelectionButton(
            "Show to class",
            110,
            id => Raise(
                new CollegeActionArgs
                {
                    Type = CollegeActionType.ShowToClass,
                    Id = id
                }));
    }

    protected override IReadOnlyList<CollegeHandInRowInfo> Items => All;

    /// <summary>Shows the hand-ins the server sent. A list that was closed starts on the first page.</summary>
    public void Open(CollegeDisplayArgs args)
    {
        if (!Visible)
            Page = 0;

        All = args.HandInRows;
        ShowRows();

        if (!Visible)
            Show();
    }

    //UTC, like the reader's header and the class timetable
    private static string HandedIn(uint unix)
        => DateTimeOffset.FromUnixTimeSeconds(unix)
                         .ToString("ddd HH:mm 'UTC'", CultureInfo.InvariantCulture);

    protected override (string Text, Color Color)[] ColumnsOf(CollegeHandInRowInfo item)
        =>
        [
            (item.Student, LegendColors.White),
            (item.Title, LegendColors.White),
            (HandedIn(item.HandedInUnix), LegendColors.Gray)
        ];

    protected override int IdOf(CollegeHandInRowInfo item) => item.Id;

    protected override CollegeActionArgs OpenAction(int id)
        => new()
        {
            Type = CollegeActionType.OpenPiece,
            Source = CollegePieceSource.HandIn,
            Id = id
        };
}
