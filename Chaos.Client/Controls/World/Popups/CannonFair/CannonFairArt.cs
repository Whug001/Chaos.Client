using Chaos.CannonFair;
using Chaos.Client.Data;
using Chaos.Client.Rendering;
using Chaos.Client.Utilities;
using Microsoft.Xna.Framework.Graphics;
using SkiaSharp;

namespace Chaos.Client.Controls.World.Popups.CannonFair;

public sealed class CannonFairArt : IDisposable
{
    private readonly Dictionary<(BlockMaterial Material, float Width, float Height), Texture2D> Blocks = [];
    private readonly Dictionary<(int Width, int Height), Texture2D> Plaques = [];
    private readonly SKImage StoneSource;
    private readonly SKImage WoodSource;

    public Texture2D Blast { get; }
    public Texture2D BrassBarrel { get; }
    public Texture2D Carriage { get; }
    public Texture2D Ground { get; }
    public Texture2D IronBarrel { get; }
    public Texture2D Sky { get; }

    public CannonFairArt()
    {
        StoneSource = CannonFairArtBaker.StoneSource();
        WoodSource = CannonFairArtBaker.WoodSource() ?? StoneSource;
        Sky = ToTexture(CannonFairArtBaker.Sky());
        Ground = ToTexture(CannonFairArtBaker.Ground());
        Blast = ToTexture(CannonFairArtBaker.Blast());
        IronBarrel = ToTexture(CannonFairArtBaker.Barrel(false));
        BrassBarrel = ToTexture(CannonFairArtBaker.Barrel(true));
        Carriage = ToTexture(CannonFairArtBaker.Carriage(WoodSource));
    }

    public void Dispose()
    {
        foreach (var texture in Blocks.Values.Concat(Plaques.Values).Concat([Sky, Ground, Blast, IronBarrel, BrassBarrel, Carriage]))
            texture.Dispose();

        if (WoodSource != StoneSource)
            WoodSource.Dispose();

        StoneSource.Dispose();
    }

    public Texture2D Block(BlockMaterial material, float width, float height)
    {
        var key = (material, width, height);

        if (!Blocks.TryGetValue(key, out var texture))
        {
            var source = material == BlockMaterial.Wood ? WoodSource : StoneSource;
            texture = ToTexture(CannonFairArtBaker.Block(source, width, height, material));
            Blocks[key] = texture;
        }

        return texture;
    }

    public Texture2D Plaque(int width, int height)
    {
        var key = (width, height);

        if (!Plaques.TryGetValue(key, out var texture))
        {
            using var tile = DataContext.UserControls.GetSpfImage("DlgBack2.spf")
                             ?? throw new InvalidOperationException("Failed to load DlgBack2.spf");

            var image = DialogFrame.Composite(tile, width, height)
                        ?? throw new InvalidOperationException("Failed to build the Cannon Fair panel frame");

            texture = ToTexture(image);
            Plaques[key] = texture;
        }

        return texture;
    }

    private static Texture2D ToTexture(SKImage image)
    {
        using (image)
            return TextureConverter.ToTexture2D(image);
    }
}
