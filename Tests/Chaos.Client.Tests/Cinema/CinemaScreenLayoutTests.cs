using Chaos.Client.Systems.Cinema;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests.Cinema;

public class CinemaScreenLayoutTests
{
    [Test]
    public void Wide_area_is_pillarboxed()
        => CinemaScreenLayout.FitPicture(new Rectangle(0, 0, 360, 190), 854, 480).Should().Be(new Rectangle(11, 0, 338, 190));

    [Test]
    public void Tall_area_is_letterboxed()
        => CinemaScreenLayout.FitPicture(new Rectangle(0, 0, 400, 400), 854, 480).Should().Be(new Rectangle(0, 88, 400, 224));

    [Test]
    public void Area_offset_is_kept()
        => CinemaScreenLayout.FitPicture(new Rectangle(100, 50, 360, 190), 854, 480).Should().Be(new Rectangle(111, 50, 338, 190));

    [Test]
    public void Wall_spot_is_the_recess()
    {
        CinemaScreenLayout.Wall.Should().Be(new CinemaWallSpot(Left: 524, Bottom: 34, Width: 296, Height: 166));
        CinemaScreenLayout.Wall.Columns.Should().Be(148);
    }

    [Test]
    public void Wall_columns_end_to_end()
    {
        var columns = CinemaScreenLayout.WallColumns(CinemaScreenLayout.Wall, 854, 480);

        columns.Should().HaveCount(148);
        columns[0].Should().Be(new CinemaWallColumn(3, new Rectangle(524, -132, 2, 166), new Rectangle(524, -132, 2, 166), new Rectangle(0, 0, 5, 480)));
        columns[147].Should().Be(new CinemaWallColumn(14, new Rectangle(818, 15, 2, 166), new Rectangle(818, 15, 1, 166), new Rectangle(851, 0, 3, 480)));
    }

    [Test]
    public void Wall_columns_step_down_with_the_wall()
    {
        var columns = CinemaScreenLayout.WallColumns(CinemaScreenLayout.Wall, 854, 480);

        for (var k = 0; k < columns.Count; k++)
        {
            var back = columns[k].Back;

            back.Bottom.Should().Be(14 + (back.X - CinemaScreenLayout.WALL_ROW_LEFT) / 2 - 32);
            CinemaScreenLayout.WallCell(back.Right - 1).Should().Be(columns[k].Cell);

            if (k > 0)
            {
                back.X.Should().Be(columns[k - 1].Back.X + 2);
                back.Y.Should().Be(columns[k - 1].Back.Y + 1);
            }
        }
    }

    [Test]
    public void Letterboxed_wall_picture_sits_in_the_middle()
    {
        var columns = CinemaScreenLayout.WallColumns(new CinemaWallSpot(0, 100, 10, 10), 10, 5);

        columns[0].Should().Be(new CinemaWallColumn(-15, new Rectangle(0, 90, 2, 10), new Rectangle(0, 92, 2, 5), new Rectangle(0, 0, 2, 5)));
        columns[4].Should().Be(new CinemaWallColumn(-14, new Rectangle(8, 94, 2, 10), new Rectangle(8, 96, 2, 5), new Rectangle(8, 0, 2, 5)));
    }

    [Test]
    public void Pillarboxed_wall_picture_leaves_dark_columns()
    {
        var columns = CinemaScreenLayout.WallColumns(new CinemaWallSpot(0, 100, 10, 10), 5, 10);

        columns[0].Picture.Should().Be(Rectangle.Empty);
        columns[1].Picture.Should().Be(new Rectangle(2, 91, 2, 10));
        columns[1].Source.Should().Be(new Rectangle(0, 0, 2, 10));
        columns[3].Picture.Should().Be(new Rectangle(6, 93, 1, 10));
        columns[3].Source.Should().Be(new Rectangle(4, 0, 1, 10));
        columns[4].Picture.Should().Be(Rectangle.Empty);
    }

    [Test]
    public void Wall_cell_is_drawn_in_the_next_stripe()
        => CinemaScreenLayout.StripeOf(3).Should().Be(4);

    [Test]
    public void Draw_stripe_inside_range_is_spot_depth()
        => CinemaScreenLayout.DrawStripe(10, 4, 30).Should().Be(10);

    [Test]
    public void Draw_stripe_above_view_is_first_stripe()
        => CinemaScreenLayout.DrawStripe(2, 4, 30).Should().Be(4);

    [Test]
    public void Draw_stripe_below_view_is_last_stripe()
        => CinemaScreenLayout.DrawStripe(40, 4, 30).Should().Be(30);
}
