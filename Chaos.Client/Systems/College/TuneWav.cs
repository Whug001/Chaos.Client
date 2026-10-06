using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace Chaos.Client.Systems.College;

/// <summary>Wraps rendered tune samples in a WAV header, so the mixer loads them like any sound effect.</summary>
public static class TuneWav
{
    private const int HEADER_BYTES = 44;
    private const short CHANNELS = 2;
    private const short BITS = 16;

    public static byte[] Wrap(short[] stereoSamples)
    {
        var dataBytes = stereoSamples.Length * 2;
        var bytes = new byte[HEADER_BYTES + dataBytes];
        var span = bytes.AsSpan();

        "RIFF"u8.CopyTo(span);
        BinaryPrimitives.WriteInt32LittleEndian(span[4..], 36 + dataBytes);
        "WAVE"u8.CopyTo(span[8..]);
        "fmt "u8.CopyTo(span[12..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[16..], 16);
        BinaryPrimitives.WriteInt16LittleEndian(span[20..], 1);
        BinaryPrimitives.WriteInt16LittleEndian(span[22..], CHANNELS);
        BinaryPrimitives.WriteInt32LittleEndian(span[24..], TuneSynth.SAMPLE_RATE);
        BinaryPrimitives.WriteInt32LittleEndian(span[28..], TuneSynth.SAMPLE_RATE * CHANNELS * (BITS / 8));
        BinaryPrimitives.WriteInt16LittleEndian(span[32..], CHANNELS * (BITS / 8));
        BinaryPrimitives.WriteInt16LittleEndian(span[34..], BITS);
        "data"u8.CopyTo(span[36..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[40..], dataBytes);

        if (BitConverter.IsLittleEndian)
            MemoryMarshal.AsBytes(stereoSamples.AsSpan()).CopyTo(span[HEADER_BYTES..]);
        else
            for (var i = 0; i < stereoSamples.Length; i++)
                BinaryPrimitives.WriteInt16LittleEndian(span[(HEADER_BYTES + (i * 2))..], stereoSamples[i]);

        return bytes;
    }
}
