namespace Chaos.Client.Definitions;

/// <summary>
///     A physical key position (SDL scancode). Values are the raw
///     <c>SDL_Scancode</c> numbers, so the SDL event byte casts directly to this enum with
///     no lookup table. A scancode names a location on the keyboard by its US-QWERTY label:
///     <see cref="A" /> is the bottom-left letter key regardless of what the current layout
///     prints there (it is "Q" on AZERTY). Use scancodes for positional hotkeys — movement,
///     slots, panel toggles — so a bind lands on the same physical key on every layout.
/// </summary>
public enum Scancode
{
    Unknown = 0,

    A = 4,
    B = 5,
    C = 6,
    D = 7,
    E = 8,
    F = 9,
    G = 10,
    H = 11,
    I = 12,
    J = 13,
    K = 14,
    L = 15,
    M = 16,
    N = 17,
    O = 18,
    P = 19,
    Q = 20,
    R = 21,
    S = 22,
    T = 23,
    U = 24,
    V = 25,
    W = 26,
    X = 27,
    Y = 28,
    Z = 29,

    //number row (numpad digits are folded onto these at the input boundary)
    D1 = 30,
    D2 = 31,
    D3 = 32,
    D4 = 33,
    D5 = 34,
    D6 = 35,
    D7 = 36,
    D8 = 37,
    D9 = 38,
    D0 = 39,

    Enter = 40,
    Escape = 41,
    Back = 42,
    Tab = 43,
    Space = 44,

    //punctuation — named by the US-QWERTY label at that physical position
    OemMinus = 45,
    OemPlus = 46,
    OemOpenBrackets = 47,
    OemCloseBrackets = 48,
    OemPipe = 49,
    OemSemicolon = 51,
    OemQuotes = 52,
    OemTilde = 53,
    OemComma = 54,
    OemPeriod = 55,
    OemQuestion = 56,

    CapsLock = 57,

    F1 = 58,
    F2 = 59,
    F3 = 60,
    F4 = 61,
    F5 = 62,
    F6 = 63,
    F7 = 64,
    F8 = 65,
    F9 = 66,
    F10 = 67,
    F11 = 68,
    F12 = 69,

    PrintScreen = 70,
    Scroll = 71,
    Pause = 72,
    Insert = 73,
    Home = 74,
    PageUp = 75,
    Delete = 76,
    End = 77,
    PageDown = 78,
    Right = 79,
    Left = 80,
    Down = 81,
    Up = 82,

    NumLock = 83,

    LeftControl = 224,
    LeftShift = 225,
    LeftAlt = 226,
    LeftWindows = 227,
    RightControl = 228,
    RightShift = 229,
    RightAlt = 230,
    RightWindows = 231
}

/// <summary>
///     A layout-mapped virtual key (SDL keycode). Values are the raw <c>SDL_Keycode</c>
///     numbers (SDL2 encoding): printable keys are their unshifted ASCII codepoint, named
///     keys are their scancode OR'd with the <c>1 &lt;&lt; 30</c> mask. A keycode names the
///     symbol the current layout <em>produces</em> — the key that types 'c' is
///     <see cref="C" /> on every layout — so it is the right identity for label-following
///     shortcuts (copy/paste/select-all) and text editing.
/// </summary>
public enum Keycode
{
    Unknown = 0,

    Back = '\b',
    Tab = '\t',
    Enter = '\r',
    Escape = 27,
    Space = ' ',

    //the two labels the ISO key immediately left of Enter produces on QWERTZ/AZERTY —
    //used only to disambiguate that key from the ANSI backslash for the whisper hotkey.
    Hash = '#',
    Asterisk = '*',

    D0 = '0',
    D1 = '1',
    D2 = '2',
    D3 = '3',
    D4 = '4',
    D5 = '5',
    D6 = '6',
    D7 = '7',
    D8 = '8',
    D9 = '9',

    A = 'a',
    B = 'b',
    C = 'c',
    D = 'd',
    E = 'e',
    F = 'f',
    G = 'g',
    H = 'h',
    I = 'i',
    J = 'j',
    K = 'k',
    L = 'l',
    M = 'm',
    N = 'n',
    O = 'o',
    P = 'p',
    Q = 'q',
    R = 'r',
    S = 's',
    T = 't',
    U = 'u',
    V = 'v',
    W = 'w',
    X = 'x',
    Y = 'y',
    Z = 'z',

    Delete = 127,

    //named keys carry the SDL scancode-mask bit (1 << 30)
    CapsLock = (1 << 30) | 57,
    F1 = (1 << 30) | 58,
    F2 = (1 << 30) | 59,
    F3 = (1 << 30) | 60,
    F4 = (1 << 30) | 61,
    F5 = (1 << 30) | 62,
    F6 = (1 << 30) | 63,
    F7 = (1 << 30) | 64,
    F8 = (1 << 30) | 65,
    F9 = (1 << 30) | 66,
    F10 = (1 << 30) | 67,
    F11 = (1 << 30) | 68,
    F12 = (1 << 30) | 69,
    Insert = (1 << 30) | 73,
    Home = (1 << 30) | 74,
    PageUp = (1 << 30) | 75,
    End = (1 << 30) | 77,
    PageDown = (1 << 30) | 78,
    Right = (1 << 30) | 79,
    Left = (1 << 30) | 80,
    Down = (1 << 30) | 81,
    Up = (1 << 30) | 82
}
