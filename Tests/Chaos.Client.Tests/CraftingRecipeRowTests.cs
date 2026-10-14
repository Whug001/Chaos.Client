using Chaos.Client.Controls.World.Popups.Crafting;
using FluentAssertions;

namespace Chaos.Client.Tests;

public sealed class CraftingRecipeRowTests
{
    [Test]
    public void A_recipe_row_lets_clicks_through_to_the_list()
        => new CraftingRecipeRow(200).IsHitTestVisible.Should().BeFalse();
}
