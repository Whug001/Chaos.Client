#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering;
using Chaos.Client.Rendering.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>
///     One editable block of the College writing window. A multi-line box keeps its height fitted to its wrapped lines, so
///     it never scrolls itself (the window's stack scrolls instead), and it lets the mouse wheel through to that stack. The
///     box reports the keys that cross block edges: Backspace at its very start, Enter in a one-line (heading) box, and Up
///     or Down past its first or last line.
/// </summary>
public sealed class BlockTextBox : UITextBox
{
    private bool LayoutColorCodes;
    private string LayoutText = string.Empty;
    private int LayoutWidth = -1;
    private List<int> Starts = [0];

    /// <summary>Shown in <see cref="HintColor" /> while the box is empty and not focused.</summary>
    public string HintText { get; set; } = string.Empty;

    public Color HintColor { get; set; } = LegendColors.Gray;

    /// <summary>The line (0-based, counting wrapped lines) the caret is on.</summary>
    public int CaretLine
    {
        get
        {
            var starts = Layout();

            for (var i = starts.Count - 1; i > 0; i--)
                if (CursorPosition >= starts[i])
                    return i;

            return 0;
        }
    }

    /// <summary>The number of display lines: line breaks plus word-wrapped continuations.</summary>
    public int WrappedLineCount => Layout().Count;

    /// <summary>Up on the first line: moves the caret to the block above, and says whether there was one.</summary>
    public Func<bool>? TryLeaveUp { get; set; }

    /// <summary>Down on the last line: moves the caret to the block below, and says whether there was one.</summary>
    public Func<bool>? TryLeaveDown { get; set; }

    public event Action? BackspaceAtStart;
    public event Action? EnterPressed;

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        base.Draw(spriteBatch);

        if ((HintText.Length > 0) && (Text.Length == 0) && !IsFocused)
            DrawTextClipped(spriteBatch, new Vector2(ScreenX + PaddingLeft, ScreenY + PaddingTop), HintText, HintColor);
    }

    /// <summary>Sizes a multi-line box to show every line, and undoes any scroll the base box did before it grew.</summary>
    public void FitHeight()
    {
        if (!IsMultiLine)
            return;

        Height = (WrappedLineCount * TextRenderer.CHAR_HEIGHT) + PaddingTop + PaddingBottom;
        ScrollOffset = 0;
    }

    public override void OnKeyDown(KeyDownEvent e)
    {
        if (IsFocused && Enabled && !IsReadOnly && TryCrossEdge(e))
        {
            e.Handled = true;

            return;
        }

        base.OnKeyDown(e);
        FitHeight();
    }

    //the base box scrolls a focused multi-line box and swallows the wheel; this box is always fully shown, so the wheel
    //goes on to the window's scrolling stack
    public override void OnMouseScroll(MouseScrollEvent e) { }

    public override void OnTextInput(TextInputEvent e)
    {
        base.OnTextInput(e);
        FitHeight();
    }

    public void PlaceCaret(int position)
    {
        CursorPosition = Math.Clamp(position, 0, Text.Length);
        ClearSelection();
        IsFocused = true;
    }

    /// <summary>
    ///     Where each display line starts, wrapped exactly as <see cref="UITextBox" /> lays out a multi-line box (its line
    ///     starts are private), so the height and caret line here match what the box draws.
    /// </summary>
    internal static List<int> LineStarts(string text, int width, bool colorCodes)
    {
        List<int> starts = [0];

        if (string.IsNullOrEmpty(text) || (width <= 0))
            return starts;

        var pos = 0;

        while (pos <= text.Length)
        {
            var newline = text.IndexOf('\n', pos);
            var paragraphEnd = newline < 0 ? text.Length : newline;
            var remaining = text[pos..paragraphEnd];
            var offset = pos;

            while (remaining.Length > 0)
            {
                var consumed = TextRenderer.FindLineBreak(remaining, width, colorCodes);

                while ((consumed < remaining.Length) && (remaining[consumed] == ' '))
                    consumed++;

                remaining = remaining[consumed..];
                offset += consumed;

                if (remaining.Length > 0)
                    starts.Add(offset);
            }

            pos = paragraphEnd + 1;

            if ((newline >= 0) && (pos <= text.Length))
                starts.Add(pos);
        }

        if ((text[^1] == '\n') && (starts[^1] != text.Length))
            starts.Add(text.Length);

        return starts;
    }

    private List<int> Layout()
    {
        //the same inner width UITextBox wraps at
        var width = Width - PaddingLeft + PaddingRight;

        if ((width == LayoutWidth) && (Text == LayoutText) && (ColorCodesEnabled == LayoutColorCodes))
            return Starts;

        LayoutWidth = width;
        LayoutText = Text;
        LayoutColorCodes = ColorCodesEnabled;
        Starts = IsMultiLine ? LineStarts(Text, width, ColorCodesEnabled) : [0];

        return Starts;
    }

    private bool TryCrossEdge(KeyDownEvent e)
    {
        switch (e.Keycode)
        {
            case Keycode.Back when (CursorPosition == 0) && !HasSelection:
                BackspaceAtStart?.Invoke();

                return true;
            case Keycode.Enter when !IsMultiLine:
                EnterPressed?.Invoke();

                return true;
            case Keycode.Up when !e.Shift && !e.LineJump && !HasSelection && (CaretLine == 0):
                return TryLeaveUp?.Invoke() ?? false;
            case Keycode.Down when !e.Shift && !e.LineJump && !HasSelection && (CaretLine == (WrappedLineCount - 1)):
                return TryLeaveDown?.Invoke() ?? false;
            default:
                return false;
        }
    }
}
