using Chaos.Client.Controls.World.Popups.Options;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Systems.KeyBinds;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

//Reset Page on a hotbar clears its keys (hotbar slots have no defaults), so the
//confirm and the status must say that; Reset All clears them too and must say so.
public class KeyBindingsResetTests
{
    private static KeyChord Key(Scancode key, KeyModifiers modifiers = KeyModifiers.None) => new(key, modifiers);

    [Test]
    public async Task Resetting_everything_clears_hotbar_keys()
    {
        var table = new KeyBindingTable();
        table.Bind(GameActions.HotbarAction(0, 0), 0, Key(Scancode.F6));
        table.Bind(GameAction.Inventory, 0, Key(Scancode.W));

        table.ResetToDefaults();

        table.Get(GameActions.HotbarAction(0, 0), 0).Should().Be(KeyChord.None);
        table.Get(GameAction.Inventory, 0).Should().Be(Key(Scancode.A));

        await Task.CompletedTask;
    }

    [Test]
    public async Task Resetting_a_hotbar_page_clears_only_that_bar()
    {
        var table = new KeyBindingTable();
        var bar = GameActions.All.Where(action => GameActions.HotbarIndex(action) == 0).ToList();
        table.Bind(bar[0], 0, Key(Scancode.F6));
        table.Bind(GameActions.HotbarAction(1, 0), 0, Key(Scancode.F5, KeyModifiers.Shift));
        table.Bind(GameAction.Inventory, 0, Key(Scancode.W));

        table.ResetToDefaults(bar);

        table.Get(bar[0], 0).Should().Be(KeyChord.None);
        table.Get(GameActions.HotbarAction(1, 0), 0).Should().Be(Key(Scancode.F5, KeyModifiers.Shift));
        table.Get(GameAction.Inventory, 0).Should().Be(Key(Scancode.W));

        await Task.CompletedTask;
    }

    [Test]
    public async Task Hotbar_page_says_it_clears_instead_of_promising_defaults()
    {
        KeyBindingsControl.ResetPageQuestionFor("Hotbar 1", hotbars: true)
                          .Should()
                          .Be("Hotbars have no default keys, so this clears every key on Hotbar 1. Are you sure?");
        KeyBindingsControl.ResetPageStatusFor("Hotbar 1", hotbars: true).Should().Be("Hotbar 1 has no keys now.");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Other_pages_keep_the_defaults_wording()
    {
        KeyBindingsControl.ResetPageQuestionFor("Movement", hotbars: false)
                          .Should()
                          .Be("This puts the Movement keys back to their defaults. Are you sure?");
        KeyBindingsControl.ResetPageStatusFor("Movement", hotbars: false)
                          .Should()
                          .Be("The Movement keys are back to their defaults.");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Reset_all_says_it_clears_the_hotbar_keys()
    {
        KeyBindingsControl.ResetAllQuestion
                          .Should()
                          .Be("This puts every key back to its default and clears all hotbar keys. Are you sure?");

        await Task.CompletedTask;
    }

    //DlgBack2.spf is 64 pixels wide (see ChatFilterDialogTests); the confirm popup tiles it
    //4-wide minus 23 for the interior, minus 2x6 content padding, and the message label wraps
    //2 pixels narrower still (its 1-pixel side padding). The default popup holds three lines.
    private const int CONFIRM_WRAP_WIDTH = (64 * 4) - 23 - 12 - 2;

    [Test]
    public async Task Reset_questions_fit_the_confirm_popup()
    {
        TextRenderer.WrapText(KeyBindingsControl.ResetAllQuestion, CONFIRM_WRAP_WIDTH).Should().HaveCountLessThanOrEqualTo(3);

        foreach (var bar in Enumerable.Range(1, 4))
            TextRenderer.WrapText(KeyBindingsControl.ResetPageQuestionFor($"Hotbar {bar}", hotbars: true), CONFIRM_WRAP_WIDTH)
                        .Should()
                        .HaveCountLessThanOrEqualTo(3);

        await Task.CompletedTask;
    }
}
