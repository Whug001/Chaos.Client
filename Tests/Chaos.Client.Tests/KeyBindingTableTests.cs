using Chaos.Client.Definitions;
using Chaos.Client.Systems.KeyBinds;
using Chaos.DarkAges.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class KeyBindingTableTests
{
    private static KeyChord Key(Scancode key, KeyModifiers modifiers = KeyModifiers.None) => new(key, modifiers);

    [Test]
    public async Task Defaults_are_the_old_fixed_keys()
    {
        var table = new KeyBindingTable();

        table.Find(Key(Scancode.Up)).Should().Be(GameAction.MoveUp);
        table.Find(Key(Scancode.C)).Should().Be(GameAction.MoveUp);
        table.Find(Key(Scancode.Z)).Should().Be(GameAction.MoveLeft);
        table.Find(Key(Scancode.Space)).Should().Be(GameAction.Assail);
        table.Find(Key(Scancode.A)).Should().Be(GameAction.Inventory);
        table.Find(Key(Scancode.S, KeyModifiers.Shift)).Should().Be(GameAction.SkillsAlt);
        table.Find(Key(Scancode.D1)).Should().Be(GameAction.Slot1);
        table.Find(Key(Scancode.OemPlus)).Should().Be(GameAction.Slot12);
        table.Find(Key(Scancode.D1, KeyModifiers.Shift)).Should().Be(GameAction.Shout);
        table.Find(Key(Scancode.D1, KeyModifiers.Ctrl)).Should().Be(GameAction.Emote01);
        table.Find(Key(Scancode.D1, KeyModifiers.Ctrl | KeyModifiers.Alt)).Should().Be(GameAction.Emote12);
        table.Find(Key(Scancode.OemMinus, KeyModifiers.Alt)).Should().Be(GameAction.Emote33);
        table.Find(Key(Scancode.U)).Should().Be(GameAction.SongNote1);
        table.Find(Key(Scancode.P)).Should().Be(GameAction.SongNote4);
        table.Find(Key(Scancode.PrintScreen)).Should().Be(GameAction.Screenshot);
        table.Find(Key(Scancode.F12)).Should().BeNull("F12 opens the key bindings window and is never bound");

        await Task.CompletedTask;
    }

    [Test]
    public async Task No_two_actions_share_a_default_key()
    {
        //the targeting keys only run while a spell is armed, so they share keys with the rest on purpose (Space is both
        //Assail and Cast on Target); within each scope every default is unique
        foreach (var targeting in new[] { false, true })
        {
            var defaults = GameActions.All
                                      .Where(action => GameActions.IsTargeting(action) == targeting)
                                      .SelectMany(action => Enumerable.Range(0, KeyBindingTable.SLOTS)
                                                                      .Select(slot => (Action: action, Chord: KeyBindingTable.DefaultChord(action, slot))))
                                      .Where(pair => !pair.Chord.IsNone)
                                      .ToList();

            defaults.Select(pair => pair.Chord).Should().OnlyHaveUniqueItems();
            defaults.Where(pair => KeyBindingTable.IsReservedFor(pair.Action, pair.Chord)).Should().BeEmpty();
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task Targeting_defaults_are_the_tab_targeting_keys()
    {
        var table = new KeyBindingTable();

        table.Find(Key(Scancode.Tab), targeting: true).Should().Be(GameAction.TargetNext);
        table.Find(Key(Scancode.Tab, KeyModifiers.Shift), targeting: true).Should().Be(GameAction.TargetPrevious);
        table.Find(Key(Scancode.Space), targeting: true).Should().Be(GameAction.TargetCast);
        table.Find(Key(Scancode.Escape), targeting: true).Should().Be(GameAction.TargetCancel);
        table.Find(Key(Scancode.F1), targeting: true).Should().Be(GameAction.TargetSelf);
        table.Find(Key(Scancode.F2), targeting: true).Should().Be(GameAction.TargetGroup1);
        table.Find(Key(Scancode.F6), targeting: true).Should().Be(GameAction.TargetGroup5);
        table.Find(Key(Scancode.F7), targeting: true).Should().Be(GameAction.TargetNearestEnemy);
        table.Find(Key(Scancode.F8), targeting: true).Should().Be(GameAction.TargetFurthestEnemy);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Normal_lookups_never_see_the_targeting_keys()
    {
        var table = new KeyBindingTable();

        table.Find(Key(Scancode.Space)).Should().Be(GameAction.Assail);
        table.Find(Key(Scancode.F1)).Should().Be(GameAction.Help);
        table.Find(Key(Scancode.F8)).Should().BeNull();
        table.Find(Key(Scancode.Escape)).Should().BeNull();

        await Task.CompletedTask;
    }

    [Test]
    public async Task A_targeting_key_only_takes_from_another_targeting_action()
    {
        var table = new KeyBindingTable();

        //F9 is Ignore List (and no targeting key); giving it to Target Self leaves Ignore List alone
        var ignore = table.Bind(GameAction.TargetSelf, 0, Key(Scancode.F9));
        ignore.Bound.Should().BeTrue();
        ignore.TakenFrom.Should().BeNull();
        table.Find(Key(Scancode.F9)).Should().Be(GameAction.IgnoreList);
        table.Find(Key(Scancode.F9), targeting: true).Should().Be(GameAction.TargetSelf);

        //F7 is Nearest Enemy; giving it to Target Self takes it from Nearest Enemy, but not from Board List
        var nearest = table.Bind(GameAction.TargetSelf, 1, Key(Scancode.F7));
        nearest.TakenFrom.Should().Be(GameAction.TargetNearestEnemy);
        table.Find(Key(Scancode.F7)).Should().Be(GameAction.BoardList);

        //and the other way: Space for Pick Up leaves Cast on Target its Space
        table.Bind(GameAction.PickUp, 0, Key(Scancode.Space)).TakenFrom.Should().Be(GameAction.Assail);
        table.Find(Key(Scancode.Space), targeting: true).Should().Be(GameAction.TargetCast);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Only_cancel_targeting_may_have_escape()
    {
        var table = new KeyBindingTable();

        table.Bind(GameAction.TargetCast, 0, Key(Scancode.Escape)).Bound.Should().BeFalse();
        table.Bind(GameAction.Assail, 0, Key(Scancode.Escape)).Bound.Should().BeFalse();

        table.Bind(GameAction.TargetCancel, 0, Key(Scancode.Q)).Bound.Should().BeTrue();
        table.Bind(GameAction.TargetCancel, 1, Key(Scancode.Escape)).Bound.Should().BeTrue();
        table.Find(Key(Scancode.Escape), targeting: true).Should().Be(GameAction.TargetCancel);

        //and a settings file can't hand Escape to anything else
        table.TryLoadSetting("Bind.TargetCast", "Escape, None").Should().BeTrue();
        table.Get(GameAction.TargetCast, 0).IsNone.Should().BeTrue();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Targeting_bindings_round_trip_through_settings()
    {
        var table = new KeyBindingTable();
        table.Bind(GameAction.TargetCast, 0, Key(Scancode.E));
        table.Bind(GameAction.TargetGroup3, 1, Key(Scancode.D3, KeyModifiers.Alt));

        var loaded = new KeyBindingTable();

        foreach (var line in table.ToSettingsLines())
        {
            var split = line.IndexOf(" : ", StringComparison.Ordinal);
            loaded.TryLoadSetting(line[..split], line[(split + 3)..]);
        }

        loaded.Get(GameAction.TargetCast, 0).Should().Be(Key(Scancode.E));
        loaded.Get(GameAction.TargetGroup3, 1).Should().Be(Key(Scancode.D3, KeyModifiers.Alt));
        loaded.Find(Key(Scancode.E)).Should().Be(GameAction.WorldList, "a targeting key never takes a normal action's key");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Every_action_has_a_name_and_a_default_key()
    {
        foreach (var action in GameActions.All)
        {
            GameActions.NameOf(action).Should().NotBeNullOrWhiteSpace();
            KeyBindingTable.DefaultChord(action, 0).IsNone.Should().BeFalse($"{action} needs a default key");
        }

        await Task.CompletedTask;
    }

    [Test]
    public async Task Emote_actions_send_the_old_body_animations()
    {
        GameActions.EmoteAnimation(GameAction.Emote01).Should().Be(BodyAnimation.Smile);
        GameActions.EmoteAnimation(GameAction.Emote11).Should().Be(BodyAnimation.Wave);
        GameActions.EmoteAnimation(GameAction.Emote12).Should().Be((BodyAnimation)23);
        GameActions.EmoteAnimation(GameAction.Emote23).Should().Be((BodyAnimation)34);
        GameActions.EmoteAnimation(GameAction.Emote33).Should().Be((BodyAnimation)44);
        GameActions.All.Count(GameActions.IsEmote).Should().Be(GameActions.EMOTE_COUNT);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Binding_a_used_key_takes_it_from_the_other_action()
    {
        var table = new KeyBindingTable();

        var result = table.Bind(GameAction.Inventory, 0, Key(Scancode.D1, KeyModifiers.Ctrl));

        result.Should().Be(new BindResult(true, GameAction.Emote01));
        table.Find(Key(Scancode.D1, KeyModifiers.Ctrl)).Should().Be(GameAction.Inventory);
        table.Get(GameAction.Emote01, 0).IsNone.Should().BeTrue();
        table.Find(Key(Scancode.A)).Should().BeNull("Inventory's old key went with the rebind");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Moving_a_key_between_an_actions_own_slots_takes_nothing_from_anyone()
    {
        var table = new KeyBindingTable();

        var result = table.Bind(GameAction.MoveUp, 1, Key(Scancode.Up));

        result.Should().Be(new BindResult(true, null));
        table.Get(GameAction.MoveUp, 0).IsNone.Should().BeTrue();
        table.Get(GameAction.MoveUp, 1).Should().Be(Key(Scancode.Up));

        await Task.CompletedTask;
    }

    //formatter:off
    [Test]
    [Arguments(Scancode.Escape, KeyModifiers.None)]
    [Arguments(Scancode.F11, KeyModifiers.None)]
    [Arguments(Scancode.F12, KeyModifiers.Ctrl)]
    [Arguments(Scancode.Enter, KeyModifiers.Alt)]
    [Arguments(Scancode.LeftShift, KeyModifiers.None)]
    //formatter:on
    public async Task Fixed_keys_cannot_be_bound(Scancode key, KeyModifiers modifiers)
    {
        var table = new KeyBindingTable();

        table.Bind(GameAction.Inventory, 0, Key(key, modifiers)).Bound.Should().BeFalse();
        table.Get(GameAction.Inventory, 0).Should().Be(Key(Scancode.A));

        await Task.CompletedTask;
    }

    [Test]
    public async Task Modifiers_must_match_exactly_but_an_unclaimed_modifier_falls_back_to_the_bare_key()
    {
        var table = new KeyBindingTable();

        table.Find(Key(Scancode.D2, KeyModifiers.Shift)).Should().BeNull();
        table.Candidates(Key(Scancode.D2, KeyModifiers.Shift)).Should().Equal(GameAction.Slot2);

        table.Candidates(Key(Scancode.Up, KeyModifiers.Shift)).Should().Equal(GameAction.ScrollChatUp, GameAction.MoveUp);
        table.Candidates(Key(Scancode.D1, KeyModifiers.Shift)).Should().Equal(GameAction.Shout, GameAction.Slot1);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Unusable_actions_are_skipped()
    {
        var table = new KeyBindingTable();

        //no live song call: U runs nothing, and Ctrl+U falls past the note to nothing either
        table.Candidates(Key(Scancode.U), action => GameActions.SongNote(action) == 0).Should().BeEmpty();
        table.Candidates(Key(Scancode.U, KeyModifiers.Ctrl), action => GameActions.SongNote(action) == 0).Should().BeEmpty();
        table.Candidates(Key(Scancode.U)).Should().Equal(GameAction.SongNote1);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Bindings_survive_a_save_and_load()
    {
        var saved = new KeyBindingTable();
        saved.Bind(GameAction.Inventory, 0, Key(Scancode.I));
        saved.Bind(GameAction.Inventory, 1, Key(Scancode.Tab, KeyModifiers.Ctrl | KeyModifiers.Shift));
        saved.Clear(GameAction.MoveUp, 1);

        var loaded = new KeyBindingTable();

        foreach (var line in saved.ToSettingsLines())
        {
            var colon = line.IndexOf(':');
            loaded.TryLoadSetting(line[..colon].Trim(), line[(colon + 1)..].Trim()).Should().BeTrue(line);
        }

        foreach (var action in GameActions.All)
            for (var slot = 0; slot < KeyBindingTable.SLOTS; slot++)
                loaded.Get(action, slot).Should().Be(saved.Get(action, slot), $"{action} slot {slot}");

        await Task.CompletedTask;
    }

    [Test]
    public async Task A_bad_or_unknown_line_changes_nothing()
    {
        var table = new KeyBindingTable();

        table.TryLoadSetting("Sound Volume", "5").Should().BeFalse();
        table.TryLoadSetting("Bind.NoSuchAction", "A, None").Should().BeFalse();
        table.TryLoadSetting("Bind.Inventory", "Hyper+A, None").Should().BeFalse();
        table.TryLoadSetting("Bind.Inventory", "F12, None").Should().BeTrue("a reserved key is dropped, not an error");

        table.Get(GameAction.Inventory, 0).IsNone.Should().BeTrue();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Two_song_notes_cannot_share_a_key_even_with_different_modifiers()
    {
        var table = new KeyBindingTable();

        table.Bind(GameAction.SongNote2, 0, Key(Scancode.U))
             .Should()
             .Be(new BindResult(false, null, GameAction.SongNote1));

        table.Bind(GameAction.SongNote2, 1, Key(Scancode.U, KeyModifiers.Ctrl))
             .Should()
             .Be(new BindResult(false, null, GameAction.SongNote1));

        table.Get(GameAction.SongNote2, 0).Should().Be(Key(Scancode.I), "a refused key changes nothing");

        //a note may still move between its own slots, and other actions may still use a note's key
        table.Bind(GameAction.SongNote1, 1, Key(Scancode.U, KeyModifiers.Ctrl)).Bound.Should().BeTrue();
        table.Bind(GameAction.Inventory, 1, Key(Scancode.O, KeyModifiers.Alt)).Bound.Should().BeTrue();

        await Task.CompletedTask;
    }

    [Test]
    public async Task The_song_bar_shows_the_bound_note_keys()
    {
        var table = new KeyBindingTable();

        table.SongNoteDisplay().Should().BeEquivalentTo((string.Empty, new[] { "U", "I", "O", "P" }), options => options.WithStrictOrdering());

        //all four on one modifier: it is said once in front
        table.Bind(GameAction.SongNote1, 0, Key(Scancode.W, KeyModifiers.Ctrl));
        table.Bind(GameAction.SongNote2, 0, Key(Scancode.A, KeyModifiers.Ctrl));
        table.Bind(GameAction.SongNote3, 0, Key(Scancode.S, KeyModifiers.Ctrl));
        table.Bind(GameAction.SongNote4, 0, Key(Scancode.D, KeyModifiers.Ctrl));
        table.SongNoteDisplay().Should().BeEquivalentTo(("Ctrl+", new[] { "W", "A", "S", "D" }), options => options.WithStrictOrdering());

        //mixed modifiers: each key carries its own
        table.Bind(GameAction.SongNote4, 0, Key(Scancode.P));
        var (prefix, keys) = table.SongNoteDisplay();
        prefix.Should().BeEmpty();
        keys.Should().Equal("Ctrl+W", "Ctrl+A", "Ctrl+S", "P");

        //no primary: the secondary shows; no key at all: "?"
        table.Clear(GameAction.SongNote2, 0);
        table.Bind(GameAction.SongNote2, 1, Key(Scancode.I));
        table.Clear(GameAction.SongNote3, 0);
        table.SongNoteDisplay().Keys.Should().Equal("Ctrl+W", "I", "?", "P");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Chords_read_in_the_bindings_window()
    {
        Key(Scancode.D1, KeyModifiers.Ctrl | KeyModifiers.Alt).DisplayName.Should().Be("Ctrl+Alt+1");
        Key(Scancode.PageUp).DisplayName.Should().Be("Page Up");
        Key(Scancode.OemQuotes, KeyModifiers.Shift).DisplayName.Should().Be("Shift+'");
        KeyChord.None.DisplayName.Should().BeEmpty();

        await Task.CompletedTask;
    }
}
