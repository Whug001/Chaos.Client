namespace Chaos.Client.ViewModel.College;

/// <summary>
///     When the writer saves a changed draft on its own: <see cref="INTERVAL" /> after the first change that hasn't been
///     sent, so a closed game or a lost connection costs at most that much writing, and the server's save throttle is
///     never near.
/// </summary>
public sealed class DraftAutosave
{
    public static readonly TimeSpan INTERVAL = TimeSpan.FromSeconds(60);

    private DateTime? ChangedSince;

    /// <summary>True when the caller should save now; the wait then starts again at the next unsaved change.</summary>
    public bool IsDue(bool dirty, DateTime now)
    {
        if (!dirty)
        {
            ChangedSince = null;

            return false;
        }

        ChangedSince ??= now;

        if ((now - ChangedSince.Value) < INTERVAL)
            return false;

        ChangedSince = null;

        return true;
    }
}
