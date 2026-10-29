#if DEBUG
using System.Collections.Concurrent;
using System.Text.Json;
using Chaos.Client;
using Chaos.Client.Definitions;

namespace Chaos.Client.Systems.TestHook;

public sealed class TestHookExecutor(ITestHookHost host)
{
    private const int DRAG_STEPS = 5;
    private const int SCREENSHOT_TIMEOUT_FRAMES = 600;

    private readonly ConcurrentQueue<(TestCommand Command, TaskCompletionSource<string> Reply)> Queue = new();
    private IEnumerator<bool>? Current;
    private TaskCompletionSource<string>? CurrentReply;

    public Task<string> Enqueue(TestCommand command)
    {
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        Queue.Enqueue((command, tcs));
        return tcs.Task;
    }

    // Called once per game frame on the game thread.
    public void Tick()
    {
        if (Current is null)
        {
            if (!Queue.TryDequeue(out var next))
                return;

            CurrentReply = next.Reply;
            Current = Run(next.Command, next.Reply).GetEnumerator();
        }

        bool finished;
        try
        {
            finished = !Current.MoveNext() || Current.Current;
        }
        catch (Exception ex)
        {
            CurrentReply!.TrySetResult(Error(ex.Message));
            finished = true;
        }

        if (!finished)
            return;

        CurrentReply!.TrySetResult(Error("command ended without a reply"));
        Current = null;
        CurrentReply = null;
    }

    // Each yield is one frame; yielding true means the command is finished.
    private IEnumerable<bool> Run(TestCommand command, TaskCompletionSource<string> reply)
    {
        switch (command)
        {
            case PingCommand:
                reply.TrySetResult(Ok("pong"));
                yield return true;
                break;

            case KeyCommand key:
                if (!host.TryResolveKey(key.Key, out var down, out var up))
                {
                    reply.TrySetResult(Error($"unknown key '{key.Key}'"));
                    yield return true;
                    break;
                }

                if (key.Action != KeyAction.Up)
                    host.Inject(down);

                if (key.Action == KeyAction.Tap)
                {
                    for (var i = 0; i < key.Frames; i++)
                        yield return false;
                    host.Inject(up);
                }
                else if (key.Action == KeyAction.Up)
                    host.Inject(up);

                reply.TrySetResult(Ok(null));
                yield return true;
                break;

            case ClickCommand click:
                host.PinMouse(click.X, click.Y);
                host.Inject(MouseEvent(click.Right, true, click.X, click.Y));
                yield return false;
                host.Inject(MouseEvent(click.Right, false, click.X, click.Y));
                reply.TrySetResult(Ok(null));
                yield return true;
                break;

            case MoveCommand move:
                host.PinMouse(move.X, move.Y);
                reply.TrySetResult(Ok(null));
                yield return true;
                break;

            case DragCommand drag:
                host.PinMouse(drag.X1, drag.Y1);
                host.Inject(MouseEvent(false, true, drag.X1, drag.Y1));
                for (var step = 1; step <= DRAG_STEPS; step++)
                {
                    yield return false;
                    host.PinMouse(drag.X1 + (drag.X2 - drag.X1) * step / DRAG_STEPS,
                        drag.Y1 + (drag.Y2 - drag.Y1) * step / DRAG_STEPS);
                }
                yield return false;
                host.Inject(MouseEvent(false, false, drag.X2, drag.Y2));
                reply.TrySetResult(Ok(null));
                yield return true;
                break;

            case TypeCommand type:
                foreach (var ch in type.Text)
                    host.Inject(BufferedInputEvent.ForTextInput(ch));
                reply.TrySetResult(Ok(null));
                yield return true;
                break;

            case WaitFramesCommand wait:
                for (var i = 1; i < wait.Frames; i++)
                    yield return false;
                reply.TrySetResult(Ok(null));
                yield return true;
                break;

            case ScreenshotCommand shot:
                string? shotError = null;
                var done = false;
                var timedOut = false;
                host.RequestScreenshot(shot.Path, err => { if (timedOut) return; shotError = err; done = true; });
                var waited = 0;
                while (!done)
                {
                    if (++waited >= SCREENSHOT_TIMEOUT_FRAMES)
                    {
                        timedOut = true;
                        reply.TrySetResult(Error("screenshot timed out"));
                        yield return true;
                        break;
                    }
                    yield return false;
                }
                if (done)
                {
                    reply.TrySetResult(shotError is null ? Ok(shot.Path) : Error(shotError));
                    yield return true;
                }
                break;

            case StateCommand stateCommand:
                reply.TrySetResult(Ok(FilterState(host, stateCommand.Filter)));
                yield return true;
                break;

            case PanelsCommand:
                var root = host.GetWorldRoot();
                reply.TrySetResult(Ok(root is null
                    ? Array.Empty<PanelReport>()
                    : PanelInspector.Inspect(root, host.MeasureText)));
                yield return true;
                break;
        }
    }

    // kart.track (large and static per race) is returned only when the filter is exactly "kart.track";
    // Every other filter (including null/empty and "kart.") excludes it; otherwise only keys starting with the filter are returned.
    private static Dictionary<string, object?> FilterState(ITestHookHost host, string? filter)
    {
        var snapshot = TestStateRegistry.Snapshot();
        var state = new Dictionary<string, object?>();

        if (filter == "kart.track")
        {
            foreach (var (k, v) in host.GetBaseState())
                if (k == filter)
                    state[k] = v;
            foreach (var (k, v) in snapshot)
                if (k == filter)
                    state[k] = v;
        }
        else if (string.IsNullOrEmpty(filter))
        {
            foreach (var (k, v) in host.GetBaseState())
                state[k] = v;
            foreach (var (k, v) in snapshot)
                if (k != "kart.track")
                    state[k] = v;
        }
        else
        {
            foreach (var (k, v) in host.GetBaseState())
                if (k != "kart.track" && k.StartsWith(filter, StringComparison.Ordinal))
                    state[k] = v;
            foreach (var (k, v) in snapshot)
                if (k != "kart.track" && k.StartsWith(filter, StringComparison.Ordinal))
                    state[k] = v;
        }

        return state;
    }

    private static BufferedInputEvent MouseEvent(bool right, bool press, int x, int y)
        => new(BufferedInputKind.MouseButton, default, default, '\0',
            right ? MouseButton.Right : MouseButton.Left, press, x, y, 0, KeyModifiers.None);

    public static string Ok(object? data) => JsonSerializer.Serialize(new { ok = true, data });
    public static string Error(string message) => JsonSerializer.Serialize(new { ok = false, error = message });
}
#endif
