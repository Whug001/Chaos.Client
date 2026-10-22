using Chaos.Client.Controls.World.Popups.GuildHall;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class GuildHallEditorStatusTests
{
    private static GuildHallPaletteSet LockedSet(string name, int price)
        => new(name.ToLowerInvariant(), name, false, price, [], [], []);

    [Test]
    public async Task Locked_set_row_shows_name_and_price_with_separators()
    {
        GuildHallEditorControl.LockedSetRowText(LockedSet("Chaos", 10000000)).Should().Be("Chaos 10,000,000");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Locked_set_price_uses_thousands_separators()
    {
        GuildHallEditorControl.LockedSetPriceText(10000000).Should().Be("10,000,000");
        GuildHallEditorControl.LockedSetPriceText(500).Should().Be("500");

        await Task.CompletedTask;
    }
}
