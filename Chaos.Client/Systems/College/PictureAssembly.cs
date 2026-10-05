using Chaos.DarkAges.Definitions;

namespace Chaos.Client.Systems.College;

/// <summary>Puts one downloaded picture back together from its parts.</summary>
public sealed class PictureAssembly(int count)
{
    private readonly byte[]?[] Parts = new byte[]?[count];

    public bool Failed { get; private set; }

    /// <summary>The whole picture once the last part arrives; null before that or after a bad part.</summary>
    public byte[]? Add(int index, int partCount, byte[] data)
    {
        if (Failed || (partCount != Parts.Length) || (index < 0) || (index >= Parts.Length) || (data.Length > CollegeProtocol.PART_SIZE))
        {
            Failed = true;

            return null;
        }

        Parts[index] = data;

        return Parts.Any(p => p is null) ? null : Parts.SelectMany(p => p!).ToArray();
    }
}
