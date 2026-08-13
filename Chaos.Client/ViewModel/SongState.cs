namespace Chaos.Client.ViewModel;

/// <summary>
///     What the client knows about the Bard's song. Harmony is never computed here - the server is authoritative
///     and this holds whatever it last sent.
/// </summary>
public sealed class SongState
{
    private readonly byte[] EnteredNotes = new byte[4];

    /// <summary>0 = no song playing. Matches the server's SongId enum.</summary>
    public byte SongId { get; private set; }

    /// <summary>Current Harmony, 0-100, as last reported by the server.</summary>
    public byte Harmony { get; private set; }

    /// <summary>The id of the outstanding call, or 0 if none.</summary>
    public ushort CallId { get; private set; }

    /// <summary>The four notes to repeat. Empty when no call is live.</summary>
    public byte[] ExpectedNotes { get; } = new byte[4];

    /// <summary>How many notes the player has entered for the live call.</summary>
    public int NotesEntered { get; private set; }

    /// <summary>Milliseconds left to answer. Counts down each frame while a call is live.</summary>
    public float RemainingMs { get; private set; }

    public bool IsPlaying => SongId != 0;
    public bool HasLiveCall => (CallId != 0) && (RemainingMs > 0f);

    /// <summary>The notes the player has entered, 0 for not yet entered.</summary>
    public ReadOnlySpan<byte> Entered => EnteredNotes;

    public void SetState(byte songId, byte harmony)
    {
        SongId = songId;
        Harmony = harmony;

        if (songId == 0)
            ClearCall();
    }

    public void BeginCall(ushort callId, byte n1, byte n2, byte n3, byte n4, ushort windowMs)
    {
        CallId = callId;
        ExpectedNotes[0] = n1;
        ExpectedNotes[1] = n2;
        ExpectedNotes[2] = n3;
        ExpectedNotes[3] = n4;
        Array.Clear(EnteredNotes);
        NotesEntered = 0;
        RemainingMs = windowMs;
    }

    /// <summary>
    ///     Records a note. Returns true when this was the fourth, meaning the answer should be sent.
    /// </summary>
    public bool EnterNote(byte note)
    {
        if (!HasLiveCall || (NotesEntered >= 4))
            return false;

        EnteredNotes[NotesEntered] = note;
        NotesEntered++;

        return NotesEntered == 4;
    }

    /// <summary>Copies the entered notes out for the answer packet.</summary>
    public (byte N1, byte N2, byte N3, byte N4) TakeAnswer()
        => (EnteredNotes[0], EnteredNotes[1], EnteredNotes[2], EnteredNotes[3]);

    public void ClearCall()
    {
        CallId = 0;
        NotesEntered = 0;
        RemainingMs = 0f;
        Array.Clear(EnteredNotes);
        Array.Clear(ExpectedNotes);
    }

    /// <summary>
    ///     Advances the live call's countdown by <paramref name="elapsedMs" />. If the window expires before all
    ///     four notes were entered, the call is cleared and the notes entered so far (0 for any not entered) are
    ///     returned so the caller can send a single <c>SongAnswer</c>. Returns null when no call expired this tick
    ///     (including when the four-note immediate-send path already cleared the call earlier this frame).
    /// </summary>
    public (ushort CallId, byte N1, byte N2, byte N3, byte N4)? Update(float elapsedMs)
    {
        if (RemainingMs <= 0f)
            return null;

        RemainingMs -= elapsedMs;

        if (RemainingMs > 0f)
            return null;

        //window expired before all four notes were entered — capture the call id and whatever
        //notes were entered (0 for the rest) so the caller can send a single SongAnswer, then
        //clear the call so this same expiry can never be reported again.
        var callId = CallId;
        var (n1, n2, n3, n4) = TakeAnswer();
        ClearCall();

        return (callId, n1, n2, n3, n4);
    }

    public void Reset()
    {
        SongId = 0;
        Harmony = 0;
        ClearCall();
    }
}
