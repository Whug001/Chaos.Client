using Chaos.Client.Rendering;
using FluentAssertions;

namespace Chaos.Client.Tests;

public class MirrorScareTimelineTests
{
    [Test]
    public void Phases_run_crack_shatter_black_then_done()
    {
        MirrorScareTimeline.PhaseAt(0).Should().Be(ScarePhase.Crack);
        MirrorScareTimeline.PhaseAt(0.11).Should().Be(ScarePhase.Crack);
        MirrorScareTimeline.PhaseAt(0.12).Should().Be(ScarePhase.Shatter);
        MirrorScareTimeline.PhaseAt(1.04).Should().Be(ScarePhase.Shatter);
        MirrorScareTimeline.PhaseAt(1.05).Should().Be(ScarePhase.Black);
        MirrorScareTimeline.PhaseAt(1.19).Should().Be(ScarePhase.Black);
        MirrorScareTimeline.PhaseAt(1.2).Should().Be(ScarePhase.Done);
        MirrorScareTimeline.PhaseAt(-0.01).Should().Be(ScarePhase.Done);
    }

    [Test]
    public void Creature_grows_from_the_break_then_holds()
    {
        MirrorScareTimeline.CreatureScale(0).Should().BeApproximately(0.9f, 0.0001f);
        MirrorScareTimeline.CreatureScale(0.12).Should().BeApproximately(0.9f, 0.0001f);
        MirrorScareTimeline.CreatureScale(0.245).Should().BeApproximately(1.125f, 0.0001f);
        MirrorScareTimeline.CreatureScale(0.37).Should().BeApproximately(1.35f, 0.0001f);
        MirrorScareTimeline.CreatureScale(0.9).Should().BeApproximately(1.35f, 0.0001f);
    }

    [Test]
    public void Shaking_starts_at_0_4_seconds()
    {
        MirrorScareTimeline.IsShaking(0.39).Should().BeFalse();
        MirrorScareTimeline.IsShaking(0.4).Should().BeTrue();
    }

    [Test]
    public void Frames_change_at_each_creatures_cues()
    {
        MirrorScareTimeline.FrameAt(ScareCreature.Mary, 0).Should().Be(0);
        MirrorScareTimeline.FrameAt(ScareCreature.Mary, 0.329).Should().Be(0);
        MirrorScareTimeline.FrameAt(ScareCreature.Mary, 0.33).Should().Be(1);
        MirrorScareTimeline.FrameAt(ScareCreature.Mary, 0.37).Should().Be(2);
        MirrorScareTimeline.FrameAt(ScareCreature.Mary, 1.1).Should().Be(2);

        MirrorScareTimeline.FrameAt(ScareCreature.Grinner, 0.3).Should().Be(1);
        MirrorScareTimeline.FrameAt(ScareCreature.Grinner, 0.39).Should().Be(2);
        MirrorScareTimeline.FrameAt(ScareCreature.Grinner, 0.42).Should().Be(3);

        MirrorScareTimeline.FrameAt(ScareCreature.Eye, 0.32).Should().Be(1);
        MirrorScareTimeline.FrameAt(ScareCreature.Eye, 0.38).Should().Be(2);
    }

    [Test]
    public void Every_creature_frame_ships_as_an_embedded_resource()
    {
        var names = typeof(MirrorScareTimeline).Assembly.GetManifestResourceNames();

        foreach (var creature in Enum.GetValues<ScareCreature>())
            for (var frame = 0; frame < MirrorScareTimeline.FrameCount(creature); frame++)
                names.Should().Contain(MirrorScareTimeline.ResourceName(creature, frame));

        MirrorScareTimeline.ResourceName(ScareCreature.Grinner, 3).Should().Be("mirrorscare.grinner3.png");
    }
}
