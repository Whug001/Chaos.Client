using Chaos.Client.ViewModel.College.Library;
using FluentAssertions;

namespace Chaos.Client.Tests.College.Library;

public class CarouselLayoutTests
{
    [Test]
    public void The_front_book_sits_in_the_middle_at_full_size()
    {
        var layout = new CarouselLayout(count: 10, centerX: 250, radius: 200);
        layout.JumpTo(3);
        var place = layout.Place(3);
        place.Visible.Should().BeTrue();
        place.X.Should().BeApproximately(250, 0.01f);
        place.Scale.Should().BeApproximately(1f, 0.001f);
    }

    [Test]
    public void Books_either_side_are_placed_symmetrically()
    {
        var layout = new CarouselLayout(10, 250, 200);
        layout.JumpTo(5);
        (250 - layout.Place(4).X).Should().BeApproximately(layout.Place(6).X - 250, 0.01f);
        layout.Place(4).Scale.Should().BeLessThan(1f);
    }

    [Test]
    public void Books_more_than_four_places_away_are_hidden()
    {
        var layout = new CarouselLayout(12, 250, 200);
        layout.JumpTo(6);
        layout.Place(1).Visible.Should().BeFalse();
        layout.Place(2).Visible.Should().BeTrue();
    }

    [Test]
    public void Spinning_past_either_end_stops_at_the_end()
    {
        var layout = new CarouselLayout(5, 250, 200);
        layout.Spin(-3);
        layout.Target.Should().Be(0);
        layout.Spin(99);
        layout.Target.Should().Be(4);
    }

    [Test]
    public void Stepping_moves_toward_the_target_without_overshooting()
    {
        var layout = new CarouselLayout(10, 250, 200);
        layout.Spin(3);
        layout.Step(0.1f);
        layout.Current.Should().BeGreaterThan(0).And.BeLessThan(3);
        layout.Step(10f);
        layout.Current.Should().Be(3);
    }

    [Test]
    public void Covers_load_only_within_two_places_of_the_front()
    {
        var layout = new CarouselLayout(10, 250, 200);
        layout.JumpTo(5);
        layout.ShouldLoadCover(3).Should().BeTrue();
        layout.ShouldLoadCover(7).Should().BeTrue();
        layout.ShouldLoadCover(2).Should().BeFalse();
        layout.ShouldLoadCover(8).Should().BeFalse();
    }

    [Test]
    public void The_front_book_draws_last()
    {
        var layout = new CarouselLayout(10, 250, 200);
        layout.JumpTo(5);
        layout.DrawOrder().Last().Should().Be(5);
        layout.DrawOrder().First().Should().BeOneOf(1, 9);
    }

    [Test]
    public void An_empty_shelf_has_nothing_to_draw()
    {
        var layout = new CarouselLayout(0, 250, 200);
        layout.DrawOrder().Should().BeEmpty();
        layout.Front.Should().Be(-1);
    }
}
