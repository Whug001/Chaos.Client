using Chaos.Client.ViewModel.College.Library;
using FluentAssertions;

namespace Chaos.Client.Tests.College.Library;

public class CoverChoiceTests
{
    [Test]
    public void A_cover_hash_wins()
        => CoverChoice.Of(new string('a', 64), drawingCover: true).Should().Be(CoverKind.Picture);

    [Test]
    public void Artwork_without_a_cover_uses_its_drawing()
        => CoverChoice.Of(string.Empty, drawingCover: true).Should().Be(CoverKind.Drawing);

    [Test]
    public void A_book_with_neither_gets_a_plain_cover()
        => CoverChoice.Of(string.Empty, drawingCover: false).Should().Be(CoverKind.Plain);

    [Test]
    public void Failed_picture_uses_plain_cover()
        => CoverChoice.Shows(CoverKind.Picture, artReady: false, artFailed: true).Should().Be(CoverShown.Plain);

    [Test]
    public void Loaded_picture_uses_its_style()
        => CoverChoice.Shows(CoverKind.Picture, artReady: true, artFailed: false).Should().Be(CoverShown.Art);

    [Test]
    public void Waiting_picture_shows_loading()
        => CoverChoice.Shows(CoverKind.Drawing, artReady: false, artFailed: false).Should().Be(CoverShown.Loading);

    [Test]
    public void Plain_book_is_plain()
        => CoverChoice.Shows(CoverKind.Plain, artReady: false, artFailed: false).Should().Be(CoverShown.Plain);

    [Test]
    public void Loaded_art_wins_over_a_stale_failure_flag()
        => CoverChoice.Shows(CoverKind.Picture, artReady: true, artFailed: true).Should().Be(CoverShown.Art);
}
