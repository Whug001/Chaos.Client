using Chaos.Client.Controls.World.Popups.Dialog;
using Chaos.Client.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests.Dialog;

/// <summary>
///     Holding Enter in a shop picked the item, and the key's repeats then answered the confirm prompt the server sent
///     back (its first option is highlighted). Only the first press may pick from a shop or list, and a hold that began
///     with such a pick may not answer the option menu it opened.
/// </summary>
public sealed class NpcSessionHeldKeyTests
{
    private static KeyDownEvent Key(Keycode key, bool repeat) => new() { Keycode = key, IsRepeat = repeat };

    //formatter:off
    [Test]
    [Arguments(Keycode.Enter)]
    [Arguments(Keycode.Space)]
    //formatter:on
    public void A_repeat_of_a_pick_key_is_swallowed_in_a_shop_or_list(Keycode key)
        => NpcSessionControl.IsHeldPick(Key(key, true), true, false, false).Should().BeTrue();

    [Test]
    public void A_repeat_is_swallowed_on_the_confirm_a_shop_or_list_pick_opened()
        => NpcSessionControl.IsHeldPick(Key(Keycode.Enter, true), false, true, true).Should().BeTrue();

    /// <summary>
    ///     Dialog crafts ask "craft another?" with Yes highlighted; holding Enter answered Yes again and again until
    ///     the guard swallowed every repeat on an option menu.
    /// </summary>
    [Test]
    public void A_held_key_still_answers_an_option_menu_reached_from_another_option_menu()
        => NpcSessionControl.IsHeldPick(Key(Keycode.Enter, true), false, true, false).Should().BeFalse();

    //formatter:off
    [Test]
    [Arguments(Keycode.Enter)]
    [Arguments(Keycode.Space)]
    //formatter:on
    public void The_first_press_still_picks(Keycode key)
        => NpcSessionControl.IsHeldPick(Key(key, false), true, true, true).Should().BeFalse();

    [Test]
    public void A_held_key_still_pages_plain_dialog_text()
        => NpcSessionControl.IsHeldPick(Key(Keycode.Enter, true), false, false, false).Should().BeFalse();

    //formatter:off
    [Test]
    [Arguments(Keycode.Up)]
    [Arguments(Keycode.Down)]
    [Arguments(Keycode.Left)]
    //formatter:on
    public void Held_arrows_keep_browsing(Keycode key)
        => NpcSessionControl.IsHeldPick(Key(key, true), true, true, true).Should().BeFalse();
}
