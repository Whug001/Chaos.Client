#region
using Chaos.Client.Screens;
using FluentAssertions;
#endregion

namespace Chaos.Client.Tests;

public sealed class MapStreamingTests
{
    [Test]
    public void IsStreamed_ShouldBeTrue_ForTheTowerMap()
        => MapStreaming.IsStreamed(32000)
                       .Should()
                       .BeTrue();

    [Test]
    public void IsStreamed_ShouldBeTrue_ForTheGuildHallMap()
        => MapStreaming.IsStreamed(32001)
                       .Should()
                       .BeTrue();

    [Test]
    public void IsStreamed_ShouldBeTrue_ForTheHouseMap()
        => MapStreaming.IsStreamed(MapStreaming.HOUSE_MAP_ID)
                       .Should()
                       .BeTrue();

    [Test]
    public void HouseMapId_ShouldBe32002()
        => MapStreaming.HOUSE_MAP_ID.Should().Be(32002);

    [Test]
    public void IsStreamed_ShouldBeFalse_ForOrdinaryMaps()
        => MapStreaming.IsStreamed(7)
                       .Should()
                       .BeFalse();

    [Test]
    public void IsEditableMap_ShouldBeTrue_ForTheGuildHallMap()
        => MapStreaming.IsEditableMap(32001)
                       .Should()
                       .BeTrue();

    [Test]
    public void IsEditableMap_ShouldBeTrue_ForTheHouseMap()
        => MapStreaming.IsEditableMap(32002)
                       .Should()
                       .BeTrue();

    [Test]
    public void IsEditableMap_ShouldBeFalse_ForTheTowerMap()
        => MapStreaming.IsEditableMap(32000)
                       .Should()
                       .BeFalse();

    [Test]
    public void IsEditableMap_ShouldBeFalse_ForOrdinaryMaps()
        => MapStreaming.IsEditableMap(7)
                       .Should()
                       .BeFalse();
}
