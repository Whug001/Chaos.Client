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
}
