#region
using Chaos.Client.Controls.World.Popups.KartRacing;
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
}
