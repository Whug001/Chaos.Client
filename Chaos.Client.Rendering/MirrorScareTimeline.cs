namespace Chaos.Client.Rendering;

public enum ScarePhase
{
    Crack,
    Shatter,
    Black,
    Done
}

public enum ScareCreature
{
    Mary,
    Grinner,
    Eye
}

/// <summary>
///     Timing of the haunted-mirror scare, in seconds from its start: the frozen screen cracks, its pieces fly off to
///     show a creature behind the glass, then a cut to black.
/// </summary>
public static class MirrorScareTimeline
{
    public const double CRACK_SECONDS = 0.12;
    public const double FLASH_SECONDS = 0.06;
    public const double BLACK_AT_SECONDS = 1.05;
    public const double DURATION_SECONDS = 1.2;
    public const double SHAKE_FROM_SECONDS = 0.4;
    public const double GROW_SECONDS = 0.25;
    public const float START_SCALE = 0.9f;
    public const float END_SCALE = 1.35f;
    public const int SHAKE_PIXELS = 4;

    //the second each frame starts, counted from the start of the scare
    private static readonly double[] MaryCues = [0, 0.33, 0.37];
    private static readonly double[] GrinnerCues = [0, 0.30, 0.39, 0.42];
    private static readonly double[] EyeCues = [0, 0.32, 0.38];

    public static int CreatureCount => Enum.GetValues<ScareCreature>().Length;

    public static ScarePhase PhaseAt(double secondsInto)
        => secondsInto switch
        {
            < 0                 => ScarePhase.Done,
            < CRACK_SECONDS     => ScarePhase.Crack,
            < BLACK_AT_SECONDS  => ScarePhase.Shatter,
            < DURATION_SECONDS  => ScarePhase.Black,
            _                   => ScarePhase.Done
        };

    public static float CreatureScale(double secondsInto)
    {
        var grown = Math.Clamp((secondsInto - CRACK_SECONDS) / GROW_SECONDS, 0, 1);

        return START_SCALE + (float)grown * (END_SCALE - START_SCALE);
    }

    public static bool IsShaking(double secondsInto) => secondsInto >= SHAKE_FROM_SECONDS;

    public static int FrameCount(ScareCreature creature) => Cues(creature).Length;

    public static int FrameAt(ScareCreature creature, double secondsInto)
    {
        var cues = Cues(creature);
        var frame = 0;

        for (var i = 1; i < cues.Length; i++)
            if (secondsInto >= cues[i])
                frame = i;

        return frame;
    }

    public static string ResourceName(ScareCreature creature, int frame) => $"mirrorscare.{creature.ToString().ToLowerInvariant()}{frame}.png";

    private static double[] Cues(ScareCreature creature)
        => creature switch
        {
            ScareCreature.Mary    => MaryCues,
            ScareCreature.Grinner => GrinnerCues,
            _                     => EyeCues
        };
}
