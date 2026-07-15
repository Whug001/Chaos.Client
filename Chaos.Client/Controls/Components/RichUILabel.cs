#region
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.Components;

/// <summary>
///     A wrapped <see cref="UILabel" /> whose text can contain <see cref="Keywords" /> -- terms the reader may hover for an
///     explanation. Each is underlined where it is drawn, which is what tells the reader it can be hovered at all.
/// </summary>
/// <remarks>
///     Underlining a term and explaining it are one feature, so they read one list: <see cref="KeywordRuns" />, built once
///     per text change. "Underlined" and "hoverable" are therefore the same set by construction, rather than two matchers
///     that can drift apart.
///     <para />
///     The label does not know what a keyword <i>means</i> -- it takes plain strings and reports the one under the cursor.
///     Where the strings come from and what they explain is the caller's business.
/// </remarks>
public class RichUILabel : UILabel
{
    /// <summary>
    ///     Where a keyword landed once the text was wrapped: the line it fell on, and where in that line it starts. A
    ///     keyword the wrapper split across a line break has one run per fragment, both naming the whole keyword.
    /// </summary>
    private readonly record struct KeywordRun(
        int Line,
        int Index,
        int Length,
        string Keyword);

    private readonly List<KeywordRun> KeywordRuns = [];

    /// <summary>
    ///     Colour of the keyword underlines. Null adopts the colour of the text each one sits under, so a term keeps
    ///     whatever colour it is written in; set it to mark every term the same way regardless.
    /// </summary>
    public Color? UnderlineColor { get; set; }

    /// <summary>
    ///     Terms to underline and report on hover. Order does not matter -- the setter sorts.
    /// </summary>
    public IReadOnlyList<string> Keywords
    {
        get => field;

        set
        {
            //longest first, or a term that is the suffix of another ("Suain" of "Beag Suain") matches inside it
            field = value.OrderByDescending(static keyword => keyword.Length)
                         .ToArray();

            BuildKeywordRuns();
        }
    } = [];

    public RichUILabel() => WordWrap = true;

    /// <summary>
    ///     The keyword under the given point, or null if the point is over none.
    /// </summary>
    public string? KeywordAt(int mouseX, int mouseY)
    {
        var lines = WrappedLines;

        if (lines is null or { Count: 0 } || (KeywordRuns.Count == 0) || !ContainsPoint(mouseX, mouseY))
            return null;

        var innerY = ScreenY + PaddingTop;
        var innerH = Height - PaddingTop - PaddingBottom;

        //mirror the render-time layout, or the hover lands on a different line than the one drawn
        var localY = mouseY - innerY - WrappedVerticalOffset(lines.Count, innerH);

        if (localY < 0)
            return null;

        var lineIndex = FirstVisibleLine + (localY / TextRenderer.CHAR_HEIGHT);

        if ((lineIndex >= lines.Count) || !IsKeywordLine(lineIndex))
            return null;

        var lineText = lines[lineIndex];
        var lineX = WrappedLineX(lineText, ScreenX + PaddingLeft, Width - PaddingLeft - PaddingRight);

        foreach (var run in KeywordRuns)
        {
            if (run.Line != lineIndex)
                continue;

            var startX = lineX + TextRenderer.MeasureWidth(lineText.AsSpan(0, run.Index));
            var endX = startX + TextRenderer.MeasureWidth(lineText.AsSpan(run.Index, run.Length));

            if ((mouseX >= startX) && (mouseX < endX))
                return run.Keyword;
        }

        return null;
    }

    protected override void DrawLineOverlay(
        SpriteBatch spriteBatch,
        int lineIndex,
        string lineText,
        int lineX,
        int lineY)
    {
        if ((KeywordRuns.Count == 0) || !IsKeywordLine(lineIndex))
            return;

        foreach (var run in KeywordRuns)
            if (run.Line == lineIndex)
                DrawUnderline(
                    spriteBatch,
                    lineText,
                    run.Index,
                    run.Length,
                    lineX,
                    lineY,
                    UnderlineColor);
    }

    protected override void OnTextInvalidated() => BuildKeywordRuns();

    private int FirstVisibleLine => ScrollOffset / TextRenderer.CHAR_HEIGHT;

    /// <summary>
    ///     True when the line is one a keyword may be underlined and hovered on: visible, and with room for its whole glyph
    ///     cell.
    /// </summary>
    /// <remarks>
    ///     The height check is what keeps the underline and the hover honest with each other. The base label draws a bottom
    ///     line that only <i>partly</i> fits, and an underline sits on the last row of the glyph cell -- the first row the
    ///     clip rectangle eats. Without this the term on that line would be hoverable but visibly unmarked, and whether it
    ///     was would depend on the panel's height modulo the line height.
    /// </remarks>
    private bool IsKeywordLine(int lineIndex)
    {
        var wholeLines = (Height - PaddingTop - PaddingBottom) / TextRenderer.CHAR_HEIGHT;

        return lineIndex < (FirstVisibleLine + wholeLines);
    }

    /// <summary>
    ///     Locates every <see cref="Keywords" /> occurrence in the wrapped text. Runs when the text, the wrap width, or the
    ///     keyword list changes -- never per frame.
    /// </summary>
    private void BuildKeywordRuns()
    {
        KeywordRuns.Clear();

        var lines = WrappedLines;

        if (lines is null || (Keywords.Count == 0))
            return;

        for (var line = 0; line < lines.Count; line++)
        {
            //a term the wrapper broke over this line and the next claims both halves before anything else is tried,
            //so the trailing half of "Beag Suain" cannot then match as "Suain" and be explained as the wrong term
            if ((line + 1) < lines.Count)
                AddSplitRuns(lines, line);

            AddRuns(line, lines[line]);
        }
    }

    private void AddRuns(int line, string text)
    {
        foreach (var keyword in Keywords)
        {
            if (keyword.Length == 0)
                continue;

            for (var index = text.IndexOf(keyword, StringComparison.Ordinal);
                 index >= 0;
                 index = text.IndexOf(keyword, index + 1, StringComparison.Ordinal))
                if (IsWholeWord(text, index, keyword.Length) && !Covered(line, index, keyword.Length))
                    KeywordRuns.Add(
                        new KeywordRun(
                            line,
                            index,
                            keyword.Length,
                            keyword));
        }
    }

    /// <summary>
    ///     Records the two halves of any multi-word keyword the wrapper split between <paramref name="line" /> and the one
    ///     after it. Rejoining the pair recovers the original text: the wrapper breaks on a space and re-opens the active
    ///     colour on the continuation line.
    /// </summary>
    /// <remarks>
    ///     Known ceiling: one break. A keyword broken across three lines -- only reachable at a wrap width narrower than the
    ///     keyword itself -- goes unmatched.
    /// </remarks>
    private void AddSplitRuns(IReadOnlyList<string> lines, int line)
    {
        var head = lines[line];
        var tail = lines[line + 1];

        if ((head.Length == 0) || (tail.Length == 0))
            return;

        var tailStart = TextRenderer.IsColorCode(tail, 0) ? 3 : 0;
        var joined = head + " " + tail[tailStart..];
        var breakAt = head.Length; //index of the space that became the line break

        foreach (var keyword in Keywords)
        {
            if (!keyword.Contains(' '))
                continue;

            for (var index = joined.IndexOf(keyword, StringComparison.Ordinal);
                 index >= 0;
                 index = joined.IndexOf(keyword, index + 1, StringComparison.Ordinal))
            {
                var end = index + keyword.Length;

                //it has to actually straddle the break, and still be a whole word across the join
                if ((index >= breakAt) || (end <= (breakAt + 1)) || !IsWholeWord(joined, index, keyword.Length))
                    continue;

                var headLength = breakAt - index;
                var tailLength = end - breakAt - 1;

                if (Covered(line, index, headLength) || Covered(line + 1, tailStart, tailLength))
                    continue;

                KeywordRuns.Add(
                    new KeywordRun(
                        line,
                        index,
                        headLength,
                        keyword));

                KeywordRuns.Add(
                    new KeywordRun(
                        line + 1,
                        tailStart,
                        tailLength,
                        keyword));
            }
        }
    }

    /// <summary>
    ///     True when a keyword already claimed this span, which a shorter one that is its suffix would otherwise match
    ///     again.
    /// </summary>
    private bool Covered(int line, int index, int length)
    {
        foreach (var run in KeywordRuns)
            if ((run.Line == line) && (index < (run.Index + run.Length)) && (run.Index < (index + length)))
                return true;

        return false;
    }

    /// <summary>
    ///     True when the span is not part of a longer word. Punctuation and brackets are boundaries, so "[Fire]" and
    ///     "Applies Burn." both match while a keyword buried inside a longer word does not.
    /// </summary>
    /// <remarks>
    ///     The colour-code check is not optional. <see cref="TextRenderer.WrapText" /> re-opens the active colour on every
    ///     continuation line, so a wrapped line can literally begin "{=aBurn." -- and the character before the keyword is
    ///     then the code's letter, which reads as part of a word. Without this, a keyword that happened to wrap to the start
    ///     of a line would quietly lose its underline and its explanation, and which ones did would change with the panel
    ///     width.
    /// </remarks>
    private static bool IsWholeWord(string text, int index, int length)
    {
        //a code occupies the 3 chars ending at index-1
        var afterColorCode = (index >= 3) && TextRenderer.IsColorCode(text, index - 3);

        if (!afterColorCode && (index > 0) && char.IsLetter(text[index - 1]))
            return false;

        var after = index + length;

        //mirror the leading side: a colour code right after the keyword is not a boundary -- the glyph past it is,
        //so "{=aFire{=bball" reads as one on-screen word and "Fire" is not whole
        if (TextRenderer.IsColorCode(text, after))
            after += 3;

        return (after >= text.Length) || !char.IsLetter(text[after]);
    }
}
