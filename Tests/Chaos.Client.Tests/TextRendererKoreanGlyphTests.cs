using Chaos.Client.Rendering;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class TextRendererKoreanGlyphTests
{
    [Test]
    public void GetKoreanGlyphIndex_FirstAndLastSyllables_AreFontEnds()
    {
        TextRenderer.GetKoreanGlyphIndex('가').Should().Be(0);
        TextRenderer.GetKoreanGlyphIndex('힝').Should().Be(2349);
    }

    [Test]
    public void GetKoreanGlyphIndex_ReportedWord_MapsToFontOrder()
    {
        TextRenderer.GetKoreanGlyphIndex('이').Should().Be(1547);
        TextRenderer.GetKoreanGlyphIndex('렇').Should().Be(681);
        TextRenderer.GetKoreanGlyphIndex('게').Should().Be(51);
    }

    [Test]
    public void GetKoreanGlyphIndex_Jamo_FollowTheSyllables()
    {
        TextRenderer.GetKoreanGlyphIndex('ㄱ').Should().Be(2350);
        TextRenderer.GetKoreanGlyphIndex('ㅣ').Should().Be(2400);
    }

    [Test]
    public void GetKoreanGlyphIndex_UnmappedChar_ReturnsMinusOne()
        => TextRenderer.GetKoreanGlyphIndex('A').Should().Be(-1);
}
