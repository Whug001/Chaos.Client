using System.Globalization;
using Chaos.Client.ViewModel.College;
using Chaos.Networking.Entities.Server;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Systems.College;

/// <summary>
///     Gallery thumbnails by entry id, at most <see cref="KEPT_DRAWINGS" /> besides those a window holds
///     (<see cref="Hold" />), until logout. A dropped drawing is fetched again when it is next shown. The server drops
///     fetches over its per-minute limit without a reply, so a fetch with none is sent again as <see cref="FetchRetry" />
///     says.
/// </summary>
public sealed class CollegeDrawings(Func<PixelDrawing, Texture2D>? makeTexture = null)
{
    public const int KEPT_DRAWINGS = 64;

    private readonly HeldCache<Entry> Entries = new(KEPT_DRAWINGS, entry => entry.Texture?.Dispose());
    private readonly Func<PixelDrawing, Texture2D> MakeTexture = makeTexture ?? (d => DrawingTextures.Build(d, 1));
    private readonly Dictionary<int, (DateTime SentAt, int Attempts)> Requested = new();

    public event Action<int>? DrawingReady;

    public bool Has(int id) => Entries.Contains(Key(id));

    /// <summary>True when the caller should send DrawingFetch now; the request is then counted as sent at <paramref name="now" />.</summary>
    public bool NeedsFetch(int id, DateTime now)
    {
        if (Has(id))
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

    /// <summary>True when the drawing's fetches ran out with no reply; the caller should stop asking and fall back.</summary>
    public bool HasGivenUp(int id, DateTime now)
        => !Has(id)
           && Requested.TryGetValue(id, out var request)
           && FetchRetry.HasGivenUp(request.SentAt, request.Attempts, now);

    public void OnDrawing(CollegeDisplayArgs args)
    {
        Entries.Add(Key(args.Id), new Entry(PixelDrawing.From(args.Palette, args.Pixels)));
        Requested.Remove(args.Id);

        DrawingReady?.Invoke(args.Id);
    }

    /// <summary>The thumbnail (96 x 72), owned by this cache; <see cref="Hold" /> it while it is shown.</summary>
    public bool TryGetTexture(int id, out Texture2D texture)
    {
        if (!Entries.TryGet(Key(id), out var entry))
        {
            texture = null!;

            return false;
        }

        texture = entry.Texture ??= MakeTexture(entry.Drawing);

        return true;
    }

    public void Hold(int id) => Entries.Hold(Key(id));

    public void Release(int id) => Entries.Release(Key(id));

    public void Clear()
    {
        Entries.Clear();
        Requested.Clear();
    }

    private static string Key(int id) => id.ToString(CultureInfo.InvariantCulture);

    private sealed class Entry(PixelDrawing drawing)
    {
        public PixelDrawing Drawing { get; } = drawing;
        public Texture2D? Texture { get; set; }
    }
}
