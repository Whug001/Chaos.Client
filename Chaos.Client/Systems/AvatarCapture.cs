using System.Text;
using Chaos.Client.Rendering;
using Chaos.DarkAges.Definitions;
using Microsoft.Xna.Framework.Graphics;

namespace Chaos.Client.Systems;

/// <summary>Renders the player's own front-facing idle figure and writes it to
/// &lt;DA_CARD_AVATAR_DIR&gt;/&lt;username&gt;.png so the launcher's Quick Launch card can show it.
/// Gated on the card env vars; best-effort (never throws to the caller). Front-facing idle frame:
/// frameIndex 5 (RIGHT_IDLE_FRAME), animSuffix "04" (IDLE_ANIM).</summary>
public static class AvatarCapture
{
    private const int FrontIdleFrame = 5;
    private const string IdleAnim = "04";

    // MUST match the launcher's AvatarPathResolver.MaxLength (cross-repo contract).
    private const int MaxLength = 64;

    public static bool IsEnabled =>
        !string.IsNullOrEmpty(GlobalSettings.CardAvatarDir) &&
        !string.IsNullOrEmpty(GlobalSettings.AutoUsername);

    /// <summary>Mirrors the launcher's AvatarPathResolver.Sanitize BYTE-FOR-BYTE: any char outside
    /// [A-Za-z0-9_-] -> '_', length capped at MaxLength, empty -> "_" (so an empty username can't
    /// produce a hidden ".png" dotfile). Keep identical to the launcher side.</summary>
    public static string Sanitize(string username)
    {
        var sb = new StringBuilder(username.Length);
        foreach (var ch in username)
        {
            if (sb.Length >= MaxLength)
                break;
            sb.Append(char.IsAsciiLetterOrDigit(ch) || ch is '_' or '-' ? ch : '_');
        }
        return sb.Length == 0 ? "_" : sb.ToString();
    }

    public static string BuildPath(string dir, string username) =>
        System.IO.Path.Combine(dir, Sanitize(username) + ".png");

    /// <summary>The vitals sidecar path: same directory and same sanitized stem as the PNG, with a
    /// .json extension. The launcher reads this via AvatarPathResolver.ResolveStatsFor.</summary>
    public static string BuildStatsPath(string dir, string username) =>
        System.IO.Path.Combine(dir, Sanitize(username) + ".json");

    public static void CaptureAndSave(AislingRenderer renderer, in AislingAppearance appearance)
    {
        if (!IsEnabled)
            return;

        try
        {
            using var texture = renderer.Render(
                in appearance,
                frameIndex: FrontIdleFrame,
                animSuffix: IdleAnim,
                flipHorizontal: false,
                isFrontFacing: true,
                emotionFrame: -1);

            if (texture is null)
                return;

            var dir = GlobalSettings.CardAvatarDir!;
            System.IO.Directory.CreateDirectory(dir);

            var path = BuildPath(dir, GlobalSettings.AutoUsername!);
            var tmp = path + ".tmp";

            using (var fs = System.IO.File.Create(tmp))
                texture.SaveAsPng(fs, texture.Width, texture.Height);

            System.IO.File.Move(tmp, path, overwrite: true);
        }
        catch
        {
            // Best-effort: a capture failure must never affect login or gameplay.
        }
    }

    /// <summary>Writes the player's current vitals to &lt;DA_CARD_AVATAR_DIR&gt;/&lt;username&gt;.json so the
    /// launcher card can show class, level, HP/MP and zone alongside the avatar. Same gating, same
    /// atomic temp-then-move, and the same never-throw contract as the avatar write.
    ///
    /// Cross-repo contract: property names are camelCase and MUST stay in sync with the launcher's
    /// Chaos.Launcher.Core.Cards.CharacterStats.</summary>
    public static void SaveStats()
    {
        if (!IsEnabled)
            return;

        try
        {
            //attributes are the authoritative vitals; without them there is nothing worth writing.
            if (Collections.WorldState.Attributes.Current is not { } attrs)
                return;

            var dir = GlobalSettings.CardAvatarDir!;
            System.IO.Directory.CreateDirectory(dir);

            var path = BuildStatsPath(dir, GlobalSettings.AutoUsername!);

            //class comes from the class enums only. The SelfProfile display string is deliberately NOT used:
            //for a master-class player the server sends "Master", which is a profile title rather than a
            //class, and showing that on the card would replace the player's actual class.
            var advClass = Collections.WorldState.AdvClass;
            var isAdvanced = advClass != AdvClass.None;

            var displayClass = isAdvanced
                ? SpaceCamelCase(advClass.ToString())
                : Collections.WorldState.BaseClass.ToString();

            var json = string.Concat(
                "{\"class\":", Quote(displayClass),
                ",\"isAdvanced\":", isAdvanced ? "true" : "false",
                ",\"level\":", attrs.Level.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ",\"abilityLevel\":", attrs.Ability.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ",\"currentHealth\":", attrs.CurrentHp.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ",\"maxHealth\":", attrs.MaximumHp.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ",\"currentMana\":", attrs.CurrentMp.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ",\"maxMana\":", attrs.MaximumMp.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ",\"zone\":", Quote(Collections.WorldState.CurrentZoneName),
                "}");

            var tmp = path + ".tmp";
            System.IO.File.WriteAllText(tmp, json, new UTF8Encoding(false));
            System.IO.File.Move(tmp, path, overwrite: true);
        }
        catch
        {
            // Best-effort: a stats write must never affect login or gameplay.
        }
    }

    /// <summary>Splits a PascalCase enum name into words: "PlagueDoctor" -> "Plague Doctor". The AdvClass
    /// enum has no spaces, but players read the class with one.</summary>
    private static string SpaceCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var sb = new StringBuilder(value.Length + 4);

        for (var i = 0; i < value.Length; i++)
        {
            if ((i > 0) && char.IsUpper(value[i]))
                sb.Append(' ');

            sb.Append(value[i]);
        }

        return sb.ToString();
    }

    /// <summary>Renders a value as a quoted, escaped JSON string. Delegates to the BCL serializer so
    /// a class or zone name containing a quote or backslash can't produce a malformed sidecar.</summary>
    private static string Quote(string? value)
        => System.Text.Json.JsonSerializer.Serialize(value ?? string.Empty);
}
