using Chaos.Client.Rendering;
using DALib.Data;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public class WetWallSimulationTests
{
    private static readonly Rectangle Everywhere = new(-4000, -4000, 12000, 12000);

    private static MapSurface Town(int size = 40)
    {
        var map = new MapFile(size, size);

        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                map.Tiles[x, y] = new MapTile { Background = 1, LeftForeground = (short)((y % 4 == 0) ? 100 : 0), RightForeground = (short)((y % 4 == 0) ? 100 : 0) };

        return MapSurface.Build(1, map, id => id == 100, _ => false, _ => false, _ => 60);
    }

    private static MapSurface Empty()
    {
        var map = new MapFile(4, 4);

        for (var y = 0; y < 4; y++)
            for (var x = 0; x < 4; x++)
                map.Tiles[x, y] = new MapTile { Background = 9 };

        return MapSurface.Build(1, map, _ => false, _ => false, id => id == 9, _ => 60);
    }

    private static WetWallSimulation Run(MapSurface? surface, int steps, bool spawning = true)
    {
        var simulation = new WetWallSimulation(new Random(5));

        for (var i = 0; i < steps; i++)
            simulation.Step(0.05f, Everywhere, surface, spawning);

        return simulation;
    }

    [Test]
    public void LongRun_StaysWithinEveryCap()
    {
        var simulation = Run(Town(), 4000);

        simulation.Drops.Count.Should().BeLessThanOrEqualTo(WetWallSimulation.MAX_DROPS);
        simulation.Streaks.Count.Should().BeLessThanOrEqualTo(WetWallSimulation.MAX_STREAKS);
        simulation.Beads.Count.Should().BeLessThanOrEqualTo(WetWallSimulation.MAX_BEADS);
        simulation.Rings.Count.Should().BeLessThanOrEqualTo(WetWallSimulation.MAX_RINGS);
        simulation.Splashes.Count.Should().BeLessThanOrEqualTo(WetWallSimulation.MAX_SPLASHES);
        simulation.Patches.Count.Should().BeLessThanOrEqualTo(WetWallSimulation.MAX_PATCHES);
    }

    [Test]
    public void Town_MakesDropsAndRings()
    {
        var simulation = Run(Town(), 200);

        simulation.Drops.Should().NotBeEmpty();
        simulation.Rings.Should().NotBeEmpty();
    }

    [Test]
    public void Drops_AlwaysStayAboveTheirWallFoot()
    {
        var simulation = new WetWallSimulation(new Random(9));
        var surface = Town();

        for (var i = 0; i < 1000; i++)
        {
            simulation.Step(0.05f, Everywhere, surface, true);
            simulation.Drops.Should().OnlyContain(drop => drop.Y < drop.Base);
        }
    }

    [Test]
    public void NullOrEmptySurface_MakesNothing()
    {
        Run(null, 200).Drops.Should().BeEmpty();
        Run(Empty(), 200).Rings.Should().BeEmpty();
    }

    [Test]
    public void NotSpawning_MakesNothing()
        => Run(Town(), 200, spawning: false).Drops.Should().BeEmpty();

    [Test]
    public void Clear_EmptiesEverything()
    {
        var simulation = Run(Town(), 200);

        simulation.Clear();

        simulation.Drops.Should().BeEmpty();
        simulation.Streaks.Should().BeEmpty();
        simulation.Rings.Should().BeEmpty();
        simulation.Beads.Should().BeEmpty();
        simulation.Splashes.Should().BeEmpty();
        simulation.Patches.Should().BeEmpty();
    }

    [Test]
    public void Renderer_DropsWaterWhenTheMapChanges()
    {
        using var renderer = new WetWallRenderer();
        renderer.SetSurface(Town());
        renderer.SetActive(true, immediate: true);

        for (var i = 0; i < 200; i++)
            renderer.Update(new GameTime(TimeSpan.FromSeconds(i), TimeSpan.FromSeconds(0.05)), new Rectangle(0, 0, 2000, 900), Vector2.Zero);

        renderer.Simulation.Drops.Should().NotBeEmpty();

        renderer.SetSurface(null);

        renderer.Simulation.Drops.Should().BeEmpty();
    }

    [Test]
    public void OffscreenWalls_MakeNoDrops()
    {
        var simulation = new WetWallSimulation(new Random(5));

        for (var i = 0; i < 200; i++)
            simulation.Step(0.05f, new Rectangle(90000, 90000, 640, 310), Town(), true);

        simulation.Drops.Should().BeEmpty();
    }
}
