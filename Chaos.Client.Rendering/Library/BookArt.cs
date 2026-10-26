using Microsoft.Xna.Framework;

namespace Chaos.Client.Rendering.Library;

/// <summary>
///     Paints Library book art on the CPU: the shelf spine and the front cover (plain, framed picture or full
///     picture). Returns straight RGBA pixels, row by row, for the caller to upload as a texture.
/// </summary>
public static class BookArt
{
    public const int SPINE_WIDTH = 28;
    public const int SPINE_HEIGHT = 120;
    public const int COVER_WIDTH = 120;
    public const int COVER_HEIGHT = 170;

    public const int FRAME_LEFT = 15;
    public const int FRAME_TOP = 6;
    public const int FRAME_RIGHT = COVER_WIDTH - 7;
    public const int FRAME_BOTTOM = COVER_HEIGHT - 7;

    private const int ADVANCE = 6;
    private const int GLYPH_HEIGHT = 12;
    private const int HINGE = 11;
    private const string DOTS = "...";

    public static readonly Color Gold = new(196, 156, 72);
    public static readonly Color GoldLit = new(236, 204, 120);
    public static readonly Color GoldDim = new(120, 92, 40);
    public static readonly Color Ink = new(240, 218, 160);
    public static readonly Color Byline = new(170, 165, 150);
    public static readonly Color Shadow = new(10, 8, 4);
    private static readonly Color Mat = new(14, 10, 6);

    /// <summary>The spine's title plate, inclusive edges as a Rectangle (Right/Bottom exclusive).</summary>
    public static Rectangle SpinePlate { get; } = new(5, 10, SPINE_WIDTH - 10, SPINE_HEIGHT - 20);

    /// <summary>Top row of the byline text on every cover.</summary>
    public static int BylineTop => FRAME_BOTTOM - 22;

    public static int TextWidth(string text) => text.Length * ADVANCE;

    public static string Fit(string text, int room)
    {
        if (TextWidth(text) <= room)
            return text;

        if (room < DOTS.Length * ADVANCE)
            return string.Empty;

        var keep = Math.Max(0, (room / ADVANCE) - DOTS.Length);

        return text[..Math.Min(keep, text.Length)].TrimEnd() + DOTS;
    }

    public static List<string> Wrap(string text, int room, int maxLines)
    {
        var lines = new List<string>();
        var current = string.Empty;

        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = current.Length == 0 ? word : $"{current} {word}";

            if ((TextWidth(candidate) <= room) || (current.Length == 0))
                current = candidate;
            else
            {
                lines.Add(current);
                current = word;
            }
        }

        if (current.Length > 0)
            lines.Add(current);

        if (lines.Count > maxLines)
        {
            lines[maxLines - 1] = Fit($"{lines[maxLines - 1]} {lines[maxLines]}", room);
            lines.RemoveRange(maxLines, lines.Count - maxLines);
        }

        for (var i = 0; i < lines.Count; i++)
            lines[i] = Fit(lines[i], room);

        return lines;
    }

    public static Color[] PaintSpine(string title, Color fill, IGlyphBits glyphs)
    {
        const int w = SPINE_WIDTH;
        const int h = SPINE_HEIGHT;
        var canvas = new Canvas(w, h);

        canvas.Leather(fill);

        //rounded leather: lighter on the left, darker on the right
        foreach (var (x, factor) in new[] { (0, 1.35f), (1, 1.2f), (2, 1.1f), (w - 3, 0.85f), (w - 2, 0.7f), (w - 1, 0.5f) })
            for (var y = 0; y < h; y++)
                canvas.Set(x, y, Shade(canvas.Get(x, y), factor));

        canvas.HLine(1, w - 2, 0, Shade(fill, 1.4f));
        canvas.Fill(0, h - 2, w - 1, h - 1, Shade(fill, 0.5f));

        foreach (var y in new[] { 4, 7, h - 9, h - 6 })
        {
            canvas.HLine(1, w - 2, y, Gold);
            canvas.Set(2, y, GoldLit);
            canvas.Set(w - 3, y, GoldDim);
        }

        var plate = SpinePlate;
        Plate(canvas, plate.Left, plate.Top, plate.Right - 1, plate.Bottom - 1, fill);

        var text = Fit(title, plate.Height - 3);
        var textWidth = TextWidth(text) + 2;
        var left = ((plate.Left + plate.Right) / 2) - ((GLYPH_HEIGHT + 1) / 2);
        var top = ((plate.Top + plate.Bottom) / 2) - (textWidth / 2);
        canvas.TextRotated(left, top, text, Ink, glyphs);

        return canvas.Pixels;
    }

    public static Color[] PaintPlainCover(string title, string author, Color fill, IGlyphBits glyphs)
    {
        var canvas = CoverBase(fill);
        Frame(canvas);
        var below = TitlePlate(canvas, title, fill, 24, glyphs);

        var mid = (FRAME_LEFT + FRAME_RIGHT) / 2;
        canvas.HLine(mid - 14, mid + 14, below + 8, GoldDim);
        canvas.Set(mid, below + 8, GoldLit);

        DrawByline(canvas, author, glyphs);

        return canvas.Pixels;
    }

    /// <summary>Option A: the picture sits in a gold-rimmed plate between the title and the byline.</summary>
    public static Color[] PaintFramedCover(string title, string author, Color fill, Color[] art, int artWidth, int artHeight, IGlyphBits glyphs)
    {
        var canvas = CoverBase(fill);
        Frame(canvas);
        var below = TitlePlate(canvas, title, fill, 20, glyphs);
        var box = FramedArtBox(artWidth, artHeight, below);

        canvas.Fill(box.Left - 1, box.Top - 1, box.Right, box.Bottom, Mat);
        canvas.Outline(box.Left - 1, box.Top - 1, box.Right, box.Bottom, Gold);
        canvas.Blit(art, artWidth, artHeight, new Rectangle(0, 0, artWidth, artHeight), new Rectangle(box.Left + 1, box.Top + 1, box.Width - 2, box.Height - 2));
        DrawByline(canvas, author, glyphs);

        return canvas.Pixels;
    }

    /// <summary>Option B: the picture fills the cover right of the hinge; frame, title and byline sit on top.</summary>
    public static Color[] PaintFullCover(string title, string author, Color fill, Color[] art, int artWidth, int artHeight, IGlyphBits glyphs)
    {
        var canvas = CoverBase(fill);
        canvas.Blit(art, artWidth, artHeight, FullArtCrop(artWidth, artHeight), new Rectangle(HINGE, 0, COVER_WIDTH - HINGE, COVER_HEIGHT - 1));
        Frame(canvas);
        TitlePlate(canvas, title, fill, 20, glyphs);

        if (author.Length > 0)
        {
            canvas.Fill(FRAME_LEFT + 6, FRAME_BOTTOM - 25, FRAME_RIGHT - 6, FRAME_BOTTOM - 8, Shade(fill, 0.42f));
            canvas.Outline(FRAME_LEFT + 6, FRAME_BOTTOM - 25, FRAME_RIGHT - 6, FRAME_BOTTOM - 8, Gold);
        }

        DrawByline(canvas, author, glyphs);

        return canvas.Pixels;
    }

    /// <summary>Where Option A puts the picture: as large as fits between the title plate and the byline, centred.</summary>
    public static Rectangle FramedArtBox(int artWidth, int artHeight, int plateBottom)
    {
        var left = FRAME_LEFT + 7;
        var roomWidth = (FRAME_RIGHT - 6) - left;
        var top = plateBottom + 9;
        var roomHeight = (BylineTop - 5) - top;
        var scale = Math.Min(roomWidth / (float)Math.Max(1, artWidth), roomHeight / (float)Math.Max(1, artHeight));
        var width = Math.Clamp((int)MathF.Round(artWidth * scale), 1, roomWidth);
        var height = Math.Clamp((int)MathF.Round(artHeight * scale), 1, roomHeight);

        return new Rectangle(left + ((roomWidth - width) / 2), top, width, height);
    }

    /// <summary>The part of the picture Option B shows: the centre, cut to the cover's shape.</summary>
    public static Rectangle FullArtCrop(int artWidth, int artHeight)
    {
        const int areaWidth = COVER_WIDTH - HINGE;
        const int areaHeight = COVER_HEIGHT - 1;
        var scale = Math.Max(areaWidth / (float)Math.Max(1, artWidth), areaHeight / (float)Math.Max(1, artHeight));
        var width = Math.Clamp((int)MathF.Round(areaWidth / scale), 1, artWidth);
        var height = Math.Clamp((int)MathF.Round(areaHeight / scale), 1, artHeight);

        return new Rectangle((artWidth - width) / 2, (artHeight - height) / 2, width, height);
    }

    public static Color Shade(Color c, float factor)
        => new(
            (byte)Math.Clamp((int)(c.R * factor), 0, 255),
            (byte)Math.Clamp((int)(c.G * factor), 0, 255),
            (byte)Math.Clamp((int)(c.B * factor), 0, 255),
            (byte)255);

    private static Canvas CoverBase(Color fill)
    {
        const int w = COVER_WIDTH;
        const int h = COVER_HEIGHT;
        var canvas = new Canvas(w, h);

        canvas.Leather(fill);

        for (var x = 0; x < 9; x++)
            for (var y = 0; y < h; y++)
                canvas.Set(x, y, Shade(canvas.Get(x, y), 0.62f + (x * 0.02f)));

        canvas.VLine(9, 0, h - 1, Shade(fill, 0.45f));
        canvas.VLine(10, 0, h - 1, Shade(fill, 1.25f));
        canvas.VLine(w - 1, 0, h - 1, Shade(fill, 0.55f));
        canvas.HLine(0, w - 1, h - 1, Shade(fill, 0.55f));

        return canvas;
    }

    private static void Frame(Canvas canvas)
    {
        canvas.Outline(FRAME_LEFT, FRAME_TOP, FRAME_RIGHT, FRAME_BOTTOM, Gold);
        canvas.Outline(FRAME_LEFT + 3, FRAME_TOP + 3, FRAME_RIGHT - 3, FRAME_BOTTOM - 3, GoldDim);

        foreach (var (cx, cy, sx, sy) in new[] { (FRAME_LEFT, FRAME_TOP, 1, 1), (FRAME_RIGHT, FRAME_TOP, -1, 1), (FRAME_LEFT, FRAME_BOTTOM, 1, -1), (FRAME_RIGHT, FRAME_BOTTOM, -1, -1) })
        {
            canvas.Fill(Math.Min(cx, cx + (4 * sx)), Math.Min(cy, cy + (4 * sy)), Math.Max(cx, cx + (4 * sx)), Math.Max(cy, cy + (4 * sy)), Gold);
            canvas.Set(cx + (2 * sx), cy + (2 * sy), GoldLit);
            canvas.Set(cx + (6 * sx), cy + (6 * sy), Gold);
        }
    }

    private static void Plate(Canvas canvas, int x0, int y0, int x1, int y1, Color fill)
    {
        canvas.Fill(x0, y0, x1, y1, Shade(fill, 0.42f));
        canvas.Outline(x0, y0, x1, y1, Gold);
        canvas.HLine(x0 + 1, x1 - 1, y0 + 1, GoldDim);
    }

    /// <summary>Draws the cover's title plate (one or two lines) and returns its bottom row.</summary>
    private static int TitlePlate(Canvas canvas, string title, Color fill, int top, IGlyphBits glyphs)
    {
        var x0 = FRAME_LEFT + 6;
        var x1 = FRAME_RIGHT - 6;
        var lines = Wrap(title, (x1 - x0) - 6, 2);

        if (lines.Count == 0)
            lines.Add(string.Empty);

        var bottom = top + 8 + (GLYPH_HEIGHT * lines.Count);
        Plate(canvas, x0, top, x1, bottom, fill);

        for (var i = 0; i < lines.Count; i++)
            canvas.Text(x0 + (((x1 - x0) - TextWidth(lines[i])) / 2) + 1, top + 4 + (GLYPH_HEIGHT * i), lines[i], Ink, glyphs);

        return bottom;
    }

    private static void DrawByline(Canvas canvas, string author, IGlyphBits glyphs)
    {
        if (author.Length == 0)
            return;

        var text = Fit($"by {author}", (FRAME_RIGHT - FRAME_LEFT) - 12);
        canvas.Text(((FRAME_LEFT + FRAME_RIGHT) / 2) - (TextWidth(text) / 2) + 1, BylineTop, text, Byline, glyphs);
    }

    private static int Noise(int x, int y)
    {
        var h = unchecked((uint)((x * 374761393) + (y * 668265263)));
        h = unchecked((h ^ (h >> 13)) * 1274126177u);

        return (int)((h >> 16) & 7) - 3;
    }

    private sealed class Canvas(int width, int height)
    {
        public Color[] Pixels { get; } = new Color[width * height];

        public Color Get(int x, int y) => Pixels[(y * width) + x];

        public void Set(int x, int y, Color c)
        {
            if ((x >= 0) && (y >= 0) && (x < width) && (y < height))
                Pixels[(y * width) + x] = c;
        }

        public void Leather(Color fill)
        {
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                {
                    var n = Noise(x, y);
                    Pixels[(y * width) + x] = new Color(
                        (byte)Math.Clamp(fill.R + n, 0, 255),
                        (byte)Math.Clamp(fill.G + n, 0, 255),
                        (byte)Math.Clamp(fill.B + n, 0, 255),
                        (byte)255);
                }
        }

        public void HLine(int x0, int x1, int y, Color c)
        {
            for (var x = x0; x <= x1; x++)
                Set(x, y, c);
        }

        public void VLine(int x, int y0, int y1, Color c)
        {
            for (var y = y0; y <= y1; y++)
                Set(x, y, c);
        }

        public void Fill(int x0, int y0, int x1, int y1, Color c)
        {
            for (var y = y0; y <= y1; y++)
                HLine(x0, x1, y, c);
        }

        public void Outline(int x0, int y0, int x1, int y1, Color c)
        {
            HLine(x0, x1, y0, c);
            HLine(x0, x1, y1, c);
            VLine(x0, y0, y1, c);
            VLine(x1, y0, y1, c);
        }

        /// <summary>Nearest-neighbour copy of part of the art into a box on the canvas.</summary>
        public void Blit(Color[] art, int artWidth, int artHeight, Rectangle source, Rectangle target)
        {
            for (var y = 0; y < target.Height; y++)
                for (var x = 0; x < target.Width; x++)
                {
                    var sx = Math.Min(artWidth - 1, source.X + (x * source.Width / target.Width));
                    var sy = Math.Min(artHeight - 1, source.Y + (y * source.Height / target.Height));
                    var pixel = art[(sy * artWidth) + sx];
                    Set(target.X + x, target.Y + y, new Color(pixel.R, pixel.G, pixel.B, (byte)255));
                }
        }

        public void Text(int left, int top, string text, Color color, IGlyphBits glyphs)
        {
            Glyphs(text, glyphs, (gx, gy) => Set(left + gx + 1, top + gy + 1, Shadow));
            Glyphs(text, glyphs, (gx, gy) => Set(left + gx, top + gy, color));
        }

        /// <summary>Text turned 90 degrees clockwise, read top to bottom; (left, top) is the turned box's corner.</summary>
        public void TextRotated(int left, int top, string text, Color color, IGlyphBits glyphs)
        {
            //turning clockwise maps text (x, y) to (GLYPH_HEIGHT - y, x); the shadow goes one step down and left
            Glyphs(text, glyphs, (gx, gy) => Set(left + (GLYPH_HEIGHT - gy) - 1, top + gx + 1, Shadow));
            Glyphs(text, glyphs, (gx, gy) => Set(left + (GLYPH_HEIGHT - gy), top + gx, color));
        }

        private static void Glyphs(string text, IGlyphBits glyphs, Action<int, int> plot)
        {
            for (var i = 0; i < text.Length; i++)
                for (var row = 0; row < GLYPH_HEIGHT; row++)
                {
                    var bits = glyphs.Row(text[i], row);

                    for (var bit = 0; bit < 8; bit++)
                        if ((bits & (0x80 >> bit)) != 0)
                            plot((i * ADVANCE) + bit, row);
                }
        }
    }
}
