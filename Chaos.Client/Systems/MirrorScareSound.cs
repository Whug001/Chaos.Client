namespace Chaos.Client.Systems;

/// <summary>A short dissonant sting played on the first frame of a mirror scare. Generated, so it needs no archive entry.</summary>
public static class MirrorScareSound
{
    public const int KEY = -1001;

    public static byte[] Wav { get; } = Build();

    private static byte[] Build()
    {
        const int RATE = 22050;
        const double SECONDS = 0.28;
        var samples = (int)(RATE * SECONDS);
        var pcm = new byte[samples * 2];

        for (var i = 0; i < samples; i++)
        {
            var t = i / (double)RATE;
            var env = Math.Exp(-t * 9);
            var noise = ((i * 1103515245 + 12345) & 0x7FFF) / 32767.0 * 2 - 1;
            var tone = Math.Sin(2 * Math.PI * 196 * t)
                       + 0.6 * Math.Sin(2 * Math.PI * 247 * t)
                       + 0.35 * Math.Sin(2 * Math.PI * 370 * t);
            var sample = (tone * 0.45 + noise * 0.25) * env;
            var value = (short)Math.Clamp(sample * 16000, short.MinValue, short.MaxValue);
            pcm[i * 2] = (byte)value;
            pcm[i * 2 + 1] = (byte)(value >> 8);
        }

        var wav = new byte[44 + pcm.Length];
        wav[0] = (byte)'R';
        wav[1] = (byte)'I';
        wav[2] = (byte)'F';
        wav[3] = (byte)'F';
        WriteInt(wav, 4, wav.Length - 8);
        wav[8] = (byte)'W';
        wav[9] = (byte)'A';
        wav[10] = (byte)'V';
        wav[11] = (byte)'E';
        wav[12] = (byte)'f';
        wav[13] = (byte)'m';
        wav[14] = (byte)'t';
        wav[15] = (byte)' ';
        WriteInt(wav, 16, 16);
        wav[20] = 1;
        wav[22] = 1;
        WriteInt(wav, 24, RATE);
        WriteInt(wav, 28, RATE * 2);
        wav[32] = 2;
        wav[34] = 16;
        wav[36] = (byte)'d';
        wav[37] = (byte)'a';
        wav[38] = (byte)'t';
        wav[39] = (byte)'a';
        WriteInt(wav, 40, pcm.Length);
        pcm.CopyTo(wav, 44);

        return wav;
    }

    private static void WriteInt(byte[] bytes, int at, int value)
    {
        bytes[at] = (byte)value;
        bytes[at + 1] = (byte)(value >> 8);
        bytes[at + 2] = (byte)(value >> 16);
        bytes[at + 3] = (byte)(value >> 24);
    }
}
