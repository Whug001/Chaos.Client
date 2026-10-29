#region
using Chaos.Client.ViewModel;
using Chaos.DarkAges.Karting;
using FluentAssertions;
#endregion

namespace Chaos.Client.Tests.Karting;

public class KartItemSlotTests
{
    [Test]
    public async Task A_rolling_slot_cycles_names_every_fifty_milliseconds()
    {
        KartItemSlot.Text(KartItemKind.None, 20, 0).Should().Be("Ember Bolt");
        KartItemSlot.Text(KartItemKind.None, 20, 49).Should().Be("Ember Bolt");
        KartItemSlot.Text(KartItemKind.None, 20, 50).Should().Be("Hunting Raven");
        KartItemSlot.Text(KartItemKind.None, 20, 350).Should().Be("Ember Bolt");
    }

    [Test]
    public async Task A_rolling_slot_advances_at_the_same_rate_at_sixty_and_three_hundred_twenty_fps()
    {
        const double elapsedSeconds = 1;
        var atSixty = (long)Math.Round(60 * elapsedSeconds) * 1000 / 60;
        var atThreeTwenty = (long)Math.Round(320 * elapsedSeconds) * 1000 / 320;

        KartItemSlot.ShownKind(KartItemKind.None, 20, atSixty)
            .Should().Be(KartItemSlot.ShownKind(KartItemKind.None, 20, atThreeTwenty));
    }

    [Test]
    public async Task A_held_item_shows_its_name_and_an_empty_slot_a_dash()
    {
        KartItemSlot.Text(KartItemKind.Gloaming, 0, 500).Should().Be("The Gloaming");
        KartItemSlot.Text(KartItemKind.None, 0, 500).Should().Be("-");
    }

    [Test]
    public async Task The_wisp_text_counts_to_ten()
    {
        KartItemSlot.WispText(3).Should().Be("Wisps 3/10");
        KartItemSlot.WispText(12).Should().Be("Wisps 10/10");
    }
}
