#region
using Chaos.Client.Controls.World.Emblems;
using Chaos.Client.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Systems;

/// <summary>
///     Town banner post textures, one per banner design id, painted by <see cref="BannerPostPainter" /> on first use from
///     the emblem in <see cref="GuildEmblemTextures" />. Game thread only; cleared on logout. The textures belong to this
///     store: never dispose them elsewhere.
/// </summary>
public static class BannerPostTextures
{
    private static readonly Dictionary<int, Texture2D> Textures = [];
    private static readonly Dictionary<(int DesignId, int X, int Y), Texture2D?> WallCells = [];
    private static readonly Dictionary<int, Color[]> WallTexels = [];

    /// <summary>True once the banner's emblem has downloaded, so the post can be painted.</summary>
    public static bool CanDraw(int designId) => GuildEmblemTextures.TryGet(designId, out _);

    /// <summary>The post's texture, or null while the emblem is unknown.</summary>
    public static Texture2D? Get(GraphicsDevice device, int designId)
    {
        if (Textures.TryGetValue(designId, out var texture))
            return texture;

        if (!GuildEmblemTextures.TryGet(designId, out var design))
            return null;

        texture = new Texture2D(device, BannerPostPainter.WIDTH, BannerPostPainter.HEIGHT);
        texture.SetData(BannerPostPainter.Paint(design));
        Textures[designId] = texture;

        return texture;
    }

    /// <summary>
    ///     Wall tile (x, y)'s repainted wall banner (see <see cref="TownWallBannerPainter.Cell" />), or null when the
    ///     emblem is unknown or the tile shows none of the banner.
    /// </summary>
    public static Texture2D? GetWallCell(GraphicsDevice device, int designId, int x, int y, bool leftWall, bool rightWall)
    {
        if (WallCells.TryGetValue((designId, x, y), out var cached))
            return cached;

        if (!GuildEmblemTextures.TryGet(designId, out var design))
            return null;

        if (!WallTexels.TryGetValue(designId, out var texels))
            WallTexels[designId] = texels = TownWallBannerPainter.Texels(design);

        var cell = TownWallBannerPainter.Cell(texels, x, y, leftWall, rightWall);
        Texture2D? texture = null;

        if (TownWallBannerPainter.Touches(cell))
        {
            texture = new Texture2D(device, TownWallBannerPainter.CELL_WIDTH, TownWallBannerPainter.CELL_HEIGHT);
            texture.SetData(cell);
        }

        WallCells[(designId, x, y)] = texture;

        return texture;
    }

    /// <summary>Drops the wall banner tiles, which belong to one map's walls; called on a change of map.</summary>
    public static void ClearWallCells()
    {
        foreach (var texture in WallCells.Values)
            texture?.Dispose();

        WallCells.Clear();
    }

    public static void Clear()
    {
        ClearWallCells();
        WallTexels.Clear();

        foreach (var texture in Textures.Values)
            texture.Dispose();

        Textures.Clear();
    }
}
