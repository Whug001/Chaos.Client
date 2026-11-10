using Chaos.Client.Controls.World.Hud.Panel.Slots;
using Chaos.Client.Systems;
using Chaos.Client.ViewModel;
using FluentAssertions;

namespace Chaos.Client.Tests;

[NotInParallel]
public sealed class ShowAbilityLevelsTests
{
    [Test]
    public void Defaults_off_and_is_listed_in_F4()
    {
        ClientSettings.ShowAbilityLevels.Should().BeFalse();
        SettingDefinitions.All.Should().Contain(d => d.Key == SettingKey.ShowAbilityLevels);
    }

    [Test]
    public void F4_entry_is_display_client_local_with_expected_label()
    {
        var definition = SettingDefinitions.ByKey(SettingKey.ShowAbilityLevels);

        definition.Label.Should().Be("Show skill and spell levels");
        definition.Section.Should().Be(SettingSection.Display);
        definition.Category.Should().Be(SettingCategory.ClientLocal);
        definition.Choices.Should().BeNull();
        definition.GetSliderValue.Should().BeNull();
        definition.Get.Should().NotBeNull();
        definition.Set.Should().NotBeNull();
    }

    [Test]
    public void F4_entry_round_trips_through_definition_seam()
    {
        var wasShowLevels = ClientSettings.ShowAbilityLevels;

        try
        {
            var definition = SettingDefinitions.ByKey(SettingKey.ShowAbilityLevels);

            definition.Set!(true);
            ClientSettings.ShowAbilityLevels.Should().BeTrue();
            definition.Get!().Should().BeTrue();

            definition.Set!(false);
            ClientSettings.ShowAbilityLevels.Should().BeFalse();
            definition.Get!().Should().BeFalse();
        }
        finally
        {
            ClientSettings.ShowAbilityLevels = wasShowLevels;
        }
    }

    [Test]
    public void Full_level_is_hidden_when_setting_off()
        => AbilitySlotControl.FormatHoverText("Ioc (Lev:100/100)", "Ioc", "100/100", showLevels: false)
            .Should().Be("Ioc");

    [Test]
    public void Partial_level_is_kept_when_setting_off()
        => AbilitySlotControl.FormatHoverText("Ioc (Lev:40/100)", "Ioc", "40/100", showLevels: false)
            .Should().Be("Ioc (Lev:40/100)");

    [Test]
    public void Setting_on_keeps_full_text()
        => AbilitySlotControl.FormatHoverText("Ioc (Lev:100/100)", "Ioc", "100/100", showLevels: true)
            .Should().Be("Ioc (Lev:100/100)");

    [Test]
    public void Name_without_lev_suffix_passes_through()
        => AbilitySlotControl.FormatHoverText("Ioc", "Ioc", null, showLevels: false)
            .Should().Be("Ioc");

    [Test]
    public void Null_returns_null()
        => AbilitySlotControl.FormatHoverText(null, null, null, showLevels: false)
            .Should().BeNull();

    [Test]
    public void Empty_returns_null()
        => AbilitySlotControl.FormatHoverText(string.Empty, null, null, showLevels: false)
            .Should().BeNull();

    [Test]
    public void Unparseable_level_keeps_full_text()
        => AbilitySlotControl.FormatHoverText("Ioc (Lev:abc)", "Ioc", "abc", showLevels: false)
            .Should().Be("Ioc (Lev:abc)");

    [Test]
    public void HoverText_hides_full_level_by_default_and_shows_when_setting_on()
    {
        var wasShowLevels = ClientSettings.ShowAbilityLevels;

        try
        {
            using var slot = new SkillSlot();
            slot.SetAbilityName("Ioc (Lev:100/100)");

            ClientSettings.ShowAbilityLevels = false;
            slot.HoverText.Should().Be("Ioc");
            slot.SlotName.Should().Be("Ioc (Lev:100/100)");

            ClientSettings.ShowAbilityLevels = true;
            slot.HoverText.Should().Be("Ioc (Lev:100/100)");
        }
        finally
        {
            ClientSettings.ShowAbilityLevels = wasShowLevels;
        }
    }
}
