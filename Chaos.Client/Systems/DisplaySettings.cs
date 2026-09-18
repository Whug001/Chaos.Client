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

    /// <summary>
    ///     The largest box with the same aspect ratio as <paramref name="virtualWidth" />x
    ///     <paramref name="virtualHeight" /> that fits inside <paramref name="width" />x<paramref name="height" />.
    /// </summary>
    /// <remarks>
    ///     Pure, and parameterised by the virtual size rather than reading it off the game, so the window maths
    ///     can be tested without a graphics device. It is the same correction a manual resize gets: the render
    ///     target is a fixed 4:3, and a window that is not gains bars rather than stretching.
    ///     <para />
    ///     Integer arithmetic rather than a float ratio. 4:3 has no exact float, and the nearest one is a hair
    ///     over, so dividing by it and truncating turned 800 wide into 599 tall rather than 600 -- a box one pixel
    ///     off 4:3, which the next resize would then correct again by another pixel. Multiplying before dividing
    ///     is exact for every size a window can be.
    /// </remarks>
    public static (int Width, int Height) FitToAspect(
        int width,
        int height,
        int virtualWidth,
        int virtualHeight)
    {
        if ((width <= 0) || (height <= 0) || (virtualWidth <= 0) || (virtualHeight <= 0))
            return (0, 0);

        var correctedWidth = (height * virtualWidth) / virtualHeight;

        return correctedWidth <= width
            ? (correctedWidth, height)
            : (width, (width * virtualHeight) / virtualWidth);
    }

    /// <summary>
    ///     Works out the window size to use from a stored manual size, or reports that there is not a usable one
    ///     and the caller should fall back to <see cref="ScreenMode" />'s own multiple.
    /// </summary>
    /// <remarks>
    ///     The stored size is fitted to the display and to the aspect ratio rather than trusted as written. It is
    ///     persisted per character, so the file can easily have been written on another machine or another
    ///     monitor; a stored size taller than the display would otherwise put the title bar off the top of the
    ///     screen with no way back to it. A result smaller than one whole virtual pixel per screen pixel is
    ///     refused rather than clamped -- there is nothing to show at that size.
    /// </remarks>
    public static bool TryResolveStoredSize(
        int storedWidth,
        int storedHeight,
        int displayWidth,
        int displayHeight,
        int virtualWidth,
        int virtualHeight,
        out int width,
        out int height)
    {
        width = 0;
        height = 0;

        if ((storedWidth <= 0) || (storedHeight <= 0))
            return false;

        (width, height) = FitToAspect(
            Math.Min(storedWidth, displayWidth),
            Math.Min(storedHeight, displayHeight),
            virtualWidth,
            virtualHeight);

        if ((width >= virtualWidth) && (height >= virtualHeight))
            return true;

        width = 0;
        height = 0;

        return false;
    }

    /// <summary>Registered by ChaosGame to apply the mode to the GraphicsDeviceManager. Null before the game starts.</summary>
    public static Action<ScreenMode>? Applier { get; set; }

    /// <summary>Persists the chosen mode and applies it live (when the game is running).</summary>
    /// <remarks>
    ///     Choosing from the dropdown forgets any size the player had dragged the window to. Picking a resolution
    ///     is them asking for that exact size, and it is also the way back: resize the window by hand, then pick
    ///     the same entry again to snap it to the multiple.
    /// </remarks>
    public static void Apply(int index)
    {
        if ((index < 0) || (index >= OptionLabels.Count))
            return;

        var mode = (ScreenMode)index;
        ClientSettings.ScreenMode = mode;
        ClientSettings.ClearCustomWindowSize();
        ClientSettings.Save();
        Applier?.Invoke(mode);
    }
}
