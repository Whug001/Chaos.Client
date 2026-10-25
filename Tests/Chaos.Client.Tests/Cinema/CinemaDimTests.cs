using Chaos.Client.Systems.Cinema;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests.Cinema;

public sealed class CinemaDimTests
{
    [Test]
    public void House_darkness_is_sixty_percent()
        => CinemaScreenLayout.CINEMA_HOUSE_DARKNESS.Should().Be(0.6f);

    [Test]
    public void No_holes_covers_the_viewport()
    {
        var viewport = new Rectangle(10, 20, 100, 80);

        CinemaScreenLayout.DimAroundColumns(viewport, []).Should().Equal(viewport);
    }

    [Test]
    public void One_column_in_the_middle_leaves_a_hole()
    {
        var viewport = new Rectangle(0, 0, 100, 80);
        var columns = new List<Rectangle> { new(40, 20, 20, 40) };

        CinemaScreenLayout.DimAroundColumns(viewport, columns)
                          .Should()
                          .Equal(
                              new Rectangle(0, 0, 40, 80),
                              new Rectangle(60, 0, 40, 80),
                              new Rectangle(40, 0, 20, 20),
                              new Rectangle(40, 60, 20, 20));
    }

    [Test]
    public void Slanted_columns_leave_a_slanted_hole()
    {
        var viewport = new Rectangle(0, 0, 100, 100);
        var columns = new List<Rectangle> { new(10, 5, 2, 50), new(12, 6, 2, 50) };

        CinemaScreenLayout.DimAroundColumns(viewport, columns)
                          .Should()
                          .Equal(
                              new Rectangle(0, 0, 10, 100),
                              new Rectangle(14, 0, 86, 100),
                              new Rectangle(10, 0, 2, 5),
                              new Rectangle(10, 55, 2, 45),
                              new Rectangle(12, 0, 2, 6),
                              new Rectangle(12, 56, 2, 44));
    }

    [Test]
    public void Column_partly_off_the_viewport_edge_still_works()
    {
        var viewport = new Rectangle(0, 0, 100, 80);
        var columns = new List<Rectangle> { new(-10, 10, 20, 60), new(10, 12, 20, 60) };

        CinemaScreenLayout.DimAroundColumns(viewport, columns)
                          .Should()
                          .Equal(
                              new Rectangle(30, 0, 70, 80),
                              new Rectangle(0, 0, 10, 10),
                              new Rectangle(0, 70, 10, 10),
                              new Rectangle(10, 0, 20, 12),
                              new Rectangle(10, 72, 20, 8));
    }

    [Test]
    public void Column_wholly_outside_does_not_punch_a_hole()
    {
        var viewport = new Rectangle(0, 0, 100, 80);
        var columns = new List<Rectangle> { new(200, 10, 20, 60) };

        CinemaScreenLayout.DimAroundColumns(viewport, columns).Should().Equal(viewport);
    }

    [Test]
    public void Dim_plus_holes_covers_the_viewport_exactly_once()
    {
        var viewport = new Rectangle(0, 0, 14, 10);
        var columns = new List<Rectangle>
        {
            new(-2, 3, 2, 4),
            new(0, 4, 2, 4),
            new(2, 1, 2, 6),
            new(4, 2, 2, 6),
            new(6, 3, 2, 6),
            new(8, 4, 2, 6),
            new(10, 5, 2, 6),
            new(12, 2, 2, 4)
        };

        var dim = CinemaScreenLayout.DimAroundColumns(viewport, columns);

        for (var y = 0; y < viewport.Height; y++)
            for (var x = 0; x < viewport.Width; x++)
            {
                var inDim = dim.Count(rect => rect.Contains(x, y));
                var inHole = columns.Count(column => column.Contains(x, y));

                (inDim + inHole).Should().Be(1, $"pixel ({x}, {y}) must be dimmed or hole, exactly once");
            }
    }
}
