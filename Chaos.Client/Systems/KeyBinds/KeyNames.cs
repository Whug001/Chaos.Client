#region
using Chaos.Client.Definitions;
#endregion

namespace Chaos.Client.Systems.KeyBinds;

/// <summary>
///     The name a key is shown under. Letters, digits and punctuation follow the player's keyboard layout (the key the
///     binding sits on reads "Q" on an AZERTY board where a US board says "A"); the named keys use fixed English names.
/// </summary>
public static class KeyNames
{
    /// <summary>
    ///     Asks the layout what a key types, or null when it cannot say. Points at SDL in the running game; left null
    ///     where SDL is not loaded (tests), which falls back to the US-QWERTY label.
    /// </summary>
    public static Func<Scancode, char?>? LayoutLookup { get; set; }

    public static string Get(Scancode key)
    {
        if (IsPrintable(key) && (LayoutLookup?.Invoke(key) is { } typed))
            return char.ToUpperInvariant(typed).ToString();

        return key switch
        {
            >= Scancode.A and <= Scancode.Z => ((char)('A' + (key - Scancode.A))).ToString(),
            >= Scancode.D1 and <= Scancode.D9 => ((char)('1' + (key - Scancode.D1))).ToString(),
            Scancode.D0 => "0",
            >= Scancode.F1 and <= Scancode.F12 => $"F{key - Scancode.F1 + 1}",
            Scancode.Enter => "Enter",
            Scancode.Escape => "Esc",
            Scancode.Back => "Backspace",
            Scancode.Tab => "Tab",
            Scancode.Space => "Space",
            Scancode.OemMinus => "-",
            Scancode.OemPlus => "=",
            Scancode.OemOpenBrackets => "[",
            Scancode.OemCloseBrackets => "]",
            Scancode.OemPipe => "\\",
            Scancode.OemSemicolon => ";",
            Scancode.OemQuotes => "'",
            Scancode.OemTilde => "`",
            Scancode.OemComma => ",",
            Scancode.OemPeriod => ".",
            Scancode.OemQuestion => "/",
            Scancode.PrintScreen => "Print Screen",
            Scancode.Scroll => "Scroll Lock",
            Scancode.Pause => "Pause",
            Scancode.Insert => "Insert",
            Scancode.Home => "Home",
            Scancode.PageUp => "Page Up",
            Scancode.Delete => "Delete",
            Scancode.End => "End",
            Scancode.PageDown => "Page Down",
            Scancode.Right => "Right",
            Scancode.Left => "Left",
            Scancode.Down => "Down",
            Scancode.Up => "Up",
            _ => $"Key {(int)key}"
        };
    }

    //the keys whose label depends on the layout
    private static bool IsPrintable(Scancode key) => key is >= Scancode.A and <= Scancode.D0 or >= Scancode.OemMinus and <= Scancode.OemQuestion;
}
