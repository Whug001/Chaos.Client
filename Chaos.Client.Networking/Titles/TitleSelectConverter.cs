using Chaos.IO.Memory;
using Chaos.Packets.Abstractions;

namespace Chaos.Client.Networking.Titles;

public sealed class TitleSelectConverter : PacketConverterBase<TitleSelectArgs>
{
    public override byte OpCode => TitleOpCodes.TitleSelect;

    public override TitleSelectArgs Deserialize(ref SpanReader reader)
        => new()
        {
            Title = reader.ReadString8()
        };

    public override void Serialize(ref SpanWriter writer, TitleSelectArgs args) => writer.WriteString8(args.Title);
}
