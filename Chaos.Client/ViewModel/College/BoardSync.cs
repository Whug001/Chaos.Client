using Chaos.DarkAges.Definitions;

namespace Chaos.Client.ViewModel.College;

/// <summary>
///     When the board editor sends: only a changed board, at most every 600 ms. It also remembers the last few boards the
///     editor sent or loaded, so their echoes from the server don't throw away newer strokes.
/// </summary>
public sealed class BoardSync
{
    private const int KNOWN_BOARDS = 8;

    private readonly Queue<byte[]> Known = new();
    private long LastSendMs = long.MinValue / 2;
    private int SentVersion;

    public bool IsKnown(ReadOnlySpan<byte> data)
    {
        foreach (var known in Known)
            if (data.SequenceEqual(known))
                return true;

        return false;
    }

    public void Remember(byte[] data)
    {
        if (IsKnown(data))
            return;

        if (Known.Count == KNOWN_BOARDS)
            Known.Dequeue();

        Known.Enqueue(data);
    }

    public bool Unsent(int version) => version != SentVersion;

    public bool Due(int version, long nowMs) => Unsent(version) && ((nowMs - LastSendMs) >= CollegeProtocol.BOARD_SEND_MS);

    public void MarkSent(int version, long nowMs)
    {
        SentVersion = version;
        LastSendMs = nowMs;
    }

    /// <summary>Counts <paramref name="version" /> as sent, for a board that came from the server.</summary>
    public void Reset(int version) => SentVersion = version;
}
