#region
using Chaos.Client.Controls.World.Popups.KartRacing;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using FluentAssertions;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Tests.Karting;

public class KartResultsControlTests
{
    [Test]
    public void Medal_places_are_gold_silver_and_bronze()
    {
        KartResultsControl.MedalColor(1).Should().Be(LegendColors.Gold);
        KartResultsControl.MedalColor(2).Should().Be(new Color(192, 192, 192));
        KartResultsControl.MedalColor(3).Should().Be(new Color(205, 127, 50));
        KartResultsControl.MedalColor(0).Should().Be(Color.White);
    }

    [Test]
    public void The_subtitle_counts_laps()
    {
        KartResultsControl.LapsText(1).Should().Be("1 lap");
        KartResultsControl.LapsText(3).Should().Be("3 laps");
    }

    [Test]
    public void The_place_column_fits_a_two_digit_ordinal()
        => KartResultsControl.PlaceLabelWidth.Should().BeGreaterThanOrEqualTo(TextRenderer.MeasureWidth("24th"));

    [Test]
    public void Results_window_centering_uses_the_current_viewport_dimensions()
    {
        KartResultsControl.CenteredPosition(640, 480, 400, 300).Should().Be(new Point(120, 90));
        KartResultsControl.CenteredPosition(800, 600, 400, 300).Should().Be(new Point(200, 150));
    }
}
