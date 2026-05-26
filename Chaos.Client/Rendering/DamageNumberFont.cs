#region
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Pre-baked glyph atlas for floating damage/heal numbers — fully programmatic (no game font). At startup
///     <see cref="Initialize" /> bakes the 10 digit glyphs (<c>0</c>–<c>9</c>) twice, once per color set (damage-red,
///     heal-green), into a single <see cref="TextureAtlas" /> page so the now-Deferred overlay pass batches every glyph
///     draw. Each glyph is the existing compact 3x5 pixel digit with an 8-direction solid-black outline (which also fills
///     the 0/8/6/9 counters) and a vertical color gradient. <see cref="Draw" /> walks a number's digits at a 4px advance —
///     the advance overlaps the shared 1px outline column, so the composite is pixel-identical to one whole-number texture.
///     Color alone signals heal vs damage; there is no <c>+</c> prefix.
/// </summary>
public static class DamageNumberFont
{
    private const int GLYPH_W = 3;
    private const int GLYPH_H = 5;

    //baked glyph cell = the 3x5 digit + a 1px outline margin on every side
    private const int CELL_W = GLYPH_W + 2;
    private const int CELL_H = GLYPH_H + 2;

    //adjacent glyphs overlap by 1px (the shared outline column) -> 4px x-advance
    private const int ADVANCE = GLYPH_W + 1;

    //two color sets baked at startup; index = (isHeal ? 10 : 0) + digit
    private const int HEAL_SET_OFFSET = 10;
    private const int GLYPH_COUNT = 20;

    //row-major 3x5 bitmaps, '1' = lit, indexed by digit 0-9
    private static readonly string[][] Digits =
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

    private static readonly Color DmgTop = new(255, 138, 138);
    private static readonly Color DmgMid = new(224, 32, 32);
    private static readonly Color DmgBot = new(110, 0, 0);
    private static readonly Color HealTop = new(185, 255, 176);
    private static readonly Color HealMid = new(63, 207, 58);
    private static readonly Color HealBot = new(12, 90, 12);

    private static TextureAtlas? Atlas;
    private static readonly AtlasRegion[] Glyphs = new AtlasRegion[GLYPH_COUNT];

    /// <summary>
    ///     The pixel height of a baked glyph (and therefore of any rendered number). Used by the overlay manager to
    ///     bottom-align a damage number to its anchor point.
    /// </summary>
    public static int GlyphHeight => CELL_H;

    /// <summary>
    ///     The pixel width a number of the given digit count occupies once drawn at the 4px advance. Used for horizontal
    ///     centering. Returns 0 for a non-positive count.
    /// </summary>
    public static int MeasureWidth(int digitCount) => digitCount <= 0 ? 0 : (ADVANCE * digitCount) + 1;

    /// <summary>
    ///     Bakes the 20 glyph textures (10 digits × {damage, heal}) into one shared atlas page. Call once at startup, after a
    ///     valid <see cref="GraphicsDevice" /> exists. Never cleared on map change — the glyph set is map-independent.
    /// </summary>
    public static void Initialize(GraphicsDevice device)
    {
        Atlas?.Dispose();
        Atlas = new TextureAtlas(
            device,
            PackingMode.Grid,
            CELL_W,
            CELL_H);

        for (var digit = 0; digit < 10; digit++)
        {
            Atlas.Add(
                digit,
                BuildGlyph(digit, false),
                CELL_W,
                CELL_H);

            Atlas.Add(
                HEAL_SET_OFFSET + digit,
                BuildGlyph(digit, true),
                CELL_W,
                CELL_H);
        }

        Atlas.Build();

        for (var i = 0; i < GLYPH_COUNT; i++)
            Glyphs[i] = Atlas.TryGetRegion(i) ?? default;
    }

    /// <summary>
    ///     Draws the digits of a number at the given top-left position, each glyph advanced 4px and tinted by
    ///     <paramref name="alpha" /> for the fade. The color set (red damage / green heal) is chosen by
    ///     <paramref name="isHeal" />. No-op before <see cref="Initialize" /> has run.
    /// </summary>
    public static void Draw(
        SpriteBatch spriteBatch,
        string digits,
        bool isHeal,
        int x,
        int y,
        float alpha)
    {
        if (Atlas is null)
            return;

        var tint = Color.White * alpha;
        var baseIndex = isHeal ? HEAL_SET_OFFSET : 0;

        for (var i = 0; i < digits.Length; i++)
        {
            var digit = digits[i] - '0';

            if ((uint)digit > 9)
                continue;

            var region = Glyphs[baseIndex + digit];

            if (region.Atlas is null)
                continue;

            spriteBatch.Draw(
                region.Atlas,
                new Vector2(x + (i * ADVANCE), y),
                region.SourceRect,
                tint);
        }
    }

    //bakes a single digit into a CELL_W x CELL_H premultiplied pixel buffer: lit pixels get the vertical color gradient,
    //the 8-direction neighborhood of any lit pixel gets a solid-black outline (the +1px margin holds the outer ring).
    private static Color[] BuildGlyph(int digit, bool isHeal)
    {
        var rows = Digits[digit];
        var (top, mid, bot) = isHeal ? (HealTop, HealMid, HealBot) : (DmgTop, DmgMid, DmgBot);

        bool Lit(int r, int c) => (r >= 0) && (r < GLYPH_H) && (c >= 0) && (c < GLYPH_W) && (rows[r][c] == '1');

        var px = new Color[CELL_W * CELL_H];

        for (var y = -1; y <= GLYPH_H; y++)
            for (var x = -1; x <= GLYPH_W; x++)
            {
                var dst = ((y + 1) * CELL_W) + (x + 1);

                if (Lit(y, x))
                    px[dst] = GradientColor(y, top, mid, bot);
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

    private static Color GradientColor(int row, Color top, Color mid, Color bot)
    {
        var t = GLYPH_H <= 1 ? 0f : row / (float)(GLYPH_H - 1);

        return t < 0.5f ? Color.Lerp(top, mid, t / 0.5f) : Color.Lerp(mid, bot, (t - 0.5f) / 0.5f);
    }
}
