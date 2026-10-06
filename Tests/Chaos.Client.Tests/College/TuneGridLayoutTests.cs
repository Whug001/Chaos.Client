using Chaos.Client.Controls.World.Popups.College;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class TuneGridLayoutTests
{
    private static readonly TuneGridLayout Composer = TuneGridLayout.Composer;

    [Test]
    public void The_composer_grid_is_530_by_251()
    {
        Composer.Width.Should().Be(530);
        Composer.Height.Should().Be(251);
        Composer.LayerTop(TuneLayer.Melody).Should().Be(14);
        Composer.LayerTop(TuneLayer.Bass).Should().Be(122);
        Composer.LayerTop(TuneLayer.Drums).Should().Be(230);
    }

    [Test]
    public void The_reader_grid_is_384_by_138()
    {
        TuneGridLayout.Reader.Width.Should().Be(384);
        TuneGridLayout.Reader.Height.Should().Be(138);
        TuneGridLayout.Reader.LayerTop(TuneLayer.Melody).Should().Be(0);
    }

    [Test]
    public void The_top_left_melody_cell_is_its_highest_row()
    {
        Composer.TryHitCell(18, 14, out var layer, out var row, out var column).Should().BeTrue();

        layer.Should().Be(TuneLayer.Melody);
        row.Should().Be(14);
        column.Should().Be(0);
    }

    [Test]
    public void The_bottom_right_drum_cell_is_row_0_column_63()
    {
        Composer.TryHitCell(529, 250, out var layer, out var row, out var column).Should().BeTrue();

        layer.Should().Be(TuneLayer.Drums);
        row.Should().Be(0);
        column.Should().Be(63);
    }

    [Test]
    public void The_label_column_the_beat_bar_and_the_gaps_are_not_cells()
    {
        Composer.TryHitCell(17, 20, out _, out _, out _).Should().BeFalse();
        Composer.TryHitCell(100, 5, out _, out _, out _).Should().BeFalse();
        Composer.TryHitCell(100, 120, out _, out _, out _).Should().BeFalse();
        Composer.TryHitCell(100, 228, out _, out _, out _).Should().BeFalse();
        Composer.TryHitCell(530, 20, out _, out _, out _).Should().BeFalse();
    }

    [Test]
    public void Row_tops_count_down_from_the_top_row()
    {
        Composer.RowTop(TuneLayer.Melody, 14).Should().Be(14);
        Composer.RowTop(TuneLayer.Bass, 0).Should().Be(122 + (14 * 7));
        Composer.RowTop(TuneLayer.Drums, 2).Should().Be(230);
    }

    [Test]
    public void The_beat_bar_names_bars_0_to_7()
    {
        Composer.BarAt(18, 0).Should().Be(0);
        Composer.BarAt(18 + (7 * 64), 11).Should().Be(7);
        Composer.BarAt(529, 5).Should().Be(7);
        Composer.BarAt(18, 12).Should().BeNull();
        Composer.BarAt(10, 5).Should().BeNull();
        TuneGridLayout.Reader.BarAt(0, 0).Should().BeNull();
    }

    [Test]
    public void Columns_for_a_drag_stay_inside_the_grid()
    {
        Composer.ColumnAt(0).Should().Be(0);
        Composer.ColumnAt(18 + 8 * 10 + 7).Should().Be(10);
        Composer.ColumnAt(10_000).Should().Be(63);
    }
}
