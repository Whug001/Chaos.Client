using System.Text;
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests;

/// <summary>
///     A panel that clears its textbox while hidden leaves the cursor where it was: Update skips hidden boxes, so it
///     never clamps. The next keystroke after the panel reopens used the stale cursor and crashed the client
///     (crash logs of 2026-07-25, 2026-07-27 and 2026-09-22).
/// </summary>
[NotInParallel]
public class UITextBoxTests
{
    static UITextBoxTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private static UITextBox FocusedBox()
        => new()
        {
            Width = 400,
            Height = 20,
            IsFocused = true
        };

    private static UITextBox ByteCappedBox()
        => new()
        {
            Width = 5000,
            Height = 20,
            IsFocused = true,
            MaxLength = 300,
            MaxByteCount = 255,
            ByteEncoding = Encoding.GetEncoding(949)
        };

    private static void Type(UITextBox box, string text)
    {
        foreach (var c in text)
            box.OnTextInput(new TextInputEvent { Character = c });
    }

    private static void ChangeTextWhileHidden(UITextBox box, string text)
    {
        box.Visible = false;
        box.Text = text;
        box.Update(new GameTime());
        box.Visible = true;
    }

    [Test]
    public void TypingAfterTheTextWasClearedWhileHiddenStartsAtTheEnd()
    {
        var box = FocusedBox();
        Type(box, "100000000");

        ChangeTextWhileHidden(box, string.Empty);
        Type(box, "5");

        box.Text.Should().Be("5");
        box.CursorPosition.Should().Be(1);
    }

    [Test]
    public void BackspaceAfterTheTextWasShortenedWhileHiddenDeletesTheLastCharacter()
    {
        var box = FocusedBox();
        Type(box, "100000000");

        ChangeTextWhileHidden(box, "12");
        box.OnKeyDown(new KeyDownEvent { Keycode = Keycode.Back });

        box.Text.Should().Be("1");
        box.CursorPosition.Should().Be(1);
    }

    [Test]
    public void TypingStopsOnceTheTextWouldExceedTheByteCap()
    {
        var box = ByteCappedBox();
        Type(box, new string('a', 255));

        box.Text.Length.Should().Be(255);

        Type(box, "b");

        box.Text.Length.Should().Be(255);
    }

    [Test]
    public void TypingCountsKoreanAsTwoBytes()
    {
        var box = ByteCappedBox();
        Type(box, new string('가', 127));

        box.Text.Length.Should().Be(127);

        Type(box, "가");

        box.Text.Length.Should().Be(127);

        Type(box, "a");

        box.Text.Should().EndWith("a");
    }
}
