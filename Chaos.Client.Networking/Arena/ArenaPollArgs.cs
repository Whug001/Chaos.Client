using Chaos.Packets.Abstractions;

namespace Chaos.Client.Networking.Arena;

public enum ArenaPollState : byte
{
    Open = 0,
    Closed = 1
}

public sealed record ArenaPollOption
{
    public byte MatchType { get; set; }
    public string Name { get; set; } = string.Empty;
    public ushort Votes { get; set; }
}

public sealed record ArenaPollArgs : IPacketSerializable
{
    public byte PollId { get; set; }
    public ArenaPollState State { get; set; }
    public ushort SecondsRemaining { get; set; }

    /// <summary>Winning option index when <see cref="State"/> is Closed; 0xFF otherwise.</summary>
    public byte WinningIndex { get; set; } = 0xFF;

    public List<ArenaPollOption> Options { get; set; } = [];
}
