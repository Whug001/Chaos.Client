using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.ViewModel.College.Library;

public static class LibraryLabels
{
    public static string ShelfName(LibraryShelfCode shelf)
        => shelf switch
        {
            LibraryShelfCode.Guides => "Guides",
            LibraryShelfCode.Lore => "Lore",
            LibraryShelfCode.ShortStories => "Short stories",
            LibraryShelfCode.PoemsAndSongs => "Poems and songs",
            LibraryShelfCode.Artwork => "Artwork",
            _ => "Guides"
        };

    public static string StatusName(LibraryStatusCode status)
        => status switch
        {
            LibraryStatusCode.Draft => "Draft",
            LibraryStatusCode.Waiting => "Waiting for review",
            LibraryStatusCode.OnShelf => "On the shelf",
            LibraryStatusCode.EditWaiting => "Edit waiting",
            LibraryStatusCode.Rejected => "Rejected",
            LibraryStatusCode.TakenDown => "Taken down",
            _ => "Draft"
        };

    public static Color[] Spines { get; } =
    [
        new Color(110, 30, 30),
        new Color(30, 80, 40),
        new Color(30, 40, 90),
        new Color(170, 120, 40),
        new Color(90, 40, 90),
        new Color(30, 100, 100),
        new Color(80, 85, 95),
        new Color(100, 70, 40)
    ];
}
