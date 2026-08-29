#region
using Chaos.Client.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace Chaos.Client.Controls.World.Popups.Beauty;

/// <summary>
///     Renders head-and-shoulders crops of an appearance for the picker strips and caches them by value. Each
///     crop is a fresh composite from <see cref="AislingRenderer.Render(in AislingAppearance,int,string,bool,bool?,int)" />
///     (which allocates per call and caches only per world entity), so this class owns every texture it hands out;
///     <see cref="Clear" /> is called whenever the base look changes and on hide.
/// </summary>
public sealed class HeadThumbnailRenderer(AislingRenderer renderer) : IDisposable
{
    public const int CROP_SIZE = 30;
    private const int FRONT_IDLE_FRAME = 5;

    private readonly Dictionary<AislingAppearance, Texture2D> Cache = [];

    public Texture2D? Get(in AislingAppearance appearance)
    {
        if (Cache.TryGetValue(appearance, out var cached))
            return cached;

        using var figure = renderer.Render(in appearance, FRONT_IDLE_FRAME, AislingRenderer.IDLE_ANIM, true, true);

        if (figure is null)
            return null;

        var top = Math.Clamp(FindContentTop(figure), 0, Math.Max(0, figure.Height - 1));
        var left = Math.Max(0, AislingRenderer.CANVAS_CENTER_X - (CROP_SIZE / 2));
        var width = Math.Min(CROP_SIZE, figure.Width - left);
        var height = Math.Min(CROP_SIZE, figure.Height - top);

        if ((width <= 0) || (height <= 0))
            return null;

        //copy the crop out into its own texture so the full composite can be disposed right away
        var pixels = new Color[width * height];
        figure.GetData(0, new Rectangle(left, top, width, height), pixels, 0, pixels.Length);
        var crop = new Texture2D(figure.GraphicsDevice, width, height);
        crop.SetData(pixels);

        Cache[appearance] = crop;

        return crop;
    }

    /// <summary>First row with a visible pixel -- the top of the hair. Same threshold as PokerTableControl.PortraitView.</summary>
    private static int FindContentTop(Texture2D texture)
    {
        using var scope = new PixelBufferScope(texture);
        var pixels = scope.AsSpan();

        for (var y = 0; y < scope.Height; y++)
        {
            var row = y * scope.Width;

            for (var x = 0; x < scope.Width; x++)
                if (pixels[row + x].A > 16)
                    return y;
        }

        return 0;
    }

    public void Clear()
    {
        foreach (var texture in Cache.Values)
            texture.Dispose();

        Cache.Clear();
    }

    public void Dispose() => Clear();
}
