#region
using Chaos.Client.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Pre-baked glyph atlas for floating damage/heal numbers — fully programmatic (no game font). At startup
///     <see cref="Initialize" /> bakes one <see cref="GlyphSet" /> per <see cref="DamageNumberSize" />; each set is the
///     10 digit glyphs baked twice (damage-red, heal-green) into its own Shelf-packed <see cref="TextureAtlas" /> page.
///     Each glyph is a small lit pixel digit with an 8-direction solid-black outline (which also fills the 0/8/6/9
///     counters) and a vertical color gradient. Digits are <b>variable-width</b>: each advances by its own width and
///     adjacent glyphs share a 1px outline column, so a narrow <c>1</c> sits tight instead of floating in a wide cell.
///     Sizes are distinct hand-authored bitmaps drawn 1:1 — there is no draw-time scaling. Color alone signals heal vs
///     damage; there is no <c>+</c> prefix.
/// </summary>
public static class DamageNumberFont
{
    //two color sets baked per glyph set; index = (isHeal ? 10 : 0) + digit
    private const int HEAL_SET_OFFSET = 10;
    private const int GLYPH_COUNT = 20;

    private static readonly Color DmgTop = new(255, 138, 138);
    private static readonly Color DmgMid = new(224, 32, 32);
    private static readonly Color DmgBot = new(110, 0, 0);
    private static readonly Color HealTop = new(185, 255, 176);
    private static readonly Color HealMid = new(63, 207, 58);
    private static readonly Color HealBot = new(12, 90, 12);

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

    private static readonly GlyphSet Compact = new(CompactDigits);
    private static readonly GlyphSet Normal = new(NormalDigits);
    private static readonly GlyphSet Large = new(LargeDigits);

    private static GlyphSet SetFor(DamageNumberSize size) => size switch
    {
        DamageNumberSize.Large => Large,
        DamageNumberSize.Normal => Normal,
        _ => Compact
    };

    /// <summary>The pixel height of a rendered number at the given size (used to bottom-align to the anchor).</summary>
    public static int GlyphHeight(DamageNumberSize size) => SetFor(size).CellHeight;

    /// <summary>
    ///     The pixel width the given digit string occupies at the given size (used for centering). Variable-width, so it
    ///     depends on which digits are present, not just the count.
    /// </summary>
    public static int MeasureWidth(string digits, DamageNumberSize size) => SetFor(size).MeasureWidth(digits);

    /// <summary>Bakes every size's glyph page. Call once at startup, after a valid <see cref="GraphicsDevice" /> exists.</summary>
    public static void Initialize(GraphicsDevice device)
    {
        Compact.Bake(device);
        Normal.Bake(device);
        Large.Bake(device);
    }

    /// <summary>
    ///     Draws the digits of a number at the given top-left position and size, tinted by <paramref name="alpha" /> for
    ///     the fade. The color set (red damage / green heal) is chosen by <paramref name="isHeal" />. No-op before
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
                isHeal,
                x,
                y,
                Color.White * alpha);

    private static Color GradientColor(int row, int glyphH, Color top, Color mid, Color bot)
    {
        var t = glyphH <= 1 ? 0f : row / (float)(glyphH - 1);

        return t < 0.5f ? Color.Lerp(top, mid, t / 0.5f) : Color.Lerp(mid, bot, (t - 0.5f) / 0.5f);
    }

    //A single baked size: its (variable-width) digit bitmaps, derived geometry, atlas page, and region lookup.
    private sealed class GlyphSet(string[][] digits)
    {
        private readonly string[][] Digits = digits;
        private readonly int GlyphH = digits[0].Length;
        private readonly AtlasRegion[] Glyphs = new AtlasRegion[GLYPH_COUNT];
        private TextureAtlas? Atlas;

        //baked cell = the lit digit + a 1px outline margin on every side; height is uniform across the set
        private int CellH => GlyphH + 2;

        public int CellHeight => CellH;

        //per-digit cell width (the digit's own lit width + the 1px outline margins)
        private int CellWidthFor(int digit) => Digits[digit][0].Length + 2;

        //adjacent glyphs overlap by 1px (the shared outline column), so each advances by its own width minus that column
        public int MeasureWidth(string digits)
        {
            var width = 0;
            var any = false;

            foreach (var ch in digits)
            {
                var digit = ch - '0';

                if ((uint)digit > 9)
                    continue;

                width += CellWidthFor(digit) - 1;
                any = true;
            }

            return any ? width + 1 : 0;
        }

        public void Bake(GraphicsDevice device)
        {
            Atlas?.Dispose();
            Atlas = new TextureAtlas(device, PackingMode.Shelf);

            for (var digit = 0; digit < 10; digit++)
            {
                var cellW = CellWidthFor(digit);

                Atlas.Add(
                    digit,
                    BuildGlyph(digit, false),
                    cellW,
                    CellH);

                Atlas.Add(
                    HEAL_SET_OFFSET + digit,
                    BuildGlyph(digit, true),
                    cellW,
                    CellH);
            }

            Atlas.Build();

            for (var i = 0; i < GLYPH_COUNT; i++)
                Glyphs[i] = Atlas.TryGetRegion(i) ?? default;
        }

        public void Draw(
            SpriteBatch spriteBatch,
            string digits,
            bool isHeal,
            int x,
            int y,
            Color tint)
        {
            if (Atlas is null)
                return;

            var baseIndex = isHeal ? HEAL_SET_OFFSET : 0;
            var penX = x;

            foreach (var ch in digits)
            {
                var digit = ch - '0';

                if ((uint)digit > 9)
                    continue;

                var region = Glyphs[baseIndex + digit];

                if (region.Atlas is null)
                    continue;

                spriteBatch.Draw(
                    region.Atlas,
                    new Vector2(penX, y),
                    region.SourceRect,
                    tint);

                //advance by this glyph's width; the -1 overlaps the shared 1px outline column with the next glyph
                penX += region.SourceRect.Width - 1;
            }
        }

        //bakes a single digit into a (digitWidth+2) x CellH premultiplied buffer: lit pixels get the vertical gradient;
        //the 8-direction neighborhood of any lit pixel gets a solid-black outline (the +1px margin holds the outer ring).
        private Color[] BuildGlyph(int digit, bool isHeal)
        {
            var rows = Digits[digit];
            var glyphW = rows[0].Length;
            var cellW = glyphW + 2;
            (var top, var mid, var bot) = isHeal ? (HealTop, HealMid, HealBot) : (DmgTop, DmgMid, DmgBot);

            bool Lit(int r, int c) => (r >= 0) && (r < GlyphH) && (c >= 0) && (c < glyphW) && (rows[r][c] == '1');

            var px = new Color[cellW * CellH];

            for (var y = -1; y <= GlyphH; y++)
                for (var x = -1; x <= glyphW; x++)
                {
                    var dst = ((y + 1) * cellW) + (x + 1);

                    if (Lit(y, x))
                        px[dst] = GradientColor(y, GlyphH, top, mid, bot);
                    else
                    {
                        var nearLit = Lit(y - 1, x - 1) || Lit(y - 1, x) || Lit(y - 1, x + 1)
                                      || Lit(y, x - 1) || Lit(y, x + 1)
                                      || Lit(y + 1, x - 1) || Lit(y + 1, x) || Lit(y + 1, x + 1);

                        if (nearLit)
                            px[dst] = Color.Black;
                    }
                }

            return px;
        }
    }
}
