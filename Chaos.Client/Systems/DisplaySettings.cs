namespace Chaos.Client.Systems;

/// <summary>
///     Window/display modes selectable from the F4 Resolution dropdown. The enum ordinal IS the
///     dropdown index and the int persisted to Darkages.cfg. Named to avoid clashing with MonoGame's
///     Microsoft.Xna.Framework.Graphics.DisplayMode.
/// </summary>
public enum ScreenMode
{
    Windowed1x,
    Windowed2x,
    Windowed3x,
    Windowed4x,
    BorderlessLetterbox,
    BorderlessStretch
}

/// <summary>
///     Bridges the F4 Resolution dropdown to the actual window. <see cref="Apply" /> persists the
///     chosen <see cref="ScreenMode" /> to <see cref="ClientSettings" /> and invokes <see cref="Applier" />,
///     which ChaosGame registers in order to resize the window. Decoupled so neither ClientSettings nor
///     the ViewModel layer needs a reference to ChaosGame.
/// </summary>
public static class DisplaySettings
{
    /// <summary>Dropdown labels. Order MUST match <see cref="ScreenMode" /> ordinals.</summary>
    public static readonly IReadOnlyList<string> OptionLabels =
    [
        "640 x 480",
        "1280 x 960",
        "1920 x 1440",
        "2560 x 1920",
        "Borderless FS",
        "Borderless FS(Stretch)"
    ];

    /// <summary>Registered by ChaosGame to apply the mode to the GraphicsDeviceManager. Null before the game starts.</summary>
    public static Action<ScreenMode>? Applier { get; set; }

    /// <summary>Persists the chosen mode and applies it live (when the game is running).</summary>
    public static void Apply(int index)
    {
        if ((index < 0) || (index >= OptionLabels.Count))
            return;

        var mode = (ScreenMode)index;
        ClientSettings.ScreenMode = mode;
        ClientSettings.Save();
        Applier?.Invoke(mode);
    }
}
