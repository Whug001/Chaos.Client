using System.Buffers;
using System.Text;
using Chaos.Client.Networking;
using Chaos.Client.Networking.Definitions;
using Chaos.DarkAges.Definitions;
using Chaos.Extensions.Common;
using Chaos.Networking.Abstractions.Definitions;
using Chaos.Networking.Entities.Server;
using Chaos.Packets;
using Chaos.Packets.Abstractions;
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

    private static ServerPacket ValidSetBindState()
    {
        //same converter discovery as GameClient: mentioning SetBindStateArgs loads the converters assembly
        //before the scan runs, so the throw below serializes with the real server converter
        var converters = new Dictionary<Type, IPacketConverter>();
        var instances = typeof(IPacketConverter<>).LoadImplementations()
            .Select(type => (IPacketConverter)Activator.CreateInstance(type)!);

        foreach (var instance in instances)
        {
            var argsType = instance.GetType()
                .GetInterfaces()
                .Where(i => i.IsGenericType)
                .First(i => i.GetGenericTypeDefinition() == typeof(IPacketConverter<>))
                .GetGenericArguments()
                .First();

            converters.TryAdd(argsType, instance);
        }

        var serializer = new PacketSerializer(Encoding.GetEncoding(949), converters);
        var packet = serializer.Serialize(
            new SetBindStateArgs
            {
                Mode = BindMeterMode.Throw,
                Grade = "Clay",
                MonsterName = "Goblin",
                Chance = 48,
                Shakes = 1,
                Outcome = BindOutcome.Pending
            });

        var rented = ArrayPool<byte>.Shared.Rent(packet.Length);
        packet.Buffer[..packet.Length].CopyTo(rented);

        var length = packet.Length;
        packet.MemoryOwner?.Dispose();

        return new ServerPacket((byte)ServerOpCode.SetBindState, 0, rented, length);
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

    [Test]
    public void A_throwing_subscriber_after_a_successful_decode_does_not_warn()
    {
        using var manager = new ConnectionManager();
        var warnings = 0;
        manager.OnTamerClientOutOfDate += () => warnings++;
        manager.OnSetBindState += _ => throw new InvalidOperationException("UI boom");

        manager.ProcessPackets([ValidSetBindState()]);

        warnings.Should().Be(0);
    }

    [Test]
    public void A_valid_packet_still_reaches_its_subscribers()
    {
        //guards the test above: a silent pass because the packet never decoded would prove nothing
        using var manager = new ConnectionManager();
        SetBindStateArgs? received = null;
        manager.OnSetBindState += args => received = args;

        manager.ProcessPackets([ValidSetBindState()]);

        received.Should().NotBeNull();
        received!.Mode.Should().Be(BindMeterMode.Throw);
        received.Grade.Should().Be("Clay");
        received.MonsterName.Should().Be("Goblin");
    }

    [Test]
    public void No_subscriber_does_not_consume_the_warning()
    {
        using var manager = new ConnectionManager();
        manager.ProcessPackets([Malformed(ServerOpCode.TamerDisplay)]);

        var warnings = 0;
        manager.OnTamerClientOutOfDate += () => warnings++;
        manager.ProcessPackets([Malformed(ServerOpCode.TamerDisplay)]);

        warnings.Should().Be(1);
    }

    [Test]
    public void A_throwing_warning_subscriber_does_not_break_the_batch()
    {
        using var manager = new ConnectionManager();
        manager.OnTamerClientOutOfDate += () => throw new InvalidOperationException("orange bar boom");
        SetBindStateArgs? received = null;
        manager.OnSetBindState += args => received = args;

        manager.ProcessPackets([Malformed(ServerOpCode.TamerDisplay), ValidSetBindState()]);

        received.Should().NotBeNull();
    }
}
