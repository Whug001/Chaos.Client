using Chaos.Client.Controls.World.Popups.BrassRing;
using Chaos.DarkAges.BrassRing;
using FluentAssertions;
using static Chaos.DarkAges.BrassRing.BrassRingRules;

namespace Chaos.Client.Tests.BrassRing;

public sealed class BrassRingDummyTests
{
    private readonly List<BrassRingEvent> Events = [];

    private BrassRingFight Started()
    {
        var fight = new BrassRingFight();
        fight.Step(INTRO_MS, Events);

        return fight;
    }

    private static BrassRingDummy Dummy(params int[] rolls)
    {
        var index = 0;

        return new BrassRingDummy((_, _) => rolls[index++ % rolls.Length]);
    }

    [Test]
    public void Punches_when_the_wait_ends()
    {
        var fight = Started();
        var dummy = Dummy(1);

        dummy.Tick(BrassRingDummy.FIRST_WAIT_MS - STEP_MS, fight, 1).Should().BeNull();
        dummy.Tick(STEP_MS, fight, 1).Should().Be(BrassRingMove.PunchRight);
    }

    [Test]
    public void Dodges_the_correct_side_after_the_reaction_delay()
    {
        var fight = Started();
        var dummy = Dummy(0);
        fight.TryMove(0, BrassRingMove.PunchRight, Events);

        dummy.Tick(STEP_MS, fight, 1).Should().BeNull();
        dummy.Tick(BrassRingDummy.REACTION_MS, fight, 1).Should().Be(BrassRingMove.DodgeRight);
    }

    [Test]
    public void Does_not_react_when_the_roll_fails()
    {
        var fight = Started();
        var dummy = Dummy(99);
        fight.TryMove(0, BrassRingMove.PunchLeft, Events);

        dummy.Tick(STEP_MS, fight, 1).Should().BeNull();
        dummy.Tick(BrassRingDummy.REACTION_MS, fight, 1).Should().BeNull();
    }

    [Test]
    public void Reacts_only_once_to_a_punch()
    {
        var fight = Started();
        var dummy = Dummy(0);
        fight.TryMove(0, BrassRingMove.PunchLeft, Events);

        dummy.Tick(STEP_MS, fight, 1);
        dummy.Tick(BrassRingDummy.REACTION_MS, fight, 1).Should().Be(BrassRingMove.DodgeLeft);
        dummy.Tick(STEP_MS, fight, 1).Should().BeNull();
    }

    [Test]
    public void Punches_at_once_during_a_counter_window()
    {
        var fight = Started();
        var dummy = Dummy(1);
        fight.Fighters[1].CounterMs = COUNTER_WINDOW_MS;

        dummy.Tick(STEP_MS, fight, 1).Should().Be(BrassRingMove.PunchRight);
    }
}
