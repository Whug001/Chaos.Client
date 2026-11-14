#region
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>
///     Fixed strings for the battle window: result, clock, rising damage, status tags and log lines.
///     Pure: no textures.
/// </summary>
public static class TamerBattleText
{
    public const string BrokenMessage = "The fight could not be shown.";
    public const string Victory = "Victory";
    public const string Defeat = "Defeat";
    public const string AllFoesDown = "All foes down";
    public const string TimeReason = "Time: decided on remaining health";
    public const string Pause = "Pause";
    public const string Play = "Play";
    public const string Skip = "Skip";
    public const string Replay = "Replay";
    public const string Close = "Close";
    public const string DamageRisingPrefix = "Damage rising";

    /// <summary>The banner over the board when the fight is finished.</summary>
    public static string ResultTitle(TamerBattleViewerSide viewer, TamerBattleSide winner, string attackerName, string defenderName)
    {
        return viewer switch
        {
            TamerBattleViewerSide.Attacker => winner == TamerBattleSide.Attacker ? Victory : Defeat,
            TamerBattleViewerSide.Defender => winner == TamerBattleSide.Defender ? Victory : Defeat,
            _ => winner == TamerBattleSide.Attacker
                ? $"{DisplaySideName(attackerName, "Attacker")} wins"
                : $"{DisplaySideName(defenderName, "Defender")} wins"
        };
    }

    /// <summary>The line under the banner: wipe-out or time.</summary>
    public static string ResultReason(TamerBattleEndReason reason)
        => reason == TamerBattleEndReason.WipeOut ? AllFoesDown : TimeReason;

    /// <summary>The clock as seconds with one decimal, e.g. "12.3".</summary>
    public static string ClockText(double clockTicks)
    {
        var seconds = Math.Max(0, clockTicks) / 10.0;
        return seconds.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>The top line "ATTACKER vs DEFENDER" in side-name form.</summary>
    public static string VersusText(string attackerName, string defenderName)
        => $"{NonBlank(attackerName, "Attacker")} vs {NonBlank(defenderName, "Defender")}";

    /// <summary>"Damage rising +N%" from the second the rise starts, or empty before it.</summary>
    public static string RisingText(int risingPercent)
    {
        return risingPercent <= 100 ? string.Empty : $"{DamageRisingPrefix} +{risingPercent - 100}%";
    }

    /// <summary>The speed button caption, e.g. "1x".</summary>
    public static string SpeedText(int speed) => $"{speed}x";

    /// <summary>"Lv N" for a level.</summary>
    public static string LevelText(byte level) => $"Lv {level}";

    /// <summary>"Health / Max" for the roster row.</summary>
    public static string HealthText(int health, int maxHealth) => $"{Math.Max(0, health)} / {Math.Max(0, maxHealth)}";

    /// <summary>The short roster tag for a status key; an arrow and stat for raised/lowered stats.</summary>
    public static string StatusTag(string? key)
    {
        if (string.IsNullOrEmpty(key))
            return string.Empty;

        return key switch
        {
            "Shield" => "Shield",
            "Stun" => "Stun",
            "Burn" => "Burn",
            "Taunt" => "Taunt",
            "AttackUp" => "+Atk",
            "AttackDown" => "-Atk",
            "ArmorUp" => "+Arm",
            "ArmorDown" => "-Arm",
            "SpeedUp" => "+Spd",
            "SpeedDown" => "-Spd",
            _ => key
        };
    }

    /// <summary>The display name for a key from the start's name table; the key itself when missing.</summary>
    public static string DisplayName(string? key, IReadOnlyList<TamerBattleNameEntry> names)
    {
        if (string.IsNullOrEmpty(key))
            return string.Empty;

        if (names is not null)
        {
            foreach (var entry in names)
            {
                if (entry is not null && entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                    return string.IsNullOrEmpty(entry.Name) ? key : entry.Name;
            }
        }

        return key;
    }

    /// <summary>Looks a key up in a pre-built case-insensitive map; the key itself when missing.</summary>
    public static string DisplayName(string? key, IReadOnlyDictionary<string, string> names)
    {
        if (string.IsNullOrEmpty(key))
            return string.Empty;

        if ((names is not null) && names.TryGetValue(key, out var name) && !string.IsNullOrEmpty(name))
            return name;

        return key;
    }

    public static string AbilityLine(string unitName, string abilityName) => $"{unitName} uses {abilityName}";

    public static string TraitLine(string unitName, string traitName) => $"{unitName}: {traitName}";

    public static string SetBonusLine(string squadName, string bonusName) => $"{squadName}: {bonusName}";

    public static string DownLine(string unitName) => $"{unitName} falls";

    public static string ElementName(TamerWireElement element)
        => element switch
        {
            TamerWireElement.Fire => "Fire",
            TamerWireElement.Water => "Water",
            TamerWireElement.Wind => "Wind",
            TamerWireElement.Earth => "Earth",
            _ => "Unknown"
        };

    private static string NonBlank(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string DisplaySideName(string? value, string fallback) => NonBlank(value, fallback);
}
