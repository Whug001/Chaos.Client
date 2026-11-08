using Chaos.DarkAges.BrassRing;
using FluentAssertions;
using static Chaos.DarkAges.BrassRing.BrassRingRules;

namespace Chaos.Client.Tests.BrassRing;

public sealed class BrassRingFightTests
{
    private readonly List<BrassRingEvent> Events = [];

    private BrassRingFight Started()
    {
        var fight = new BrassRingFight();
        fight.Step(INTRO_MS, Events);
        Events.Clear();

        return fight;
    }

    private void Steps(BrassRingFight fight, int count)
    {
        for (var i = 0; i < count; i++)
            fight.Step(STEP_MS, Events);
    }

    private void Knockout(BrassRingFight fight, int winner)
    {
        fight.Fighters[1 - winner].Health = PUNCH_DAMAGE;
        fight.TryMove(winner, BrassRingMove.PunchLeft, Events);
        Steps(fight, 9);
    }

    [Test]
    public void Punch_lands_when_the_wind_up_ends()
    {
        var fight = Started();
        fight.TryMove(0, BrassRingMove.PunchLeft, Events).Should().BeTrue();

        Steps(fight, 8);
        fight.Fighters[1].Health.Should().Be(MAX_HEALTH);

        Steps(fight, 1);
        fight.Fighters[1].Health.Should().Be(MAX_HEALTH - PUNCH_DAMAGE);
        fight.Fighters[1].Pose.Should().Be(BrassRingPose.Stunned);
        fight.Fighters[0].Pose.Should().Be(BrassRingPose.Recover);
        Events.Should().Contain(e => e.Type == BrassRingEventType.Hit && e.Fighter == 1 && e.Damage == PUNCH_DAMAGE);
    }

    [Test]
    public void Block_takes_a_quarter_damage_and_drains_stamina()
    {
        var fight = Started();
        fight.TryMove(1, BrassRingMove.BlockStart, Events).Should().BeTrue();
        fight.TryMove(0, BrassRingMove.PunchRight, Events);
        Steps(fight, 9);

        fight.Fighters[1].Health.Should().Be(MAX_HEALTH - 2);
        fight.Fighters[1].Stamina.Should().BeLessThan(MAX_STAMINA - BLOCK_HIT_STAMINA);
        fight.Fighters[1].Pose.Should().Be(BrassRingPose.Block);
    }

    [Test]
    public void Correct_dodge_avoids_the_punch_and_opens_a_counter()
    {
        var fight = Started();
        fight.TryMove(0, BrassRingMove.PunchLeft, Events);
        Steps(fight, 6);
        fight.TryMove(1, BrassRingMove.DodgeLeft, Events).Should().BeTrue();
        Steps(fight, 3);

        fight.Fighters[1].Health.Should().Be(MAX_HEALTH);
        fight.Fighters[1].Pose.Should().Be(BrassRingPose.Idle);
        fight.Fighters[1].CounterMs.Should().Be(COUNTER_WINDOW_MS);
        Events.Should().Contain(e => e.Type == BrassRingEventType.Dodged && e.Fighter == 1);
    }

    [Test]
    public void Counter_winds_up_faster_and_does_double_damage()
    {
        var fight = Started();
        fight.TryMove(0, BrassRingMove.PunchLeft, Events);
        Steps(fight, 6);
        fight.TryMove(1, BrassRingMove.DodgeLeft, Events);
        Steps(fight, 3);

        fight.TryMove(1, BrassRingMove.PunchRight, Events).Should().BeTrue();
        fight.Fighters[1].PoseMs.Should().Be(COUNTER_WIND_UP_MS);
        Steps(fight, 4);

        fight.Fighters[0].Health.Should().Be(MAX_HEALTH - COUNTER_DAMAGE);
    }

    [Test]
    public void Wrong_side_dodge_takes_double_damage()
    {
        var fight = Started();
        fight.TryMove(0, BrassRingMove.PunchLeft, Events);
        Steps(fight, 6);
        fight.TryMove(1, BrassRingMove.DodgeRight, Events);
        Steps(fight, 3);

        fight.Fighters[1].Health.Should().Be(MAX_HEALTH - PUNCH_DAMAGE * 2);
    }

    [Test]
    public void Hit_during_dodge_recovery_takes_double_damage()
    {
        var fight = Started();
        fight.TryMove(1, BrassRingMove.DodgeLeft, Events);
        Steps(fight, 3);
        fight.TryMove(0, BrassRingMove.PunchLeft, Events);
        Steps(fight, 9);

        fight.Fighters[1].Health.Should().Be(MAX_HEALTH - PUNCH_DAMAGE * 2);
    }

    [Test]
    public void Moves_need_enough_stamina()
    {
        var fight = Started();
        fight.Fighters[0].StaminaMilli = (PUNCH_STAMINA - 1) * 1000;

        fight.TryMove(0, BrassRingMove.PunchLeft, Events).Should().BeFalse();
        fight.Fighters[0].Pose.Should().Be(BrassRingPose.Idle);
    }

    [Test]
    public void Block_drops_when_stamina_runs_out()
    {
        var fight = Started();
        fight.TryMove(1, BrassRingMove.BlockStart, Events);
        fight.Fighters[1].StaminaMilli = 100;
        Steps(fight, 1);

        fight.Fighters[1].Pose.Should().Be(BrassRingPose.Idle);
        fight.Fighters[1].StaminaMilli.Should().Be(0);
        Events.Should().Contain(e => e.Type == BrassRingEventType.BlockEnded && e.Fighter == 1);
    }

    [Test]
    public void Punches_landing_in_the_same_step_both_land()
    {
        var fight = Started();
        fight.TryMove(0, BrassRingMove.PunchLeft, Events);
        fight.TryMove(1, BrassRingMove.PunchRight, Events);
        Steps(fight, 9);

        fight.Fighters[0].Health.Should().Be(MAX_HEALTH - PUNCH_DAMAGE);
        fight.Fighters[1].Health.Should().Be(MAX_HEALTH - PUNCH_DAMAGE);
    }

    [Test]
    public void Landed_hit_stops_the_other_wind_up()
    {
        var fight = Started();
        fight.TryMove(0, BrassRingMove.PunchLeft, Events);
        Steps(fight, 2);
        fight.TryMove(1, BrassRingMove.PunchLeft, Events);
        Steps(fight, 7);

        fight.Fighters[1].Pose.Should().Be(BrassRingPose.Stunned);
        Steps(fight, 8);
        fight.Fighters[0].Health.Should().Be(MAX_HEALTH);
    }

    [Test]
    public void Double_knockout_is_a_drawn_round()
    {
        var fight = Started();
        fight.Fighters[0].Health = PUNCH_DAMAGE;
        fight.Fighters[1].Health = PUNCH_DAMAGE;
        fight.TryMove(0, BrassRingMove.PunchLeft, Events);
        fight.TryMove(1, BrassRingMove.PunchLeft, Events);
        Steps(fight, 9);

        fight.Phase.Should().Be(BrassRingPhase.Rest);
        fight.Fighters[0].RoundWins.Should().Be(0);
        fight.Fighters[1].RoundWins.Should().Be(0);
        Events.Should().Contain(e => e.Type == BrassRingEventType.RoundEnded && e.Fighter == NO_WINNER);
    }

    [Test]
    public void Time_limit_ends_the_round_once_for_the_healthier_fighter()
    {
        var fight = Started();
        fight.Fighters[0].Health = 50;
        fight.Fighters[1].Health = 40;
        fight.PhaseMs = STEP_MS;
        Steps(fight, 3);

        fight.Fighters[0].RoundWins.Should().Be(1);
        Events.Count(e => e.Type == BrassRingEventType.RoundEnded).Should().Be(1);
    }

    [Test]
    public void Equal_health_at_the_time_limit_is_a_draw()
    {
        var fight = Started();
        fight.PhaseMs = STEP_MS;
        Steps(fight, 1);

        fight.Fighters[0].RoundWins.Should().Be(0);
        fight.Fighters[1].RoundWins.Should().Be(0);
    }

    [Test]
    public void Two_round_wins_end_the_match()
    {
        var fight = Started();
        Knockout(fight, 0);
        fight.Step(REST_MS, Events);
        Knockout(fight, 0);

        fight.Phase.Should().Be(BrassRingPhase.Finished);
        fight.Winner.Should().Be(0);
        fight.Fighters[1].Pose.Should().Be(BrassRingPose.Down);
    }

    [Test]
    public void Three_drawn_rounds_draw_the_match()
    {
        var fight = Started();

        for (var round = 0; round < MAX_ROUNDS; round++)
        {
            fight.PhaseMs = STEP_MS;
            Steps(fight, 1);

            if (fight.Phase == BrassRingPhase.Rest)
                fight.Step(REST_MS, Events);
        }

        fight.Phase.Should().Be(BrassRingPhase.Finished);
        fight.Winner.Should().Be(NO_WINNER);
    }

    [Test]
    public void New_round_refills_health_and_stamina()
    {
        var fight = Started();
        Knockout(fight, 0);
        fight.Step(REST_MS, Events);

        fight.Round.Should().Be(2);
        fight.Fighters[1].Health.Should().Be(MAX_HEALTH);
        fight.Fighters[0].Stamina.Should().Be(MAX_STAMINA);
        fight.Fighters[1].Pose.Should().Be(BrassRingPose.Idle);
    }

    [Test]
    public void Moves_are_ignored_outside_a_round()
    {
        var fight = new BrassRingFight();
        fight.TryMove(0, BrassRingMove.PunchLeft, Events).Should().BeFalse();

        fight.Step(INTRO_MS, Events);
        Knockout(fight, 0);
        fight.Phase.Should().Be(BrassRingPhase.Rest);
        fight.TryMove(0, BrassRingMove.DodgeLeft, Events).Should().BeFalse();
    }

    [Test]
    public void Fighter_cannot_act_while_blocking_or_stunned()
    {
        var fight = Started();
        fight.TryMove(0, BrassRingMove.BlockStart, Events);

        fight.TryMove(0, BrassRingMove.PunchLeft, Events).Should().BeFalse();
        fight.TryMove(0, BrassRingMove.DodgeLeft, Events).Should().BeFalse();
    }
}
