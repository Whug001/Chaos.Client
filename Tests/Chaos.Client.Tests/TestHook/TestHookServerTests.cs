#if DEBUG
using System.IO.Pipes;
using Chaos.Client.Systems.TestHook;
using FluentAssertions;

namespace Chaos.Client.Tests.TestHook;

[NotInParallel]
public class TestHookServerTests
{
    private static async Task<string?> Send(string pipe, string line)
    {
        await using var client = new NamedPipeClientStream(".", pipe, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(15000);
        var writer = new StreamWriter(client) { AutoFlush = true };
        var reader = new StreamReader(client);
        await writer.WriteLineAsync(line);
        return await reader.ReadLineAsync();
    }

    [Test]
    public async Task ParseError_IsRepliedWithoutExecutor()
    {
        var pipe = $"chaos-pt-test-{Guid.NewGuid():N}";
        using var server = new TestHookServer(pipe, _ => Task.FromResult("unused"), TimeSpan.FromSeconds(10));
        server.Start();

        (await Send(pipe, "fly")).Should().Contain("\"ok\":false");
    }

    [Test]
    public async Task SlowCommand_TimesOut()
    {
        var pipe = $"chaos-pt-test-{Guid.NewGuid():N}";
        using var server = new TestHookServer(pipe, _ => new TaskCompletionSource<string>().Task, TimeSpan.FromMilliseconds(200));
        server.Start();

        (await Send(pipe, "ping")).Should().Be("{\"ok\":false,\"error\":\"timeout\"}");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ExecutorThrows_RepliesOnce_AndNextCommandStillServed(bool faultTask)
    {
        var pipe = $"chaos-pt-test-{Guid.NewGuid():N}";
        using var server = new TestHookServer(pipe,
            command => command is PingCommand
                ? Task.FromResult("{\"ok\":true,\"data\":\"pong\"}")
                : faultTask ? Task.FromException<string>(new InvalidOperationException("executor failed"))
                    : throw new InvalidOperationException("executor failed"),
            TimeSpan.FromSeconds(10));
        server.Start();

        await using var client = new NamedPipeClientStream(".", pipe, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(15000);
        using var writer = new StreamWriter(client, leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(client, leaveOpen: true);
        await writer.WriteLineAsync("state");
        (await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)))
            .Should().Be("{\"ok\":false,\"error\":\"executor failed\"}");
        await writer.WriteLineAsync("ping");
        (await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)))
            .Should().Be("{\"ok\":true,\"data\":\"pong\"}");
    }

    [Test]
    public async Task TwoLines_OneConnection_RepliesInOrder()
    {
        var pipe = $"chaos-pt-test-{Guid.NewGuid():N}";
        using var server = new TestHookServer(pipe,
            command => Task.FromResult(command is PingCommand
                ? "{\"ok\":true,\"data\":\"pong\"}"
                : "{\"ok\":true,\"data\":\"state\"}"),
            TimeSpan.FromSeconds(10));
        server.Start();

        await using var client = new NamedPipeClientStream(".", pipe, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(15000);
        using var writer = new StreamWriter(client, leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(client, leaveOpen: true);
        // Drain replies while sending: unbuffered Windows pipes can block both peers on writes.
        var send = writer.WriteLineAsync("ping\nstate");
        (await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)))
            .Should().Be("{\"ok\":true,\"data\":\"pong\"}");
        (await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)))
            .Should().Be("{\"ok\":true,\"data\":\"state\"}");
        await send.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task CleanDisconnect_NextClientStillServed()
    {
        var pipe = $"chaos-pt-test-{Guid.NewGuid():N}";
        using var server = new TestHookServer(pipe,
            _ => Task.FromResult("{\"ok\":true,\"data\":\"pong\"}"), TimeSpan.FromSeconds(10));
        server.Start();

        (await Send(pipe, "ping")).Should().Be("{\"ok\":true,\"data\":\"pong\"}");
        (await Send(pipe, "ping")).Should().Be("{\"ok\":true,\"data\":\"pong\"}");
    }

    [Test]
    public async Task Disconnect_MidCommand_NextClientStillServed()
    {
        var pipe = $"chaos-pt-test-{Guid.NewGuid():N}";
        var calls = 0;
        using var server = new TestHookServer(pipe,
            _ => Interlocked.Increment(ref calls) == 1
                ? Task.Delay(300).ContinueWith(_ => "{\"ok\":true,\"data\":null}")
                : Task.FromResult("{\"ok\":true,\"data\":\"pong\"}"),
            TimeSpan.FromSeconds(10));
        server.Start();

        await using (var first = new NamedPipeClientStream(".", pipe, PipeDirection.InOut, PipeOptions.Asynchronous))
        {
            await first.ConnectAsync(15000);
            await new StreamWriter(first) { AutoFlush = true }.WriteLineAsync("ping");
        }

        (await Send(pipe, "ping")).Should().Contain("pong");
    }
}
#endif
