namespace Chaos.Client.Systems;

/// <summary>How far the map music is turned down while a College tune plays: 1 is full volume, 0 is silent.</summary>
public sealed class MusicDuck
{
    public const double FADE_SECONDS = 0.3;

    public double Level { get; private set; } = 1;
    public bool Ducked { get; set; }

    /// <summary>Moves the level toward its target. True when it changed, so the caller applies it.</summary>
    public bool Step(double elapsedSeconds)
    {
        var target = Ducked ? 0 : 1;

        if (Level == target)
            return false;

        var delta = elapsedSeconds / FADE_SECONDS;
        Level = Ducked ? Math.Max(0, Level - delta) : Math.Min(1, Level + delta);

        return true;
    }

    public int Apply(int musicVolume) => (int)Math.Round(musicVolume * Level);
}
