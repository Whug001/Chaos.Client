#region
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
#endregion

namespace Chaos.Client.ViewModel;

/// <summary>The Creatures list order.</summary>
public enum TamerStableSort : byte
{
    Newest = 0,
    Level = 1,
    Name = 2
}

/// <summary>What the ability row shows.</summary>
public enum TamerAbilityRowState : byte
{
    Chosen = 0,
    CanChoose = 1,
    Locked = 2
}

/// <summary>One ability in the species' file order, with the row's state for a monster.</summary>
public sealed record TamerAbilityRow(TamerAbilityView Ability, TamerAbilityRowState State);

/// <summary>A monster's four numbers at its level: base plus per-level for each level above 1.</summary>
public sealed record TamerMonsterStats(int Health, int Attack, int Armor, int Speed);

/// <summary>
///     The stable window's last catalog and snapshot from the server, the sorted monster list, the selection,
///     derived values (stats, experience, ability rows, feed limits) and the request builders. Pure: no textures.
///     The level maths mirrors the server's <c>TamerRaisingRules</c> and <c>TamerStable.AddExperience</c> exactly.
/// </summary>
public sealed class TamerStableState
{
    private readonly List<TamerSpeciesView> SpeciesList = [];
    private readonly List<TamerTraitView> TraitList = [];
    private readonly List<TamerBonusView> BonusList = [];
    private readonly Dictionary<string, TamerSpeciesView> SpeciesByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TamerTraitView> TraitsByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<TamerSpeciesView> SpeciesPartBuffer = [];
    private readonly List<TamerTraitView> TraitsPartBuffer = [];
    private readonly List<TamerBonusView> BonusesPartBuffer = [];
    private readonly List<TamerMonsterView> RawMonsters = [];
    private readonly List<TamerMonsterView> SortedMonsters = [];
    private Guid? SelectedId;

    public bool HasCatalog { get; private set; }
    public bool HasSnapshot { get; private set; }

    /// <summary>A catalog just arrived and the next snapshot may open the window. Set by <see cref="ApplyCatalog" />,
    ///     consumed by the next snapshot, cleared by a close.</summary>
    public bool CatalogJustArrived { get; private set; }
    public IReadOnlyList<TamerSpeciesView> Species => SpeciesList;
    public IReadOnlyList<TamerTraitView> Traits => TraitList;
    public IReadOnlyList<TamerBonusView> Bonuses => BonusList;
    public int StableCapacity { get; private set; }
    public int ExperiencePerEssence { get; private set; }
    public int ExperiencePerFeed { get; private set; }
    public long Essence { get; private set; }
    public byte SquadSlots { get; private set; }
    public int FeedItemsHeld { get; private set; }
    public byte ActiveSquad { get; private set; }
    public IReadOnlyList<IReadOnlyList<TamerDisplaySquadMember>> Squads { get; private set; } = [[], [], []];
    public string LastMessage { get; private set; } = string.Empty;
    public TamerStableSort Sort { get; private set; } = TamerStableSort.Newest;
    public IReadOnlyList<TamerMonsterView> Monsters => SortedMonsters;

    public TamerMonsterView? Selected => SelectedId is { } id ? SortedMonsters.FirstOrDefault(m => m.Id == id) : null;

    /// <summary>Takes a catalog part; other display kinds leave the state alone.</summary>
    public void ApplyCatalog(TamerDisplayArgs args)
    {
        if ((args is null) || (args.Kind != TamerDisplayKind.Catalog))
            return;

        if ((args.CatalogPartCount > 1) || (args.CatalogPartNumber != 0))
        {
            if (args.CatalogPartNumber == 0)
            {
                SpeciesPartBuffer.Clear();
                TraitsPartBuffer.Clear();
                BonusesPartBuffer.Clear();
            }

            SpeciesPartBuffer.AddRange(args.Species ?? []);
            TraitsPartBuffer.AddRange(args.Traits ?? []);
            BonusesPartBuffer.AddRange(args.Bonuses ?? []);

            if ((byte)(args.CatalogPartNumber + 1) != args.CatalogPartCount)
                return;

            ReplaceCatalog(
                SpeciesPartBuffer,
                TraitsPartBuffer,
                BonusesPartBuffer,
                args.StableCapacity,
                args.ExperiencePerEssence,
                args.ExperiencePerFeed);
            SpeciesPartBuffer.Clear();
            TraitsPartBuffer.Clear();
            BonusesPartBuffer.Clear();

            return;
        }

        ReplaceCatalog(args.Species, args.Traits, args.Bonuses, args.StableCapacity, args.ExperiencePerEssence, args.ExperiencePerFeed);
    }

    /// <summary>Takes a snapshot; other display kinds leave the state alone.</summary>
    public void ApplySnapshot(TamerDisplayArgs args)
    {
        if ((args is null) || (args.Kind != TamerDisplayKind.Snapshot))
            return;

        Essence = args.Essence;
        SquadSlots = args.SquadSlots;
        FeedItemsHeld = args.FeedItemsHeld;
        ActiveSquad = args.ActiveSquad;
        Squads = NormalizeSquads(args.Squads);
        RawMonsters.Clear();
        RawMonsters.AddRange(args.Monsters ?? []);
        LastMessage = args.Message ?? string.Empty;
        HasSnapshot = true;

        RebuildSorted();
        FixSelection();
    }

    public void Clear()
    {
        SpeciesList.Clear();
        TraitList.Clear();
        BonusList.Clear();
        SpeciesByKey.Clear();
        TraitsByKey.Clear();
        SpeciesPartBuffer.Clear();
        TraitsPartBuffer.Clear();
        BonusesPartBuffer.Clear();
        RawMonsters.Clear();
        SortedMonsters.Clear();
        SelectedId = null;
        CatalogJustArrived = false;
        HasCatalog = false;
        HasSnapshot = false;
        StableCapacity = 0;
        ExperiencePerEssence = 0;
        ExperiencePerFeed = 0;
        Essence = 0;
        SquadSlots = 0;
        FeedItemsHeld = 0;
        ActiveSquad = 0;
        Squads = [[], [], []];
        LastMessage = string.Empty;
        Sort = TamerStableSort.Newest;
    }

    /// <summary>Whether the next snapshot may open the window. Consumes the flag: true once, then false until the next catalog.</summary>
    public bool ConsumeCatalogJustArrived()
    {
        var pending = CatalogJustArrived;
        CatalogJustArrived = false;
        return pending;
    }

    /// <summary>Clears the pending open, for a close or the player closing the window.</summary>
    public void ClearCatalogJustArrived() => CatalogJustArrived = false;

    public void CycleSort() => SetSort((TamerStableSort)(((int)Sort + 1) % 3));

    public void SetSort(TamerStableSort sort)
    {
        Sort = sort;
        RebuildSorted();
    }

    public void Select(Guid monsterId)
    {
        if (monsterId == Guid.Empty)
            return;

        if (RawMonsters.All(m => m.Id != monsterId))
            return;

        SelectedId = monsterId;
    }

    public TamerSpeciesView? GetSpecies(string? key)
    {
        if (string.IsNullOrEmpty(key))
            return null;

        return SpeciesByKey.TryGetValue(key, out var species) ? species : null;
    }

    public TamerTraitView? GetTrait(string? key)
    {
        if (string.IsNullOrEmpty(key))
            return null;

        return TraitsByKey.TryGetValue(key, out var trait) ? trait : null;
    }

    public TamerAbilityView? GetAbility(TamerSpeciesView? species, string? abilityKey)
    {
        if ((species is null) || string.IsNullOrEmpty(abilityKey))
            return null;

        return species.Abilities.FirstOrDefault(a => a.Key.Equals(abilityKey, StringComparison.OrdinalIgnoreCase));
    }

    public TamerSpeciesView? SpeciesOf(TamerMonsterView? monster)
        => monster is null ? null : GetSpecies(monster.SpeciesKey);

    public TamerTraitView? TraitOf(TamerMonsterView? monster)
        => monster is null ? null : GetTrait(monster.TraitKey);

    public TamerAbilityView? AbilityOf(TamerMonsterView? monster)
        => monster is null ? null : GetAbility(SpeciesOf(monster), monster.AbilityKey);

    /// <summary>The monster's four numbers at its level, or null when its species is missing.</summary>
    public TamerMonsterStats? StatsOf(TamerMonsterView? monster)
    {
        var species = SpeciesOf(monster);

        if ((monster is null) || (species is null))
            return null;

        var level = Math.Clamp((int)monster.Level, 1, TamerStableText.MAX_LEVEL);
        var steps = level - 1;

        return new TamerMonsterStats(
            species.BaseHealth + (species.PerLevelHealth * steps),
            species.BaseAttack + (species.PerLevelAttack * steps),
            species.BaseArmor + (species.PerLevelArmor * steps),
            species.BaseSpeed + (species.PerLevelSpeed * steps));
    }

    /// <summary>"N / M" for the bar, or "Peak level" at 20; empty with no monster.</summary>
    public string ExperienceText(TamerMonsterView? monster)
    {
        if (monster is null)
            return string.Empty;

        if (monster.Level >= TamerStableText.MAX_LEVEL)
            return TamerStableText.PeakLevel;

        var total = (long)monster.Experience + Math.Max(0, monster.ExperienceToNext);

        return $"{Math.Max(0, monster.Experience)} / {total}";
    }

    public double ExperienceFraction(TamerMonsterView? monster)
    {
        if (monster is null)
            return 0;

        if (monster.Level >= TamerStableText.MAX_LEVEL)
            return 1;

        var total = (long)monster.Experience + Math.Max(0, monster.ExperienceToNext);

        return total <= 0 ? 0 : (double)Math.Max(0, monster.Experience) / total;
    }

    /// <summary>True when the Rename button can stay on: not null, not protected, species present.</summary>
    public bool CanRename(TamerMonsterView? monster, out string? problem)
    {
        if (monster is null)
        {
            problem = "That creature is no longer in your stable.";
            return false;
        }

        if (monster.Protected || (SpeciesOf(monster) is null))
        {
            problem = "That creature's species is unknown.";
            return false;
        }

        problem = null;
        return true;
    }

    /// <summary>True when the Release button can stay on.</summary>
    public bool CanRelease(TamerMonsterView? monster, out string? problem)
    {
        if (monster is null)
        {
            problem = "That creature is no longer in your stable.";
            return false;
        }

        if (monster.Protected || (SpeciesOf(monster) is null))
        {
            problem = "That creature's species is unknown.";
            return false;
        }

        problem = null;
        return true;
    }

    /// <summary>True when the Feed button can stay on: not null, not protected, not at peak.</summary>
    public bool CanFeed(TamerMonsterView? monster, out string? problem)
    {
        if (monster is null)
        {
            problem = "That creature is no longer in your stable.";
            return false;
        }

        if (monster.Protected || (SpeciesOf(monster) is null))
        {
            problem = "That creature's species is unknown.";
            return false;
        }

        if (monster.Level >= TamerStableText.MAX_LEVEL)
        {
            problem = "That creature is already at its peak.";
            return false;
        }

        problem = null;
        return true;
    }

    /// <summary>True when a feed of <paramref name="amount" /> from <paramref name="source" /> may be sent.</summary>
    public bool CanFeed(TamerMonsterView? monster, TamerWireFeedSource source, int amount, out string? problem)
    {
        if (!CanFeed(monster, out problem))
            return false;

        if (!Enum.IsDefined(source))
        {
            problem = "That creature is no longer in your stable.";
            return false;
        }

        if (amount < 1)
        {
            problem = "The amount must be at least 1.";
            return false;
        }

        if (amount > FeedLimit(monster, source))
        {
            problem = source == TamerWireFeedSource.Essence ? "You do not have enough essence." : "You do not have enough feed.";
            return false;
        }

        problem = null;
        return true;
    }

    /// <summary>The species' abilities in file order, each marked chosen, choosable or locked for the monster.</summary>
    public IReadOnlyList<TamerAbilityRow> AbilityRows(TamerMonsterView? monster)
    {
        var species = SpeciesOf(monster);

        if ((monster is null) || (species is null))
            return [];

        var rows = new List<TamerAbilityRow>(species.Abilities.Count);
        var level = Math.Clamp((int)monster.Level, 1, TamerStableText.MAX_LEVEL);

        foreach (var ability in species.Abilities)
        {
            var chosen = ability.Key.Equals(monster.AbilityKey, StringComparison.OrdinalIgnoreCase);
            var available = level >= ability.UnlockLevel;

            rows.Add(
                new TamerAbilityRow(
                    ability,
                    chosen ? TamerAbilityRowState.Chosen
                        : available ? TamerAbilityRowState.CanChoose
                        : TamerAbilityRowState.Locked));
        }

        return rows;
    }

    /// <summary>The most units worth sending from <paramref name="source" />: useful, capped by what is held; 0 when none.</summary>
    public int FeedLimit(TamerMonsterView? monster, TamerWireFeedSource source)
    {
        if ((monster is null) || (monster.Protected) || (SpeciesOf(monster) is null))
            return 0;

        if (monster.Level >= TamerStableText.MAX_LEVEL)
            return 0;

        var rate = RateOf(source);

        if (rate <= 0)
            return 0;

        var useful = UsefulUnits(monster, rate);
        return useful <= 0 ? 0 : Math.Min(useful, HeldOf(source));
    }

    /// <summary>The level reached after feeding <paramref name="amount" /> units, per the server's carry-over.</summary>
    public int FeedPreview(TamerMonsterView? monster, TamerWireFeedSource source, int amount)
    {
        if (monster is null)
            return 0;

        var level = Math.Clamp((int)monster.Level, 1, TamerStableText.MAX_LEVEL);

        if ((monster.Protected) || (SpeciesOf(monster) is null) || (level >= TamerStableText.MAX_LEVEL) || (amount < 1))
            return level;

        var rate = RateOf(source);

        if (rate <= 0)
            return level;

        var perLevel = ExperiencePerLevelOf(monster);

        if (perLevel <= 0)
            return level;

        var experience = Math.Max(0, monster.Experience) + ((long)amount * rate);
        var current = level;

        while (current < TamerStableText.MAX_LEVEL)
        {
            var needed = (long)perLevel * current;

            if (experience < needed)
                break;

            experience -= needed;
            current++;
        }

        return current;
    }

    public string FeedPreviewText(TamerMonsterView? monster, TamerWireFeedSource source, int amount)
    {
        if (monster is null)
            return string.Empty;

        return TamerStableText.FeedPreviewLine(monster.Level, FeedPreview(monster, source, amount));
    }

    public static TamerActionArgs OpenAction() => new() { Type = TamerActionType.Open };

    public static TamerActionArgs CloseAction() => new() { Type = TamerActionType.Close };

    /// <summary>Null unless the trimmed name passes the house-pet rule and the id is set.</summary>
    public static TamerActionArgs? RenameAction(Guid monsterId, string name)
    {
        if (monsterId == Guid.Empty)
            return null;

        var cleaned = name is null ? string.Empty : name.Trim();

        if (!TamerStableText.IsValidName(cleaned))
            return null;

        return new TamerActionArgs { Type = TamerActionType.Rename, MonsterId = monsterId, Name = cleaned };
    }

    public static TamerActionArgs? ReleaseAction(Guid monsterId)
        => monsterId == Guid.Empty ? null : new TamerActionArgs { Type = TamerActionType.Release, MonsterId = monsterId };

    public static TamerActionArgs? SetAbilityAction(Guid monsterId, string abilityKey)
    {
        if (monsterId == Guid.Empty)
            return null;

        var key = abilityKey is null ? string.Empty : abilityKey.Trim();

        if (key.Length == 0)
            return null;

        return new TamerActionArgs { Type = TamerActionType.SetAbility, MonsterId = monsterId, AbilityKey = key };
    }

    /// <summary>Null unless the source is known and the amount is at least 1.</summary>
    public static TamerActionArgs? FeedAction(Guid monsterId, TamerWireFeedSource source, int amount)
    {
        if ((monsterId == Guid.Empty) || !Enum.IsDefined(source) || (amount < 1))
            return null;

        return new TamerActionArgs { Type = TamerActionType.Feed, MonsterId = monsterId, FeedSource = source, FeedAmount = amount };
    }

    /// <summary>Null for a bad squad number, more than 5 members, bad tiles, duplicate ids or duplicate tiles.</summary>
    public static TamerActionArgs? SaveSquadAction(byte squad, IReadOnlyList<TamerDisplaySquadMember> members)
    {
        if ((members is null) || (squad > 2))
            return null;

        if (members.Count > 5)
            return null;

        var seenIds = new HashSet<Guid>();
        var seenTiles = new HashSet<(int Column, int Row)>();
        var list = new List<TamerActionSquadMember>(members.Count);

        foreach (var member in members)
        {
            if ((member is null) || (member.MonsterId == Guid.Empty))
                return null;

            if ((member.Column > 6) || (member.Row > 2))
                return null;

            if (!seenIds.Add(member.MonsterId))
                return null;

            if (!seenTiles.Add((member.Column, member.Row)))
                return null;

            list.Add(new TamerActionSquadMember(member.MonsterId, member.Column, member.Row));
        }

        return new TamerActionArgs { Type = TamerActionType.SaveSquad, Squad = squad, Members = list };
    }

    public static TamerActionArgs? SetActiveSquadAction(byte squad)
        => squad > 2 ? null : new TamerActionArgs { Type = TamerActionType.SetActiveSquad, Squad = squad };

    private void ReplaceCatalog(
        IReadOnlyList<TamerSpeciesView> species,
        IReadOnlyList<TamerTraitView> traits,
        IReadOnlyList<TamerBonusView> bonuses,
        int capacity,
        int perEssence,
        int perFeed)
    {
        SpeciesList.Clear();
        TraitList.Clear();
        BonusList.Clear();
        SpeciesByKey.Clear();
        TraitsByKey.Clear();

        foreach (var view in species ?? [])
        {
            if (view is null)
                continue;

            SpeciesList.Add(view);

            if (!string.IsNullOrEmpty(view.Key))
                SpeciesByKey[view.Key] = view;
        }

        foreach (var view in traits ?? [])
        {
            if (view is null)
                continue;

            TraitList.Add(view);

            if (!string.IsNullOrEmpty(view.Key))
                TraitsByKey[view.Key] = view;
        }

        foreach (var view in bonuses ?? [])
        {
            if (view is not null)
                BonusList.Add(view);
        }

        StableCapacity = capacity;
        ExperiencePerEssence = perEssence;
        ExperiencePerFeed = perFeed;
        HasCatalog = true;
        CatalogJustArrived = true;
    }

    private static IReadOnlyList<IReadOnlyList<TamerDisplaySquadMember>> NormalizeSquads(List<List<TamerDisplaySquadMember>>? squads)
    {
        var list = squads ?? [[], [], []];

        return [CopySquad(list, 0), CopySquad(list, 1), CopySquad(list, 2)];

        static List<TamerDisplaySquadMember> CopySquad(List<List<TamerDisplaySquadMember>> rows, int index)
            => (rows is not null) && (index < rows.Count) && (rows[index] is not null)
                ? rows[index].Where(member => member is not null).ToList()
                : [];
    }

    private void RebuildSorted()
    {
        SortedMonsters.Clear();

        SortedMonsters.AddRange(
            Sort switch
            {
                TamerStableSort.Level => RawMonsters
                    .Select((monster, index) => (monster, index))
                    .OrderByDescending(entry => entry.monster.Level)
                    .ThenBy(entry => entry.monster.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(entry => entry.index)
                    .Select(entry => entry.monster),
                TamerStableSort.Name => RawMonsters
                    .Select((monster, index) => (monster, index))
                    .OrderBy(entry => entry.monster.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(entry => entry.index)
                    .Select(entry => entry.monster),
                _ => RawMonsters.AsEnumerable().Reverse()
            });
    }

    private void FixSelection()
    {
        if (SortedMonsters.Count == 0)
        {
            SelectedId = null;
            return;
        }

        if ((SelectedId is null) || (RawMonsters.All(m => m.Id != SelectedId)))
            SelectedId = SortedMonsters[0].Id;
    }

    private int RateOf(TamerWireFeedSource source)
    {
        var rate = source == TamerWireFeedSource.Essence ? ExperiencePerEssence : ExperiencePerFeed;

        return rate <= 0 ? 1 : rate;
    }

    private int HeldOf(TamerWireFeedSource source)
    {
        if (source == TamerWireFeedSource.Essence)
            return Essence <= 0 ? 0 : (Essence >= int.MaxValue ? int.MaxValue : (int)Essence);

        return Math.Max(0, FeedItemsHeld);
    }

    private int ExperiencePerLevelOf(TamerMonsterView monster)
    {
        var level = Math.Clamp((int)monster.Level, 1, TamerStableText.MAX_LEVEL);

        if (level >= TamerStableText.MAX_LEVEL)
            return 100;

        var total = (long)monster.Experience + Math.Max(0, monster.ExperienceToNext);

        return total <= 0 ? 100 : (int)(total / level);
    }

    private int UsefulUnits(TamerMonsterView monster, int rate)
    {
        var level = Math.Clamp((int)monster.Level, 1, TamerStableText.MAX_LEVEL);

        if (level >= TamerStableText.MAX_LEVEL)
            return 0;

        var perLevel = ExperiencePerLevelOf(monster);

        if (perLevel <= 0)
            return 0;

        long needed = 0;
        var experience = Math.Max(0, monster.Experience);
        var toNext = (long)perLevel * level;

        if (experience < toNext)
            needed += toNext - experience;

        for (var l = level + 1; l < TamerStableText.MAX_LEVEL; l++)
            needed += (long)perLevel * l;

        if (needed <= 0)
            return 0;

        return (int)((needed + rate - 1) / rate);
    }
}
