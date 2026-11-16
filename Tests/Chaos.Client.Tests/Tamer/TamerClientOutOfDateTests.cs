using System.Buffers;
using Chaos.Client.Networking;
using Chaos.Client.Networking.Definitions;
using Chaos.Networking.Abstractions.Definitions;
using FluentAssertions;

namespace Chaos.Client.Tests;

/// <summary>
///     A tamer packet the client cannot decode means the server speaks a newer tamer protocol. The client must say so
///     once per login instead of failing silently, and keep silently skipping every other opcode.
/// </summary>
public sealed class TamerClientOutOfDateTests
{
    private static ServerPacket Malformed(ServerOpCode opcode)
    {
        var rented = ArrayPool<byte>.Shared.Rent(0);

        return new ServerPacket((byte)opcode, 0, rented, 0);
    }

    private static void EnterLogin(ConnectionManager manager)
        => typeof(ConnectionManager).GetProperty("State")!
            .GetSetMethod(nonPublic: true)!
            .Invoke(manager, [ConnectionState.Login]);

    [Test]
    public void The_warning_text_is_the_out_of_date_message()
    {
        ConnectionManager.TamerClientOutOfDateMessage.Should()
            .Be("Your client is out of date for the tamer feature. Please update.");
    }

    [Test]
    public void Each_tamer_opcode_that_fails_to_decode_warns()
    {
        foreach (var opcode in new[]
                 {
                     ServerOpCode.TamerDisplay, ServerOpCode.TamerBattleDisplay, ServerOpCode.SetBindState
                 })
        {
            using var manager = new ConnectionManager();
            var warnings = 0;
            manager.OnTamerClientOutOfDate += () => warnings++;

            manager.ProcessPackets([Malformed(opcode)]);

            warnings.Should().Be(1, $"{opcode} should warn");
        }
    }

    [Test]
    public void Repeated_failures_warn_only_once_per_login()
    {
        using var manager = new ConnectionManager();
        var warnings = 0;
        manager.OnTamerClientOutOfDate += () => warnings++;

        manager.ProcessPackets([Malformed(ServerOpCode.TamerDisplay)]);
        manager.ProcessPackets([Malformed(ServerOpCode.TamerBattleDisplay)]);
        manager.ProcessPackets([Malformed(ServerOpCode.SetBindState)]);

        warnings.Should().Be(1);
    }

    [Test]
    public void Other_opcodes_that_fail_to_decode_stay_silent()
    {
        using var manager = new ConnectionManager();
        var warnings = 0;
        manager.OnTamerClientOutOfDate += () => warnings++;

        manager.ProcessPackets([Malformed(ServerOpCode.Attributes)]);
        manager.ProcessPackets([Malformed(ServerOpCode.Location)]);

        warnings.Should().Be(0);
    }

    [Test]
    public void A_new_login_warns_again()
    {
        using var manager = new ConnectionManager();
        var warnings = 0;
        manager.OnTamerClientOutOfDate += () => warnings++;

        manager.ProcessPackets([Malformed(ServerOpCode.TamerDisplay)]);
        EnterLogin(manager);
        manager.ProcessPackets([Malformed(ServerOpCode.TamerDisplay)]);

        warnings.Should().Be(2);
    }
}
