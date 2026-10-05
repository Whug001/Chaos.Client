namespace Chaos.Client.Systems.College;

/// <summary>College pictures on disk, named by hash, in a folder capped at a size; the least recently used go first.</summary>
public sealed class CollegePictureCache
{
    public const long DEFAULT_CAP = 50L * 1024 * 1024;

    private readonly long Cap;
    private readonly string Directory;

    public CollegePictureCache()
        : this(Path.Combine(GlobalSettings.DataPath, "CollegePictures"), DEFAULT_CAP) { }

    public CollegePictureCache(string directory, long cap)
    {
        Directory = directory;
        Cap = cap;
        System.IO.Directory.CreateDirectory(directory);
    }

    private static bool IsHash(string hash) => (hash.Length == 64) && hash.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    private string PathOf(string hash) => Path.Combine(Directory, hash + ".pic");

    /// <summary>The cached bytes, or null when the picture isn't cached or its file can't be read.</summary>
    public byte[]? TryRead(string hash)
    {
        if (!IsHash(hash))
            return null;

        try
        {
            if (!File.Exists(PathOf(hash)))
                return null;

            File.SetLastWriteTimeUtc(PathOf(hash), DateTime.UtcNow);

            return File.ReadAllBytes(PathOf(hash));
        } catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Caches a picture. A failed write (a locked file, a full disk) only means it is fetched again next time.</summary>
    public void Write(string hash, byte[] bytes)
    {
        if (!IsHash(hash))
            return;

        try
        {
            File.WriteAllBytes(PathOf(hash), bytes);
            Trim();
        } catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Drops a cached picture, for one that turned out not to decode.</summary>
    public void Delete(string hash)
    {
        if (!IsHash(hash))
            return;

        try
        {
            File.Delete(PathOf(hash));
        } catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    private void Trim()
    {
        var files = new DirectoryInfo(Directory).GetFiles("*.pic").OrderBy(f => f.LastWriteTimeUtc).ToList();
        var total = files.Sum(f => f.Length);

        foreach (var file in files)
        {
            if (total <= Cap)
                break;

            total -= file.Length;
            file.Delete();
        }
    }
}
