#region
using Chaos.Client.Definitions;
#endregion

namespace Chaos.Client.Systems.KeyBinds;

/// <summary>
///     One key press a binding answers to: a physical key (scancode) and the modifiers held with it. Only Shift, Ctrl
///     and Alt count; the Windows/Command key is dropped so it never stops a binding from matching.
/// </summary>
public readonly record struct KeyChord
{
    private const KeyModifiers COUNTED = KeyModifiers.Shift | KeyModifiers.Ctrl | KeyModifiers.Alt;

    public static readonly KeyChord None = default;

    public KeyChord(Scancode key, KeyModifiers modifiers = KeyModifiers.None)
    {
        Key = key;
        Modifiers = modifiers & COUNTED;
    }

    public Scancode Key { get; }
    public KeyModifiers Modifiers { get; }

    public bool IsNone => Key == Scancode.Unknown;

    /// <summary>The same key with no modifiers.</summary>
    public KeyChord Bare => new(Key);

    public bool Shift => (Modifiers & KeyModifiers.Shift) != 0;
    public bool Ctrl => (Modifiers & KeyModifiers.Ctrl) != 0;
    public bool Alt => (Modifiers & KeyModifiers.Alt) != 0;

    public static KeyChord From(KeyEvent e) => new(e.Scancode, e.Modifiers);

    /// <summary>True for the keys that only ever modify another key; they cannot be bound on their own.</summary>
    public static bool IsModifierKey(Scancode key)
        => key is >= Scancode.LeftControl and <= Scancode.RightWindows or Scancode.CapsLock or Scancode.NumLock;

    /// <summary>The settings-file form, e.g. "Ctrl+D1", or "None". Round-trips through <see cref="TryParse" />.</summary>
    public string Serialize()
    {
        if (IsNone)
            return "None";

        return ModifierPrefix() + Key;
    }

    public static bool TryParse(string text, out KeyChord chord)
    {
        chord = None;
        text = text.Trim();

        if (text.Length == 0)
            return false;

        if (text.Equals("None", StringComparison.OrdinalIgnoreCase))
            return true;

        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
            return false;

        var modifiers = KeyModifiers.None;

        for (var i = 0; i < (parts.Length - 1); i++)
            switch (parts[i].ToLowerInvariant())
            {
                case "shift":
                    modifiers |= KeyModifiers.Shift;

                    break;
                case "ctrl":
                    modifiers |= KeyModifiers.Ctrl;

                    break;
                case "alt":
                    modifiers |= KeyModifiers.Alt;

                    break;
                default:
                    return false;
            }

        if (!Enum.TryParse<Scancode>(parts[^1], true, out var key) || (key == Scancode.Unknown) || IsModifierKey(key))
            return false;

        chord = new KeyChord(key, modifiers);

        return true;
    }

    /// <summary>How the chord reads in the key bindings window, e.g. "Ctrl+1" or "Page Up". Empty for <see cref="None" />.</summary>
    public string DisplayName => IsNone ? string.Empty : ModifierPrefix() + KeyNames.Get(Key);

    /// <summary>The held modifiers as they read in front of a key, e.g. "Ctrl+Alt+", or empty.</summary>
    public string ModifierPrefix() => (Ctrl ? "Ctrl+" : string.Empty) + (Alt ? "Alt+" : string.Empty) + (Shift ? "Shift+" : string.Empty);

    public override string ToString() => Serialize();
}
