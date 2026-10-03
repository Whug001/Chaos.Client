using Microsoft.Xna.Framework;

namespace Chaos.Client.Controls.World.Popups.Fishing;

/// <summary>
///     The fishing window's colour rules, kept free of drawing so they can be tested. Positions use the 0-100 scale
///     the server sends.
/// </summary>
public static class FishingLook
{
    public const float ZONE_FADE_SECONDS = 0.2f;

    public static readonly Color MeterLow = new(210, 60, 40);
    public static readonly Color MeterMid = new(230, 190, 50);
    public static readonly Color MeterHigh = new(90, 210, 70);
    public static readonly Color ZoneIn = new(120, 220, 90);
    public static readonly Color ZoneOut = new(230, 170, 60);

    /// <summary>The same test the server uses: the fish counts as caught while it sits on or between the bar's edges.</summary>
    public static bool Covers(float fishY, float barY, float barHeight) => (fishY >= barY) && (fishY <= (barY + barHeight));

    /// <summary>Red when the catch meter is empty, amber at half, green when full.</summary>
    public static Color MeterColor(float percent)
    {
        var t = Math.Clamp(percent, 0f, 100f) / 100f;

        return t < 0.5f ? Color.Lerp(MeterLow, MeterMid, t / 0.5f) : Color.Lerp(MeterMid, MeterHigh, (t - 0.5f) / 0.5f);
    }

    /// <summary>Moves the zone tint (0 = amber, 1 = green) toward its target so a full change takes the fade time.</summary>
    public static float Approach(float current, float target, float seconds)
    {
        var step = seconds / ZONE_FADE_SECONDS;

        return current < target ? Math.Min(target, current + step) : Math.Max(target, current - step);
    }

    public static Color ZoneColor(float inside) => Color.Lerp(ZoneOut, ZoneIn, Math.Clamp(inside, 0f, 1f));
}
