using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class DyeShopViewModelTests
{
    private static DyeShopItem Item(ulong id, DisplayColor color = DisplayColor.Default)
        => new()
        {
            Slot = (byte)(id % 60),
            UniqueId = id,
            Name = $"Item {id}",
            PanelSprite = 1,
            Color = color,
            Gender = Gender.Male
        };

    private static DyeShopDisplayArgs Open(int itemCount = 3, int gold = 60_000)
        => new()
        {
            Type = DyeShopDisplayType.Open,
            Gender = Gender.Male,
            HairStyle = 2,
            HairColor = DisplayColor.Default,
            Gold = gold,
            DyePrice = 5_000,
            OmbreDyePrice = 25_000,
            Colors = [DisplayColor.Apple, DisplayColor.Blue],
            OmbreColors = [(DisplayColor)72],
            Items = Enumerable.Range(1, itemCount).Select(i => Item((ulong)i)).ToList()
        };

    private static DyeShop Ready(int itemCount = 3, int gold = 60_000)
    {
        var shop = new DyeShop();
        shop.ApplyOpen(Open(itemCount, gold));

        return shop;
    }

    [Test]
    public async Task Open_loads_the_packet()
    {
        var shop = Ready();

        shop.IsOpen.Should().BeTrue();
        shop.Gender.Should().Be(Gender.Male);
        shop.HairStyle.Should().Be(2);
        shop.Gold.Should().Be(60_000);
        shop.DyePrice.Should().Be(5_000);
        shop.OmbreDyePrice.Should().Be(25_000);
        shop.Colors.Should().HaveCount(2);
        shop.OmbreColors.Should().HaveCount(1);
        shop.Items.Should().HaveCount(3);
        shop.PageCount.Should().Be(1);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Refresh_keeps_the_selected_item_by_unique_id_and_clears_the_colour()
    {
        var shop = Ready();
        shop.SelectItem(shop.Items[1]);
        shop.SelectColor(DisplayColor.Apple);
        shop.HoveredColor = DisplayColor.Blue;
        shop.ApplyRejected(DyeShopRejectReason.ItemGone);
        shop.MarkSent();

        var refreshed = Open();
        refreshed.Items = [Item(2, DisplayColor.Blue), Item(5)];
        shop.ApplyOpen(refreshed);

        shop.SelectedItem.Should().BeSameAs(refreshed.Items[0]);
        shop.SelectedColor.Should().BeNull();
        shop.HoveredColor.Should().BeNull();
        shop.LastRejectReason.Should().BeNull();
        shop.AwaitingReply.Should().BeFalse();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Refresh_without_the_selected_item_clears_the_selection()
    {
        var shop = Ready();
        shop.SelectItem(shop.Items[0]);
        shop.SelectColor(DisplayColor.Apple);

        var refreshed = Open();
        refreshed.Items = [Item(9)];
        shop.ApplyOpen(refreshed);

        shop.SelectedItem.Should().BeNull();
        shop.SelectedColor.Should().BeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Refresh_that_shrinks_the_list_clamps_the_page()
    {
        var shop = Ready(13);
        shop.PageCount.Should().Be(2);
        shop.StepPage(1);
        shop.Page.Should().Be(1);
        shop.SelectItem(shop.Items[12]);

        shop.ApplyOpen(Open(3));

        shop.Page.Should().Be(0);
        shop.VisibleItems.Should().HaveCount(3);
        shop.SelectedItem.Should().BeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task PreviewColor_prefers_hover_then_selection_then_the_item()
    {
        var shop = Ready();
        var item = Item(1, DisplayColor.Blue);
        shop.SelectItem(item);

        shop.PreviewColor.Should().Be(DisplayColor.Blue);

        shop.SelectColor(DisplayColor.Apple);
        shop.PreviewColor.Should().Be(DisplayColor.Apple);

        shop.HoveredColor = DisplayColor.Default;
        shop.PreviewColor.Should().Be(DisplayColor.Default);

        await Task.CompletedTask;
    }

    [Test]
    public async Task PreviewColor_is_null_with_no_item()
    {
        var shop = Ready();
        shop.HoveredColor = DisplayColor.Apple;

        shop.PreviewColor.Should().BeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task CostOf_is_the_ombre_price_for_an_ombre()
    {
        var shop = Ready();

        shop.CostOf((DisplayColor)72).Should().Be(25_000);
        shop.CostOf(DisplayColor.Apple).Should().Be(5_000);

        await Task.CompletedTask;
    }

    [Test]
    public async Task CanDye_needs_an_item()
    {
        var shop = Ready();

        shop.CanDye.Should().BeFalse();
        shop.DisabledReason.Should().Be("Pick an item to dye.");

        await Task.CompletedTask;
    }

    [Test]
    public async Task CanDye_needs_a_colour()
    {
        var shop = Ready();
        shop.SelectItem(shop.Items[0]);

        shop.CanDye.Should().BeFalse();
        shop.DisabledReason.Should().Be("Pick a colour.");

        await Task.CompletedTask;
    }

    [Test]
    public async Task CanDye_refuses_the_same_colour()
    {
        var shop = Ready();
        shop.SelectItem(Item(1, DisplayColor.Apple));
        shop.SelectColor(DisplayColor.Apple);

        shop.CanDye.Should().BeFalse();
        shop.DisabledReason.Should().Be("It is already that colour.");

        await Task.CompletedTask;
    }

    [Test]
    public async Task CanDye_refuses_without_the_gold()
    {
        var shop = Ready(gold: 4_999);
        shop.SelectItem(shop.Items[0]);
        shop.SelectColor(DisplayColor.Apple);

        shop.CanDye.Should().BeFalse();
        shop.DisabledReason.Should().Be("You need 5,000 gold.");

        await Task.CompletedTask;
    }

    [Test]
    public async Task CanDye_is_true_with_everything()
    {
        var shop = Ready();
        shop.SelectItem(shop.Items[0]);
        shop.SelectColor(DisplayColor.Apple);

        shop.CanDye.Should().BeTrue();
        shop.DisabledReason.Should().BeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task MarkSent_blocks_until_the_server_answers()
    {
        var shop = Ready();
        shop.SelectItem(shop.Items[0]);
        shop.SelectColor(DisplayColor.Apple);

        shop.MarkSent();
        shop.CanDye.Should().BeFalse();
        shop.DisabledReason.Should().Be("Dyeing...");

        shop.ApplyRejected(DyeShopRejectReason.NotEnoughGold);
        shop.CanDye.Should().BeTrue();
        shop.LastRejectReason.Should().Be(DyeShopRejectReason.NotEnoughGold);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Selecting_an_item_clears_the_reject_reason()
    {
        var shop = Ready();
        shop.ApplyRejected(DyeShopRejectReason.InvalidColor);

        shop.SelectItem(shop.Items[0]);

        shop.LastRejectReason.Should().BeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Paging_is_clamped()
    {
        var shop = Ready(25);
        shop.PageCount.Should().Be(3);

        shop.StepPage(-1);
        shop.Page.Should().Be(0);

        shop.StepPage(10);
        shop.Page.Should().Be(2);
        shop.VisibleItems.Should().HaveCount(1);

        await Task.CompletedTask;
    }
}
