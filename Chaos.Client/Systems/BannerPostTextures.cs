#region
using Chaos.Client.Controls.World.Emblems;
using Chaos.Client.Rendering;
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

    public static void Clear()
    {
        foreach (var texture in Textures.Values)
            texture.Dispose();

        Textures.Clear();
    }
}
