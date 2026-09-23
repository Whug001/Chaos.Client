#region
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
#endregion

namespace Chaos.Client.Chat;

/// <summary>
///     Read-only snapshot of rule-ID to fantasy-substitution mappings. Loaded once (see
///     <see cref="Default" />) and reused for every message: <see cref="ChatSpanApplier.Apply" /> does no IO.
///     <para>
///     Loader choice: the client has no JSON content pattern (LocalPlayerSettings reads per-character .cfg
///     files from DataPath, which is wrong for a shipped seed map), so the seed travels as an embedded
///     resource (<c>fantasy-chat-map.json</c> beside this file) parsed once into this immutable snapshot.
///     </para>
///     <para>
///     Safety: duplicate rule IDs poison the ID (it looks up as unknown and masks to <c>****</c>), keys that
///     do not match the wire rule-ID shape are ignored, and malformed content logs and yields an empty map
///     instead of throwing into chat. The file holds no real slur strings: non-slur rules map to the spec
///     fantasy words, and slur-category IDs map only to outlander/outsider/lowborn/clan-rat.
///     </para>
/// </summary>
public sealed partial class FantasyDictionary
{
    private static readonly Lazy<FantasyDictionary> LazyDefault = new(
        LoadEmbedded,
        LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly Dictionary<string, string> Substitutions;

    private FantasyDictionary(Dictionary<string, string> substitutions) => Substitutions = substitutions;

    /// <summary>The seed map, parsed once from the embedded resource and shared by every message.</summary>
    public static FantasyDictionary Default => LazyDefault.Value;

    /// <summary>An empty map: every span masks. Used when the seed is missing or malformed.</summary>
    public static FantasyDictionary Empty { get; } = new(new Dictionary<string, string>(StringComparer.Ordinal));

    /// <summary>How many rule IDs this snapshot maps.</summary>
    public int Count => Substitutions.Count;

    /// <summary>Looks up the fantasy substitution for a rule ID. Poisoned and unknown IDs return false.</summary>
    public bool TryGetSubstitution(string ruleId, out string? substitution)
    {
        if (ruleId is null)
        {
            substitution = null;

            return false;
        }

        return Substitutions.TryGetValue(ruleId, out substitution);
    }

    /// <summary>
    ///     Parses a JSON object of rule-ID to substitution into a snapshot. Never throws: malformed content is
    ///     logged (via <paramref name="log" />, defaulting to debug output) and yields an empty map.
    /// </summary>
    public static FantasyDictionary FromJson(string json, Action<string>? log = null)
    {
        log ??= message => Debug.WriteLine($"[FantasyDictionary] {message}");

        try
        {
            using var document = JsonDocument.Parse(json);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                log(
                    $"expected a JSON object of rule-ID to substitution, got {document.RootElement.ValueKind}; using an empty map");

                return Empty;
            }

            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            var duplicates = new HashSet<string>(StringComparer.Ordinal);

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!RuleIdPattern()
                        .IsMatch(property.Name))
                {
                    log($"ignoring entry with malformed rule ID '{property.Name}'");

                    continue;
                }

                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    log($"ignoring '{property.Name}': substitution must be a string");

                    continue;
                }

                var word = property.Value.GetString();

                if (string.IsNullOrWhiteSpace(word))
                {
                    log($"ignoring '{property.Name}': empty substitution");

                    continue;
                }

                //the parser would silently keep the last duplicate, but the contract says duplicates
                //mask: the first repeat poisons the ID and every repeat after it is dropped too
                if (!map.TryAdd(property.Name, word))
                {
                    duplicates.Add(property.Name);
                    log($"ignoring '{property.Name}': duplicate rule ID masks to ****");
                }
            }

            foreach (var duplicate in duplicates)
                map.Remove(duplicate);

            return new FantasyDictionary(map);
        } catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            log($"malformed fantasy map ({ex.Message}); using an empty map so chat stays usable");

            return Empty;
        }
    }

    private static FantasyDictionary LoadEmbedded()
    {
        try
        {
            using var stream = System.Reflection.Assembly.GetExecutingAssembly()
                                       .GetManifestResourceStream("fantasy-chat-map.json");

            if (stream is null)
            {
                Debug.WriteLine("[FantasyDictionary] embedded fantasy-chat-map.json is missing; using an empty map");

                return Empty;
            }

            using var reader = new StreamReader(stream);

            return FromJson(reader.ReadToEnd());
        } catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException)
        {
            Debug.WriteLine($"[FantasyDictionary] failed to load the seed map ({ex.Message}); using an empty map");

            return Empty;
        }
    }

    //wire rule-ID shape from the spec: lowercase category, lowercase token, exactly three digits
    [GeneratedRegex("^[a-z]+\\.[a-z0-9_]+_\\d{3}$")]
    private static partial Regex RuleIdPattern();
}
