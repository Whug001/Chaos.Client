using Chaos.Client.Systems;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class DyeShopFigureTests
{
    private const ushort SPRITE = 321;

    private static DyeShopItem Item(
        TownImportLayer layer,
        Gender gender = Gender.Unisex,
        bool hidesHead = false,
        bool hidesBoots = false,
        bool hasLook = true)
        => new()
        {
            Slot = 1,
            UniqueId = 1,
            Name = "Thing",
            PanelSprite = 1,
            Color = DisplayColor.Default,
            Gender = gender,
            HasLook = hasLook,
            Look = new TownImportLookInfo
            {
                Layer = layer,
                DisplaySprite = SPRITE,
                HidesHead = hidesHead,
                HidesBoots = hidesBoots,
                KeepsHairColor = false
            }
        };

    [Test]
    public async Task Figure_wears_hair_and_nothing_else_before_the_item()
    {
        var look = DyeShopFigure.Compose(Gender.Male, 7, DisplayColor.Apple, Item(TownImportLayer.Overcoat), DisplayColor.Blue);

        look.HeadSprite.Should().Be(7);
        look.HeadColor.Should().Be(DisplayColor.Apple);
        look.ArmorSprite.Should().Be(0);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Overcoat_is_drawn_in_the_preview_colour()
    {
        var look = DyeShopFigure.Compose(Gender.Male, 7, DisplayColor.Apple, Item(TownImportLayer.Overcoat), DisplayColor.Blue);

        look.OvercoatSprite.Should().Be(SPRITE);
        look.OvercoatColor.Should().Be(DisplayColor.Blue);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Armor_layer_rides_the_overcoat_colour()
    {
        var look = DyeShopFigure.Compose(Gender.Male, 7, DisplayColor.Apple, Item(TownImportLayer.Armor), DisplayColor.Blue);

        look.ArmorSprite.Should().Be(SPRITE);
        look.OvercoatSprite.Should().Be(SPRITE);
        look.OvercoatColor.Should().Be(DisplayColor.Blue);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Armor_in_Default_shows_no_overcoat()
    {
        var look = DyeShopFigure.Compose(Gender.Male, 7, DisplayColor.Apple, Item(TownImportLayer.Armor), DisplayColor.Default);

        look.ArmorSprite.Should().Be(SPRITE);
        look.OvercoatSprite.Should().Be(0);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Helmet_layer_replaces_the_hair_in_the_preview_colour()
    {
        var look = DyeShopFigure.Compose(Gender.Male, 7, DisplayColor.Apple, Item(TownImportLayer.Helmet), DisplayColor.Blue);

        look.HeadSprite.Should().Be(SPRITE);
        look.HeadColor.Should().Be(DisplayColor.Blue);

        await Task.CompletedTask;
    }

    [Test]
    public async Task HidesBoots_and_HidesHead_are_honoured()
    {
        var head = DyeShopFigure.Compose(Gender.Male, 7, DisplayColor.Apple, Item(TownImportLayer.Helmet, hidesHead: true), DisplayColor.Blue);
        var boots = DyeShopFigure.Compose(Gender.Male, 7, DisplayColor.Apple, Item(TownImportLayer.Armor, hidesBoots: true), DisplayColor.Blue);

        head.HeadSprite.Should().Be(0);
        boots.BootsSprite.Should().Be(0);
        boots.HeadSprite.Should().Be(7);

        var shod = TownImportPreviewLook.Plain(Gender.Male) with { BootsSprite = 9 };
        TownImportPreviewLook.Apply(shod, Item(TownImportLayer.Armor, hidesBoots: true).Look).BootsSprite.Should().Be(0);

        await Task.CompletedTask;
    }

    [Test]
    public async Task CanWear_needs_a_look()
    {
        DyeShopFigure.CanWear(Item(TownImportLayer.Armor, hasLook: false), Gender.Male).Should().BeFalse();
        DyeShopFigure.CanWear(Item(TownImportLayer.Armor), Gender.Male).Should().BeTrue();

        await Task.CompletedTask;
    }

    [Test]
    public async Task CanWear_refuses_the_other_gender()
    {
        DyeShopFigure.CanWear(Item(TownImportLayer.Armor, Gender.Female), Gender.Male).Should().BeFalse();
        DyeShopFigure.CanWear(Item(TownImportLayer.Armor, Gender.Female), Gender.Female).Should().BeTrue();
        DyeShopFigure.CanWear(Item(TownImportLayer.Armor, Gender.Unisex), Gender.Male).Should().BeTrue();

        await Task.CompletedTask;
    }
}
