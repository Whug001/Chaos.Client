#region
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.Components;

/// <summary>
///     Stores text-draw state and renders it via the font texture atlas. Re-measures only when content, color,
///     <see cref="WrapWidth" />, or <see cref="ShadowStyle" /> changes between successive <see cref="Update" /> calls.
///     No GPU resources are held — the shared font atlas handles all rendering.
/// </summary>
public sealed class TextElement
{
    private int LastWrapWidth;
    private ShadowStyle LastShadowStyle;

    public bool ColorCodesEnabled { get; set; } = true;
    public Color Color { get; private set; } = LegendColors.Silver;
    public int Height { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public int Width { get; private set; }
    public IReadOnlyList<string>? WrappedLines { get; private set; }
    public bool HasContent => Width > 0;

    /// <summary>
    ///     Width to wrap at, in pixels. Zero disables wrapping. Read by <see cref="Update" />.
    /// </summary>
    public int WrapWidth { get; set; }

    /// <summary>
    ///     Shadow style applied during <see cref="Draw" />; also widens/heightens the bounding box reported by
    ///     <see cref="Width" /> and <see cref="Height" />.
    /// </summary>
    public ShadowStyle ShadowStyle { get; set; }

    /// <summary>
    ///     Shadow color used when <see cref="ShadowStyle" /> is not <see cref="ShadowStyle.None" />.
    /// </summary>
    public Color ShadowColor { get; set; } = Color.Black;

    /// <summary>
    ///     Re-measures, and (when <see cref="WrapWidth" /> is positive) re-wraps the text. No-op when
    ///     <paramref name="text" />, <paramref name="color" />, <see cref="WrapWidth" />, and
    ///     <see cref="ShadowStyle" /> all match the previous call.
    /// </summary>
    public void Update(string text, Color color)
    {
        if ((text == Text) && (color == Color) && (WrapWidth == LastWrapWidth) && (ShadowStyle == LastShadowStyle))
            return;

        Text = text;
        Color = color;
        LastWrapWidth = WrapWidth;
        LastShadowStyle = ShadowStyle;

        if (string.IsNullOrEmpty(text))
        {
            Width = 0;
            Height = 0;
            WrappedLines = null;

            return;
        }

        if (WrapWidth > 0)
        {
            WrappedLines = TextRenderer.WrapText(text, WrapWidth);
            Width = WrapWidth;
            Height = Math.Max(TextRenderer.CHAR_HEIGHT, WrappedLines.Count * TextRenderer.CHAR_HEIGHT);

            return;
        }

        WrappedLines = null;
        var marginX = ShadowStyle switch
        {
            ShadowStyle.BothSides                              => 2,
            ShadowStyle.BottomLeft or ShadowStyle.BottomRight  => 1,
            _                                                  => 0
        };
        var marginY = ShadowStyle == ShadowStyle.None ? 0 : 1;
        Width = TextRenderer.MeasureWidth(text) + marginX;
        Height = TextRenderer.CHAR_HEIGHT + marginY;
    }

    /// <summary>
    ///     Draws <paramref name="text" /> (or <see cref="Text" /> when null) at <paramref name="position" />,
    ///     applying <see cref="ShadowStyle" /> and clipping each pass to <paramref name="clipRect" />. Pass
    ///     <see cref="Rectangle.Empty" /> (or omit) to skip clipping entirely.
    /// </summary>
    /// <summary>
    ///     Draws <paramref name="text" /> (or <see cref="Text" /> when null) at <paramref name="position" />,
    ///     applying <see cref="ShadowStyle" /> and clipping each pass to <paramref name="clipRect" />. Pass
    ///     <see cref="Rectangle.Empty" /> (or omit) to skip clipping entirely.
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, Vector2 position, Rectangle clipRect = default, string? text = null, float opacity = 1f)
    {
        text ??= Text;

        if (string.IsNullOrEmpty(text))
            return;

        switch (ShadowStyle)
        {
            case ShadowStyle.None:
                DrawClipped(spriteBatch, position, text, Color, clipRect, opacity, applyCodeColors: true);

                break;
            case ShadowStyle.BottomLeft:
                DrawClipped(spriteBatch, position + new Vector2(0, 1), text, ShadowColor, clipRect, opacity, applyCodeColors: false);
                DrawClipped(spriteBatch, position + new Vector2(1, 0), text, Color, clipRect, opacity, applyCodeColors: true);

                break;
            case ShadowStyle.BottomRight:
                DrawClipped(spriteBatch, position + new Vector2(1, 1), text, ShadowColor, clipRect, opacity, applyCodeColors: false);
                DrawClipped(spriteBatch, position, text, Color, clipRect, opacity, applyCodeColors: true);

                break;
            case ShadowStyle.BothSides:
                DrawClipped(spriteBatch, position + new Vector2(2, 1), text, ShadowColor, clipRect, opacity, applyCodeColors: false);
                DrawClipped(spriteBatch, position + new Vector2(0, 1), text, ShadowColor, clipRect, opacity, applyCodeColors: false);
                DrawClipped(spriteBatch, position + new Vector2(1, 0), text, Color, clipRect, opacity, applyCodeColors: true);

                break;
        }
    }

    /// <summary>
    ///     Underlines the <paramref name="length" /> characters of <paramref name="text" /> starting at
    ///     <paramref name="index" />, as drawn by <see cref="Draw" /> at the same <paramref name="position" />. The rule sits
    ///     on the bottom row of the glyph cell.
    /// </summary>
    /// <param name="color">
    ///     The rule's colour. Omit it to adopt the colour the underlined glyphs are drawn in -- including any {=x} code that
    ///     colours them -- so a rule under coloured text matches it without the caller working out which colour that is.
    ///     Pass one to override that, when the rule means something the text's own colour does not.
    /// </param>
    public void DrawUnderline(
        SpriteBatch spriteBatch,
        Vector2 position,
        string text,
        int index,
        int length,
        Rectangle clipRect = default,
        float opacity = 1f,
        Color? color = null)
    {
        if (string.IsNullOrEmpty(text) || (length <= 0) || (index < 0) || ((index + length) > text.Length))
            return;

        //the shadowed styles draw their main pass offset from position; the rule has to follow it
        var origin = position + MainPassOffset;
        var ruleColor = color ?? ColorAt(text, index);

        var bounds = new Rectangle(
            (int)origin.X + TextRenderer.MeasureWidth(text.AsSpan(0, index)),
            (int)origin.Y + TextRenderer.CHAR_HEIGHT - 1,
            TextRenderer.MeasureWidth(text.AsSpan(index, length)),
            1);

        if (!clipRect.IsEmpty)
        {
            bounds = Rectangle.Intersect(bounds, clipRect);

            if (bounds is { Width: <= 0 } or { Height: <= 0 })
                return;
        }

        UIElement.DrawRect(spriteBatch, bounds, ruleColor * opacity);
    }

    /// <summary>
    ///     The colour the glyph at <paramref name="index" /> is drawn in: the nearest {=x} code before it, or this element's
    ///     own <see cref="Color" /> when no code precedes it.
    /// </summary>
    private Color ColorAt(string text, int index)
    {
        if (!ColorCodesEnabled)
            return Color;

        //a code occupies 3 chars, so the nearest one able to colour index starts at index-3
        for (var i = index - 3; i >= 0; i--)
            if (TextRenderer.IsColorCode(text, i))
                return TextRenderer.GetColorCode(text[i + 2]) ?? Color;

        return Color;
    }

    /// <summary>
    ///     Offset of the main (non-shadow) text pass from the draw position, per <see cref="ShadowStyle" />.
    /// </summary>
    private Vector2 MainPassOffset
        => ShadowStyle switch
        {
            ShadowStyle.BottomLeft or ShadowStyle.BothSides => new Vector2(1, 0),
            _                                               => Vector2.Zero
        };

    private void DrawClipped(
        SpriteBatch spriteBatch,
        Vector2 position,
        string text,
        Color color,
        Rectangle clipRect,
        float opacity,
        bool applyCodeColors)
    {
        if (clipRect.IsEmpty)
        {
            TextRenderer.DrawText(spriteBatch, position, text, color, ColorCodesEnabled, opacity, applyCodeColors);

            return;
        }

        var textWidth = TextRenderer.MeasureWidth(text);
        var textBounds = new Rectangle((int)position.X, (int)position.Y, textWidth, TextRenderer.CHAR_HEIGHT);

        if (!clipRect.Intersects(textBounds))
            return;

        if (clipRect.Contains(textBounds))
        {
            TextRenderer.DrawText(spriteBatch, position, text, color, ColorCodesEnabled, opacity, applyCodeColors);

            return;
        }

        TextRenderer.DrawTextClipped(spriteBatch, position, text, color, clipRect, ColorCodesEnabled, opacity, applyCodeColors);
    }
}
