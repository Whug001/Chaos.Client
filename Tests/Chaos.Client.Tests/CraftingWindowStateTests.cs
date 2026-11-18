using Chaos.Client.ViewModel;
using Chaos.DarkAges.Definitions;
using Chaos.Networking.Entities.Client;
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

    private static CraftingDisplayArgs CookingOpen(params CraftingRecipeInfo[] recipes)
        => Open(recipes) with
        {
            Mode = CraftingWindowMode.Cooking,
            CraftName = "Cooking",
            RankTitle = string.Empty,
            NextRankTitle = "Beginner Cook",
            RankCount = 0,
            RankThreshold = 1
        };

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

    [Test]
    public void Swap_action_carries_row_and_direction()
    {
        var state = Opened();
        state.Select(1);
        state.Apply(Detail(1, 3));

        state.SwapAction(3, false).Should().BeEquivalentTo(
            new { Type = CraftingActionType.SwapIngredient, RecipeId = (ushort)1, IngredientRow = (byte)3, Forward = false });
        state.SwapAction(2, true).Should().BeEquivalentTo(
            new { Type = CraftingActionType.SwapIngredient, RecipeId = (ushort)1, IngredientRow = (byte)2, Forward = true });
    }

    [Test]
    public void Swap_action_is_null_while_running()
    {
        var state = Opened();
        state.Select(1);
        state.Apply(Detail(1, 5));
        state.CraftAction();

        state.SwapAction(0, true).Should().BeNull();
    }

    [Test]
    public void Swap_action_is_null_without_selection()
        => Opened().SwapAction(0, true).Should().BeNull();

    [Test]
    public void Cooking_rank_text_uses_full_title()
    {
        var state = new CraftingWindowState();
        state.Apply(
            CookingOpen(Recipe(1, "Apple Pie")) with
            {
                RankTitle = "Apprentice Cook", NextRankTitle = "Sous Chef", RankCount = 212, RankThreshold = 300
            });

        state.IsCooking.Should().BeTrue();
        state.RankBarText.Should().Be("212 / 300 to Sous Chef");
        state.RankShortTitle.Should().Be("Apprentice Cook");

        var fresh = new CraftingWindowState();
        fresh.Apply(CookingOpen(Recipe(1, "Apple Pie")));

        fresh.RankBarText.Should().Be("0 / 1 to Beginner Cook");
    }

    [Test]
    public void Cooking_progress_says_cooking()
    {
        var state = new CraftingWindowState();
        state.Apply(CookingOpen(Recipe(1, "Apple Pie")));
        state.Select(1);
        state.Apply(Detail(1, 5));
        state.CraftAction();

        state.ProgressText.Should().Be("Cooking 0 / 1");

        state.Apply(new CraftingDisplayArgs { Type = CraftingDisplayType.Progress, Done = 2, Total = 5 });

        state.ProgressText.Should().Be("Cooking 2 / 5");
    }

    [Test]
    public void Catalyst_cannot_be_set_when_cooking()
    {
        var state = Opened();
        state.Select(1);
        state.Apply(Detail(1, 10));
        state.SetCatalyst(7, "Bright Catalyst", 2).Should().BeTrue();

        state.Apply(CookingOpen(Recipe(1, "Apple Pie")) with { Type = CraftingDisplayType.List });

        state.CatalystRate.Should().BeNull();
        state.SetCatalyst(7, "Bright Catalyst", 2).Should().BeFalse();
        state.CatalystRate.Should().BeNull();
    }

    [Test]
    public void Clear_catalyst_does_nothing_when_cooking()
    {
        var state = new CraftingWindowState();
        state.Apply(CookingOpen(Recipe(1, "Apple Pie")));
        state.Select(1);
        state.Apply(Detail(1, 5));

        var raised = 0;
        state.Changed += () => raised++;

        var quantity = state.Quantity;
        var slot = state.CatalystSlot;
        state.ClearCatalyst();

        raised.Should().Be(0);
        state.Quantity.Should().Be(quantity);
        state.CatalystSlot.Should().Be(slot);
        state.CatalystRate.Should().BeNull();
    }

    [Test]
    public void Times_made_dash_when_negative()
    {
        var state = Opened();

        state.TimesMadeText.Should().BeEmpty();

        state.Select(1);
        state.Apply(Detail(1, 3) with { TimesMade = -1 });

        state.TimesMadeText.Should().Be("-");

        state.Apply(Detail(1, 3) with { TimesMade = 12 });

        state.TimesMadeText.Should().Be("12");
    }

    [Test]
    public void Other_crafts_keep_short_rank_text()
    {
        var state = new CraftingWindowState();
        state.Apply(Open(Recipe(1, "Hemloch Draught")) with { RankTitle = "Novice Alchemist", NextRankTitle = "Initiate Alchemist" });

        state.IsCooking.Should().BeFalse();
        state.RankBarText.Should().Be("30 / 75 to Initiate");
        state.RankShortTitle.Should().Be("Novice");
    }

    private static CraftingIngredientInfo FixedIngredient(string name)
        => new() { Name = name, Have = 4, Need = 2 };

    private static CraftingIngredientInfo ChoosableIngredient(string name)
        => new() { Name = name, Have = 4, Need = 2, Choosable = true };

    private static CraftingDisplayArgs CookingDetail(ushort id, byte max, params CraftingIngredientInfo[] ingredients)
        => new()
        {
            Type = CraftingDisplayType.Detail,
            RecipeId = id,
            RecipeName = "Dinner Plate",
            MaxQuantity = max,
            SuccessRate = 70,
            Ingredients = ingredients
        };

    private static CraftingWindowState CookingOpened()
    {
        var state = new CraftingWindowState();
        state.Apply(CookingOpen(Recipe(1, "Apple Pie"), Recipe(2, "Dinner Plate")));

        return state;
    }

    [Test]
    public void Active_row_defaults_to_the_first_choosable_row()
    {
        var state = CookingOpened();
        state.Select(1);
        state.Apply(CookingDetail(1, 5, FixedIngredient("Salt"), ChoosableIngredient("Beef"), ChoosableIngredient("Pork")));

        state.ActiveSwapRow.Should().Be(1);
    }

    [Test]
    public void No_choosable_row_leaves_no_active_row()
    {
        var state = CookingOpened();
        state.Select(1);
        state.Apply(CookingDetail(1, 5, FixedIngredient("Salt"), FixedIngredient("Water")));

        state.ActiveSwapRow.Should().Be(-1);
        state.SwapActiveAction(true).Should().BeNull();
        state.SwapActiveAction(false).Should().BeNull();
    }

    [Test]
    public void Setting_a_non_choosable_or_out_of_range_row_is_ignored()
    {
        var state = CookingOpened();
        state.Select(1);
        state.Apply(CookingDetail(1, 5, FixedIngredient("Salt"), ChoosableIngredient("Beef"), ChoosableIngredient("Pork")));

        state.SetActiveSwapRow(2);
        state.SetActiveSwapRow(0);
        state.SetActiveSwapRow(99);
        state.SetActiveSwapRow(-1);

        state.ActiveSwapRow.Should().Be(2);
    }

    [Test]
    public void Active_row_survives_a_detail_rebuild_for_the_same_recipe()
    {
        var state = CookingOpened();
        state.Select(1);
        state.Apply(CookingDetail(1, 5, FixedIngredient("Salt"), ChoosableIngredient("Beef"), ChoosableIngredient("Pork")));
        state.SetActiveSwapRow(2);

        state.Apply(CookingDetail(1, 5, FixedIngredient("Salt"), ChoosableIngredient("Beef"), ChoosableIngredient("Pork")));

        state.ActiveSwapRow.Should().Be(2);
    }

    [Test]
    public void Selecting_another_recipe_resets_the_active_row()
    {
        var state = CookingOpened();
        state.Select(1);
        state.Apply(CookingDetail(1, 5, FixedIngredient("Salt"), ChoosableIngredient("Beef"), ChoosableIngredient("Pork")));
        state.SetActiveSwapRow(2);

        state.Select(2);

        state.ActiveSwapRow.Should().Be(-1);

        state.Apply(CookingDetail(2, 5, ChoosableIngredient("Apple"), FixedIngredient("Sugar")));

        state.ActiveSwapRow.Should().Be(0);
    }

    [Test]
    public void Active_row_falls_back_when_its_row_stops_being_choosable()
    {
        var state = CookingOpened();
        state.Select(1);
        state.Apply(CookingDetail(1, 5, FixedIngredient("Salt"), ChoosableIngredient("Beef"), ChoosableIngredient("Pork")));
        state.SetActiveSwapRow(2);

        state.Apply(CookingDetail(1, 5, FixedIngredient("Salt"), ChoosableIngredient("Beef"), FixedIngredient("Pork")));

        state.ActiveSwapRow.Should().Be(1);
    }

    [Test]
    public void Active_row_falls_back_to_none_when_nothing_is_choosable()
    {
        var state = CookingOpened();
        state.Select(1);
        state.Apply(CookingDetail(1, 5, FixedIngredient("Salt"), ChoosableIngredient("Beef")));
        state.SetActiveSwapRow(1);

        state.Apply(CookingDetail(1, 5, FixedIngredient("Salt"), FixedIngredient("Beef")));

        state.ActiveSwapRow.Should().Be(-1);
    }

    [Test]
    public void Swap_active_action_carries_the_active_row_and_direction()
    {
        var state = CookingOpened();
        state.Select(1);
        state.Apply(CookingDetail(1, 5, FixedIngredient("Salt"), ChoosableIngredient("Beef"), ChoosableIngredient("Pork")));
        state.SetActiveSwapRow(2);

        state.SwapActiveAction(true).Should().BeEquivalentTo(
            new { Type = CraftingActionType.SwapIngredient, RecipeId = (ushort)1, IngredientRow = (byte)2, Forward = true });
        state.SwapActiveAction(false).Should().BeEquivalentTo(
            new { Type = CraftingActionType.SwapIngredient, RecipeId = (ushort)1, IngredientRow = (byte)2, Forward = false });
    }

    [Test]
    public void Swap_active_action_is_null_while_running()
    {
        var state = CookingOpened();
        state.Select(1);
        state.Apply(CookingDetail(1, 5, FixedIngredient("Salt"), ChoosableIngredient("Beef")));

        state.CraftAction();

        state.SwapActiveAction(true).Should().BeNull();
    }

    [Test]
    public void Non_cooking_modes_have_no_active_row()
    {
        var state = Opened();
        state.Select(1);
        state.Apply(Detail(1, 3) with { Ingredients = [FixedIngredient("Hemloch"), ChoosableIngredient("Mandrake")] });

        state.ActiveSwapRow.Should().Be(-1);
        state.SetActiveSwapRow(1);

        state.ActiveSwapRow.Should().Be(-1);
        state.SwapActiveAction(true).Should().BeNull();
    }
}
