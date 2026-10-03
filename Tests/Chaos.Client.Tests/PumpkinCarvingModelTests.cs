using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class PumpkinCarvingModelTests
{
    private static void Stroke(PumpkinCarvingModel model, params (int X, int Y)[] cells)
    {
        model.BeginStroke();

        foreach ((var x, var y) in cells)
            model.Apply(x, y, erase: false);

        model.EndStroke();
    }

    [Test]
    public void The_knife_cuts_and_mirror_cuts_the_matching_column()
    {
        var model = new PumpkinCarvingModel { Mirror = true };

        Stroke(model, (2, 5));

        PumpkinGrid.IsCut(model.Grid, 2, 5).Should().BeTrue();
        PumpkinGrid.IsCut(model.Grid, 19, 5).Should().BeTrue();
        model.CutCount.Should().Be(2);
        model.HasUnsent.Should().BeTrue();
    }

    [Test]
    public void The_eraser_and_a_right_click_fill_cells_back_in()
    {
        var model = new PumpkinCarvingModel();
        Stroke(model, (1, 1), (2, 1));

        model.Apply(1, 1, erase: true);
        model.Tool = PumpkinTool.Eraser;
        model.Apply(2, 1, erase: false);

        model.CutCount.Should().Be(0);
    }

    [Test]
    public void A_stroke_undoes_as_one_step_and_redo_restores_it()
    {
        var model = new PumpkinCarvingModel();
        Stroke(model, (0, 0), (1, 0), (2, 0));

        model.Undo();

        model.CutCount.Should().Be(0);
        model.CanRedo.Should().BeTrue();

        model.Redo();

        model.CutCount.Should().Be(3);

        model.Undo();
        Stroke(model, (5, 5));

        model.CanRedo.Should().BeFalse();
    }

    [Test]
    public void Clear_is_undoable()
    {
        var model = new PumpkinCarvingModel();
        Stroke(model, (3, 3));

        model.Clear();

        model.CutCount.Should().Be(0);

        model.Undo();

        model.CutCount.Should().Be(1);
    }

    [Test]
    public void Load_resets_history_and_the_unsent_flag()
    {
        var model = new PumpkinCarvingModel();
        Stroke(model, (3, 3));
        var saved = PumpkinGrid.Empty();
        PumpkinGrid.SetCut(saved, 9, 9, true);

        model.Load(saved);

        PumpkinGrid.IsCut(model.Grid, 9, 9).Should().BeTrue();
        model.CanUndo.Should().BeFalse();
        model.HasUnsent.Should().BeFalse();
    }

    [Test]
    public void Taking_the_grid_for_sending_copies_it_and_clears_the_flag()
    {
        var model = new PumpkinCarvingModel();
        Stroke(model, (4, 4));

        var sent = model.TakeForSend();
        sent[0] = 0xFF;

        model.HasUnsent.Should().BeFalse();
        model.CutCount.Should().Be(1);
    }

    [Test]
    public void Unsent_work_is_handed_over_once_and_nothing_when_all_was_sent()
    {
        var model = new PumpkinCarvingModel();

        model.TakeUnsent().Should().BeNull();

        Stroke(model, (4, 4));
        var unsent = model.TakeUnsent();

        unsent.Should().NotBeNull();
        PumpkinGrid.IsCut(unsent!, 4, 4).Should().BeTrue();
        model.TakeUnsent().Should().BeNull();
    }

    [Test]
    public void Cells_outside_the_grid_are_ignored()
    {
        var model = new PumpkinCarvingModel();

        Stroke(model, (-1, 0), (22, 0), (0, 14));

        model.CutCount.Should().Be(0);
        model.CanUndo.Should().BeFalse();
    }
}
