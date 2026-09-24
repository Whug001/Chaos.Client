using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.World.Popups;
using Chaos.Client.Definitions;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

/// <summary>
///     The "How many?" popup opens with the whole stack typed in and selected, so Enter moves the whole stack and typing
///     a number replaces it (player suggestions "Bank QoL" and "Deposit/trade/drop 'all' option"). The popup fills the
///     box while it is still hidden, which is the case <see cref="UITextBoxTests" /> covers for a crash.
/// </summary>
[NotInParallel]
public class ItemAmountPrefillTests
{
    private static UITextBox OpenedWith(uint amount, string earlierText = "")
    {
        var box = new UITextBox
        {
            Width = 400,
            Height = 20,
            MaxLength = 5,
            IsFocused = true
        };

        foreach (var c in earlierText)
            box.OnTextInput(new TextInputEvent { Character = c });

        box.Visible = false;
        ItemAmountControl.Prefill(box, amount);
        box.Visible = true;
        box.Update(new GameTime());

        return box;
    }

    private static void Type(UITextBox box, string text)
    {
        foreach (var c in text)
            box.OnTextInput(new TextInputEvent { Character = c });
    }

    [Test]
    public void TheBoxStartsWithTheWholeStack()
    {
        var box = OpenedWith(250);

        box.Text.Should().Be("250");
        box.SelectedText.Should().Be("250");
    }

    [Test]
    public void TypingReplacesTheWholeStack()
    {
        var box = OpenedWith(250);

        Type(box, "12");

        box.Text.Should().Be("12");
    }

    [Test]
    public void TypingReplacesAStackThatFillsTheBox()
    {
        var box = OpenedWith(99999);

        Type(box, "7");

        box.Text.Should().Be("7");
    }

    [Test]
    public void BackspaceClearsTheWholeStack()
    {
        var box = OpenedWith(250);

        box.OnKeyDown(new KeyDownEvent { Keycode = Keycode.Back });

        box.Text.Should().BeEmpty();
    }

    [Test]
    public void AnAmountLeftFromTheLastPromptIsReplaced()
    {
        var box = OpenedWith(3, "4000");

        Type(box, "2");

        box.Text.Should().Be("2");
    }
}
