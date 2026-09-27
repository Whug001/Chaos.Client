using Chaos.Client.Systems;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

//the settings tests flip static ClientSettings values, so they can't share a run with each other
[NotInParallel]
public class MapEffectFilterTests
{
    [Test]
    public void HidingNothing_KeepsEveryFlag()
    {
        const MapFlags FLAGS = MapFlags.Rain | MapFlags.Lightning | MapFlags.Fog | MapFlags.Blizzard;

        MapEffectFilter.Visible(FLAGS, MapFlags.None)
                       .Should()
                       .Be(FLAGS);
    }

    [Test]
    public void HidingLightning_KeepsTheRestOfTheStorm()
        => MapEffectFilter.Visible(MapFlags.Rain | MapFlags.Lightning | MapFlags.Fog, MapFlags.Lightning)
                          .Should()
                          .Be(MapFlags.Rain | MapFlags.Fog);

    [Test]
    public void HidingRain_RemovesRainFromARainyMap()
        => MapEffectFilter.Visible(MapFlags.Rain | MapFlags.Fog, MapFlags.Rain)
                          .Should()
                          .Be(MapFlags.Fog);

    /// <summary>
    ///     Darkness is stored as Rain | Snow. Taking the rain bit out of a dark map would leave Snow on its own, and the
    ///     weather renderer would start snowing on it.
    /// </summary>
    [Test]
    public void HidingRain_LeavesADarkMapDark()
        => MapEffectFilter.Visible(MapFlags.Darkness | MapFlags.Lightning, MapFlags.Rain)
                          .Should()
                          .Be(MapFlags.Darkness | MapFlags.Lightning);

    [Test]
    public void HidingRain_LeavesSnowAlone()
        => MapEffectFilter.Visible(MapFlags.Snow, MapFlags.Rain)
                          .Should()
                          .Be(MapFlags.Snow);

    [Test]
    public void EachSetting_HidesItsOwnEffect()
    {
        var saved = Capture();

        try
        {
            foreach ((var key, var flag) in Expected)
            {
                SetAll(false);
                SettingDefinitions.ByKey(key).Set!(true);

                MapEffectFilter.HiddenByPlayer
                               .Should()
                               .Be(flag, $"{key} should hide exactly {flag}");
            }
        } finally
        {
            Restore(saved);
        }
    }

    [Test]
    public void EverySettingStartsUnchecked()
    {
        var saved = Capture();

        try
        {
            SetAll(false);

            MapEffectFilter.HiddenByPlayer
                           .Should()
                           .Be(MapFlags.None);
        } finally
        {
            Restore(saved);
        }
    }

    /// <summary>The F4 rows live together in their own section, and each one is a plain local checkbox.</summary>
    [Test]
    public void EverySettingIsALocalCheckboxInTheMapEffectsSection()
    {
        foreach (var key in Expected.Keys)
        {
            var definition = SettingDefinitions.ByKey(key);

            definition.Category.Should().Be(SettingCategory.ClientLocal);
            definition.Section.Should().Be(SettingSection.MapEffects);
            definition.Get.Should().NotBeNull();
            definition.Set.Should().NotBeNull();
        }

        SettingDefinitions.PanelSections.Should().Contain(SettingSection.MapEffects);
        MapEffectFilter.Keys.Should().BeEquivalentTo(Expected.Keys);
    }

    private static readonly Dictionary<SettingKey, MapFlags> Expected = new()
    {
        [SettingKey.HideLightning] = MapFlags.Lightning,
        [SettingKey.HideRain] = MapFlags.Rain,
        [SettingKey.HideBloodMoon] = MapFlags.BloodMoon,
        [SettingKey.HideSandstorm] = MapFlags.Sandstorm,
        [SettingKey.HideFrost] = MapFlags.Frost,
        [SettingKey.HideBlizzard] = MapFlags.Blizzard
    };

    private static Dictionary<SettingKey, bool> Capture()
        => Expected.Keys.ToDictionary(key => key, key => SettingDefinitions.ByKey(key).Get!());

    private static void Restore(Dictionary<SettingKey, bool> saved)
    {
        foreach ((var key, var value) in saved)
            SettingDefinitions.ByKey(key).Set!(value);
    }

    private static void SetAll(bool value)
    {
        foreach (var key in Expected.Keys)
            SettingDefinitions.ByKey(key).Set!(value);
    }
}
