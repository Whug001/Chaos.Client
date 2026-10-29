#if DEBUG
using System.Diagnostics;
using System.IO.Pipes;

namespace Chaos.Client.Systems.TestHook;

public sealed class TestHookServer(string pipeName, Func<TestCommand, Task<string>> execute, TimeSpan timeout) : IDisposable
{
    private readonly CancellationTokenSource Cts = new();

    public void Start() => _ = Task.Run(() => AcceptLoop(Cts.Token));

    private async Task AcceptLoop(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await pipe.WaitForConnectionAsync(ct);
                    await ServeClient(pipe, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (IOException)
                {
                    // Client went away or the pipe could not be created; retry without spinning.
                    await Task.Delay(100, ct);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Test hook pipe failed: {ex}");
                    await Task.Delay(100, ct);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutdown can also cancel the retry delay.
        }
    }

    private async Task ServeClient(NamedPipeServerStream pipe, CancellationToken ct)
    {
        using var reader = new StreamReader(pipe, leaveOpen: true);
        await using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };

        string? line;
        while (pipe.IsConnected && (line = await reader.ReadLineAsync(ct)) is not null)
        {
            string reply;
            if (!TestCommandParser.TryParse(line, out var command, out var error))
                reply = TestHookExecutor.Error(error);
            else
            {
                try
                {
                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    var task = execute(command!);
                    var finished = await Task.WhenAny(task, Task.Delay(timeout, timeoutCts.Token));
                    timeoutCts.Cancel();
                    reply = finished == task ? await task : TestHookExecutor.Error("timeout");
                }
                catch (Exception ex)
                {
                    reply = TestHookExecutor.Error(ex.Message);
                }
            }

            await writer.WriteLineAsync(reply);
        }
    }

    public void Dispose()
    {
        Cts.Cancel();
        Cts.Dispose();
    }
}
#endif
