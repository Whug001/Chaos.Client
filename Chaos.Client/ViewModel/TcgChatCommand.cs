namespace Chaos.Client.ViewModel;

/// <summary>What the match chat box sends: a whisper (Target + Message), a local notice, or nothing.</summary>
public readonly record struct TcgChatSend(string? Target, string Message, string? Notice)
{
    public static TcgChatSend Nothing { get; } = new(null, string.Empty, null);
}

public static class TcgChatCommand
{
    public const string NO_WHISPERER = "No one has whispered you.";
    public const string PRACTICE = "Your opponent can't read chat.";

    public static TcgChatSend Parse(string text, string opponent, IReadOnlyList<string> recentSenders, bool practice)
    {
        text = text.Trim();

        if (text.Length == 0)
            return TcgChatSend.Nothing;

        if (IsCommand(text, "/r", out var reply))
        {
            if (reply.Length == 0)
                return TcgChatSend.Nothing;

            return recentSenders.Count == 0 ? new TcgChatSend(null, string.Empty, NO_WHISPERER) : new TcgChatSend(recentSenders[0], reply, null);
        }

        if (IsCommand(text, "/w", out var rest))
        {
            var space = rest.IndexOf(' ');

            return space <= 0 ? TcgChatSend.Nothing : new TcgChatSend(rest[..space], rest[(space + 1)..].Trim(), null);
        }

        return practice ? new TcgChatSend(null, string.Empty, PRACTICE) : new TcgChatSend(opponent, text, null);
    }

    private static bool IsCommand(string text, string command, out string rest)
    {
        if (text.Equals(command, StringComparison.OrdinalIgnoreCase))
        {
            rest = string.Empty;

            return true;
        }

        if (text.StartsWith(command + " ", StringComparison.OrdinalIgnoreCase))
        {
            rest = text[(command.Length + 1)..].Trim();

            return true;
        }

        rest = string.Empty;

        return false;
    }
}
