using Chaos.Client.Controls.World.Popups.College.Library;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests.College.Library;

public class LibraryBookOptionsTests
{
    [Test]
    public void Style_toggle_shows_for_picture_or_artwork_only()
    {
        LibraryBookOptions.ShowsStyle(new string('a', 64), fixedShelf: false).Should().BeTrue();
        LibraryBookOptions.ShowsStyle(string.Empty, fixedShelf: true).Should().BeTrue();
        LibraryBookOptions.ShowsStyle(string.Empty, fixedShelf: false).Should().BeFalse();
    }

    [Test]
    public void Style_toggle_flips()
    {
        LibraryBookOptions.Toggle(LibraryCoverStyle.Framed).Should().Be(LibraryCoverStyle.Full);
        LibraryBookOptions.Toggle(LibraryCoverStyle.Full).Should().Be(LibraryCoverStyle.Framed);
    }
}
