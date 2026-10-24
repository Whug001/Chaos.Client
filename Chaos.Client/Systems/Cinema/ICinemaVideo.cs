using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Systems.Cinema;

/// <summary>One open film stream. Members are called from the game loop only.</summary>
public interface ICinemaVideo : IDisposable
{
    /// <summary>True once the stream has hit an error it will not recover from.</summary>
    bool Failed { get; }

    /// <summary>True while frames are actually playing; false while opening or buffering.</summary>
    bool Playing { get; }

    /// <summary>True once the stream has reached its end (or VLC stopped it early).</summary>
    bool Ended { get; }

    /// <summary>The player's position in milliseconds. Reads 0 while buffering.</summary>
    long TimeMs { get; }

    /// <summary>The latest frame, or null before the first one.</summary>
    Texture2D? Frame { get; }

    void Open(string url, long startMs);
    void Seek(long positionMs);
    void SetPaused(bool paused);
    void SetVolume(int volume, bool mute);

    /// <summary>Copies the newest decoded frame into <see cref="Frame" />, if a new one arrived.</summary>
    void UploadFrame();
}
