using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class TcgRevealStateTests
{
    private static TcgCollectionDisplayArgs Pack(params TcgPackCardView[] cards)
        => new()
        {
            Kind = TcgCollectionDisplayKind.PackOpened,
            Cards = [..cards.Select(card => card.Key).Distinct().Select(key => new TcgCatalogCardView(key, key, TcgElement.Fire, TcgRarity.Common, TcgCardType.Creature, 1, 0, 0, 0, 0, 1, 1, TcgKeyword.None, string.Empty, TcgArtKind.None, 0))],
            Pack = [..cards]
        };

    private static TcgCollectionDisplayArgs Eight(string key = "imp")
        => Pack([..Enumerable.Range(0, 8).Select(i => new TcgPackCardView($"{key}{i}", TcgRarity.Common, false, i == 0))]);

    private static TcgRevealState Started(TcgCollectionDisplayArgs? pack = null)
    {
        var state = new TcgRevealState();
        state.Start(pack ?? Eight());

        return state;
    }

    [Test]
    public void Cards_slide_in_after_the_tear_one_after_another()
    {
        var state = Started();

        state.SlideProgress(0).Should().Be(0);
        state.TearProgress.Should().Be(0);

        state.Update(TcgRevealState.TEAR_MS + TcgRevealState.SLIDE_MS);

        state.TearProgress.Should().Be(1);
        state.HasArrived(0).Should().BeTrue();
        state.HasArrived(1).Should().BeFalse();
        state.SlideProgress(1).Should().BeApproximately(0.8, 0.0001);
    }

    [Test]
    public void A_card_flips_once_it_has_arrived_and_only_once()
    {
        var state = Started();
        state.Update(TcgRevealState.ArrivalMs(0));

        state.TryFlip(1).Should().BeFalse();
        state.Update(TcgRevealState.SLIDE_STAGGER_MS);
        state.TryFlip(1).Should().BeTrue();
        state.TryFlip(1).Should().BeFalse();
        state.TryFlip(8).Should().BeFalse();

        state.FlipProgress(1).Should().Be(0);
        state.Update(TcgRevealState.FLIP_MS / 2);
        state.FlipProgress(1).Should().BeApproximately(0.5, 0.0001);
        state.IsFaceUp(1).Should().BeFalse();
        state.Update(TcgRevealState.FLIP_MS / 2);
        state.IsFaceUp(1).Should().BeTrue();
    }

    [Test]
    public void Reveal_all_flips_the_rest_in_order()
    {
        var state = Started();
        state.Update(TcgRevealState.ArrivalMs(7));
        state.TryFlip(2);

        state.RevealAll();
        state.Update(0);
        state.Update(TcgRevealState.FLIP_MS);

        state.IsFaceUp(0).Should().BeTrue();
        state.IsFaceUp(2).Should().BeTrue();
        state.IsFaceUp(1).Should().BeFalse();
        state.AllFaceUp.Should().BeFalse();

        state.Update((6 * TcgRevealState.REVEAL_ALL_STEP_MS) + TcgRevealState.FLIP_MS);

        state.AllFaceUp.Should().BeTrue();
    }

    [Test]
    public void Reveal_all_waits_for_cards_still_sliding_in()
    {
        var state = Started();

        state.RevealAll();
        state.Update(TcgRevealState.ArrivalMs(0) - 1);

        state.FlipProgress(0).Should().Be(0);

        state.Update(1 + TcgRevealState.FLIP_MS);

        state.IsFaceUp(0).Should().BeTrue();
    }

    [Test]
    public void Glows_show_by_rarity_and_foil_until_the_flip_starts()
    {
        var state = Started(
            Pack(
                new TcgPackCardView("common", TcgRarity.Common, false, false),
                new TcgPackCardView("rare", TcgRarity.Rare, false, false),
                new TcgPackCardView("legend", TcgRarity.Legendary, false, false),
                new TcgPackCardView("shiny", TcgRarity.Uncommon, true, false),
                new TcgPackCardView("shinylegend", TcgRarity.Legendary, true, false)));

        Enumerable.Range(0, 5)
                  .Select(state.Glow)
                  .Should()
                  .Equal(TcgRevealGlow.None, TcgRevealGlow.Rare, TcgRevealGlow.Legendary, TcgRevealGlow.Foil, TcgRevealGlow.LegendaryFoil);

        state.Update(TcgRevealState.ArrivalMs(4));
        state.TryFlip(2);

        state.Glow(2).Should().Be(TcgRevealGlow.None);
    }

    [Test]
    public void New_shows_only_on_face_up_cards_that_are_new()
    {
        var state = Started();
        state.Update(TcgRevealState.ArrivalMs(7));

        state.ShowsNew(0).Should().BeFalse();

        state.TryFlip(0);
        state.TryFlip(1);
        state.Update(TcgRevealState.FLIP_MS);

        state.ShowsNew(0).Should().BeTrue();
        state.ShowsNew(1).Should().BeFalse();
    }

    [Test]
    public void Done_needs_every_card_face_up()
    {
        new TcgRevealState().AllFaceUp.Should().BeFalse();

        var state = Started();
        state.Update(TcgRevealState.ArrivalMs(7));

        for (var i = 0; i < 7; i++)
            state.TryFlip(i);

        state.Update(TcgRevealState.FLIP_MS);
        state.AllFaceUp.Should().BeFalse();

        state.TryFlip(7);
        state.Update(TcgRevealState.FLIP_MS);
        state.AllFaceUp.Should().BeTrue();
    }

    [Test]
    public void A_new_pack_replaces_an_open_reveal()
    {
        var state = Started();
        state.Update(TcgRevealState.ArrivalMs(7));
        state.TryFlip(0);
        state.RevealAll();

        state.Start(Eight("bolt"));

        state.ElapsedMs.Should().Be(0);
        state.FlipProgress(0).Should().Be(0);
        state.View(0)!.Key.Should().Be("bolt0");
        state.Update(TcgRevealState.ArrivalMs(7) + TcgRevealState.FLIP_MS);
        state.IsFaceUp(3).Should().BeFalse();
    }
}
