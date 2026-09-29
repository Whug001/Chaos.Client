#region
using Chaos.Client.Controls.Components;
using Chaos.Client.Controls.World.Emblems;
using Chaos.Client.Definitions;
using Chaos.Client.Rendering.Definitions;
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.GuildEmblem;

/// <summary>
///     An emblem at every size the game shows it: in a world list row (1×), then at the Emblem tab's grid (2×) and large box
///     (3×) sizes. Draws <see cref="DesignId" /> from <see cref="GuildEmblemTextures" />.
/// </summary>
public sealed class GuildEmblemPreviewStrip : UIElement
{
    public const int ROW_HEIGHT = 15;
    public const int ROW_WIDTH = 90;
    public const int TOTAL_HEIGHT = 3 * SIZE;
    public const int TOTAL_WIDTH = ROW_WIDTH + GAP + (2 * SIZE) + GAP + (3 * SIZE);
    private const int GAP = 8;
    private const int SIZE = GuildEmblemProtocol.SIZE;

    public GuildEmblemPreviewStrip()
    {
        Width = TOTAL_WIDTH;
        Height = TOTAL_HEIGHT;
    }

    public int DesignId { get; set; }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!Visible)
            return;

        base.Draw(spriteBatch);

        var rowY = ScreenY + ((TOTAL_HEIGHT - ROW_HEIGHT) / 2);
        DrawRectClipped(spriteBatch, new Rectangle(ScreenX, rowY, ROW_WIDTH, ROW_HEIGHT), Color.Black);
        DrawTextClipped(spriteBatch, new Vector2(ScreenX + 3, rowY + 2), "World list", LegendColors.Gray, false);

        if (GuildEmblemTextures.Get(DesignId) is not { } texture)
            return;

        DrawTextureFitted(spriteBatch, texture, new Rectangle(ScreenX + ROW_WIDTH - SIZE - 2, rowY + 2, SIZE, SIZE), Color.White);

        var doubleX = ScreenX + ROW_WIDTH + GAP;
        DrawTextureFitted(spriteBatch, texture, new Rectangle(doubleX, ScreenY + ((TOTAL_HEIGHT - (2 * SIZE)) / 2), 2 * SIZE, 2 * SIZE), Color.White);
        DrawTextureFitted(spriteBatch, texture, new Rectangle(doubleX + (2 * SIZE) + GAP, ScreenY, 3 * SIZE, 3 * SIZE), Color.White);
    }
}
