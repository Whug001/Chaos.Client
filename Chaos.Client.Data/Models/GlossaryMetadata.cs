#region
using System.Collections.Frozen;
using DALib.Data;
#endregion

namespace Chaos.Client.Data.Models;

/// <summary>
///     Parsed keyword explanations from the Glossary metadata file. A keyword is a word that appears in game text -- a tag
///     such as "Fire" or "Black Dugon", or a status such as "Beag Suain" -- and its explanation is what a player sees when
///     they hover it.
/// </summary>
public sealed class GlossaryMetadata
{
    private readonly FrozenDictionary<string, string> Explanations;

    /// <summary>
    ///     Every keyword, in no particular order.
    /// </summary>
    public IReadOnlyList<string> Keywords => Explanations.Keys;

    private GlossaryMetadata(FrozenDictionary<string, string> explanations) => Explanations = explanations;

    /// <summary>
    ///     Parses a Glossary MetaFile. Each entry is a keyword whose single property is its explanation.
    /// </summary>
    /// <remarks>
    ///     Keyed ordinally, because the case of a keyword is load-bearing: game text capitalizes a word only where it means
    ///     the term ("Applies Burn") and leaves it lowercase as prose ("strikes them with fire"). Two entries differing only
    ///     in case are two terms, not one.
    /// </remarks>
    public static GlossaryMetadata Parse(MetaFile metaFile)
    {
        var explanations = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var entry in metaFile)
        {
            //an empty key would match every position in every line, forever
            if (string.IsNullOrWhiteSpace(entry.Key) || (entry.Properties.Count == 0))
                continue;

            explanations[entry.Key] = entry.Properties[0];
        }

        return new GlossaryMetadata(explanations.ToFrozenDictionary(StringComparer.Ordinal));
    }

    public bool TryGetExplanation(string keyword, out string? explanation) => Explanations.TryGetValue(keyword, out explanation);
}
