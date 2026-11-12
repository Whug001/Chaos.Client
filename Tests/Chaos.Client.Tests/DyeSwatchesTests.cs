using Chaos.Client.Controls.World.Popups.Beauty;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class DyeSwatchesTests
{
    private static readonly IReadOnlyList<DisplayColor> Colors = [DisplayColor.Default, DisplayColor.Apple, (DisplayColor)72, (DisplayColor)77];

    [Test]
    public async Task Modern_dye_table_shows_catalog_ombres()
    {
        DyeSwatches.VisibleOmbres(Colors, _ => true).Should().Equal((DisplayColor)72, (DisplayColor)77);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Old_dye_table_hides_ombres()
    {
        DyeSwatches.VisibleOmbres(Colors, _ => false).Should().BeEmpty();
        DyeSwatches.VisibleOmbres(Colors, i => i == 72).Should().BeEmpty();
        DyeSwatches.VisibleOmbres(Colors, i => i == 200).Should().BeEmpty();

        await Task.CompletedTask;
    }

    [Test]
    public async Task No_catalog_ombres_shows_nothing()
    {
        DyeSwatches.VisibleOmbres([DisplayColor.Default, DisplayColor.Apple], _ => true).Should().BeEmpty();

        await Task.CompletedTask;
    }
}
