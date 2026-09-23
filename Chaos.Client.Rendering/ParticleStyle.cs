#region
using Microsoft.Xna.Framework;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>The procedural shape a <see cref="ParticleRenderer" /> draws for each particle.</summary>
public enum ParticleShape
{
    /// <summary>Hard-edged square — ash flakes.</summary>
    Square,

    /// <summary>Soft round glow — embers, fireflies.</summary>
    Dot,

    /// <summary>Pointed lens shape that spins and tumbles — leaves.</summary>
    Leaf,

    /// <summary>Soft oval that spins and tumbles — petals.</summary>
    Petal,

    /// <summary>Thin ring with a highlight — bubbles.</summary>
    Bubble,

    /// <summary>Thin line aligned to its motion — blowing sand.</summary>
    Streak
}

/// <summary>
///     Tunables for one <see cref="ParticleRenderer" /> effect. Every "Min/Max" pair is rolled uniformly per particle.
///     Each preset below is one piece of a map flag's look. Adjust for feel and rebuild to apply.
/// </summary>
public sealed record ParticleStyle
{
    /// <summary>Seconds for the whole effect to fade fully in or out.</summary>
    public float FadeSeconds { get; init; } = 1.2f;

    /// <summary>Particle count for a 640x480 viewport; scaled by viewport area.</summary>
    public int Count { get; init; }

    public ParticleShape Shape { get; init; }

    /// <summary>Additive glow instead of normal alpha blending — for light sources like embers and fireflies.</summary>
    public bool Additive { get; init; }

    /// <summary>One color is picked per particle.</summary>
    public Color[] Colors { get; init; } = [Color.White];

    /// <summary>Particle size in screen px (for <see cref="ParticleShape.Streak" />, its length).</summary>
    public float SizeMin { get; init; } = 1f;

    public float SizeMax { get; init; } = 1f;

    /// <summary>Base velocity in screen px/sec. Negative Y rises.</summary>
    public Vector2 VelocityMin { get; init; }

    public Vector2 VelocityMax { get; init; }

    /// <summary>Peak side-to-side (X) and bobbing (Y) speed, px/sec, added on top of the base velocity.</summary>
    public Vector2 Sway { get; init; }

    /// <summary>Sway cycles per second.</summary>
    public float SwayFreqMin { get; init; } = 0.5f;

    public float SwayFreqMax { get; init; } = 0.5f;

    /// <summary>Maximum spin, radians/sec, either direction. Leaves and petals also tumble (squash) as they spin.</summary>
    public float Spin { get; init; }

    public float AlphaMin { get; init; } = 1f;

    public float AlphaMax { get; init; } = 1f;

    /// <summary>How much the particle blinks: 0 = steady, 1 = fully off at the bottom of each blink.</summary>
    public float Twinkle { get; init; }

    /// <summary>Blink cycles per second.</summary>
    public float TwinkleFreqMin { get; init; } = 1f;

    public float TwinkleFreqMax { get; init; } = 1f;

    /// <summary>Sharpens the blink: 1 is a smooth swell, higher values give short flashes with long dark gaps.</summary>
    public float TwinkleSharpness { get; init; } = 1f;

    /// <summary>A second, larger faint copy drawn behind each particle as a glow. Zero means none.</summary>
    public float HaloScale { get; init; }

    public float HaloAlpha { get; init; }

    // ============================================================
    // Presets
    // ============================================================

    /// <summary>Grey ash drifting down. Paired with <see cref="Embers" /> for the Ash flag.</summary>
    public static ParticleStyle Ash { get; } = new()
    {
        Count = 70,
        Shape = ParticleShape.Square,
        Colors = [new Color(120, 120, 120), new Color(90, 90, 90), new Color(150, 148, 145)],
        SizeMin = 1f,
        SizeMax = 2.5f,
        VelocityMin = new Vector2(-8f, 18f),
        VelocityMax = new Vector2(8f, 40f),
        Sway = new Vector2(12f, 0f),
        SwayFreqMin = 0.3f,
        SwayFreqMax = 0.8f,
        AlphaMin = 0.5f,
        AlphaMax = 0.85f
    };

    /// <summary>Glowing orange sparks rising and flickering.</summary>
    public static ParticleStyle Embers { get; } = new()
    {
        Count = 25,
        Shape = ParticleShape.Dot,
        Additive = true,
        Colors = [new Color(255, 140, 40), new Color(255, 90, 20), new Color(255, 200, 80)],
        SizeMin = 2f,
        SizeMax = 4f,
        VelocityMin = new Vector2(-10f, -45f),
        VelocityMax = new Vector2(10f, -20f),
        Sway = new Vector2(15f, 0f),
        SwayFreqMin = 0.5f,
        SwayFreqMax = 1.2f,
        AlphaMin = 0.6f,
        AlphaMax = 1f,
        Twinkle = 0.6f,
        TwinkleFreqMin = 3f,
        TwinkleFreqMax = 7f,
        HaloScale = 3f,
        HaloAlpha = 0.25f
    };

    /// <summary>Autumn leaves blowing down and across, spinning and tumbling.</summary>
    public static ParticleStyle Leaves { get; } = new()
    {
        Count = 18,
        Shape = ParticleShape.Leaf,
        Colors = [new Color(196, 98, 32), new Color(214, 150, 40), new Color(150, 60, 24), new Color(120, 130, 40)],
        SizeMin = 7f,
        SizeMax = 11f,
        VelocityMin = new Vector2(10f, 25f),
        VelocityMax = new Vector2(30f, 45f),
        Sway = new Vector2(25f, 0f),
        SwayFreqMin = 0.3f,
        SwayFreqMax = 0.7f,
        Spin = 2f,
        AlphaMin = 0.9f,
        AlphaMax = 1f
    };

    /// <summary>Pink blossom petals fluttering down.</summary>
    public static ParticleStyle Petals { get; } = new()
    {
        Count = 30,
        Shape = ParticleShape.Petal,
        Colors = [new Color(255, 183, 197), new Color(255, 208, 220), new Color(250, 160, 180), new Color(255, 235, 240)],
        SizeMin = 4f,
        SizeMax = 6f,
        VelocityMin = new Vector2(15f, 20f),
        VelocityMax = new Vector2(35f, 35f),
        Sway = new Vector2(20f, 0f),
        SwayFreqMin = 0.4f,
        SwayFreqMax = 0.9f,
        Spin = 3f,
        AlphaMin = 0.8f,
        AlphaMax = 1f
    };

    /// <summary>Yellow-green lights wandering slowly and blinking on and off.</summary>
    public static ParticleStyle Fireflies { get; } = new()
    {
        FadeSeconds = 2f,
        Count = 22,
        Shape = ParticleShape.Dot,
        Additive = true,
        Colors = [new Color(210, 255, 120), new Color(240, 255, 150)],
        SizeMin = 2f,
        SizeMax = 3f,
        VelocityMin = new Vector2(-4f, -4f),
        VelocityMax = new Vector2(4f, 4f),
        Sway = new Vector2(18f, 14f),
        SwayFreqMin = 0.1f,
        SwayFreqMax = 0.35f,
        AlphaMin = 0.8f,
        AlphaMax = 1f,
        Twinkle = 1f,
        TwinkleFreqMin = 0.3f,
        TwinkleFreqMax = 0.8f,
        TwinkleSharpness = 3f,
        HaloScale = 5f,
        HaloAlpha = 0.3f
    };

    /// <summary>Bubbles wobbling upward. Paired with the Underwater mist.</summary>
    public static ParticleStyle Bubbles { get; } = new()
    {
        Count = 25,
        Shape = ParticleShape.Bubble,
        Colors = [new Color(200, 240, 255)],
        SizeMin = 3f,
        SizeMax = 8f,
        VelocityMin = new Vector2(-3f, -40f),
        VelocityMax = new Vector2(3f, -15f),
        Sway = new Vector2(8f, 0f),
        SwayFreqMin = 0.8f,
        SwayFreqMax = 1.6f,
        AlphaMin = 0.5f,
        AlphaMax = 0.8f
    };

    /// <summary>Fast sand grains streaking sideways. Paired with the Sandstorm mist.</summary>
    public static ParticleStyle SandGrains { get; } = new()
    {
        FadeSeconds = 1.5f,
        Count = 90,
        Shape = ParticleShape.Streak,
        Colors = [new Color(225, 190, 130), new Color(200, 165, 110), new Color(240, 215, 160)],
        SizeMin = 4f,
        SizeMax = 10f,
        VelocityMin = new Vector2(-380f, 5f),
        VelocityMax = new Vector2(-240f, 30f),
        Sway = new Vector2(0f, 10f),
        SwayFreqMin = 1f,
        SwayFreqMax = 2f,
        AlphaMin = 0.35f,
        AlphaMax = 0.7f
    };
}
