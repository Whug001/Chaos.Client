using Chaos.Client.Rendering;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class GuildCloakEditorModelTests
{
    /// <summary>Every cell is inside the outline except the back's top-left corner.</summary>
    private static GuildCloakEditorModel Model() => new((part, x, y) => !((part == GuildCloakPart.Back) && (x == 0) && (y == 0)));

    /// <summary>A model with a second color (red), which is selected.</summary>
    private static GuildCloakEditorModel TwoColors()
    {
        var model = Model();
        model.AddColor(new GuildCloakColor(200, 0, 0)).Should().BeTrue();

        return model;
    }

    [Test]
    public void Pencil_paints_one_cell_with_the_selected_color()
    {
        var model = TwoColors();

        model.Apply(GuildCloakPart.Back, 5, 5);

        model.CellAt(GuildCloakPart.Back, 5, 5).Should().Be(2);
        model.CellAt(GuildCloakPart.Back, 6, 5).Should().Be(1);
        model.IsDirty.Should().BeTrue();
    }

    [Test]
    public void Cells_outside_the_outline_or_the_grid_are_never_painted()
    {
        var model = Model();
        model.AddColor(new GuildCloakColor(200, 0, 0));
        model.Load(model.Design);
        model.SelectColor(2);

        model.Apply(GuildCloakPart.Back, 0, 0);
        model.Apply(GuildCloakPart.Back, -1, 3);
        model.Apply(GuildCloakPart.Back, GuildCloakProtocol.BACK_WIDTH, 3);

        model.CellAt(GuildCloakPart.Back, 0, 0).Should().Be(1);
        model.CanUndo.Should().BeFalse();
    }

    [Test]
    public void Mirror_paints_the_matching_cell_on_the_other_half()
    {
        var model = TwoColors();
        model.Mirror = true;

        model.Apply(GuildCloakPart.Collar, 2, 3);

        model.CellAt(GuildCloakPart.Collar, 2, 3).Should().Be(2);
        model.CellAt(GuildCloakPart.Collar, GuildCloakProtocol.COLLAR_WIDTH - 3, 3).Should().Be(2);
    }

    [Test]
    public void Fill_changes_only_the_connected_cells_of_one_color()
    {
        var model = TwoColors();
        model.BeginStroke();

        for (var y = 0; y < GuildCloakProtocol.COLLAR_HEIGHT; y++)
            model.Apply(GuildCloakPart.Collar, 4, y);

        model.EndStroke();
        model.AddColor(new GuildCloakColor(0, 0, 200));
        model.Tool = GuildCloakTool.Fill;

        model.Apply(GuildCloakPart.Collar, 0, 0);

        model.CellAt(GuildCloakPart.Collar, 3, 7).Should().Be(3);
        model.CellAt(GuildCloakPart.Collar, 4, 0).Should().Be(2);
        model.CellAt(GuildCloakPart.Collar, 5, 0).Should().Be(1);
    }

    [Test]
    public void Pick_selects_the_color_of_a_cell()
    {
        var model = TwoColors();
        model.Apply(GuildCloakPart.Back, 5, 5);
        model.SelectColor(1);
        model.Tool = GuildCloakTool.Pick;

        model.Apply(GuildCloakPart.Back, 5, 5);

        model.SelectedColor.Should().Be(2);
    }

    [Test]
    public void A_stroke_undoes_as_one_step_and_redo_brings_it_back()
    {
        var model = TwoColors();
        model.BeginStroke();
        model.Apply(GuildCloakPart.Back, 5, 5);
        model.Apply(GuildCloakPart.Back, 6, 5);
        model.EndStroke();

        model.Undo();

        model.CellAt(GuildCloakPart.Back, 5, 5).Should().Be(1);
        model.CellAt(GuildCloakPart.Back, 6, 5).Should().Be(1);
        model.Design.Colors.Should().HaveCount(2);

        model.Redo();

        model.CellAt(GuildCloakPart.Back, 5, 5).Should().Be(2);
        model.CellAt(GuildCloakPart.Back, 6, 5).Should().Be(2);
    }

    [Test]
    public void A_color_drag_undoes_as_one_step()
    {
        var model = TwoColors();
        model.BeginStroke();
        model.SetColor(2, new GuildCloakColor(10, 10, 10));
        model.SetColor(2, new GuildCloakColor(20, 20, 20));
        model.EndStroke();

        model.Undo();

        model.Design.Colors[1].Should().Be(new GuildCloakColor(200, 0, 0));
    }

    [Test]
    public void Undo_keeps_at_most_fifty_steps()
    {
        var model = TwoColors();

        for (var i = 0; i < 60; i++)
        {
            model.SelectColor(i % 2 == 0 ? 1 : 2);
            model.Apply(GuildCloakPart.Back, 5, 5);
        }

        var undone = 0;

        while (model.CanUndo)
        {
            model.Undo();
            undone++;
        }

        undone.Should().Be(GuildCloakEditorModel.MAX_UNDO);
    }

    [Test]
    public void Six_colors_is_the_limit()
    {
        var model = Model();

        for (var i = 0; i < 5; i++)
            model.AddColor(new GuildCloakColor(1, 2, 3)).Should().BeTrue();

        model.AddColor(new GuildCloakColor(1, 2, 3)).Should().BeFalse();
        model.Design.Colors.Should().HaveCount(GuildCloakProtocol.MAX_COLORS);
    }

    [Test]
    public void Load_resets_the_history_the_selection_and_the_dirty_flag()
    {
        var model = TwoColors();
        model.Apply(GuildCloakPart.Back, 5, 5);

        model.Load(GuildCloakDesign.CreateDefault());

        model.IsDirty.Should().BeFalse();
        model.CanUndo.Should().BeFalse();
        model.SelectedColor.Should().Be(1);
        model.Design.Colors.Should().HaveCount(1);
    }

    /// <summary>A model whose only hidden lining cell is (0, 0), filled from the cell below it.</summary>
    private static GuildCloakEditorModel HiddenCorner()
        => new((_, _, _) => true, lining => lining[0] = lining[GuildCloakProtocol.LINING_WIDTH]);

    [Test]
    public void A_change_refills_the_hidden_lining()
    {
        var model = HiddenCorner();
        model.AddColor(new GuildCloakColor(200, 0, 0));

        model.Apply(GuildCloakPart.Lining, 0, 1);

        model.CellAt(GuildCloakPart.Lining, 0, 0).Should().Be(2);
    }

    [Test]
    public void Load_refills_the_hidden_lining_without_marking_a_change()
    {
        var model = HiddenCorner();
        var design = GuildCloakDesign.CreateDefault();
        design.Colors.Add(new GuildCloakColor(200, 0, 0));
        design.Lining[GuildCloakProtocol.LINING_WIDTH] = 2;

        model.Load(design);

        model.CellAt(GuildCloakPart.Lining, 0, 0).Should().Be(2);
        model.IsDirty.Should().BeFalse();
        design.Lining[0].Should().Be(1);
    }

    [Test]
    public void The_painted_design_is_always_valid()
    {
        var model = TwoColors();
        model.Tool = GuildCloakTool.Fill;

        model.Apply(GuildCloakPart.Lining, 3, 3);

        model.Design.IsValid().Should().BeTrue();
    }

    [Test]
    [Arguments(GuildCloakStatus.Draft, "", false, "Draft")]
    [Arguments(GuildCloakStatus.Waiting, "", true, "Waiting for review - unsaved changes")]
    [Arguments(GuildCloakStatus.Approved, "", false, "Approved")]
    [Arguments(GuildCloakStatus.Rejected, "Too bright.", false, "Rejected: Too bright.")]
    public void StatusText_matches_the_spec(GuildCloakStatus status, string reason, bool dirty, string expected)
        => GuildCloakEditorModel.StatusText(status, reason, dirty).Should().Be(expected);
}
