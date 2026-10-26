using Chaos.Client.Rendering.Library;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests.College.Library;

public sealed class BookArtTests
{
    private sealed class SolidGlyphs : IGlyphBits
    {
        public byte Row(char c, int row) => c == ' ' ? (byte)0 : (byte)0xFC;
    }

    private static readonly Color Green = new(30, 80, 40);

    [Test]
    public void Fit_keeps_text_that_fits()
        => BookArt.Fit("The Bee Problem", 96).Should().Be("The Bee Problem");

    [Test]
    public void Fit_cuts_long_text_with_dots()
    {
        var fitted = BookArt.Fit("The Long Winter Road to Piet", 96);

        fitted.Should().EndWith("...");
        BookArt.TextWidth(fitted).Should().BeLessThanOrEqualTo(96);
    }

    [Test]
    public void Fit_handles_room_smaller_than_dots()
        => BookArt.TextWidth(BookArt.Fit("Anything", 10)).Should().BeLessThanOrEqualTo(18);

    [Test]
    public void Wrap_splits_on_words_into_two_lines()
        => BookArt.Wrap("The Bee Problem", 80, 2).Should().Equal("The Bee", "Problem");

    [Test]
    public void Wrap_cuts_the_last_line_when_more_words_remain()
    {
        var lines = BookArt.Wrap("Songs of the Long Woods and Other Tales of Temuair", 80, 2);

        lines.Should().HaveCount(2);
        lines[1].Should().EndWith("...");
        lines.Should().OnlyContain(l => BookArt.TextWidth(l) <= 80);
    }

    [Test]
    public void Wrap_cuts_a_single_long_word()
    {
        var lines = BookArt.Wrap(new string('W', 30), 80, 2);

        lines.Should().ContainSingle().Which.Should().EndWith("...");
        BookArt.TextWidth(lines[0]).Should().BeLessThanOrEqualTo(80);
    }

    [Test]
    public void Spine_is_spine_sized()
        => BookArt.PaintSpine("Poems", Green, new SolidGlyphs()).Should().HaveCount(BookArt.SPINE_WIDTH * BookArt.SPINE_HEIGHT);

    [Test]
    public void Spine_title_stays_inside_plate()
    {
        var pixels = BookArt.PaintSpine(new string('M', 60), Green, new SolidGlyphs());
        var plate = BookArt.SpinePlate;

        for (var y = 0; y < BookArt.SPINE_HEIGHT; y++)
        for (var x = 0; x < BookArt.SPINE_WIDTH; x++)
            if (pixels[(y * BookArt.SPINE_WIDTH) + x] == BookArt.Ink)
                plate.Contains(x, y).Should().BeTrue($"ink at {x},{y} is outside the plate");
    }

    [Test]
    public void Spine_has_gold_bands()
    {
        var pixels = BookArt.PaintSpine("Poems", Green, new SolidGlyphs());

        pixels[(4 * BookArt.SPINE_WIDTH) + 10].Should().Be(BookArt.Gold);
        pixels[((BookArt.SPINE_HEIGHT - 6) * BookArt.SPINE_WIDTH) + 10].Should().Be(BookArt.Gold);
    }

    [Test]
    public void Framed_art_box_keeps_clear_of_byline()
    {
        foreach (var (w, h) in new[] { (384, 288), (100, 300), (400, 50), (1, 1) })
        {
            var box = BookArt.FramedArtBox(w, h, plateBottom: 44);

            box.Bottom.Should().BeLessThanOrEqualTo(BookArt.BylineTop - 4, $"{w}x{h}");
            box.Left.Should().BeGreaterThanOrEqualTo(BookArt.FRAME_LEFT + 6);
            box.Right.Should().BeLessThanOrEqualTo(BookArt.FRAME_RIGHT - 5);
            box.Width.Should().BeGreaterThan(0);
            box.Height.Should().BeGreaterThan(0);
        }
    }

    [Test]
    public void Full_art_crop_comes_from_the_centre()
    {
        var crop = BookArt.FullArtCrop(384, 288);

        // 109 x 169 area, so the scale is set by the height and the sides are cut evenly
        crop.Y.Should().Be(0);
        crop.Height.Should().Be(288);
        crop.X.Should().Be((384 - crop.Width) / 2);
    }

    [Test]
    public void Covers_are_cover_sized()
    {
        var art = Enumerable.Repeat(Color.Red, 384 * 288).ToArray();
        var size = BookArt.COVER_WIDTH * BookArt.COVER_HEIGHT;

        BookArt.PaintPlainCover("T", "A", Green, new SolidGlyphs()).Should().HaveCount(size);
        BookArt.PaintFramedCover("T", "A", Green, art, 384, 288, new SolidGlyphs()).Should().HaveCount(size);
        BookArt.PaintFullCover("T", "A", Green, art, 384, 288, new SolidGlyphs()).Should().HaveCount(size);
    }

    [Test]
    public void Fit_returns_empty_when_room_holds_no_dots()
        => BookArt.Fit("Long title", 10).Should().BeEmpty();

    [Test]
    public void Framed_cover_leaves_a_dark_mat_around_the_art()
    {
        const int artWidth = 40;
        const int artHeight = 30;
        var art = Enumerable.Repeat(Color.Red, artWidth * artHeight).ToArray();
        var pixels = BookArt.PaintFramedCover("T", "A", Green, art, artWidth, artHeight, new SolidGlyphs());

        var room = (BookArt.FRAME_RIGHT - 6 - (BookArt.FRAME_LEFT + 6)) - 6;
        var below = 20 + 8 + (12 * BookArt.Wrap("T", room, 2).Count);
        var box = BookArt.FramedArtBox(artWidth, artHeight, below);
        var midY = box.Top + (box.Height / 2);

        pixels[(midY * BookArt.COVER_WIDTH) + box.Left].Should().Be(new Color(14, 10, 6));
        pixels[(midY * BookArt.COVER_WIDTH) + box.Left + 1 + 2].Should().Be(Color.Red);
    }

    [Test]
    public void Full_cover_shows_the_art_right_of_the_hinge()
    {
        var art = Enumerable.Repeat(Color.Red, 384 * 288).ToArray();
        var pixels = BookArt.PaintFullCover("T", "A", Green, art, 384, 288, new SolidGlyphs());

        pixels[(90 * BookArt.COVER_WIDTH) + 60].Should().Be(Color.Red);
        pixels[(90 * BookArt.COVER_WIDTH) + 3].Should().NotBe(Color.Red);
    }

    [Test]
    public void Empty_author_draws_no_byline()
    {
        var with = BookArt.PaintPlainCover("T", "Aromatic", Green, new SolidGlyphs());
        var without = BookArt.PaintPlainCover("T", string.Empty, Green, new SolidGlyphs());

        without.Count(p => p == BookArt.Byline).Should().Be(0);
        with.Count(p => p == BookArt.Byline).Should().BeGreaterThan(0);
    }
}
