#region
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Tiny 3x5 digits for the stack level drawn in the corner of an effect-bar icon (Cunning, Fury). The effect icons
///     are half-size (15px), so neither the game font nor <see cref="CooldownNumberFont" /> fits; with the 1px black
///     outline a single digit takes a 5x7 corner of the icon.
/// </summary>
public static class StackNumberFont
{
    private static readonly DigitPalette White = new(Color.White, Color.White, Color.White);

    //row-major bitmaps, '1' = lit. Rows WITHIN a digit must be equal length; all ten digits must share the same row count.
    private static readonly string[][] Digits =
    [
        ["111", "101", "101", "101", "111"], //0
        ["010", "110", "010", "010", "111"], //1
        ["111", "001", "111", "100", "111"], //2
        ["111", "001", "011", "001", "111"], //3
        ["101", "101", "111", "001", "001"], //4
        ["111", "100", "111", "001", "111"], //5
        ["111", "100", "111", "101", "111"], //6
        ["111", "001", "010", "010", "010"], //7
        ["111", "101", "111", "101", "111"], //8
        ["111", "101", "111", "001", "111"]  //9
    ];

    private static readonly PixelDigitFont Font = new(Digits, White);

    /// <summary>The pixel height of a rendered number, including the outline margin.</summary>
    public static int GlyphHeight => Font.CellHeight;

    /// <summary>Bakes the glyph page. Call once at startup, after a valid <see cref="GraphicsDevice" /> exists.</summary>
    public static void Initialize(GraphicsDevice device) => Font.Bake(device);

    /// <summary>The pixel width the given digits occupy, including the outline margin.</summary>
    public static int MeasureWidth(string digits) => Font.MeasureWidth(digits);

    /// <summary>Draws the digits at the given top-left. No-op before <see cref="Initialize" /> has run.</summary>
    public static void Draw(
        SpriteBatch spriteBatch,
        string digits,
        int x,
        int y)
        => Font.Draw(
            spriteBatch,
            digits,
            0, //the only palette
            x,
            y,
            Color.White);
}
