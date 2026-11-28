#region
using System.Text.Json;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>
///     Loads and saves the per-character quest watch data (the seen snapshot, the pinned quest key and the
///     manually dismissed keys). The file lives in the character's own settings folder next to
///     <c>Settings.cfg</c>; callers resolve the path with
///     <c>DataContext.LocalPlayerSettings.GetFilePath(QuestWatchStore.FILE_NAME)</c>.
/// </summary>
public static class QuestWatchStore
{
    public const string FILE_NAME = "questwatch.json";

    /// <summary>
    ///     Reads the watch data at <paramref name="path" />. A missing, empty or invalid file loads as empty
    ///     data, so a fresh character or a corrupt file never crashes login.
    /// </summary>
    public static QuestWatchData Load(string path)
    {
        try
        {
            if (!File.Exists(path))
                return new QuestWatchData();

            var text = File.ReadAllText(path);

            if (string.IsNullOrWhiteSpace(text))
                return new QuestWatchData();

            var data = JsonSerializer.Deserialize<QuestWatchData>(text);

            if (data is null)
                return new QuestWatchData();

            data.Seen ??= [];
            data.Dismissed ??= [];

            return data;
        } catch
        {
            return new QuestWatchData();
        }
    }

    /// <summary>
    ///     Writes <paramref name="data" /> to <paramref name="path" /> via a temp file next to the target,
    ///     then moves it over, so a crash mid-write never leaves a half-written snapshot behind.
    /// </summary>
    public static void Save(string path, QuestWatchData data)
    {
        var directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(data));
        File.Move(temp, path, true);
    }
}
