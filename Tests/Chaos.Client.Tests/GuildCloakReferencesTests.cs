using Chaos.Client.Rendering;
using DALib.Drawing;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class GuildCloakReferencesTests
{
    private static EpfFrame Frame(int left, int top, int width, int height, params byte[] data)
        => new()
        {
            Left = (short)left,
            Top = (short)top,
            Right = (short)(left + width),
            Bottom = (short)(top + height),
            Data = data
        };

    /// <summary>
    ///     A 3x3 lining at (0, 0) and a 2x2 collar at (1, 0) with its bottom-right pixel empty, so the collar hides lining
    ///     cells (1, 0), (2, 0) and (1, 1). <paramref name="lining" /> is the lining frame's pixels.
    /// </summary>
    private static GuildCloakReferences References(params byte[] lining)
        => new(
            Frame(0, 0, 1, 1, 25),
            Frame(0, 0, 3, 3, lining),
            Frame(1, 0, 2, 2, 25, 25, 25, 0),
            null);

    [Test]
    public void Hidden_lining_cells_copy_the_nearest_visible_cell_below()
    {
        var references = References(25, 25, 25, 25, 25, 25, 25, 25, 25);

        byte[] cells =
        [
            7, 7, 7,
            7, 7, 8,
            1, 2, 3
        ];

        references.FillHiddenLining(cells);

        cells.Should()
             .Equal(
                 7, 2, 8,
                 7, 2, 8,
                 1, 2, 3);
    }

    [Test]
    public void A_hidden_cell_with_no_visible_lining_below_keeps_its_color()
    {
        //the lining's middle column has only its top pixel, which the collar hides
        var references = References(25, 25, 25, 25, 0, 25, 25, 0, 25);

        byte[] cells =
        [
            7, 7, 7,
            7, 7, 8,
            1, 2, 3
        ];

        references.FillHiddenLining(cells);

        cells[1].Should().Be(7);
        cells[2].Should().Be(8);
    }
}
