#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>How the flyers of one <see cref="FlyByFlock" /> group are arranged when they enter.</summary>
public enum FlyByFormation
{
    /// <summary>Each flyer starts at a random spot within the group's spread — bats.</summary>
    Scatter,

    /// <summary>One leader in front and two arms trailing back and out from it — geese.</summary>
    Vee
}

/// <summary>
///     Tunables for one <see cref="FlyByRenderer" /> effect: groups of small pixel sprites that cross the screen now and
///     then, with quiet gaps between. Every "Min/Max" pair is rolled uniformly. Adjust for feel and rebuild to apply.
/// </summary>
public sealed record FlyByStyle
{
    /// <summary>Seconds for the whole effect to fade fully in or out.</summary>
    public float FadeSeconds { get; init; } = 1.5f;

    /// <summary>The animation frames, as <see cref="SpriteGrid" /> text grids.</summary>
    public IReadOnlyList<string[]> Frames { get; init; } = [];

    /// <summary>Frame indices played in order, once per flap cycle.</summary>
    public int[] FrameCycle { get; init; } = [0];

    /// <summary>Tint of every flyer (RGB only).</summary>
    public Color Color { get; init; } = Color.Black;

    /// <summary>Opacity of every flyer when fully faded in [0..1].</summary>
    public float Alpha { get; init; } = 1f;

    /// <summary>Seconds from switching on to the first group.</summary>
    public float FirstGroupMin { get; init; } = 2f;

    public float FirstGroupMax { get; init; } = 6f;

    /// <summary>Seconds from one group to the next.</summary>
    public float GapMin { get; init; } = 8f;

    public float GapMax { get; init; } = 20f;

    /// <summary>Flyers per group, both ends included.</summary>
    public int GroupMin { get; init; } = 4;

    public int GroupMax { get; init; } = 8;

    /// <summary>Group speed across the screen, px/sec.</summary>
    public float SpeedMin { get; init; } = 100f;

    public float SpeedMax { get; init; } = 100f;

    /// <summary>Each flyer's speed is the group's times 1 plus or minus this.</summary>
    public float SpeedJitter { get; init; } = 0.1f;

    /// <summary>How far behind the front of its group a flyer may start, px.</summary>
    public float SpreadAlong { get; init; }

    /// <summary>How far above or below its group's line a flyer may start, px.</summary>
    public float SpreadAcross { get; init; }

    /// <summary>
    ///     How a group is arranged. <see cref="FlyByFormation.Vee" /> places the flyers by the Vee fields below and does
    ///     not use <see cref="SpreadAlong" /> or <see cref="SpreadAcross" />.
    /// </summary>
    public FlyByFormation Formation { get; init; } = FlyByFormation.Scatter;

    /// <summary>In a Vee, how far behind the flyer ahead of it on its arm each flyer sits, px.</summary>
    public float VeeAlong { get; init; } = 13f;

    /// <summary>In a Vee, how far further out to the side than the flyer ahead of it each flyer sits, px.</summary>
    public float VeeAcross { get; init; } = 8f;

    /// <summary>In a Vee, the largest random offset of a flyer from its exact place, px on each axis.</summary>
    public float VeeJitter { get; init; } = 1.5f;

    /// <summary>
    ///     Draws flyers heading left mirrored. For sprites that face right, like the geese; the bats look the same
    ///     either way.
    /// </summary>
    public bool FlipWithDirection { get; init; }

    /// <summary>Largest up or down speed a group shares, px/sec. Rolled between minus and plus this.</summary>
    public float DriftMax { get; init; }

    /// <summary>Up-and-down bob height in px. Each flyer rolls 0.7 to 1.3 times this.</summary>
    public float WaveAmplitude { get; init; }

    /// <summary>Bob cycles per second. Each flyer rolls 0.8 to 1.2 times this.</summary>
    public float WaveFreq { get; init; } = 1f;

    /// <summary>Seconds between darts. A max of zero turns darting off.</summary>
    public float DartIntervalMin { get; init; }

    public float DartIntervalMax { get; init; }

    /// <summary>Largest dart push in px/sec: sideways (X) and up or down (Y).</summary>
    public Vector2 DartSpeed { get; init; }

    /// <summary>Flap cycles per second.</summary>
    public float FlapMin { get; init; } = 3f;

    public float FlapMax { get; init; } = 3f;

    /// <summary>Px outside the viewport edge where a group enters.</summary>
    public float EntryMargin { get; init; } = 30f;

    /// <summary>Px past the far edge where a flyer is removed.</summary>
    public float ExitMargin { get; init; } = 140f;

    /// <summary>Highest start line, px from the top of the viewport.</summary>
    public float BandTop { get; init; } = 30f;

    /// <summary>Lowest start line, as a fraction of the viewport height.</summary>
    public float BandBottom { get; init; } = 0.7f;

    // ============================================================
    // Presets — one per map flag
    // ============================================================

    /// <summary>Groups of 4-8 black bats, 19 px wide, darting across every 8-20 s.</summary>
    public static FlyByStyle Bats { get; } = new()
    {
        FadeSeconds = 1.5f,
        Frames = SpriteGrid.BatFrames,
        FrameCycle = [0, 1, 2, 1],
        Color = new Color(18, 10, 22),
        Alpha = 0.95f,
        FirstGroupMin = 2f,
        FirstGroupMax = 6f,
        GapMin = 8f,
        GapMax = 20f,
        GroupMin = 4,
        GroupMax = 8,
        SpeedMin = 95f,
        SpeedMax = 135f,
        SpeedJitter = 0.1f,
        SpreadAlong = 80f,
        SpreadAcross = 36f,
        DriftMax = 14f,
        WaveAmplitude = 4f,
        WaveFreq = 1.8f,
        DartIntervalMin = 0.15f,
        DartIntervalMax = 0.45f,
        DartSpeed = new Vector2(40f, 55f),
        FlapMin = 4f,
        FlapMax = 5f
    };

    /// <summary>
    ///     A V of 7-11 goose-shaped shadows sliding over the ground every 15-35 s. The geese themselves fly above the
    ///     camera and are never drawn. Every bird shares one speed, so the V holds its shape.
    /// </summary>
    public static FlyByStyle GooseShadows { get; } = new()
    {
        FadeSeconds = 1.5f,
        Frames = SpriteGrid.GooseFrames,
        FrameCycle = [0, 1, 2, 1],
        Color = Color.Black,
        Alpha = 0.18f,
        Formation = FlyByFormation.Vee,
        VeeAlong = 13f,
        VeeAcross = 8f,
        VeeJitter = 1.5f,
        FlipWithDirection = true,
        FirstGroupMin = 4f,
        FirstGroupMax = 10f,
        GapMin = 15f,
        GapMax = 35f,
        GroupMin = 7,
        GroupMax = 11,
        SpeedMin = 34f,
        SpeedMax = 44f,
        SpeedJitter = 0f,
        DriftMax = 5f,
        WaveAmplitude = 1.2f,
        WaveFreq = 0.2f,
        DartIntervalMax = 0f,
        FlapMin = 2.2f,
        FlapMax = 2.8f,
        BandTop = 30f,
        BandBottom = 0.75f
    };
}
