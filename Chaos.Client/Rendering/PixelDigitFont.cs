#region
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     A vertical three-stop gradient applied down a glyph's lit pixels.
/// </summary>
public readonly record struct DigitPalette(Color Top, Color Mid, Color Bot);

/// <summary>
///     A hand-authored bitmap digit set baked into a Shelf-packed <see cref="TextureAtlas" /> page — the client's answer to
///     the game font not being scalable. Each digit is drawn 1:1 at its authored size (there is no draw-time scaling), gets
///     an 8-direction solid-black outline (which also fills the 0/8/6/9 counters), and a vertical colour gradient from one
///     of the supplied <see cref="DigitPalette" />s.
///     <para>
///         Digits are <b>variable-width</b>: each advances by its own width and adjacent glyphs share a 1px outline column,
///         so a narrow <c>1</c> sits tight instead of floating in a wide cell.
///     </para>
///     <para>
///         Authoring rules: rows WITHIN a digit must all be the same length, and all ten digits must share the same row
///         count. Baking one set per palette means the gradient is baked in, so a caller's tint multiplies the baked colour
///         rather than replacing it.
///     </para>
/// </summary>
public sealed class PixelDigitFont
{
    private const int DIGIT_COUNT = 10;

    private readonly string[][] Digits;
    private readonly int GlyphH;
    private readonly AtlasRegion[] Glyphs;
    private readonly DigitPalette[] Palettes;
    private TextureAtlas? Atlas;

    /// <summary>The pixel height of a rendered number — the authored rows plus the 1px outline margin above and below.</summary>
    public int CellHeight => GlyphH + 2;

    public PixelDigitFont(string[][] digits, params DigitPalette[] palettes)
    {
        if (digits.Length != DIGIT_COUNT)
            throw new ArgumentException($"a digit set needs exactly {DIGIT_COUNT} bitmaps", nameof(digits));

        if (palettes.Length == 0)
            throw new ArgumentException("a digit set needs at least one palette", nameof(palettes));

        Digits = digits;
        Palettes = palettes;
        GlyphH = digits[0].Length;
        Glyphs = new AtlasRegion[DIGIT_COUNT * palettes.Length];
    }

    /// <summary>Bakes every palette's glyph page. Call once at startup, after a valid <see cref="GraphicsDevice" /> exists.</summary>
    public void Bake(GraphicsDevice device)
    {
        Atlas?.Dispose();
        Atlas = new TextureAtlas(device, PackingMode.Shelf);

        for (var palette = 0; palette < Palettes.Length; palette++)
            for (var digit = 0; digit < DIGIT_COUNT; digit++)
                Atlas.Add(
                    Key(palette, digit),
                    BuildGlyph(digit, Palettes[palette]),
                    CellWidthFor(digit),
                    CellHeight);

        Atlas.Build();

        for (var i = 0; i < Glyphs.Length; i++)
            Glyphs[i] = Atlas.TryGetRegion(i) ?? default;
    }

    /// <summary>
    ///     Draws <paramref name="digits" /> at the given top-left, in the palette at <paramref name="paletteIndex" />.
    ///     <paramref name="tint" /> multiplies the baked colour (use <c>Color.White * alpha</c> to fade). Non-digit
    ///     characters are skipped. No-op before <see cref="Bake" /> has run.
    /// </summary>
    public void Draw(
        SpriteBatch spriteBatch,
        string digits,
        int paletteIndex,
        int x,
        int y,
        Color tint)
    {
        if (Atlas is null)
            return;

        var penX = x;

        foreach (var ch in digits)
        {
            var digit = ch - '0';

            if ((uint)digit > 9)
                continue;

            var region = Glyphs[Key(paletteIndex, digit)];

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

    /// <summary>
    ///     The pixel width <paramref name="digits" /> occupies. Variable-width, so it depends on which digits are present,
    ///     not just how many.
    /// </summary>
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

    //bakes one digit into a (digitWidth+2) x CellHeight premultiplied buffer: lit pixels take the vertical gradient; the
    //8-direction neighbourhood of any lit pixel takes a solid-black outline (the 1px margin holds that outer ring).
    private Color[] BuildGlyph(int digit, DigitPalette palette)
    {
        var rows = Digits[digit];
        var glyphW = rows[0].Length;
        var cellW = glyphW + 2;

        bool Lit(int r, int c) => (r >= 0) && (r < GlyphH) && (c >= 0) && (c < glyphW) && (rows[r][c] == '1');

        var px = new Color[cellW * CellHeight];

        for (var y = -1; y <= GlyphH; y++)
            for (var x = -1; x <= glyphW; x++)
            {
                var dst = (y + 1) * cellW + x + 1;

                if (Lit(y, x))
                    px[dst] = GradientColor(y, palette);
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

    //per-digit cell width — the digit's own lit width plus the 1px outline margin each side
    private int CellWidthFor(int digit) => Digits[digit][0].Length + 2;

    private Color GradientColor(int row, DigitPalette palette)
    {
        var t = GlyphH <= 1 ? 0f : row / (float)(GlyphH - 1);

        return t < 0.5f
            ? Color.Lerp(palette.Top, palette.Mid, t / 0.5f)
            : Color.Lerp(palette.Mid, palette.Bot, (t - 0.5f) / 0.5f);
    }

    private int Key(int paletteIndex, int digit) => paletteIndex * DIGIT_COUNT + digit;
}
