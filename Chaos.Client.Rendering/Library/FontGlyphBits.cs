using Chaos.Client.Data;

namespace Chaos.Client.Rendering.Library;

public sealed class FontGlyphBits : IGlyphBits
{
    public static FontGlyphBits Instance { get; } = new();

    public byte Row(char c, int row)
    {
        var font = DataContext.Fonts.EnglishFont;

        if (c is < (char)33 or > (char)126 || ((c - 33) >= font.GlyphCount) || (row is < 0 or >= 12))
            return 0;

        var bytesPerRow = (font.GlyphWidth + 7) / 8;

        return font.Data[((c - 33) * bytesPerRow * 12) + (row * bytesPerRow)];
    }
}
