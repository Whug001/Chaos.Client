namespace Chaos.Client.Systems;

/// <summary>
///     Reads and writes the client settings file (Darkages.cfg). Line-delimited key-value format:
///     "Key : Value" or "Key: Value". Saved next to the executable. Only the settings the client
///     actually consumes are persisted; original-client compatibility lines were removed.
/// </summary>
public static class ClientSettings
{
    private const string FILE_NAME = "Darkages.cfg";
    public static bool AutoAcceptGroupInvites { get; set; } = true;
    public static bool ClickToOpenProfile { get; set; }
    public static bool AllowGroupInvites { get; set; }
    public static int MusicVolume { get; set; } = 5;
    public static bool NpcRecordChat { get; set; } = true;
    public static int ScrollLevel { get; set; }
    public static ScreenMode ScreenMode { get; set; } = ScreenMode.Windowed1x;

    //defaults match the original client
    public static int SoundVolume { get; set; } = 5;
    public static bool DoubleTapForAltPanels { get; set; } = true;

    private static string FilePath => Path.Combine(GlobalSettings.DataPath, FILE_NAME);

    /// <summary>
    ///     Loads settings from Darkages.cfg into static properties. Uses defaults if the file doesn't exist or is corrupt.
    ///     Unknown keys (including removed legacy keys) are ignored.
    /// </summary>
    public static void Load()
    {
        if (!File.Exists(FilePath))
            return;

        try
        {
            foreach (var line in File.ReadLines(FilePath))
            {
                var colonIndex = line.IndexOf(':');

                if (colonIndex < 0)
                    continue;

                var key = line[..colonIndex]
                    .Trim();

                var value = line[(colonIndex + 1)..]
                    .Trim();

                switch (key)
                {
                    case "Sound Volume":
                        if (int.TryParse(value, out var sv))
                            SoundVolume = Math.Clamp(sv, 0, 10);

                        break;

                    case "Music Volume":
                        if (int.TryParse(value, out var mv))
                            MusicVolume = Math.Clamp(mv, 0, 10);

                        break;

                    case "SkillSpellSelectByToggle":
                        DoubleTapForAltPanels = value == "1";

                        break;

                    case "GroupAnswer":
                        AllowGroupInvites = value == "1";

                        break;

                    case "ScrollLevel":
                        if (int.TryParse(value, out var sl))
                            ScrollLevel = sl;

                        break;

                    case "UserClickMode":
                        ClickToOpenProfile = value != "1";

                        break;

                    case "MonsterSayRecordMode":
                        NpcRecordChat = value == "1";

                        break;

                    case "GroupObjectOption":
                        AutoAcceptGroupInvites = value == "1";

                        break;

                    case "ScreenMode":
                        if (int.TryParse(value, out var sm) && Enum.IsDefined((ScreenMode)sm))
                            ScreenMode = (ScreenMode)sm;

                        break;
                }
            }
        } catch
        {
            //corrupted file — use whatever defaults/partial state was already set
        }
    }

    /// <summary>
    ///     Saves the current settings to Darkages.cfg. Only client-used keys are written.
    /// </summary>
    public static void Save()
    {
        try
        {
            using var writer = new StreamWriter(FilePath, false);
            writer.WriteLine($"Sound Volume : {SoundVolume}");
            writer.WriteLine($"Music Volume : {MusicVolume}");
            writer.WriteLine($"SkillSpellSelectByToggle : {(DoubleTapForAltPanels ? 1 : 0)}");
            writer.WriteLine($"GroupAnswer : {(AllowGroupInvites ? 1 : 0)}");
            writer.WriteLine($"ScrollLevel : {ScrollLevel}");
            writer.WriteLine($"UserClickMode : {(ClickToOpenProfile ? 0 : 1)}");
            writer.WriteLine($"MonsterSayRecordMode : {(NpcRecordChat ? 1 : 0)}");
            writer.WriteLine($"GroupObjectOption : {(AutoAcceptGroupInvites ? 1 : 0)}");
            writer.WriteLine($"ScreenMode : {(int)ScreenMode}");
        } catch
        {
            //best effort — don't crash on save failure
        }
    }
}