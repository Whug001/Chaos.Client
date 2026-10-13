using Chaos.DarkAges.Definitions;

namespace Chaos.Client.ViewModel.College;

/// <summary>When the board editor sends: only a changed board, at most every 600 ms.</summary>
public sealed class BoardSync
{
    private long LastSendMs = long.MinValue / 2;
    private int SentVersion;

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
