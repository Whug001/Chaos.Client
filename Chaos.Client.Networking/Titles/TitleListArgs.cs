using Chaos.Packets.Abstractions;

namespace Chaos.Client.Networking.Titles;

public sealed record TitleListArgs : IPacketSerializable
{
    public string ActiveTitle { get; set; } = string.Empty;
    public List<string> Titles { get; set; } = [];
}
