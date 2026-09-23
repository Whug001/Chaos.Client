#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>How the full-screen wash of a <see cref="MistRenderer" /> breathes.</summary>
public enum MistPulse
{
    /// <summary>Steady wash.</summary>
    None,

    /// <summary>Smooth sine swell.</summary>
    Sine,

    /// <summary>"Lub-dub" double beat, then rest.</summary>
    Heartbeat
}

/// <summary>One drifting layer of cloud texture in a <see cref="MistRenderer" />.</summary>
/// <param name="Scale">Texture magnification. Bigger reads as further away.</param>
/// <param name="Alpha">Peak opacity of the layer [0..1].</param>
/// <param name="Velocity">Drift in screen px/sec. Negative X drifts left, negative Y drifts up.</param>
public readonly record struct MistLayer(float Scale, float Alpha, Vector2 Velocity);

/// <summary>
///     Tunables for one <see cref="MistRenderer" /> effect: a full-screen color wash, drifting parallax layers of
///     seamless cloud noise, and an optional dark edge (vignette). Each preset below is one map flag's look.
///     Adjust for feel and rebuild to apply.
/// </summary>
public sealed record MistStyle
{
    /// <summary>Seconds for the whole effect to fade fully in or out.</summary>
    public float FadeSeconds { get; init; } = 1.2f;

    /// <summary>Color of the flat wash painted under the layers — this tints and dims the scene.</summary>
    public Color WashColor { get; init; }

    /// <summary>Resting coverage of the wash [0..1].</summary>
    public float WashAlpha { get; init; }

    public MistPulse Pulse { get; init; } = MistPulse.None;

    /// <summary>How far the pulse swings the wash, as a fraction of <see cref="WashAlpha" />.</summary>
    public float PulseDepth { get; init; }

    /// <summary>Seconds per pulse cycle.</summary>
    public float PulsePeriod { get; init; } = 1f;

    /// <summary>Tint of the cloud layers (RGB only; alpha comes from the texture, layer and fade).</summary>
    public Color LayerTint { get; init; }

    public MistLayer[] Layers { get; init; } = [];

    /// <summary>Seed for the cloud texture. Different seeds give different cloud shapes.</summary>
    public int Seed { get; init; } = 1337;

    /// <summary>Noise level where clouds start. Higher gives sparser, wispier clouds.</summary>
    public float NoiseKnee { get; init; } = 0.35f;

    /// <summary>Noise range over which clouds go from clear to solid. Smaller gives harder edges.</summary>
    public float NoiseRange { get; init; } = 0.5f;

    /// <summary>Color at the viewport edges. Only drawn when <see cref="VignetteAlpha" /> is above zero.</summary>
    public Color VignetteColor { get; init; }

    /// <summary>Peak coverage of the dark edge [0..1]. Zero means no vignette.</summary>
    public float VignetteAlpha { get; init; }

    /// <summary>Normalized radius where the edge starts darkening (1 = edge midpoints, ~1.41 = corners).</summary>
    public float VignetteInner { get; init; } = 0.55f;

    // ============================================================
    // Presets — one per map flag
    // ============================================================

    /// <summary>Grey-green dim with slow rolling fog banks.</summary>
    public static MistStyle Fog { get; } = new()
    {
        WashColor = new Color(38, 50, 42),
        WashAlpha = 0.34f,
        LayerTint = new Color(150, 170, 152),
        Layers =
        [
            new MistLayer(3.5f, 0.28f, new Vector2(7f, 0f)),
            new MistLayer(2.5f, 0.42f, new Vector2(13f, 0f))
        ]
    };

    /// <summary>Crimson wash on a heartbeat, faint blood-red mist, dark red edges.</summary>
    public static MistStyle BloodMoon { get; } = new()
    {
        FadeSeconds = 1.8f,
        WashColor = new Color(90, 8, 12),
        WashAlpha = 0.26f,
        Pulse = MistPulse.Heartbeat,
        PulseDepth = 0.25f,
        PulsePeriod = 2.4f,
        LayerTint = new Color(180, 40, 45),
        Layers =
        [
            new MistLayer(3.5f, 0.18f, new Vector2(5f, 0f)),
            new MistLayer(2.5f, 0.28f, new Vector2(9f, 0f))
        ],
        Seed = 7211,
        NoiseKnee = 0.42f,
        VignetteColor = new Color(20, 0, 0),
        VignetteAlpha = 0.45f
    };

    /// <summary>Dusty orange-tan haze with dense banks tearing sideways. Paired with blowing sand grains.</summary>
    public static MistStyle Sandstorm { get; } = new()
    {
        FadeSeconds = 1.5f,
        WashColor = new Color(150, 110, 60),
        WashAlpha = 0.30f,
        LayerTint = new Color(210, 170, 110),
        Layers =
        [
            new MistLayer(3.0f, 0.30f, new Vector2(-90f, 6f)),
            new MistLayer(2.0f, 0.38f, new Vector2(-150f, 10f)),
            new MistLayer(1.4f, 0.26f, new Vector2(-230f, 14f))
        ],
        Seed = 9001,
        NoiseKnee = 0.30f,
        VignetteColor = new Color(70, 45, 20),
        VignetteAlpha = 0.30f,
        VignetteInner = 0.6f
    };

    /// <summary>Sickly green murk that rises slowly and swells, with dark green edges.</summary>
    public static MistStyle Miasma { get; } = new()
    {
        FadeSeconds = 1.6f,
        WashColor = new Color(40, 62, 20),
        WashAlpha = 0.30f,
        Pulse = MistPulse.Sine,
        PulseDepth = 0.2f,
        PulsePeriod = 5f,
        LayerTint = new Color(115, 155, 50),
        Layers =
        [
            new MistLayer(4.0f, 0.30f, new Vector2(4f, -3f)),
            new MistLayer(2.8f, 0.40f, new Vector2(-6f, -5f))
        ],
        Seed = 3163,
        NoiseKnee = 0.38f,
        VignetteColor = new Color(15, 30, 5),
        VignetteAlpha = 0.35f
    };

    /// <summary>
    ///     Blue-green tint with pale light patches drifting in two directions, so they overlap and shimmer like
    ///     caustics. Paired with rising bubbles.
    /// </summary>
    public static MistStyle Underwater { get; } = new()
    {
        FadeSeconds = 1.0f,
        WashColor = new Color(10, 60, 90),
        WashAlpha = 0.32f,
        Pulse = MistPulse.Sine,
        PulseDepth = 0.12f,
        PulsePeriod = 6f,
        LayerTint = new Color(150, 225, 240),
        Layers =
        [
            new MistLayer(2.2f, 0.16f, new Vector2(10f, 4f)),
            new MistLayer(1.6f, 0.20f, new Vector2(-14f, -6f))
        ],
        Seed = 4242,
        NoiseKnee = 0.50f,
        NoiseRange = 0.35f,
        VignetteColor = new Color(0, 15, 35),
        VignetteAlpha = 0.40f,
        VignetteInner = 0.6f
    };
}
