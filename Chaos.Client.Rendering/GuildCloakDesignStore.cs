#region
using System.Diagnostics.CodeAnalysis;
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.Rendering;

/// <summary>
///     Guild cloak designs by id. Positive ids come from the server; the client asks for each one at most once every
///     <see cref="REQUEST_RETRY_MS" />. Negative ids are designs painted on this client (the editor's draft, a design under
///     review): every change gets a new id, so the preview never reuses a drawing of an older version. Game thread only.
/// </summary>
public sealed class GuildCloakDesignStore
{
    public const long REQUEST_RETRY_MS = 10_000;

    private readonly Dictionary<int, GuildCloakDesign> Designs = [];
    private readonly Dictionary<int, long> RequestedAtMs = [];
    private int NextLocalId = -1;

    public void Set(int designId, GuildCloakDesign design)
    {
        Designs[designId] = design;
        RequestedAtMs.Remove(designId);
    }

    /// <summary>
    ///     Forgets every design and pending request, for a new session (reconnecting, or a server whose storage was reset).
    ///     Local ids keep counting down from where they left off, so a design painted before the clear and one painted
    ///     after never collide.
    /// </summary>
    public void Clear()
    {
        Designs.Clear();
        RequestedAtMs.Clear();
    }

    /// <summary>Stores a design painted on this client under a new negative id, and drops the local design it replaces.</summary>
    public int SetLocal(int replacedId, GuildCloakDesign design)
    {
        if (replacedId < 0)
            Designs.Remove(replacedId);

        var id = NextLocalId--;
        Designs[id] = design;

        return id;
    }

    /// <summary>True when a server design is missing and was not asked for in the last <see cref="REQUEST_RETRY_MS" />.</summary>
    public bool ShouldRequest(int designId, long nowMs)
    {
        if ((designId <= 0) || Designs.ContainsKey(designId))
            return false;

        if (RequestedAtMs.TryGetValue(designId, out var askedAt) && ((nowMs - askedAt) < REQUEST_RETRY_MS))
            return false;

        RequestedAtMs[designId] = nowMs;

        return true;
    }

    public bool TryGet(int designId, [MaybeNullWhen(false)] out GuildCloakDesign design)
    {
        design = null;

        return (designId != 0) && Designs.TryGetValue(designId, out design);
    }
}
