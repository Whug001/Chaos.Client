using Chaos.Client.Controls.World.Popups.Dye;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class DyeShopControlTextTests
{
    //formatter:off
    [Test]
    [Arguments(DyeShopRejectReason.NotNearShop, "Return to the tailor.")]
    [Arguments(DyeShopRejectReason.ItemGone, "That item is no longer in your pack.")]
    [Arguments(DyeShopRejectReason.InvalidColor, "The tailor can't do that colour.")]
    [Arguments(DyeShopRejectReason.NotEnoughGold, "You can't afford that.")]
    //formatter:on
    public async Task Each_reject_reason_has_its_own_text(DyeShopRejectReason reason, string expected)
    {
        DyeShopControl.RejectText(reason).Should().Be(expected);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Confirm_text_names_the_item_the_colour_and_the_price()
    {
        DyeShopControl.ConfirmText("Cotton Coat", DisplayColor.Apple, 5_000)
                      .Should()
                      .Be("Dye Cotton Coat Apple for 5,000 gold?");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Confirm_text_uses_the_ombre_name()
    {
        DyeShopControl.ConfirmText("Cotton Coat", (DisplayColor)84, 25_000)
                      .Should()
                      .Be("Dye Cotton Coat Wildfire for 25,000 gold?");

        await Task.CompletedTask;
    }
}
