using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.CustomEmotes;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class EmoteCatalogTests
{
    [Test]
    public async Task All_contains_the_33_keyboard_emotes_plus_the_client_side_ones()
    {
        EmoteCatalog.All.Should().HaveCount(33 + CustomEmoteRegistry.All.Count);
        EmoteCatalog.All.Select(e => e.Animation).Should().OnlyHaveUniqueItems();
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_client_side_emotes_come_last_in_registry_order()
    {
        var tail = EmoteCatalog.All.TakeLast(CustomEmoteRegistry.All.Count).ToList();

        tail.Select(e => (int)e.Animation).Should().Equal(CustomEmoteRegistry.All.Select(e => e.BodyAnimation));
        tail.Select(e => e.Name).Should().Equal(CustomEmoteRegistry.All.Select(e => e.Name));
        tail.Select(e => e.PreviewFrame).Should().Equal(CustomEmoteRegistry.All.Select(e => e.PreviewFrame));

        EmoteCatalog.TryGet((BodyAnimation)SunglassesEmote.BODY_ANIMATION, out var sunglasses).Should().BeTrue();
        sunglasses.Name.Should().Be("Sunglasses");

        EmoteCatalog.TryGet((BodyAnimation)MiddleFingerEmote.BODY_ANIMATION, out var middleFinger).Should().BeTrue();
        middleFinger.Name.Should().Be("Middle Finger");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Middle_finger_uses_its_own_preview_frame()
    {
        EmoteCatalog.TryGet((BodyAnimation)MiddleFingerEmote.BODY_ANIMATION, out var entry)
                    .Should()
                    .BeTrue();

        entry.PreviewFrame.Should().Be(MiddleFingerEmote.PREVIEW_FRAME);

        EmoteCatalog.All.Select(e => e.PreviewFrame)
                    .Where(f => f >= 1000)
                    .Should()
                    .OnlyHaveUniqueItems();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Sunglasses_uses_its_own_preview_frame_not_an_emot01_frame()
    {
        EmoteCatalog.TryGet((BodyAnimation)SunglassesEmote.BODY_ANIMATION, out var entry)
                    .Should()
                    .BeTrue();

        entry.PreviewFrame.Should().Be(SunglassesEmote.PREVIEW_FRAME);

        //every other entry indexes emot01 directly, so the sentinel must not collide with a real frame
        EmoteCatalog.All.Where(e => (int)e.Animation != SunglassesEmote.BODY_ANIMATION)
                    .Should()
                    .OnlyContain(e => e.PreviewFrame != SunglassesEmote.PREVIEW_FRAME);

        await Task.CompletedTask;
    }

    [Test]
    public async Task The_sunglasses_body_animation_is_relayed_by_the_server_untouched()
    {
        //Chaos-Server WorldServer.OnEmote relays 1..44 and drops everything else
        SunglassesEmote.BODY_ANIMATION.Should().BeInRange(1, 44);

        //and it must not collide with a named animation the rest of the client already handles
        Enum.IsDefined(typeof(BodyAnimation), (byte)SunglassesEmote.BODY_ANIMATION)
            .Should()
            .BeFalse();

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

    [Test]
    public async Task The_catalog_has_forty_emotes()
    {
        EmoteCatalog.All.Should().HaveCount(40);
        await Task.CompletedTask;
    }
}
