using Chaos.Client.Controls.World.Popups.Fishing;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public class FishingLookTests
{
    [Test]
    public async Task Fish_inside_the_bar_is_covered()
    {
        FishingLook.Covers(50f, 40f, 30f).Should().BeTrue();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Fish_on_either_edge_is_covered_like_the_server()
    {
        FishingLook.Covers(40f, 40f, 30f).Should().BeTrue();
        FishingLook.Covers(70f, 40f, 30f).Should().BeTrue();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Fish_above_or_below_the_bar_is_not_covered()
    {
        FishingLook.Covers(39f, 40f, 30f).Should().BeFalse();
        FishingLook.Covers(71f, 40f, 30f).Should().BeFalse();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Meter_is_red_when_empty_amber_at_half_and_green_when_full()
    {
        FishingLook.MeterColor(0f).Should().Be(FishingLook.MeterLow);
        FishingLook.MeterColor(50f).Should().Be(FishingLook.MeterMid);
        FishingLook.MeterColor(100f).Should().Be(FishingLook.MeterHigh);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Meter_colour_clamps_outside_0_to_100()
    {
        FishingLook.MeterColor(-20f).Should().Be(FishingLook.MeterLow);
        FishingLook.MeterColor(140f).Should().Be(FishingLook.MeterHigh);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Meter_colour_blends_between_stops()
    {
        var quarter = FishingLook.MeterColor(25f);

        quarter.Should().Be(Color.Lerp(FishingLook.MeterLow, FishingLook.MeterMid, 0.5f));
        await Task.CompletedTask;
    }

    [Test]
    public async Task Zone_tint_moves_toward_the_target_at_the_fade_rate()
    {
        FishingLook.Approach(0f, 1f, 0.1f).Should().BeApproximately(0.1f / FishingLook.ZONE_FADE_SECONDS, 0.0001f);
        FishingLook.Approach(1f, 0f, 0.1f).Should().BeApproximately(1f - (0.1f / FishingLook.ZONE_FADE_SECONDS), 0.0001f);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Zone_tint_never_overshoots()
    {
        FishingLook.Approach(0.9f, 1f, 5f).Should().Be(1f);
        FishingLook.Approach(0.1f, 0f, 5f).Should().Be(0f);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Zone_colour_is_amber_outside_and_green_inside()
    {
        FishingLook.ZoneColor(0f).Should().Be(FishingLook.ZoneOut);
        FishingLook.ZoneColor(1f).Should().Be(FishingLook.ZoneIn);
        await Task.CompletedTask;
    }
}
