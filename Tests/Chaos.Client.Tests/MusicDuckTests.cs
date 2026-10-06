using Chaos.Client.Systems;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class MusicDuckTests
{
    [Test]
    public void Ducking_fades_the_music_out_over_the_fade_time()
    {
        var duck = new MusicDuck { Ducked = true };

        duck.Step(MusicDuck.FADE_SECONDS / 2).Should().BeTrue();
        duck.Level.Should().BeApproximately(0.5, 1e-9);
        duck.Apply(120).Should().Be(60);

        duck.Step(1).Should().BeTrue();
        duck.Level.Should().Be(0);
        duck.Step(1).Should().BeFalse();
    }

    [Test]
    public void Releasing_brings_the_music_back()
    {
        var duck = new MusicDuck { Ducked = true };
        duck.Step(1);

        duck.Ducked = false;
        duck.Step(MusicDuck.FADE_SECONDS);

        duck.Level.Should().Be(1);
        duck.Apply(84).Should().Be(84);
    }

    [Test]
    public void At_rest_nothing_changes()
    {
        var duck = new MusicDuck();

        duck.Step(0.5).Should().BeFalse();
        duck.Apply(96).Should().Be(96);
    }
}
