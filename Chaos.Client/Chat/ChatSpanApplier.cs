#region
using System.Text;
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.Chat;

/// <summary>
///     Pure client-side transform for tagged chat spans. Tags index the message BODY (UTF-16 indices, no sender
///     prefix); the caller passes the body plus its tags and reattaches any prefix itself (Task 5).
///     Out-of-range spans are clamped and empty ones skipped, so stale indices can never break rendering.
///     Unknown rule IDs always mask to <c>****</c> and are never shown raw (spec safety rule).
/// </summary>
public static class ChatSpanApplier
{
    /// <summary>The whole-body replacement for <see cref="ChatFilterMode.Hide" />; the sender prefix stays.</summary>
    public const string HiddenMarker = "[Message contains profanity and was removed.]";

    private const string MASK = "****";

    /// <summary>
    ///     Returns the display body for <paramref name="body" /> under <paramref name="mode" />. No IO: the
    ///     caller loads <see cref="FantasyDictionary.Default" /> once and reuses it for every message.
    /// </summary>
    public static string Apply(
        string body,
        IReadOnlyList<ChatTag> tags,
        ChatFilterMode mode,
        FantasyDictionary fantasy)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentNullException.ThrowIfNull(fantasy);

        //unfiltered never transforms; with no tags every mode is an identity (Hide included: there is
        //nothing to hide, so the caller still gets the body to prefix as usual)
        if ((mode == ChatFilterMode.Unfiltered) || (tags.Count == 0))
            return body;

        //Hide swaps the whole body for the marker; the render boundary keeps the sender prefix, so a
        //hidden public line still reads "Name: [Message contains profanity and was removed.]"
        if (mode == ChatFilterMode.Hide)
            return HiddenMarker;

        //end-to-start so an earlier replacement never shifts the indices of a later one
        var ordered = tags.OrderByDescending(tag => tag.Start)
                          .ToArray();

        var sb = new StringBuilder(body);

        foreach (var tag in ordered)
        {
            var start = Math.Max(0, tag.Start);
            var end = Math.Min(body.Length, tag.Start + tag.Length);

            if (start >= end)
                continue;

            string replacement;

            if (mode == ChatFilterMode.Censored)
                replacement = MASK;
            else if (!fantasy.TryGetSubstitution(tag.RuleId, out var word) || string.IsNullOrEmpty(word))
                replacement = MASK;
            else
                replacement = CopyCase(body.Substring(start, end - start), word);

            sb.Remove(start, end - start);
            sb.Insert(start, replacement);
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Copies the source span's casing onto the substitution: all-caps shouts it, leading-caps
    ///     capitalizes it, anything else keeps the map's own casing.
    /// </summary>
    private static string CopyCase(string source, string word)
    {
        if (source.Any(char.IsLetter) && source.Where(char.IsLetter)
                                               .All(char.IsUpper))
            return word.ToUpperInvariant();

        if (char.IsUpper(source[0]))
            return string.Concat(char.ToUpperInvariant(word[0]), word[1..]);

        return word;
    }
}
