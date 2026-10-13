namespace Chaos.Client.ViewModel.College;

public readonly record struct ScrambleTile(int X, char Letter);

/// <summary>Where each letter tile of a scramble sits on one line: letters share a width that shrinks to fit, a space is a gap.</summary>
public sealed class ScrambleTileLayout
{
    public const int TILE_GAP = 2;
    public const int SPACE_WIDTH = 8;

    public ScrambleTileLayout(string text, int innerWidth, int maxTileWidth)
    {
        var spaces = text.Count(c => c == ' ');
        var letters = text.Length - spaces;
        var gaps = Math.Max(0, text.Length - 1) * TILE_GAP;
        var room = innerWidth - (spaces * SPACE_WIDTH) - gaps;
        TileWidth = letters == 0 ? maxTileWidth : Math.Clamp(room / letters, 1, maxTileWidth);
        TotalWidth = (letters * TileWidth) + (spaces * SPACE_WIDTH) + gaps;

        var tiles = new List<ScrambleTile>();
        var x = Math.Max(0, (innerWidth - TotalWidth) / 2);

        foreach (var c in text)
        {
            if (c != ' ')
                tiles.Add(new ScrambleTile(x, c));

            x += (c == ' ' ? SPACE_WIDTH : TileWidth) + TILE_GAP;
        }

        Tiles = tiles;
    }

    public int TileWidth { get; }
    public int TotalWidth { get; }
    public IReadOnlyList<ScrambleTile> Tiles { get; }
}
