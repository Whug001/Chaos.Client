using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class ChatBubblePickerStateTests
{
    private static EffectWindowDisplayArgs OpenArgs(bool license, byte current = 0)
        => new()
        {
            Type = EffectWindowDisplayType.Open,
            BubbleStyles = Enumerable.Range(1, 10)
                                     .Select(i => new ChatBubbleEntry { Key = (byte)i, Name = $"S{i}", Price = i <= 6 ? 5_000_000 : 10_000_000, Look = ChatBubbleLook.Default, Owned = i == 2 })
                                     .ToList(),
            LicenseOwned = license,
            LicensePrice = 15_000_000,
            CurrentBubble = current,
            CustomLook = new ChatBubbleLook { Border = 0x3AD5C8FF, Fill = 0x0B2A33B4, Text = 0xE6FFFCFF }
        };

    [Test]
    public void Entries_are_default_presets_custom_and_open_selects_current()
    {
        var state = new ChatBubblePickerState();
        state.Open(OpenArgs(license: false, current: 2));

        state.Entries.Select(e => e.Key).Should().Equal(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 255 });
        state.Selected.Key.Should().Be(2);
        state.Page.Should().Be(0);
        state.CanChoose.Should().BeTrue();
    }

    [Test]
    public void Unowned_preset_cannot_be_chosen()
    {
        var state = new ChatBubblePickerState();
        state.Open(OpenArgs(license: false));
        state.Select(state.Entries[7]);

        state.CanChoose.Should().BeFalse();
        state.StatusText.Should().Be("10,000,000 gold - Sold by Illustria in Abel.");
    }

    [Test]
    public void Custom_needs_license_valid_hex_and_readable_colors()
    {
        var state = new ChatBubblePickerState();
        state.Open(OpenArgs(license: true));
        state.Select(state.Entries[^1]);

        state.ShowsHexBoxes.Should().BeTrue();
        state.CanChoose.Should().BeTrue();
        state.ChoosePayload.Custom!.Fill.Should().Be(0x0B2A33B4u);

        state.SetHex(BubbleColorField.Text, "#12345");
        state.Warning.Should().Be(ChatBubblePickerState.BAD_HEX);
        state.CanChoose.Should().BeFalse();

        state.SetHex(BubbleColorField.Text, " 0b2a33 ");
        state.Warning.Should().Be(ChatBubblePickerState.UNREADABLE);
        state.CanChoose.Should().BeFalse();

        state.SetHex(BubbleColorField.Text, "#FFFFFF");
        state.Warning.Should().BeNull();
        state.CanChoose.Should().BeTrue();
    }

    [Test]
    public void Custom_without_license_shows_no_boxes()
    {
        var state = new ChatBubblePickerState();
        state.Open(OpenArgs(license: false));
        state.Select(state.Entries[^1]);

        state.ShowsHexBoxes.Should().BeFalse();
        state.CanChoose.Should().BeFalse();
    }

    [Test]
    public void StepAlpha_stays_in_range()
    {
        var state = new ChatBubblePickerState();
        state.Open(OpenArgs(license: true));

        for (var i = 0; i < 20; i++)
            state.StepAlpha(ChatBubblePickerState.ALPHA_STEP);

        state.FillAlpha.Should().Be(ChatBubbleColors.MAX_FILL_ALPHA);

        for (var i = 0; i < 20; i++)
            state.StepAlpha(-ChatBubblePickerState.ALPHA_STEP);

        state.FillAlpha.Should().Be(ChatBubbleColors.MIN_FILL_ALPHA);
    }
}
