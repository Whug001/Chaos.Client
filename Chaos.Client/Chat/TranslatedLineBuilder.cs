#region
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.Chat;

/// <summary>
///     Builds the text shown when a chat line's translation arrives: the original line's prefix, the translated body
///     through the reader's chat filter, and, for the chat log only, a tag naming the original language.
/// </summary>
public static class TranslatedLineBuilder
{
    public static (string ChatLog, string Bubble) Build(
        string originalLine,
        string translatedBody,
        IReadOnlyList<ChatTag> tags,
        ChatLanguage source,
        ChatLanguage reader,
        ChatFilterMode mode,
        FantasyDictionary fantasy)
    {
        var (prefix, _) = ChatDisplayBuilder.SplitPrefix(originalLine);
        var display = ChatDisplayBuilder.Build(prefix, translatedBody, tags, mode, fantasy).DisplayText;
        var tag = SourceTag(source, reader);

        //a channel line may open with a color code ("{=" plus one color byte); the tag goes after it so the color holds
        var insertAt = display.StartsWith("{=", StringComparison.Ordinal) && (display.Length >= 3) ? 3 : 0;

        return (display.Insert(insertAt, tag), display);
    }

    /// <summary>The chat log tag naming the line's original language: in Korean for a Korean reader, else a short English code.</summary>
    public static string SourceTag(ChatLanguage source, ChatLanguage reader)
        => reader == ChatLanguage.Korean
            ? source switch
            {
                ChatLanguage.Spanish => "[스페인어] ",
                ChatLanguage.French  => "[프랑스어] ",
                ChatLanguage.German  => "[독일어] ",
                ChatLanguage.Korean  => "[한국어] ",
                _                    => "[영어] "
            }
            : source switch
            {
                ChatLanguage.Korean  => "[Kor] ",
                ChatLanguage.Spanish => "[Spa] ",
                ChatLanguage.French  => "[Fre] ",
                ChatLanguage.German  => "[Ger] ",
                _                    => "[Eng] "
            };
}
