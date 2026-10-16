using Chaos.Client.Controls.World.Popups.Exchange;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests.Exchange;

public sealed class ExchangePortraitTests
{
    private static readonly string Hash = new('a', 64);

    [Test]
    public void Added_exchange_items_carry_the_portrait_hash()
    {
        var exchange = new global::Chaos.Client.ViewModel.Exchange();
        exchange.AddItem(
            rightSide: true,
            exchangeIndex: 1,
            sprite: 100,
            color: DisplayColor.Default,
            name: "Painted Portrait",
            portraitHash: Hash,
            portraitPainter: "Tester");
        var item = exchange.GetItem(rightSide: true, index: 0).GetValueOrDefault();
        item.PortraitHash.Should().Be(Hash);
        item.PortraitPainter.Should().Be("Tester");
    }

    [Test]
    public void Items_without_a_portrait_carry_no_hash()
    {
        var exchange = new global::Chaos.Client.ViewModel.Exchange();
        exchange.AddItem(
            rightSide: false,
            exchangeIndex: 1,
            sprite: 200,
            color: DisplayColor.Default,
            name: "Ordinary Sword");
        var item = exchange.GetItem(rightSide: false, index: 0).GetValueOrDefault();
        item.PortraitHash.Should().BeNull();
        item.PortraitPainter.Should().BeNull();
        item.Name.Should().Be("Ordinary Sword");
    }

    [Test]
    public void Clearing_a_hovered_row_unreports_the_portrait_hover()
    {
        var row = new ExchangeItemControl();
        SeedPortrait(row, Hash, "Tester");

        var hovered = 0;
        var unhovered = 0;
        row.PortraitHovered += _ => hovered++;
        row.PortraitUnhovered += () => unhovered++;

        row.OnMouseEnter();
        hovered.Should().Be(1);

        row.ClearItem();
        unhovered.Should().Be(1);

        row.OnMouseLeave();
        unhovered.Should().Be(1);
    }

    [Test]
    public void Leaving_a_hovered_row_unreports_exactly_once()
    {
        var row = new ExchangeItemControl();
        SeedPortrait(row, Hash, "Tester");

        var unhovered = 0;
        row.PortraitUnhovered += () => unhovered++;

        row.OnMouseEnter();
        row.OnMouseLeave();
        unhovered.Should().Be(1);

        row.OnMouseLeave();
        unhovered.Should().Be(1);
    }

    [Test]
    public void Rows_without_a_portrait_report_nothing()
    {
        var row = new ExchangeItemControl();

        var hovered = 0;
        var unhovered = 0;
        row.PortraitHovered += _ => hovered++;
        row.PortraitUnhovered += () => unhovered++;

        row.OnMouseEnter();
        row.OnMouseLeave();
        row.ClearItem();

        hovered.Should().Be(0);
        unhovered.Should().Be(0);
    }

    private static void SeedPortrait(ExchangeItemControl row, string hash, string? painter)
    {
        var type = typeof(ExchangeItemControl);
        type.GetProperty(nameof(ExchangeItemControl.PortraitHash))!.GetSetMethod(nonPublic: true)!.Invoke(row, new object?[] { hash });
        type.GetProperty(nameof(ExchangeItemControl.PortraitPainter))!.GetSetMethod(nonPublic: true)!.Invoke(row, new object?[] { painter });
    }
}
