#region
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>
///     The squad being edited: positions, the picked-up monster, the bench and the set bonuses.
///     Tile rules (7 by 3) and change tracking live here; species, protected and slot rules are checked
///     against <see cref="TamerStableState" /> in <see cref="CanPlace" /> and <see cref="ToAction" />.
/// </summary>
public sealed class TamerSquadDraft
{
    public const int COLUMNS = 7;
    public const int ROWS = 3;

    private readonly List<TamerDisplaySquadMember> MemberList;
    private readonly List<TamerDisplaySquadMember> OriginalList;

    public TamerSquadDraft(IReadOnlyList<TamerDisplaySquadMember> squad)
    {
        MemberList = (squad ?? [])
            .Where(member => member is not null)
            .ToList();
        OriginalList = MemberList.ToList();
    }

    public Guid? Picked { get; private set; }

    public IReadOnlyList<TamerDisplaySquadMember> Members => MemberList;

    public int SlotsUsed => MemberList.Count;

    public bool IsChanged => !SameMembers(MemberList, OriginalList);

    public void Pick(Guid monsterId)
    {
        if (monsterId == Guid.Empty)
            return;

        Picked = monsterId;
    }

    /// <summary>Places the pick: moves it, swaps it with the occupant, or puts a bench monster on the tile.</summary>
    public void PlaceAt(int column, int row)
    {
        if (Picked is not { } picked)
            return;

        if ((column < 0) || (column >= COLUMNS) || (row < 0) || (row >= ROWS))
            return;

        var pickedIndex = MemberList.FindIndex(member => member.MonsterId == picked);
        var targetIndex = MemberList.FindIndex(member => (member.Column == column) && (member.Row == row));

        if ((targetIndex >= 0) && (MemberList[targetIndex].MonsterId == picked))
        {
            Picked = null;
            return;
        }

        if (pickedIndex < 0)
        {
            if (targetIndex >= 0)
                return;

            MemberList.Add(new TamerDisplaySquadMember(picked, (byte)column, (byte)row));
            Picked = null;
            return;
        }

        if (targetIndex >= 0)
        {
            var occupant = MemberList[targetIndex];
            var pickedMember = MemberList[pickedIndex];
            MemberList[targetIndex] = pickedMember with { Column = (byte)column, Row = (byte)row };
            MemberList[pickedIndex] = occupant with { Column = pickedMember.Column, Row = pickedMember.Row };
            Picked = null;
            return;
        }

        MemberList[pickedIndex] = MemberList[pickedIndex] with { Column = (byte)column, Row = (byte)row };
        Picked = null;
    }

    /// <summary>Returns the pick to the bench.</summary>
    public void Remove()
    {
        if (Picked is not { } picked)
            return;

        MemberList.RemoveAll(member => member.MonsterId == picked);
        Picked = null;
    }

    public void CancelPick() => Picked = null;

    public IReadOnlyList<TamerMonsterView> Bench(TamerStableState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var placed = new HashSet<Guid>(MemberList.Select(member => member.MonsterId));

        return state.Monsters.Where(monster => !placed.Contains(monster.Id)).ToList();
    }

    /// <summary>True when <paramref name="monster" /> may be added to the draft, or why not.</summary>
    public bool CanPlace(TamerStableState state, TamerMonsterView? monster, out string? reason)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (monster is null)
        {
            reason = "That creature is no longer in your stable.";
            return false;
        }

        if (MemberList.Any(member => member.MonsterId == monster.Id))
        {
            reason = "That creature is already in the squad.";
            return false;
        }

        if (state.Monsters.All(held => held.Id != monster.Id))
        {
            reason = "That creature is no longer in your stable.";
            return false;
        }

        if (monster.Protected || (state.SpeciesOf(monster) is null))
        {
            reason = "That creature's species is unknown.";
            return false;
        }

        if (MemberList.Count >= state.SquadSlots)
        {
            reason = TamerStableText.SlotsLine(MemberList.Count, state.SquadSlots);
            return false;
        }

        var speciesKey = monster.SpeciesKey;

        foreach (var member in MemberList)
        {
            var held = state.Monsters.FirstOrDefault(current => current.Id == member.MonsterId);

            if ((held is not null) && held.SpeciesKey.Equals(speciesKey, StringComparison.OrdinalIgnoreCase))
            {
                reason = "Two creatures of one kind cannot share a squad.";
                return false;
            }
        }

        reason = null;
        return true;
    }

    public void Revert()
    {
        MemberList.Clear();
        MemberList.AddRange(OriginalList);
        Picked = null;
    }

    /// <summary>Drops from the draft only monsters that no longer exist in the snapshot.</summary>
    public void Reconcile(TamerStableState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var existing = new HashSet<Guid>(state.Monsters.Select(monster => monster.Id));

        MemberList.RemoveAll(member => !existing.Contains(member.MonsterId));
        OriginalList.RemoveAll(member => !existing.Contains(member.MonsterId));

        if ((Picked is { } picked) && !existing.Contains(picked))
            Picked = null;
    }

    /// <summary>The bonuses the draft would have: highest tier per element and per family, as the engine picks them.</summary>
    public IReadOnlyList<TamerBonusView> SetBonuses(TamerStableState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var units = MemberList
            .Select(member => state.Monsters.FirstOrDefault(monster => monster.Id == member.MonsterId))
            .Where(monster => (monster is not null) && (state.SpeciesOf(monster) is not null))
            .Select(monster => state.SpeciesOf(monster)!)
            .ToList();

        if ((units.Count == 0) || (state.Bonuses.Count == 0))
            return [];

        var met = state.Bonuses
            .Where(bonus => CountMatching(units, bonus) >= bonus.Count)
            .ToList();

        if (met.Count == 0)
            return [];

        var groups = new Dictionary<string, List<TamerBonusView>>(StringComparer.OrdinalIgnoreCase);

        foreach (var bonus in met)
        {
            var key = bonus.HasElement ? "E:" + (int)bonus.Element : "F:" + (bonus.Family ?? string.Empty);

            if (!groups.TryGetValue(key, out var list))
            {
                list = [];
                groups[key] = list;
            }

            list.Add(bonus);
        }

        var kept = new HashSet<TamerBonusView>();

        foreach (var group in groups.Values)
        {
            var max = group.Max(bonus => bonus.Count);

            foreach (var bonus in group.Where(bonus => bonus.Count == max))
                kept.Add(bonus);
        }

        return state.Bonuses.Where(kept.Contains).ToList();
    }

    /// <summary>The SaveSquad request for the draft, or null when the draft must not be sent.</summary>
    public TamerActionArgs? ToAction(TamerStableState state, byte squadNumber)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (squadNumber > 2)
            return null;

        if (MemberList.Count > state.SquadSlots)
            return null;

        if (MemberList.Count > 5)
            return null;

        var monsters = new Dictionary<Guid, TamerMonsterView>();

        foreach (var monster in state.Monsters)
            monsters[monster.Id] = monster;

        var seenSpecies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var member in MemberList)
        {
            if (!monsters.TryGetValue(member.MonsterId, out var monster))
                return null;

            if (monster.Protected || (state.SpeciesOf(monster) is null))
                return null;

            if (!seenSpecies.Add(monster.SpeciesKey))
                return null;
        }

        return TamerStableState.SaveSquadAction(squadNumber, MemberList);
    }

    private static int CountMatching(List<TamerSpeciesView> units, TamerBonusView bonus)
    {
        if (bonus.HasElement)
            return units.Count(species => species.Element == bonus.Element);

        return units.Count(species => species.Family.Equals(bonus.Family ?? string.Empty, StringComparison.OrdinalIgnoreCase));
    }

    private static bool SameMembers(List<TamerDisplaySquadMember> left, List<TamerDisplaySquadMember> right)
    {
        if (left.Count != right.Count)
            return false;

        var set = new HashSet<(Guid Id, byte Column, byte Row)>(left.Select(member => (member.MonsterId, member.Column, member.Row)));

        return right.All(member => set.Contains((member.MonsterId, member.Column, member.Row)));
    }
}
