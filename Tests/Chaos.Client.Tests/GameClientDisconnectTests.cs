using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using Chaos.Client.Networking;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class GameClientDisconnectTests
{
    static GameClientDisconnectTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    [Test]
    public async Task A_server_side_close_raises_OnDisconnected_once_and_Disconnect_frees_the_connection()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        using var client = new GameClient();
        var disconnects = 0;
        var disconnected = new TaskCompletionSource();

        client.OnDisconnected += () =>
        {
            Interlocked.Increment(ref disconnects);
            disconnected.TrySetResult();
        };

        var accept = listener.AcceptSocketAsync();
        await client.ConnectAsync("127.0.0.1", port);
        using (var server = await accept)
            server.Close();

        await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(10));

        client.Disconnect();
        client.Disconnect();

        disconnects.Should().Be(1);
        client.Connected.Should().BeFalse();
        PrivateField(client, "Socket").Should().BeNull();
        PrivateField(client, "ReceiveCts").Should().BeNull();
        PrivateField(client, "ReceiveMemoryOwner").Should().BeNull();
    }

    [Test]
    public async Task The_client_reconnects_after_a_server_side_close()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        using var client = new GameClient();
        var disconnected = new TaskCompletionSource();
        client.OnDisconnected += () => disconnected.TrySetResult();

        var accept = listener.AcceptSocketAsync();
        await client.ConnectAsync("127.0.0.1", port);
        using (var server = await accept)
            server.Close();

        await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var secondAccept = listener.AcceptSocketAsync();
        await client.ConnectAsync("127.0.0.1", port);
        using var secondServer = await secondAccept;

        client.Connected.Should().BeTrue();
    }

    [Test]
    public async Task A_failed_connect_leaves_the_client_able_to_connect()
    {
        var closedPort = FreePort();
        using var client = new GameClient();

        var failing = () => client.ConnectAsync("127.0.0.1", closedPort);

        await failing.Should().ThrowAsync<SocketException>();
        client.Connected.Should().BeFalse();

        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var accept = listener.AcceptSocketAsync();
        await client.ConnectAsync("127.0.0.1", port);
        using var server = await accept;

        client.Connected.Should().BeTrue();
    }

    private static object? PrivateField(GameClient client, string name)
        => typeof(GameClient).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client);

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        return port;
    }
}
