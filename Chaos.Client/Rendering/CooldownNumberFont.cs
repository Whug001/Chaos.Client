#region
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Pre-baked glyph atlas for the numeric cooldown readout on skill/spell slots. The game font is a fixed 8x12 bitmap
///     and Dark Ages ships only the one English face, so a number big enough to read on a slot icon has to be authored
///     rather than scaled — see <see cref="PixelDigitFont" /> for the baking machinery this shares with
///     <see cref="DamageNumberFont" />.
///     <para>
///         Digits are 8px wide (with a narrow 5px <c>1</c>) and 10 rows tall. That is sized against
///         <c>PanelBase.ICON_SIZE</c> (32px) so that even a three-digit cooldown stays inside the icon: with the shared
///         1px outline column, the widest three-digit value ("999") measures 28px and the readout is 12px tall. Cooldowns
///         run to several minutes, so three digits is a case that really occurs, not a hypothetical.
///     </para>
///     <para>
///         White-to-slate gradient over a solid black outline, so it reads on both dark and light icon art and over the
///         blue cooldown tint.
///     </para>
/// </summary>
public static class CooldownNumberFont
{
    private static readonly DigitPalette White = new(
        new Color(255, 255, 255),
        new Color(214, 222, 235),
        new Color(140, 152, 175));

    //row-major bitmaps, '1' = lit. Rows WITHIN a digit must be equal length; all ten digits must share the same row count.
    private static readonly string[][] Digits =
    [
        ["00111100", "01111110", "11100111", "11000011", "11000011", "11000011", "11000011", "11100111", "01111110", "00111100"], //0
        ["00110", "01110", "11110", "00110", "00110", "00110", "00110", "00110", "11111", "11111"],                               //1
        ["01111100", "11111110", "11000111", "00000111", "00001110", "00011100", "00111000", "01110000", "11111111", "11111111"], //2
        ["01111100", "11111110", "11000111", "00000111", "00111110", "00111110", "00000111", "11000111", "11111110", "01111100"], //3
        ["00001110", "00011110", "00111110", "01110110", "11100110", "11111111", "11111111", "00000110", "00000110", "00000110"], //4
        ["11111111", "11111111", "11000000", "11111100", "11111110", "00000111", "00000111", "11000111", "11111110", "01111100"], //5
        ["00111110", "01111110", "11100000", "11000000", "11111100", "11111110", "11000111", "11000111", "01111110", "00111100"], //6
        ["11111111", "11111111", "00000111", "00001110", "00011100", "00011000", "00110000", "00110000", "01100000", "01100000"], //7
        ["01111100", "11111110", "11000111", "11000111", "01111110", "01111110", "11000111", "11000111", "11111110", "01111100"], //8
        ["00111100", "01111110", "11000111", "11000111", "01111111", "00111111", "00000011", "00000111", "01111110", "01111100"]  //9
    ];

    private static readonly PixelDigitFont Font = new(Digits, White);

    /// <summary>The pixel height of a rendered number, including the outline margin. Used to centre on the icon.</summary>
    public static int GlyphHeight => Font.CellHeight;

    /// <summary>Bakes the glyph page. Call once at startup, after a valid <see cref="GraphicsDevice" /> exists.</summary>
    public static void Initialize(GraphicsDevice device) => Font.Bake(device);

    /// <summary>
    ///     The pixel width the given digits occupy. Variable-width, so it depends on which digits are present, not just the
    ///     count.
    /// </summary>
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
