#region
using Chaos.Client.Controls.Components;
#endregion

namespace Chaos.Client.Definitions;

[Flags]
public enum KeyModifiers
{
    None = 0,
    Shift = 1,
    Ctrl = 2,
    Alt = 4,

    //the GUI/meta key — Command (⌘) on macOS, the Windows key on Windows. tracked so
    //text-editing shortcuts can follow the Cmd convention on macOS; see
    //<see cref="ModifierExtensions.HasAccelerator" />.
    Command = 8
}

public static class ModifierExtensions
{
    //resolved once — the host OS doesn't change mid-process.
    private static readonly bool IsMacOs = OperatingSystem.IsMacOS();

    //the platform's primary text-editing shortcut modifier: Command (⌘) on macOS, Ctrl on
    //Windows/Linux. used for copy/paste/select-all.
    public static readonly KeyModifiers AcceleratorModifier = IsMacOs ? KeyModifiers.Command : KeyModifiers.Ctrl;

    //the modifier that moves/deletes a whole word at a time: Option (Alt) on macOS, Ctrl on
    //Windows/Linux.
    public static readonly KeyModifiers WordModifier = IsMacOs ? KeyModifiers.Alt : KeyModifiers.Ctrl;

    //true when the accelerator modifier is held for a shortcut — Cmd+C on macOS, Ctrl+C on
    //Windows/Linux. Alt is excluded because AltGr (Ctrl+Alt on Windows) and Option (macOS) are
    //typing modifiers on AZERTY/QWERTZ, so an AltGr/Option-typed character must not read as a
    //clipboard/select-all shortcut.
    public static bool HasAccelerator(this KeyModifiers mods)
        => ((mods & AcceleratorModifier) != 0) && ((mods & KeyModifiers.Alt) == 0);

    //true when the word-jump modifier is held — Option+arrows on macOS, Ctrl+arrows on
    //Windows/Linux — for word-at-a-time cursor movement and deletion.
    public static bool HasWordJump(this KeyModifiers mods) => (mods & WordModifier) != 0;

    //true for a jump to the line/document edge. macOS uses Cmd+arrows for this; Windows/Linux
    //use the Home/End keys directly, so there is no arrow-key line-jump there (Ctrl already
    //means word-jump).
    public static bool HasLineJump(this KeyModifiers mods) => IsMacOs && ((mods & KeyModifiers.Command) != 0);
}

public enum MouseButton
{
    Left,
    Right
}

public abstract class InputEvent
{
    public bool Handled { get; set; }
    public UIElement? Target { get; internal set; }

    /// <summary>
    ///     Resets dispatch state for pooled reuse. Subclasses should reset their own fields
    ///     and call base.Reset().
    /// </summary>
    public virtual void Reset()
    {
        Handled = false;
        Target = null;
    }
}

//── mouse events ──

public abstract class MouseEvent : InputEvent
{
    public int ScreenX { get; set; }
    public int ScreenY { get; set; }
    public MouseButton Button { get; set; }
    public KeyModifiers Modifiers { get; set; }

    public bool Shift => (Modifiers & KeyModifiers.Shift) != 0;
    public bool Ctrl => (Modifiers & KeyModifiers.Ctrl) != 0;
    public bool Alt => (Modifiers & KeyModifiers.Alt) != 0;
}

public sealed class MouseDownEvent : MouseEvent;

public sealed class MouseUpEvent : MouseEvent;

public sealed class ClickEvent : MouseEvent;

public sealed class DoubleClickEvent : MouseEvent;

public sealed class MouseMoveEvent : MouseEvent
{
    public int DeltaX { get; set; }
    public int DeltaY { get; set; }
}

public sealed class MouseScrollEvent : MouseEvent
{
    public int Delta { get; set; }
}

//── key events ──

public abstract class KeyEvent : InputEvent
{
    //physical key position — layout-independent. use for positional hotkeys.
    public Scancode Scancode { get; set; }

    //layout-mapped label — the symbol the key produces. use for text editing and
    //label-following shortcuts (copy/paste/select-all).
    public Keycode Keycode { get; set; }

    public KeyModifiers Modifiers { get; set; }

    public bool Shift => (Modifiers & KeyModifiers.Shift) != 0;
    public bool Ctrl => (Modifiers & KeyModifiers.Ctrl) != 0;
    public bool Alt => (Modifiers & KeyModifiers.Alt) != 0;

    //the platform's primary text-editing shortcut modifier — Command on macOS, Ctrl elsewhere —
    //held for a shortcut (Alt excluded; see ModifierExtensions.HasAccelerator). use for
    //copy/paste/select-all so they follow each platform's convention.
    public bool Accelerator => Modifiers.HasAccelerator();

    //word-at-a-time cursor movement/deletion — Option+arrows on macOS, Ctrl+arrows elsewhere.
    public bool WordJump => Modifiers.HasWordJump();

    //jump to the line/document edge — Cmd+arrows on macOS (Windows/Linux use the Home/End keys).
    public bool LineJump => Modifiers.HasLineJump();

    //true for the OS auto-repeats of a held key, false for the initial press. text editing wants
    //the repeats; a one-shot gesture must ignore them or a held key runs straight through it.
    public bool IsRepeat { get; set; }
}

public sealed class KeyDownEvent : KeyEvent;

public sealed class KeyUpEvent : KeyEvent;

public sealed class TextInputEvent : InputEvent
{
    public char Character { get; set; }
}

//── drag events ──

public abstract class DragEvent : InputEvent
{
    public int ScreenX { get; set; }
    public int ScreenY { get; set; }
    public MouseButton Button { get; set; }
}

public sealed class DragStartEvent : DragEvent
{
    public UIElement? Source { get; set; }
    public object? Payload { get; set; }

    public override void Reset()
    {
        base.Reset();
        Source = null;
        Payload = null;
    }
}

public sealed class DragMoveEvent : DragEvent
{
    public object? Payload { get; set; }

    public override void Reset()
    {
        base.Reset();
        Payload = null;
    }
}

public sealed class DragDropEvent : DragEvent
{
    public object? Payload { get; set; }
    public UIElement? DropTarget { get; set; }

    public override void Reset()
    {
        base.Reset();
        Payload = null;
        DropTarget = null;
    }
}
