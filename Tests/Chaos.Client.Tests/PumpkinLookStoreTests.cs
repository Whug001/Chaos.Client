using Chaos.Client.Collections;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class PumpkinLookStoreTests
{
    [Test]
    public void Each_look_is_kept_by_id_with_its_state()
    {
        var store = new PumpkinLookStore();
        var grid = PumpkinGrid.Empty();
        PumpkinGrid.SetCut(grid, 2, 3, true);

        var blank = store.Apply(7, PumpkinLookState.Blank, null);
        var carving = store.Apply(7, PumpkinLookState.Carving, grid);

        store.Get(7).Should().Be(carving);
        carving.EntityId.Should().Be(7);
        carving.State.Should().Be(PumpkinLookState.Carving);
        carving.Lit.Should().BeFalse();
        PumpkinGrid.CountCut(blank.Grid).Should().Be(0);
        PumpkinGrid.IsCut(carving.Grid, 2, 3).Should().BeTrue();
        store.Get(8).Should().BeNull();
    }

    [Test]
    public void The_stored_grid_is_a_copy()
    {
        var store = new PumpkinLookStore();
        var grid = PumpkinGrid.Empty();
        PumpkinGrid.SetCut(grid, 0, 0, true);

        store.Apply(7, PumpkinLookState.Lit, grid);
        grid[0] = 0;

        PumpkinGrid.IsCut(store.Get(7)!.Grid, 0, 0).Should().BeTrue();
    }

    [Test]
    public void Removal_and_clear_drop_looks()
    {
        var store = new PumpkinLookStore();
        store.Apply(7, PumpkinLookState.Lit, PumpkinGrid.Empty());
        store.Apply(8, PumpkinLookState.Carving, PumpkinGrid.Empty());

        store.Remove(7);

        store.Get(7).Should().BeNull();

        store.Clear();

        store.Get(8).Should().BeNull();
    }

    [Test]
    public void Only_a_lit_pumpkin_carries_a_light()
    {
        var store = new PumpkinLookStore();

        PumpkinLookStore.LanternFor(store.Apply(7, PumpkinLookState.Lit, PumpkinGrid.Empty())).Should().Be(LanternSize.Small);
        PumpkinLookStore.LanternFor(store.Apply(8, PumpkinLookState.Carving, PumpkinGrid.Empty())).Should().Be(LanternSize.None);
        PumpkinLookStore.LanternFor(store.Apply(9, PumpkinLookState.Blank, null)).Should().Be(LanternSize.None);
        PumpkinLookStore.LanternFor(null).Should().Be(LanternSize.None);
    }
}
