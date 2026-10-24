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
    public void Hall_spot_depth_is_anchor_sum()
        => CinemaScreenLayout.Hall.Depth.Should().Be(CinemaScreenLayout.Hall.AnchorX + CinemaScreenLayout.Hall.AnchorY);

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
