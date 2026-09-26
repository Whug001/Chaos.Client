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

    /// <summary>Wing length as a multiple of the particle size. Zero means no wings.</summary>
    public float WingScale { get; init; }

    /// <summary>Wing width as a multiple of the particle size.</summary>
    public float WingWidth { get; init; } = 1f;

    /// <summary>Wing opacity as a fraction of the particle's own. Wings are the particle's color, paled toward white.</summary>
    public float WingAlpha { get; init; } = 0.5f;

    /// <summary>1 draws one pair of wings; 2 adds a smaller lower pair, like fairy wings.</summary>
    public int WingPairs { get; init; } = 1;

    /// <summary>Wing flap cycles per second. The wings beat twice per cycle, so 7 gives 14 beats a second.</summary>
    public float WingFlapMin { get; init; } = 2f;

    public float WingFlapMax { get; init; } = 2f;

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

    /// <summary>Golden motes rising and glimmering. Paired with the Radiance mist.</summary>
    public static ParticleStyle RadianceMotes { get; } = new()
    {
        Count = 30,
        Shape = ParticleShape.Dot,
        Additive = true,
        Colors = [new Color(255, 220, 120), new Color(255, 240, 180), new Color(255, 200, 90)],
        SizeMin = 1.5f,
        SizeMax = 3f,
        VelocityMin = new Vector2(-3f, -18f),
        VelocityMax = new Vector2(3f, -8f),
        Sway = new Vector2(6f, 0f),
        SwayFreqMin = 0.3f,
        SwayFreqMax = 0.7f,
        AlphaMin = 0.6f,
        AlphaMax = 1f,
        Twinkle = 0.5f,
        TwinkleFreqMin = 0.5f,
        TwinkleFreqMax = 1.2f,
        TwinkleSharpness = 1.5f,
        HaloScale = 4f,
        HaloAlpha = 0.25f
    };

    /// <summary>Violet and blue sparks drifting up and flickering. Paired with the Arcane mist.</summary>
    public static ParticleStyle ArcaneSparks { get; } = new()
    {
        Count = 35,
        Shape = ParticleShape.Dot,
        Additive = true,
        Colors = [new Color(170, 120, 255), new Color(110, 160, 255), new Color(220, 170, 255)],
        SizeMin = 1.5f,
        SizeMax = 3f,
        VelocityMin = new Vector2(-5f, -25f),
        VelocityMax = new Vector2(5f, -10f),
        Sway = new Vector2(10f, 4f),
        SwayFreqMin = 0.4f,
        SwayFreqMax = 1f,
        AlphaMin = 0.7f,
        AlphaMax = 1f,
        Twinkle = 0.8f,
        TwinkleFreqMin = 1.5f,
        TwinkleFreqMax = 3f,
        TwinkleSharpness = 2.5f,
        HaloScale = 3.5f,
        HaloAlpha = 0.3f
    };

    /// <summary>Pale sparkles drifting down and twinkling. Paired with the Frost mist.</summary>
    public static ParticleStyle FrostSparkles { get; } = new()
    {
        Count = 30,
        Shape = ParticleShape.Dot,
        Additive = true,
        Colors = [new Color(190, 225, 255), new Color(235, 245, 255)],
        SizeMin = 1.5f,
        SizeMax = 2.5f,
        VelocityMin = new Vector2(-3f, 4f),
        VelocityMax = new Vector2(3f, 10f),
        Sway = new Vector2(6f, 0f),
        Twinkle = 0.8f,
        TwinkleFreqMin = 0.5f,
        TwinkleFreqMax = 1.5f,
        TwinkleSharpness = 2f,
        HaloScale = 3f,
        HaloAlpha = 0.2f
    };

    /// <summary>Snow streaks driving sideways. Paired with the Blizzard mist and flakes.</summary>
    public static ParticleStyle BlizzardStreaks { get; } = new()
    {
        FadeSeconds = 1.5f,
        Count = 120,
        Shape = ParticleShape.Streak,
        Colors = [new Color(255, 255, 255), new Color(230, 238, 250), new Color(210, 222, 240)],
        SizeMin = 3f,
        SizeMax = 8f,
        VelocityMin = new Vector2(-420f, 60f),
        VelocityMax = new Vector2(-260f, 140f),
        Sway = new Vector2(0f, 15f),
        SwayFreqMin = 1f,
        SwayFreqMax = 2f,
        AlphaMin = 0.5f,
        AlphaMax = 0.9f
    };

    /// <summary>Snow flakes blowing sideways, slower than the streaks. Paired with the Blizzard mist.</summary>
    public static ParticleStyle BlizzardFlakes { get; } = new()
    {
        FadeSeconds = 1.5f,
        Count = 70,
        Shape = ParticleShape.Square,
        Colors = [new Color(255, 255, 255), new Color(235, 242, 252)],
        SizeMin = 1.5f,
        SizeMax = 3f,
        VelocityMin = new Vector2(-240f, 40f),
        VelocityMax = new Vector2(-150f, 90f),
        Sway = new Vector2(0f, 20f),
        SwayFreqMin = 0.8f,
        SwayFreqMax = 1.6f,
        AlphaMin = 0.6f,
        AlphaMax = 1f
    };

    /// <summary>Warm specks hanging in the air and catching the light. Paired with the Dust mist.</summary>
    public static ParticleStyle DustMotes { get; } = new()
    {
        FadeSeconds = 2f,
        Count = 70,
        Shape = ParticleShape.Dot,
        Additive = true,
        Colors = [new Color(255, 235, 200), new Color(240, 220, 190)],
        SizeMin = 1.5f,
        SizeMax = 3f,
        VelocityMin = new Vector2(-2f, -2f),
        VelocityMax = new Vector2(2f, 3f),
        Sway = new Vector2(4f, 3f),
        SwayFreqMin = 0.05f,
        SwayFreqMax = 0.2f,
        AlphaMin = 0.5f,
        AlphaMax = 0.9f,
        Twinkle = 0.5f,
        TwinkleFreqMin = 0.2f,
        TwinkleFreqMax = 0.5f,
        HaloScale = 2.5f,
        HaloAlpha = 0.3f
    };

    /// <summary>White flecks of spray blowing sideways. Paired with the SeaSpray mist.</summary>
    public static ParticleStyle SeaSprayFlecks { get; } = new()
    {
        Count = 45,
        Shape = ParticleShape.Dot,
        Colors = [new Color(245, 250, 255), new Color(225, 238, 245)],
        SizeMin = 1f,
        SizeMax = 2.5f,
        VelocityMin = new Vector2(-160f, -20f),
        VelocityMax = new Vector2(-90f, 10f),
        Sway = new Vector2(0f, 20f),
        SwayFreqMin = 0.8f,
        SwayFreqMax = 1.5f,
        AlphaMin = 0.4f,
        AlphaMax = 0.8f
    };

    /// <summary>A few blue lights on small fluttering fairy wings, drifting slowly and dimming.</summary>
    public static ParticleStyle Wisps { get; } = new()
    {
        FadeSeconds = 2f,
        Count = 6,
        Shape = ParticleShape.Dot,
        Additive = true,
        Colors = [new Color(120, 190, 255), new Color(160, 230, 255), new Color(100, 255, 230)],
        SizeMin = 4f,
        SizeMax = 6f,
        VelocityMin = new Vector2(-6f, -4f),
        VelocityMax = new Vector2(6f, 4f),
        Sway = new Vector2(22f, 16f),
        SwayFreqMin = 0.05f,
        SwayFreqMax = 0.15f,
        AlphaMin = 0.7f,
        AlphaMax = 1f,
        Twinkle = 0.4f,
        TwinkleFreqMin = 0.2f,
        TwinkleFreqMax = 0.4f,
        TwinkleSharpness = 1.5f,
        HaloScale = 6f,
        HaloAlpha = 0.3f,
        WingScale = 1.5f,
        WingWidth = 0.6f,
        WingAlpha = 0.5f,
        WingPairs = 2,
        WingFlapMin = 7f,
        WingFlapMax = 9f
    };

    /// <summary>A few water drops falling from above.</summary>
    public static ParticleStyle Drips { get; } = new()
    {
        Count = 8,
        Shape = ParticleShape.Streak,
        Colors = [new Color(170, 200, 230), new Color(200, 225, 250)],
        SizeMin = 4f,
        SizeMax = 7f,
        VelocityMin = new Vector2(0f, 220f),
        VelocityMax = new Vector2(0f, 320f),
        AlphaMin = 0.35f,
        AlphaMax = 0.6f
    };
}
