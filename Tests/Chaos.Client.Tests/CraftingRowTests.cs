using Chaos.Client.Controls.World.Popups.Crafting;
using Chaos.Networking.Entities.Server;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class CraftingRowTests
{
    [Test]
    public void A_recipe_row_shows_the_name()
    {
        var row = new CraftingRecipeRow(150);

        row.Bind(new CraftingRecipeInfo { Name = "Hemloch Draught", Makeable = true }, false);

        row.Text.Should().Be("Hemloch Draught");
    }

    [Test]
    public void A_short_ingredient_count_is_red()
    {
        var row = new CraftingIngredientRow(150);

        row.Bind(new CraftingIngredientInfo { Name = "Hemloch", Have = 1, Need = 2 }, 1);

        row.CountText.Should().Be("{=b1 / 2");
    }

    [Test]
    public void A_met_ingredient_count_is_not_red()
    {
        var row = new CraftingIngredientRow(150);

        row.Bind(new CraftingIngredientInfo { Name = "Hemloch", Have = 2, Need = 2 }, 1);

        row.CountText.Should().Be("{=u2 / 2");
    }
}
