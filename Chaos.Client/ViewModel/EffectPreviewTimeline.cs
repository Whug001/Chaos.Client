namespace Chaos.Client.ViewModel;

/// <summary>One effect's playback data, from EffectRenderer.GetEffectInfo. FrameCount 0 means the effect is missing.</summary>
public readonly record struct EffectClip(int EffectId, int FrameCount, int FrameIntervalMs)
{
    private const int DEFAULT_INTERVAL_MS = 100;

    public int IntervalMs => FrameIntervalMs > 0 ? FrameIntervalMs : DEFAULT_INTERVAL_MS;
    public int LengthMs => FrameCount * IntervalMs;

    public int FrameAt(double elapsedMs) => Math.Clamp((int)(elapsedMs / IntervalMs), 0, Math.Max(FrameCount - 1, 0));
}

/// <summary>What the preview draws at one moment. EffectId 0 means no effect frame.</summary>
public readonly record struct PreviewFrame(bool ShowFigure, int EffectId, int EffectFrame);

/// <summary>The Effects window preview's loop, as pure functions of time.</summary>
public static class EffectPreviewTimeline
{
    //the character stands this long between effects
    public const int HOLD_MS = 1200;

    //empty pedestal before the loop starts again
    public const int GAP_MS = 600;

    private static readonly PreviewFrame Standing = new(true, 0, 0);
    private static readonly PreviewFrame Empty = new(false, 0, 0);

    /// <summary>
    ///     Login effect (the character appears at <paramref name="revealMs" />), a hold, the logout effect (the
    ///     character vanishes <paramref name="revealMs" /> into it), then an empty gap.
    /// </summary>
    public static PreviewFrame LoginLogout(EffectClip login, EffectClip logout, int revealMs, double timeMs)
    {
        var loop = login.LengthMs + HOLD_MS + logout.LengthMs + GAP_MS;
        var t = timeMs % loop;

        if (t < login.LengthMs)
            return new PreviewFrame(t >= revealMs, login.EffectId, login.FrameAt(t));

        t -= login.LengthMs;

        if (t < HOLD_MS)
            return Standing;

        t -= HOLD_MS;

        if (t < logout.LengthMs)
            return new PreviewFrame(t < revealMs, logout.EffectId, logout.FrameAt(t));

        return Empty;
    }

    /// <summary>A hold, the death effect (the character vanishes at <paramref name="hideMs" />), then an empty gap.</summary>
    public static PreviewFrame Death(EffectClip death, int hideMs, double timeMs)
    {
        var loop = HOLD_MS + death.LengthMs + GAP_MS;
        var t = timeMs % loop;

        if (t < HOLD_MS)
            return Standing;

        t -= HOLD_MS;

        if (t < death.LengthMs)
            return new PreviewFrame(t < hideMs, death.EffectId, death.FrameAt(t));

        return Empty;
    }

    /// <summary>The tile's still frame: the one showing at <paramref name="keyMs" />. -1 when the effect is missing.</summary>
    public static int StillFrame(EffectClip clip, int keyMs) => clip.FrameCount == 0 ? -1 : clip.FrameAt(keyMs);
}
