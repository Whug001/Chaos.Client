namespace Chaos.Client.ViewModel.College;

/// <summary>
///     A save with at most one in flight: the first save goes out at once, and a second save asked
///     for while it is pending (a logout or exit save landing on top of an earlier one) is remembered
///     and goes out when the first one answers, carrying the newest edits. Two saves are never on the
///     wire at once, so an older save can never land last and wipe newer edits.
/// </summary>
public sealed class PendingSave
{
    public bool AwaitingSave { get; private set; }

    public bool SaveQueued { get; private set; }

    public int SentVersion { get; private set; }

    public void Reset()
    {
        AwaitingSave = false;
        SaveQueued = false;
    }

    /// <summary>
    ///     True when the caller should send <paramref name="version" /> now; false when a save is
    ///     already in flight and this one was queued behind it instead.
    /// </summary>
    public bool TryBegin(int version)
    {
        if (AwaitingSave)
        {
            SaveQueued = true;

            return false;
        }

        AwaitingSave = true;
        SentVersion = version;

        return true;
    }

    /// <summary>
    ///     The in-flight save answered. True when a save queued behind it still has unsent edits
    ///     (newer than the answered save and not a refused version) and must go out as a follow-up.
    /// </summary>
    public bool TakeQueued(bool isDirty, int currentVersion, int refusedVersion)
    {
        AwaitingSave = false;

        var followUp = SaveQueued && isDirty && (currentVersion != refusedVersion);
        SaveQueued = false;

        return followUp;
    }
}
