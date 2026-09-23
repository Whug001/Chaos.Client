#region
using System.Diagnostics;
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.Chat;

/// <summary>
///     The display string every inbound chat path shares, with the pre-transform original and its filter tags
///     beside it for debug.
/// </summary>
public readonly record struct ChatDisplayResult(string DisplayText, string OriginalText, IReadOnlyList<ChatTag> Tags);

/// <summary>
///     Single client render boundary for inbound chat. Every inbound chat path (public, whisper, group, guild)
///     transforms exactly once here, before display: the body transforms via <see cref="ChatSpanApplier" />,
///     then this unit reattaches the sender prefix. The chat log, orange-bar history, chat bubbles, and poker
///     bubbles all read <see cref="ChatDisplayResult.DisplayText" />; the original plus tags are retained beside
///     it. Tag spans are BODY-relative (the wire prefix is split off first); a span outside the body is dropped
///     with a single log line, never a throw, and missing tags show the original.
/// </summary>
public static class ChatDisplayBuilder
{
    /// <summary>
    ///     Transforms <paramref name="originalBody" /> under <paramref name="mode" /> and reattaches
    ///     <paramref name="senderPrefix" /> (which already carries its trailing separator, e.g. "Bob: ").
    ///     For <see cref="ChatFilterMode.Hide" /> the display is the prefix plus the bare marker; with no usable
    ///     tags every mode shows the original.
    /// </summary>
    public static ChatDisplayResult Build(
        string? senderPrefix,
        string? originalBody,
        IReadOnlyList<ChatTag>? tags,
        ChatFilterMode mode = ChatFilterMode.Unfiltered,
        FantasyDictionary? fantasy = null,
        Action<string>? log = null)
    {
        var prefix = senderPrefix ?? string.Empty;
        var body = originalBody ?? string.Empty;
        IReadOnlyList<ChatTag> received = tags ?? [];
        var map = fantasy ?? FantasyDictionary.Empty;
        log ??= static message => Debug.WriteLine($"[ChatDisplayBuilder] {message}");

        var valid = received;

        if (received.Count != 0)
        {
            var dropped = 0;

            foreach (var tag in received)
                if (!IsBodySpan(tag, body.Length))
                    dropped++;

            //a span outside the body is dropped (the applier would only clamp it) and reported once, so one
            //stale index can neither corrupt the line nor spam the log; the surviving spans still transform
            if (dropped != 0)
            {
                log($"{dropped} filter span(s) fall outside a {body.Length}-character body; showing the original there");

                var kept = new List<ChatTag>(received.Count - dropped);

                foreach (var tag in received)
                    if (IsBodySpan(tag, body.Length))
                        kept.Add(tag);

                valid = kept;
            }
        }

        var displayBody = ChatSpanApplier.Apply(body, valid, mode, map);

        return new ChatDisplayResult(string.Concat(prefix, displayBody), string.Concat(prefix, body), received);
    }

    /// <summary>
    ///     Public chat ("Name: body" / "Name! body", plus channel overflow through
    ///     <c>DisplayPublicMessage</c>): splits the sender prefix, transforms the body once, reattaches it.
    /// </summary>
    public static ChatDisplayResult BuildPublic(
        string message,
        IReadOnlyList<ChatTag>? tags,
        ChatFilterMode mode = ChatFilterMode.Unfiltered,
        FantasyDictionary? fantasy = null,
        Action<string>? log = null)
    {
        var (prefix, body) = SplitPrefix(message);

        return Build(prefix, body, tags, mode, fantasy, log);
    }

    /// <summary>
    ///     Whispers ("[name]: body" direct, "[name]> body" echo): the same single transform.
    /// </summary>
    public static ChatDisplayResult BuildWhisper(
        string message,
        IReadOnlyList<ChatTag>? tags,
        ChatFilterMode mode = ChatFilterMode.Unfiltered,
        FantasyDictionary? fantasy = null,
        Action<string>? log = null)
    {
        var (prefix, body) = SplitPrefix(message);

        return Build(prefix, body, tags, mode, fantasy, log);
    }

    /// <summary>
    ///     Group and guild share the "[!channel] Name: body" wire shape (plus an optional color prefix), so both
    ///     branches call this.
    /// </summary>
    public static ChatDisplayResult BuildGroupChat(
        string message,
        IReadOnlyList<ChatTag>? tags,
        ChatFilterMode mode = ChatFilterMode.Unfiltered,
        FantasyDictionary? fantasy = null,
        Action<string>? log = null)
    {
        var (prefix, body) = SplitPrefix(message);

        return Build(prefix, body, tags, mode, fantasy, log);
    }

    private static bool IsBodySpan(ChatTag tag, int bodyLength)
        => (tag.Start >= 0)
            && (tag.Length > 0)
            && (tag.Start < bodyLength)
            && (tag.Length <= bodyLength - tag.Start);

    private static (string Prefix, string Body) SplitPrefix(string message)
    {
        //whispers lead with a bracketed name: split right after "]:" / "]>" so body text holding those
        //sequences can never mis-split
        if (message.StartsWith('['))
        {
            var close = message.IndexOf(']');

            if ((close >= 1)
                && (close + 3 <= message.Length)
                && (message[close + 1] is ':' or '>')
                && (message[close + 2] == ' '))
                return (message[..(close + 3)], message[(close + 3)..]);
        }

        //public, group, and guild prefixes all end at the first ": " / "! " (a color byte can never form one:
        //it is always followed by '['); a message with neither separator has no prefix to reattach
        var say = message.IndexOf(": ", StringComparison.Ordinal);
        var shout = message.IndexOf("! ", StringComparison.Ordinal);

        var at = (say, shout) switch
        {
            (-1, -1) => -1,
            (-1, _)  => shout,
            (_, -1)  => say,
            _        => Math.Min(say, shout)
        };

        return at < 0 ? (string.Empty, message) : (message[..(at + 2)], message[(at + 2)..]);
    }
}
