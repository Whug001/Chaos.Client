using Chaos.CannonFair;

namespace Chaos.Client.Controls.World.Popups.CannonFair;

public static class CannonFairCamera
{
    private const float WIDE_SCALE = 16f;
    private const float IMPACT_SCALE = 35f;
    private const float WIDE_FOCUS_X = 20f;
    private const float WIDE_FOCUS_Y = -10f;
    private const float IMPACT_FOCUS_X = 32f;
    private const float IMPACT_FOCUS_Y = -3.5f;
    private const float SCREEN_CENTER_X = 320f;
    private const float SCREEN_CENTER_Y = 240f;
    private const int LEAD_MS = 300;
    private const int ZOOM_MS = 800;

    public static int ImpactMs(VolleyResult volley)
    {
        var first = int.MaxValue;

        foreach (var score in volley.Scores)
            first = Math.Min(first, score.Ms);

        foreach (var blast in volley.Blasts)
            first = Math.Min(first, blast.Ms);

        return first == int.MaxValue ? -1 : Math.Max(0, first - LEAD_MS);
    }

    public static float ZoomAt(int ms, int impactMs)
    {
        if (impactMs < 0)
            return 0f;

        var t = Math.Clamp((ms - impactMs) / (float)ZOOM_MS, 0f, 1f);

        return t * t * (3f - (2f * t));
    }

    public static (float X, float Y, float Scale) Project(float worldX, float worldY, float zoom)
    {
        var scale = WIDE_SCALE + ((IMPACT_SCALE - WIDE_SCALE) * zoom);
        var focusX = WIDE_FOCUS_X + ((IMPACT_FOCUS_X - WIDE_FOCUS_X) * zoom);
        var focusY = WIDE_FOCUS_Y + ((IMPACT_FOCUS_Y - WIDE_FOCUS_Y) * zoom);

        return (SCREEN_CENTER_X + ((worldX - focusX) * scale), SCREEN_CENTER_Y + ((worldY - focusY) * scale), scale);
    }
}
