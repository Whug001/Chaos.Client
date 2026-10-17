using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class TcgCollectionStateTests
{
    private static TcgCatalogCardView Card(string key, TcgElement element, int generic, string? name = null, TcgRarity rarity = TcgRarity.Common)
        => new(
            key,
            name ?? key,
            element,
            rarity,
            TcgCardType.Creature,
            (byte)generic,
            0,
            0,
            0,
            0,
            1,
            1,
            TcgKeyword.None,
            string.Empty,
            TcgArtKind.None,
            0,
            TcgTargetKind.None);

    private static TcgCollectionDisplayArgs Snapshot(
        IEnumerable<TcgCatalogCardView> cards,
        IEnumerable<TcgOwnedView>? owned = null,
        int dust = 0,
        IEnumerable<TcgDeckView>? decks = null)
        => new()
        {
            Kind = TcgCollectionDisplayKind.Snapshot,
            Cards = [..cards],
            Owned = [..owned ?? []],
            Dust = dust,
            Decks = [..decks ?? []]
        };

    private static TcgCollectionState State(params TcgCatalogCardView[] cards)
    {
        var state = new TcgCollectionState();
        state.Apply(Snapshot(cards));

        return state;
    }

    [Test]
    public void Cards_sort_by_element_then_cost_then_name()
    {
        var state = State(
            Card("n1", TcgElement.Neutral, 1),
            Card("e1", TcgElement.Earth, 1),
            Card("f3", TcgElement.Fire, 3),
            Card("fb", TcgElement.Fire, 1, "Bravo"),
            Card("fa", TcgElement.Fire, 1, "alpha"),
            Card("w1", TcgElement.Water, 1),
            Card("i1", TcgElement.Wind, 1));

        state.Cards.Select(card => card.Key).Should().Equal("fa", "fb", "f3", "w1", "i1", "e1", "n1");
    }

    [Test]
    public void Tabs_filter_by_element_and_the_decks_tab_shows_no_cards()
    {
        var state = State(Card("f1", TcgElement.Fire, 1), Card("w1", TcgElement.Water, 1), Card("n1", TcgElement.Neutral, 1));

        state.SetTab(TcgBinderTab.Water);
        state.Filtered.Select(card => card.Key).Should().Equal("w1");

        state.SetTab(TcgBinderTab.Neutral);
        state.Filtered.Select(card => card.Key).Should().Equal("n1");

        state.SetTab(TcgBinderTab.Decks);
        state.Filtered.Should().BeEmpty();

        state.SetTab(TcgBinderTab.All);
        state.Filtered.Should().HaveCount(3);
    }

    [Test]
    public void Owned_only_hides_unowned_cards_and_goes_back_to_the_first_page()
    {
        var cards = Enumerable.Range(0, 30).Select(i => Card($"c{i:D2}", TcgElement.Fire, 1)).ToList();
        var state = new TcgCollectionState();
        state.Apply(Snapshot(cards, [new TcgOwnedView("c05", 1, 0), new TcgOwnedView("c06", 0, 1)]));
        state.NextPage();

        state.SetOwnedOnly(true);

        state.Page.Should().Be(0);
        state.Filtered.Select(card => card.Key).Should().Equal("c05", "c06");
        state.PageText.Should().Be("Page 1 of 1 - Owned 2 of 2");
    }

    [Test]
    public void Pages_hold_twelve_cards_and_stop_at_the_ends()
    {
        var cards = Enumerable.Range(0, 30).Select(i => Card($"c{i:D2}", TcgElement.Fire, 1)).ToList();
        var state = new TcgCollectionState();
        state.Apply(Snapshot(cards, [new TcgOwnedView("c00", 2, 0)]));

        state.PageCount.Should().Be(3);
        state.PageCards.Should().HaveCount(12);
        state.PreviousPage();
        state.Page.Should().Be(0);

        for (var i = 0; i < 5; i++)
            state.NextPage();

        state.Page.Should().Be(2);
        state.PageCards.Select(card => card.Key).Should().Equal("c24", "c25", "c26", "c27", "c28", "c29");
        state.PageText.Should().Be("Page 3 of 3 - Owned 1 of 30");
        TcgCollectionState.PageCountOf(0).Should().Be(1);
    }

    [Test]
    public void A_new_snapshot_keeps_the_selection_only_while_the_card_exists()
    {
        var cards = Enumerable.Range(0, 30).Select(i => Card($"c{i:D2}", TcgElement.Fire, 1)).ToList();
        var state = new TcgCollectionState();
        state.Apply(Snapshot(cards));
        state.NextPage();
        state.NextPage();
        state.Select("c29");

        state.Apply(Snapshot(cards));
        state.SelectedKey.Should().Be("c29");

        state.Apply(Snapshot(cards.Take(5)));
        state.SelectedKey.Should().BeNull();
        state.Page.Should().Be(0);

        state.Select("nosuchcard");
        state.SelectedKey.Should().BeNull();
    }

    [Test]
    public void Counts_add_normal_and_foil()
    {
        var state = new TcgCollectionState();
        state.Apply(Snapshot([Card("imp", TcgElement.Fire, 1)], [new TcgOwnedView("IMP", 2, 1)], 75));

        state.Count("imp", false).Should().Be(2);
        state.Count("imp", true).Should().Be(1);
        state.Copies("imp").Should().Be(3);
        state.Copies("other").Should().Be(0);
        state.Dust.Should().Be(75);
        state.HasSnapshot.Should().BeTrue();
    }

    [Test]
    public void Break_needs_more_than_three_copies_and_one_of_that_kind()
    {
        var state = new TcgCollectionState();
        state.Apply(Snapshot([Card("imp", TcgElement.Fire, 1)], [new TcgOwnedView("imp", 4, 0)]));

        state.CanBreak("imp", false).Should().BeTrue();
        state.CanBreak("imp", true).Should().BeFalse();

        state.Apply(Snapshot([Card("imp", TcgElement.Fire, 1)], [new TcgOwnedView("imp", 3, 0)]));
        state.CanBreak("imp", false).Should().BeFalse();

        state.Apply(Snapshot([Card("imp", TcgElement.Fire, 1)], [new TcgOwnedView("imp", 3, 1)]));
        state.CanBreak("imp", false).Should().BeTrue();
        state.CanBreak("imp", true).Should().BeTrue();
        state.CanPullOut("imp", true).Should().BeTrue();
    }

    [Test]
    public void Craft_needs_room_for_a_copy_and_enough_dust()
    {
        var imp = Card("imp", TcgElement.Fire, 1);
        var state = new TcgCollectionState();

        state.Apply(Snapshot([imp], [], 39));
        state.CanCraft("imp").Should().BeFalse();

        state.Apply(Snapshot([imp], [], 40));
        state.CanCraft("imp").Should().BeTrue();

        state.Apply(Snapshot([imp], [new TcgOwnedView("imp", 2, 1)], 4000));
        state.CanCraft("imp").Should().BeFalse();
        state.CanCraft("nosuchcard").Should().BeFalse();
    }

    [Test]
    public void Pull_out_warns_about_each_deck_left_short()
    {
        var state = new TcgCollectionState();

        state.Apply(
            Snapshot(
                [Card("imp", TcgElement.Fire, 1)],
                [new TcgOwnedView("imp", 2, 0)],
                decks:
                [
                    new TcgDeckView("Fire Rush", ["imp", "imp"], TcgDeckStatus.Incomplete, 2),
                    new TcgDeckView("Embers", ["imp"], TcgDeckStatus.Incomplete, 1),
                    new TcgDeckView("Blaze", ["imp", "IMP"], TcgDeckStatus.Incomplete, 2)
                ]));

        state.PullOutWarning("imp").Should().Be("Fire Rush and Blaze will be missing a card.");

        state.Apply(Snapshot([Card("imp", TcgElement.Fire, 1)], [new TcgOwnedView("imp", 3, 0)], decks: [new TcgDeckView("Fire Rush", ["imp", "imp"], TcgDeckStatus.Incomplete, 2)]));
        state.PullOutWarning("imp").Should().BeNull();
    }

    [Test]
    public void Questions_and_deck_status_read_as_the_spec_says()
    {
        var imp = Card("imp", TcgElement.Fire, 1, "Ember Imp");
        var state = State(imp);

        state.BreakQuestion(imp, false).Should().Be("Break Ember Imp for 5 dust?");
        state.BreakQuestion(imp, true).Should().Be("Break Ember Imp (Foil) for 20 dust?");
        state.CraftQuestion(imp).Should().Be("Craft Ember Imp for 40 dust?");
        TcgCollectionState.StatusText(new TcgDeckView("a", [], TcgDeckStatus.Ready, 0)).Should().Be("Ready");
        TcgCollectionState.StatusText(new TcgDeckView("a", [], TcgDeckStatus.Missing, 1)).Should().Be("Missing 1 card");
        TcgCollectionState.StatusText(new TcgDeckView("a", [], TcgDeckStatus.Missing, 2)).Should().Be("Missing 2 cards");
        TcgCollectionState.StatusText(new TcgDeckView("a", [], TcgDeckStatus.Incomplete, 31)).Should().Be("31/40");
    }

    [Test]
    public void Dust_numbers_match_the_server()
    {
        TcgCollectionState.CraftCost(TcgRarity.Common).Should().Be(40);
        TcgCollectionState.CraftCost(TcgRarity.Uncommon).Should().Be(100);
        TcgCollectionState.CraftCost(TcgRarity.Rare).Should().Be(400);
        TcgCollectionState.CraftCost(TcgRarity.Legendary).Should().Be(1600);
        TcgCollectionState.BreakValue(TcgRarity.Common, false).Should().Be(5);
        TcgCollectionState.BreakValue(TcgRarity.Uncommon, false).Should().Be(20);
        TcgCollectionState.BreakValue(TcgRarity.Rare, false).Should().Be(100);
        TcgCollectionState.BreakValue(TcgRarity.Legendary, false).Should().Be(400);
        TcgCollectionState.BreakValue(TcgRarity.Legendary, true).Should().Be(1600);
    }

    [Test]
    public void Only_snapshots_change_the_state()
    {
        var state = State(Card("imp", TcgElement.Fire, 1));

        state.Apply(new TcgCollectionDisplayArgs { Kind = TcgCollectionDisplayKind.PackOpened, Cards = [] });
        state.Apply(new TcgCollectionDisplayArgs { Kind = TcgCollectionDisplayKind.Close, Message = "Gone." });

        state.Cards.Should().ContainSingle();
        state.Message.Should().BeEmpty();
    }

    [Test]
    public void Requests_carry_their_card_and_deck()
    {
        TcgCollectionState.OpenAction().Type.Should().Be(TcgCollectionActionType.Open);
        TcgCollectionState.CloseAction().Type.Should().Be(TcgCollectionActionType.Close);
        TcgCollectionState.PullOutAction("imp", true).Should().BeEquivalentTo(new TcgCollectionActionArgs { Type = TcgCollectionActionType.PullOut, CardKey = "imp", Foil = true });
        TcgCollectionState.BreakAction("imp", false).Should().BeEquivalentTo(new TcgCollectionActionArgs { Type = TcgCollectionActionType.Break, CardKey = "imp" });
        TcgCollectionState.CraftAction("imp").Should().BeEquivalentTo(new TcgCollectionActionArgs { Type = TcgCollectionActionType.Craft, CardKey = "imp" });
        TcgCollectionState.RenameAction("Fire", "Blaze").Should().BeEquivalentTo(new TcgCollectionActionArgs { Type = TcgCollectionActionType.RenameDeck, DeckName = "Fire", NewName = "Blaze" });
        TcgCollectionState.DeleteAction("Fire").Should().BeEquivalentTo(new TcgCollectionActionArgs { Type = TcgCollectionActionType.DeleteDeck, DeckName = "Fire" });
    }
}
