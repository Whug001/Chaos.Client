using System.Diagnostics.CodeAnalysis;
using Chaos.Client.ViewModel.College;
using Chaos.Networking.Entities.Server;

namespace Chaos.Client.Systems.College;

/// <summary>
///     Gallery tunes by entry id, kept until logout. A tune is fetched when someone presses its Play. A fetch with no
///     reply after <see cref="FETCH_TIMEOUT" /> (the server's fetch limit drops it silently) may be sent again.
/// </summary>
public sealed class CollegeTunes
{
    public static readonly TimeSpan FETCH_TIMEOUT = TimeSpan.FromSeconds(5);

    private readonly Dictionary<int, DateTime> Requested = new();
    private readonly Dictionary<int, TuneData> Tunes = new();

    public event Action<int>? TuneReady;

    /// <summary>True when the caller should send TuneFetch now; the request is then counted as sent at <paramref name="now" />.</summary>
    public bool NeedsFetch(int id, DateTime now)
    {
        if (Tunes.ContainsKey(id) || IsWaiting(id, now))
            return false;

        Requested[id] = now;

        return true;
    }

    /// <summary>A fetch for <paramref name="id" /> is on its way and not yet timed out.</summary>
    public bool IsWaiting(int id, DateTime now)
        => !Tunes.ContainsKey(id) && Requested.TryGetValue(id, out var sent) && ((now - sent) < FETCH_TIMEOUT);

    public void OnTune(CollegeDisplayArgs args)
    {
        Tunes[args.Id] = TuneData.From(args.Tune);
        Requested.Remove(args.Id);
        TuneReady?.Invoke(args.Id);
    }

    public bool TryGet(int id, [MaybeNullWhen(false)] out TuneData tune) => Tunes.TryGetValue(id, out tune);

    public void Clear()
    {
        Tunes.Clear();
        Requested.Clear();
    }
}
