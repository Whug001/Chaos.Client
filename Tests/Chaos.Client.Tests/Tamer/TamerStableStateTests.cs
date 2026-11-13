using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class TamerStableStateTests
{
    private static Guid Id(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:D12}");

    private static TamerSpeciesView Species(
        string key,
        TamerWireElement element = TamerWireElement.Fire,
        string family = "Beast",
        string name = "Ember Imp",
        TamerWireRarity rarity = TamerWireRarity.Common,
        TamerWireRole role = TamerWireRole.Bruiser,
        ushort sprite = 100,
        int baseHealth = 20,
        int baseAttack = 10,
        int baseArmor = 5,
        int baseSpeed = 8,
        int perHealth = 3,
        int perAttack = 2,
        int perArmor = 1,
        int perSpeed = 1,
        List<TamerAbilityView>? abilities = null)
        => new(
            key,
            name,
            sprite,
            rarity,
            element,
            family,
            role,
            1,
            baseHealth,
            baseAttack,
            baseArmor,
            baseSpeed,
            perHealth,
            perAttack,
            perArmor,
            perSpeed,
            abilities ?? [new("a1", "Ember Strike", 1, "Hits hard.")]);

    private static TamerMonsterView Monster(
        Guid id,
        string speciesKey,
        string name,
        byte level,
        int experience,
        int experienceToNext,
        string traitKey = "trait1",
        string abilityKey = "a1",
        bool isProtected = false,
        long releaseEssence = 5)
        => new(id, speciesKey, name, level, experience, experienceToNext, traitKey, abilityKey, isProtected, releaseEssence);

    private static TamerMonsterView Starter(Guid id, string speciesKey = "ember", string name = "Ember", byte level = 1)
    {
        var total = 100 * level;
        var experience = level switch { 1 => 0, 4 => 50, 20 => 0, _ => 0 };
        var toNext = (level >= 20) ? 0 : total - experience;

        return Monster(id, speciesKey, name, level, experience, toNext);
    }

    private static TamerDisplayArgs Catalog(
        List<TamerSpeciesView>? species = null,
        List<TamerTraitView>? traits = null,
        List<TamerBonusView>? bonuses = null,
        int capacity = 60,
        int perEssence = 20,
        int perFeed = 100)
        => new()
        {
            Kind = TamerDisplayKind.Catalog,
            CatalogPartNumber = 0,
            CatalogPartCount = 1,
            Species = species ?? [Species("ember")],
            Traits = traits ?? [new("trait1", "Kindled", "Grows stronger.")],
            Bonuses = bonuses ?? [],
            StableCapacity = capacity,
            ExperiencePerEssence = perEssence,
            ExperiencePerFeed = perFeed
        };

    private static TamerDisplayArgs Snapshot(
        List<TamerMonsterView>? monsters = null,
        long essence = 100,
        byte slots = 3,
        int feedHeld = 2,
        byte active = 0,
        List<List<TamerDisplaySquadMember>>? squads = null,
        string message = "")
        => new()
        {
            Kind = TamerDisplayKind.Snapshot,
            Essence = essence,
            SquadSlots = slots,
            FeedItemsHeld = feedHeld,
            ActiveSquad = active,
            Squads = squads ?? [[], [], []],
            Monsters = monsters ?? [],
            Message = message
        };

    private static TamerStableState Ready(List<TamerMonsterView> monsters, List<TamerSpeciesView>? species = null)
    {
        var state = new TamerStableState();
        state.ApplyCatalog(Catalog(species: species ?? [Species("ember")]));
        state.ApplySnapshot(Snapshot(monsters: monsters));

        return state;
    }

    [Test]
    public void Applying_a_catalog_sets_the_catalog_and_rates()
    {
        var state = new TamerStableState();
        state.HasCatalog.Should().BeFalse();
        state.ApplyCatalog(Catalog(capacity: 60, perEssence: 20, perFeed: 100));

        state.HasCatalog.Should().BeTrue();
        state.Species.Should().HaveCount(1);
        state.Species[0].Key.Should().Be("ember");
        state.Traits.Should().ContainSingle();
        state.StableCapacity.Should().Be(60);
        state.ExperiencePerEssence.Should().Be(20);
        state.ExperiencePerFeed.Should().Be(100);
    }

    [Test]
    public void Applying_a_snapshot_sets_the_stable_and_the_message()
    {
        var state = new TamerStableState();
        state.ApplyCatalog(Catalog());
        var monster = Starter(Id(1));

        state.ApplySnapshot(Snapshot(monsters: [monster], essence: 250, slots: 4, feedHeld: 6, active: 2, message: "That squad is full."));

        state.HasSnapshot.Should().BeTrue();
        state.Essence.Should().Be(250);
        state.SquadSlots.Should().Be(4);
        state.FeedItemsHeld.Should().Be(6);
        state.ActiveSquad.Should().Be(2);
        state.Monsters.Should().ContainSingle().Which.Id.Should().Be(Id(1));
        state.LastMessage.Should().Be("That squad is full.");
    }

    [Test]
    public void A_catalog_only_applies_catalogs_and_a_snapshot_only_applies_snapshots()
    {
        var state = new TamerStableState();

        state.ApplyCatalog(Snapshot());
        state.HasCatalog.Should().BeFalse();
        state.HasSnapshot.Should().BeFalse();

        state.ApplySnapshot(Catalog());
        state.HasSnapshot.Should().BeFalse();
        state.Monsters.Should().BeEmpty();
    }

    [Test]
    public void A_second_catalog_replaces_the_first()
    {
        var state = new TamerStableState();
        state.ApplyCatalog(Catalog(species: [Species("ember")]));
        state.ApplyCatalog(Catalog(species: [Species("tide", TamerWireElement.Water, "Beast")], capacity: 40));

        state.Species.Should().HaveCount(1);
        state.Species[0].Key.Should().Be("tide");
        state.StableCapacity.Should().Be(40);
    }

    [Test]
    public void Newest_sort_is_the_snapshot_order_reversed()
    {
        var state = Ready([Starter(Id(1), name: "First"), Starter(Id(2), name: "Second"), Starter(Id(3), name: "Third")]);

        state.Sort.Should().Be(TamerStableSort.Newest);
        state.Monsters.Select(m => m.Name).Should().Equal("Third", "Second", "First");
    }

    [Test]
    public void Level_sort_is_highest_first_then_name()
    {
        var low = Starter(Id(1), name: "Bravo", level: 2);
        var high = Starter(Id(2), name: "Zulu", level: 5);
        var midAlpha = Starter(Id(3), name: "Alpha", level: 5);
        var state = Ready([low, high, midAlpha]);

        state.SetSort(TamerStableSort.Level);

        state.Monsters.Select(m => m.Name).Should().Equal("Alpha", "Zulu", "Bravo");
    }

    [Test]
    public void Name_sort_is_alphabetical_case_insensitively()
    {
        var state = Ready([Starter(Id(1), name: "bravo"), Starter(Id(2), name: "Alpha"), Starter(Id(3), name: "charlie")]);

        state.SetSort(TamerStableSort.Name);

        state.Monsters.Select(m => m.Name).Should().Equal("Alpha", "bravo", "charlie");
    }

    [Test]
    public void Cycle_sort_goes_newest_level_name_newest()
    {
        var state = new TamerStableState();
        state.Sort.Should().Be(TamerStableSort.Newest);

        state.CycleSort();
        state.Sort.Should().Be(TamerStableSort.Level);

        state.CycleSort();
        state.Sort.Should().Be(TamerStableSort.Name);

        state.CycleSort();
        state.Sort.Should().Be(TamerStableSort.Newest);
    }

    [Test]
    public void Sorting_keeps_the_selection_by_id()
    {
        var state = Ready([Starter(Id(1), name: "Bravo"), Starter(Id(2), name: "Alpha")]);
        state.Select(Id(1));

        state.SetSort(TamerStableSort.Name);

        state.Selected.Should().NotBeNull();
        state.Selected!.Id.Should().Be(Id(1));
    }

    [Test]
    public void The_first_row_is_selected_when_the_window_opens()
    {
        var state = new TamerStableState();
        state.ApplyCatalog(Catalog());

        state.Selected.Should().BeNull();

        state.ApplySnapshot(Snapshot(monsters: [Starter(Id(1), name: "Bravo"), Starter(Id(2), name: "Alpha")]));

        state.Selected.Should().NotBeNull();
        state.Selected!.Id.Should().Be(state.Monsters[0].Id);
        state.Selected!.Name.Should().Be("Alpha");
    }

    [Test]
    public void Selection_is_kept_by_id_across_snapshots()
    {
        var state = Ready([Starter(Id(1), name: "Bravo"), Starter(Id(2), name: "Alpha")]);
        state.Select(Id(2));

        state.ApplySnapshot(Snapshot(monsters: [Starter(Id(1), name: "Bravo"), Starter(Id(2), name: "Alpha"), Starter(Id(3), name: "Charlie")]));

        state.Selected.Should().NotBeNull();
        state.Selected!.Id.Should().Be(Id(2));
    }

    [Test]
    public void Selection_moves_to_the_first_row_when_the_monster_is_gone()
    {
        var state = Ready([Starter(Id(1), name: "Bravo"), Starter(Id(2), name: "Alpha")]);
        state.Select(Id(2));

        state.ApplySnapshot(Snapshot(monsters: [Starter(Id(1), name: "Bravo")]));

        state.Selected.Should().NotBeNull();
        state.Selected!.Id.Should().Be(Id(1));
    }

    [Test]
    public void An_empty_snapshot_has_no_selection()
    {
        var state = Ready([Starter(Id(1))]);

        state.ApplySnapshot(Snapshot(monsters: []));

        state.Selected.Should().BeNull();
        state.Monsters.Should().BeEmpty();
    }

    [Test]
    public void Selecting_an_unknown_id_leaves_the_selection()
    {
        var state = Ready([Starter(Id(1))]);

        state.Select(Id(9));

        state.Selected.Should().NotBeNull();
        state.Selected!.Id.Should().Be(Id(1));
    }

    [Test]
    public void Species_trait_and_ability_lookups_are_case_insensitive()
    {
        var state = Ready([Starter(Id(1))]);
        var monster = state.Monsters.Single();

        state.GetSpecies("EMBER")!.Key.Should().Be("ember");
        state.GetSpecies("nosuch").Should().BeNull();
        state.GetTrait("TRAIT1")!.Name.Should().Be("Kindled");
        state.GetTrait("nosuch").Should().BeNull();
        state.GetAbility(state.GetSpecies("ember"), "A1")!.Name.Should().Be("Ember Strike");
        state.GetAbility(state.GetSpecies("ember"), "nosuch").Should().BeNull();
        state.GetAbility(null, "a1").Should().BeNull();
        state.SpeciesOf(monster)!.Key.Should().Be("ember");
        state.TraitOf(monster)!.Key.Should().Be("trait1");
        state.AbilityOf(monster)!.Key.Should().Be("a1");
    }

    [Test]
    public void Stats_match_the_server_at_level_one_and_above()
    {
        var state = Ready([Starter(Id(1), level: 1)]);
        var monster = state.Monsters.Single();

        state.StatsOf(monster)!.Health.Should().Be(20);
        state.StatsOf(monster)!.Attack.Should().Be(10);
        state.StatsOf(monster)!.Armor.Should().Be(5);
        state.StatsOf(monster)!.Speed.Should().Be(8);
    }

    [Test]
    public void Stats_grow_by_per_level_for_each_level_above_one()
    {
        var levelFive = Monster(Id(1), "ember", "Ember", 5, 0, 500);
        var state = Ready([levelFive]);
        var monster = state.Monsters.Single();

        state.StatsOf(monster)!.Health.Should().Be(20 + (3 * 4));
        state.StatsOf(monster)!.Attack.Should().Be(10 + (2 * 4));
        state.StatsOf(monster)!.Armor.Should().Be(5 + (1 * 4));
        state.StatsOf(monster)!.Speed.Should().Be(8 + (1 * 4));
        state.StatsOf(null).Should().BeNull();
    }

    [Test]
    public void Experience_text_is_current_over_total_and_peak_at_twenty()
    {
        var mid = Monster(Id(1), "ember", "Ember", 4, 50, 350);
        var peak = Monster(Id(2), "ember", "Peak", 20, 0, 0);
        var state = Ready([mid, peak]);

        state.ExperienceText(state.Monsters.First(m => m.Id == Id(1))).Should().Be("50 / 400");
        state.ExperienceText(state.Monsters.First(m => m.Id == Id(2))).Should().Be("Peak level");
        state.ExperienceText(null).Should().BeEmpty();
        state.ExperienceFraction(state.Monsters.First(m => m.Id == Id(1))).Should().BeApproximately(0.125, 0.0001);
        state.ExperienceFraction(state.Monsters.First(m => m.Id == Id(2))).Should().Be(1);
    }

    [Test]
    public void Ability_rows_chosen_can_choose_and_locked_with_unlock_level()
    {
        var species = Species(
            "ember",
            abilities:
            [
                new("a1", "First", 1, "First text."),
                new("a2", "Second", 3, "Second text."),
                new("a3", "Third", 5, "Third text.")
            ]);
        var monster = Monster(Id(1), "ember", "Ember", 4, 50, 350, abilityKey: "a1");
        var state = Ready([monster], species: [species]);

        var rows = state.AbilityRows(state.Monsters.Single());

        rows.Should().HaveCount(3);
        rows[0].Ability.Key.Should().Be("a1");
        rows[0].State.Should().Be(TamerAbilityRowState.Chosen);
        rows[1].State.Should().Be(TamerAbilityRowState.CanChoose);
        rows[2].State.Should().Be(TamerAbilityRowState.Locked);
        rows[2].Ability.UnlockLevel.Should().Be(5);
    }

    [Test]
    public void A_chosen_ability_always_reads_as_chosen()
    {
        var species = Species("ember", abilities: [new("a1", "First", 1, "Text."), new("a2", "Second", 1, "Text.")]);
        var monster = Monster(Id(1), "ember", "Ember", 1, 0, 100, abilityKey: "a2");
        var state = Ready([monster], species: [species]);

        state.AbilityRows(state.Monsters.Single())[1].State.Should().Be(TamerAbilityRowState.Chosen);
    }

    [Test]
    public void Feed_limit_is_the_useful_amount_capped_by_what_is_held()
    {
        var state = new TamerStableState();
        state.ApplyCatalog(Catalog(perEssence: 20, perFeed: 100));
        state.ApplySnapshot(Snapshot(monsters: [Starter(Id(1))], essence: 10000, feedHeld: 500));

        var monster = state.Monsters.Single();

        state.FeedLimit(monster, TamerWireFeedSource.Essence).Should().Be(950);
        state.FeedLimit(monster, TamerWireFeedSource.Feed).Should().Be(190);
    }

    [Test]
    public void Feed_limit_is_capped_by_essence_and_feed_held()
    {
        var state = new TamerStableState();
        state.ApplyCatalog(Catalog(perEssence: 20, perFeed: 100));
        state.ApplySnapshot(Snapshot(monsters: [Starter(Id(1))], essence: 10, feedHeld: 3));

        var monster = state.Monsters.Single();

        state.FeedLimit(monster, TamerWireFeedSource.Essence).Should().Be(10);
        state.FeedLimit(monster, TamerWireFeedSource.Feed).Should().Be(3);
    }

    [Test]
    public void Feed_limit_rounds_up_for_a_partial_unit()
    {
        var monster = Monster(Id(1), "ember", "Ember", 4, 50, 350);
        var state = new TamerStableState();
        state.ApplyCatalog(Catalog(perEssence: 20, perFeed: 100));
        state.ApplySnapshot(Snapshot(monsters: [monster], essence: 100000, feedHeld: 500));

        state.FeedLimit(state.Monsters.Single(), TamerWireFeedSource.Essence).Should().Be(918);
        state.FeedLimit(state.Monsters.Single(), TamerWireFeedSource.Feed).Should().Be(184);
    }

    [Test]
    public void Feed_preview_levels_follow_the_server_carry_over()
    {
        var monster = Monster(Id(1), "ember", "Ember", 4, 50, 350);
        var state = new TamerStableState();
        state.ApplyCatalog(Catalog(perEssence: 20, perFeed: 100));
        state.ApplySnapshot(Snapshot(monsters: [monster], essence: 1000, feedHeld: 10));
        var held = state.Monsters.Single();

        state.FeedPreview(held, TamerWireFeedSource.Essence, 1).Should().Be(4);
        state.FeedPreview(held, TamerWireFeedSource.Essence, 43).Should().Be(6);
        state.FeedPreview(held, TamerWireFeedSource.Feed, 1).Should().Be(4);
        state.FeedPreviewText(held, TamerWireFeedSource.Essence, 43).Should().Be("Level 4 \u2192 6");
    }

    [Test]
    public void Feed_preview_stops_at_level_twenty_and_ignores_non_positive_amounts()
    {
        var monster = Monster(Id(1), "ember", "Ember", 4, 50, 350);
        var state = new TamerStableState();
        state.ApplyCatalog(Catalog(perEssence: 20, perFeed: 100));
        state.ApplySnapshot(Snapshot(monsters: [monster], essence: 100000, feedHeld: 500));
        var held = state.Monsters.Single();

        state.FeedPreview(held, TamerWireFeedSource.Feed, 9999).Should().Be(20);
        state.FeedPreview(held, TamerWireFeedSource.Essence, 0).Should().Be(4);
        state.FeedPreview(held, TamerWireFeedSource.Essence, -5).Should().Be(4);
        state.FeedLimit(null, TamerWireFeedSource.Essence).Should().Be(0);
        state.FeedPreview(null, TamerWireFeedSource.Essence, 5).Should().Be(0);
    }

    [Test]
    public void Buttons_are_disabled_for_protected_and_peak_monsters()
    {
        var normal = Starter(Id(1));
        var locked = Monster(Id(2), "missing", "Gone", 4, 50, 350, isProtected: true);
        var peak = Monster(Id(3), "ember", "Peak", 20, 0, 0);
        var state = Ready([normal, locked, peak]);
        var byId = state.Monsters.ToDictionary(m => m.Id);

        state.CanRename(byId[Id(1)], out var ok).Should().BeTrue();
        ok.Should().BeNull();
        state.CanRelease(byId[Id(1)], out _).Should().BeTrue();
        state.CanFeed(byId[Id(1)], out _).Should().BeTrue();

        state.CanRename(byId[Id(2)], out var lockedProblem).Should().BeFalse();
        lockedProblem.Should().NotBeNullOrEmpty();
        state.CanRelease(byId[Id(2)], out _).Should().BeFalse();
        state.CanFeed(byId[Id(2)], out _).Should().BeFalse();

        state.CanFeed(byId[Id(3)], out var peakProblem).Should().BeFalse();
        peakProblem.Should().Be("That creature is already at its peak.");
        state.CanRename(byId[Id(3)], out _).Should().BeTrue();
        state.CanRelease(byId[Id(3)], out _).Should().BeTrue();
        state.CanRename(null, out var nullProblem).Should().BeFalse();
        nullProblem.Should().NotBeNullOrEmpty();
    }

    [Test]
    public void Feeding_checks_the_amount_and_what_is_held()
    {
        var monster = Monster(Id(1), "ember", "Ember", 4, 50, 350);
        var state = new TamerStableState();
        state.ApplyCatalog(Catalog(perEssence: 20, perFeed: 100));
        state.ApplySnapshot(Snapshot(monsters: [monster], essence: 5, feedHeld: 1));
        var held = state.Monsters.Single();

        state.CanFeed(held, TamerWireFeedSource.Essence, 0, out var zero).Should().BeFalse();
        zero.Should().Be("The amount must be at least 1.");
        state.CanFeed(held, TamerWireFeedSource.Essence, 6, out var broke).Should().BeFalse();
        broke.Should().Be("You do not have enough essence.");
        state.CanFeed(held, TamerWireFeedSource.Feed, 2, out var noFeed).Should().BeFalse();
        noFeed.Should().Be("You do not have enough feed.");
        state.CanFeed(held, TamerWireFeedSource.Essence, 5, out var fine).Should().BeTrue();
        fine.Should().BeNull();
    }

    [Test]
    public void Protected_monsters_have_no_stats_or_ability_rows_but_keep_experience()
    {
        var locked = Monster(Id(1), "missing", "Gone", 4, 50, 350, traitKey: "missing", isProtected: true);
        var state = Ready([locked], species: [Species("ember")]);
        var held = state.Monsters.Single();

        held.Protected.Should().BeTrue();
        state.SpeciesOf(held).Should().BeNull();
        state.StatsOf(held).Should().BeNull();
        state.AbilityRows(held).Should().BeEmpty();
        state.AbilityOf(held).Should().BeNull();
        state.ExperienceText(held).Should().Be("50 / 400");
        state.FeedLimit(held, TamerWireFeedSource.Essence).Should().Be(0);
    }

    [Test]
    public void A_catalog_that_loses_a_species_makes_no_dereference()
    {
        var monster = Starter(Id(1));
        var state = Ready([monster], species: [Species("ember")]);
        var held = state.Monsters.Single();

        state.ApplyCatalog(Catalog(species: []));

        state.HasCatalog.Should().BeTrue();
        state.GetSpecies("ember").Should().BeNull();
        state.SpeciesOf(held).Should().BeNull();
        state.StatsOf(held).Should().BeNull();
        state.CanRename(held, out _).Should().BeFalse();
        state.CanFeed(held, out _).Should().BeFalse();
        state.CanRelease(held, out _).Should().BeFalse();
    }

    [Test]
    public void Sprite_ids_do_not_change_the_state_maths()
    {
        var noArt = Species("ember", sprite: 0);
        var held = Monster(Id(1), "ember", "Ember", 5, 0, 500);
        var state = Ready([held], species: [noArt]);

        state.SpeciesOf(state.Monsters.Single())!.Sprite.Should().Be((ushort)0);
        state.StatsOf(state.Monsters.Single())!.Health.Should().Be(32);
        state.AbilityRows(state.Monsters.Single()).Should().HaveCount(1);
    }

    [Test]
    public void Clear_resets_the_catalog_snapshot_selection_and_sort()
    {
        var state = Ready([Starter(Id(1)), Starter(Id(2))]);
        state.SetSort(TamerStableSort.Name);
        state.Select(Id(2));

        state.Clear();

        state.HasCatalog.Should().BeFalse();
        state.HasSnapshot.Should().BeFalse();
        state.Monsters.Should().BeEmpty();
        state.Selected.Should().BeNull();
        state.Sort.Should().Be(TamerStableSort.Newest);
        state.LastMessage.Should().BeEmpty();
        state.Essence.Should().Be(0);
        state.StableCapacity.Should().Be(0);
    }

    [Test]
    public void Sixty_long_named_monsters_and_a_peak_monster_stay_sane()
    {
        var monsters = Enumerable.Range(1, 60).Select(i => Monster(Id(i), "ember", $"Longname{i:D2}ab", 20, 0, 0)).ToList();
        var species = Species("ember", abilities: [new("a1", "First", 1, "A long ability text that wraps."), new("a2", "Second", 3, "Another long text."), new("a3", "Third", 5, "Third long text.")]);
        var state = Ready(monsters, species: [species]);

        state.Monsters.Should().HaveCount(60);
        state.Monsters[0].Name.Length.Should().Be(12);
        TamerStableText.CountLine(state.Monsters.Count, state.StableCapacity).Should().Be("60 / 60 creatures");
        state.ExperienceText(state.Monsters[0]).Should().Be("Peak level");
        state.StatsOf(state.Monsters[0])!.Health.Should().Be(20 + (3 * 19));
        state.AbilityRows(state.Monsters[0]).Should().HaveCount(3);
    }

    [Test]
    public void A_snapshot_for_a_released_monster_leaves_the_others_selected()
    {
        var state = Ready([Starter(Id(1), name: "Bravo"), Starter(Id(2), name: "Alpha")]);
        state.Select(Id(2));

        state.ApplySnapshot(Snapshot(monsters: [Starter(Id(1), name: "Bravo")], essence: 95));

        state.Monsters.Should().HaveCount(1);
        state.Selected.Should().NotBeNull();
        state.Selected!.Id.Should().Be(Id(1));
        state.LastMessage.Should().BeEmpty();
    }

    [Test]
    public void A_snapshot_with_no_catalog_does_not_open()
    {
        var state = new TamerStableState();

        state.ApplySnapshot(Snapshot(monsters: [Starter(Id(1))]));

        state.CatalogJustArrived.Should().BeFalse();
        state.ConsumeCatalogJustArrived().Should().BeFalse();
        state.CatalogJustArrived.Should().BeFalse();
    }

    [Test]
    public void Catalog_then_snapshot_opens_once_and_then_only_refreshes()
    {
        var state = new TamerStableState();

        state.ApplyCatalog(Catalog());
        state.CatalogJustArrived.Should().BeTrue();

        state.ApplySnapshot(Snapshot(monsters: [Starter(Id(1))]));

        state.ConsumeCatalogJustArrived().Should().BeTrue();
        state.CatalogJustArrived.Should().BeFalse();

        state.ApplySnapshot(Snapshot(monsters: [Starter(Id(1))]));

        state.ConsumeCatalogJustArrived().Should().BeFalse();
    }

    [Test]
    public void A_late_snapshot_after_the_player_closed_does_not_reopen()
    {
        var state = new TamerStableState();

        state.ApplyCatalog(Catalog());
        state.ApplySnapshot(Snapshot(monsters: [Starter(Id(1))]));
        state.ConsumeCatalogJustArrived().Should().BeTrue();

        state.ClearCatalogJustArrived();

        state.ApplySnapshot(Snapshot(monsters: [Starter(Id(1))]));

        state.ConsumeCatalogJustArrived().Should().BeFalse();
    }

    [Test]
    public void Clearing_the_pending_open_for_a_close_stops_the_next_snapshot_opening()
    {
        var state = new TamerStableState();

        state.ApplyCatalog(Catalog());
        state.CatalogJustArrived.Should().BeTrue();
        state.ClearCatalogJustArrived();

        state.ApplySnapshot(Snapshot(monsters: [Starter(Id(1))]));

        state.ConsumeCatalogJustArrived().Should().BeFalse();
    }

    [Test]
    public void Clear_resets_the_pending_open()
    {
        var state = new TamerStableState();
        state.ApplyCatalog(Catalog());
        state.CatalogJustArrived.Should().BeTrue();

        state.Clear();

        state.CatalogJustArrived.Should().BeFalse();
        state.ConsumeCatalogJustArrived().Should().BeFalse();
    }

    [Test]
    public void Open_and_close_actions_carry_no_payload()
    {
        TamerStableState.OpenAction().Type.Should().Be(TamerActionType.Open);
        TamerStableState.CloseAction().Type.Should().Be(TamerActionType.Close);
    }

    [Test]
    public void Rename_release_and_ability_actions_carry_their_monster()
    {
        var state = Ready([Starter(Id(1))]);

        state.RenameAction(Id(1), "  Ember Imp  ")!.Name.Should().Be("Ember Imp");
        state.RenameAction(Id(1), "Ember Imp")!.Type.Should().Be(TamerActionType.Rename);
        state.RenameAction(Id(1), "Ember")!.MonsterId.Should().Be(Id(1));
        state.ReleaseAction(Id(1))!.Type.Should().Be(TamerActionType.Release);
        state.ReleaseAction(Id(1))!.MonsterId.Should().Be(Id(1));
        state.SetAbilityAction(Id(1), "a1")!.Type.Should().Be(TamerActionType.SetAbility);
        state.SetAbilityAction(Id(1), "a1")!.AbilityKey.Should().Be("a1");
    }

    [Test]
    public void Rename_with_a_bad_name_or_empty_id_is_blocked()
    {
        var state = Ready([Starter(Id(1))]);

        state.RenameAction(Id(1), string.Empty).Should().BeNull();
        state.RenameAction(Id(1), "Ember  Imp").Should().BeNull();
        state.RenameAction(Id(1), "Abcdefghijklm").Should().BeNull();
        state.RenameAction(Id(1), "Ember2").Should().BeNull();
        state.RenameAction(Guid.Empty, "Ember").Should().BeNull();
        state.ReleaseAction(Guid.Empty).Should().BeNull();
        state.SetAbilityAction(Id(1), string.Empty).Should().BeNull();
        state.SetAbilityAction(Id(1), "   ").Should().BeNull();
        state.SetAbilityAction(Guid.Empty, "a1").Should().BeNull();
    }

    [Test]
    public void Feed_actions_carry_source_and_amount_and_block_zero()
    {
        var state = new TamerStableState();
        state.ApplyCatalog(Catalog());
        state.ApplySnapshot(Snapshot(monsters: [Starter(Id(1))], essence: 100, feedHeld: 10));

        var action = state.FeedAction(Id(1), TamerWireFeedSource.Feed, 3);

        action!.Type.Should().Be(TamerActionType.Feed);
        action.MonsterId.Should().Be(Id(1));
        action.FeedSource.Should().Be(TamerWireFeedSource.Feed);
        action.FeedAmount.Should().Be(3);
        state.FeedAction(Id(1), TamerWireFeedSource.Essence, 0).Should().BeNull();
        state.FeedAction(Id(1), TamerWireFeedSource.Essence, -1).Should().BeNull();
        state.FeedAction(Guid.Empty, TamerWireFeedSource.Essence, 1).Should().BeNull();
        state.FeedAction(Id(1), (TamerWireFeedSource)99, 1).Should().BeNull();
    }

    [Test]
    public void A_stale_view_after_release_is_refused_by_guards_and_builders()
    {
        var state = Ready([Starter(Id(1))]);
        var stale = state.Monsters.Single();

        state.ApplySnapshot(Snapshot(monsters: []));

        state.CanRename(stale, out _).Should().BeFalse();
        state.CanRelease(stale, out _).Should().BeFalse();
        state.CanFeed(stale, out _).Should().BeFalse();
        state.AbilityRows(stale).Should().BeEmpty();
        state.RenameAction(Id(1), "Ember").Should().BeNull();
        state.ReleaseAction(Id(1)).Should().BeNull();
        state.SetAbilityAction(Id(1), "a1").Should().BeNull();
        state.FeedAction(Id(1), TamerWireFeedSource.Essence, 1).Should().BeNull();
    }

    [Test]
    public void A_stale_view_after_reaching_the_peak_is_refused_for_feed()
    {
        var state = Ready([Starter(Id(1), level: 4)]);
        var stale = state.Monsters.Single();
        stale.Level.Should().Be((byte)4);

        state.ApplySnapshot(Snapshot(monsters: [Monster(Id(1), "ember", "Ember", 20, 0, 0)]));

        state.CanFeed(stale, out var problem).Should().BeFalse();
        problem.Should().Be("That creature is already at its peak.");
        state.CanFeed(stale, TamerWireFeedSource.Essence, 1, out _).Should().BeFalse();
        state.FeedAction(Id(1), TamerWireFeedSource.Essence, 1).Should().BeNull();
        state.CanRename(stale, out _).Should().BeTrue();
        state.CanRelease(stale, out _).Should().BeTrue();
        state.RenameAction(Id(1), "Ember").Should().NotBeNull();
        state.ReleaseAction(Id(1)).Should().NotBeNull();
    }

    [Test]
    public void A_stale_view_after_becoming_protected_is_refused_by_guards_and_builders()
    {
        var state = Ready([Starter(Id(1))]);
        var stale = state.Monsters.Single();
        stale.Protected.Should().BeFalse();

        state.ApplySnapshot(Snapshot(monsters: [Monster(Id(1), "ember", "Ember", 1, 0, 100, isProtected: true)]));

        state.CanRename(stale, out _).Should().BeFalse();
        state.CanRelease(stale, out _).Should().BeFalse();
        state.CanFeed(stale, out _).Should().BeFalse();
        state.RenameAction(Id(1), "Ember").Should().BeNull();
        state.ReleaseAction(Id(1)).Should().BeNull();
        state.SetAbilityAction(Id(1), "a1").Should().BeNull();
        state.FeedAction(Id(1), TamerWireFeedSource.Essence, 1).Should().BeNull();
    }

    [Test]
    public void Ability_rows_follow_the_current_record_not_the_stale_view()
    {
        var species = Species(
            "ember",
            abilities:
            [
                new("a1", "First", 1, "First text."),
                new("a2", "Second", 3, "Second text.")
            ]);
        var state = Ready([Monster(Id(1), "ember", "Ember", 1, 0, 100, abilityKey: "a1")], species: [species]);
        var stale = state.Monsters.Single();

        state.ApplySnapshot(Snapshot(monsters: [Monster(Id(1), "ember", "Ember", 4, 50, 350, abilityKey: "a1")]));

        var rows = state.AbilityRows(stale);
        rows.Should().HaveCount(2);
        rows[1].State.Should().Be(TamerAbilityRowState.CanChoose);
    }

    [Test]
    public void A_protected_monster_with_a_known_species_offers_no_ability_choice()
    {
        var species = Species(
            "ember",
            abilities:
            [
                new("a1", "First", 1, "First text."),
                new("a2", "Second", 3, "Second text.")
            ]);
        var state = Ready([Monster(Id(1), "ember", "Ember", 4, 50, 350, traitKey: "missing", abilityKey: "a1", isProtected: true)], species: [species]);
        var held = state.Monsters.Single();

        held.Protected.Should().BeTrue();
        state.SpeciesOf(held).Should().NotBeNull();

        var rows = state.AbilityRows(held);
        rows.Should().HaveCount(2);
        rows.Should().NotContain(row => row.State == TamerAbilityRowState.CanChoose);
        rows[0].State.Should().Be(TamerAbilityRowState.Chosen);
        rows[1].State.Should().Be(TamerAbilityRowState.Locked);
        state.SetAbilityAction(Id(1), "a2").Should().BeNull();
        state.SetAbilityAction(Id(1), "a1").Should().BeNull();
    }

    [Test]
    public void Odd_saved_experience_uses_the_server_threshold()
    {
        var monster = Monster(Id(1), "ember", "Ember", 4, 450, -50);
        var state = new TamerStableState();
        state.ApplyCatalog(Catalog(perEssence: 20, perFeed: 100));
        state.ApplySnapshot(Snapshot(monsters: [monster], essence: 100000, feedHeld: 500));
        var held = state.Monsters.Single();

        held.Experience.Should().Be(450);
        held.ExperienceToNext.Should().Be(-50);
        state.ExperienceText(held).Should().Be("450 / 400");
        state.ExperienceFraction(held).Should().Be(1);
        state.FeedLimit(held, TamerWireFeedSource.Essence).Should().Be(898);
        state.FeedLimit(held, TamerWireFeedSource.Feed).Should().Be(180);
        state.FeedPreview(held, TamerWireFeedSource.Essence, 1).Should().Be(5);
        state.FeedPreview(held, TamerWireFeedSource.Essence, 100).Should().Be(8);
        state.FeedPreviewText(held, TamerWireFeedSource.Essence, 1).Should().Be("Level 4 \u2192 5");
    }

    [Test]
    public void Feed_limit_subtracts_excess_over_the_current_threshold()
    {
        var monster = Monster(Id(1), "ember", "Ember", 4, 450, -50);
        var state = new TamerStableState();
        state.ApplyCatalog(Catalog(perEssence: 20, perFeed: 100));
        state.ApplySnapshot(Snapshot(monsters: [monster], essence: 100000, feedHeld: 500));
        var held = state.Monsters.Single();

        state.CanFeed(held, TamerWireFeedSource.Essence, 898, out _).Should().BeTrue();
        state.CanFeed(held, TamerWireFeedSource.Essence, 899, out _).Should().BeFalse();
        state.CanFeed(held, TamerWireFeedSource.Feed, 180, out _).Should().BeTrue();
        state.CanFeed(held, TamerWireFeedSource.Feed, 181, out _).Should().BeFalse();
        state.FeedPreview(held, TamerWireFeedSource.Feed, 180).Should().Be(20);
    }

    [Test]
    public void Squad_actions_carry_squad_and_members_and_block_bad_shapes()
    {
        var members = new List<TamerDisplaySquadMember> { new(Id(1), 0, 0), new(Id(2), 6, 2) };
        var save = TamerStableState.SaveSquadAction(1, members);

        save!.Type.Should().Be(TamerActionType.SaveSquad);
        save.Squad.Should().Be(1);
        save.Members.Should().HaveCount(2);
        save.Members[0].MonsterId.Should().Be(Id(1));
        TamerStableState.SetActiveSquadAction(2)!.Type.Should().Be(TamerActionType.SetActiveSquad);
        TamerStableState.SetActiveSquadAction(2)!.Squad.Should().Be(2);
    }

    [Test]
    public void Squad_actions_block_over_full_bad_tiles_and_duplicates()
    {
        var six = Enumerable.Range(1, 6).Select(i => new TamerDisplaySquadMember(Id(i), (byte)i, 0)).ToList();

        TamerStableState.SaveSquadAction(3, []).Should().BeNull();
        TamerStableState.SaveSquadAction(0, six).Should().BeNull();
        TamerStableState.SaveSquadAction(0, [new(Id(1), 7, 0)]).Should().BeNull();
        TamerStableState.SaveSquadAction(0, [new(Id(1), 0, 3)]).Should().BeNull();
        TamerStableState.SaveSquadAction(0, [new(Id(1), 0, 0), new(Id(1), 1, 0)]).Should().BeNull();
        TamerStableState.SaveSquadAction(0, [new(Id(1), 0, 0), new(Id(2), 0, 0)]).Should().BeNull();
        TamerStableState.SaveSquadAction(0, [new(Guid.Empty, 0, 0)]).Should().BeNull();
        TamerStableState.SetActiveSquadAction(3).Should().BeNull();
        TamerStableState.SetActiveSquadAction(255).Should().BeNull();
    }
}

