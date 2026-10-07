#region
using Chaos.Client.Rendering.Definitions;
using Chaos.DarkAges.Definitions;
#endregion

namespace Chaos.Client.Systems;

/// <summary>
///     What tab targeting needs to know about one entity on screen. Built from a <c>WorldEntity</c> each time the
///     candidates are gathered, so the selector itself needs no world state and can be tested on its own.
/// </summary>
public readonly record struct TargetCandidate(
    uint Id,
    string? Name,
    ClientEntityType Type,
    CreatureType CreatureType,
    bool IsDead,
    int TileX,
    int TileY);

/// <summary>
///     The tab-targeting selection: which entity the armed spell will be cast on when the player presses Enter. Holds
///     only the selected id; the candidate list is gathered fresh on every key press and frame, since entities move.
/// </summary>
/// <remarks>
///     Eligibility is the client's reading of the server's <c>TargetFilter</c>. Monsters are hostile, aislings are
///     friendly and merchants neutral. PvP isn't modelled, so another aisling is never hostile here, and pet flags
///     can't be checked (the client doesn't know who owns a pet) so they are ignored.
/// </remarks>
public sealed class TabTargeting
{
    /// <summary>The selected entity, or null when nothing is selected.</summary>
    public uint? SelectedId { get; private set; }

    /// <summary>
    ///     The entity the player last cast an offensive spell on (see <see cref="IsOffensive" />), by click or by key.
    ///     Arming the next offensive spell goes back to it while it is still a living candidate.
    /// </summary>
    public uint? LastOffensiveTargetId { get; private set; }

    /// <summary>
    ///     The entity the player last cast a supportive spell on (a heal, a cure). Kept apart from the offensive one, so
    ///     suain on a monster and then ard ioc goes back to the ally, and the next suain back to the monster.
    /// </summary>
    public uint? LastSupportiveTargetId { get; private set; }

    /// <summary>Drops the selection. The remembered last target stays.</summary>
    public void Clear() => SelectedId = null;

    /// <summary>
    ///     The tile a ground-targeted spell is aimed at from the keyboard, or null while the mouse aims it. Set when a ground
    ///     spell is armed and moved by the arrow keys.
    /// </summary>
    public (int X, int Y)? AimTile { get; private set; }

    /// <summary>Puts the keyboard aim on a tile.</summary>
    public void Aim(int x, int y) => AimTile = (x, y);

    /// <summary>Hands the ground aim back to the mouse.</summary>
    public void ClearAim() => AimTile = null;

    /// <summary>
    ///     Moves the keyboard aim one step, clamped to the map. Does nothing while the mouse is aiming (no aim tile).
    /// </summary>
    public void MoveAim(int dx, int dy, int mapWidth, int mapHeight)
    {
        if (AimTile is not { } tile)
            return;

        AimTile = (Math.Clamp(tile.X + dx, 0, mapWidth - 1), Math.Clamp(tile.Y + dy, 0, mapHeight - 1));
    }

    /// <summary>
    ///     Remembers the entity a spell was just cast on, as the last offensive or last supportive target by the spell's
    ///     kind, for <see cref="SelectInitial" />.
    /// </summary>
    public void Remember(uint id, bool offensive)
    {
        if (offensive)
            LastOffensiveTargetId = id;
        else
            LastSupportiveTargetId = id;
    }

    /// <summary>
    ///     The pick when a spell is armed. An offensive spell takes its last target while that is a living candidate, else
    ///     the nearest enemy, else the player (so the box still starts somewhere with no enemy on screen). A supportive spell
    ///     takes its own last target while that lives, else the player, else the nearest candidate (a revive's nearest dead
    ///     ally, say). The two kinds remember separately (see <see cref="Remember" />).
    /// </summary>
    public void SelectInitial(IReadOnlyList<TargetCandidate> candidates, bool offensive, uint selfId)
    {
        var last = offensive ? LastOffensiveTargetId : LastSupportiveTargetId;

        if (FindLiving(candidates, last) is { } remembered)
        {
            SelectedId = remembered;

            return;
        }

        if (offensive)
        {
            foreach (var candidate in candidates)
                if (IsHostile(candidate))
                {
                    SelectedId = candidate.Id;

                    return;
                }

            //no enemy on screen: the player, even though the spell can't hit them, so there is still a box to start from
            SelectedId = selfId;

            return;
        }

        if (IndexOf(candidates, selfId) >= 0)
        {
            SelectedId = selfId;

            return;
        }

        SelectFirst(candidates);
    }

    /// <summary>
    ///     Keeps the selection while it is still a candidate (or still the offensive fallback onto the player, while no enemy
    ///     is on screen), and otherwise picks again as <see cref="SelectInitial" /> would. So the box stays on its target
    ///     until the target leaves, then moves to the next best one.
    /// </summary>
    public void Retarget(IReadOnlyList<TargetCandidate> candidates, bool offensive, uint selfId)
    {
        if (IndexOf(candidates, SelectedId) >= 0)
            return;

        if (offensive && (SelectedId == selfId) && !candidates.Any(IsHostile))
            return;

        SelectInitial(candidates, offensive, selfId);
    }

    private static uint? FindLiving(IReadOnlyList<TargetCandidate> candidates, uint? id)
    {
        foreach (var candidate in candidates)
            if ((candidate.Id == id) && !candidate.IsDead)
                return candidate.Id;

        return null;
    }

    /// <summary>
    ///     Whether a spell with this filter is offensive (suain, cradh, the wizard spells): its filter names hostile targets.
    ///     Everything else is supportive (heals, cures, ao spells, revives): their filters are AliveOnly, FriendlyOnly,
    ///     GroupOnly and the like. A spell whose filter never arrived (None) counts as offensive.
    /// </summary>
    public static bool IsOffensive(SpellTargetFilter filter)
        => (filter == SpellTargetFilter.None)
           || ((filter & (SpellTargetFilter.HostileOnly | SpellTargetFilter.NonFriendlyOnly | SpellTargetFilter.MonstersOnly)) != 0);

    /// <summary>Selects an entity by id, or clears the selection when given null.</summary>
    public void Select(uint? id) => SelectedId = id;

    /// <summary>Selects the first candidate (the nearest, for a list from <see cref="Candidates" />), or nothing.</summary>
    public void SelectFirst(IReadOnlyList<TargetCandidate> candidates)
        => SelectedId = candidates.Count > 0 ? candidates[0].Id : null;

    /// <summary>Selects the last candidate (the furthest, for a list from <see cref="Candidates" />), or nothing.</summary>
    public void SelectLast(IReadOnlyList<TargetCandidate> candidates)
        => SelectedId = candidates.Count > 0 ? candidates[^1].Id : null;

    /// <summary>
    ///     Moves the selection <paramref name="step" /> places along the list, wrapping at either end. With nothing
    ///     selected (or the selection gone from the list), forward starts at the first candidate and back at the last.
    /// </summary>
    public void Cycle(IReadOnlyList<TargetCandidate> candidates, int step)
    {
        if (candidates.Count == 0)
        {
            SelectedId = null;

            return;
        }

        var index = IndexOf(candidates, SelectedId);

        if (index < 0)
        {
            SelectedId = step >= 0 ? candidates[0].Id : candidates[^1].Id;

            return;
        }

        var next = ((index + step) % candidates.Count + candidates.Count) % candidates.Count;
        SelectedId = candidates[next].Id;
    }

    /// <summary>Drops the selection when it is no longer one of the candidates: it walked off screen, died, or vanished.</summary>
    private static int IndexOf(IReadOnlyList<TargetCandidate> candidates, uint? id)
    {
        if (id is null)
            return -1;

        for (var i = 0; i < candidates.Count; i++)
            if (candidates[i].Id == id)
                return i;

        return -1;
    }

    /// <summary>
    ///     The entities a spell with <paramref name="filter" /> can be cast on, nearest first (by tile distance from the
    ///     player, ties broken by id so the order holds still between frames).
    /// </summary>
    /// <param name="filter">The spell's target filter. None allows any creature or aisling.</param>
    /// <param name="entities">The entities to choose from (normally those in the viewport).</param>
    /// <param name="selfId">The player's own entity id.</param>
    /// <param name="selfX">The player's tile X.</param>
    /// <param name="selfY">The player's tile Y.</param>
    /// <param name="groupNames">The names of the player's group members, not counting the player.</param>
    public static List<TargetCandidate> Candidates(
        SpellTargetFilter filter,
        IEnumerable<TargetCandidate> entities,
        uint selfId,
        int selfX,
        int selfY,
        IReadOnlyCollection<string> groupNames)
    {
        var list = new List<TargetCandidate>();

        //a supportive spell (a heal, a cure) is only AliveOnly on the server, so a monster passes its filter; tabbing onto
        //monsters with a heal is never wanted, so they're left out here. A click can still put one on a monster
        var supportive = !IsOffensive(filter);

        foreach (var entity in entities)
            if (IsEligible(filter, entity, selfId, groupNames) && !(supportive && IsHostile(entity)))
                list.Add(entity);

        list.Sort(
            (a, b) =>
            {
                var byDistance = Distance(a, selfX, selfY)
                    .CompareTo(Distance(b, selfX, selfY));

                return byDistance != 0 ? byDistance : a.Id.CompareTo(b.Id);
            });

        return list;
    }

    /// <summary>
    ///     The candidate that belongs to the <paramref name="memberIndex" />th group panel (0 = the top panel), or null when
    ///     there is no such member or they aren't a candidate (off screen, or not a target the spell allows).
    /// </summary>
    public static TargetCandidate? GroupMember(
        IReadOnlyList<TargetCandidate> candidates,
        IReadOnlyList<string> orderedGroupNames,
        int memberIndex)
    {
        if ((memberIndex < 0) || (memberIndex >= orderedGroupNames.Count))
            return null;

        var name = orderedGroupNames[memberIndex];

        foreach (var candidate in candidates)
            if ((candidate.Type == ClientEntityType.Aisling) && string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
                return candidate;

        return null;
    }

    /// <summary>The hostile candidates only, keeping their order.</summary>
    public static List<TargetCandidate> Hostiles(IReadOnlyList<TargetCandidate> candidates)
        => candidates.Where(IsHostile)
                     .ToList();

    /// <summary>Whether a spell with <paramref name="filter" /> can be cast on <paramref name="target" />.</summary>
    public static bool IsEligible(
        SpellTargetFilter filter,
        TargetCandidate target,
        uint selfId,
        IReadOnlyCollection<string> groupNames)
    {
        //ground items and creatures you can't touch are never spell targets
        if (target.Type == ClientEntityType.GroundItem)
            return false;

        if ((target.Type == ClientEntityType.Creature) && (target.CreatureType == CreatureType.Intangible))
            return false;

        var isSelf = target.Id == selfId;
        var isAisling = target.Type == ClientEntityType.Aisling;
        var isMerchant = !isAisling && (target.CreatureType == CreatureType.Merchant);
        var isMonster = !isAisling && !isMerchant;
        var isHostile = IsHostile(target);
        var isFriendly = isAisling;

        var isGroup = isSelf
                      || (isAisling
                          && (target.Name is not null)
                          && groupNames.Contains(target.Name, StringComparer.OrdinalIgnoreCase));

        foreach (var flag in Enum.GetValues<SpellTargetFilter>())
        {
            if ((flag == SpellTargetFilter.None) || !filter.HasFlag(flag))
                continue;

            var passes = flag switch
            {
                SpellTargetFilter.FriendlyOnly     => isFriendly,
                SpellTargetFilter.HostileOnly      => isHostile,
                SpellTargetFilter.NeutralOnly      => !isFriendly && !isHostile,
                SpellTargetFilter.NonFriendlyOnly  => !isFriendly,
                SpellTargetFilter.NonHostileOnly   => !isHostile,
                SpellTargetFilter.NonNeutralOnly   => isFriendly || isHostile,
                SpellTargetFilter.AliveOnly        => !target.IsDead,
                SpellTargetFilter.DeadOnly         => target.IsDead,
                SpellTargetFilter.AislingsOnly     => isAisling,
                SpellTargetFilter.MonstersOnly     => isMonster,
                SpellTargetFilter.MerchantsOnly    => isMerchant,
                SpellTargetFilter.NonAislingsOnly  => !isAisling,
                SpellTargetFilter.NonMonstersOnly  => !isMonster,
                SpellTargetFilter.NonMerchantsOnly => !isMerchant,
                SpellTargetFilter.SelfOnly         => isSelf,
                SpellTargetFilter.OthersOnly       => !isSelf,
                SpellTargetFilter.GroupOnly        => isGroup,

                //pet ownership isn't visible to the client
                _ => true
            };

            if (!passes)
                return false;
        }

        return true;
    }

    /// <summary>A monster: any creature that is neither a merchant nor an aisling.</summary>
    public static bool IsHostile(TargetCandidate target)
        => (target.Type == ClientEntityType.Creature) && target.CreatureType is not (CreatureType.Merchant or CreatureType.Aisling);

    private static int Distance(TargetCandidate target, int x, int y) => Math.Abs(target.TileX - x) + Math.Abs(target.TileY - y);
}
