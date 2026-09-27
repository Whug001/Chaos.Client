#region
using Chaos.Client.Rendering;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Emblems;

/// <summary>
///     Emblem art: <c>emblNNN.spf</c> in setoa.dat, by art number. Animated emblems advance one frame every
///     <see cref="FRAME_MS" /> from one shared clock, so every copy of an emblem stays in step. The textures are
///     <see cref="UiRenderer" />'s cached ones: never dispose them.
/// </summary>
public static class EmblemTextures
{
    public const int FRAME_MS = 120;

    //frame counts by art number; 0 = the file is missing. Read on the UI thread only.
    private static readonly Dictionary<ushort, int> FrameCounts = [];

    public static string FileName(ushort art) => $"embl{art:D3}.spf";

    public static int FrameAt(int frameCount, long clockMs) => frameCount <= 1 ? 0 : (int)(clockMs / FRAME_MS % frameCount);

    /// <summary>The emblem's frame for <paramref name="clockMs" />, or null for art 0 or art missing from setoa.dat.</summary>
    public static Texture2D? Get(ushort art, long clockMs)
    {
        if ((art == 0) || UiRenderer.Instance is not { } cache)
            return null;

        var fileName = FileName(art);

        if (!FrameCounts.TryGetValue(art, out var count))
            FrameCounts[art] = count = cache.GetSpfFrameCount(fileName);

        return count == 0 ? null : cache.GetSpfTexture(fileName, FrameAt(count, clockMs));
    }
}
