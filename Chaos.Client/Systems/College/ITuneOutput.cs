namespace Chaos.Client.Systems.College;

/// <summary>The one mixer channel College tunes play on. <see cref="SoundSystem" /> is the real one.</summary>
public interface ITuneOutput
{
    bool IsTunePlaying { get; }

    /// <summary>Plays a whole tune and fades the map music out until it stops.</summary>
    bool PlayTune(byte[] wav);

    /// <summary>Plays one note's preview on the same channel, leaving the map music as it is.</summary>
    bool PlayPreview(byte[] wav);

    void StopTune();
}
