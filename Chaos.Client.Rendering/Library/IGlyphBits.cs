namespace Chaos.Client.Rendering.Library;

/// <summary>One 8 px row of a 12 px English glyph; bit 7 is the leftmost pixel.</summary>
public interface IGlyphBits
{
    byte Row(char c, int row);
}
