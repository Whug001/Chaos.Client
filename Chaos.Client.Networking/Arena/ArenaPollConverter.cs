using Chaos.IO.Memory;
using Chaos.Packets.Abstractions;

namespace Chaos.Client.Networking.Arena;

public sealed class ArenaPollConverter : PacketConverterBase<ArenaPollArgs>
{
    public override byte OpCode => ArenaOpCodes.ArenaPoll;

    public override ArenaPollArgs Deserialize(ref SpanReader reader)
    {
        var pollId = reader.ReadByte();
        var state = (ArenaPollState)reader.ReadByte();
        var seconds = reader.ReadUInt16();
        var winning = reader.ReadByte();
        var count = reader.ReadByte();

        var options = new List<ArenaPollOption>(count);
        for (var i = 0; i < count; i++)
            options.Add(
                new ArenaPollOption
                {
                    MatchType = reader.ReadByte(),
                    Name = reader.ReadString16(),
                    Votes = reader.ReadUInt16()
                });

        return new ArenaPollArgs
        {
            PollId = pollId,
            State = state,
            SecondsRemaining = seconds,
            WinningIndex = winning,
            Options = options
        };
    }

    public override void Serialize(ref SpanWriter writer, ArenaPollArgs args)
    {
        writer.WriteByte(args.PollId);
        writer.WriteByte((byte)args.State);
        writer.WriteUInt16(args.SecondsRemaining);
        writer.WriteByte(args.WinningIndex);
        writer.WriteByte((byte)args.Options.Count);

        foreach (var option in args.Options)
        {
            writer.WriteByte(option.MatchType);
            writer.WriteString16(option.Name);
            writer.WriteUInt16(option.Votes);
        }
    }
}
