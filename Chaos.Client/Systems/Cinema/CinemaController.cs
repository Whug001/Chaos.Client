using Chaos.Networking.Entities.Server;

namespace Chaos.Client.Systems.Cinema;

public enum CinemaStatus
{
    /// <summary>Nothing is showing.</summary>
    Idle,

    /// <summary>A film is showing (its video may be hidden by the player's settings).</summary>
    Playing,

    /// <summary>The film's video failed; a retry is waiting.</summary>
    Unavailable
}

/// <summary>
///     Keeps a video player in step with the server's <see cref="CinemaStateArgs" />. Times are the caller's
///     millisecond clock (Environment.TickCount64 in the game).
/// </summary>
public sealed class CinemaController(Func<ICinemaVideo> createVideo, Func<bool>? videoReady = null)
{
    private long AnchorNowMs;
    private long AnchorPositionMs;
    private long NextCheckMs;
    private long RetryAtMs;
    private long EndedSinceMs;
    private bool ShowVideo = true;
    private bool WaitingForVideo;
    private int Volume = 80;
    private bool Mute;

    public CinemaStatus Status { get; private set; }
    public CinemaStateArgs? State { get; private set; }
    public ICinemaVideo? Video { get; private set; }

    public bool IsShowing => State is not null;

    public void Apply(CinemaStateArgs args, long nowMs, long? pingMs)
    {
        if (string.IsNullOrEmpty(args.Url))
        {
            Stop();

            return;
        }

        var sameFilm = State is not null && (State.FilmId == args.FilmId) && (State.Url == args.Url);
        var start = args.Paused ? args.PositionMs : CinemaSync.StartPosition(args.PositionMs, pingMs);

        State = args;
        AnchorNowMs = nowMs;
        AnchorPositionMs = start;

        if (!ShowVideo)
        {
            Status = CinemaStatus.Playing;

            return;
        }

        if (sameFilm && Video is not null)
        {
            if (Video.Ended)
            {
                Open(start, nowMs);

                return;
            }

            Video.SetPaused(args.Paused);
            Video.Seek(start);
            NextCheckMs = nowMs + 2 * CinemaSync.CHECK_INTERVAL_MS;

            return;
        }

        if (sameFilm && Status == CinemaStatus.Unavailable)
            return;

        Open(start, nowMs);
    }

    public void Update(long nowMs)
    {
        if (State is null || !ShowVideo)
            return;

        if (WaitingForVideo)
        {
            if (videoReady!())
                Open(ExpectedPositionMs(nowMs), nowMs);

            return;
        }

        if (Status == CinemaStatus.Unavailable)
        {
            if (nowMs >= RetryAtMs)
                Open(ExpectedPositionMs(nowMs), nowMs);

            return;
        }

        if (Video is null)
            return;

        if (Video.Failed)
        {
            MarkUnavailable(nowMs);

            return;
        }

        if (Video.Ended)
        {
            if (EndedSinceMs == 0)
                EndedSinceMs = nowMs;
            else if (nowMs - EndedSinceMs >= CinemaSync.ENDED_GRACE_MS)
            {
                MarkUnavailable(nowMs);

                return;
            }
        }
        else
            EndedSinceMs = 0;

        Video.UploadFrame();

        if (nowMs < NextCheckMs)
            return;

        NextCheckMs = nowMs + CinemaSync.CHECK_INTERVAL_MS;

        //VLC reads 0 while it fills its cache, so a check then would seek again and again
        if (State.Paused || !Video.Playing)
            return;

        var expected = ExpectedPositionMs(nowMs);

        if (CinemaSync.NeedsSeek(Video.TimeMs, expected))
        {
            Video.Seek(expected);
            NextCheckMs = nowMs + 2 * CinemaSync.CHECK_INTERVAL_MS;
        }
    }

    public long ExpectedPositionMs(long nowMs)
    {
        if (State is null)
            return 0;

        return State.Paused ? AnchorPositionMs : AnchorPositionMs + (nowMs - AnchorNowMs);
    }

    public void SetShowVideo(bool show, long nowMs)
    {
        if (show == ShowVideo)
            return;

        ShowVideo = show;

        if (!show)
        {
            DisposeVideo();

            WaitingForVideo = false;

            if (State is not null)
                Status = CinemaStatus.Playing;

            return;
        }

        if (State is not null)
            Open(ExpectedPositionMs(nowMs), nowMs);
    }

    public void SetVolume(int volume, bool mute)
    {
        if ((volume == Volume) && (mute == Mute))
            return;

        Volume = volume;
        Mute = mute;
        Video?.SetVolume(volume, mute);
    }

    public void Stop()
    {
        DisposeVideo();
        WaitingForVideo = false;
        State = null;
        Status = CinemaStatus.Idle;
    }

    public bool ShouldDuckMusic(bool lowerMusic)
        => lowerMusic && ShowVideo && State is { Paused: false } && (Status == CinemaStatus.Playing);

    private void Open(long startMs, long nowMs)
    {
        EndedSinceMs = 0;
        DisposeVideo();

        if ((videoReady is not null) && !videoReady())
        {
            WaitingForVideo = true;
            Status = CinemaStatus.Playing;

            return;
        }

        WaitingForVideo = false;

        ICinemaVideo? video = null;

        try
        {
            //the factory builds a LibVLC player, which throws when LibVLC is missing
            video = createVideo();
            Video = video;
            video.SetVolume(Volume, Mute);
            video.Open(State!.Url, startMs);

            if (State.Paused)
                video.SetPaused(true);

            Status = CinemaStatus.Playing;
            NextCheckMs = nowMs + CinemaSync.CHECK_INTERVAL_MS;
        } catch (Exception)
        {
            MarkUnavailable(nowMs);
        }
    }

    private void MarkUnavailable(long nowMs)
    {
        DisposeVideo();
        Status = CinemaStatus.Unavailable;
        RetryAtMs = nowMs + CinemaSync.RETRY_INTERVAL_MS;
    }

    private void DisposeVideo()
    {
        Video?.Dispose();
        Video = null;
    }
}
