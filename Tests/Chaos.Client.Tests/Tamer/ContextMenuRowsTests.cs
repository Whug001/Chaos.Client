using Chaos.Client.ViewModel;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class ContextMenuRowsTests
{
    [Test]
    public void Each_of_five_rows_maps_from_its_y_range()
    {
        //the name row is 6 to 19; the options start 2 pixels lower, 14 pixels each
        for (var row = 0; row < 5; row++)
        {
            var top = 22 + (row * 14);

            ContextMenuRows.RowTop(row).Should().Be(top);
            ContextMenuRows.RowAt(top, 5).Should().Be(row);
            ContextMenuRows.RowAt(top + 7, 5).Should().Be(row);
            ContextMenuRows.RowAt(top + 13, 5).Should().Be(row);
        }
    }

    [Test]
    public void A_y_above_or_below_the_rows_maps_to_none()
    {
        ContextMenuRows.RowAt(-5, 5).Should().Be(-1);
        ContextMenuRows.RowAt(0, 5).Should().Be(-1);
        ContextMenuRows.RowAt(6, 5).Should().Be(-1);
        ContextMenuRows.RowAt(21, 5).Should().Be(-1);
        ContextMenuRows.RowAt(22 + (5 * 14), 5).Should().Be(-1);
        ContextMenuRows.RowAt(500, 5).Should().Be(-1);

        //the fifth row is not there in a four row menu
        ContextMenuRows.RowAt(22 + (4 * 14), 4).Should().Be(-1);
        ContextMenuRows.RowAt(22, 0).Should().Be(-1);
    }

    [Test]
    public void The_frame_height_grows_by_one_band_per_extra_row()
    {
        //the art fits three rows
        ContextMenuRows.FrameHeight(70, 3).Should().Be(70);
        ContextMenuRows.FrameHeight(70, 4).Should().Be(84);
        ContextMenuRows.FrameHeight(70, 5).Should().Be(98);
        ContextMenuRows.FrameHeight(70, 2).Should().Be(70);
        ContextMenuRows.ExtraBands(5).Should().Be(2);
        ContextMenuRows.ExtraBands(1).Should().Be(0);
    }

    [Test]
    public void The_taller_frame_repeats_the_middle_band_and_then_moves_the_rest_down()
    {
        //rows above the third option's end stay put
        ContextMenuRows.SourceRow(0, 2).Should().Be(0);
        ContextMenuRows.SourceRow(63, 2).Should().Be(63);

        //each added band is the third option's 14 rows again
        ContextMenuRows.SourceRow(64, 2).Should().Be(50);
        ContextMenuRows.SourceRow(77, 2).Should().Be(63);
        ContextMenuRows.SourceRow(78, 2).Should().Be(50);
        ContextMenuRows.SourceRow(91, 2).Should().Be(63);

        //what was under the third option follows, moved down by the bands
        ContextMenuRows.SourceRow(92, 2).Should().Be(64);
        ContextMenuRows.SourceRow(97, 2).Should().Be(69);
    }

    [Test]
    public void One_extra_band_copies_the_rows_the_four_row_menu_always_copied()
    {
        for (var y = 0; y < 84; y++)
            ContextMenuRows.SourceRow(y, 1).Should().Be(y < 64 ? y : y - 14);
    }

    [Test]
    public void Every_row_of_the_taller_frame_reads_from_inside_the_art()
    {
        const int SOURCE_HEIGHT = 70;

        for (var y = 0; y < ContextMenuRows.FrameHeight(SOURCE_HEIGHT, 5); y++)
            ContextMenuRows.SourceRow(y, 2).Should().BeInRange(0, SOURCE_HEIGHT - 1);
    }

    [Test]
    public void The_tamer_bout_line_is_ascii_and_fits_its_row()
    {
        ContextMenuRows.TamerBout.Should().Be("tamer bout");
        TamerDrawableText.IsPrintableAscii(ContextMenuRows.TamerBout).Should().BeTrue();
        (ContextMenuRows.TamerBout.Length * 6).Should().BeLessThanOrEqualTo(ContextMenuRows.ROW_WIDTH);
    }
}
