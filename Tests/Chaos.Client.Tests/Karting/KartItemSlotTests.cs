#region
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Karting;
using FluentAssertions;
#endregion

namespace Chaos.Client.Tests.Karting;

public class KartItemSlotTests
{
    [Test]
    public async Task A_rolling_slot_cycles_names_every_three_frames()
    {
        KartItemSlot.Text(KartItemKind.None, 20, 0).Should().Be("Ember Bolt");
        KartItemSlot.Text(KartItemKind.None, 20, 2).Should().Be("Ember Bolt");
        KartItemSlot.Text(KartItemKind.None, 20, 3).Should().Be("Hunting Raven");
        KartItemSlot.Text(KartItemKind.None, 20, 21).Should().Be("Ember Bolt");
    }

    [Test]
    public async Task A_held_item_shows_its_name_and_an_empty_slot_a_dash()
    {
        KartItemSlot.Text(KartItemKind.Gloaming, 0, 5).Should().Be("The Gloaming");
        KartItemSlot.Text(KartItemKind.None, 0, 5).Should().Be("-");
    }

    [Test]
    public async Task The_wisp_text_counts_to_ten()
    {
        KartItemSlot.WispText(3).Should().Be("Wisps 3/10");
        KartItemSlot.WispText(12).Should().Be("Wisps 10/10");
    }
}
