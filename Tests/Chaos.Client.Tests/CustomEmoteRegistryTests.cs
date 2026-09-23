using Chaos.Client.Rendering;
using Chaos.Client.Rendering.CustomEmotes;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class CustomEmoteRegistryTests
{
    [Test]
    public async Task Every_custom_emote_byte_is_relayed_by_the_server_and_collides_with_nothing()
    {
        foreach (var emote in CustomEmoteRegistry.All)
        {
            //Chaos-Server WorldServer.OnEmote relays 1..44 and drops everything else
            emote.BodyAnimation.Should().BeInRange(1, 44, emote.Name);

            Enum.IsDefined(typeof(BodyAnimation), (byte)emote.BodyAnimation)
                .Should()
                .BeFalse($"{emote.Name} must ride on an unused byte");
        }

        CustomEmoteRegistry.All.Select(e => e.BodyAnimation).Should().OnlyHaveUniqueItems();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Every_icon_code_sits_past_emot01_and_is_unique()
    {
        CustomEmoteRegistry.All.Should().OnlyContain(e => e.PreviewFrame >= 1000);
        CustomEmoteRegistry.All.Select(e => e.PreviewFrame).Should().OnlyHaveUniqueItems();
        await Task.CompletedTask;
    }

    [Test]
    public async Task Lookups_find_every_registered_emote()
    {
        foreach (var emote in CustomEmoteRegistry.All)
        {
            CustomEmoteRegistry.TryGet(emote.BodyAnimation, out var byByte).Should().BeTrue();
            byByte.Should().BeSameAs(emote);

            CustomEmoteRegistry.TryGetByPreviewFrame(emote.PreviewFrame, out var byIcon).Should().BeTrue();
            byIcon.Should().BeSameAs(emote);
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task Lookups_miss_on_ordinary_emotes()
    {
        CustomEmoteRegistry.TryGet((int)BodyAnimation.Smile, out _).Should().BeFalse();
        CustomEmoteRegistry.TryGetByPreviewFrame(0, out _).Should().BeFalse();
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_registry_starts_with_the_two_original_emotes()
    {
        CustomEmoteRegistry.All[0].Should().BeSameAs(SunglassesEmote.Instance);
        CustomEmoteRegistry.All[1].Should().BeSameAs(MiddleFingerEmote.Instance);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Settled_sunglasses_sit_where_the_old_renderer_put_them()
    {
        var layers = SunglassesEmote.Instance.Compose(SunglassesEmote.Instance.IconTimeMs, 0);

        layers.Should().ContainSingle();
        layers[0].X.Should().Be(SunglassesEmote.SETTLED_LEFT_X);
        layers[0].Y.Should().Be(SunglassesEmote.SETTLED_TOP_Y);
        layers[0].Art.Width.Should().Be(SunglassesEmote.GLASSES_WIDTH);
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_sparkle_hangs_off_the_glasses_corner()
    {
        var layers = SunglassesEmote.Instance.Compose(SunglassesEmote.DROP_MS + 1f, 0);

        layers.Should().HaveCount(2);
        layers[1].X.Should().Be(SunglassesEmote.SETTLED_LEFT_X + SunglassesEmote.SPARKLE_OFFSET_X);
        layers[1].Y.Should().Be(SunglassesEmote.SETTLED_TOP_Y + SunglassesEmote.SPARKLE_OFFSET_Y);
        await Task.CompletedTask;
    }

    [Test]
    public async Task Sunglasses_follow_the_head_bob()
    {
        SunglassesEmote.Instance.Compose(SunglassesEmote.Instance.IconTimeMs, 1)[0]
                       .Y
                       .Should()
                       .Be(SunglassesEmote.SETTLED_TOP_Y + 1);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Flipped_sunglasses_and_sparkle_land_where_the_old_renderer_put_them()
    {
        const int SPARKLE_WIDTH = 5;
        var flippedGlasses = SunglassesEmote.ResolveLeftX(true);

        CustomEmoteDrawContext.ResolveLeftX(true, SunglassesEmote.SETTLED_LEFT_X, SunglassesEmote.GLASSES_WIDTH)
                              .Should()
                              .Be(flippedGlasses);

        //the old renderer put the flipped sparkle at leftX + GLASSES_WIDTH - SPARKLE_OFFSET_X - sparkle.Width
        CustomEmoteDrawContext.ResolveLeftX(
                                  true,
                                  SunglassesEmote.SETTLED_LEFT_X + SunglassesEmote.SPARKLE_OFFSET_X,
                                  SPARKLE_WIDTH)
                              .Should()
                              .Be(flippedGlasses + SunglassesEmote.GLASSES_WIDTH - SunglassesEmote.SPARKLE_OFFSET_X - SPARKLE_WIDTH);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Sunglasses_are_gone_at_the_end()
    {
        SunglassesEmote.Instance.Compose(SunglassesEmote.TOTAL_MS, 0).Should().BeEmpty();
        SunglassesEmote.Instance.DurationMs.Should().Be(SunglassesEmote.TOTAL_MS);
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_middle_finger_icon_starts_from_the_stop_hand()
    {
        MiddleFingerEmote.Instance.IconSourceFrame.Should().Be(MiddleFingerEmote.SOURCE_FRAME);
        MiddleFingerEmote.Instance.DurationMs.Should().Be(MiddleFingerEmote.DURATION_MS);

        //the old renderer drew the bubble at ResolveLeftX; the shared draw must match for any width
        CustomEmoteDrawContext.ResolveLeftX(true, AislingRenderer.LAYER_OFFSET_PADDING, 34)
                              .Should()
                              .Be(MiddleFingerEmote.ResolveLeftX(true, 34));

        await Task.CompletedTask;
    }

    [Test]
    public async Task The_registry_lists_all_seven_in_catalog_order()
    {
        CustomEmoteRegistry.All.Select(e => e.Name)
                           .Should()
                           .Equal("Sunglasses", "Middle Finger", "Heart Eyes", "Halo", "Lightbulb", "Anger Steam", "Party Hat");

        await Task.CompletedTask;
    }
}
