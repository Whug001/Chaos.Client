using System.Buffers.Binary;
using System.Text;
using Chaos.Client.Systems.College;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class TuneWavTests
{
    [Test]
    public void The_header_describes_16_bit_stereo_at_48_kHz()
    {
        var wav = TuneWav.Wrap([1, -1, 300, -300]);
        var span = wav.AsSpan();

        wav.Should().HaveCount(44 + 8);
        Encoding.ASCII.GetString(wav, 0, 4).Should().Be("RIFF");
        BinaryPrimitives.ReadInt32LittleEndian(span[4..]).Should().Be(36 + 8);
        Encoding.ASCII.GetString(wav, 8, 8).Should().Be("WAVEfmt ");
        BinaryPrimitives.ReadInt32LittleEndian(span[16..]).Should().Be(16);
        BinaryPrimitives.ReadInt16LittleEndian(span[20..]).Should().Be(1);
        BinaryPrimitives.ReadInt16LittleEndian(span[22..]).Should().Be(2);
        BinaryPrimitives.ReadInt32LittleEndian(span[24..]).Should().Be(TuneSynth.SAMPLE_RATE);
        BinaryPrimitives.ReadInt32LittleEndian(span[28..]).Should().Be(TuneSynth.SAMPLE_RATE * 4);
        BinaryPrimitives.ReadInt16LittleEndian(span[32..]).Should().Be(4);
        BinaryPrimitives.ReadInt16LittleEndian(span[34..]).Should().Be(16);
        Encoding.ASCII.GetString(wav, 36, 4).Should().Be("data");
        BinaryPrimitives.ReadInt32LittleEndian(span[40..]).Should().Be(8);
    }

    [Test]
    public void The_samples_follow_the_header_little_endian()
    {
        var wav = TuneWav.Wrap([1, -1, 300, -300]).AsSpan(44);

        BinaryPrimitives.ReadInt16LittleEndian(wav).Should().Be(1);
        BinaryPrimitives.ReadInt16LittleEndian(wav[2..]).Should().Be(-1);
        BinaryPrimitives.ReadInt16LittleEndian(wav[4..]).Should().Be(300);
        BinaryPrimitives.ReadInt16LittleEndian(wav[6..]).Should().Be(-300);
    }
}
