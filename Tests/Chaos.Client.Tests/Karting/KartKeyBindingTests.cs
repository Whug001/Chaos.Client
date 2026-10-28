using Chaos.Client.Controls.World.Popups.Options;
using Chaos.Client.Definitions;
using Chaos.Client.Systems.KeyBinds;
using FluentAssertions;

namespace Chaos.Client.Tests.Karting;

public class KartKeyBindingTests
{
    private static KeyChord Key(Scancode key, KeyModifiers modifiers = KeyModifiers.None) => new(key, modifiers);

    [Test]
    public async Task Racing_actions_have_their_default_keys()
    {
        var table = new KeyBindingTable();

        table.Get(GameAction.KartAccelerate, 0).Should().Be(Key(Scancode.Up));
        table.Get(GameAction.KartBrake, 0).Should().Be(Key(Scancode.Down));
        table.Get(GameAction.KartSteerLeft, 0).Should().Be(Key(Scancode.Left));
        table.Get(GameAction.KartSteerRight, 0).Should().Be(Key(Scancode.Right));
        table.Get(GameAction.KartDrift, 0).Should().Be(Key(Scancode.LeftShift));
        table.Get(GameAction.KartUseItem, 0).Should().Be(Key(Scancode.Space));

        await Task.CompletedTask;
    }

    [Test]
    public async Task Racing_actions_have_their_own_category_and_are_not_emotes()
    {
        var actions = new[]
        {
            GameAction.KartAccelerate, GameAction.KartBrake, GameAction.KartSteerLeft,
            GameAction.KartSteerRight, GameAction.KartDrift, GameAction.KartUseItem
        };

        actions.Should().OnlyContain(action => GameActions.CategoryOf(action) == GameActionCategory.Racing);
        actions.Should().OnlyContain(action => !GameActions.IsEmote(action));
        GameActions.IsEmote(GameAction.Emote33).Should().BeTrue();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Racing_actions_only_clash_with_other_racing_actions()
    {
        KeyBindingTable.SameScope(GameAction.KartAccelerate, GameAction.MoveUp).Should().BeFalse();
        KeyBindingTable.SameScope(GameAction.KartDrift, GameAction.KartUseItem).Should().BeTrue();

        var table = new KeyBindingTable();
        table.Bind(GameAction.KartDrift, 0, Key(Scancode.Space)).Bound.Should().BeTrue();
        table.Bind(GameAction.KartUseItem, 0, Key(Scancode.Space)).TakenFrom.Should().Be(GameAction.KartDrift);
        table.Get(GameAction.MoveUp, 0).Should().Be(Key(Scancode.Up));

        await Task.CompletedTask;
    }

    [Test]
    public async Task Only_racing_actions_may_bind_a_lone_modifier()
    {
        var table = new KeyBindingTable();

        table.Bind(GameAction.KartDrift, 1, Key(Scancode.LeftShift)).Bound.Should().BeTrue();
        table.Bind(GameAction.Emote01, 1, Key(Scancode.LeftShift)).Bound.Should().BeFalse();
        KeyBindingTable.IsReservedFor(GameAction.KartDrift, Key(Scancode.LeftShift)).Should().BeFalse();
        KeyBindingTable.IsReservedFor(GameAction.Emote01, Key(Scancode.LeftShift)).Should().BeTrue();

        await Task.CompletedTask;
    }

    [Test]
    public async Task A_lone_modifier_chord_loads_without_modifiers()
    {
        KeyChord.TryParse("LeftShift", out var chord).Should().BeTrue();
        chord.Should().Be(Key(Scancode.LeftShift));

        await Task.CompletedTask;
    }

    [Test]
    public async Task F12_capture_accepts_a_lone_modifier_for_racing_actions()
    {
        KeyBindingsControl.AllowsLoneModifier(GameAction.KartDrift).Should().BeTrue();
        KeyBindingsControl.AllowsLoneModifier(GameAction.Emote01).Should().BeFalse();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Old_saved_bindings_fill_in_racing_defaults()
    {
        var table = new KeyBindingTable();

        foreach (var line in new[] { "Bind.MoveUp : Up, C", "Bind.MoveDown : Down, X", "Bind.Assail : Space, None" })
        {
            var colon = line.IndexOf(':');
            table.TryLoadSetting(line[..colon].Trim(), line[(colon + 1)..].Trim()).Should().BeTrue();
        }

        table.Get(GameAction.KartAccelerate, 0).Should().Be(Key(Scancode.Up));
        table.Get(GameAction.KartBrake, 0).Should().Be(Key(Scancode.Down));
        table.Get(GameAction.KartSteerLeft, 0).Should().Be(Key(Scancode.Left));
        table.Get(GameAction.KartSteerRight, 0).Should().Be(Key(Scancode.Right));
        table.Get(GameAction.KartDrift, 0).Should().Be(Key(Scancode.LeftShift));
        table.Get(GameAction.KartUseItem, 0).Should().Be(Key(Scancode.Space));

        await Task.CompletedTask;
    }
}
