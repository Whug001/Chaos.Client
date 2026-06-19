using Chaos.IO.Memory;
using Chaos.Packets.Abstractions;

namespace Chaos.Client.Networking.Titles;

public sealed class TitleListRequestConverter : PacketConverterBase<TitleListRequestArgs>
{
    public override byte OpCode => TitleOpCodes.TitleListRequest;

    public override TitleListRequestArgs Deserialize(ref SpanReader reader) => new();

    public override void Serialize(ref SpanWriter writer, TitleListRequestArgs args) { }
}
