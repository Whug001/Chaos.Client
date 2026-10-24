using Chaos.Client.Controls.World.Popups.College.Library;
using Chaos.Client.ViewModel.College.Library;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests.College.Library;

public class LibraryCarouselDrawTests
{
    [Test]
    public void A_spine_at_full_size_is_28_by_120_with_its_bottom_on_the_shelf()
    {
        var rect = BookSpine.SpineRect(100f, 1f, 300);

        rect.Width.Should().Be(28);
        rect.Height.Should().Be(120);
        rect.Bottom.Should().Be(300);
        rect.X.Should().Be(100 - 14);
    }

    [Test]
    public void A_spine_shrinks_around_its_centre_and_never_collapses()
    {
        var rect = BookSpine.SpineRect(100f, 0.4f, 300);

        rect.Width.Should().Be(11);
        rect.Height.Should().Be(48);
        rect.Bottom.Should().Be(300);
        rect.Center.X.Should().Be(100);
        BookSpine.SpineRect(100f, 100f, 300).Width.Should().BeGreaterThan(0);
    }

    [Test]
    public void Spine_colours_wrap_the_eight_entry_palette()
    {
        BookSpine.SpineColor(0).Should().Be(LibraryLabels.Spines[0]);
        BookSpine.SpineColor(8).Should().Be(LibraryLabels.Spines[0]);
        BookSpine.SpineColor(9).Should().Be(LibraryLabels.Spines[1]);
    }

    [Test]
    public void The_spine_edge_is_darker_than_the_fill()
    {
        var fill = BookSpine.SpineColor(0);
        var edge = BookSpine.EdgeColor(fill);

        edge.R.Should().BeLessThanOrEqualTo(fill.R);
        edge.G.Should().BeLessThanOrEqualTo(fill.G);
        edge.B.Should().BeLessThanOrEqualTo(fill.B);
        edge.A.Should().Be(fill.A);
    }

    [Test]
    public void Short_titles_fit_rotated_and_long_ones_fall_back_to_twelve_chars()
    {
        BookSpine.FitsRotatedTitle(28).Should().BeTrue();
        BookSpine.FitsRotatedTitle(9).Should().BeFalse();
        BookSpine.FallbackTitle("Short").Should().Be("Short");
        BookSpine.FallbackTitle("A much longer title").Should().Be("A much longe");
    }

    [Test]
    public void The_front_cover_is_120_by_170_on_its_centre()
    {
        var rect = BookCover.CoverRect(280, 56);

        rect.Width.Should().Be(120);
        rect.Height.Should().Be(170);
        rect.X.Should().Be(220);
        rect.Y.Should().Be(56);
    }

    [Test]
    public void The_turn_narrows_the_cover_around_its_centre()
    {
        var full = BookCover.CoverRect(280, 56);

        BookCover.TurnRect(full, 1f).Should().Be(full);

        var half = BookCover.TurnRect(full, 0.5f);
        half.Width.Should().Be(60);
        half.Center.X.Should().Be(full.Center.X);
        half.Y.Should().Be(full.Y);
        half.Height.Should().Be(full.Height);

        BookCover.TurnRect(full, 0f).Width.Should().Be(0);
    }

    [Test]
    public void A_wide_texture_is_letterboxed_top_and_bottom()
    {
        var fitted = BookCover.FitTexture(240, 100, new Rectangle(220, 56, 120, 170));

        fitted.Width.Should().Be(120);
        fitted.Height.Should().Be(50);
        fitted.X.Should().Be(220);
        fitted.Y.Should().Be(116);
    }

    [Test]
    public void A_tall_texture_is_letterboxed_left_and_right()
    {
        var fitted = BookCover.FitTexture(100, 340, new Rectangle(220, 56, 120, 170));

        fitted.Width.Should().Be(50);
        fitted.Height.Should().Be(170);
        fitted.X.Should().Be(255);
        fitted.Y.Should().Be(56);
    }

    [Test]
    public void Approval_dates_render_as_year_month_day()
    {
        BookCover.FormatDate(0).Should().Be("1970-01-01");
        BookCover.FormatDate(1700000000).Should().Be("2023-11-14");
    }
}
