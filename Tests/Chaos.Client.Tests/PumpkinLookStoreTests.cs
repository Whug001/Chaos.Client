using Chaos.Client.Collections;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class PumpkinLookStoreTests
{
    [Test]
    public void Each_look_is_kept_by_id_with_a_new_version()
    {
        var store = new PumpkinLookStore();
        var grid = PumpkinGrid.Empty();
        PumpkinGrid.SetCut(grid, 2, 3, true);

        var blank = store.Apply(7, lit: false, grid: null);
        var lit = store.Apply(7, lit: true, grid: grid);

        store.Get(7).Should().Be(lit);
        lit.Version.Should().BeGreaterThan(blank.Version);
        PumpkinGrid.CountCut(blank.Grid).Should().Be(0);
        PumpkinGrid.IsCut(lit.Grid, 2, 3).Should().BeTrue();
        store.Get(8).Should().BeNull();
    }

    [Test]
    public void The_stored_grid_is_a_copy()
    {
        var store = new PumpkinLookStore();
        var grid = PumpkinGrid.Empty();
        PumpkinGrid.SetCut(grid, 0, 0, true);

        store.Apply(7, lit: true, grid: grid);
        grid[0] = 0;

        PumpkinGrid.IsCut(store.Get(7)!.Grid, 0, 0).Should().BeTrue();
    }

    [Test]
    public void Removal_and_clear_drop_looks()
    {
        var store = new PumpkinLookStore();
        store.Apply(7, lit: true, grid: PumpkinGrid.Empty());
        store.Apply(8, lit: true, grid: PumpkinGrid.Empty());

        store.Remove(7);

        store.Get(7).Should().BeNull();

        store.Clear();

        store.Get(8).Should().BeNull();
    }

    [Test]
    public void Only_a_lit_pumpkin_carries_a_light()
    {
        var store = new PumpkinLookStore();

        PumpkinLookStore.LanternFor(store.Apply(7, lit: true, grid: PumpkinGrid.Empty())).Should().Be(LanternSize.Small);
        PumpkinLookStore.LanternFor(store.Apply(8, lit: false, grid: null)).Should().Be(LanternSize.None);
        PumpkinLookStore.LanternFor(null).Should().Be(LanternSize.None);
    }
}
