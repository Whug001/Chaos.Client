using Chaos.Client.Rendering;
using Chaos.Client.Systems;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class TownImportPreviewLookTests
{
    private static readonly AislingAppearance Base = new()
    {
        Gender = Gender.Female,
        BodySpriteId = 1,
        HeadSprite = 12,
        HeadColor = DisplayColor.Fire,
        Accessory1Sprite = 900,
        GuildCloakDesignId = 7,
        OvercoatSprite = 40,
        ArmorSprite = 30,
        BootsSprite = 3
    };

    private static TownImportLookInfo Look(TownImportLayer layer, bool keepsHair = false, bool hidesHead = false, bool hidesBoots = false)
        => new()
        {
            Layer = layer,
            DisplaySprite = 500,
            Color = DisplayColor.Lime,
            KeepsHairColor = keepsHair,
            HidesHead = hidesHead,
            HidesBoots = hidesBoots
        };

    [Test]
    public void Accessory_replaces_the_first_accessory_and_drops_the_guild_cloak()
    {
        var look = TownImportPreviewLook.Apply(Base, Look(TownImportLayer.Accessory));

        look.Accessory1Sprite.Should().Be(500);
        look.Accessory1Color.Should().Be(DisplayColor.Lime);
        look.GuildCloakDesignId.Should().Be(0);
        look.OvercoatSprite.Should().Be(40);
    }

    [Test]
    public void Overcoat_and_overcoat_as_armor_use_their_layers()
    {
        var coat = TownImportPreviewLook.Apply(Base, Look(TownImportLayer.Overcoat));
        coat.OvercoatSprite.Should().Be(500);
        coat.OvercoatColor.Should().Be(DisplayColor.Lime);
        coat.GuildCloakDesignId.Should().Be(7);

        var armor = TownImportPreviewLook.Apply(Base, Look(TownImportLayer.OvercoatAsArmor));
        armor.ArmorSprite.Should().Be(500);
        armor.OvercoatSprite.Should().Be(0);
    }

    [Test]
    public void Head_keeps_hair_color_only_when_asked()
    {
        TownImportPreviewLook.Apply(Base, Look(TownImportLayer.Head, true)).HeadColor.Should().Be(DisplayColor.Fire);

        var dyed = TownImportPreviewLook.Apply(Base, Look(TownImportLayer.Head));
        dyed.HeadSprite.Should().Be(500);
        dyed.HeadColor.Should().Be(DisplayColor.Lime);
    }

    [Test]
    public void Hiding_flags_clear_head_and_boots()
    {
        var look = TownImportPreviewLook.Apply(Base, Look(TownImportLayer.Overcoat, hidesHead: true, hidesBoots: true));

        look.HeadSprite.Should().Be(0);
        look.BootsSprite.Should().Be(0);
    }

    [Test]
    public void Over_hair_takes_the_first_empty_accessory_slot_and_leaves_the_head()
    {
        var look = TownImportPreviewLook.Apply(Base, Look(TownImportLayer.OverHair));

        look.Accessory1Sprite.Should().Be(900);
        look.Accessory2Sprite.Should().Be(500);
        look.Accessory2Color.Should().Be(DisplayColor.Lime);
        look.HeadSprite.Should().Be(12);
        look.HeadColor.Should().Be(DisplayColor.Fire);
    }

    [Test]
    public void Over_hair_keeps_its_own_color_not_the_hair_color()
    {
        var look = TownImportPreviewLook.Apply(Base, Look(TownImportLayer.OverHair, true));

        look.Accessory2Color.Should().Be(DisplayColor.Lime);
    }

    [Test]
    public void Over_hair_uses_the_third_slot_when_all_are_full()
    {
        var full = Base with { Accessory2Sprite = 901, Accessory3Sprite = 902 };

        var look = TownImportPreviewLook.Apply(full, Look(TownImportLayer.OverHair));

        look.Accessory3Sprite.Should().Be(500);
        look.Accessory1Sprite.Should().Be(900);
    }
}
