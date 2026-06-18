using Chaos.IO.Memory;
using Chaos.Packets.Abstractions;

namespace Chaos.Client.Networking.Arena;

public sealed class ArenaVoteConverter : PacketConverterBase<ArenaVoteArgs>
{
    public override byte OpCode => ArenaOpCodes.ArenaVote;

    public override ArenaVoteArgs Deserialize(ref SpanReader reader)
        => new()
        {
            PollId = reader.ReadByte(),
            OptionIndex = reader.ReadByte()
        };

    public override void Serialize(ref SpanWriter writer, ArenaVoteArgs args)
    {
        writer.WriteByte(args.PollId);
        writer.WriteByte(args.OptionIndex);
    }
}
