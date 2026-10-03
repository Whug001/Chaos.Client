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
    public void IsStreamed_ShouldBeFalse_ForOrdinaryMaps()
        => MapStreaming.IsStreamed(7)
                       .Should()
                       .BeFalse();
}
