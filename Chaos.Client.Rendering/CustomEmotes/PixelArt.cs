#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     Maps a pixel-map character to the colour it paints. Returns false for a character the palette does not define.
/// </summary>
public delegate bool PixelColorLookup(char c, out Color color);

/// <summary>
///     A small piece of hand-drawn pixel art: rows of characters plus a lookup from character to colour. '.' is
///     transparent. <see cref="Key" /> names the art in texture caches, so it must be unique across every custom emote.
/// </summary>
public sealed class PixelArt
{
    public const char TRANSPARENT = '.';

    public PixelArt(string key, IReadOnlyList<string> rows, PixelColorLookup lookup)
    {
        if ((rows.Count == 0) || (rows[0].Length == 0) || rows.Any(r => r.Length != rows[0].Length))
            throw new ArgumentException($"Pixel art '{key}' must be a non-empty rectangle.", nameof(rows));

        Key = key;
        Rows = rows;
        Lookup = lookup;
    }

    public string Key { get; }
    public IReadOnlyList<string> Rows { get; }
    public PixelColorLookup Lookup { get; }
    public int Width => Rows[0].Length;
    public int Height => Rows.Count;

    /// <summary>Builds art from a plain character-to-colour table. '.' is transparent and needs no entry.</summary>
    public static PixelArt FromPalette(string key, IReadOnlyList<string> rows, IReadOnlyDictionary<char, Color> palette)
        => new(
            key,
            rows,
            (char c, out Color color) =>
            {
                if (c == TRANSPARENT)
                {
                    color = Color.Transparent;

                    return true;
                }

                return palette.TryGetValue(c, out color);
            });

    /// <summary>A single opaque pixel, for confetti and glints.</summary>
    public static PixelArt Dot(string key, Color color) => FromPalette(key, ["x"], new Dictionary<char, Color> { ['x'] = color });

    /// <summary>
    ///     Every distinct colour this art paints, transparent pixels excluded. Throws when a character has no colour, so a
    ///     typo in a pixel map fails a test instead of silently drawing nothing.
    /// </summary>
    public IEnumerable<Color> Colors()
    {
        var seen = new HashSet<Color>();

        foreach (var row in Rows)
        foreach (var c in row)
        {
            if (c == TRANSPARENT)
                continue;

            if (!Lookup(c, out var color))
                throw new InvalidOperationException($"Pixel art '{Key}' uses '{c}', which its palette does not define.");

            if ((color.A != 0) && seen.Add(color))
                yield return color;
        }
    }
}

/// <summary>
///     One piece of art placed at an unflipped composite position. <see cref="Opacity" /> multiplies the aisling's own
///     alpha.
/// </summary>
public readonly record struct PixelLayer(PixelArt Art, int X, int Y, float Opacity = 1f);
