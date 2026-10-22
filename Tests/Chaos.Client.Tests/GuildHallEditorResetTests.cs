using Chaos.Client.Controls.World.Popups.GuildHall;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

public sealed class GuildHallEditorResetTests
{
    [Test]
    public async Task First_click_arms_the_confirm_and_sends_nothing()
    {
        var state = new GuildHallEditorState();
        state.ResetCaption.Should().Be("Reset");

        state.RequestReset(out var args).Should().BeFalse();

        args.Should().BeNull();
        state.ResetArmed.Should().BeTrue();
        state.ResetCaption.Should().Be("Sure?");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Second_click_within_the_window_sends_a_reset()
    {
        var state = new GuildHallEditorState();
        state.RequestReset(out _);
        state.ResetUpdate(TimeSpan.FromSeconds(2.9));

        state.RequestReset(out var args).Should().BeTrue();

        args.Should().NotBeNull();
        args!.Action.Should().Be(GuildHallEditorAction.Reset);
        state.ResetArmed.Should().BeFalse();
        state.ResetCaption.Should().Be("Reset");

        await Task.CompletedTask;
    }

    [Test]
    public async Task After_three_seconds_the_caption_resets_and_a_click_sends_nothing()
    {
        var state = new GuildHallEditorState();
        state.RequestReset(out _);
        state.ResetUpdate(TimeSpan.FromSeconds(3));

        state.ResetArmed.Should().BeFalse();
        state.ResetCaption.Should().Be("Reset");

        state.RequestReset(out var args).Should().BeFalse();
        args.Should().BeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Close_disarms_the_confirm()
    {
        var state = new GuildHallEditorState();
        state.RequestReset(out _);

        state.Close();

        state.ResetArmed.Should().BeFalse();
        state.ResetCaption.Should().Be("Reset");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Reset_button_sits_left_of_the_checkmark_clear_of_the_title()
    {
        (GuildHallEditorControl.RESET_X + GuildHallEditorControl.RESET_W).Should()
                                                                          .BeLessThanOrEqualTo(GuildHallEditorControl.CHECK_X);

        (GuildHallEditorControl.PAD + GuildHallEditorControl.TITLE_W).Should()
                                                                      .BeLessThanOrEqualTo(GuildHallEditorControl.RESET_X);

        GuildHallEditorControl.VisibleBounds(GuildHallTab.Floors)
                              .Should()
                              .Contain(
                                  new Rectangle(
                                      GuildHallEditorControl.RESET_X,
                                      GuildHallEditorControl.HEADER_Y,
                                      GuildHallEditorControl.RESET_W,
                                      GuildHallEditorControl.HEADER_H));

        await Task.CompletedTask;
    }
}
