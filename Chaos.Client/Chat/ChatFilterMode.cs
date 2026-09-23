namespace Chaos.Client.Chat;

/// <summary>
///     How the chat render boundary displays a message that carries server filter tags. The detector runs
///     server-side (Tasks 1-3); the client only chooses how to paint the tagged spans.
/// </summary>
public enum ChatFilterMode
{
    /// <summary>Show the original text untouched.</summary>
    Unfiltered,

    /// <summary>Swap each tagged span for its fantasy-map word (unknown IDs mask).</summary>
    Fantasy,

    /// <summary>Write <c>****</c> over each tagged span.</summary>
    Censored,

    /// <summary>Replace the whole body with <see cref="ChatSpanApplier.HiddenMarker" />; the caller keeps the sender prefix.</summary>
    Hide
}
