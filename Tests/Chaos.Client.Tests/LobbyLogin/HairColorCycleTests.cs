using Chaos.Client.Controls.LobbyLogin;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class HairColorCycleTests
{
    [Test]
    public void Offers_the_fourteen_swatch_colors_in_swatch_order()
        => HairColorCycle.Colors.Should()
                         .Equal(
                             DisplayColor.Teal,
                             DisplayColor.Green,
                             DisplayColor.Olive,
                             DisplayColor.Yellow,
                             DisplayColor.Pumpkin,
                             DisplayColor.Apple,
                             DisplayColor.Violet,
                             DisplayColor.Default,
                             DisplayColor.Navy,
                             DisplayColor.Blue,
                             DisplayColor.Gray,
                             DisplayColor.Carrot,
                             DisplayColor.Brown,
                             DisplayColor.Black);

    [Test]
    public void Steps_to_the_neighbours()
    {
        HairColorCycle.Step(DisplayColor.Default, 1).Should().Be(DisplayColor.Navy);
        HairColorCycle.Step(DisplayColor.Default, -1).Should().Be(DisplayColor.Violet);
    }

    [Test]
    public void Wraps_at_both_ends()
    {
        HairColorCycle.Step(DisplayColor.Black, 1).Should().Be(DisplayColor.Teal);
        HairColorCycle.Step(DisplayColor.Teal, -1).Should().Be(DisplayColor.Black);
    }

    [Test]
    public void Fourteen_steps_return_to_the_start()
    {
        var color = DisplayColor.Default;

        for (var i = 0; i < 14; i++)
            color = HairColorCycle.Step(color, 1);

        color.Should().Be(DisplayColor.Default);
    }

    [Test]
    public void A_color_outside_the_list_starts_at_an_end()
    {
        HairColorCycle.Step(DisplayColor.Pink, 1).Should().Be(DisplayColor.Teal);
        HairColorCycle.Step(DisplayColor.Pink, -1).Should().Be(DisplayColor.Black);
    }

    [Test]
    public void Direction_zero_keeps_the_color()
        => HairColorCycle.Step(DisplayColor.Navy, 0).Should().Be(DisplayColor.Navy);
}
