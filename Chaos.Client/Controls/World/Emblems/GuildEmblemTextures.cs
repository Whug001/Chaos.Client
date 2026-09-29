#region
using System.Diagnostics.CodeAnalysis;
using Chaos.Client.Rendering;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Emblems;

/// <summary>
///     Guild emblems by id, and their 11 × 11 textures (built on first draw). Positive ids come from the server; the client
///     asks for each missing one at most once every <see cref="REQUEST_RETRY_MS" />. Negative ids are emblems painted on
///     this client (the editor's draft, an emblem under review): every change gets a new id. Game thread only; cleared on
///     logout. The textures belong to this store: never dispose them elsewhere.
/// </summary>
public static class GuildEmblemTextures
{
    public const long REQUEST_RETRY_MS = 10_000;

    private static readonly Dictionary<int, GuildEmblemDesign> Designs = [];
    private static readonly Dictionary<int, long> RequestedAtMs = [];
    private static readonly Dictionary<int, Texture2D> Textures = [];
    private static int NextLocalId = -1;

    /// <summary>
    ///     Forgets every emblem, texture and pending request, for a new session. Local ids keep counting down, so an emblem
    ///     painted before the clear and one painted after never collide.
    /// </summary>
    public static void Clear()
    {
        Designs.Clear();
        RequestedAtMs.Clear();

        foreach (var texture in Textures.Values)
            texture.Dispose();

        Textures.Clear();
    }

    /// <summary>The emblem's texture, built on first use; null while the emblem is unknown or for id 0.</summary>
    public static Texture2D? Get(int designId)
    {
        if ((designId == 0) || !Designs.TryGetValue(designId, out var design))
            return null;

        if (!Textures.TryGetValue(designId, out var texture))
        {
            texture = new Texture2D(TextureConverter.Device, GuildEmblemProtocol.SIZE, GuildEmblemProtocol.SIZE);
            texture.SetData(ToPixels(design));
            Textures[designId] = texture;
        }

        return texture;
    }

    public static void Set(int designId, GuildEmblemDesign design)
    {
        DropTexture(designId);
        Designs[designId] = design;
        RequestedAtMs.Remove(designId);
    }

    /// <summary>Stores an emblem painted on this client under a new negative id, and drops the local emblem it replaces.</summary>
    public static int SetLocal(int replacedId, GuildEmblemDesign design)
    {
        if (replacedId < 0)
        {
            DropTexture(replacedId);
            Designs.Remove(replacedId);
        }

        var id = NextLocalId--;
        Designs[id] = design;

        return id;
    }

    /// <summary>True when a server emblem is missing and was not asked for in the last <see cref="REQUEST_RETRY_MS" />.</summary>
    public static bool ShouldRequest(int designId, long nowMs)
    {
        if ((designId <= 0) || Designs.ContainsKey(designId))
            return false;

        if (RequestedAtMs.TryGetValue(designId, out var askedAt) && ((nowMs - askedAt) < REQUEST_RETRY_MS))
            return false;

        RequestedAtMs[designId] = nowMs;

        return true;
    }

    /// <summary>The emblem's pixels, row-major. See-through (0) and out-of-range numbers are transparent.</summary>
    public static Color[] ToPixels(GuildEmblemDesign design)
    {
        var pixels = new Color[GuildEmblemProtocol.PIXEL_COUNT];

        for (var i = 0; (i < pixels.Length) && (i < design.Pixels.Length); i++)
        {
            var number = design.Pixels[i];

            if ((number == 0) || (number > design.Colors.Count))
                continue;

            var color = design.Colors[number - 1];
            pixels[i] = new Color(color.R, color.G, color.B);
        }

        return pixels;
    }

    public static bool TryGet(int designId, [MaybeNullWhen(false)] out GuildEmblemDesign design)
    {
        design = null;

        return (designId != 0) && Designs.TryGetValue(designId, out design);
    }

    private static void DropTexture(int designId)
    {
        if (Textures.Remove(designId, out var texture))
            texture.Dispose();
    }
}
