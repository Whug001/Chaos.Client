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

    [Test]
    public void A_hold_that_began_on_a_shop_pick_stays_latched_through_its_repeats()
    {
        var held = NpcSessionControl.NextShopPickHeld(Key(Keycode.Space, false), true, false);
        held = NpcSessionControl.NextShopPickHeld(Key(Keycode.Space, true), false, held);

        held.Should().BeTrue();
        NpcSessionControl.IsHeldPick(Key(Keycode.Space, true), held).Should().BeTrue();
    }

    [Test]
    public void A_hold_on_a_plain_option_menu_keeps_answering_it()
    {
        //refining: Yes is highlighted and the hold should keep accepting one refine after another
        var held = NpcSessionControl.NextShopPickHeld(Key(Keycode.Space, false), false, true);

        held.Should().BeFalse();
        NpcSessionControl.IsHeldPick(Key(Keycode.Space, true), held).Should().BeFalse();
    }
}
