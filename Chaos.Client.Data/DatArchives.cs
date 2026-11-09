#region
using Chaos.Extensions.Common;
using DALib.Data;
using DALib.Extensions;
#endregion

namespace Chaos.Client.Data;

public static class DatArchives
{
    public static DataArchive Cious { get; }
    public static DataArchive Hades { get; }
    public static DataArchive Ia { get; }
    public static DataArchive Khanmad { get; }
    public static DataArchive Khanmeh { get; }
    public static DataArchive Khanmim { get; }
    public static DataArchive Khanmns { get; }
    public static DataArchive Khanmtz { get; }
    public static DataArchive Khanpal { get; }
    public static DataArchive Khanwad { get; }
    public static DataArchive Khanweh { get; }
    public static DataArchive Khanwim { get; }
    public static DataArchive Khanwns { get; }
    public static DataArchive Khanwtz { get; }
    public static DataArchive Legend { get; }
    public static DataArchive Misc { get; }
    public static DataArchive National { get; }
    public static DataArchive Npcbase { get; }
    public static DataArchive Roh { get; }
    public static DataArchive Seo { get; }
    public static DataArchive Setoa { get; }

    static DatArchives()
    {
        Cious = Load(nameof(Cious));
        Hades = Load(nameof(Hades));
        Ia = Load(nameof(Ia));
        Khanmad = Load(nameof(Khanmad));
        Khanmeh = Load(nameof(Khanmeh));
        Khanmim = Load(nameof(Khanmim));
        Khanmns = Load(nameof(Khanmns));
        Khanmtz = Load(nameof(Khanmtz));
        Khanpal = Load(nameof(Khanpal));
        Khanwad = Load(nameof(Khanwad));
        Khanweh = Load(nameof(Khanweh));
        Khanwim = Load(nameof(Khanwim));
        Khanwns = Load(nameof(Khanwns));
        Khanwtz = Load(nameof(Khanwtz));
        Legend = Load(nameof(Legend));
        Misc = Load(nameof(Misc));
        National = Load(nameof(National));
        Roh = Load(nameof(Roh));
        Seo = Load(nameof(Seo));
        Setoa = Load(nameof(Setoa));
        Npcbase = Load(nameof(Npcbase));
    }

    public static DataArchive Load(string key)
    {
        // Linux filesystems are case-sensitive and most DA .dat archives ship lowercase, so normalize the
        // nameof()-derived keys (e.g. "Cious") to match the on-disk names. Harmless on Windows.
        key = key.WithExtension(".dat")
                 .ToLowerInvariant();

        var directory = key.StartsWithI("npcbase")
            ? Path.Combine(DataContext.DataPath, "npc")
            : DataContext.DataPath;

        return DataArchive.FromFile(ResolvePath(directory, key));
    }

    // Not every archive is lowercase on disk (e.g. Legend.dat), so fall back to a case-insensitive match.
    // Returns the exact path when nothing matches so the open still reports the expected file as missing.
    private static string ResolvePath(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);

        if (File.Exists(path) || !Directory.Exists(directory))
            return path;

        return Directory.EnumerateFiles(directory)
                        .FirstOrDefault(file => string.Equals(Path.GetFileName(file), fileName, StringComparison.OrdinalIgnoreCase))
               ?? path;
    }
}