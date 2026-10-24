using System.Globalization;
using System.Runtime.InteropServices;
using LibVLCSharp.Shared;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Systems.Cinema;

/// <summary>Streams one film with LibVLC and hands its frames to the game as a texture.</summary>
public sealed class CinemaPlayer : ICinemaVideo
{
    private const int WIDTH = CinemaScreenLayout.VIDEO_WIDTH;
    private const int HEIGHT = CinemaScreenLayout.VIDEO_HEIGHT;
    private const int PITCH = WIDTH * 4;
    private const int FRAME_BYTES = PITCH * HEIGHT;

    private static readonly Lazy<LibVLC> Vlc = new(() =>
    {
        Core.Initialize();

        return new LibVLC("--no-video-title-show");
    });

    private readonly GraphicsDevice Device;
    private readonly MediaPlayer Player;
    private readonly nint Buffer;
    private readonly byte[] Staging = new byte[FRAME_BYTES];
    private readonly byte[] Upload = new byte[FRAME_BYTES];
    private readonly Lock FrameLock = new();

    //kept in fields so the garbage collector can't free them while VLC still calls them
    private readonly MediaPlayer.LibVLCVideoLockCb LockCallback;
    private readonly MediaPlayer.LibVLCVideoDisplayCb DisplayCallback;

    private Media? CurrentMedia;
    private Texture2D? Texture;
    private volatile bool FrameReady;
    private volatile bool IsFailed;
    private volatile bool IsPlaying;
    private volatile bool IsEnded;
    private volatile bool IsBuffering;
    private bool Disposed;

    public CinemaPlayer(GraphicsDevice device)
    {
        Device = device;
        Player = new MediaPlayer(Vlc.Value);
        Buffer = Marshal.AllocHGlobal(FRAME_BYTES);
        LockCallback = OnLock;
        DisplayCallback = OnDisplay;

        Player.SetVideoFormat("RV32", WIDTH, HEIGHT, PITCH);
        Player.SetVideoCallbacks(LockCallback, null, DisplayCallback);

        Player.EncounteredError += (_, _) => IsFailed = true;
        Player.Playing += (_, _) => IsPlaying = true;
        Player.EndReached += (_, _) => { IsPlaying = false; IsEnded = true; };
        Player.Buffering += (_, e) => IsBuffering = e.Cache < 100;
        Player.Stopped += (_, _) => IsPlaying = false;
    }

    public bool Failed => IsFailed;
    public bool Ended => IsEnded;
    public bool Playing => IsPlaying && !IsBuffering;
    public long TimeMs => Math.Max(0, Player.Time);
    public Texture2D? Frame => Texture;

    public void Open(string url, long startMs)
    {
        var start = (startMs / 1000.0).ToString("0.###", CultureInfo.InvariantCulture);
        CurrentMedia = new Media(Vlc.Value, new Uri(url), ":network-caching=3000", $":start-time={start}");
        Player.Play(CurrentMedia);
    }

    public void Seek(long positionMs) => Player.Time = Math.Max(0, positionMs);

    public void SetPaused(bool paused) => Player.SetPause(paused);

    public void SetVolume(int volume, bool mute)
    {
        Player.Volume = Math.Clamp(volume, 0, 100);
        Player.Mute = mute;
    }

    public void UploadFrame()
    {
        if (Disposed)
            return;

        if (!FrameReady)
            return;

        using (FrameLock.EnterScope())
        {
            Array.Copy(Staging, Upload, FRAME_BYTES);
            FrameReady = false;
        }

        //VLC's RV32 is B, G, R, X in memory; the texture wants R, G, B, A
        for (var i = 0; i < FRAME_BYTES; i += 4)
        {
            (Upload[i], Upload[i + 2]) = (Upload[i + 2], Upload[i]);
            Upload[i + 3] = 255;
        }

        Texture ??= new Texture2D(Device, WIDTH, HEIGHT, false, SurfaceFormat.Color);
        Texture.SetData(Upload);
    }

    public void Dispose()
    {
        if (Disposed)
            return;

        Disposed = true;
        Texture?.Dispose();
        Texture = null;

        var player = Player;
        var media = CurrentMedia;
        var buffer = Buffer;

        //Stop waits for VLC's threads, which can take a moment; the game loop must not wait for it
        _ = Task.Run(() =>
        {
            player.Stop();
            player.Dispose();
            media?.Dispose();
            Marshal.FreeHGlobal(buffer);
            //the delegates live on this, so keep it alive until VLC's threads are done
            GC.KeepAlive(this);
        });
    }

    private nint OnLock(nint opaque, nint planes)
    {
        Marshal.WriteIntPtr(planes, Buffer);

        return nint.Zero;
    }

    private void OnDisplay(nint opaque, nint picture)
    {
        using (FrameLock.EnterScope())
        {
            Marshal.Copy(Buffer, Staging, 0, FRAME_BYTES);
            FrameReady = true;
        }
    }
}
