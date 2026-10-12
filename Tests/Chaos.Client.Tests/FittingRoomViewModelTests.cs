using Chaos.Client.Rendering;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class FittingRoomViewModelTests
{
    private static readonly AislingAppearance Live = new()
    {
        Gender = Gender.Female,
        BodySpriteId = 1,
        HeadSprite = 99,
        HeadColor = DisplayColor.Blue,
        OvercoatSprite = 40,
        Accessory1Sprite = 7,
        GuildCloakDesignId = 5
    };

    private static FittingRoomLook Look(
        string name,
        TownImportLayer layer,
        ushort sprite = 500,
        Gender gender = Gender.Female,
        bool dyeable = false,
        DisplayColor color = DisplayColor.Default)
        => new()
        {
            Name = name,
            PanelSprite = 1,
            Gender = gender,
            Dyeable = dyeable,
            Colors = [color, DisplayColor.Lime],
            Look = new TownImportLookInfo
            {
                Layer = layer,
                DisplaySprite = sprite,
                Color = color
            }
        };

    private static FittingRoom Open(params FittingRoomLook[] looks)
    {
        var room = new FittingRoom();

        room.ApplyOpen(
            new FittingRoomDisplayArgs
            {
                Type = FittingRoomDisplayType.Open,
                Gender = Gender.Female,
                HairStyle = 4,
                HairColor = DisplayColor.Fire,
                Looks = looks
            },
            Live);

        return room;
    }

    [Test]
    public void Fourth_accessory_replaces_the_oldest()
    {
        var a = Enumerable.Range(1, 4).Select(i => Look($"acc{i}", TownImportLayer.Accessory, (ushort)(100 + i))).ToArray();
        var room = Open(a);

        foreach (var look in a)
            room.Toggle(look);

        room.Worn.Select(w => w.Look.Name).Should().Equal("acc2", "acc3", "acc4");
        room.Effective.Accessory1Sprite.Should().Be(102);
    }

    [Test]
    public void Crown_leaves_room_for_two_accessories()
    {
        var a = Enumerable.Range(1, 3).Select(i => Look($"acc{i}", TownImportLayer.Accessory, (ushort)(100 + i))).ToArray();
        var crown = Look("crown", TownImportLayer.OverHair, 600);
        var room = Open([..a, crown]);

        foreach (var look in a)
            room.Toggle(look);

        room.Toggle(crown);

        room.Worn.Select(w => w.Look.Name).Should().Equal("crown", "acc2", "acc3");
        room.Effective.Accessory3Sprite.Should().Be(600);

        var acc4 = Look("acc4", TownImportLayer.Accessory, 104);
        room = Open([..a, crown, acc4]);
        room.Toggle(crown);

        foreach (var look in a)
            room.Toggle(look);

        room.Worn.Select(w => w.Look.Name).Should().Equal("crown", "acc2", "acc3");
    }

    [Test]
    public void Toggle_removes_a_worn_look()
    {
        var coat = Look("coat", TownImportLayer.Overcoat);
        var room = Open(coat);

        room.Toggle(coat);
        room.Toggle(coat);

        room.Worn.Should().BeEmpty();
    }

    [Test]
    public void Overcoat_and_head_hold_one_each()
    {
        var c1 = Look("c1", TownImportLayer.Overcoat);
        var c2 = Look("c2", TownImportLayer.OvercoatAsArmor);
        var h1 = Look("h1", TownImportLayer.Head);
        var h2 = Look("h2", TownImportLayer.OverHair);
        var room = Open(c1, c2, h1, h2);

        room.Toggle(c1);
        room.Toggle(c2);
        room.Toggle(h1);
        room.Toggle(h2);

        room.Worn.Select(w => w.Look.Name).Should().Equal("c2", "h2");
    }

    [Test]
    public void Over_hair_look_counts_as_the_head_piece_and_leaves_the_head_alone()
    {
        var hat = Look("hat", TownImportLayer.OverHair, 600);
        var room = Open(hat);

        room.Toggle(hat);

        room.Effective.HeadSprite.Should().Be(99);
        room.Effective.Accessory2Sprite.Should().Be(600);
    }

    [Test]
    public void Set_color_changes_the_worn_color()
    {
        var coat = Look("coat", TownImportLayer.Overcoat);
        var room = Open(coat);
        room.Toggle(coat);

        room.SetColor(coat, DisplayColor.Lime);

        room.Effective.OvercoatColor.Should().Be(DisplayColor.Lime);
        room.ColorOf(coat).Should().Be(DisplayColor.Lime);
    }

    [Test]
    public void Stripped_uses_the_packet_hair_and_drops_the_player_gear()
    {
        var room = Open();

        room.Stripped = true;

        var look = room.Effective;
        look.HeadSprite.Should().Be(4);
        look.HeadColor.Should().Be(DisplayColor.Fire);
        look.OvercoatSprite.Should().Be(0);
        look.Accessory1Sprite.Should().Be(0);
        look.GuildCloakDesignId.Should().Be(0);
    }

    [Test]
    public void Stripping_takes_off_the_tried_on_pieces_and_they_stay_off()
    {
        var coat = Look("coat", TownImportLayer.Overcoat, 777);
        var hat = Look("hat", TownImportLayer.Head, 600);
        var acc = Look("acc", TownImportLayer.Accessory, 101);
        var room = Open(coat, hat, acc);
        room.Toggle(coat);
        room.Toggle(hat);
        room.Toggle(acc);

        room.Stripped = true;

        room.Worn.Should().BeEmpty();
        room.Effective.OvercoatSprite.Should().Be(0);
        room.Effective.Accessory1Sprite.Should().Be(0);

        room.Stripped = false;

        room.Worn.Should().BeEmpty();
        room.Effective.OvercoatSprite.Should().Be(40);
    }

    [Test]
    public void Pieces_tried_on_after_stripping_stay_on()
    {
        var coat = Look("coat", TownImportLayer.Overcoat, 777);
        var room = Open(coat);
        room.Stripped = true;

        room.Toggle(coat);

        room.Effective.OvercoatSprite.Should().Be(777);
    }

    [Test]
    public void Not_stripped_shows_the_live_look()
    {
        var look = Open().Effective;

        look.HeadSprite.Should().Be(99);
        look.OvercoatSprite.Should().Be(40);
    }

    [Test]
    public void Hovered_look_is_previewed_on_top_without_being_worn()
    {
        var coat = Look("coat", TownImportLayer.Overcoat, 777);
        var room = Open(coat);

        room.Hovered = coat;

        room.Effective.OvercoatSprite.Should().Be(777);
        room.Worn.Should().BeEmpty();

        room.Hovered = null;
        room.Effective.OvercoatSprite.Should().Be(40);
    }

    [Test]
    public void Hovering_a_worn_accessory_keeps_its_slot()
    {
        var a = Enumerable.Range(1, 3).Select(i => Look($"acc{i}", TownImportLayer.Accessory, (ushort)(100 + i))).ToArray();
        var room = Open(a);

        foreach (var look in a)
            room.Toggle(look);

        room.Hovered = a[0];

        room.Effective.Accessory1Sprite.Should().Be(101);
        room.Effective.Accessory3Sprite.Should().Be(103);
    }

    [Test]
    public void Gender_filter_hides_the_other_gender_but_keeps_unisex()
    {
        var room = Open(
            Look("her", TownImportLayer.Overcoat),
            Look("him", TownImportLayer.Overcoat, gender: Gender.Male),
            Look("both", TownImportLayer.Overcoat, gender: Gender.Unisex));

        room.VisibleLooks.Select(l => l.Name).Should().Equal("her", "both");
    }

    [Test]
    public void Tabs_show_their_layers()
    {
        var room = Open(
            Look("coat", TownImportLayer.Overcoat),
            Look("armor", TownImportLayer.OvercoatAsArmor),
            Look("hat", TownImportLayer.Head),
            Look("over", TownImportLayer.OverHair),
            Look("acc", TownImportLayer.Accessory));

        room.VisibleLooks.Select(l => l.Name).Should().Equal("coat", "armor");

        room.Tab = FittingRoomTab.Hats;
        room.VisibleLooks.Select(l => l.Name).Should().Equal("hat", "over");

        room.Tab = FittingRoomTab.Accessories;
        room.VisibleLooks.Select(l => l.Name).Should().Equal("acc");
    }

    [Test]
    public void Wearing_tab_lists_the_worn_looks()
    {
        var coat = Look("coat", TownImportLayer.Overcoat);
        var room = Open(coat, Look("hat", TownImportLayer.Head));
        room.Toggle(coat);

        room.Tab = FittingRoomTab.Wearing;

        room.VisibleLooks.Select(l => l.Name).Should().Equal("coat");
    }

    [Test]
    public void Search_is_a_case_insensitive_substring_of_the_name()
    {
        var room = Open(Look("Red Robe", TownImportLayer.Overcoat), Look("Blue Cloak", TownImportLayer.Overcoat));

        room.Search = "rOB";

        room.VisibleLooks.Select(l => l.Name).Should().Equal("Red Robe");
    }

    [Test]
    public void Paging_splits_by_page_size_and_wraps()
    {
        var room = Open(Enumerable.Range(0, 65).Select(i => Look($"coat{i}", TownImportLayer.Overcoat)).ToArray());

        room.PageCount.Should().Be(3);
        room.VisibleLooks.Should().HaveCount(30);

        room.StepPage(2);
        room.Page.Should().Be(2);
        room.VisibleLooks.Should().HaveCount(5);

        room.StepPage(1);
        room.Page.Should().Be(0);

        room.StepPage(-1);
        room.Page.Should().Be(2);
    }

    [Test]
    public void Changing_tab_or_search_resets_the_page()
    {
        var room = Open(Enumerable.Range(0, 65).Select(i => Look($"coat{i}", TownImportLayer.Overcoat)).ToArray());

        room.StepPage(1);
        room.Tab = FittingRoomTab.Hats;
        room.Page.Should().Be(0);

        room.Tab = FittingRoomTab.Overcoats;
        room.StepPage(1);
        room.Search = "coat";
        room.Page.Should().Be(0);
    }

    [Test]
    public void Swatches_offer_every_color_when_dyeable_else_the_listed_ones()
    {
        var plain = Look("plain", TownImportLayer.Overcoat);
        var dyeable = Look("dye", TownImportLayer.Overcoat, dyeable: true);
        var room = Open(plain, dyeable);

        room.SwatchColors(plain).Should().Equal(DisplayColor.Default, DisplayColor.Lime);
        room.SwatchColors(dyeable).Should().BeEquivalentTo(Enum.GetValues<DisplayColor>());
    }

    [Test]
    public void Clear_resets_everything()
    {
        var coat = Look("coat", TownImportLayer.Overcoat);
        var room = Open(coat);
        room.Toggle(coat);
        room.Stripped = true;

        room.Clear();

        room.IsOpen.Should().BeFalse();
        room.Looks.Should().BeEmpty();
        room.Worn.Should().BeEmpty();
        room.Stripped.Should().BeFalse();
    }
}
