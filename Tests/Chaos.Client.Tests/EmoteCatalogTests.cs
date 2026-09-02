using Chaos.Client.Definitions;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class EmoteCatalogTests
{
    [Test]
    public async Task All_contains_33_unique_keyboard_emotes()
    {
        EmoteCatalog.All.Should().HaveCount(33);
        EmoteCatalog.All.Select(e => e.Animation).Should().OnlyHaveUniqueItems();
        await Task.CompletedTask;
    }

    [Test]
    public async Task All_includes_ctrl_tier_smile_and_alt_tier_confused()
    {
        EmoteCatalog.All.Should().Contain(e => e.Animation == BodyAnimation.Smile);
        EmoteCatalog.All.Should().Contain(e => e.Animation == BodyAnimation.Confused);
        await Task.CompletedTask;
    }

    [Test]
    public async Task TryGet_returns_entry_for_known_emote()
    {
        EmoteCatalog.TryGet(BodyAnimation.Wink, out var entry).Should().BeTrue();
        entry.Name.Should().Be("Wink");
        entry.PreviewFrame.Should().BeGreaterThanOrEqualTo(0);
        await Task.CompletedTask;
    }

    [Test]
    public async Task DefaultWheelSlots_has_six_entries()
    {
        EmoteCatalog.DefaultWheelSlots.Should().HaveCount(6);
        EmoteCatalog.DefaultWheelSlots[0].Should().Be(BodyAnimation.Smile);
        await Task.CompletedTask;
    }
}
