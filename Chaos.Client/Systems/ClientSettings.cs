#region
using Chaos.Client.Definitions;
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.Systems;

/// <summary>
///     Reads and writes the client settings file. Line-delimited key-value format: "Key : Value" or
///     "Key: Value". Only the settings the client actually consumes are persisted; original-client
///     compatibility lines were removed.
/// </summary>
/// <remarks>
///     There are two files, and which one is in play depends on whether a character is logged in.
///     <para />
///     Before login there is only <c>Darkages.cfg</c> next to the executable. Once a character enters the world,
///     <see cref="LoadForCharacter" /> switches to that character's own <c>Settings.cfg</c>, and every
///     <see cref="Save" /> from then on writes there. Two characters on one machine each keep their own F4
///     settings, which is the point; the install-wide file stays as the starting point a brand new character
///     inherits.
///     <para />
///     Settings the F4 screen does not own -- the emote wheel, for instance -- ride along in the same file and so
///     become per-character too. That is the same answer for the same reason, not an accident.
/// </remarks>
public static class ClientSettings
{
    private const string FILE_NAME = "Darkages.cfg";

    /// <summary>The per-character file's name, inside that character's folder.</summary>
    private const string CHARACTER_FILE_NAME = "Settings.cfg";

    /// <summary>Who is logged in, or null before anyone is. Decides which file <see cref="FilePath" /> names.</summary>
    private static string? ActiveCharacter;
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

    // --- Group member panels down the side of the viewport (client-local; gated in GroupPanelStack) ---
    public static bool GroupPanelEnabled { get; set; } = true;

    // --- Group panel backing (client-local; read by GroupPanelStack) ---
    // Off by default: the column is a readout to be glanced at mid-fight, and a solid plate behind it is what
    // keeps it legible over whatever map art it happens to be sitting on. Players who would rather read it
    // against the world turn this on.
    public static bool TransparentGroupPanels { get; set; }

    // --- Ground-target aim snapping (client-local; applied in WorldScreen.GroundTargetTileAt) ---
    public static bool GroundTargetSnapToEntity { get; set; } = true;

    // --- Emote wheel slot assignments (client-local; middle-mouse radial wheel) ---
    public static BodyAnimation[] EmoteWheelSlots { get; set; } = (BodyAnimation[])EmoteCatalog.DefaultWheelSlots.Clone();

    private static string FilePath
        => ActiveCharacter is null
            ? Path.Combine(GlobalSettings.DataPath, FILE_NAME)
            : Path.Combine(GlobalSettings.DataPath, ActiveCharacter, CHARACTER_FILE_NAME);

    /// <summary>
    ///     Switches to <paramref name="characterName" />'s own settings file and reads it. Call this when a
    ///     character enters the world; callers must re-apply anything with a live effect afterwards, since this
    ///     only changes the values.
    /// </summary>
    /// <remarks>
    ///     A character who has no file of their own yet keeps whatever is already loaded -- which is the
    ///     install-wide <c>Darkages.cfg</c>, or the previous character on a character switch -- and it is written
    ///     out under their name immediately. So an existing player's settings follow them onto their first login
    ///     rather than resetting to defaults, and every login after that reads their own file.
    /// </remarks>
    public static void LoadForCharacter(string characterName)
    {
        if (string.IsNullOrWhiteSpace(characterName))
            return;

        ActiveCharacter = characterName;

        if (File.Exists(FilePath))
        {
            Load();

            return;
        }

        //Load() only overwrites the keys a file actually contains, so seeding by saving what is in memory loses
        //nothing and gives the new file every key at once
        Save();
    }

    /// <summary>
    ///     Loads settings from Darkages.cfg into static properties. Uses defaults if the file doesn't exist or is corrupt.
    ///     Unknown keys (including removed legacy keys) are ignored.
    /// </summary>
    public static void Load()
    {
        if (!File.Exists(FilePath))
            return;

        //note: keys absent from the file keep whatever value they already hold, rather than reverting to the
        //property initializer. That is what lets LoadForCharacter seed a new character from the old values

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

                    case "GroupPanelEnabled":
                        GroupPanelEnabled = value == "1";

                        break;

                    case "TransparentGroupPanels":
                        TransparentGroupPanels = value == "1";

                        break;

                    case "GroundTargetSnapToEntity":
                        GroundTargetSnapToEntity = value == "1";

                        break;

                    case "MaxEffectAnimations":
                        if (int.TryParse(value, out var mea))
                            MaxEffectAnimationsPerEntity = Math.Clamp(mea, 0, 10);

                        break;

                    case "EmoteWheel0":
                    case "EmoteWheel1":
                    case "EmoteWheel2":
                    case "EmoteWheel3":
                    case "EmoteWheel4":
                    case "EmoteWheel5":
                        //the catalog is the authority on what a slot may hold — it includes the client-side
                        //sunglasses emote, whose byte is deliberately not a defined BodyAnimation member, so
                        //an Enum.IsDefined check here would silently drop that slot on every restart.
                        if (int.TryParse(value, out var ew) && EmoteCatalog.TryGet((BodyAnimation)ew, out _))
                        {
                            var idx = key[^1] - '0';
                            EmoteWheelSlots[idx] = (BodyAnimation)ew;
                        }

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
            var directory = Path.GetDirectoryName(FilePath);

            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

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
            writer.WriteLine($"GroupPanelEnabled : {(GroupPanelEnabled ? 1 : 0)}");
            writer.WriteLine($"TransparentGroupPanels : {(TransparentGroupPanels ? 1 : 0)}");
            writer.WriteLine($"GroundTargetSnapToEntity : {(GroundTargetSnapToEntity ? 1 : 0)}");
            writer.WriteLine($"MaxEffectAnimations : {MaxEffectAnimationsPerEntity}");

            for (var i = 0; i < EmoteCatalog.SLOT_COUNT; i++)
                writer.WriteLine($"EmoteWheel{i} : {(int)EmoteWheelSlots[i]}");
        } catch
        {
            //best effort — don't crash on save failure
        }
    }
}