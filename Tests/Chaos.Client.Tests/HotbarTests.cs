using Chaos.Client.Systems;
using Chaos.Client.Systems.KeyBinds;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

/// <summary>
///     The hotbars' model and the pieces around it: what a slot holds and how it is saved, how an item, skill or spell is
///     found again by name, the F12 actions and the F4 rows.
/// </summary>
//the bar settings tests change and reset the static ClientSettings arrays, so they can't share a run with each other
[NotInParallel]
public class HotbarTests
{
    private static readonly HotbarEntry Potion = new(HotbarEntryKind.Item, "Hemloch Potion", 140, DisplayColor.Default);
    private static readonly HotbarEntry Ioc = new(HotbarEntryKind.Spell, "beag ioc", 12);
    private static readonly HotbarEntry Strike = new(HotbarEntryKind.Skill, "Assail", 1);

    #region HotbarSet
    [Test]
    public void A_set_reads_back_what_it_wrote()
    {
        var set = new HotbarSet();
        set.Set(0, 0, Potion);
        set.Set(1, 5, Ioc);
        set.Set(3, 11, Strike);

        var copy = new HotbarSet();
        copy.LoadLines(set.ToLines());

        copy.Get(0, 0).Should().Be(Potion);
        copy.Get(1, 5).Should().Be(Ioc);
        copy.Get(3, 11).Should().Be(Strike);
        copy.Get(2, 0).Should().BeNull();
    }

    [Test]
    public void A_name_with_the_separator_in_it_survives_the_round_trip()
    {
        var odd = new HotbarEntry(HotbarEntryKind.Item, "Odd|Name", 3);

        HotbarEntry.TryParse(odd.Serialize(), out var parsed).Should().BeTrue();
        parsed.Should().Be(odd);
    }

    [Test]
    public void Lines_that_cannot_be_read_are_skipped_without_losing_the_rest()
    {
        var set = new HotbarSet();

        set.LoadLines(
        [
            "Bar1Slot1 : Item|140|0|Hemloch Potion",
            "Bar9Slot1 : Item|140|0|Out Of Range Bar",
            "Bar1Slot16 : Item|140|0|Out Of Range Slot",
            "Bar2Slot2 : Nonsense|1|0|Bad Kind",
            "Bar2Slot3 : Spell|notanumber|0|Bad Sprite",
            "garbage",
            "Bar4Slot12 : Skill|1|0|Assail"
        ]);

        set.Get(0, 0).Should().Be(Potion);
        set.Get(3, 11).Should().Be(Strike);
        set.ToLines().Should().HaveCount(2);
    }

    [Test]
    public void Swapping_moves_a_slot_onto_another_bar_and_onto_an_empty_slot()
    {
        var set = new HotbarSet();
        set.Set(0, 0, Potion);
        set.Set(2, 4, Ioc);

        set.Swap(0, 0, 2, 4);

        set.Get(0, 0).Should().Be(Ioc);
        set.Get(2, 4).Should().Be(Potion);

        set.Swap(2, 4, 3, 7);

        set.Get(2, 4).Should().BeNull();
        set.Get(3, 7).Should().Be(Potion);
    }

    [Test]
    public void Changes_are_announced_so_they_can_be_saved()
    {
        var set = new HotbarSet();
        var changes = 0;
        set.Changed += () => changes++;

        set.Set(0, 0, Potion);
        set.Set(0, 0, Potion); //no change, no announcement
        set.Swap(0, 0, 0, 1);
        set.Set(0, 1, null);

        changes.Should().Be(3);
    }

    [Test]
    public void Out_of_range_slots_are_ignored()
    {
        var set = new HotbarSet();

        set.Set(4, 0, Potion);
        set.Set(0, HotbarSet.MAX_SLOTS, Potion);

        set.Get(4, 0).Should().BeNull();
        set.ToLines().Should().BeEmpty();
    }
    #endregion

    #region Finding by name
    [Test]
    public void An_item_is_found_by_name_whether_or_not_it_is_stacked()
    {
        var inventory = new Inventory();
        inventory.SetSlot(3, 140, DisplayColor.Default, "Hemloch Potion", true, 7, 0, 0);
        inventory.SetSlot(5, 200, DisplayColor.Default, "Stick", false, 1, 10, 10);

        inventory.FindSlot("Hemloch Potion").Should().Be(3);
        inventory.FindSlot("Stick").Should().Be(5);
        inventory.FindSlot("Nothing").Should().Be(0);
    }

    [Test]
    public void A_stacked_slots_base_name_drops_the_count()
    {
        var inventory = new Inventory();
        inventory.SetSlot(3, 140, DisplayColor.Default, "Hemloch Potion", true, 7, 0, 0);

        Inventory.BaseName(in inventory.GetSlot(3)).Should().Be("Hemloch Potion");
        Inventory.BaseName(in inventory.GetSlot(4)).Should().BeNull();
    }

    [Test]
    public void A_skill_is_found_by_its_name_through_level_changes()
    {
        var book = new SkillBook();
        book.SetSlot(40, 7, "Assail (Lev:10/100)");

        book.FindSlotByAbilityName("assail").Should().Be(40);

        book.SetSlot(40, 7, "Assail (Lev:11/100)");

        book.FindSlotByAbilityName("Assail").Should().Be(40);
        book.FindSlotByAbilityName("Ass").Should().Be(0);
    }

    [Test]
    public void A_spell_is_found_by_its_name_on_either_page()
    {
        var book = new SpellBook();
        book.SetSlot(50, 12, "beag ioc (Lev:25/50)");

        book.FindSlotByAbilityName("Beag Ioc").Should().Be(50);
        book.FindSlotByAbilityName("ioc").Should().Be(0);
    }
    #endregion

    #region Key bindings
    [Test]
    public void Every_hotbar_slot_has_its_own_action()
    {
        for (var bar = 0; bar < HotbarSet.BAR_COUNT; bar++)
            for (var slot = 0; slot < HotbarSet.MAX_SLOTS; slot++)
            {
                var action = GameActions.HotbarAction(bar, slot);

                GameActions.HotbarIndex(action).Should().Be(bar);
                GameActions.HotbarSlot(action).Should().Be(slot);
                GameActions.CategoryOf(action).Should().Be(GameActionCategory.Hotbars);
                GameActions.NameOf(action).Should().Be($"Hotbar {bar + 1} Slot {slot + 1}");
            }

        GameActions.InCategory(GameActionCategory.Hotbars).Should().HaveCount(HotbarSet.BAR_COUNT * HotbarSet.MAX_SLOTS);
    }

    [Test]
    public void Hotbar_keys_start_unbound()
    {
        foreach (var action in GameActions.InCategory(GameActionCategory.Hotbars))
            for (var slot = 0; slot < KeyBindingTable.SLOTS; slot++)
                KeyBindingTable.DefaultChord(action, slot).IsNone.Should().BeTrue();
    }

    [Test]
    public void Other_actions_are_not_hotbar_actions()
    {
        GameActions.HotbarIndex(GameAction.Slot1).Should().Be(-1);
        GameActions.HotbarIndex(GameAction.TargetFurthestEnemy).Should().Be(-1);
        GameActions.HotbarIndex(GameAction.Emote01).Should().Be(-1);
        GameActions.IsEmote(GameAction.Hotbar4Slot15).Should().BeFalse();
    }
    #endregion

    #region Settings
    [Test]
    public void Each_bar_has_its_own_section_after_the_shared_one()
    {
        SettingDefinitions.PanelRows(SettingSection.Hotbars)
                          .Select(row => row.Key)
                          .Should()
                          .Equal(SettingKey.SolidHotbars);

        SettingDefinitions.PanelRows(SettingSection.Hotbar2)
                          .Select(row => row.Key)
                          .Should()
                          .Equal(
                              SettingKey.Hotbar2Visible,
                              SettingKey.Hotbar2Vertical,
                              SettingKey.Hotbar2Slots,
                              SettingKey.Hotbar2Locked,
                              SettingKey.Hotbar2Dock);

        var sections = SettingDefinitions.PanelSections.ToList();
        sections.IndexOf(SettingSection.Hotbar1).Should().Be(sections.IndexOf(SettingSection.Hotbars) + 1);
        sections.IndexOf(SettingSection.Hotbar4).Should().Be(sections.IndexOf(SettingSection.Hotbars) + 4);

        var slots = SettingDefinitions.ByKey(SettingKey.Hotbar2Slots);
        slots.Choices.Should().Equal(Enumerable.Range(1, 15).Select(count => count.ToString()));
        slots.GatedBy.Should().Be(SettingKey.Hotbar2Visible);
        SettingDefinitions.ByKey(SettingKey.Hotbar2Vertical).GatedBy.Should().Be(SettingKey.Hotbar2Visible);
        SettingDefinitions.ByKey(SettingKey.Hotbar2Locked).GatedBy.Should().Be(SettingKey.Hotbar2Visible);

        var position = SettingDefinitions.ByKey(SettingKey.Hotbar2Dock);
        position.GatedBy.Should().Be(SettingKey.Hotbar2Visible);
        position.Choices.Should().Equal("Free", "Bottom", "Top", "Left", "Right");
        position.Choices.Should().HaveCount(Enum.GetValues<HotbarDock>().Length);
    }

    [Test]
    public void Hotbar_keys_are_recognised_so_they_stay_out_of_the_shared_file()
    {
        ClientSettings.IsHotbarSetting("SolidHotbars").Should().BeTrue();
        ClientSettings.IsHotbarSetting("Hotbar1Visible").Should().BeTrue();
        ClientSettings.IsHotbarSetting("Hotbar4Locked").Should().BeTrue();
        ClientSettings.IsHotbarSetting("Hotbar2SomethingNewer").Should().BeTrue();

        ClientSettings.IsHotbarSetting("Hotbar5Visible").Should().BeFalse();
        ClientSettings.IsHotbarSetting("HotbarX").Should().BeFalse();
        ClientSettings.IsHotbarSetting("TransparentGroupPanels").Should().BeFalse();
    }

    [Test]
    public void Resetting_puts_every_bar_back_to_its_defaults()
    {
        var solid = ClientSettings.SolidHotbars;
        var visible = (bool[])ClientSettings.HotbarVisible.Clone();
        var slots = (int[])ClientSettings.HotbarSlotCount.Clone();
        var vertical = (bool[])ClientSettings.HotbarVertical.Clone();
        var locked = (bool[])ClientSettings.HotbarLocked.Clone();
        var edges = (HotbarDock[])ClientSettings.HotbarDockEdge.Clone();

        try
        {
            //another character's bars, still in memory when the next one logs in
            ClientSettings.SolidHotbars = true;
            ClientSettings.HotbarVisible[0] = true;
            ClientSettings.HotbarSlotCount[1] = 4;
            ClientSettings.HotbarVertical[2] = true;
            ClientSettings.HotbarLocked[3] = true;
            ClientSettings.HotbarDockEdge[3] = HotbarDock.Left;

            ClientSettings.ResetHotbarSettings();

            ClientSettings.SolidHotbars.Should().BeFalse();
            ClientSettings.HotbarVisible.Should().AllBeEquivalentTo(false);
            ClientSettings.HotbarSlotCount.Should().AllBeEquivalentTo(HotbarSet.DEFAULT_SLOTS);
            ClientSettings.HotbarVertical.Should().AllBeEquivalentTo(false);
            ClientSettings.HotbarLocked.Should().AllBeEquivalentTo(false);
            ClientSettings.HotbarDockEdge.Should().AllBeEquivalentTo(HotbarDock.None);
        } finally
        {
            ClientSettings.SolidHotbars = solid;
            visible.CopyTo(ClientSettings.HotbarVisible, 0);
            slots.CopyTo(ClientSettings.HotbarSlotCount, 0);
            vertical.CopyTo(ClientSettings.HotbarVertical, 0);
            locked.CopyTo(ClientSettings.HotbarLocked, 0);
            edges.CopyTo(ClientSettings.HotbarDockEdge, 0);
        }
    }

    [Test]
    public void Lock_and_position_lines_load_and_an_unknown_edge_is_ignored()
    {
        var locked = ClientSettings.HotbarLocked[1];
        var edge = ClientSettings.HotbarDockEdge[1];

        try
        {
            //the slots had their own lock once; it is one lock with the position now, and the old line is not read
            ClientSettings.TryLoadHotbarSetting("Hotbar2SlotsLocked", "1").Should().BeFalse();

            ClientSettings.TryLoadHotbarSetting("Hotbar2Locked", "1").Should().BeTrue();
            ClientSettings.TryLoadHotbarSetting("Hotbar2Edge", ((int)HotbarDock.Right).ToString()).Should().BeTrue();

            ClientSettings.HotbarLocked[1].Should().BeTrue();
            ClientSettings.HotbarDockEdge[1].Should().Be(HotbarDock.Right);

            ClientSettings.TryLoadHotbarSetting("Hotbar2Edge", "9").Should().BeTrue();
            ClientSettings.HotbarDockEdge[1].Should().Be(HotbarDock.Right);

            //a line from before edges were picked in the dropdown is not read: it would dock a bar its player had moved
            ClientSettings.TryLoadHotbarSetting("Hotbar2Dock", "1").Should().BeFalse();
            ClientSettings.HotbarDockEdge[1].Should().Be(HotbarDock.Right);
        } finally
        {
            ClientSettings.HotbarLocked[1] = locked;
            ClientSettings.HotbarDockEdge[1] = edge;
        }
    }

    [Test]
    public void Settings_lines_load_into_the_right_bar_and_clamp_the_slot_count()
    {
        var visible = ClientSettings.HotbarVisible[2];
        var vertical = ClientSettings.HotbarVertical[2];
        var count = ClientSettings.HotbarSlotCount[2];

        try
        {
            ClientSettings.TryLoadHotbarSetting("Hotbar3Visible", "1").Should().BeTrue();
            ClientSettings.TryLoadHotbarSetting("Hotbar3Vertical", "1").Should().BeTrue();
            ClientSettings.TryLoadHotbarSetting("Hotbar3Slots", "40").Should().BeTrue();

            ClientSettings.HotbarVisible[2].Should().BeTrue();
            ClientSettings.HotbarVertical[2].Should().BeTrue();
            ClientSettings.HotbarSlotCount[2].Should().Be(HotbarSet.MAX_SLOTS);

            ClientSettings.TryLoadHotbarSetting("Hotbar3Slots", "0").Should().BeTrue();
            ClientSettings.HotbarSlotCount[2].Should().Be(1);

            ClientSettings.TryLoadHotbarSetting("Hotbar5Visible", "1").Should().BeFalse();
            ClientSettings.TryLoadHotbarSetting("HotbarX", "1").Should().BeFalse();
            ClientSettings.TryLoadHotbarSetting("Sound Volume", "1").Should().BeFalse();
        } finally
        {
            ClientSettings.HotbarVisible[2] = visible;
            ClientSettings.HotbarVertical[2] = vertical;
            ClientSettings.HotbarSlotCount[2] = count;
        }
    }
    #endregion
}
