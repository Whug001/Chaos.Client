#region
using System.Runtime.InteropServices;
using Chaos.Client.Definitions;
#endregion

namespace Chaos.Client;

/// <summary>
///     Static, process-global input buffer. Captures keyboard, text, mouse button, and
///     mouse wheel events from SDL via a single <c>SDL_AddEventWatch</c> callback so that
///     every discrete event is preserved in its true OS post order and carries the modifier
///     state that was live at the moment it fired. Also tracks the live cursor position
///     (refreshed from <c>SDL_GetMouseState</c> on every <see cref="Update" />) and the
///     per-window button-held flags.
///     <para>
///         Lifecycle: call <see cref="Initialize" /> once at startup (installs the SDL
///         watcher), then <see cref="Update" /> at the start of every frame (drains the
///         accumulated events into the frame snapshot and refreshes the cursor position),
///         and <see cref="Shutdown" /> on application exit (removes the watcher). Any code
///         can read the static query surface: <see cref="MouseX" /> / <see cref="MouseY" />,
///         <see cref="IsLeftButtonHeld" /> / <see cref="IsRightButtonHeld" />,
///         <see cref="IsScancodeHeld" /> / <see cref="WasScancodePressed" /> / <see cref="WasScancodeReleased" />,
///         <see cref="TextInput" />, and the chronologically-ordered <see cref="Events" /> stream.
///     </para>
/// </summary>
public static class InputBuffer
{
    //─────────────────────────────────────────────────────────────────────────────
    //  live state (held / tracked across frames, updated by the SDL watcher)
    //─────────────────────────────────────────────────────────────────────────────

    //the held-key set: each currently-held physical key mapped to the keycode it produced at
    //press time. keyed by scancode — the stable physical identity — so held state can never be
    //stranded by a mid-hold layout switch. keycodes are layout-dependent, so a KeyUp emits the
    //same keycode the press did even if the layout switched mid-hold.
    private static readonly Dictionary<Scancode, Keycode> PressedKeycodeByScancode = [];
    private static int RawMouseX;
    private static int RawMouseY;
    private static float VirtualScaleX = 1f;
    private static float VirtualScaleY = 1f;
    private static int VirtualOffsetX;
    private static int VirtualOffsetY;

    //─────────────────────────────────────────────────────────────────────────────
    //  accumulation buffer (filled by the watcher between Update() calls)
    //─────────────────────────────────────────────────────────────────────────────

    //authoritative chronological event stream — keyboard, text, mouse button, and
    //mouse wheel events interleaved in true OS post order. the dispatcher walks it
    //each frame, and Update() scans it once to populate the query-style frame
    //snapshot (FrameKeyPresses/FrameKeyReleases/TextBuffer).
    private static readonly List<BufferedInputEvent> PendingEvents = [];

    //─────────────────────────────────────────────────────────────────────────────
    //  frame snapshot (frozen at the start of each Update())
    //─────────────────────────────────────────────────────────────────────────────

    private static readonly HashSet<Scancode> FrameKeyPresses = [];
    private static readonly HashSet<Scancode> FrameKeyReleases = [];
    private static BufferedInputEvent[] EventBuffer = [];
    private static int EventCount;
    private static char[] TextBuffer = [];
    private static int TextCount;
    private static bool WasActivePreviousFrame = true;

    //─────────────────────────────────────────────────────────────────────────────
    //  unmanaged callback lifetime
    //─────────────────────────────────────────────────────────────────────────────

    //the SDL event watcher delegate — must be held in a static field so the GC doesn't
    //collect it while SDL still has the function pointer. SdlEventWatchPtr is also kept
    //so Shutdown() can pass the exact same pointer to SDL_DelEventWatch.
    private static Sdl.EventWatchCallback? SdlEventWatch;
    private static nint SdlEventWatchPtr;
    private static bool Initialized;

    //─────────────────────────────────────────────────────────────────────────────
    //  public query surface
    //─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Current cursor X in virtual coordinates (640×480). Refreshed from
    ///     <c>SDL_GetMouseState</c> at the end of every <see cref="Update" /> after
    ///     <c>SDL_PumpEvents</c>, so it reflects the true end-of-frame cursor position
    ///     even when a macro fires its trailing move mid-frame.
    /// </summary>
    public static int MouseX => ToVirtualX(RawMouseX);

    /// <summary>
    ///     Current cursor Y in virtual coordinates (640×480). See <see cref="MouseX" />.
    /// </summary>
    public static int MouseY => ToVirtualY(RawMouseY);

    //single point where raw window pixels → virtual 640×480 coords. called from the
    //MouseX/MouseY getters and from the per-event coordinate capture in the SDL
    //watcher — must always use the same transform so polled and event positions agree.
    //scale is per-axis because the backbuffer can be non-4:3 when the window is maximized
    //(the render target is stretch-drawn to fill in that case, not letterboxed).
    private static int ToVirtualX(int raw) => (int)((raw - VirtualOffsetX) / VirtualScaleX);
    private static int ToVirtualY(int raw) => (int)((raw - VirtualOffsetY) / VirtualScaleY);

    /// <summary>
    ///     True while the left mouse button is held down. Flipped per-event by the SDL
    ///     watcher — a click in another application never sets this to <c>true</c>,
    ///     unlike MonoGame's <c>Mouse.GetState().LeftButton</c> which reports global state.
    /// </summary>
    public static bool IsLeftButtonHeld { get; private set; }

    /// <summary>
    ///     True while the right mouse button is held down. Same per-window semantics as
    ///     <see cref="IsLeftButtonHeld" />.
    /// </summary>
    public static bool IsRightButtonHeld { get; private set; }

    /// <summary>
    ///     Returns true if the physical key is currently held down (event-tracked, not
    ///     polled). Keyed by scancode so it is layout-independent — the caller asks about a
    ///     physical position, not a printed label.
    /// </summary>
    public static bool IsScancodeHeld(Scancode scancode) => PressedKeycodeByScancode.ContainsKey(scancode);

    /// <summary>
    ///     Returns true if the physical key had a rising edge (was pressed) during this
    ///     frame. OS key-repeat events are filtered out — only the initial press fires.
    /// </summary>
    public static bool WasScancodePressed(Scancode scancode) => FrameKeyPresses.Contains(scancode);

    /// <summary>
    ///     Returns true if the physical key had a falling edge (was released) during this frame.
    /// </summary>
    public static bool WasScancodeReleased(Scancode scancode) => FrameKeyReleases.Contains(scancode);

    /// <summary>
    ///     Characters typed during this frame (from TextInput events). Includes OS
    ///     key-repeat characters.
    /// </summary>
    public static ReadOnlySpan<char> TextInput => TextBuffer.AsSpan(0, TextCount);

    /// <summary>
    ///     Chronologically ordered input events for this frame. Keyboard, text, mouse
    ///     button, and mouse wheel events interleaved in the exact OS post order captured
    ///     by the SDL watcher. Consumers walk this stream and dispatch each event in
    ///     sequence so that rapid macros which mix multiple input kinds fire in their
    ///     original order rather than being reordered to all-of-type-A-then-all-of-type-B.
    /// </summary>
    public static ReadOnlySpan<BufferedInputEvent> Events => EventBuffer.AsSpan(0, EventCount);

    /// <summary>
    ///     Live modifier state from SDL — the same snapshot stamped onto each per-event
    ///     modifier field. Used for synthesized events (e.g. MouseMove) that don't have
    ///     an underlying buffered event to source modifiers from.
    /// </summary>
    public static KeyModifiers CurrentModifiers => TranslateSdlMods(Sdl.SDL_GetModState());

    /// <summary>
    ///     Sets the virtual-to-raw scale factor used by <see cref="MouseX" /> /
    ///     <see cref="MouseY" /> and by the per-click coordinate capture in the SDL watcher.
    ///     Called by <c>ChaosGame</c> whenever the window size changes.
    /// </summary>
    public static void SetVirtualScale(float scale)
    {
        VirtualScaleX = scale;
        VirtualScaleY = scale;
        VirtualOffsetX = 0;
        VirtualOffsetY = 0;
    }

    /// <summary>
    ///     Sets the per-axis raw→virtual scale plus an optional pixel offset (the top-left of the
    ///     presented render target within the backbuffer) that raw coordinates are measured from.
    ///     The offset is non-zero only in borderless-letterbox mode, where the 640×480 target is
    ///     centered with black bars; in stretched/windowed modes it fills the backbuffer (offset 0).
    /// </summary>
    public static void SetVirtualScale(float scaleX, float scaleY, int offsetX = 0, int offsetY = 0)
    {
        VirtualScaleX = scaleX;
        VirtualScaleY = scaleY;
        VirtualOffsetX = offsetX;
        VirtualOffsetY = offsetY;
    }

    //─────────────────────────────────────────────────────────────────────────────
    //  lifecycle
    //─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Installs the SDL event watcher. Call once at startup, before the first
    ///     <see cref="Update" />. Idempotent — repeat calls are no-ops.
    /// </summary>
    public static void Initialize()
    {
        if (Initialized)
            return;

        SdlEventWatch = OnSdlEvent;
        SdlEventWatchPtr = Marshal.GetFunctionPointerForDelegate(SdlEventWatch);
        Sdl.SDL_AddEventWatch(SdlEventWatchPtr, nint.Zero);
        Initialized = true;
    }

    /// <summary>
    ///     Removes the SDL event watcher. Call once at application exit.
    /// </summary>
    public static void Shutdown()
    {
        if (!Initialized)
            return;

        Sdl.SDL_DelEventWatch(SdlEventWatchPtr, nint.Zero);
        SdlEventWatchPtr = nint.Zero;
        SdlEventWatch = null;
        Initialized = false;
    }

    /// <summary>
    ///     Freezes all buffered input for this frame. Call once at the start of each
    ///     game update before any consumer reads the query surface or the event stream.
    /// </summary>
    /// <param name="isActive">Whether the game window currently has focus. When <c>false</c>, all buffered input is dropped.</param>
    public static void Update(bool isActive)
    {
        //drain the OS event queue → our watcher fires for every event posted since
        //MonoGame's start-of-tick pump, SDL's internal cursor state advances to its
        //real current position, and any wheel notches arriving mid-frame are captured.
        //without this a macro that fires move→click→move→click→move in one frame would
        //leave the cursor's last-known position stuck at the penultimate move until
        //the next pump.
        Sdl.SDL_PumpEvents();

        EventCount = 0;
        TextCount = 0;
        FrameKeyPresses.Clear();
        FrameKeyReleases.Clear();

        //clear stuck held state on the active→inactive edge. while another window has
        //focus SDL doesn't deliver key/button-up events to us, so held flags would
        //otherwise persist as stale state until the user refocuses and re-taps.
        if (WasActivePreviousFrame && !isActive)
        {
            PressedKeycodeByScancode.Clear();
            IsLeftButtonHeld = false;
            IsRightButtonHeld = false;
        }

        WasActivePreviousFrame = isActive;

        //do NOT early-return on !isActive: when the user clicks the unfocused window
        //with SDL_HINT_MOUSE_FOCUS_CLICKTHROUGH enabled, SDL queues MOUSEBUTTONDOWN one
        //frame before MonoGame's IsActive transitions to true. the watcher has already
        //captured it into PendingEvents — dropping here would swallow the focus click.
        //the watcher only delivers events for our window, so buffered events are always
        //legitimately ours regardless of the IsActive snapshot at Update time.

        //freeze the unified event stream and derive the query-style frame snapshot
        //in one pass. FrameKeyPresses/FrameKeyReleases/TextBuffer exist only so that
        //the O(1) accessors (WasScancodePressed etc.) don't have to scan Events each call.
        var pendingCount = PendingEvents.Count;

        if (pendingCount > 0)
        {
            if (EventBuffer.Length < pendingCount)
                EventBuffer = new BufferedInputEvent[Math.Max(pendingCount, 16)];

            //TextBuffer is sized to the total pending count which is always ≥ text
            //event count — conservative but only grows on spikes.
            if (TextBuffer.Length < pendingCount)
                TextBuffer = new char[Math.Max(pendingCount, 16)];

            for (var i = 0; i < pendingCount; i++)
            {
                var evt = PendingEvents[i];
                EventBuffer[EventCount++] = evt;

                switch (evt.Kind)
                {
                    case BufferedInputKind.KeyDown:
                        FrameKeyPresses.Add(evt.Scancode);

                        break;
                    case BufferedInputKind.KeyUp:
                        FrameKeyReleases.Add(evt.Scancode);

                        break;
                    case BufferedInputKind.TextInput:
                        TextBuffer[TextCount++] = evt.Character;

                        break;
                }
            }

            PendingEvents.Clear();
        }

        //read the latest cursor position from SDL's internal state. the pump at the
        //top of this method guarantees this reflects every OS event up to now, so
        //MouseX / MouseY always show the true end-state of the cursor this frame.
        _ = Sdl.SDL_GetMouseState(out RawMouseX, out RawMouseY);
    }

    //─────────────────────────────────────────────────────────────────────────────
    //  SDL event watcher
    //─────────────────────────────────────────────────────────────────────────────

    //all keyboard, text-input, mouse-button and mouse-wheel events funnel through
    //this single watcher callback. SDL fires it synchronously during SDL_PumpEvents
    //on the main thread, in the exact order the OS posted events — that shared
    //ordering is what lets the dispatcher later reconstruct per-event modifier state
    //and preserve the true temporal relationship between keyboard and mouse input.
    private static int OnSdlEvent(nint userdata, nint sdlEvent)
    {
        var eventType = (uint)Marshal.ReadInt32(sdlEvent);

        switch (eventType)
        {
            case Sdl.KEYDOWN:
            case Sdl.KEYUP:
                HandleKeyEvent(sdlEvent, eventType == Sdl.KEYDOWN);

                break;

            case Sdl.TEXTINPUT:
                HandleTextInputEvent(sdlEvent);

                break;

            case Sdl.MOUSEBUTTONDOWN:
            case Sdl.MOUSEBUTTONUP:
                HandleMouseButtonEvent(sdlEvent, eventType == Sdl.MOUSEBUTTONDOWN);

                break;

            case Sdl.MOUSEWHEEL:
                HandleMouseWheelEvent(sdlEvent);

                break;
        }

        return 1;
    }

    private static void HandleKeyEvent(nint sdlEvent, bool isDown)
    {
        //raw scancode (physical position) and keycode (layout-mapped label) are recorded
        //verbatim — no translation policy. consumers decide per-hotkey which one they want:
        //positional hotkeys read Scancode, label-following shortcuts read Keycode.
        var scancode = NormalizeScancode(Marshal.ReadInt32(sdlEvent, Sdl.KEYBOARDEVENT_SCANCODE_OFFSET));

        if (scancode == Scancode.Unknown)
            return;

        //SDL stamps keysym.mod with the live modifier state at event time. reading
        //it per-event keeps a chorded keydown's modifier bit attached to the event
        //itself, so a macro's KEYDOWN(D) still reports Shift even when the bracketing
        //KEYDOWN(Shift) lands in a different frame.
        var sdlMods = (uint)(ushort)Marshal.ReadInt16(sdlEvent, Sdl.KEYBOARDEVENT_MOD_OFFSET);
        var mods = TranslateSdlMods(sdlMods);

        if (isDown)
        {
            //record the keycode ONCE per hold and reuse it for every KEYDOWN of the hold,
            //including unfiltered OS key-repeats. keycodes are layout-dependent, so a layout
            //switch mid-hold must not change the keycode a repeat or the release reports — press
            //and release always agree because both read this record. held state is keyed by
            //scancode, which never changes for a physical key, so it can never be stranded. a
            //KeyDown is still emitted per repeat so held-key auto-repeat (textbox
            //backspace/arrows) works.
            //a keycode already on record means the key was down before this event — i.e. a repeat.
            var isRepeat = PressedKeycodeByScancode.TryGetValue(scancode, out var keycode);

            if (!isRepeat)
            {
                keycode = NormalizeKeycode(Marshal.ReadInt32(sdlEvent, Sdl.KEYBOARDEVENT_SYM_OFFSET));
                PressedKeycodeByScancode[scancode] = keycode;
            }

            PendingEvents.Add(BufferedInputEvent.ForKeyDown(scancode, keycode, mods, isRepeat));
        } else
        {
            //fall back to the live keycode for a key pressed before Initialize or a focus
            //regain, where no press keycode was recorded.
            if (!PressedKeycodeByScancode.Remove(scancode, out var keycode))
                keycode = NormalizeKeycode(Marshal.ReadInt32(sdlEvent, Sdl.KEYBOARDEVENT_SYM_OFFSET));

            PendingEvents.Add(BufferedInputEvent.ForKeyUp(scancode, keycode, mods));
        }
    }

    //folds the numpad digits, minus, and Enter onto their main-keyboard equivalents so a
    //hotkey bound to a digit or Enter fires whether the user hits the number row or the numpad.
    //this is a fixed physical equivalence, not a layout mapping — it does not vary by locale.
    //everything else casts straight through, since Scancode's values are the raw SDL scancodes.
    private static Scancode NormalizeScancode(int rawScancode)
        => rawScancode switch
        {
            >= 89 and <= 97 => Scancode.D1 + (rawScancode - 89), //keypad 1-9 -> D1-D9
            98 => Scancode.D0,                       //keypad 0
            88 => Scancode.Enter,                    //keypad enter
            86 => Scancode.OemMinus,                 //keypad minus
            _ => (Scancode)rawScancode
        };

    //keeps NormalizeScancode's numpad-Enter fold consistent on the keycode side so numpad
    //Enter still submits a focused textbox (which matches on keycode, not scancode). everything
    //else casts straight through, since Keycode's values are the raw SDL keycodes.
    private static Keycode NormalizeKeycode(int rawKeycode)
        //SDLK_KP_ENTER = SDL_SCANCODE_KP_ENTER (88) | the named-key mask
        => rawKeycode == ((1 << 30) | 88) ? Keycode.Enter : (Keycode)rawKeycode;

    private static void HandleTextInputEvent(nint sdlEvent)
    {
        //SDL delivers text as a UTF-8 null-terminated string inline in the event struct.
        //For ASCII input (the common case for Dark Ages) it's one byte per character,
        //but IME composition can deliver multi-character strings in a single event.
        var textPtr = sdlEvent + Sdl.TEXTINPUTEVENT_TEXT_OFFSET;
        var text = Marshal.PtrToStringUTF8(textPtr);

        if (string.IsNullOrEmpty(text))
            return;

        foreach (var ch in text)
            PendingEvents.Add(BufferedInputEvent.ForTextInput(ch));
    }

    private static void HandleMouseButtonEvent(nint sdlEvent, bool isPress)
    {
        var sdlButton = Marshal.ReadByte(sdlEvent, Sdl.MOUSEBUTTONEVENT_BUTTON_OFFSET);

        var mouseButton = sdlButton switch
        {
            Sdl.BUTTON_LEFT => MouseButton.Left,
            Sdl.BUTTON_RIGHT => MouseButton.Right,
            _ => (MouseButton)(-1)
        };

        if ((int)mouseButton < 0)
            return;

        //flip held flags so IsLeftButtonHeld / IsRightButtonHeld report per-window
        //state. SDL only delivers button events for our window, so a click in another
        //application never sets these to true — which was the whole reason the
        //pre-refactor Mouse.GetState() path needed a mouseOutsideClient guard.
        if (mouseButton == MouseButton.Left)
            IsLeftButtonHeld = isPress;
        else if (mouseButton == MouseButton.Right)
            IsRightButtonHeld = isPress;

        //capture click position at the exact moment of the event in raw window pixels,
        //then translate to virtual coordinates via ToVirtual so polled and event
        //positions always agree. using per-event coordinates means that a click which
        //lands while the cursor is in flight (turbo-click during fast movement, or a
        //drag release) reports its true position rather than the frame-end cursor
        //position.
        var rawX = Marshal.ReadInt32(sdlEvent, Sdl.MOUSEBUTTONEVENT_X_OFFSET);
        var rawY = Marshal.ReadInt32(sdlEvent, Sdl.MOUSEBUTTONEVENT_Y_OFFSET);
        var virtualX = ToVirtualX(rawX);
        var virtualY = ToVirtualY(rawY);

        //capture modifier state at the exact moment of the event. SDL maintains its
        //own running modifier state; SDL_GetModState() reads it synchronously from
        //within the watcher callback on the same thread SDL updates it on, so the
        //value reflects what was held when the OS posted this button event.
        var mods = TranslateSdlMods(Sdl.SDL_GetModState());

        PendingEvents.Add(BufferedInputEvent.ForMouseButton(mouseButton, isPress, virtualX, virtualY, mods));
    }

    //promotes each SDL_MOUSEWHEEL event to a first-class BufferedInputEvent so that
    //a macro sequence click→scroll→click→scroll preserves relative ordering against
    //the click events. SDL reports y in notches (±1 per detent; positive = scroll up).
    //horizontal wheel and the precise* float fields (SDL 2.0.18+) are intentionally
    //ignored — consumers only want integer vertical notches.
    private static void HandleMouseWheelEvent(nint sdlEvent)
    {
        var y = Marshal.ReadInt32(sdlEvent, Sdl.MOUSEWHEELEVENT_Y_OFFSET);

        if (y == 0)
            return;

        //wheel events don't carry a click position in SDL 2.0.x — use the live
        //tracked cursor position as the wheel target. this is usually accurate because
        //cursor movement between consecutive OS events within the same pump is rare.
        var mods = TranslateSdlMods(Sdl.SDL_GetModState());
        var virtualX = ToVirtualX(RawMouseX);
        var virtualY = ToVirtualY(RawMouseY);

        PendingEvents.Add(BufferedInputEvent.ForMouseWheel(y, virtualX, virtualY, mods));
    }

    private static KeyModifiers TranslateSdlMods(uint sdlMods)
    {
        var mods = KeyModifiers.None;

        if ((sdlMods & (Sdl.KMOD_LSHIFT | Sdl.KMOD_RSHIFT)) != 0)
            mods |= KeyModifiers.Shift;

        if ((sdlMods & (Sdl.KMOD_LCTRL | Sdl.KMOD_RCTRL)) != 0)
            mods |= KeyModifiers.Ctrl;

        if ((sdlMods & (Sdl.KMOD_LALT | Sdl.KMOD_RALT)) != 0)
            mods |= KeyModifiers.Alt;

        if ((sdlMods & (Sdl.KMOD_LGUI | Sdl.KMOD_RGUI)) != 0)
            mods |= KeyModifiers.Command;

        return mods;
    }


}

public enum BufferedInputKind : byte
{
    KeyDown,
    KeyUp,
    TextInput,
    MouseButton,
    MouseWheel
}

/// <summary>
///     A single captured input event — keyboard (KeyDown/KeyUp/TextInput), mouse button
///     press/release, or mouse wheel notch. <see cref="Kind" /> selects which fields are
///     meaningful; unused fields carry default values. Consumers should walk
///     <see cref="InputBuffer.Events" /> in order and switch on <see cref="Kind" /> to
///     dispatch appropriately.
/// </summary>
public readonly record struct BufferedInputEvent(
    BufferedInputKind Kind,
    Scancode Scancode,
    Keycode Keycode,
    char Character,
    MouseButton Button,
    bool IsPress,
    int X,
    int Y,
    int WheelDelta,
    KeyModifiers Modifiers,
    bool IsRepeat = false)
{
    public static BufferedInputEvent ForKeyDown(
        Scancode scancode,
        Keycode keycode,
        KeyModifiers modifiers,
        bool isRepeat)
        => new(
            BufferedInputKind.KeyDown,
            scancode,
            keycode,
            '\0',
            default,
            false,
            0,
            0,
            0,
            modifiers,
            isRepeat);

    public static BufferedInputEvent ForKeyUp(Scancode scancode, Keycode keycode, KeyModifiers modifiers)
        => new(
            BufferedInputKind.KeyUp,
            scancode,
            keycode,
            '\0',
            default,
            false,
            0,
            0,
            0,
            modifiers);

    public static BufferedInputEvent ForTextInput(char character)
        => new(
            BufferedInputKind.TextInput,
            default,
            default,
            character,
            default,
            false,
            0,
            0,
            0,
            KeyModifiers.None);

    public static BufferedInputEvent ForMouseButton(
        MouseButton button,
        bool isPress,
        int x,
        int y,
        KeyModifiers modifiers)
        => new(
            BufferedInputKind.MouseButton,
            default,
            default,
            '\0',
            button,
            isPress,
            x,
            y,
            0,
            modifiers);

    public static BufferedInputEvent ForMouseWheel(int delta, int x, int y, KeyModifiers modifiers)
        => new(
            BufferedInputKind.MouseWheel,
            default,
            default,
            '\0',
            default,
            false,
            x,
            y,
            delta,
            modifiers);
}