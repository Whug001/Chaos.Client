#region
using Chaos.Client.Definitions;
#endregion

namespace Chaos.Client.Systems.KeyBinds;

/// <summary>
///     The bindings in use. Loaded and saved with the rest of <see cref="ClientSettings" />, so each character keeps their
///     own, and a character with none of their own starts from the install-wide file.
/// </summary>
public static class KeyBindings
{
    public static KeyBindingTable Current { get; } = new();

    /// <summary>True while the key bindings window is waiting for a key, so the global keys stand aside.</summary>
    public static bool IsCapturing { get; set; }

    /// <summary>
    ///     True when a key bound to <paramref name="action" /> went down this frame with exactly its modifiers held. For
    ///     the keys read outside the UI dispatch (the screenshot key in <c>ChaosGame</c>).
    /// </summary>
    public static bool WasPressed(GameAction action)
    {
        if (IsCapturing)
            return false;

        var held = new KeyChord(Scancode.Unknown, InputBuffer.CurrentModifiers).Modifiers;

        foreach (var chord in Current.ChordsFor(action))
            if (InputBuffer.WasScancodePressed(chord.Key) && (chord.Modifiers == held))
                return true;

        return false;
    }

    /// <summary>True while any key bound to <paramref name="action" /> is held, whatever the modifiers.</summary>
    public static bool IsHeld(GameAction action)
    {
        foreach (var chord in Current.ChordsFor(action))
            if (InputBuffer.IsScancodeHeld(chord.Key))
                return true;

        return false;
    }
}
