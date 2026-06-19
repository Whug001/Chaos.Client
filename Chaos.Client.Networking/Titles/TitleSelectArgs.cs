using Chaos.Packets.Abstractions;

namespace Chaos.Client.Networking.Titles;

public sealed record TitleSelectArgs : IPacketSerializable
{
    public string Title { get; set; } = string.Empty;
}
