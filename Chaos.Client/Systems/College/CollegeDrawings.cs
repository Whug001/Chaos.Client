using Chaos.Client.ViewModel.College;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Systems.College;

/// <summary>
///     Gallery thumbnails by entry id, kept until logout. The server drops fetches over its per-minute limit without a
///     reply, so a fetch with none is sent again as <see cref="FetchRetry" /> says.
/// </summary>
public sealed class CollegeDrawings(Func<PixelDrawing, Texture2D>? makeTexture = null)
{
    private readonly Dictionary<int, PixelDrawing> Drawings = new();
    private readonly Func<PixelDrawing, Texture2D> MakeTexture = makeTexture ?? (d => DrawingTextures.Build(d, 1));
    private readonly Dictionary<int, (DateTime SentAt, int Attempts)> Requested = new();
    private readonly Dictionary<int, Texture2D> Textures = new();

    public event Action<int>? DrawingReady;

    public bool Has(int id) => Drawings.ContainsKey(id);

    /// <summary>True when the caller should send DrawingFetch now; the request is then counted as sent at <paramref name="now" />.</summary>
    public bool NeedsFetch(int id, DateTime now)
    {
        if (Drawings.ContainsKey(id))
            return false;

        if (!Requested.TryGetValue(id, out var request))
        {
            Requested[id] = (now, 1);

            return true;
        }

        if (!FetchRetry.ShouldResend(request.SentAt, request.Attempts, now))
            return false;

        Requested[id] = (now, request.Attempts + 1);

        return true;
    }

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
