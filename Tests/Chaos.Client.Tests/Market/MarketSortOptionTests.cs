using System.Text;
using Chaos.Client.Controls.World.Popups.Market;
using Chaos.DarkAges.Definitions;
using Chaos.IO.Memory;
using Chaos.Networking.Converters.Client;
using Chaos.Networking.Entities.Client;
using FluentAssertions;

namespace Chaos.Client.Tests.Market;

public class MarketSortOptionTests
{
    [Test]
    public async Task The_sort_dropdown_offers_lowest_price_first_then_newest()
    {
        MarketSearchControl.SortOptions
                           .Select(option => option.Order)
                           .Should()
                           .Equal(MarketSortOrder.Price, MarketSortOrder.Newest);

        MarketSearchControl.SortOptions
                           .Select(option => option.Label)
                           .Should()
                           .Equal("Lowest price", "Newest");

        await Task.CompletedTask;
    }

    //formatter:off
    [Test]
    [Arguments(0, MarketSortOrder.Price)]
    [Arguments(1, MarketSortOrder.Newest)]
    [Arguments(-1, MarketSortOrder.Price)]
    [Arguments(2, MarketSortOrder.Price)]
    //formatter:on
    public async Task A_picked_row_maps_to_its_sort_and_anything_else_to_price(int index, MarketSortOrder expected)
    {
        MarketSearchControl.SortOrderAt(index).Should().Be(expected);

        await Task.CompletedTask;
    }

    [Test]
    public async Task A_newest_search_reaches_the_server_as_newest()
    {
        var sent = new MarketInteractionArgs
        {
            Type = MarketInteractionType.Search,
            Criteria = new MarketSearchCriteria { ItemName = "ring", SortOrder = MarketSearchControl.SortOrderAt(1) }
        };

        var received = RoundTrip(sent);

        received.Criteria!.SortOrder.Should().Be(MarketSortOrder.Newest);
        received.Criteria.ItemName.Should().Be("ring");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Changing_page_keeps_the_sort()
    {
        //WorldScreen re-sends the last criteria `with { Page = n }` when the Results tab pages
        var criteria = new MarketSearchCriteria { SortOrder = MarketSortOrder.Newest };

        (criteria with { Page = 2 }).SortOrder.Should().Be(MarketSortOrder.Newest);

        await Task.CompletedTask;
    }

    //spans can't live across an await, so the wire round trip sits in a sync helper
    private static MarketInteractionArgs RoundTrip(MarketInteractionArgs args)
    {
        var converter = new MarketInteractionConverter();
        var backing = new byte[256].AsSpan();
        var writer = new SpanWriter(Encoding.ASCII, ref backing);
        converter.Serialize(ref writer, args);
        var reader = new SpanReader(Encoding.ASCII, backing[..writer.Position]);

        return converter.Deserialize(ref reader);
    }
}
