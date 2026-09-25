using Chaos.Client.Controls.World.Popups.GuildCloak;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class GuildCloakWalkCycleTests
{
    private static GuildCloakWalkCycle Cycle() => new(4, 160);

    [Test]
    public void Playing_moves_on_one_step_per_interval()
    {
        var cycle = Cycle();

        cycle.Advance(100);
        cycle.Step.Should().Be(0);

        cycle.Advance(60);
        cycle.Step.Should().Be(1);
    }

    [Test]
    public void A_paused_cycle_stays_on_its_step()
    {
        var cycle = Cycle();
        cycle.TogglePause();

        cycle.Advance(1_000);

        cycle.Paused.Should().BeTrue();
        cycle.Step.Should().Be(0);
    }

    [Test]
    public void Stepping_pauses_and_wraps_around()
    {
        var cycle = Cycle();

        cycle.StepBy(-1);
        cycle.Step.Should().Be(3);
        cycle.Paused.Should().BeTrue();

        cycle.StepBy(1);
        cycle.Step.Should().Be(0);
    }

    [Test]
    public void Playing_again_moves_on_from_the_paused_step()
    {
        var cycle = Cycle();
        cycle.StepBy(2);

        cycle.TogglePause();
        cycle.Advance(160);

        cycle.Paused.Should().BeFalse();
        cycle.Step.Should().Be(3);
    }
}
