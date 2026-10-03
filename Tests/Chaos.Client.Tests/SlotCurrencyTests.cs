using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class SlotCurrencyTests
{
    private static SlotMachineDisplayArgs CandyOpen()
        => new()
        {
            Type = SlotDisplayType.Open,
            MachineName = "Jack's Candy Reels",
            Bet = 1,
            CurrencyName = "candy",
            CurrencyItemName = "Halloween Candy",
            HasJackpot = false
        };

    [Test]
    public async Task ApplyOpen_stores_the_currency_fields()
    {
        var slots = new SlotMachine();

        slots.ApplyOpen(CandyOpen());

        slots.CurrencyName.Should().Be("candy");
        slots.CurrencyItemName.Should().Be("Halloween Candy");
        slots.HasJackpot.Should().BeFalse();
        slots.UsesItemCurrency.Should().BeTrue();

        await Task.CompletedTask;
    }

    [Test]
    public async Task A_gold_machine_has_no_item_currency()
    {
        var slots = new SlotMachine();

        slots.ApplyOpen(new SlotMachineDisplayArgs { Type = SlotDisplayType.Open, MachineName = "Copper Wheel", Bet = 1000 });

        slots.UsesItemCurrency.Should().BeFalse();
        slots.HasJackpot.Should().BeTrue();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Clear_resets_the_currency_fields()
    {
        var slots = new SlotMachine();
        slots.ApplyOpen(CandyOpen());

        slots.Clear();

        slots.CurrencyName.Should().BeEmpty();
        slots.CurrencyItemName.Should().BeEmpty();
        slots.HasJackpot.Should().BeTrue();

        await Task.CompletedTask;
    }

    [Test]
    public async Task CountOf_sums_matching_stacks_only()
    {
        var inventory = new Inventory();
        inventory.SetSlot(1, 10, DisplayColor.Default, "Halloween Candy", true, 30, 0, 0);
        inventory.SetSlot(2, 11, DisplayColor.Default, "Apple", true, 5, 0, 0);
        inventory.SetSlot(3, 10, DisplayColor.Default, "Halloween Candy", true, 12, 0, 0);
        inventory.SetSlot(4, 12, DisplayColor.Default, "Halloween Candy Bag", false, 1, 0, 0);

        inventory.CountOf("Halloween Candy").Should().Be(42);

        await Task.CompletedTask;
    }
}
