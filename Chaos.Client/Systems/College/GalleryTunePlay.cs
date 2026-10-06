namespace Chaos.Client.Systems.College;

public enum TuneRowAction
{
    None,
    Fetch,
    Play,
    Stop
}

/// <summary>
///     The gallery's Music rows: what a Play press does, and which row waits for its tune to arrive. A row waits at most
///     <see cref="CollegeTunes.FETCH_TIMEOUT" />, so a fetch the server dropped leaves its button reading Play again.
/// </summary>
public sealed class GalleryTunePlay
{
    private DateTime PendingSince;

    /// <summary>The entry whose tune is awaited (0 for none).</summary>
    public int Pending { get; private set; }

    /// <param name="playingThis">The row's own tune is playing or rendering.</param>
    /// <param name="cached">The tune is already in <see cref="CollegeTunes" />.</param>
    /// <param name="fetchDue">The cache said to send TuneFetch now.</param>
    public TuneRowAction Press(int id, bool playingThis, bool cached, bool fetchDue, DateTime now)
    {
        if (playingThis)
        {
            Pending = 0;

            return TuneRowAction.Stop;
        }

        if (cached)
        {
            Pending = 0;

            return TuneRowAction.Play;
        }

        Pending = id;
        PendingSince = now;

        return fetchDue ? TuneRowAction.Fetch : TuneRowAction.None;
    }

    /// <summary>True when the arriving tune is the awaited one; the caller then plays it.</summary>
    public bool Arrived(int id)
    {
        if ((id == 0) || (id != Pending))
            return false;

        Pending = 0;

        return true;
    }

    public void Tick(DateTime now)
    {
        if ((Pending != 0) && ((now - PendingSince) >= CollegeTunes.FETCH_TIMEOUT))
            Pending = 0;
    }

    public void Cancel() => Pending = 0;

    public static string Caption(bool playingThis, bool pending) => playingThis ? "Stop" : pending ? "Wait" : "Play";
}
