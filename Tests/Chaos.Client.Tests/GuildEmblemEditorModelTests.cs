using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class GuildEmblemEditorModelTests
{
    [Test]
    public void Pencil_paints_the_selected_color_and_mirror_paints_the_matching_column()
    {
        var model = new GuildEmblemEditorModel { Mirror = true };

        model.Apply(2, 5);

        model.CellAt(2, 5).Should().Be(1);
        model.CellAt(8, 5).Should().Be(1);
        model.IsDirty.Should().BeTrue();
    }

    [Test]
    public void See_through_erases()
    {
        var model = new GuildEmblemEditorModel();
        model.Apply(0, 0);

        model.SelectColor(0);
        model.Apply(0, 0);

        model.CellAt(0, 0).Should().Be(0);
        model.Design.HasPaint().Should().BeFalse();
    }

    [Test]
    public void Fill_colors_the_connected_area_only()
    {
        var model = new GuildEmblemEditorModel();
        model.AddColor(new GuildCloakColor(140, 20, 30));
        model.SelectColor(1);

        for (var y = 0; y < GuildEmblemProtocol.SIZE; y++)
            model.Apply(5, y);

        model.SelectColor(2);
        model.Tool = GuildCloakTool.Fill;
        model.Apply(0, 0);

        model.CellAt(4, 10).Should().Be(2);
        model.CellAt(5, 3).Should().Be(1);
        model.CellAt(6, 0).Should().Be(0);
    }

    [Test]
    public void A_stroke_undoes_as_one_step_and_redo_brings_it_back()
    {
        var model = new GuildEmblemEditorModel();

        model.BeginStroke();
        model.Apply(1, 1);
        model.Apply(2, 1);
        model.EndStroke();

        model.Undo();
        model.Design.HasPaint().Should().BeFalse();

        model.Redo();
        model.CellAt(1, 1).Should().Be(1);
        model.CellAt(2, 1).Should().Be(1);
    }

    [Test]
    public void Pick_selects_the_cell_color_including_see_through()
    {
        var model = new GuildEmblemEditorModel();
        model.Apply(3, 3);
        model.Tool = GuildCloakTool.Pick;

        model.Apply(4, 4);
        model.SelectedColor.Should().Be(0);

        model.Apply(3, 3);
        model.SelectedColor.Should().Be(1);
    }

    [Test]
    public void LoadSaved_keeps_unsaved_painting_while_the_window_is_open()
    {
        var model = new GuildEmblemEditorModel();
        model.Apply(0, 0);

        model.LoadSaved(GuildEmblemDesign.CreateDefault(), true).Should().BeFalse();
        model.CellAt(0, 0).Should().Be(1);

        model.LoadSaved(GuildEmblemDesign.CreateDefault(), false).Should().BeTrue();
        model.CellAt(0, 0).Should().Be(0);
        model.IsDirty.Should().BeFalse();
    }
}
