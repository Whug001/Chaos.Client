namespace Chaos.Client.ViewModel;

/// <summary>Match chat: whispers between the two players only. Incoming lines are "[Name]: text", echoes are "[Name]> text".</summary>
public sealed class TcgMatchChat(string opponent)
{
    public const int MAX_LINES = 50;

    private readonly List<string> AllLines = [];

    public IReadOnlyList<string> Lines => AllLines;

    public bool Accept(string rawMessage, string displayText)
    {
        if (!rawMessage.StartsWith('['))
            return false;

        var end = rawMessage.IndexOf(']');

        if ((end <= 1) || (end + 1 >= rawMessage.Length) || (rawMessage[end + 1] is not (':' or '>')))
            return false;

        if (!string.Equals(rawMessage[1..end], opponent, StringComparison.OrdinalIgnoreCase))
            return false;

        AllLines.Add(displayText);

        if (AllLines.Count > MAX_LINES)
            AllLines.RemoveAt(0);

        return true;
    }
}
