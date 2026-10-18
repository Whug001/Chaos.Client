namespace Chaos.Client.ViewModel;

public enum TcgChatKind
{
    Opponent,
    Other,
    Notice
}

public readonly record struct TcgChatLine(string Text, TcgChatKind Kind);

/// <summary>Match chat: every whisper (incoming "[Name]: text", echoes "[Name]> text"), notices, and the players' chat for spectators.</summary>
public sealed class TcgMatchChat(string opponent)
{
    public const int MAX_LINES = 50;

    private readonly List<TcgChatLine> AllLines = [];

    public IReadOnlyList<TcgChatLine> Lines => AllLines;

    public bool Accept(string rawMessage, string displayText)
    {
        if (!rawMessage.StartsWith('['))
            return false;

        var end = rawMessage.IndexOf(']');

        if ((end <= 1) || (end + 1 >= rawMessage.Length) || (rawMessage[end + 1] is not (':' or '>')))
            return false;

        Add(displayText, KindOf(rawMessage[1..end]));

        return true;
    }

    public void AddNotice(string text) => Add(text, TcgChatKind.Notice);

    public void AddSpectatorLine(string from, string text) => Add($"[{from}]: {text}", KindOf(from));

    private TcgChatKind KindOf(string name)
        => string.Equals(name, opponent, StringComparison.OrdinalIgnoreCase) ? TcgChatKind.Opponent : TcgChatKind.Other;

    private void Add(string text, TcgChatKind kind)
    {
        AllLines.Add(new TcgChatLine(text, kind));

        if (AllLines.Count > MAX_LINES)
            AllLines.RemoveAt(0);
    }
}
