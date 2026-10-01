namespace Chaos.Client.Systems;

/// <summary>Legend.dat stingers. A haunted-mirror scare plays one of these, chosen at random.</summary>
public static class MirrorScareSound
{
    public static readonly int[] Ids = [120, 134, 136, 154, 17];

    public static int Pick(Random random) => Ids[random.Next(Ids.Length)];
}
