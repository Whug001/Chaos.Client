using Chaos.DarkAges.Definitions;

namespace Chaos.Client.ViewModel.College;

/// <summary>
///     A judge's or the Director's unsaved tier and comment per entry, kept for the session, so Prev, Next, Close or a piece
///     shown to the class doesn't throw them away.
/// </summary>
public sealed class VoteDrafts
{
    private readonly Dictionary<(CollegePieceContext Context, int Id), (byte Tier, string Comment)> Drafts = new();

    /// <summary>The reader leaves an entry: what differs from the saved vote is kept, and a vote left as saved needs nothing.</summary>
    public void Leave(CollegePieceContext context, int id, byte tier, string comment, byte savedTier, string savedComment)
    {
        if ((tier == savedTier) && (comment == savedComment))
            Drafts.Remove((context, id));
        else
            Drafts[(context, id)] = (tier, comment);
    }

    /// <summary>What the reader shows for an entry: the kept draft, or else the saved vote.</summary>
    public (byte Tier, string Comment) Restore(CollegePieceContext context, int id, byte savedTier, string savedComment)
        => Drafts.TryGetValue((context, id), out var draft) ? draft : (savedTier, savedComment);

    public void Saved(CollegePieceContext context, int id) => Drafts.Remove((context, id));
}
