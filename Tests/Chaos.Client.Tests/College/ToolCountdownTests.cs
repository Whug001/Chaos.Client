using Chaos.Client.ViewModel.College;
using FluentAssertions;

namespace Chaos.Client.Tests.College;

public class ToolCountdownTests
{
    [Test]
    public void It_counts_whole_seconds_down_and_stops_at_zero()
    {
        var countdown = new ToolCountdown();
        countdown.Start(20);

        countdown.Left.Should().Be(20);

        countdown.Advance(0.9);
        countdown.Left.Should().Be(20);

        countdown.Advance(0.2);
        countdown.Left.Should().Be(19);

        countdown.Advance(30);
        countdown.Left.Should().Be(0);
    }

    [Test]
    public void Starting_again_resets_it()
    {
        var countdown = new ToolCountdown();
        countdown.Start(10);
        countdown.Advance(5);

        countdown.Start(30);

        countdown.Left.Should().Be(30);
    }
}
