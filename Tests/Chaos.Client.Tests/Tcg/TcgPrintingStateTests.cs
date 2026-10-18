using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class TcgPrintingStateTests
{
    private static TcgCatalogCardView Card(string key, TcgRarity rarity = TcgRarity.Rare, string? name = null)
        => new(
            key,
            name ?? key,
            TcgElement.Fire,
            rarity,
            TcgCardType.Creature,
            1,
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

    private static TcgCollectionState State(TcgCatalogCardView card, TcgOwnedView owned, int dust = 0)
    {
        var state = new TcgCollectionState();

        state.Apply(
            new TcgCollectionDisplayArgs
            {
                Kind = TcgCollectionDisplayKind.Snapshot,
                Cards = [card],
                Owned = [owned],
                Dust = dust
            });

        return state;
    }

    [Test]
    public void The_style_switch_shows_each_styles_counts()
    {
        var state = State(Card("golem"), new TcgOwnedView("golem", 2, 1, 0, 3, 1, 0));

        state.StyleText("golem", TcgCardStyle.Normal).Should().Be("Normal: 2, foil 1");
        state.StyleText("golem", TcgCardStyle.Extended).Should().Be("Extended: 0, foil 3");
        state.StyleText("golem", TcgCardStyle.Full).Should().Be("Full art: 1, foil 0");
        TcgCollectionState.Styles(Card("golem")).Should().Equal(TcgCardStyle.Normal, TcgCardStyle.Extended, TcgCardStyle.Full);
        TcgCollectionState.Styles(Card("imp", TcgRarity.Common)).Should().Equal(TcgCardStyle.Normal, TcgCardStyle.Extended);
    }

    [Test]
    public void Selecting_a_card_shows_its_best_printing()
    {
        var state = State(Card("golem"), new TcgOwnedView("golem", 2, 0, 0, 1));

        state.BestPrinting("golem").Should().Be(TcgPrinting.ExtendedFoil);
        state.BestPrinting("nosuchcard").Should().BeNull();
        state.Select("golem");
        state.DetailStyle.Should().Be(TcgCardStyle.Extended);
        state.SetDetailStyle(TcgCardStyle.Normal);
        state.DetailStyle.Should().Be(TcgCardStyle.Normal);
    }

    [Test]
    public void The_deck_builder_shows_the_printing_each_first_copy_gets()
    {
        var state = State(Card("golem"), new TcgOwnedView("golem", 3, 0, 1, 0, 0, 1));

        state.AssignPrintings(["golem", "golem", "golem"]).Should().Equal(TcgPrinting.FullFoil, TcgPrinting.Extended, TcgPrinting.Normal);
        state.DeckFacePrinting(["golem"], "golem").Should().Be(TcgPrinting.FullFoil);
        state.DeckFacePrinting([], "golem").Should().Be(TcgPrinting.FullFoil);
        state.DeckFacePrinting([], "ghost").Should().Be(TcgPrinting.Normal);
    }

    [Test]
    public void Dust_follows_style_and_foil_like_the_server()
    {
        TcgCollectionState.BreakValue(TcgRarity.Rare, TcgPrinting.Normal).Should().Be(100);
        TcgCollectionState.BreakValue(TcgRarity.Rare, TcgPrinting.Extended).Should().Be(200);
        TcgCollectionState.BreakValue(TcgRarity.Rare, TcgPrinting.Full).Should().Be(400);
        TcgCollectionState.BreakValue(TcgRarity.Rare, TcgPrinting.FullFoil).Should().Be(1600);
        TcgCollectionState.BreakValue(TcgRarity.Mythic, TcgPrinting.Full).Should().Be(1600);
        TcgCollectionState.BreakValue(TcgRarity.Mythic, TcgPrinting.FullFoil).Should().Be(6400);
        State(Card("morwenna", TcgRarity.Mythic), new TcgOwnedView("morwenna", 0, 0, 0, 0, 1, 0), 100_000).CanCraft("morwenna").Should().BeFalse();
        TcgCollectionState.Craftable(TcgRarity.Mythic).Should().BeFalse();
    }

    [Test]
    public void Requests_carry_the_style()
    {
        var golem = Card("golem", name: "Stone Golem");

        TcgCollectionState.PullOutAction("golem", TcgPrinting.ExtendedFoil)
                          .Should()
                          .BeEquivalentTo(new TcgCollectionActionArgs { Type = TcgCollectionActionType.PullOut, CardKey = "golem", Foil = true, Style = TcgCardStyle.Extended });
        TcgCollectionState.BreakAction("golem", TcgPrinting.Full)
                          .Should()
                          .BeEquivalentTo(new TcgCollectionActionArgs { Type = TcgCollectionActionType.Break, CardKey = "golem", Style = TcgCardStyle.Full });
        State(golem, new TcgOwnedView("golem", 4, 0, 0, 0, 1, 0)).BreakQuestion(golem, TcgPrinting.Full).Should().Be("Break Stone Golem (Full Art) for 400 dust?");
    }

    [Test]
    public void A_mythic_glows_purple_in_the_reveal()
    {
        var reveal = new TcgRevealState();

        reveal.Start(
            new TcgCollectionDisplayArgs
            {
                Kind = TcgCollectionDisplayKind.PackOpened,
                Cards = [Card("morwenna", TcgRarity.Mythic)],
                Pack =
                [
                    new TcgPackCardView("morwenna", TcgRarity.Mythic, false, true, TcgCardStyle.Full),
                    new TcgPackCardView("morwenna", TcgRarity.Mythic, true, false, TcgCardStyle.Full)
                ]
            });

        reveal.Glow(0).Should().Be(TcgRevealGlow.Mythic);
        reveal.Glow(1).Should().Be(TcgRevealGlow.MythicFoil);
        reveal.Cards[0].Printing.Should().Be(TcgPrinting.Full);
    }

    [Test]
    public void A_deck_takes_one_copy_of_a_mythic()
    {
        var draft = TcgDeckDraft.New([]);

        draft.TryAdd("morwenna", 2, TcgRarity.Mythic).Should().BeTrue();
        draft.TryAdd("morwenna", 2, TcgRarity.Mythic).Should().BeFalse();
        draft.AddProblem("morwenna", 2, TcgRarity.Mythic).Should().Be("A deck can hold 1 copy of a Mythic card.");
        draft.TryAdd("golem", 3, TcgRarity.Rare).Should().BeTrue();
        draft.TryAdd("golem", 3, TcgRarity.Rare).Should().BeTrue();
    }

    [Test]
    public void A_match_update_keeps_printings_and_shields()
    {
        var state = new TcgMatchState();
        var update = new TcgDisplayArgs
        {
            Type = TcgDisplayType.Update,
            Players =
            [
                new TcgPlayerView { Name = "Alice", Hand = [new TcgCardView(1, "bear", TcgPrinting.FullFoil)] },
                new TcgPlayerView { Name = "Bob", Shielded = true }
            ],
            Stack = [new TcgStackView(9, "zap", TcgSeat.B, -1, false, TcgPrinting.Extended)]
        };

        state.Apply(update, DateTime.UtcNow, key => key);

        state.Latest!.Players[0].Hand.Single().Printing.Should().Be(TcgPrinting.FullFoil);
        state.Latest.Players[1].Shielded.Should().BeTrue();
        state.Latest.Stack.Single().Printing.Should().Be(TcgPrinting.Extended);
    }

    [Test]
    public void Ante_text_names_the_printing()
        => TcgMatchText.StakesResult(
                           TcgEndOutcome.Victory,
                           TcgStakes.Ante,
                           0,
                           [new TcgAnteView(TcgSeat.A, "bear", false), new TcgAnteView(TcgSeat.B, "bolt", true, TcgCardStyle.Full)],
                           TcgSeat.A,
                           key => key)
                       .Should()
                       .Be("You won bolt (full art, foil) and kept bear.");
}
