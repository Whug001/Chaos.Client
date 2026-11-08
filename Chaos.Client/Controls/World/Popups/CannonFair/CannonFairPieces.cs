using Chaos.CannonFair;
using Chaos.Networking.Entities.Server;

namespace Chaos.Client.Controls.World.Popups.CannonFair;

public static class CannonFairPieces
{
    public static List<CannonFairPieceInfo> From(FortState state)
        => [.. state.Pieces.Select(p => new CannonFairPieceInfo(
            (byte)p.Kind,
            (byte)p.Material,
            (short)Math.Round(p.X * RecordingCodec.POSITION_SCALE),
            (short)Math.Round(p.Y * RecordingCodec.POSITION_SCALE),
            RecordingCodec.EncodeRotation(p.Rotation),
            (ushort)Math.Clamp(Math.Round(p.Width * RecordingCodec.SIZE_SCALE), 0, ushort.MaxValue),
            (ushort)Math.Clamp(Math.Round(p.Height * RecordingCodec.SIZE_SCALE), 0, ushort.MaxValue)))];
}
