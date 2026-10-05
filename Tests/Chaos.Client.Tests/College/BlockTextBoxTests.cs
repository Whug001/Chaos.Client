using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.World.Popups.College;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using FluentAssertions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.Tests.College;

[NotInParallel]
public class BlockTextBoxTests
{
    private static BlockTextBox Box(string text, int width = 62, bool multiLine = true)
        => new()
        {
            Width = width,
            Height = TextRenderer.CHAR_HEIGHT + 4,
            IsMultiLine = multiLine,
            MaxLength = 10_000,
            Text = text,
            IsFocused = true
        };

    private static int BaseLineCount(UITextBox box)
    {
        box.Update(new GameTime());

        return box.LineCount;
    }

    [Test]
    [Arguments("")]
    [Arguments("short")]
    [Arguments("aaaa bbbb cccc")]
    [Arguments("aaaaaaaaaaaaaaaaaaaaaaaaa")]
    [Arguments("one\ntwo\n\nthree   four five six seven")]
    [Arguments("ends with a break\n")]
    [Arguments("a b c d e f g h i j k l m n o p q r s t u v w x y z")]
    public void Wrapped_lines_match_the_base_text_box(string text)
    {
        var box = Box(text);

        box.WrappedLineCount.Should().Be(BaseLineCount(box));
    }

    [Test]
    public void The_caret_line_counts_wrapped_lines()
    {
        var box = Box("aaaa bbbb cccc");

        box.PlaceCaret(3);
        box.CaretLine.Should().Be(0);

        box.PlaceCaret(12);
        box.CaretLine.Should().Be(1);
    }

    [Test]
    public void Fitting_shows_every_line_without_scrolling()
    {
        var box = Box("one\ntwo\nthree");
        box.ScrollOffset = TextRenderer.CHAR_HEIGHT;

        box.FitHeight();

        box.Height.Should().Be((3 * TextRenderer.CHAR_HEIGHT) + 4);
        box.ScrollOffset.Should().Be(0);
    }

    [Test]
    public void Backspace_at_the_start_is_reported_and_deletes_nothing()
    {
        var box = Box("text");
        var reported = 0;
        box.BackspaceAtStart += () => reported++;

        box.PlaceCaret(0);
        box.OnKeyDown(new KeyDownEvent { Keycode = Keycode.Back });

        reported.Should().Be(1);
        box.Text.Should().Be("text");

        box.PlaceCaret(2);
        box.OnKeyDown(new KeyDownEvent { Keycode = Keycode.Back });

        reported.Should().Be(1);
        box.Text.Should().Be("txt");
    }

    [Test]
    public void Enter_is_reported_in_a_one_line_box_and_types_a_break_in_a_multi_line_box()
    {
        var heading = Box("Title", 300, false);
        var entered = 0;
        heading.EnterPressed += () => entered++;

        heading.OnKeyDown(new KeyDownEvent { Keycode = Keycode.Enter });

        entered.Should().Be(1);

        var text = Box("ab", 300);
        text.EnterPressed += () => entered++;
        text.PlaceCaret(1);
        text.OnKeyDown(new KeyDownEvent { Keycode = Keycode.Enter });

        entered.Should().Be(1);
        text.Text.Should().Be("a\nb");
        text.Height.Should().Be((2 * TextRenderer.CHAR_HEIGHT) + 4);
    }

    [Test]
    public void Up_and_down_leave_the_box_only_from_its_edge_lines()
    {
        var box = Box("one\ntwo", 300);
        var left = new List<string>();
        box.TryLeaveUp = () => { left.Add("up"); return true; };
        box.TryLeaveDown = () => false;
        box.Update(new GameTime());

        box.PlaceCaret(5);
        box.OnKeyDown(new KeyDownEvent { Keycode = Keycode.Up });

        left.Should().BeEmpty();
        box.CursorPosition.Should().Be(1);

        box.OnKeyDown(new KeyDownEvent { Keycode = Keycode.Up });

        left.Should().Equal("up");

        box.PlaceCaret(5);
        box.OnKeyDown(new KeyDownEvent { Keycode = Keycode.Down });

        box.CursorPosition.Should().Be(7);
    }
}
