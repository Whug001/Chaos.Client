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

    // --- Effect animation cap (client-local; enforced in WorldScreen effect handling) ---
    public static int MaxEffectAnimationsPerEntity { get; set; } = 2;

    // --- Floating damage / heal numbers (client-local; gated in EntityOverlayManager) ---
    public static bool DamageNumbersEnabled { get; set; } = true;
    public static bool ShowDamageNumbersOnAislings { get; set; } = true;
    public static bool ShowHealNumbersOnAislings { get; set; } = true;
    public static bool ShowDamageNumbersOnNpcs { get; set; } = true;
    public static bool ShowHealNumbersOnNpcs { get; set; } = true;
    public static DamageNumberSize DamageNumberSize { get; set; } = DamageNumberSize.Compact;

    // --- Numeric cooldown readout on skill/spell slots (client-local; gated in PanelSlot) ---
    public static bool CooldownNumbersEnabled { get; set; } = true;

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

                    case "DamageNumbersEnabled":
                        DamageNumbersEnabled = value == "1";

                        break;

                    case "ShowDamageNumbersOnAislings":
                        ShowDamageNumbersOnAislings = value == "1";

                        break;

                    case "ShowHealNumbersOnAislings":
                        ShowHealNumbersOnAislings = value == "1";

                        break;

                    case "ShowDamageNumbersOnNpcs":
                        ShowDamageNumbersOnNpcs = value == "1";

                        break;

                    case "ShowHealNumbersOnNpcs":
                        ShowHealNumbersOnNpcs = value == "1";

                        break;

                    case "DamageNumberSize":
                        if (int.TryParse(value, out var dnsz) && Enum.IsDefined((DamageNumberSize)dnsz))
                            DamageNumberSize = (DamageNumberSize)dnsz;

                        break;

                    case "CooldownNumbersEnabled":
                        CooldownNumbersEnabled = value == "1";

                        break;

                    case "MaxEffectAnimations":
                        if (int.TryParse(value, out var mea))
                            MaxEffectAnimationsPerEntity = Math.Clamp(mea, 0, 10);

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
            writer.WriteLine($"DamageNumbersEnabled : {(DamageNumbersEnabled ? 1 : 0)}");
            writer.WriteLine($"ShowDamageNumbersOnAislings : {(ShowDamageNumbersOnAislings ? 1 : 0)}");
            writer.WriteLine($"ShowHealNumbersOnAislings : {(ShowHealNumbersOnAislings ? 1 : 0)}");
            writer.WriteLine($"ShowDamageNumbersOnNpcs : {(ShowDamageNumbersOnNpcs ? 1 : 0)}");
            writer.WriteLine($"ShowHealNumbersOnNpcs : {(ShowHealNumbersOnNpcs ? 1 : 0)}");
            writer.WriteLine($"DamageNumberSize : {(int)DamageNumberSize}");
            writer.WriteLine($"CooldownNumbersEnabled : {(CooldownNumbersEnabled ? 1 : 0)}");
            writer.WriteLine($"MaxEffectAnimations : {MaxEffectAnimationsPerEntity}");
        } catch
        {
            //best effort — don't crash on save failure
        }
    }
}