using Chaos.Client.ViewModel.College;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class BoardFontTests
{
    [Test]
    public void An_I_is_nine_pixels_three_wide_and_five_tall()
    {
        var pixels = BoardFont.Pixels("I", 0, 0, 1).ToList();

        pixels.Should().HaveCount(9);
        pixels.Max(p => p.X).Should().Be(2);
        pixels.Max(p => p.Y).Should().Be(4);
    }

    [Test]
    public void Letters_are_one_pixel_apart()
    {
        BoardFont.MeasureWidth("HI", 1).Should().Be(7);
        BoardFont.Pixels("HI", 0, 0, 1).Should().HaveCount(20);
        BoardFont.Pixels("HI", 0, 0, 1).Min(p => p.X).Should().Be(0);
    }

    [Test]
    public void Large_doubles_every_pixel()
    {
        var pixels = BoardFont.Pixels("I", 5, 5, 2).ToList();

        pixels.Should().HaveCount(36);
        pixels.Max(p => p.Y).Should().Be(14);
        BoardFont.MeasureWidth("HI", 2).Should().Be(14);
    }

    [Test]
    public void Lower_case_draws_as_upper_case_and_unknown_characters_as_spaces()
    {
        BoardFont.Pixels("hi", 0, 0, 1).Should().Equal(BoardFont.Pixels("HI", 0, 0, 1));
        BoardFont.MeasureWidth("A@A", 1).Should().Be(11);
    }

    [Test]
    public void Filtering_keeps_drawable_characters_up_to_forty()
    {
        BoardFont.FilterText("hi @ there!").Should().Be("HI  THERE!");
        BoardFont.FilterText(new string('a', 50)).Should().HaveLength(CollegeProtocol.MAX_BOARD_TEXT_CHARS);
    }
}
