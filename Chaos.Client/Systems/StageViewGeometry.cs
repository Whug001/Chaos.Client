#region
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Systems;

/// <summary>
///     Maps between map tiles and pixels in the Stage Lighting window's diamond view, which draws the stage at the game's
///     angle (+X goes down-right, +Y goes down-left). Tile centres are whole numbers; the stage's centre tile sits in the
///     middle of the view.
/// </summary>
public static class StageViewGeometry
{
    public const float HALF_TILE_WIDTH = 11f;
    public const float HALF_TILE_HEIGHT = 5.5f;

    public static Vector2 TileToView(Vector2 tile, Rectangle stage, int viewWidth, int viewHeight)
    {
        var centre = Centre(stage);
        var u = tile.X - centre.X;
        var v = tile.Y - centre.Y;

        return new Vector2((viewWidth / 2f) + ((u - v) * HALF_TILE_WIDTH), (viewHeight / 2f) + ((u + v) * HALF_TILE_HEIGHT));
    }

    public static Vector2 ViewToTile(Vector2 view, Rectangle stage, int viewWidth, int viewHeight)
    {
        var centre = Centre(stage);
        var a = (view.X - (viewWidth / 2f)) / HALF_TILE_WIDTH;
        var b = (view.Y - (viewHeight / 2f)) / HALF_TILE_HEIGHT;

        return new Vector2(centre.X + ((a + b) / 2f), centre.Y + ((b - a) / 2f));
    }

    /// <summary>A fractional tile as wire units (sixteenths), held to the stage's tile centres.</summary>
    public static (ushort X, ushort Y) ToUnits(Vector2 tile, Rectangle stage)
    {
        var x = Math.Clamp(tile.X, stage.Left, stage.Right - 1);
        var y = Math.Clamp(tile.Y, stage.Top, stage.Bottom - 1);

        return ((ushort)MathF.Round(x * StageLightInfo.UNITS_PER_TILE), (ushort)MathF.Round(y * StageLightInfo.UNITS_PER_TILE));
    }

    public static Vector2 Centre(Rectangle stage) => new(stage.Left + ((stage.Width - 1) / 2f), stage.Top + ((stage.Height - 1) / 2f));
}
