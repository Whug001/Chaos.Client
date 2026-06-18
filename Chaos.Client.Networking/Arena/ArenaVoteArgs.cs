using Chaos.Packets.Abstractions;

namespace Chaos.Client.Networking.Arena;

public sealed record ArenaVoteArgs : IPacketSerializable
{
    public byte PollId { get; set; }
    public byte OptionIndex { get; set; }
}
