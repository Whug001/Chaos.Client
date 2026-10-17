using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class TcgDeckDraftTests
{
    private static readonly Dictionary<string, TcgCatalogCardView> Catalog = new(StringComparer.OrdinalIgnoreCase)
    {
        ["imp"] = Card("imp", "Ember Imp", 2),
        ["bolt"] = Card("bolt", "Firebolt", 1),
        ["beetle"] = Card("beetle", "Canyon Beetle", 4),
        ["ant"] = Card("ant", "Army Ant", 4),
        ["free"] = Card("free", "Free Thing", 0),
        ["dragon"] = Card("dragon", "Elder Dragon", 9)
    };

    private static TcgCatalogCardView Card(string key, string name, int generic)
        => new(key, name, TcgElement.Fire, TcgRarity.Common, TcgCardType.Creature, (byte)generic, 0, 0, 0, 0, 1, 1, TcgKeyword.None, string.Empty, TcgArtKind.None, 0, TcgTargetKind.None);

    private static TcgCatalogCardView? Lookup(string key) => Catalog.GetValueOrDefault(key);

    private static TcgDeckDraft Draft(params string[] keys) => TcgDeckDraft.Edit(new TcgDeckView("Fire", [..keys], TcgDeckStatus.Incomplete, (byte)keys.Length));

    [Test]
    public void Default_names_count_up_past_taken_names()
    {
        TcgDeckDraft.DefaultName([]).Should().Be("Untitled deck");
        TcgDeckDraft.DefaultName(["untitled deck"]).Should().Be("Untitled deck 2");
        TcgDeckDraft.DefaultName(["Untitled deck", "Untitled deck 2"]).Should().Be("Untitled deck 3");
        TcgDeckDraft.DefaultName(["Untitled deck 2"]).Should().Be("Untitled deck");
        TcgDeckDraft.New(["Untitled deck"]).Name.Should().Be("Untitled deck 2");
    }

    [Test]
    public void Adding_needs_an_owned_copy_and_stops_at_three()
    {
        var draft = TcgDeckDraft.New([]);

        draft.TryAdd("imp", 0).Should().BeFalse();
        draft.AddProblem("imp", 0).Should().Be("You don't own this card.");

        draft.TryAdd("imp", 2).Should().BeTrue();
        draft.TryAdd("IMP", 2).Should().BeTrue();
        draft.TryAdd("imp", 2).Should().BeFalse();
        draft.AddProblem("imp", 2).Should().Be("You own only 2.");

        draft.TryAdd("bolt", 5).Should().BeTrue();
        draft.TryAdd("bolt", 5).Should().BeTrue();
        draft.TryAdd("bolt", 5).Should().BeTrue();
        draft.TryAdd("bolt", 5).Should().BeFalse();
        draft.AddProblem("bolt", 5).Should().Be("A deck can hold 3 copies of a card.");
        draft.InDeck("bolt").Should().Be(3);
    }

    [Test]
    public void A_deck_stops_at_forty_cards()
    {
        var draft = Draft([..Enumerable.Range(0, 40).Select(i => $"card{i}")]);

        draft.TryAdd("imp", 3).Should().BeFalse();
        draft.AddProblem("imp", 3).Should().Be("A deck holds 40 cards.");
        draft.Count.Should().Be(40);
    }

    [Test]
    public void Remove_takes_away_one_copy()
    {
        var draft = Draft("imp", "imp", "bolt");

        draft.Remove("IMP").Should().BeTrue();
        draft.InDeck("imp").Should().Be(1);
        draft.Remove("beetle").Should().BeFalse();
        draft.Count.Should().Be(2);
    }

    [Test]
    public void Rows_sort_by_cost_then_name_with_counts()
    {
        var draft = Draft("beetle", "imp", "ant", "imp", "bolt", "missing");

        draft.Rows(Lookup)
             .Should()
             .Equal(
                 new TcgDeckRow("bolt", "Firebolt", TcgElement.Fire, 1, 1),
                 new TcgDeckRow("imp", "Ember Imp", TcgElement.Fire, 2, 2),
                 new TcgDeckRow("ant", "Army Ant", TcgElement.Fire, 4, 1),
                 new TcgDeckRow("beetle", "Canyon Beetle", TcgElement.Fire, 4, 1),
                 new TcgDeckRow("missing", "missing", TcgElement.Neutral, 99, 1));
    }

    [Test]
    public void The_cost_curve_has_buckets_one_to_five_and_six_plus()
    {
        var draft = Draft("free", "bolt", "imp", "imp", "beetle", "dragon");

        draft.CostCurve(Lookup).Should().Equal(2, 2, 0, 1, 0, 1);
        TcgDeckDraft.Bucket(0).Should().Be(0);
        TcgDeckDraft.Bucket(6).Should().Be(5);
        TcgDeckDraft.Bucket(12).Should().Be(5);
        TcgDeckDraft.BucketLabel(0).Should().Be("1");
        TcgDeckDraft.BucketLabel(5).Should().Be("6+");
    }

    [Test]
    public void The_first_problem_is_size_then_ownership_then_ready()
    {
        var draft = Draft([..Enumerable.Repeat("imp", 3), ..Enumerable.Range(0, 34).Select(i => $"card{i}")]);

        draft.FirstProblem(_ => 3, Lookup).Should().Be("Needs 3 more cards");
        draft.CountText.Should().Be("37 / 40");

        draft.TryAdd("bolt", 3);
        draft.TryAdd("bolt", 3);
        draft.FirstProblem(_ => 3, Lookup).Should().Be("Needs 1 more card");

        draft.TryAdd("bolt", 3);
        draft.FirstProblem(key => key == "imp" ? 1 : 3, Lookup).Should().Be("You own only 1 Ember Imp");
        draft.FirstProblem(_ => 3, Lookup).Should().Be("Ready to play");
    }

    [Test]
    public void Names_must_be_one_to_twenty_four_characters()
    {
        var draft = TcgDeckDraft.New([]);
        draft.CanSave.Should().BeTrue();

        draft.Name = "   ";
        draft.NameProblem.Should().Be("The deck needs a name");
        draft.CanSave.Should().BeFalse();
        draft.FirstProblem(_ => 3, Lookup).Should().Be("The deck needs a name");

        draft.Name = new string('a', 25);
        draft.NameProblem.Should().Be("Names can be 24 characters");

        draft.Name = new string('a', 24);
        draft.NameProblem.Should().BeNull();
    }

    [Test]
    public void Unsaved_changes_track_the_name_and_cards_but_not_their_order()
    {
        var draft = Draft("imp", "bolt");
        draft.IsDirty.Should().BeFalse();

        draft.Remove("imp");
        draft.TryAdd("imp", 3);
        draft.IsDirty.Should().BeFalse();

        draft.TryAdd("imp", 3);
        draft.IsDirty.Should().BeTrue();
        draft.Remove("imp");
        draft.IsDirty.Should().BeFalse();

        draft.Name = " Fire ";
        draft.IsDirty.Should().BeFalse();
        draft.Name = "Blaze";
        draft.IsDirty.Should().BeTrue();

        TcgDeckDraft.New([]).IsDirty.Should().BeFalse();
    }

    [Test]
    public void A_save_has_landed_once_a_deck_has_its_name_and_cards()
    {
        var draft = Draft("imp", "bolt");
        draft.Name = " Blaze ";
        draft.TryAdd("bolt", 3);

        draft.IsSavedIn([new TcgDeckView("Fire", ["imp", "bolt"], TcgDeckStatus.Ready, 0)]).Should().BeFalse();
        draft.IsSavedIn([new TcgDeckView("Blaze", ["imp", "bolt"], TcgDeckStatus.Ready, 0)]).Should().BeFalse();
        draft.IsSavedIn([new TcgDeckView("blaze", ["bolt", "imp", "bolt"], TcgDeckStatus.Ready, 0)]).Should().BeTrue();
    }

    [Test]
    public void Saving_sends_the_old_name_the_new_name_and_the_keys()
    {
        var edited = Draft("imp");
        edited.Name = " Blaze ";

        edited.SaveAction()
              .Should()
              .BeEquivalentTo(
                  new TcgCollectionActionArgs
                  {
                      Type = TcgCollectionActionType.SaveDeck,
                      DeckName = "Fire",
                      NewName = "Blaze",
                      Keys = ["imp"]
                  });

        var created = TcgDeckDraft.New([]);
        created.IsNew.Should().BeTrue();
        created.SaveAction().DeckName.Should().BeEmpty();
        created.SaveAction().NewName.Should().Be("Untitled deck");
    }
}
