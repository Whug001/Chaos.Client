using Chaos.Client.Rendering;
using Chaos.Client.Systems;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class FittingRoomOutfitTests
{
    private static readonly AislingAppearance Base = new()
    {
        Gender = Gender.Female,
        BodySpriteId = 1,
        HeadSprite = 12,
        HeadColor = DisplayColor.Fire,
        OvercoatSprite = 40,
        ArmorSprite = 30,
        BootsSprite = 3,
        GuildCloakDesignId = 7
    };

    private static FittingRoomPiece Piece(
        TownImportLayer layer,
        ushort sprite = 500,
        DisplayColor color = DisplayColor.Lime,
        bool keepsHair = false,
        bool hidesHead = false,
        bool hidesBoots = false)
        => new(
            new TownImportLookInfo
            {
                Layer = layer,
                DisplaySprite = sprite,
                KeepsHairColor = keepsHair,
                HidesHead = hidesHead,
                HidesBoots = hidesBoots
            },
            color);

    [Test]
    public void Overcoat_layer_sets_overcoat_sprite_and_color()
    {
        var look = FittingRoomOutfit.Compose(Base, DisplayColor.Fire, Piece(TownImportLayer.Overcoat), null, []);

        look.OvercoatSprite.Should().Be(500);
        look.OvercoatColor.Should().Be(DisplayColor.Lime);
        look.ArmorSprite.Should().Be(30);
    }

    [Test]
    public void Overcoat_as_armor_replaces_armor_and_clears_overcoat()
    {
        var look = FittingRoomOutfit.Compose(Base, DisplayColor.Fire, Piece(TownImportLayer.OvercoatAsArmor, color: DisplayColor.Default), null, []);

        look.ArmorSprite.Should().Be(500);
        look.OvercoatSprite.Should().Be(0);
        look.OvercoatColor.Should().Be(DisplayColor.Default);
    }

    [Test]
    public void Colored_overcoat_as_armor_also_stays_in_the_overcoat_slot()
    {
        var look = FittingRoomOutfit.Compose(Base, DisplayColor.Fire, Piece(TownImportLayer.OvercoatAsArmor, color: DisplayColor.Lime), null, []);

        look.ArmorSprite.Should().Be(500);
        look.OvercoatSprite.Should().Be(500);
        look.OvercoatColor.Should().Be(DisplayColor.Lime);
    }

    [Test]
    public void Tried_on_overcoat_brings_back_hair_hidden_by_the_players_costume()
    {
        var costumed = Base with { HeadSprite = 0, HeadColor = DisplayColor.Default };

        var look = FittingRoomOutfit.Compose(costumed, DisplayColor.Fire, Piece(TownImportLayer.Overcoat), null, [], 9);

        look.HeadSprite.Should().Be(9);
        look.HeadColor.Should().Be(DisplayColor.Fire);
    }

    [Test]
    public void Tried_on_hat_stays_hidden_under_the_players_head_hiding_costume()
    {
        var costumed = Base with { HeadSprite = 0, HeadColor = DisplayColor.Default };

        var look = FittingRoomOutfit.Compose(costumed, DisplayColor.Fire, null, Piece(TownImportLayer.Head), [], 9);

        look.HeadSprite.Should().Be(0);
    }

    [Test]
    public void Tried_on_hat_shows_once_a_tried_on_overcoat_replaces_the_costume()
    {
        var costumed = Base with { HeadSprite = 0, HeadColor = DisplayColor.Default };

        var look = FittingRoomOutfit.Compose(costumed, DisplayColor.Fire, Piece(TownImportLayer.Overcoat), Piece(TownImportLayer.Head, 600), [], 9);

        look.HeadSprite.Should().Be(600);
    }

    [Test]
    public void Head_takes_the_chosen_color()
    {
        var look = FittingRoomOutfit.Compose(Base, DisplayColor.Fire, null, Piece(TownImportLayer.Head, keepsHair: true), []);

        look.HeadSprite.Should().Be(500);
        look.HeadColor.Should().Be(DisplayColor.Lime);
    }

    [Test]
    public void Head_that_keeps_hair_color_shows_it_on_the_default_swatch()
    {
        var look = FittingRoomOutfit.Compose(Base, DisplayColor.Fire, null, Piece(TownImportLayer.Head, color: DisplayColor.Default, keepsHair: true), []);

        look.HeadColor.Should().Be(DisplayColor.Fire);
    }

    [Test]
    public void Head_that_does_not_keep_hair_color_uses_the_default_swatch_as_is()
    {
        var look = FittingRoomOutfit.Compose(Base, DisplayColor.Fire, null, Piece(TownImportLayer.Head, color: DisplayColor.Default), []);

        look.HeadColor.Should().Be(DisplayColor.Default);
    }

    [Test]
    public void Over_hair_goes_in_slot_three_after_two_tried_on_accessories()
    {
        var look = FittingRoomOutfit.Compose(
            Base,
            DisplayColor.Fire,
            null,
            Piece(TownImportLayer.OverHair, 600),
            [Piece(TownImportLayer.Accessory, 101), Piece(TownImportLayer.Accessory, 102)]);

        look.Accessory1Sprite.Should().Be(101);
        look.Accessory2Sprite.Should().Be(102);
        look.Accessory3Sprite.Should().Be(600);
        look.HeadSprite.Should().Be(12);
        look.HeadColor.Should().Be(DisplayColor.Fire);
    }

    [Test]
    public void Over_hair_with_no_accessories_lands_in_the_first_empty_slot_and_drops_the_guild_cloak()
    {
        var look = FittingRoomOutfit.Compose(Base, DisplayColor.Fire, null, Piece(TownImportLayer.OverHair, 600), []);

        look.Accessory1Sprite.Should().Be(600);
        look.Accessory1Color.Should().Be(DisplayColor.Lime);
        look.GuildCloakDesignId.Should().Be(0);
    }

    [Test]
    public void Over_hair_always_uses_the_chosen_color_not_the_hair_color()
    {
        var look = FittingRoomOutfit.Compose(Base, DisplayColor.Fire, null, Piece(TownImportLayer.OverHair, 600, DisplayColor.Default, true), []);

        look.Accessory1Color.Should().Be(DisplayColor.Default);
    }

    [Test]
    public void Over_hair_uses_slot_three_when_the_base_has_all_slots_full()
    {
        var full = Base with { Accessory1Sprite = 1, Accessory2Sprite = 2, Accessory3Sprite = 3 };

        var look = FittingRoomOutfit.Compose(full, DisplayColor.Fire, null, Piece(TownImportLayer.OverHair, 600), []);

        look.Accessory3Sprite.Should().Be(600);
        look.Accessory1Sprite.Should().Be(1);
    }

    [Test]
    public void Tried_on_accessories_replace_the_base_in_order_and_slot_one_drops_the_guild_cloak()
    {
        var withBase = Base with { Accessory1Sprite = 1, Accessory2Sprite = 2, Accessory3Sprite = 3 };

        var look = FittingRoomOutfit.Compose(withBase, DisplayColor.Fire, null, null, [Piece(TownImportLayer.Accessory, 101), Piece(TownImportLayer.Accessory, 102)]);

        look.Accessory1Sprite.Should().Be(101);
        look.Accessory2Sprite.Should().Be(102);
        look.Accessory3Sprite.Should().Be(3);
        look.GuildCloakDesignId.Should().Be(0);
    }

    [Test]
    public void Without_accessories_the_guild_cloak_stays()
    {
        FittingRoomOutfit.Compose(Base, DisplayColor.Fire, Piece(TownImportLayer.Overcoat), null, []).GuildCloakDesignId.Should().Be(7);
    }

    [Test]
    public void Hiding_flags_of_any_worn_piece_clear_head_and_boots()
    {
        var look = FittingRoomOutfit.Compose(
            Base,
            DisplayColor.Fire,
            null,
            Piece(TownImportLayer.Head),
            [Piece(TownImportLayer.Accessory, hidesHead: true, hidesBoots: true)]);

        look.HeadSprite.Should().Be(0);
        look.HeadColor.Should().Be(DisplayColor.Default);
        look.BootsSprite.Should().Be(0);
        look.BootsColor.Should().Be(DisplayColor.Default);
    }
    [Test]
    public void Hair_colored_hats_use_the_real_hair_color_not_the_helmet_color()
    {
        var helmeted = Base with { HeadColor = DisplayColor.Scarlet };

        var head = FittingRoomOutfit.Compose(helmeted, DisplayColor.Blue, null, Piece(TownImportLayer.Head, color: DisplayColor.Default, keepsHair: true), []);

        head.HeadColor.Should().Be(DisplayColor.Blue);
    }
}
