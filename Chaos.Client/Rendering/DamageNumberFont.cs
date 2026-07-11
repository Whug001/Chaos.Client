#region
using Chaos.Client.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Pre-baked glyph atlas for floating damage/heal numbers — fully programmatic (no game font). At startup
///     <see cref="Initialize" /> bakes one <see cref="PixelDigitFont" /> per <see cref="DamageNumberSize" />; each is the 10
///     digit glyphs baked twice (damage-red, heal-green). Sizes are distinct hand-authored bitmaps drawn 1:1 — there is no
///     draw-time scaling. Colour alone signals heal vs damage; there is no <c>+</c> prefix.
/// </summary>
public static class DamageNumberFont
{
    //palette order within each set; the index is passed straight to PixelDigitFont.Draw
    private const int DAMAGE_PALETTE = 0;
    private const int HEAL_PALETTE = 1;

    private static readonly DigitPalette Damage = new(
        new Color(255, 138, 138),
        new Color(224, 32, 32),
        new Color(110, 0, 0));

    private static readonly DigitPalette Heal = new(
        new Color(185, 255, 176),
        new Color(63, 207, 58),
        new Color(12, 90, 12));

    //row-major bitmaps, '1' = lit, indexed by digit 0-9. The Compact set is the original 3-wide digit (uniform width).
    private static readonly string[][] CompactDigits =
    [
        ["111", "101", "101", "101", "111"], //0
        ["010", "110", "010", "010", "111"], //1
        ["111", "001", "111", "100", "111"], //2
        ["111", "001", "111", "001", "111"], //3
        ["101", "101", "111", "001", "001"], //4
        ["111", "100", "111", "001", "111"], //5
        ["111", "100", "111", "101", "111"], //6
        ["111", "001", "010", "010", "010"], //7
        ["111", "101", "111", "101", "111"], //8
        ["111", "101", "111", "001", "111"]  //9
    ];

    //Normal = hand-authored bubble digits: 6 rows tall, fat 2px strokes with rounded corners. Variable width: rows WITHIN
    //a digit must be equal length, and all 10 digits must share the same height (6 rows). '1' is intentionally narrow (4 wide).
    private static readonly string[][] NormalDigits =
    [
        ["01110", "11011", "11011", "11011", "11011", "01110"], //0
        ["0110", "1110", "0110", "0110", "0110", "1111"],       //1
        ["01110", "11011", "00011", "00110", "01100", "11111"], //2
        ["01110", "11011", "00011", "00110", "10011", "01110"], //3
        ["11011", "11011", "11011", "11111", "00011", "00011"], //4
        ["11111", "11000", "11110", "00011", "11011", "01110"], //5
        ["01110", "11000", "11110", "11011", "11011", "01110"], //6
        ["11111", "00011", "00110", "01100", "01100", "01100"], //7
        ["01110", "11011", "01110", "11011", "11011", "01110"], //8
        ["01110", "11011", "11011", "01111", "00011", "01110"]  //9
    ];

    //Large = hand-authored bubble digits: 7 rows tall, fat 2px strokes with rounded corners. Variable width: rows WITHIN a
    //digit must be equal length, and all 10 digits must share the same height (7 rows). '1' is intentionally narrow (4 wide).
    private static readonly string[][] LargeDigits =
    [
        ["011110", "110011", "110011", "110011", "110011", "110011", "011110"], //0
        ["0110", "1110", "0110", "0110", "0110", "0110", "1111"],               //1
        ["011110", "110011", "000011", "000110", "001100", "011000", "111111"], //2
        ["011110", "110011", "000011", "001110", "000011", "110011", "011110"], //3
        ["110011", "110011", "111111", "111111", "000011", "000011", "000011"], //4
        ["111111", "110000", "111110", "000011", "110011", "110011", "011110"], //5
        ["011110", "110000", "110000", "111110", "110011", "110011", "011110"], //6
        ["111111", "000011", "000110", "001100", "001100", "001100", "001100"], //7
        ["011110", "110011", "110011", "011110", "110011", "110011", "011110"], //8
        ["011110", "110011", "110011", "011111", "000011", "000011", "011110"]  //9
    ];

    private static readonly PixelDigitFont Compact = new(CompactDigits, Damage, Heal);
    private static readonly PixelDigitFont Normal = new(NormalDigits, Damage, Heal);
    private static readonly PixelDigitFont Large = new(LargeDigits, Damage, Heal);

    /// <summary>The pixel height of a rendered number at the given size (used to bottom-align to the anchor).</summary>
    public static int GlyphHeight(DamageNumberSize size) => SetFor(size).CellHeight;

    /// <summary>Bakes every size's glyph page. Call once at startup, after a valid <see cref="GraphicsDevice" /> exists.</summary>
    public static void Initialize(GraphicsDevice device)
    {
        Compact.Bake(device);
        Normal.Bake(device);
        Large.Bake(device);
    }

    /// <summary>
    ///     The pixel width the given digit string occupies at the given size (used for centering). Variable-width, so it
    ///     depends on which digits are present, not just the count.
    /// </summary>
    public static int MeasureWidth(string digits, DamageNumberSize size) => SetFor(size).MeasureWidth(digits);

    /// <summary>
    ///     Draws the digits of a number at the given top-left position and size, tinted by <paramref name="alpha" /> for
    ///     the fade. The colour set (red damage / green heal) is chosen by <paramref name="isHeal" />. No-op before
    ///     <see cref="Initialize" /> has run.
    /// </summary>
    public static void Draw(
        SpriteBatch spriteBatch,
        string digits,
        bool isHeal,
        int x,
        int y,
        float alpha,
        DamageNumberSize size)
        => SetFor(size)
            .Draw(
                spriteBatch,
                digits,
                isHeal ? HEAL_PALETTE : DAMAGE_PALETTE,
                x,
                y,
                Color.White * alpha);

    private static PixelDigitFont SetFor(DamageNumberSize size)
        => size switch
        {
            DamageNumberSize.Large  => Large,
            DamageNumberSize.Normal => Normal,
            _                       => Compact
        };
}
