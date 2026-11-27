using Chaos.Client.Controls.World.Hud.Panel;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class AbilityPageSwapTests
{
    [Test]
    public async Task Only_the_last_cell_of_pages_1_and_2_is_a_swap_cell()
    {
        //page offsets are 0, 36 and 72 (SkillBookPage)
        AbilityPageSwap.IsSwapSlot(0, 36).Should().BeTrue();
        AbilityPageSwap.IsSwapSlot(36, 72).Should().BeTrue();
        AbilityPageSwap.IsSwapSlot(0, 35).Should().BeFalse();
        AbilityPageSwap.IsSwapSlot(36, 71).Should().BeFalse();
        AbilityPageSwap.IsSwapSlot(0, 72).Should().BeFalse();
        AbilityPageSwap.IsSwapSlot(72, 108).Should().BeFalse();
        await Task.CompletedTask;
    }

    [Test]
    public async Task The_label_names_the_page_it_sends_to()
    {
        AbilityPageSwap.Label(36).Should().Be("Drop here to move to page 2");
        AbilityPageSwap.Label(72).Should().Be("Drop here to move to page 1");
        await Task.CompletedTask;
    }
}
