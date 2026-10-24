using Chaos.Client.Systems;
using Chaos.Client.ViewModel;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class CinemaSettingsTests
{
    [Test]
    public void Cinema_section_has_four_rows_in_order()
        => SettingDefinitions.All
                             .Where(d => d.Section == SettingSection.Cinema)
                             .Select(d => d.Key)
                             .Should()
                             .Equal(SettingKey.CinemaVolume, SettingKey.CinemaMute, SettingKey.CinemaHideVideo, SettingKey.CinemaLowerMusic);

    [Test]
    public void Cinema_section_comes_after_sound()
    {
        var sections = SettingDefinitions.PanelSections.ToList();

        sections.IndexOf(SettingSection.Cinema).Should().Be(sections.IndexOf(SettingSection.Sound) + 1);
    }

    [Test]
    public void Film_volume_slider_is_volume_over_ten()
    {
        var row = SettingDefinitions.All.Single(d => d.Key == SettingKey.CinemaVolume);
        var saved = ClientSettings.CinemaVolume;

        try
        {
            ClientSettings.CinemaVolume = 70;
            row.GetSliderValue!().Should().Be(7);
        } finally
        {
            ClientSettings.CinemaVolume = saved;
        }
    }
}
