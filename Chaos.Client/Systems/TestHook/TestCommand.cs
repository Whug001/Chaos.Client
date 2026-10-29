#if DEBUG
namespace Chaos.Client.Systems.TestHook;

public enum KeyAction { Tap, Down, Up }

public abstract record TestCommand;
public sealed record PingCommand : TestCommand;
public sealed record KeyCommand(string Key, KeyAction Action, int Frames) : TestCommand;
public sealed record ClickCommand(int X, int Y, bool Right) : TestCommand;
public sealed record MoveCommand(int X, int Y) : TestCommand;
public sealed record DragCommand(int X1, int Y1, int X2, int Y2) : TestCommand;
public sealed record TypeCommand(string Text) : TestCommand;
public sealed record ScreenshotCommand(string Path) : TestCommand;
public sealed record PanelsCommand : TestCommand;
public sealed record StateCommand(string? Filter) : TestCommand;
public sealed record WaitFramesCommand(int Frames) : TestCommand;

public static class TestCommandParser
{
    private const int VirtualWidth = 640;
    private const int VirtualHeight = 480;

    public static bool TryParse(string line, out TestCommand? command, out string error)
    {
        command = null;
        error = string.Empty;
        var trimmed = line.Trim();
        var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
            return Fail("empty command", out error);

        var name = parts[0].ToLowerInvariant();
        var args = parts.AsSpan(1);

        switch (name)
        {
            case "ping": command = new PingCommand(); return true;
            case "panels": command = new PanelsCommand(); return true;
            case "state":
            {
                if (args.Length > 1)
                    return Fail("usage: state [key-prefix]", out error);

                command = new StateCommand(args.Length == 1 ? args[0] : null);
                return true;
            }

            case "key":
            {
                if (args.Length is < 1 or > 3)
                    return Fail("usage: key <name> [tap|down|up] [frames]", out error);

                var action = KeyAction.Tap;
                if (args.Length >= 2 && !Enum.TryParse(args[1], true, out action))
                    return Fail($"unknown key action '{args[1]}'", out error);

                var frames = 1;
                if (args.Length == 3 && (!int.TryParse(args[2], out frames) || frames < 1))
                    return Fail("frames must be a positive number", out error);

                command = new KeyCommand(args[0], action, frames);
                return true;
            }

            case "click":
            {
                if (args.Length is < 2 or > 3 || !TryPoint(args[0], args[1], out var x, out var y, out error))
                    return error.Length > 0 ? false : Fail("usage: click <x> <y> [left|right]", out error);

                var right = args.Length == 3 && args[2].Equals("right", StringComparison.OrdinalIgnoreCase);
                if (args.Length == 3 && !right && !args[2].Equals("left", StringComparison.OrdinalIgnoreCase))
                    return Fail($"unknown button '{args[2]}'", out error);

                command = new ClickCommand(x, y, right);
                return true;
            }

            case "move":
            {
                if (args.Length != 2 || !TryPoint(args[0], args[1], out var x, out var y, out error))
                    return error.Length > 0 ? false : Fail("usage: move <x> <y>", out error);

                command = new MoveCommand(x, y);
                return true;
            }

            case "drag":
            {
                if (args.Length != 4)
                    return Fail("usage: drag <x1> <y1> <x2> <y2>", out error);
                if (!TryPoint(args[0], args[1], out var x1, out var y1, out error)
                    || !TryPoint(args[2], args[3], out var x2, out var y2, out error))
                    return false;

                command = new DragCommand(x1, y1, x2, y2);
                return true;
            }

            case "type":
            {
                var text = trimmed.Length > 4 ? trimmed[4..].TrimStart() : string.Empty;
                if (text.Length == 0)
                    return Fail("usage: type <text>", out error);

                command = new TypeCommand(text);
                return true;
            }

            case "screenshot":
            {
                var path = trimmed.Length > 10 ? trimmed[10..].Trim() : string.Empty;
                if (path.Length == 0 || !Path.IsPathFullyQualified(path))
                    return Fail("screenshot needs an absolute path", out error);

                command = new ScreenshotCommand(path);
                return true;
            }

            case "wait-frames":
            {
                if (args.Length != 1 || !int.TryParse(args[0], out var frames) || frames < 1)
                    return Fail("usage: wait-frames <positive count>", out error);

                command = new WaitFramesCommand(frames);
                return true;
            }

            default:
                return Fail($"unknown command '{parts[0]}'", out error);
        }
    }

    private static bool TryPoint(string xs, string ys, out int x, out int y, out string error)
    {
        error = string.Empty;
        y = 0;
        if (!int.TryParse(xs, out x) || !int.TryParse(ys, out y))
            return Fail("coordinates must be numbers", out error);
        if (x is < 0 or > VirtualWidth - 1 || y is < 0 or > VirtualHeight - 1)
            return Fail($"point {x},{y} is outside 640x480", out error);

        return true;
    }

    private static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }
}
#endif
