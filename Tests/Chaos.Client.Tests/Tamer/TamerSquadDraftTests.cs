using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class TamerSquadDraftTests
{
    private static Guid Id(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:D12}");

    private static TamerSpeciesView Species(string key, TamerWireElement element, string family)
        => new(key, key, 100, TamerWireRarity.Common, element, family, TamerWireRole.Bruiser, 1, 20, 10, 5, 8, 3, 2, 1, 1, [new("a1", "First", 1, "Text.")]);

    private static TamerMonsterView Monster(Guid id, string speciesKey, bool isProtected = false)
        => new(id, speciesKey, speciesKey, 1, 0, 100, "trait1", "a1", isProtected, 5);

    private static TamerDisplayArgs Catalog(List<TamerSpeciesView> species, List<TamerBonusView>? bonuses = null)
        => new()
        {
            Kind = TamerDisplayKind.Catalog,
            CatalogPartNumber = 0,
            CatalogPartCount = 1,
            Species = species,
            Traits = [new("trait1", "Kindled", "Grows stronger.")],
            Bonuses = bonuses ?? [],
            StableCapacity = 60,
            ExperiencePerEssence = 20,
            ExperiencePerFeed = 100
        };

    private static TamerDisplayArgs Snapshot(List<TamerMonsterView> monsters, byte slots = 3)
        => new()
        {
            Kind = TamerDisplayKind.Snapshot,
            Essence = 100,
            SquadSlots = slots,
            FeedItemsHeld = 2,
            ActiveSquad = 0,
            Squads = [[], [], []],
            Monsters = monsters
        };

    private static TamerStableState State(
        List<TamerMonsterView> monsters,
        List<TamerSpeciesView> species,
        byte slots = 3,
        List<TamerBonusView>? bonuses = null)
    {
        var state = new TamerStableState();
        state.ApplyCatalog(Catalog(species, bonuses));
        state.ApplySnapshot(Snapshot(monsters, slots));

        return state;
    }

    private static TamerBonusView ElementBonus(string key, TamerWireElement element, byte count)
        => new(key, key, "Text.", TamerWireBonusScope.Members, count, true, element, string.Empty);

    private static TamerBonusView FamilyBonus(string key, string family, byte count)
        => new(key, key, "Text.", TamerWireBonusScope.Members, count, false, TamerWireElement.Fire, family);

    [Test]
    public void Pick_and_place_puts_a_bench_monster_on_the_grid()
    {
        var draft = new TamerSquadDraft([]);

        draft.Pick(Id(1));
        draft.Picked.Should().Be(Id(1));

        draft.PlaceAt(2, 1);

        draft.Members.Should().ContainSingle().Which.MonsterId.Should().Be(Id(1));
        draft.Members[0].Column.Should().Be((byte)2);
        draft.Members[0].Row.Should().Be((byte)1);
        draft.Picked.Should().BeNull();
        draft.SlotsUsed.Should().Be(1);
    }

    [Test]
    public void Pick_and_place_moves_a_placed_monster()
    {
        var draft = new TamerSquadDraft([new(Id(1), 0, 0)]);

        draft.Pick(Id(1));
        draft.PlaceAt(3, 2);

        draft.Members.Should().ContainSingle();
        draft.Members[0].Column.Should().Be((byte)3);
        draft.Members[0].Row.Should().Be((byte)2);
    }

    [Test]
    public void Pick_and_place_on_another_monster_swaps_them()
    {
        var draft = new TamerSquadDraft([new(Id(1), 0, 0), new(Id(2), 3, 1)]);

        draft.Pick(Id(1));
        draft.PlaceAt(3, 1);

        draft.Members.Should().HaveCount(2);
        draft.Members.Single(m => m.MonsterId == Id(1)).Column.Should().Be((byte)3);
        draft.Members.Single(m => m.MonsterId == Id(1)).Row.Should().Be((byte)1);
        draft.Members.Single(m => m.MonsterId == Id(2)).Column.Should().Be((byte)0);
        draft.Members.Single(m => m.MonsterId == Id(2)).Row.Should().Be((byte)0);
    }

    [Test]
    public void Remove_returns_a_placed_monster_to_the_bench()
    {
        var draft = new TamerSquadDraft([new(Id(1), 0, 0), new(Id(2), 1, 0)]);

        draft.Pick(Id(1));
        draft.Remove();

        draft.Members.Should().ContainSingle().Which.MonsterId.Should().Be(Id(2));
        draft.Picked.Should().BeNull();
    }

    [Test]
    public void Cancel_pick_drops_the_pick_and_place_without_a_pick_does_nothing()
    {
        var draft = new TamerSquadDraft([]);

        draft.PlaceAt(0, 0);
        draft.Members.Should().BeEmpty();

        draft.Pick(Id(1));
        draft.CancelPick();
        draft.Picked.Should().BeNull();

        draft.PlaceAt(0, 0);
        draft.Members.Should().BeEmpty();
    }

    [Test]
    public void Place_at_ignores_out_of_bounds_and_unknown_picks()
    {
        var draft = new TamerSquadDraft([]);

        draft.Pick(Guid.Empty);
        draft.Picked.Should().BeNull();

        draft.Pick(Id(1));
        draft.PlaceAt(7, 0);
        draft.Members.Should().BeEmpty();
        draft.Picked.Should().Be(Id(1));

        draft.PlaceAt(0, 3);
        draft.Members.Should().BeEmpty();
        draft.PlaceAt(-1, 0);
        draft.Members.Should().BeEmpty();

        draft.PlaceAt(6, 2);
        draft.Members.Should().ContainSingle();
    }

    [Test]
    public void Bench_is_every_monster_not_in_the_draft()
    {
        var state = State(
            [Monster(Id(1), "ember"), Monster(Id(2), "tide"), Monster(Id(3), "leaf")],
            [Species("ember", TamerWireElement.Fire, "Beast"), Species("tide", TamerWireElement.Water, "Beast"), Species("leaf", TamerWireElement.Wind, "Plant")]);
        var draft = new TamerSquadDraft([new(Id(1), 0, 0)]);

        var bench = draft.Bench(state);

        bench.Select(m => m.Id).Should().NotContain(Id(1));
        bench.Should().HaveCount(2);

        draft.Pick(Id(3));
        draft.PlaceAt(1, 0);

        draft.Bench(state).Select(m => m.Id).Should().Equal(Id(2));
    }

    [Test]
    public void Can_place_refuses_a_full_squad_with_the_slot_count()
    {
        var state = State(
            [Monster(Id(1), "ember"), Monster(Id(2), "tide"), Monster(Id(3), "leaf"), Monster(Id(4), "stone")],
            [Species("ember", TamerWireElement.Fire, "Beast"), Species("tide", TamerWireElement.Water, "Beast"), Species("leaf", TamerWireElement.Wind, "Plant"), Species("stone", TamerWireElement.Earth, "Rock")],
            slots: 3);
        var draft = new TamerSquadDraft([new(Id(1), 0, 0), new(Id(2), 1, 0), new(Id(3), 2, 0)]);

        draft.CanPlace(state, state.Monsters.Single(m => m.Id == Id(4)), out var reason).Should().BeFalse();
        reason.Should().Be("3 of 3 slots used");
    }

    [Test]
    public void Can_place_refuses_two_of_one_species()
    {
        var state = State(
            [Monster(Id(1), "ember"), Monster(Id(2), "ember")],
            [Species("ember", TamerWireElement.Fire, "Beast")]);
        var draft = new TamerSquadDraft([new(Id(1), 0, 0)]);

        draft.CanPlace(state, state.Monsters.Single(m => m.Id == Id(2)), out var reason).Should().BeFalse();
        reason.Should().Be("Two creatures of one kind cannot share a squad.");
    }

    [Test]
    public void Can_place_refuses_protected_gone_and_already_placed_monsters()
    {
        var state = State(
            [Monster(Id(1), "ember"), Monster(Id(2), "ember", isProtected: true)],
            [Species("ember", TamerWireElement.Fire, "Beast")]);
        var draft = new TamerSquadDraft([new(Id(1), 0, 0)]);

        draft.CanPlace(state, state.Monsters.Single(m => m.Id == Id(2)), out _).Should().BeFalse();
        draft.CanPlace(state, Monster(Id(9), "ember"), out _).Should().BeFalse();
        draft.CanPlace(state, state.Monsters.Single(m => m.Id == Id(1)), out var placed).Should().BeFalse();
        placed.Should().Be("That creature is already in the squad.");
        draft.CanPlace(state, null, out _).Should().BeFalse();
    }

    [Test]
    public void Is_changed_tracks_place_move_and_remove_and_revert_clears_it()
    {
        var draft = new TamerSquadDraft([new(Id(1), 0, 0)]);

        draft.IsChanged.Should().BeFalse();

        draft.Pick(Id(2));
        draft.PlaceAt(1, 0);
        draft.IsChanged.Should().BeTrue();

        draft.Revert();
        draft.IsChanged.Should().BeFalse();
        draft.Members.Should().ContainSingle().Which.MonsterId.Should().Be(Id(1));
        draft.Picked.Should().BeNull();

        draft.Pick(Id(1));
        draft.Remove();
        draft.IsChanged.Should().BeTrue();

        draft.Revert();
        draft.IsChanged.Should().BeFalse();
        draft.Members.Should().ContainSingle();
    }

    [Test]
    public void Moving_back_to_the_original_tile_is_not_changed()
    {
        var draft = new TamerSquadDraft([new(Id(1), 0, 0)]);

        draft.Pick(Id(1));
        draft.PlaceAt(2, 2);
        draft.IsChanged.Should().BeTrue();

        draft.Pick(Id(1));
        draft.PlaceAt(0, 0);
        draft.IsChanged.Should().BeFalse();
    }

    [Test]
    public void A_save_action_is_empty_when_nothing_changed_but_valid()
    {
        var state = State(
            [Monster(Id(1), "ember")],
            [Species("ember", TamerWireElement.Fire, "Beast")]);
        var draft = new TamerSquadDraft([new(Id(1), 0, 0)]);

        var action = draft.ToAction(state, 0);

        action.Should().NotBeNull();
        action!.Type.Should().Be(TamerActionType.SaveSquad);
        action.Squad.Should().Be(0);
        action.Members.Should().ContainSingle();
    }

    [Test]
    public void To_action_sends_the_draft_members_for_the_shown_squad()
    {
        var state = State(
            [Monster(Id(1), "ember"), Monster(Id(2), "tide")],
            [Species("ember", TamerWireElement.Fire, "Beast"), Species("tide", TamerWireElement.Water, "Beast")]);
        var draft = new TamerSquadDraft([]);
        draft.Pick(Id(1));
        draft.PlaceAt(0, 0);
        draft.Pick(Id(2));
        draft.PlaceAt(6, 2);

        var action = draft.ToAction(state, 2);

        action.Should().NotBeNull();
        action!.Squad.Should().Be(2);
        action.Members.Should().HaveCount(2);
        draft.ToAction(state, 3).Should().BeNull();
    }

    [Test]
    public void To_action_is_blocked_for_over_full_duplicate_species_and_protected_drafts()
    {
        var over = State(
            [Monster(Id(1), "ember"), Monster(Id(2), "tide"), Monster(Id(3), "leaf"), Monster(Id(4), "stone")],
            [Species("ember", TamerWireElement.Fire, "Beast"), Species("tide", TamerWireElement.Water, "Beast"), Species("leaf", TamerWireElement.Wind, "Plant"), Species("stone", TamerWireElement.Earth, "Rock")],
            slots: 3);
        var full = new TamerSquadDraft([new(Id(1), 0, 0), new(Id(2), 1, 0), new(Id(3), 2, 0), new(Id(4), 3, 0)]);
        full.ToAction(over, 0).Should().BeNull();

        var dupeState = State(
            [Monster(Id(1), "ember"), Monster(Id(2), "ember")],
            [Species("ember", TamerWireElement.Fire, "Beast")]);
        var dupe = new TamerSquadDraft([new(Id(1), 0, 0), new(Id(2), 1, 0)]);
        dupe.ToAction(dupeState, 0).Should().BeNull();

        var lockedState = State(
            [Monster(Id(1), "ember", isProtected: true)],
            [Species("ember", TamerWireElement.Fire, "Beast")]);
        var locked = new TamerSquadDraft([new(Id(1), 0, 0)]);
        locked.ToAction(lockedState, 0).Should().BeNull();
    }

    [Test]
    public void Reconcile_drops_released_monsters_and_keeps_the_rest()
    {
        var before = State(
            [Monster(Id(1), "ember"), Monster(Id(2), "tide"), Monster(Id(3), "leaf")],
            [Species("ember", TamerWireElement.Fire, "Beast"), Species("tide", TamerWireElement.Water, "Beast"), Species("leaf", TamerWireElement.Wind, "Plant")]);
        var draft = new TamerSquadDraft([new(Id(1), 0, 0), new(Id(2), 1, 0)]);
        draft.Pick(Id(3));
        draft.PlaceAt(2, 0);
        draft.IsChanged.Should().BeTrue();

        var after = State(
            [Monster(Id(1), "ember"), Monster(Id(3), "leaf")],
            [Species("ember", TamerWireElement.Fire, "Beast"), Species("leaf", TamerWireElement.Wind, "Plant")]);
        draft.Reconcile(after);

        draft.Members.Select(m => m.MonsterId).Should().Equal(Id(1), Id(3));
        draft.Members.Single(m => m.MonsterId == Id(1)).Column.Should().Be((byte)0);
        draft.Members.Single(m => m.MonsterId == Id(3)).Column.Should().Be((byte)2);
        draft.Bench(after).Select(m => m.Id).Should().BeEmpty();
    }

    [Test]
    public void Reconcile_clears_a_pick_for_a_released_monster()
    {
        var before = State(
            [Monster(Id(1), "ember"), Monster(Id(2), "tide")],
            [Species("ember", TamerWireElement.Fire, "Beast"), Species("tide", TamerWireElement.Water, "Beast")]);
        var draft = new TamerSquadDraft([new(Id(1), 0, 0)]);
        draft.Pick(Id(2));

        var after = State([Monster(Id(1), "ember")], [Species("ember", TamerWireElement.Fire, "Beast")]);
        draft.Reconcile(after);

        draft.Picked.Should().BeNull();
        draft.Bench(after).Should().BeEmpty();

        draft.Reconcile(before);
        draft.Members.Select(m => m.MonsterId).Should().Equal(Id(1));
    }

    [Test]
    public void Set_bonuses_keep_only_the_highest_tier_per_element()
    {
        var bonuses = new List<TamerBonusView> { ElementBonus("fire2", TamerWireElement.Fire, 2), ElementBonus("fire3", TamerWireElement.Fire, 3) };
        var state = State(
            [Monster(Id(1), "ember"), Monster(Id(2), "flame")],
            [Species("ember", TamerWireElement.Fire, "Beast"), Species("flame", TamerWireElement.Fire, "Flame")],
            bonuses: bonuses);
        var draft = new TamerSquadDraft([new(Id(1), 0, 0), new(Id(2), 1, 0)]);

        draft.SetBonuses(state).Select(b => b.Key).Should().Equal("fire2");
    }

    [Test]
    public void Set_bonuses_keep_only_the_highest_tier_per_family()
    {
        var bonuses = new List<TamerBonusView> { FamilyBonus("beast2", "Beast", 2), FamilyBonus("beast3", "Beast", 3) };
        var state = State(
            [Monster(Id(1), "ember"), Monster(Id(2), "tide")],
            [Species("ember", TamerWireElement.Fire, "Beast"), Species("tide", TamerWireElement.Water, "Beast")],
            bonuses: bonuses);
        var draft = new TamerSquadDraft([new(Id(1), 0, 0), new(Id(2), 1, 0)]);

        draft.SetBonuses(state).Select(b => b.Key).Should().Equal("beast2");
    }

    [Test]
    public void Set_bonuses_give_the_higher_tier_at_three_and_nothing_when_unmet()
    {
        var bonuses = new List<TamerBonusView> { ElementBonus("fire2", TamerWireElement.Fire, 2), ElementBonus("fire3", TamerWireElement.Fire, 3) };
        var state = State(
            [Monster(Id(1), "ember"), Monster(Id(2), "flame"), Monster(Id(3), "blaze")],
            [Species("ember", TamerWireElement.Fire, "Beast"), Species("flame", TamerWireElement.Fire, "Flame"), Species("blaze", TamerWireElement.Fire, "Blaze")],
            bonuses: bonuses);
        var three = new TamerSquadDraft([new(Id(1), 0, 0), new(Id(2), 1, 0), new(Id(3), 2, 0)]);
        three.SetBonuses(state).Select(b => b.Key).Should().Equal("fire3");

        var one = new TamerSquadDraft([new(Id(1), 0, 0)]);
        one.SetBonuses(state).Should().BeEmpty();
    }

    [Test]
    public void Set_bonuses_cover_elements_and_families_but_skip_missing_monsters()
    {
        var bonuses = new List<TamerBonusView> { ElementBonus("fire2", TamerWireElement.Fire, 2), FamilyBonus("beast2", "Beast", 2) };
        var state = State(
            [Monster(Id(1), "ember"), Monster(Id(2), "flame")],
            [Species("ember", TamerWireElement.Fire, "Beast"), Species("flame", TamerWireElement.Fire, "Beast")],
            bonuses: bonuses);
        var draft = new TamerSquadDraft([new(Id(1), 0, 0), new(Id(2), 1, 0), new(Id(9), 2, 0)]);

        draft.SetBonuses(state).Select(b => b.Key).Should().BeEquivalentTo("fire2", "beast2");
    }

    [Test]
    public void Set_bonuses_are_empty_without_a_catalog_or_a_squad()
    {
        var state = State(
            [Monster(Id(1), "ember")],
            [Species("ember", TamerWireElement.Fire, "Beast")]);
        var empty = new TamerSquadDraft([]);

        empty.SetBonuses(state).Should().BeEmpty();
        empty.Bench(state).Should().HaveCount(1);
    }
}
