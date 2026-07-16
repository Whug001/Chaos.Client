#region
using System.Runtime.InteropServices;
#endregion

namespace Chaos.Client;

/// <summary>
///     Win11-only opt-out from the OS cursor-scaling pipeline. We build the hardware cursor at physical
///     window-pixel size ourselves; without this, Windows 11 multiplies it again by per-monitor DPI and by the
///     Accessibility "mouse pointer size" slider, so a player who enlarged their pointer sees a compounded,
///     oversized cursor. Opting the thread out makes the OS present cursors at the exact pixels we hand it.
/// </summary>
internal static partial class Win32Cursor
{
    //winuser.h CURSOR_CREATION_SCALING_NONE: the "never scale" sentinel in the 96-based DPI parameter space.
    private const uint CURSOR_CREATION_SCALING_NONE = 1;

    //Win11 build 22000+ only; the export is absent on Win10, so callers must gate on the OS version below.
    [LibraryImport("user32.dll")]
    private static partial uint SetThreadCursorCreationScaling(uint cursorDpi);

    /// <summary>
    ///     Opts every cursor created afterward on the calling thread out of OS DPI/accessibility scaling.
    ///     Thread-local and sticky, so one call before the first <c>MouseCursor.FromTexture2D</c> also covers
    ///     later scale rebuilds. No-op below Windows 11 build 22000, where the API does not exist.
    /// </summary>
    public static void DisableOsScalingForThisThread()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            return;

        SetThreadCursorCreationScaling(CURSOR_CREATION_SCALING_NONE);
    }
}
