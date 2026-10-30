namespace Chaos.Client.Systems;

/// <summary>
///     Frame pacing modes selectable from the F4 Frame rate dropdown. The enum ordinal IS the dropdown index and the int
///     persisted to Settings.cfg, so new modes may only be added at the end.
/// </summary>
public enum FrameRateMode
{
    Fixed60,
    MatchMonitor,
    Fixed120,
    Fixed144,
    Fixed165,
    Fixed240,
    Fixed320
}

/// <summary>
///     Bridges the F4 Frame rate dropdown to the game loop. <see cref="Apply" /> persists the chosen
///     <see cref="FrameRateMode" /> to <see cref="ClientSettings" /> and invokes <see cref="Applier" />, which ChaosGame
///     registers in order to change its timing. Shaped like <see cref="DisplaySettings" /> for the same reason: neither
///     ClientSettings nor the ViewModel layer needs a reference to ChaosGame.
/// </summary>
/// <remarks>
///     One setting covers both the cap and vsync so a player cannot pick a fixed step that fights vsync. Every
///     Update in the client times itself from elapsed time rather than counting frames (audited 2026-10-26), so any
///     of these rates runs the game at the same speed.
/// </remarks>
public static class FrameRateSettings
{
    /// <summary>Dropdown labels. Order MUST match <see cref="FrameRateMode" /> ordinals.</summary>
    public static readonly IReadOnlyList<string> OptionLabels =
    [
        "60 (default)",
        "Match monitor (vsync)",
        "120",
        "144",
        "165",
        "240",
        "320"
    ];

    /// <summary>Registered by ChaosGame; applies a mode to the running game loop.</summary>
    public static Action<FrameRateMode>? Applier { get; set; }

    /// <summary>
    ///     The MonoGame timing values for a mode. Pure, so the mapping can be tested without a graphics device.
    /// </summary>
    /// <remarks>
    ///     <see cref="FrameRateMode.MatchMonitor" /> still returns a 60fps target: MonoGame ignores it while the fixed
    ///     step is off, and it keeps the value sane if anything reads it.
    /// </remarks>
    public static (bool FixedStep, TimeSpan Target, bool VSync) Resolve(FrameRateMode mode)
        => mode switch
        {
            FrameRateMode.MatchMonitor => (false, PerSecond(60), true),
            FrameRateMode.Fixed120     => (true, PerSecond(120), false),
            FrameRateMode.Fixed144     => (true, PerSecond(144), false),
            FrameRateMode.Fixed165     => (true, PerSecond(165), false),
            FrameRateMode.Fixed240     => (true, PerSecond(240), false),
            FrameRateMode.Fixed320     => (true, PerSecond(320), false),
            _                          => (true, PerSecond(60), false)
        };

    /// <summary>
    ///     The fixed-step cap for <see cref="FrameRateMode.MatchMonitor" />: the monitor's own refresh rate, or 240
    ///     when it is unknown, so the loop never runs uncapped if vsync is ignored.
    /// </summary>
    public static int MatchMonitorCap(int refreshRateHz) => refreshRateHz > 0 ? refreshRateHz : 240;

    /// <summary>The frame budget in milliseconds, or null when vsync paces the loop and there is no fixed target.</summary>
    public static float? TargetFrameMs(FrameRateMode mode)
    {
        var (fixedStep, target, _) = Resolve(mode);

        return fixedStep ? (float)target.TotalMilliseconds : null;
    }

    /// <summary>Reads a stored value. Refuses anything that is not a defined mode, so a bad file keeps the default.</summary>
    public static bool TryParse(string value, out FrameRateMode mode)
    {
        mode = FrameRateMode.Fixed60;

        if (!int.TryParse(value, out var index) || !Enum.IsDefined((FrameRateMode)index))
            return false;

        mode = (FrameRateMode)index;

        return true;
    }

    /// <summary>Persists the chosen mode and applies it live (when the game is running).</summary>
    public static void Apply(int index)
    {
        if ((index < 0) || (index >= OptionLabels.Count))
            return;

        var mode = (FrameRateMode)index;
        ClientSettings.FrameRateMode = mode;
        ClientSettings.Save();
        Applier?.Invoke(mode);
    }

    private static TimeSpan PerSecond(int hz) => TimeSpan.FromTicks(TimeSpan.TicksPerSecond / hz);
}
