namespace Chaos.Client.Systems.College;

/// <summary>The mixer channels College tunes play on. <see cref="SoundSystem" /> is the real one.</summary>
public interface ITuneOutput
{
    bool IsTunePlaying { get; }

    /// <summary>True while the sound-effects volume is off, so a tune would play silently.</summary>
    bool IsMuted { get; }

    /// <summary>
    ///     Plays a whole tune and fades the map music out until it stops. The tune before it, a loop's previous pass, rings
    ///     out underneath rather than being cut off.
    /// </summary>
    bool PlayTune(byte[] wav);

    /// <summary>Plays one note's preview on the same channel, leaving the map music as it is.</summary>
    bool PlayPreview(byte[] wav);

    /// <summary>Stops every tune sound. <paramref name="holdMusicDown" /> keeps the map music down for a tune about to replace it.</summary>
    void StopTune(bool holdMusicDown);
}
