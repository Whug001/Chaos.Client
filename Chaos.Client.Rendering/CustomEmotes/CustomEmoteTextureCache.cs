#region
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Rendering.CustomEmotes;

/// <summary>
///     Textures built for custom emotes, keyed by name. Pixel-art textures are small and shared by every aisling
///     playing the emote.
/// </summary>
public sealed class CustomEmoteTextureCache : IDisposable
{
    private readonly Dictionary<string, Texture2D?> Cache = [];

    public void Dispose() => Clear();

    /// <summary>
    ///     Returns the texture for this art, building it on first use. A null result (no graphics device yet) is not
    ///     cached, so the next draw tries again.
    /// </summary>
    public Texture2D? GetOrBuild(PixelArt art)
    {
        if (Cache.TryGetValue(art.Key, out var cached))
            return cached;

        var texture = PixelSprite.ToTexture(art);

        if (texture is not null)
            Cache[art.Key] = texture;

        return texture;
    }

    /// <summary>
    ///     Returns the texture for this key, building it on first use. A null result is cached, so a failed build (missing
    ///     or wrong-shaped source art) is not retried every frame.
    /// </summary>
    public Texture2D? GetOrBuild(string key, Func<Texture2D?> build)
    {
        if (Cache.TryGetValue(key, out var cached))
            return cached;

        var texture = build();
        Cache[key] = texture;

        return texture;
    }

    /// <summary>Disposes every cached texture. They rebuild on the next draw.</summary>
    public void Clear()
    {
        foreach (var texture in Cache.Values)
            texture?.Dispose();

        Cache.Clear();
    }
}
