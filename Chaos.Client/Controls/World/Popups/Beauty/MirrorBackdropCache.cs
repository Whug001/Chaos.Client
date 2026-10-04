#region
using Chaos.Client.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Beauty;

/// <summary>
///     The mirror's backdrop pictures, read from the client's embedded PNGs on first use. <see cref="Get" /> hands out
///     one texture per (place, box size, zoom), cut on the CPU through <see cref="MirrorBackdrops.SourcePixel" /> so it
///     draws at native size with the standing tile under the figure's feet. Owns every texture it hands out;
///     <see cref="Clear" /> is called when the mirror hides.
/// </summary>
public sealed class MirrorBackdropCache : IDisposable
{
    private readonly Dictionary<(MirrorBackdrop Place, int Width, int Height, int Scale), Texture2D> Cuts = [];
    private readonly Dictionary<MirrorBackdrop, Color[]> Sources = [];
    private readonly Dictionary<MirrorBackdrop, Texture2D> Thumbnails = [];

    /// <summary>The picture for a <paramref name="width" /> x <paramref name="height" /> box at <paramref name="scale" />, or null for Plain.</summary>
    public Texture2D? Get(MirrorBackdrop place, int width, int height, int scale)
    {
        if ((place == MirrorBackdrop.Plain) || (width <= 0) || (height <= 0))
            return null;

        if (Cuts.TryGetValue((place, width, height, scale), out var cut))
            return cut;

        var source = Source(place);
        var pixels = new Color[width * height];

        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var from = MirrorBackdrops.SourcePixel(x, y, width, height, scale);

                //a box bigger than the picture leaves the pedestal showing at its edges
                if ((from.X >= 0) && (from.X < MirrorBackdrops.SOURCE_WIDTH) && (from.Y >= 0) && (from.Y < MirrorBackdrops.SOURCE_HEIGHT))
                    pixels[(y * width) + x] = source[(from.Y * MirrorBackdrops.SOURCE_WIDTH) + from.X];
            }

        cut = new Texture2D(ChaosGame.Device, width, height);
        cut.SetData(pixels);
        Cuts[(place, width, height, scale)] = cut;

        return cut;
    }

    /// <summary>The picker's 24 x 24 picture of a place, or null for Plain.</summary>
    public Texture2D? Thumbnail(MirrorBackdrop place)
    {
        if (MirrorBackdrops.Key(place) is not { } key)
            return null;

        if (Thumbnails.TryGetValue(place, out var thumbnail))
            return thumbnail;

        using var stream = MirrorBackdropAssets.Open(MirrorBackdropAssets.ThumbnailResourceName(key));
        thumbnail = Texture2D.FromStream(ChaosGame.Device, stream);
        Thumbnails[place] = thumbnail;

        return thumbnail;
    }

    private Color[] Source(MirrorBackdrop place)
    {
        if (Sources.TryGetValue(place, out var pixels))
            return pixels;

        var key = MirrorBackdrops.Key(place)!;
        using var stream = MirrorBackdropAssets.Open(MirrorBackdropAssets.ResourceName(key));
        using var texture = Texture2D.FromStream(ChaosGame.Device, stream);

        if ((texture.Width != MirrorBackdrops.SOURCE_WIDTH) || (texture.Height != MirrorBackdrops.SOURCE_HEIGHT))
            throw new InvalidOperationException(
                $"mirror backdrop {key} is {texture.Width}x{texture.Height}, expected {MirrorBackdrops.SOURCE_WIDTH}x{MirrorBackdrops.SOURCE_HEIGHT}");

        pixels = new Color[texture.Width * texture.Height];
        texture.GetData(pixels);
        Sources[place] = pixels;

        return pixels;
    }

    public void Clear()
    {
        foreach (var texture in Cuts.Values)
            texture.Dispose();

        foreach (var texture in Thumbnails.Values)
            texture.Dispose();

        Cuts.Clear();
        Sources.Clear();
        Thumbnails.Clear();
    }

    public void Dispose() => Clear();
}
