using Chaos.Client.Controls.Components;
using Chaos.Client.ViewModel.College;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Controls.World.Popups.College;

/// <summary>Draws the letter tiles of a <see cref="ScrambleTileLayout" />: a cream tile with a shadow edge, or a green one once revealed.</summary>
public static class ScrambleTileDrawer
{
    private static readonly Color Cream = new(232, 216, 168);
    private static readonly Color CreamShadow = new(138, 122, 80);
    private static readonly Color Green = new(138, 208, 138);
    private static readonly Color GreenShadow = new(70, 120, 70);
    private static readonly Color Ink = new(42, 32, 16);

    public static void Draw(
        SpriteBatch spriteBatch,
        ScrambleTileLayout layout,
        int originX,
        int originY,
        int tileHeight,
        bool revealed)
    {
        var fill = revealed ? Green : Cream;
        var shadow = revealed ? GreenShadow : CreamShadow;

        foreach (var tile in layout.Tiles)
        {
            var x = originX + tile.X;
            UIElement.DrawRect(spriteBatch, new Rectangle(x, originY + 2, layout.TileWidth, tileHeight - 2), shadow);
            UIElement.DrawRect(spriteBatch, new Rectangle(x, originY, layout.TileWidth, tileHeight - 2), fill);

            TextRenderer.DrawText(
                spriteBatch,
                new Vector2(x + ((layout.TileWidth - TextRenderer.CHAR_WIDTH) / 2), originY + ((tileHeight - 2 - TextRenderer.CHAR_HEIGHT) / 2)),
                tile.Letter.ToString(),
                Ink,
                false);
        }
    }
}
