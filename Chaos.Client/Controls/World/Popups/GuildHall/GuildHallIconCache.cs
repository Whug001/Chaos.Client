#region
using Chaos.Client.Rendering;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.GuildHall;

/// <summary>
///     The editor's icon textures, read from the client's embedded PNGs on first use. White-on-transparent glyphs, so
///     buttons tint them (silver normally, dark on a selected gold button). Owned here; <see cref="Clear" /> is called
///     when the editor hides.
/// </summary>
public sealed class GuildHallIconCache : IDisposable
{
    private readonly Dictionary<string, Texture2D> Cache = [];

    /// <summary>The icon for <paramref name="key" /> (a GuildHallIcons file name without extension), or null.</summary>
    public Texture2D? Get(string key)
    {
        if (Cache.TryGetValue(key, out var texture))
            return texture;

        try
        {
            using var stream = GuildHallIconAssets.Open(GuildHallIconAssets.ResourceName(key));

            //the world batches draw with premultiplied AlphaBlend, and PNGs are straight alpha
            texture = Texture2D.FromStream(ChaosGame.Device, stream, DefaultColorProcessors.PremultiplyAlpha);
        } catch
        {
            //a missing or unreadable glyph leaves the button to draw its caption alone
            return null;
        }

        Cache[key] = texture;

        return texture;
    }

    public void Clear()
    {
        foreach (var texture in Cache.Values)
            texture.Dispose();

        Cache.Clear();
    }

    public void Dispose() => Clear();
}
