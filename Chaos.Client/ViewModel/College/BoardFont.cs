using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;

namespace Chaos.Client.ViewModel.College;

/// <summary>The board's text tool font: 5 px tall glyphs, one pixel apart. Large draws each pixel as 2 x 2.</summary>
public static class BoardFont
{
    public const int HEIGHT = 5;
    public const int GAP = 1;

    private static readonly string[] Space = ["...", "...", "...", "...", "..."];

    private static readonly Dictionary<char, string[]> Glyphs = new()
    {
        ['A'] = [".#.", "#.#", "###", "#.#", "#.#"],
        ['B'] = ["##.", "#.#", "##.", "#.#", "##."],
        ['C'] = [".##", "#..", "#..", "#..", ".##"],
        ['D'] = ["##.", "#.#", "#.#", "#.#", "##."],
        ['E'] = ["###", "#..", "##.", "#..", "###"],
        ['F'] = ["###", "#..", "##.", "#..", "#.."],
        ['G'] = [".##", "#..", "#.#", "#.#", ".##"],
        ['H'] = ["#.#", "#.#", "###", "#.#", "#.#"],
        ['I'] = ["###", ".#.", ".#.", ".#.", "###"],
        ['J'] = ["..#", "..#", "..#", "#.#", ".#."],
        ['K'] = ["#.#", "#.#", "##.", "#.#", "#.#"],
        ['L'] = ["#..", "#..", "#..", "#..", "###"],
        ['M'] = ["#...#", "##.##", "#.#.#", "#...#", "#...#"],
        ['N'] = ["#..#", "##.#", "#.##", "#..#", "#..#"],
        ['O'] = [".#.", "#.#", "#.#", "#.#", ".#."],
        ['P'] = ["##.", "#.#", "##.", "#..", "#.."],
        ['Q'] = [".#.", "#.#", "#.#", "##.", ".##"],
        ['R'] = ["##.", "#.#", "##.", "#.#", "#.#"],
        ['S'] = [".##", "#..", ".#.", "..#", "##."],
        ['T'] = ["###", ".#.", ".#.", ".#.", ".#."],
        ['U'] = ["#.#", "#.#", "#.#", "#.#", "###"],
        ['V'] = ["#.#", "#.#", "#.#", "#.#", ".#."],
        ['W'] = ["#...#", "#...#", "#.#.#", "##.##", "#...#"],
        ['X'] = ["#.#", "#.#", ".#.", "#.#", "#.#"],
        ['Y'] = ["#.#", "#.#", ".#.", ".#.", ".#."],
        ['Z'] = ["###", "..#", ".#.", "#..", "###"],
        ['0'] = ["###", "#.#", "#.#", "#.#", "###"],
        ['1'] = [".#.", "##.", ".#.", ".#.", "###"],
        ['2'] = ["##.", "..#", ".#.", "#..", "###"],
        ['3'] = ["##.", "..#", ".#.", "..#", "##."],
        ['4'] = ["#.#", "#.#", "###", "..#", "..#"],
        ['5'] = ["###", "#..", "##.", "..#", "##."],
        ['6'] = [".##", "#..", "###", "#.#", "###"],
        ['7'] = ["###", "..#", ".#.", ".#.", ".#."],
        ['8'] = ["###", "#.#", "###", "#.#", "###"],
        ['9'] = ["###", "#.#", "###", "..#", "##."],
        ['.'] = [".", ".", ".", ".", "#"],
        [','] = ["..", "..", "..", ".#", "#."],
        ['!'] = ["#", "#", "#", ".", "#"],
        ['?'] = ["##.", "..#", ".#.", "...", ".#."],
        ['\''] = ["#", "#", ".", ".", "."],
        ['-'] = ["...", "...", "###", "...", "..."],
        [':'] = [".", "#", ".", "#", "."],
        ['('] = [".#", "#.", "#.", "#.", ".#"],
        [')'] = ["#.", ".#", ".#", ".#", "#."],
        ['/'] = ["..#", "..#", ".#.", "#..", "#.."],
        ['+'] = ["...", ".#.", "###", ".#.", "..."],
        ['='] = ["...", "###", "...", "###", "..."],
        [' '] = Space
    };

    public static int MeasureWidth(string text, int scale)
    {
        var width = text.Sum(c => (GlyphOf(c)[0].Length + GAP) * scale);

        return Math.Max(0, width - (GAP * scale));
    }

    /// <summary>Every lit pixel of <paramref name="text" /> with its top-left at (<paramref name="x" />, <paramref name="y" />).</summary>
    public static IEnumerable<Point> Pixels(string text, int x, int y, int scale)
    {
        var left = x;

        foreach (var c in text)
        {
            var glyph = GlyphOf(c);

            for (var row = 0; row < HEIGHT; row++)
                for (var col = 0; col < glyph[row].Length; col++)
                    if (glyph[row][col] == '#')
                        for (var dy = 0; dy < scale; dy++)
                            for (var dx = 0; dx < scale; dx++)
                                yield return new Point(left + (col * scale) + dx, y + (row * scale) + dy);

            left += (glyph[0].Length + GAP) * scale;
        }
    }

    /// <summary>The typed text as the board will draw it: drawable characters only, upper case, at most 40.</summary>
    public static string FilterText(string text)
        => new(text.Select(char.ToUpperInvariant).Where(Glyphs.ContainsKey).Take(CollegeProtocol.MAX_BOARD_TEXT_CHARS).ToArray());

    private static string[] GlyphOf(char c) => Glyphs.GetValueOrDefault(char.ToUpperInvariant(c), Space);
}
