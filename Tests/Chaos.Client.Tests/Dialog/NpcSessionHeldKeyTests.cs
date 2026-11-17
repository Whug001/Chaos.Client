using Chaos.Client.Controls.World.Popups.Dialog;
using Chaos.Client.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests.Dialog;

/// <summary>
///     Holding Enter in a shop picked the item, and the key's repeats then answered the confirm prompt the server sent
///     back (its first option is highlighted). Only the first press may pick.
/// </summary>
public sealed class NpcSessionHeldKeyTests
{
    private static KeyDownEvent Key(Keycode key, bool repeat) => new() { Keycode = key, IsRepeat = repeat };

    //formatter:off
    [Test]
    [Arguments(Keycode.Enter)]
    [Arguments(Keycode.Space)]
    //formatter:on
    public void A_repeat_of_a_pick_key_is_swallowed_while_a_picker_is_open(Keycode key)
        => NpcSessionControl.IsHeldPick(Key(key, true), true).Should().BeTrue();

    //formatter:off
    [Test]
    [Arguments(Keycode.Enter)]
    [Arguments(Keycode.Space)]
    //formatter:on
    public void The_first_press_still_picks(Keycode key)
        => NpcSessionControl.IsHeldPick(Key(key, false), true).Should().BeFalse();

    [Test]
    public void A_held_key_still_pages_plain_dialog_text()
        => NpcSessionControl.IsHeldPick(Key(Keycode.Enter, true), false).Should().BeFalse();

    //formatter:off
    [Test]
    [Arguments(Keycode.Up)]
    [Arguments(Keycode.Down)]
    [Arguments(Keycode.Left)]
    //formatter:on
    public void Held_arrows_keep_browsing(Keycode key)
        => NpcSessionControl.IsHeldPick(Key(key, true), true).Should().BeFalse();
}
