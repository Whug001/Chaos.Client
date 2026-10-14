using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class CraftingWindowStateTests
{
    private static CraftingRecipeInfo Recipe(ushort id, string name, bool makeable = true)
        => new() { Id = id, Name = name, Makeable = makeable, Rank = 1 };

    private static CraftingDisplayArgs Open(params CraftingRecipeInfo[] recipes)
        => new()
        {
            Type = CraftingDisplayType.Open,
            Mode = CraftingWindowMode.Alchemy,
            CraftName = "Alchemy",
            RankTitle = "Novice Alchemist",
            NextRankTitle = "Apprentice Alchemist",
            RankCount = 30,
            RankThreshold = 75,
            Recipes = recipes
        };

    private static CraftingDisplayArgs Detail(ushort id, byte max, int have = 4, int need = 2)
        => new()
        {
            Type = CraftingDisplayType.Detail,
            RecipeId = id,
            RecipeName = "Hemloch Draught",
            MaxQuantity = max,
            SuccessRate = 50,
            CatalystRates = [55, 61, 66],
            Ingredients = [new CraftingIngredientInfo { Name = "Hemloch", Have = have, Need = need }]
        };

    private static CraftingWindowState Opened()
    {
        var state = new CraftingWindowState();
        state.Apply(Open(Recipe(1, "Hemloch Draught"), Recipe(2, "Mana Tonic"), Recipe(3, "Strong Hemloch", false)));

        return state;
    }

    [Test]
    public void Filter_matches_names_case_insensitively()
    {
        var state = Opened();

        state.Filter = "hEMLOCH";

        state.VisibleRecipes.Select(r => r.Id).Should().Equal(1, 3);
    }

    [Test]
    public void Open_resets_the_filter_but_List_keeps_it()
    {
        var state = Opened();
        state.Filter = "mana";
        state.Apply(Open(Recipe(2, "Mana Tonic")) with { Type = CraftingDisplayType.List });
        state.Filter.Should().Be("mana");

        state.Apply(Open(Recipe(2, "Mana Tonic")));
        state.Filter.Should().BeEmpty();
    }

    [Test]
    public void Select_returns_a_select_action()
    {
        var state = Opened();

        state.Select(2).Should().BeEquivalentTo(new { Type = CraftingActionType.Select, RecipeId = (ushort)2 });
        state.SelectedId.Should().Be((ushort)2);
    }

    [Test]
    public void Quantity_steps_within_one_and_max()
    {
        var state = Opened();
        state.Select(1);
        state.Apply(Detail(1, 3));

        state.Step(-1);
        state.Quantity.Should().Be(1);
        state.Step(+1);
        state.Step(+1);
        state.Step(+1);
        state.Quantity.Should().Be(3);
    }

    [Test]
    public void Typed_quantity_is_kept_within_one_and_max()
    {
        var state = Opened();
        state.Select(1);
        state.Apply(Detail(1, 20));

        state.SetQuantity(12);
        state.Quantity.Should().Be(12);
        state.SetQuantity(999);
        state.Quantity.Should().Be(20);
        state.SetQuantity(0);
        state.Quantity.Should().Be(1);
    }

    [Test]
    public void Typed_quantity_is_ignored_when_nothing_can_be_made()
    {
        var state = Opened();
        state.Select(3);
        state.Apply(Detail(3, 0));

        state.SetQuantity(5);
        state.Quantity.Should().Be(0);
    }

    [Test]
    public void A_catalyst_stack_limits_the_quantity()
    {
        var state = Opened();
        state.Select(1);
        state.Apply(Detail(1, 10));

        state.SetCatalyst(7, "Bright Catalyst", 2).Should().BeTrue();

        state.QuantityMax.Should().Be(2);
        state.CatalystRate.Should().Be((byte)61);
    }

    [Test]
    public void A_stacked_catalyst_name_is_matched_by_its_base_name()
    {
        var state = Opened();
        state.Select(1);
        state.Apply(Detail(1, 10));
        var slot = new Inventory.InventorySlotData(5, default, "Bright Catalyst[ 4 ]", true, 4, 0, 0);

        state.SetCatalystFromSlot(9, in slot).Should().BeTrue();

        state.CatalystSlot.Should().Be((byte)9);
        state.QuantityMax.Should().Be(4);
    }

    [Test]
    public void A_finished_progress_with_nothing_done_stops_the_batch()
    {
        var state = Opened();
        state.Select(1);
        state.Apply(Detail(1, 5));
        state.CraftAction();

        state.Apply(new CraftingDisplayArgs { Type = CraftingDisplayType.Progress, Finished = true });

        state.Running.Should().BeFalse();
    }

    [Test]
    public void Only_catalysts_fit_the_slot()
        => Opened().SetCatalyst(7, "Iron Bar", 5).Should().BeFalse();

    [Test]
    public void Craft_action_carries_quantity_and_catalyst()
    {
        var state = Opened();
        state.Select(1);
        state.Apply(Detail(1, 10));
        state.SetCatalyst(7, "Faint Catalyst", 9);
        state.Step(+1);

        state.CraftAction().Should().BeEquivalentTo(
            new { Type = CraftingActionType.Craft, RecipeId = (ushort)1, Quantity = (byte)2, CatalystSlot = (byte)7 });
        state.Running.Should().BeTrue();
        state.CraftAction().Should().BeNull();
    }

    [Test]
    public void No_craft_when_nothing_is_makeable()
    {
        var state = Opened();
        state.Select(3);
        state.Apply(Detail(3, 0));

        state.CraftAction().Should().BeNull();
    }

    [Test]
    public void Ingredients_scale_with_the_quantity()
    {
        var state = Opened();
        state.Select(1);
        state.Apply(Detail(1, 2, have: 3, need: 2));
        state.Step(+1);

        state.ScaledIngredients
             .Select(i => (i.Name, i.Have, i.Need))
             .Should()
             .Equal(("Hemloch", 3, 4));
    }

    [Test]
    public void Progress_shows_and_finish_clears_running()
    {
        var state = Opened();
        state.Select(1);
        state.Apply(Detail(1, 5));
        state.CraftAction();

        state.Apply(new CraftingDisplayArgs { Type = CraftingDisplayType.Progress, Done = 2, Total = 5 });
        state.ProgressText.Should().Be("Crafting 2 / 5");

        state.Apply(new CraftingDisplayArgs { Type = CraftingDisplayType.Progress, Done = 5, Total = 5, Finished = true });
        state.Running.Should().BeFalse();
    }

    [Test]
    public void Rank_bar_shows_progress_to_the_next_rank()
    {
        var state = Opened();

        state.RankBarPercent.Should().BeApproximately(0.4f, 0.001f);
        state.RankBarText.Should().Be("30 / 75 to Apprentice");
        state.RankShortTitle.Should().Be("Novice");
    }

    [Test]
    public void Rank_texts_fit_their_boxes()
    {
        var state = new CraftingWindowState();
        state.Apply(
            Open(Recipe(1, "x")) with
            {
                RankTitle = "Ascendant Weaponsmith", NextRankTitle = "Eternal Weaponsmith", RankCount = 99999, RankThreshold = 99999
            });

        state.RankShortTitle.Length.Should().BeLessThanOrEqualTo(10);
        state.RankBarText.Length.Should().BeLessThanOrEqualTo(28);
    }

    [Test]
    public void A_list_without_the_selected_recipe_clears_the_selection()
    {
        var state = Opened();
        state.Select(1);
        state.Apply(Detail(1, 3));

        state.Apply(Open(Recipe(2, "Mana Tonic"), Recipe(4, "Upgraded Blade")) with { Type = CraftingDisplayType.List });

        state.SelectedId.Should().BeNull();
        state.Detail.Should().BeNull();
        state.Quantity.Should().Be(0);
    }

    [Test]
    public void A_list_with_the_selected_recipe_keeps_it()
    {
        var state = Opened();
        state.Select(2);
        state.Apply(Detail(2, 3));

        state.Apply(Open(Recipe(2, "Mana Tonic")) with { Type = CraftingDisplayType.List });

        state.SelectedId.Should().Be((ushort)2);
        state.Detail.Should().NotBeNull();
    }

    [Test]
    public void Confirm_opens_at_once_when_the_detail_is_here()
    {
        var state = Opened();
        state.Select(1);
        state.Apply(Detail(1, 3));

        state.RequestConfirm().Should().BeTrue();
        state.PendingConfirmId.Should().BeNull();
    }

    [Test]
    public void Confirm_waits_for_the_detail_of_a_new_recipe()
    {
        var state = Opened();
        state.Select(1);

        state.RequestConfirm().Should().BeFalse();
        state.TakePendingConfirm().Should().BeFalse();

        state.Apply(Detail(1, 3));

        state.TakePendingConfirm().Should().BeTrue();
        state.TakePendingConfirm().Should().BeFalse();
    }

    [Test]
    public void Selecting_another_recipe_drops_the_waiting_confirm()
    {
        var state = Opened();
        state.Select(1);
        state.RequestConfirm();

        state.Select(2);
        state.Apply(Detail(2, 3));

        state.PendingConfirmId.Should().BeNull();
        state.TakePendingConfirm().Should().BeFalse();
    }

    [Test]
    public void A_list_reload_drops_the_waiting_confirm()
    {
        var state = Opened();
        state.Select(1);
        state.RequestConfirm();

        state.Apply(Open(Recipe(1, "Hemloch Draught")) with { Type = CraftingDisplayType.List });
        state.Apply(Detail(1, 3));

        state.TakePendingConfirm().Should().BeFalse();
    }

    [Test]
    public void Cancelling_drops_the_waiting_confirm()
    {
        var state = Opened();
        state.Select(1);
        state.RequestConfirm();

        state.CancelPendingConfirm();
        state.Apply(Detail(1, 3));

        state.TakePendingConfirm().Should().BeFalse();
    }

    [Test]
    public void Rank_bar_is_full_at_the_top_rank()
    {
        var state = new CraftingWindowState();
        state.Apply(Open(Recipe(1, "x")) with { RankTitle = "Eternal Alchemist", NextRankTitle = "", RankThreshold = 0 });

        state.RankBarPercent.Should().Be(1f);
        state.RankBarText.Should().Be("Eternal Alchemist");
    }
}
