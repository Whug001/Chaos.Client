using Chaos.Client.Systems.Cinema;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests.Cinema;

public sealed class SeatedViewLayoutTests
{
    //formatter:off
    [Test]
    [Arguments(0, 80, 60, 480, 263)]
    [Arguments(1, 100, 72, 440, 241)]
    [Arguments(2, 120, 84, 400, 219)]
    //formatter:on
    public void Screen_shrinks_with_distance(int row, int x, int y, int w, int h)
        => SeatedViewLayout.Screen(row).Should().Be(new Rectangle(x, y, w, h));

    //formatter:off
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    //formatter:on
    public void Backdrop_covers_the_window(int row)
        => SeatedViewLayout.Backdrop(SeatedViewLayout.Screen(row)).Contains(new Rectangle(0, 0, 640, 480)).Should().BeTrue();

    [Test]
    public void Rows_in_front_are_farthest_first()
    {
        SeatedViewLayout.RowsInFront(0).Should().BeEmpty();
        SeatedViewLayout.RowsInFront(1).Should().Equal(0);
        SeatedViewLayout.RowsInFront(2).Should().Equal(0, 1);
    }

    [Test]
    public void Nearest_row_seat_straight_ahead()
        => SeatedViewLayout.SeatBack(2, 3, 1, 3).Should().Be(new Rectangle(265, 370, 110, 70));

    [Test]
    public void Next_seat_to_the_right_is_one_spacing_over()
        => SeatedViewLayout.SeatBack(2, 3, 1, 4).X.Should().Be(265 + 120);

    [Test]
    public void Far_row_is_smaller_and_higher()
        => SeatedViewLayout.SeatBack(2, 3, 0, 3).Should().Be(new Rectangle(282, 330, 77, 49));

    [Test]
    public void Head_sits_on_its_seat_back()
    {
        var seat = SeatedViewLayout.SeatBack(2, 3, 1, 3);

        SeatedViewLayout.Head(seat, 1).Should().Be(new Rectangle(275, 328, 90, 90));
    }

    [Test]
    public void Head_on_far_row_is_smaller_and_higher()
    {
        var seat = SeatedViewLayout.SeatBack(2, 3, 0, 3);

        SeatedViewLayout.Head(seat, 2).Should().Be(new Rectangle(289, 300, 63, 63));
    }

    [Test]
    public void Panel_near_row_is_one_full_width_tile()
    {
        var tiles = SeatedViewLayout.Panel(1);

        tiles.Should().HaveCount(1);
        tiles[0].X.Should().BeLessThanOrEqualTo(0);
        tiles[^1].Right.Should().BeGreaterThanOrEqualTo(SeatedViewLayout.WIDTH);
        tiles[0].Y.Should().Be(410);
        tiles[0].Width.Should().Be(640);
        tiles[0].Height.Should().Be(96);
    }

    [Test]
    public void Panel_far_row_tiles_cover_the_window()
    {
        var tiles = SeatedViewLayout.Panel(2);

        tiles.Should().HaveCount(3);
        tiles[0].X.Should().BeLessThanOrEqualTo(0);
        tiles[^1].Right.Should().BeGreaterThanOrEqualTo(SeatedViewLayout.WIDTH);

        foreach (var tile in tiles)
        {
            tile.Y.Should().Be(358);
            tile.Width.Should().Be(448);
            tile.Height.Should().Be(67);
        }
    }
}
