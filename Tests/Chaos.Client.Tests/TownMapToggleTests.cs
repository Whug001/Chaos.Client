using Chaos.Client.Screens;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class TownMapToggleTests
{
    [Test]
    public async Task Visible_map_closes_even_on_a_no_town_map()
    {
        //the flag may have arrived while the map was open: toggling still closes it
        WorldScreen.DecideTownMapToggle(true, MapFlags.NoTownMap)
                   .Should()
                   .Be(WorldScreen.TownMapToggle.Close);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Visible_map_closes_normally()
    {
        WorldScreen.DecideTownMapToggle(true, MapFlags.None)
                   .Should()
                   .Be(WorldScreen.TownMapToggle.Close);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Hidden_map_stays_closed_on_a_no_town_map()
    {
        WorldScreen.DecideTownMapToggle(false, MapFlags.NoTownMap)
                   .Should()
                   .Be(WorldScreen.TownMapToggle.Ignore);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Hidden_map_opens_normally()
    {
        WorldScreen.DecideTownMapToggle(false, MapFlags.None)
                   .Should()
                   .Be(WorldScreen.TownMapToggle.Open);

        await Task.CompletedTask;
    }
}
