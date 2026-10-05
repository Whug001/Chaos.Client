using Chaos.Client.ViewModel.College;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Systems.College;

/// <summary>Gallery thumbnails by entry id: each is fetched once per session and kept until logout.</summary>
public sealed class CollegeDrawings(Func<PixelDrawing, Texture2D>? makeTexture = null)
{
    private readonly Dictionary<int, PixelDrawing> Drawings = new();
    private readonly Func<PixelDrawing, Texture2D> MakeTexture = makeTexture ?? (d => DrawingTextures.Build(d, 1));
    private readonly HashSet<int> Requested = [];
    private readonly Dictionary<int, Texture2D> Textures = new();

    public event Action<int>? DrawingReady;

    public bool Has(int id) => Drawings.ContainsKey(id);

    /// <summary>True the first time an id without a drawing is asked about; the caller then sends DrawingFetch.</summary>
    public bool NeedsFetch(int id) => !Drawings.ContainsKey(id) && Requested.Add(id);

    public void OnDrawing(CollegeDisplayArgs args)
    {
        Drawings[args.Id] = PixelDrawing.From(args.Palette, args.Pixels);
        Requested.Remove(args.Id);

        if (Textures.Remove(args.Id, out var old))
            old.Dispose();

        DrawingReady?.Invoke(args.Id);
    }

    /// <summary>The thumbnail (96 x 72), owned by this cache.</summary>
    public bool TryGetTexture(int id, out Texture2D texture)
    {
        if (Textures.TryGetValue(id, out texture!))
            return true;

        if (!Drawings.TryGetValue(id, out var drawing))
            return false;

        texture = Textures[id] = MakeTexture(drawing);

        return true;
    }

    public void Clear()
    {
        foreach (var texture in Textures.Values)
            texture.Dispose();

        Textures.Clear();
        Drawings.Clear();
        Requested.Clear();
    }
}
