using Chaos.Client.Systems;
using Chaos.Client.ViewModel;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class FrameRateSettingsTests
{
    private static TimeSpan Hz(int hz) => TimeSpan.FromTicks(TimeSpan.TicksPerSecond / hz);

    [Test]
    public void Fixed60_IsTodaysTiming()
        => FrameRateSettings.Resolve(FrameRateMode.Fixed60)
                            .Should()
                            .Be((true, Hz(60), false));

    [Test]
    public void MatchMonitor_TurnsTheFixedStepOffAndVsyncOn()
    {
        var (fixedStep, _, vsync) = FrameRateSettings.Resolve(FrameRateMode.MatchMonitor);

        fixedStep.Should().BeFalse();
        vsync.Should().BeTrue();
    }

    [Test]
    [Arguments(FrameRateMode.Fixed120, 120)]
    [Arguments(FrameRateMode.Fixed144, 144)]
    [Arguments(FrameRateMode.Fixed165, 165)]
    [Arguments(FrameRateMode.Fixed240, 240)]
    [Arguments(FrameRateMode.Fixed320, 320)]
    public void FixedModes_UseTheirRateWithVsyncOff(FrameRateMode mode, int hz)
        => FrameRateSettings.Resolve(mode)
                            .Should()
                            .Be((true, Hz(hz), false));

    [Test]
    public void LabelsMatchTheEnumOrder()
        => FrameRateSettings.OptionLabels
                            .Should()
                            .Equal("60 (default)", "Match monitor (vsync)", "120", "144", "165", "240", "320");

    [Test]
    public void EveryModeHasALabel()
        => FrameRateSettings.OptionLabels.Count
                            .Should()
                            .Be(Enum.GetValues<FrameRateMode>().Length);

    [Test]
    public void DefaultIsFixed60()
        => default(FrameRateMode)
           .Should()
           .Be(FrameRateMode.Fixed60);

    [Test]
    public void TryParse_ReadsAStoredIndex()
    {
        FrameRateSettings.TryParse("3", out var mode).Should().BeTrue();
        mode.Should().Be(FrameRateMode.Fixed144);
    }

    [Test]
    [Arguments("99")]
    [Arguments("-1")]
    [Arguments("abc")]
    [Arguments("")]
    public void TryParse_RefusesBadValues(string value)
        => FrameRateSettings.TryParse(value, out _)
                            .Should()
                            .BeFalse();

    [Test]
    public void TargetFrameMs_IsTheFrameBudgetForFixedModes()
    {
        FrameRateSettings.TargetFrameMs(FrameRateMode.Fixed60).Should().BeApproximately(16.667f, 0.01f);
        FrameRateSettings.TargetFrameMs(FrameRateMode.Fixed320).Should().BeApproximately(3.125f, 0.01f);
    }

    [Test]
    public void TargetFrameMs_IsNullUnderMatchMonitor()
        => FrameRateSettings.TargetFrameMs(FrameRateMode.MatchMonitor)
                            .Should()
                            .BeNull();

    [Test]
    public void Dropdown_SitsDirectlyBelowResolution()
    {
        var display = SettingDefinitions.All
                                        .Where(d => d.Section == SettingSection.Display)
                                        .Select(d => d.Key)
                                        .ToList();

        display.IndexOf(SettingKey.FrameRate).Should().Be(display.IndexOf(SettingKey.Resolution) + 1);
    }

    [Test]
    public void Dropdown_ReadsTheSavedModeAndOffersEveryLabel()
    {
        var row = SettingDefinitions.ByKey(SettingKey.FrameRate);
        var saved = ClientSettings.FrameRateMode;

        try
        {
            ClientSettings.FrameRateMode = FrameRateMode.Fixed240;

            row.Label.Should().Be("Frame rate");
            row.Choices.Should().Equal(FrameRateSettings.OptionLabels);
            row.GetChoice!().Should().Be((int)FrameRateMode.Fixed240);
        } finally
        {
            ClientSettings.FrameRateMode = saved;
        }
    }
}
