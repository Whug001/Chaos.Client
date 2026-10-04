using Chaos.Client.Controls.World.Popups.Beauty;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class ThumbnailGridTests
{
    [Test]
    public async Task Default_grid_keeps_its_arrows_and_32_px_cells()
    {
        ThumbnailGrid<int>.WidthFor(8).Should().Be(315);
        ThumbnailGrid<int>.HeightFor(2).Should().Be(67);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Backdrop_row_is_188_by_28_without_arrows()
    {
        ThumbnailGrid<int>.WidthFor(6, 4, 28, arrows: false).Should().Be(188);
        ThumbnailGrid<int>.HeightFor(1, 4, 28).Should().Be(28);

        await Task.CompletedTask;
    }
}
