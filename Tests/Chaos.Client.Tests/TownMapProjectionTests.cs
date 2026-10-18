using Chaos.Client.Controls.World.Popups;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class TownMapProjectionTests
{
    [Test]
    public void Matches_the_marker_math_for_a_middle_tile()
        => TownMapProjection.TileToPixel(-40, 30, 600, 300, 100, 80, 50, 40)
                            .Should()
                            .Be((259, 179));

    [Test]
    public void Matches_the_marker_math_for_the_origin_tile()
        => TownMapProjection.TileToPixel(-40, 30, 600, 300, 100, 80, 0, 0)
                            .Should()
                            .Be((226, 30));

    [Test]
    public void A_map_without_tiles_projects_nowhere()
    {
        TownMapProjection.TileToPixel(0, 0, 600, 300, 0, 80, 1, 1)
                         .Should()
                         .BeNull();

        TownMapProjection.TileToPixel(0, 0, 600, 300, 100, 0, 1, 1)
                         .Should()
                         .BeNull();
    }

    [Test]
    public void IsOnImage_holds_inside_and_fails_one_pixel_outside()
    {
        TownMapProjection.IsOnImage(-40, 30, 600, 300, (-40, 30)).Should().BeTrue();
        TownMapProjection.IsOnImage(-40, 30, 600, 300, (559, 329)).Should().BeTrue();
        TownMapProjection.IsOnImage(-40, 30, 600, 300, (-41, 100)).Should().BeFalse();
        TownMapProjection.IsOnImage(-40, 30, 600, 300, (560, 100)).Should().BeFalse();
        TownMapProjection.IsOnImage(-40, 30, 600, 300, (0, 29)).Should().BeFalse();
        TownMapProjection.IsOnImage(-40, 30, 600, 300, (0, 330)).Should().BeFalse();
    }
}
