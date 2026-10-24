using Chaos.Client.Systems.Cinema;
using FluentAssertions;

namespace Chaos.Client.Tests.Cinema;

public class CinemaSyncTests
{
    [Test]
    [Arguments(1000L, null, 1000L)]
    [Arguments(1000L, 300L, 1150L)]
    [Arguments(1000L, -5L, 1000L)]
    public void Start_position_adds_half_the_ping(long serverMs, long? pingMs, long expected)
        => CinemaSync.StartPosition(serverMs, pingMs).Should().Be(expected);

    [Test]
    [Arguments(0L, 1500L, false)]
    [Arguments(0L, 1501L, true)]
    [Arguments(5000L, 3000L, true)]
    [Arguments(5000L, 5000L, false)]
    public void Needs_seek_past_the_threshold(long actual, long expected, bool needs)
        => CinemaSync.NeedsSeek(actual, expected).Should().Be(needs);
}
